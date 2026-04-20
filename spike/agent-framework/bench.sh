#!/usr/bin/env bash
# Phase 0 benchmark harness — Microsoft Agent Framework vs hand-rolled az-ai.
#
# Measures (per auth path):
#   - cold-start (process exit time for --version-style probe)
#   - TTFT (time-to-first-token, measured via [mark] stderr lines)
#   - end-to-end latency (start → complete)
#   - binary size (AOT)
#
# Pass thresholds (plan.md Phase 0):
#   cold-start regression   ≤ 10%
#   TTFT regression         ≤ 5 ms
#   tool round-trip         ≤ 5 ms (deferred to Phase 0 part 2)
#
# Requires: real Azure endpoint via .env (AZUREOPENAIENDPOINT / AZUREOPENAIAPI / AZUREOPENAIMODEL)
# Output: docs/spikes/af-benchmarks.md (appends a run section)

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SPIKE_DIR="$REPO_ROOT/spike/agent-framework"
HANDROLLED="$REPO_ROOT/azureopenai-cli"
RESULTS="$REPO_ROOT/docs/spikes/af-benchmarks.md"
RUNS=${RUNS:-10}
PROMPT=${PROMPT:-"reply with the single word: pong"}

export PATH="$HOME/.dotnet:$PATH"

if [[ ! -f "$REPO_ROOT/.env" ]]; then
  echo "❌  no .env at $REPO_ROOT — provide AZUREOPENAIENDPOINT/AZUREOPENAIAPI/AZUREOPENAIMODEL"
  exit 1
fi

echo "▶ Building handrolled (AOT)…"
dotnet publish "$HANDROLLED" -c Release -r linux-x64 \
  -p:PublishAot=true -p:StripSymbols=true --nologo -v quiet >/dev/null

HANDROLLED_BIN=$(find "$HANDROLLED/bin/Release" -type f -name 'AzureOpenAI_CLI' -o -name 'az-ai' | head -1)
HANDROLLED_SIZE=$(stat -c %s "$HANDROLLED_BIN")

echo "▶ Building spike (AOT)…"
dotnet publish "$SPIKE_DIR" -c Release -r linux-x64 \
  -p:PublishAot=true -p:StripSymbols=true --nologo -v quiet >/dev/null

SPIKE_BIN="$SPIKE_DIR/bin/Release/net10.0/linux-x64/publish/af-spike"
SPIKE_SIZE=$(stat -c %s "$SPIKE_BIN")

bench_one () {
  local label=$1
  local cmd=$2
  local total_ms=0
  local ttft_total_ns=0
  local ttft_count=0

  # Warm-up
  eval "$cmd" >/dev/null 2>&1 || true

  for i in $(seq 1 "$RUNS"); do
    local start_ns=$(date +%s%N)
    local stderr_capture
    stderr_capture=$(eval "$cmd" 2>&1 >/dev/null) || true
    local end_ns=$(date +%s%N)
    local dur_ms=$(( (end_ns - start_ns) / 1000000 ))
    total_ms=$(( total_ms + dur_ms ))

    local ft_ns
    ft_ns=$(echo "$stderr_capture" | awk '/\[mark\] first-token/ {print $3}' | tr -d 'ns' | head -1)
    if [[ -n "$ft_ns" ]]; then
      ttft_total_ns=$(( ttft_total_ns + ft_ns ))
      ttft_count=$(( ttft_count + 1 ))
    fi
  done

  local avg_total_ms=$(( total_ms / RUNS ))
  local avg_ttft_ms="n/a"
  if [[ $ttft_count -gt 0 ]]; then
    avg_ttft_ms=$(( ttft_total_ns / ttft_count / 1000000 ))
  fi

  echo "$label: avg total=${avg_total_ms}ms  avg TTFT=${avg_ttft_ms}ms  (n=$RUNS)"
}

echo
echo "▶ Cold-start probe (no LLM call):"
bench_one "  handrolled --help    " "$HANDROLLED_BIN --help"
bench_one "  spike       --help   " "$SPIKE_BIN --help"

echo
echo "▶ Real prompt (one streaming response):"
bench_one "  handrolled (apikey)  " "echo '$PROMPT' | $HANDROLLED_BIN --raw"
bench_one "  spike     (apikey)   " "$SPIKE_BIN --auth apikey --prompt '$PROMPT'"
bench_one "  spike     (aad)      " "$SPIKE_BIN --auth aad    --prompt '$PROMPT' || true"

echo
echo "▶ Binary sizes:"
echo "  handrolled: $(numfmt --to=iec-i --suffix=B "$HANDROLLED_SIZE")"
echo "  spike     : $(numfmt --to=iec-i --suffix=B "$SPIKE_SIZE")"

mkdir -p "$(dirname "$RESULTS")"
{
  echo
  echo "## Run $(date -u +%Y-%m-%dT%H:%M:%SZ) — n=$RUNS"
  echo
  echo "| Probe | Avg total (ms) | Avg TTFT (ms) |"
  echo "|---|---|---|"
  echo "| (results manually appended; see stdout above) |  |  |"
  echo
  echo "Binary sizes: handrolled=$(numfmt --to=iec-i --suffix=B "$HANDROLLED_SIZE"), spike=$(numfmt --to=iec-i --suffix=B "$SPIKE_SIZE")"
} >> "$RESULTS"
echo
echo "▶ Appended to $RESULTS"
