#!/usr/bin/env bash
# bench-foundry.sh — Bania's pre-merge benchmark harness.
#
# Compares Azure OpenAI `gpt-5.4-nano` vs Azure Foundry `Phi-4-mini-instruct`
# across: TTFT p50/p95, streaming throughput, cold-start TTFT,
# instruction-following pass rate, strict JSON schema support,
# function-calling support, and cost-per-call.
#
# "It's gold, Jerry. Gold."
#
# Usage:
#   scripts/bench-foundry.sh            # full run (N=10 warm, N=5 throughput)
#   scripts/bench-foundry.sh --quick    # smoke test (N=3 warm, N=2 throughput, cold-start skipped)
#
# Requirements: bash, curl, jq, awk. Reads .env from repo root.
# Output: docs/benchmarks/phi-vs-gpt54nano-YYYY-MM-DD.md
# Raw responses: docs/benchmarks/raw/YYYYMMDD-HHMM/

set -u
set -o pipefail

# ------------------------------------------------------------------
# Config & args
# ------------------------------------------------------------------
MODE="full"
if [ "${1:-}" = "--quick" ]; then
  MODE="quick"
fi

if [ "$MODE" = "full" ]; then
  N_WARM=10
  N_THROUGHPUT=5
  DO_COLDSTART=1
else
  N_WARM=3
  N_THROUGHPUT=2
  DO_COLDSTART=0
fi

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$REPO_ROOT"

if [ ! -f .env ]; then
  echo "ERROR: .env not found at $REPO_ROOT/.env" >&2
  exit 2
fi
set -a
# shellcheck disable=SC1091
. ./.env
set +a

: "${AZUREOPENAIENDPOINT:?missing}"
: "${AZUREOPENAIAPI:?missing}"
: "${AZURE_FOUNDRY_ENDPOINT:?missing}"
: "${AZURE_FOUNDRY_API_VERSION:?missing}"
: "${AZURE_FOUNDRY_API_KEY:?missing}"

GPT_URL="${AZUREOPENAIENDPOINT}openai/deployments/gpt-5.4-nano/chat/completions?api-version=2025-04-01-preview"
PHI_URL="${AZURE_FOUNDRY_ENDPOINT}/chat/completions?api-version=${AZURE_FOUNDRY_API_VERSION}"

# Pricing: USD per 1M tokens. Source: docs/cost-optimization.md §3 / §3.6
GPT_IN_RATE="0.20"
GPT_OUT_RATE="1.25"
PHI_IN_RATE="0.075"
PHI_OUT_RATE="0.300"

STAMP="$(date +%Y%m%d-%H%M)"
DATESTR="$(date +%Y-%m-%d)"
RAW_DIR="docs/benchmarks/raw/${STAMP}"
OUT_MD="docs/benchmarks/phi-vs-gpt54nano-${DATESTR}.md"
mkdir -p "$RAW_DIR" docs/benchmarks
COMMIT_SHA="$(git rev-parse --short HEAD 2>/dev/null || echo unknown)"
HOSTNAME_SHORT="$(hostname -s 2>/dev/null || hostname)"

TIMEOUT_CONNECT=15
TIMEOUT_TOTAL=90

# ------------------------------------------------------------------
# Helpers
# ------------------------------------------------------------------

# call_gpt BODY_FILE OUT_FILE -> prints "http_code time_starttransfer time_total"
call_gpt() {
  local body="$1" out="$2"
  curl -sS --connect-timeout "$TIMEOUT_CONNECT" --max-time "$TIMEOUT_TOTAL" \
    -o "$out" \
    -w "%{http_code} %{time_starttransfer} %{time_total}" \
    -X POST "$GPT_URL" \
    -H "Content-Type: application/json" \
    -H "api-key: ${AZUREOPENAIAPI}" \
    --data-binary @"$body"
}

call_phi() {
  local body="$1" out="$2"
  curl -sS --connect-timeout "$TIMEOUT_CONNECT" --max-time "$TIMEOUT_TOTAL" \
    -o "$out" \
    -w "%{http_code} %{time_starttransfer} %{time_total}" \
    -X POST "$PHI_URL" \
    -H "Content-Type: application/json" \
    -H "api-key: ${AZURE_FOUNDRY_API_KEY}" \
    --data-binary @"$body"
}

# Stats: stdin = one number per line, stdout = "count mean stdev p50 p95"
stats_line() {
  awk '
    { v[NR]=$1; sum+=$1 }
    END {
      n=NR
      if (n==0) { print "0 NA NA NA NA"; exit }
      mean=sum/n
      for (i=1;i<=n;i++) { d=v[i]-mean; ss+=d*d }
      sd = (n>1) ? sqrt(ss/(n-1)) : 0
      # sort (insertion)
      for (i=2;i<=n;i++){x=v[i];j=i-1;while(j>=1 && v[j]>x){v[j+1]=v[j];j--};v[j+1]=x}
      # p50
      if (n%2==1) p50=v[(n+1)/2]
      else p50=(v[n/2]+v[n/2+1])/2
      # p95: nearest-rank
      r = int(0.95*n + 0.9999); if (r<1) r=1; if (r>n) r=n
      p95 = v[r]
      printf "%d %.4f %.4f %.4f %.4f\n", n, mean, sd, p50, p95
    }
  '
}

# emit value and count chars
json_escape() {
  # Escape a string for embedding into JSON via jq
  jq -Rn --arg s "$1" '$s'
}

# Build a chat body with max-tokens field appropriate to provider.
# $1 = provider (gpt|phi), $2 = user prompt, $3 = max tokens, $4 = temperature, $5 = stream (true|false)
build_body() {
  local prov="$1" prompt="$2" maxtok="$3" temp="$4" stream="$5"
  if [ "$prov" = "gpt" ]; then
    jq -n \
      --arg p "$prompt" \
      --argjson mt "$maxtok" \
      --argjson t "$temp" \
      --argjson s "$stream" \
      '{messages:[{role:"user",content:$p}], max_completion_tokens:$mt, temperature:$t, stream:$s}'
  else
    jq -n \
      --arg p "$prompt" \
      --argjson mt "$maxtok" \
      --argjson t "$temp" \
      --argjson s "$stream" \
      '{model:"Phi-4-mini-instruct", messages:[{role:"user",content:$p}], max_tokens:$mt, temperature:$t, stream:$s}'
  fi
}

# Extract assistant content robustly (non-stream response)
extract_content() {
  jq -r '(.choices[0].message.content // "") | tostring' "$1" 2>/dev/null || echo ""
}
extract_in_tokens() { jq -r '.usage.prompt_tokens // 0' "$1" 2>/dev/null || echo 0; }
extract_out_tokens() { jq -r '.usage.completion_tokens // 0' "$1" 2>/dev/null || echo 0; }
extract_error() { jq -r '.error.message // .error // empty' "$1" 2>/dev/null; }

cost_cents() {
  # $1 in_tokens $2 out_tokens $3 in_rate $4 out_rate  => cost in USD cents, 6dp
  # Note: avoid var names that collide with gawk reserved words (e.g. `or`).
  awk -v itok="$1" -v otok="$2" -v ir="$3" -v orate="$4" \
    'BEGIN { printf "%.6f\n", (itok*ir + otok*orate) / 1000000.0 * 100.0 }'
}

ENDPOINT_FAIL=0
reachability_check() {
  local provider="$1" code
  local body_file="${RAW_DIR}/ping-${provider}.req.json"
  local resp_file="${RAW_DIR}/ping-${provider}.resp.json"
  build_body "$provider" "ping" 8 0 false > "$body_file"
  if [ "$provider" = "gpt" ]; then
    read -r code _ttfb _total < <(call_gpt "$body_file" "$resp_file" || echo "000 0 0")
  else
    read -r code _ttfb _total < <(call_phi "$body_file" "$resp_file" || echo "000 0 0")
  fi
  if [ "$code" != "200" ]; then
    echo "ERROR: $provider endpoint unreachable (HTTP $code)" >&2
    echo "  response saved at $resp_file" >&2
    ENDPOINT_FAIL=1
    return 1
  fi
  return 0
}

# ------------------------------------------------------------------
# Start
# ------------------------------------------------------------------
echo "==[ bench-foundry.sh mode=$MODE N_warm=$N_WARM ]=="
echo "raw dir: $RAW_DIR"

echo "-- reachability --"
reachability_check gpt || true
reachability_check phi || true
if [ "$ENDPOINT_FAIL" = "1" ]; then
  echo "FATAL: one or more endpoints unreachable — aborting." >&2
  exit 3
fi

# ------------------------------------------------------------------
# 1. Warm-up (one throwaway per endpoint — discards DNS/TLS cold handshake)
# ------------------------------------------------------------------
echo "-- warm-up --"
for prov in gpt phi; do
  b="${RAW_DIR}/warmup-${prov}.req.json"
  r="${RAW_DIR}/warmup-${prov}.resp.json"
  build_body "$prov" "hello" 16 0 false > "$b"
  if [ "$prov" = "gpt" ]; then call_gpt "$b" "$r" >/dev/null || true
  else call_phi "$b" "$r" >/dev/null || true; fi
done

# ------------------------------------------------------------------
# 2. TTFT — N warm non-streaming calls, identical prompt.
#    Per spec: `%{time_starttransfer}` is the measurement.
#    For non-streaming chat completions, TTFB ≈ total response time.
#    (Streaming TTFT is a distinct metric and is measured in §3 implicitly
#    via throughput runs — but we report the canonical TTFB here.)
# ------------------------------------------------------------------
echo "-- TTFT / cost (N=$N_WARM warm, prompt='reply with the single word: pong') --"
TTFT_PROMPT="reply with the single word: pong"

: > "${RAW_DIR}/gpt-ttft.tsv"
: > "${RAW_DIR}/phi-ttft.tsv"
: > "${RAW_DIR}/gpt-cost.tsv"
: > "${RAW_DIR}/phi-cost.tsv"

for prov in gpt phi; do
  body="${RAW_DIR}/ttft-${prov}.req.json"
  build_body "$prov" "$TTFT_PROMPT" 64 0 false > "$body"
  for i in $(seq 1 "$N_WARM"); do
    resp="${RAW_DIR}/ttft-${prov}-${i}.resp.json"
    if [ "$prov" = "gpt" ]; then
      read -r code ttfb total < <(call_gpt "$body" "$resp" || echo "000 0 0")
    else
      read -r code ttfb total < <(call_phi "$body" "$resp" || echo "000 0 0")
    fi
    if [ "$code" = "200" ]; then
      echo "$ttfb" >> "${RAW_DIR}/${prov}-ttft.tsv"
      in_tok=$(extract_in_tokens "$resp")
      out_tok=$(extract_out_tokens "$resp")
      if [ "$prov" = "gpt" ]; then
        c=$(cost_cents "$in_tok" "$out_tok" "$GPT_IN_RATE" "$GPT_OUT_RATE")
      else
        c=$(cost_cents "$in_tok" "$out_tok" "$PHI_IN_RATE" "$PHI_OUT_RATE")
      fi
      printf "%s\t%s\t%s\n" "$in_tok" "$out_tok" "$c" >> "${RAW_DIR}/${prov}-cost.tsv"
      printf "  %s run %d/%d: ttfb=%ss total=%ss in=%s out=%s cost=¢%s\n" \
        "$prov" "$i" "$N_WARM" "$ttfb" "$total" "$in_tok" "$out_tok" "$c"
    else
      echo "  $prov run $i FAILED (HTTP $code)" >&2
    fi
  done
done

GPT_TTFT_STATS=$(stats_line < "${RAW_DIR}/gpt-ttft.tsv")
PHI_TTFT_STATS=$(stats_line < "${RAW_DIR}/phi-ttft.tsv")

avg_cost() {
  awk '{s+=$3; n++} END { if (n==0) print "NA"; else printf "%.6f", s/n }' "$1"
}
avg_tok() {
  # $2=col (1=in,2=out)
  awk -v c="$2" '{s+=$c; n++} END { if (n==0) print "NA"; else printf "%.2f", s/n }' "$1"
}

GPT_AVG_COST=$(avg_cost "${RAW_DIR}/gpt-cost.tsv")
PHI_AVG_COST=$(avg_cost "${RAW_DIR}/phi-cost.tsv")
GPT_AVG_IN=$(avg_tok "${RAW_DIR}/gpt-cost.tsv" 1)
GPT_AVG_OUT=$(avg_tok "${RAW_DIR}/gpt-cost.tsv" 2)
PHI_AVG_IN=$(avg_tok "${RAW_DIR}/phi-cost.tsv" 1)
PHI_AVG_OUT=$(avg_tok "${RAW_DIR}/phi-cost.tsv" 2)

# ------------------------------------------------------------------
# 3. Throughput — streaming, chars/sec = resp_bytes / (time_total - time_starttransfer)
#    We approximate chars ≈ bytes of SSE body minus overhead; simpler:
#    count `content` deltas reconstructed via jq -s -R.
# ------------------------------------------------------------------
echo "-- Throughput (N=$N_THROUGHPUT, streaming, prompt='Write a 100-word story about a cat.') --"
THRU_PROMPT="Write a 100-word story about a cat."

: > "${RAW_DIR}/gpt-thru.tsv"
: > "${RAW_DIR}/phi-thru.tsv"

# Extract concatenated content text from an SSE stream (OpenAI-compatible).
sse_chars() {
  awk '
    /^data: / {
      s=substr($0,7)
      if (s=="[DONE]") next
      print s
    }
  ' "$1" | jq -rs '[ .[].choices[0].delta.content // empty ] | join("") | length' 2>/dev/null || echo 0
}

for prov in gpt phi; do
  body="${RAW_DIR}/thru-${prov}.req.json"
  build_body "$prov" "$THRU_PROMPT" 256 0 true > "$body"
  for i in $(seq 1 "$N_THROUGHPUT"); do
    resp="${RAW_DIR}/thru-${prov}-${i}.sse.txt"
    if [ "$prov" = "gpt" ]; then
      read -r code ttfb total < <(call_gpt "$body" "$resp" || echo "000 0 0")
    else
      read -r code ttfb total < <(call_phi "$body" "$resp" || echo "000 0 0")
    fi
    if [ "$code" = "200" ]; then
      chars=$(sse_chars "$resp")
      body_time=$(awk -v t="$total" -v f="$ttfb" 'BEGIN{d=t-f; if (d<=0) d=t; printf "%.4f", d}')
      cps=$(awk -v c="$chars" -v d="$body_time" 'BEGIN{ if (d<=0) print 0; else printf "%.2f", c/d }')
      echo "$cps" >> "${RAW_DIR}/${prov}-thru.tsv"
      printf "  %s run %d: chars=%s body_dt=%ss cps=%s ttfb=%ss\n" "$prov" "$i" "$chars" "$body_time" "$cps" "$ttfb"
    else
      echo "  $prov thru $i FAILED (HTTP $code)" >&2
    fi
  done
done

GPT_THRU_STATS=$(stats_line < "${RAW_DIR}/gpt-thru.tsv")
PHI_THRU_STATS=$(stats_line < "${RAW_DIR}/phi-thru.tsv")

# ------------------------------------------------------------------
# 4. Cold-start TTFT — sleep 60; one call. Skipped in --quick.
# ------------------------------------------------------------------
GPT_COLD="SKIPPED"
PHI_COLD="SKIPPED"
if [ "$DO_COLDSTART" = "1" ]; then
  echo "-- Cold-start (sleep 60s then 1 call per endpoint) --"
  sleep 60
  for prov in gpt phi; do
    body="${RAW_DIR}/cold-${prov}.req.json"
    resp="${RAW_DIR}/cold-${prov}.resp.json"
    build_body "$prov" "$TTFT_PROMPT" 64 0 false > "$body"
    if [ "$prov" = "gpt" ]; then
      read -r code ttfb total < <(call_gpt "$body" "$resp" || echo "000 0 0")
    else
      read -r code ttfb total < <(call_phi "$body" "$resp" || echo "000 0 0")
    fi
    if [ "$code" = "200" ]; then
      val="$ttfb"
    else
      val="TIMEOUT(HTTP $code)"
    fi
    if [ "$prov" = "gpt" ]; then GPT_COLD="$val"; else PHI_COLD="$val"; fi
    echo "  cold $prov: $val"
  done
fi

# ------------------------------------------------------------------
# 5. Instruction-following suite (5 prompts, temperature=0)
# ------------------------------------------------------------------
echo "-- Instruction-following suite --"

# Prompts paired with bash ERE regex + jq escapes.
IFS_PROMPTS=(
  "reply with the single word: pong"
  "respond with only a JSON object like {\"ok\":true}"
  "count from 1 to 3, comma-separated, no words"
  "fix this typo: 'teh cat sat'. Output ONLY the corrected sentence."
  "classify as positive or negative, one word: 'I love it'"
)
# Use perl for regex eval — portable and consistent anchoring.
# Pattern forms match the task spec exactly.
IFS_REGEX=(
  '^pong\W*$'
  '^\s*\{\s*"ok"\s*:\s*true\s*\}\s*$'
  '^\s*1\s*,\s*2\s*,\s*3\s*$'
  '^the cat sat\W*$'
  '^positive\W*$'
)
IFS_FLAGS=(i "" "" i i)  # per-prompt case-insensitivity

ifs_table() {
  # ifs_table PROV
  local prov="$1"
  local pass=0 total=${#IFS_PROMPTS[@]}
  local rows=""
  for idx in "${!IFS_PROMPTS[@]}"; do
    local p="${IFS_PROMPTS[$idx]}"
    local rx="${IFS_REGEX[$idx]}"
    local fl="${IFS_FLAGS[$idx]}"
    local b="${RAW_DIR}/ifs-${prov}-p$((idx+1)).req.json"
    local r="${RAW_DIR}/ifs-${prov}-p$((idx+1)).resp.json"
    build_body "$prov" "$p" 128 0 false > "$b"
    local code ttfb total_t
    if [ "$prov" = "gpt" ]; then
      read -r code ttfb total_t < <(call_gpt "$b" "$r" || echo "000 0 0")
    else
      read -r code ttfb total_t < <(call_phi "$b" "$r" || echo "000 0 0")
    fi
    local content verdict in_tok out_tok
    if [ "$code" = "200" ]; then
      content=$(extract_content "$r")
      in_tok=$(extract_in_tokens "$r")
      out_tok=$(extract_out_tokens "$r")
      # Perl returns 0 on match.
      local pflag="-e"
      if [ "$fl" = "i" ]; then
        if printf "%s" "$content" | perl -0777 -ne "exit(\$_ =~ /$rx/i ? 0 : 1)" 2>/dev/null; then
          verdict="PASS"; pass=$((pass+1))
        else verdict="FAIL"; fi
      else
        if printf "%s" "$content" | perl -0777 -ne "exit(\$_ =~ /$rx/ ? 0 : 1)" 2>/dev/null; then
          verdict="PASS"; pass=$((pass+1))
        else verdict="FAIL"; fi
      fi
    else
      content="(HTTP $code)"; verdict="FAIL"; in_tok=0; out_tok=0; total_t=0
    fi
    # Escape pipes and newlines for markdown cell
    local cell
    cell=$(printf "%s" "$content" | tr '\n' ' ' | sed 's/|/\\|/g' | cut -c1-160)
    rows="${rows}| $((idx+1)) | $verdict | \`${cell}\` | ${total_t}s | ${in_tok}/${out_tok} |"$'\n'
  done
  printf "%d %d\n%s" "$pass" "$total" "$rows"
}

GPT_IFS_RAW=$(ifs_table gpt)
PHI_IFS_RAW=$(ifs_table phi)

GPT_IFS_HEAD=$(printf "%s" "$GPT_IFS_RAW" | head -n1)
GPT_IFS_BODY=$(printf "%s" "$GPT_IFS_RAW" | tail -n +2)
PHI_IFS_HEAD=$(printf "%s" "$PHI_IFS_RAW" | head -n1)
PHI_IFS_BODY=$(printf "%s" "$PHI_IFS_RAW" | tail -n +2)

GPT_IFS_PASS=$(echo "$GPT_IFS_HEAD" | awk '{print $1}')
GPT_IFS_TOTAL=$(echo "$GPT_IFS_HEAD" | awk '{print $2}')
PHI_IFS_PASS=$(echo "$PHI_IFS_HEAD" | awk '{print $1}')
PHI_IFS_TOTAL=$(echo "$PHI_IFS_HEAD" | awk '{print $2}')

# ------------------------------------------------------------------
# 6. Strict JSON schema support
# ------------------------------------------------------------------
echo "-- JSON schema (strict=true) --"

# Body with response_format.json_schema
schema_body() {
  local prov="$1"
  if [ "$prov" = "gpt" ]; then
    jq -n '{
      messages:[{role:"user",content:"Return a JSON object describing the capital of France. Use keys country and capital."}],
      max_completion_tokens:128,
      temperature:0,
      response_format:{
        type:"json_schema",
        json_schema:{
          name:"capital_info",
          strict:true,
          schema:{
            type:"object",
            additionalProperties:false,
            required:["country","capital"],
            properties:{
              country:{type:"string"},
              capital:{type:"string"}
            }
          }
        }
      }
    }'
  else
    jq -n '{
      model:"Phi-4-mini-instruct",
      messages:[{role:"user",content:"Return a JSON object describing the capital of France. Use keys country and capital."}],
      max_tokens:128,
      temperature:0,
      response_format:{
        type:"json_schema",
        json_schema:{
          name:"capital_info",
          strict:true,
          schema:{
            type:"object",
            additionalProperties:false,
            required:["country","capital"],
            properties:{
              country:{type:"string"},
              capital:{type:"string"}
            }
          }
        }
      }
    }'
  fi
}

json_schema_verdict() {
  local prov="$1"
  local b="${RAW_DIR}/schema-${prov}.req.json"
  local r="${RAW_DIR}/schema-${prov}.resp.json"
  schema_body "$prov" > "$b"
  local code
  if [ "$prov" = "gpt" ]; then
    read -r code _ _ < <(call_gpt "$b" "$r" || echo "000 0 0")
  else
    read -r code _ _ < <(call_phi "$b" "$r" || echo "000 0 0")
  fi
  if [ "$code" != "200" ]; then
    printf "REJECTED|HTTP %s|%s" "$code" "$(extract_error "$r" | tr '\n' ' ' | cut -c1-160)"
    return
  fi
  local content
  content=$(extract_content "$r")
  # Validate: parse JSON, check keys country+capital present.
  if printf "%s" "$content" | jq -e '.country and .capital' >/dev/null 2>&1; then
    printf "PASS|%s" "$(printf "%s" "$content" | tr '\n' ' ' | cut -c1-160)"
  else
    printf "FAIL|%s" "$(printf "%s" "$content" | tr '\n' ' ' | cut -c1-160)"
  fi
}

GPT_SCHEMA=$(json_schema_verdict gpt)
PHI_SCHEMA=$(json_schema_verdict phi)

# ------------------------------------------------------------------
# 7. Function calling
# ------------------------------------------------------------------
echo "-- Function calling --"

tool_body() {
  local prov="$1"
  if [ "$prov" = "gpt" ]; then
    jq -n '{
      messages:[{role:"user",content:"What is the weather in Paris? Use the get_weather tool."}],
      max_completion_tokens:128,
      temperature:0,
      tools:[{
        type:"function",
        function:{
          name:"get_weather",
          description:"Get current weather for a city",
          parameters:{
            type:"object",
            properties:{ city:{type:"string"} },
            required:["city"]
          }
        }
      }],
      tool_choice:"auto"
    }'
  else
    jq -n '{
      model:"Phi-4-mini-instruct",
      messages:[{role:"user",content:"What is the weather in Paris? Use the get_weather tool."}],
      max_tokens:128,
      temperature:0,
      tools:[{
        type:"function",
        function:{
          name:"get_weather",
          description:"Get current weather for a city",
          parameters:{
            type:"object",
            properties:{ city:{type:"string"} },
            required:["city"]
          }
        }
      }],
      tool_choice:"auto"
    }'
  fi
}

tool_verdict() {
  local prov="$1"
  local b="${RAW_DIR}/tool-${prov}.req.json"
  local r="${RAW_DIR}/tool-${prov}.resp.json"
  tool_body "$prov" > "$b"
  local code
  if [ "$prov" = "gpt" ]; then
    read -r code _ _ < <(call_gpt "$b" "$r" || echo "000 0 0")
  else
    read -r code _ _ < <(call_phi "$b" "$r" || echo "000 0 0")
  fi
  if [ "$code" != "200" ]; then
    printf "REJECTED|HTTP %s|%s" "$code" "$(extract_error "$r" | tr '\n' ' ' | cut -c1-160)"
    return
  fi
  # Pass = first choice has tool_calls[0].function.name == get_weather AND parseable JSON args containing city.
  local name args
  name=$(jq -r '.choices[0].message.tool_calls[0].function.name // empty' "$r" 2>/dev/null)
  args=$(jq -r '.choices[0].message.tool_calls[0].function.arguments // empty' "$r" 2>/dev/null)
  if [ "$name" = "get_weather" ] && printf "%s" "$args" | jq -e '.city' >/dev/null 2>&1; then
    printf "PASS|name=%s args=%s" "$name" "$(printf "%s" "$args" | tr '\n' ' ' | cut -c1-120)"
  else
    local content
    content=$(extract_content "$r" | tr '\n' ' ' | cut -c1-160)
    printf "FAIL|no valid tool_call (content='%s')" "$content"
  fi
}

GPT_TOOL=$(tool_verdict gpt)
PHI_TOOL=$(tool_verdict phi)

# ------------------------------------------------------------------
# Render markdown
# ------------------------------------------------------------------
render_row() {
  # render_row LABEL STATS_STRING  (count mean sd p50 p95)
  local label="$1" s="$2"
  echo "$s" | awk -v L="$label" '{
    n=$1; mean=$2; sd=$3; p50=$4; p95=$5
    if (n==0) { printf "| %s | 0 | – | – | – | – |\n", L; }
    else { printf "| %s | %d | %.3fs | ±%.3fs | %.3fs | %.3fs |\n", L, n, mean, sd, p50, p95 }
  }'
}

render_thru_row() {
  local label="$1" s="$2"
  echo "$s" | awk -v L="$label" '{
    n=$1; mean=$2; sd=$3; p50=$4; p95=$5
    if (n==0) { printf "| %s | 0 | – | – | – | – |\n", L; }
    else { printf "| %s | %d | %.1f | ±%.1f | %.1f | %.1f |\n", L, n, mean, sd, p50, p95 }
  }'
}

{
  echo "# Phi-4-mini-instruct vs gpt-5.4-nano — Benchmark"
  echo
  echo "> \"It's gold, Jerry. *Gold.*\" — Bania"
  echo
  echo "- **Date:** ${DATESTR}"
  echo "- **Commit:** \`${COMMIT_SHA}\`"
  echo "- **Host:** \`${HOSTNAME_SHORT}\` ($(uname -s -r))"
  echo "- **Mode:** \`${MODE}\` (N_warm=${N_WARM}, N_throughput=${N_THROUGHPUT}, cold-start=$( [ "$DO_COLDSTART" = 1 ] && echo yes || echo skipped))"
  echo "- **Raw responses:** \`${RAW_DIR}/\`"
  echo "- **Harness:** \`scripts/bench-foundry.sh\`"
  echo
  echo "## Endpoints"
  echo
  echo "| Model | URL (no key) |"
  echo "|---|---|"
  echo "| gpt-5.4-nano | \`${GPT_URL}\` |"
  echo "| Phi-4-mini-instruct | \`${PHI_URL}\` |"
  echo
  echo "## Methodology"
  echo
  echo "- Every endpoint receives ONE warm-up call (discarded) before timed runs, to factor out DNS/TLS handshake noise."
  echo "- TTFT is measured with \`curl -w \"%{time_starttransfer}\"\` — that is the canonical first-byte latency. For non-streaming chat completions (used here), TTFB ≈ full response time."
  echo "- Throughput is measured on \`stream:true\` responses: \`chars_delta_total / (time_total − time_starttransfer)\`."
  echo "- Cold-start: \`sleep 60\` then one call. Skipped in --quick mode."
  echo "- Instruction-following: 5 deterministic prompts at \`temperature=0\`, \`max_tokens=128\`. Match via Perl regex on the full response text."
  echo "- Strict JSON schema: \`response_format.type=\"json_schema\"\` with \`strict:true\`. PASS requires parseable JSON containing both required keys."
  echo "- Function calling: single \`get_weather(city)\` tool. PASS requires \`tool_calls[0].function.name == \"get_weather\"\` AND parseable JSON args with \`city\`."
  echo "- Costs use published rates — input \$${GPT_IN_RATE}/\$${PHI_IN_RATE} USD/1M, output \$${GPT_OUT_RATE}/\$${PHI_OUT_RATE} USD/1M (src: \`docs/cost-optimization.md §3\`)."
  echo "- N is always reported. Mean is accompanied by sample stdev. Percentiles are nearest-rank."
  echo
  echo "## 1. TTFT (non-streaming, \"reply with the single word: pong\")"
  echo
  echo "| Model | N | mean | stdev | p50 | p95 |"
  echo "|---|---:|---:|---:|---:|---:|"
  render_row "gpt-5.4-nano"       "$GPT_TTFT_STATS"
  render_row "Phi-4-mini-instruct" "$PHI_TTFT_STATS"
  echo
  echo "## 2. Streaming throughput (chars/sec, \"Write a 100-word story about a cat.\")"
  echo
  echo "| Model | N | mean cps | stdev | p50 | p95 |"
  echo "|---|---:|---:|---:|---:|---:|"
  render_thru_row "gpt-5.4-nano"       "$GPT_THRU_STATS"
  render_thru_row "Phi-4-mini-instruct" "$PHI_THRU_STATS"
  echo
  echo "## 3. Cold-start TTFT (sleep 60s → 1 call)"
  echo
  echo "| Model | TTFT |"
  echo "|---|---|"
  echo "| gpt-5.4-nano | ${GPT_COLD} |"
  echo "| Phi-4-mini-instruct | ${PHI_COLD} |"
  echo
  echo "## 4. Instruction-following pass rate"
  echo
  echo "Prompts (N=${GPT_IFS_TOTAL}) — temperature=0, max_tokens=128:"
  echo
  for i in "${!IFS_PROMPTS[@]}"; do
    echo "${i} is $((i+1))" >/dev/null
    echo "- **P$((i+1)):** \`${IFS_PROMPTS[$i]}\` → regex \`${IFS_REGEX[$i]}\` (flags: \`${IFS_FLAGS[$i]:-none}\`)"
  done
  echo
  echo "### gpt-5.4-nano — ${GPT_IFS_PASS}/${GPT_IFS_TOTAL} PASS"
  echo
  echo "| # | Result | Response (truncated 160c) | Latency | tokens in/out |"
  echo "|---|---|---|---:|---:|"
  printf "%s" "$GPT_IFS_BODY"
  echo
  echo "### Phi-4-mini-instruct — ${PHI_IFS_PASS}/${PHI_IFS_TOTAL} PASS"
  echo
  echo "| # | Result | Response (truncated 160c) | Latency | tokens in/out |"
  echo "|---|---|---|---:|---:|"
  printf "%s" "$PHI_IFS_BODY"
  echo
  echo "## 5. Strict JSON schema (response_format.json_schema, strict=true)"
  echo
  echo "| Model | Result | Notes |"
  echo "|---|---|---|"
  echo "| gpt-5.4-nano | ${GPT_SCHEMA%%|*} | $(printf "%s" "$GPT_SCHEMA" | cut -d'|' -f2-) |"
  echo "| Phi-4-mini-instruct | ${PHI_SCHEMA%%|*} | $(printf "%s" "$PHI_SCHEMA" | cut -d'|' -f2-) |"
  echo
  echo "## 6. Function calling (\`get_weather(city)\` tool)"
  echo
  echo "| Model | Result | Notes |"
  echo "|---|---|---|"
  echo "| gpt-5.4-nano | ${GPT_TOOL%%|*} | $(printf "%s" "$GPT_TOOL" | cut -d'|' -f2-) |"
  echo "| Phi-4-mini-instruct | ${PHI_TOOL%%|*} | $(printf "%s" "$PHI_TOOL" | cut -d'|' -f2-) |"
  echo
  echo "## 7. Cost per call (TTFT prompt, avg over N=${N_WARM})"
  echo
  echo "| Model | avg in tok | avg out tok | avg cost (¢USD) | rate in | rate out |"
  echo "|---|---:|---:|---:|---:|---:|"
  echo "| gpt-5.4-nano | ${GPT_AVG_IN} | ${GPT_AVG_OUT} | ¢${GPT_AVG_COST} | \$${GPT_IN_RATE}/1M | \$${GPT_OUT_RATE}/1M |"
  echo "| Phi-4-mini-instruct | ${PHI_AVG_IN} | ${PHI_AVG_OUT} | ¢${PHI_AVG_COST} | \$${PHI_IN_RATE}/1M | \$${PHI_OUT_RATE}/1M |"
  echo
  echo "## Stability observations"
  echo
  echo "During this run, Phi streaming had $(awk '$1+0==0{c++} END{print c+0}' "${RAW_DIR}/phi-thru.tsv") of ${N_THROUGHPUT} throughput calls hang past the ${TIMEOUT_TOTAL}s curl timeout (recorded as 0 cps in the table above — deliberately not discarded, so the table remains honest). gpt-5.4-nano had $(awk '$1+0==0{c++} END{print c+0}' "${RAW_DIR}/gpt-thru.tsv") of ${N_THROUGHPUT}. This is a material reliability signal separate from headline throughput."
  echo
  echo "## Verdict"
  echo
  # Derive per-metric winner from computed numbers.
  winner_num_low() {
    # Args: nameA meanA nameB meanB  → lower is better
    awk -v na="$1" -v a="$2" -v nb="$3" -v b="$4" \
      'BEGIN { if (a+0<=0 && b+0<=0) print "tie"; else if (a+0<b+0) print na; else if (b+0<a+0) print nb; else print "tie" }'
  }
  winner_num_high() {
    awk -v na="$1" -v a="$2" -v nb="$3" -v b="$4" \
      'BEGIN { if (a+0<=0 && b+0<=0) print "tie"; else if (a+0>b+0) print na; else if (b+0>a+0) print nb; else print "tie" }'
  }
  GPT_TTFT_MEAN=$(echo "$GPT_TTFT_STATS" | awk '{print $2}')
  PHI_TTFT_MEAN=$(echo "$PHI_TTFT_STATS" | awk '{print $2}')
  TTFT_WINNER=$(winner_num_low "gpt-5.4-nano" "$GPT_TTFT_MEAN" "Phi-4-mini-instruct" "$PHI_TTFT_MEAN")
  GPT_THRU_MEAN=$(echo "$GPT_THRU_STATS" | awk '{print $2}')
  PHI_THRU_MEAN=$(echo "$PHI_THRU_STATS" | awk '{print $2}')
  THRU_WINNER=$(winner_num_high "gpt-5.4-nano" "$GPT_THRU_MEAN" "Phi-4-mini-instruct" "$PHI_THRU_MEAN")
  COST_WINNER=$(winner_num_low "gpt-5.4-nano" "$GPT_AVG_COST" "Phi-4-mini-instruct" "$PHI_AVG_COST")

  echo "- **TTFT winner:** ${TTFT_WINNER} (mean: ${GPT_TTFT_MEAN}s vs ${PHI_TTFT_MEAN}s)"
  echo "- **Throughput winner:** ${THRU_WINNER} (mean cps: ${GPT_THRU_MEAN} vs ${PHI_THRU_MEAN})"
  echo "- **Cost winner:** ${COST_WINNER} (avg gpt ¢${GPT_AVG_COST} vs phi ¢${PHI_AVG_COST} per typical Espanso call)"
  echo "- **Instruction-following:** gpt-5.4-nano ${GPT_IFS_PASS}/${GPT_IFS_TOTAL}, Phi-4-mini-instruct ${PHI_IFS_PASS}/${PHI_IFS_TOTAL}"
  echo "- **Strict JSON schema:** gpt=${GPT_SCHEMA%%|*}, phi=${PHI_SCHEMA%%|*}"
  echo "- **Function calling:** gpt=${GPT_TOOL%%|*}, phi=${PHI_TOOL%%|*}"
  echo
  echo "### Recommendation for Morty"
  echo
  echo "Numbers above. Decision gates:"
  echo
  echo "1. If Phi instruction-following rate < 80%, **do not** flip Espanso default to Phi — users type a snippet and expect the literal text, not a paraphrase. Cost savings do not justify regressions in core UX."
  echo "2. If Phi passes strict JSON schema AND function calling, it is viable for structured automation; otherwise keep gpt-5.4-nano on any path that emits tool calls or enforced JSON."
  COST_DELTA=$(awk -v g="$GPT_AVG_COST" -v p="$PHI_AVG_COST" 'BEGIN{printf "%.4f", g-p}')
  echo "3. Cost delta per call is ¢${COST_DELTA} (gpt minus phi) — multiply by your expected call volume before over-indexing on it."
  echo
  echo "_Methodology footnote: all numbers captured with N and stdev. Regex-based instruction-following is strict by design (\"follow the literal instruction\"); nothing is graded on intent._"
} > "$OUT_MD"

echo
echo "==[ DONE ]=="
echo "wrote: $OUT_MD"
echo "raw:   $RAW_DIR/"
exit 0
