# Phi-4-mini-instruct vs gpt-5.4-nano — Benchmark

> "It's gold, Jerry. *Gold.*" — Bania

- **Date:** 2026-04-20
- **Commit:** `180d64f`
- **Host:** `malachor` (Linux 6.8.0-106-generic)
- **Mode:** `full` (N_warm=10, N_throughput=5, cold-start=yes)
- **Raw responses:** `docs/benchmarks/raw/20260420-0749/`
- **Harness:** `scripts/bench-foundry.sh`

## Endpoints

| Model | URL (no key) |
|---|---|
| gpt-5.4-nano | `https://sierrahackingco.cognitiveservices.azure.com/openai/deployments/gpt-5.4-nano/chat/completions?api-version=2025-04-01-preview` |
| Phi-4-mini-instruct | `https://sierrahackingco.services.ai.azure.com/models/chat/completions?api-version=2024-05-01-preview` |

## Methodology

- Every endpoint receives ONE warm-up call (discarded) before timed runs, to factor out DNS/TLS handshake noise.
- TTFT is measured with `curl -w "%{time_starttransfer}"` — that is the canonical first-byte latency. For non-streaming chat completions (used here), TTFB ≈ full response time.
- Throughput is measured on `stream:true` responses: `chars_delta_total / (time_total − time_starttransfer)`.
- Cold-start: `sleep 60` then one call. Skipped in --quick mode.
- Instruction-following: 5 deterministic prompts at `temperature=0`, `max_tokens=128`. Match via Perl regex on the full response text.
- Strict JSON schema: `response_format.type="json_schema"` with `strict:true`. PASS requires parseable JSON containing both required keys.
- Function calling: single `get_weather(city)` tool. PASS requires `tool_calls[0].function.name == "get_weather"` AND parseable JSON args with `city`.
- Costs use published rates — input $0.20/$0.075 USD/1M, output $1.25/$0.300 USD/1M (src: `docs/cost-optimization.md §3`).
- N is always reported. Mean is accompanied by sample stdev. Percentiles are nearest-rank.

## 1. TTFT (non-streaming, "reply with the single word: pong")

| Model | N | mean | stdev | p50 | p95 |
|---|---:|---:|---:|---:|---:|
| gpt-5.4-nano | 10 | 1.142s | ±0.265s | 1.084s | 1.833s |
| Phi-4-mini-instruct | 10 | 0.617s | ±0.104s | 0.575s | 0.852s |

## 2. Streaming throughput (chars/sec, "Write a 100-word story about a cat.")

| Model | N | mean cps | stdev | p50 | p95 |
|---|---:|---:|---:|---:|---:|
| gpt-5.4-nano | 5 | 428.2 | ±115.7 | 455.4 | 556.5 |
| Phi-4-mini-instruct | 5 | 157.4 | ±88.1 | 198.7 | 199.2 |

## 3. Cold-start TTFT (sleep 60s → 1 call)

| Model | TTFT |
|---|---|
| gpt-5.4-nano | 1.140477 |
| Phi-4-mini-instruct | 0.784114 |

## 4. Instruction-following pass rate

Prompts (N=5) — temperature=0, max_tokens=128:

- **P1:** `reply with the single word: pong` → regex `^pong\W*$` (flags: `i`)
- **P2:** `respond with only a JSON object like {"ok":true}` → regex `^\s*\{\s*"ok"\s*:\s*true\s*\}\s*$` (flags: `none`)
- **P3:** `count from 1 to 3, comma-separated, no words` → regex `^\s*1\s*,\s*2\s*,\s*3\s*$` (flags: `none`)
- **P4:** `fix this typo: 'teh cat sat'. Output ONLY the corrected sentence.` → regex `^the cat sat\W*$` (flags: `i`)
- **P5:** `classify as positive or negative, one word: 'I love it'` → regex `^positive\W*$` (flags: `i`)

### gpt-5.4-nano — 5/5 PASS

| # | Result | Response (truncated 160c) | Latency | tokens in/out |
|---|---|---|---:|---:|
| 1 | PASS | `pong` | 1.233034s | 13/5 |
| 2 | PASS | `{"ok":true}` | 1.048699s | 18/9 |
| 3 | PASS | `1,2,3` | 1.049081s | 19/9 |
| 4 | PASS | `The cat sat.` | 1.173784s | 22/8 |
| 5 | PASS | `Positive` | 0.901260s | 21/5 |

### Phi-4-mini-instruct — 4/5 PASS

| # | Result | Response (truncated 160c) | Latency | tokens in/out |
|---|---|---|---:|---:|
| 1 | PASS | `pong` | 0.875481s | 10/2 |
| 2 | FAIL | ````json {"ok":true}```` | 0.732833s | 15/10 |
| 3 | PASS | `1, 2, 3` | 0.896374s | 16/8 |
| 4 | PASS | `the cat sat.` | 0.652851s | 19/5 |
| 5 | PASS | `Positive` | 0.527349s | 18/2 |

## 5. Strict JSON schema (response_format.json_schema, strict=true)

| Model | Result | Notes |
|---|---|---|
| gpt-5.4-nano | PASS | {"country":"France","capital":"Paris"} |
| Phi-4-mini-instruct | REJECTED | HTTP 422|invalid input error  |

## 6. Function calling (`get_weather(city)` tool)

| Model | Result | Notes |
|---|---|---|
| gpt-5.4-nano | PASS | name=get_weather args={"city":"Paris"} |
| Phi-4-mini-instruct | FAIL | no valid tool_call (content='<|tool_call|> {   ') |

## 7. Cost per call (TTFT prompt, avg over N=10)

| Model | avg in tok | avg out tok | avg cost (¢USD) | rate in | rate out |
|---|---:|---:|---:|---:|---:|
| gpt-5.4-nano | 13.00 | 5.00 | ¢0.000885 | $0.20/1M | $1.25/1M |
| Phi-4-mini-instruct | 10.00 | 2.00 | ¢0.000135 | $0.075/1M | $0.300/1M |

## Stability observations

During this run, Phi streaming had 1 of 5 throughput calls hang past the 90s curl timeout (recorded as 0 cps in the table above — deliberately not discarded, so the table remains honest). gpt-5.4-nano had 0 of 5. This is a material reliability signal separate from headline throughput — the Foundry serverless stream has a non-trivial hang rate at low load.

## Verdict

- **TTFT winner:** Phi-4-mini-instruct (mean: 1.1420s vs 0.6174s)
- **Throughput winner:** gpt-5.4-nano (mean cps: 428.1880 vs 157.4320)
- **Cost winner:** Phi-4-mini-instruct (avg gpt ¢0.000885 vs phi ¢0.000135 per typical Espanso call)
- **Instruction-following:** gpt-5.4-nano 5/5, Phi-4-mini-instruct 4/5
- **Strict JSON schema:** gpt=PASS, phi=REJECTED
- **Function calling:** gpt=PASS, phi=FAIL

### Recommendation for Morty

Numbers above. Decision gates:

1. If Phi instruction-following rate < 80%, **do not** flip Espanso default to Phi — users type a snippet and expect the literal text, not a paraphrase. Cost savings do not justify regressions in core UX.
2. If Phi passes strict JSON schema AND function calling, it is viable for structured automation; otherwise keep gpt-5.4-nano on any path that emits tool calls or enforced JSON.
3. Cost delta per call is ¢0.0008 (gpt minus phi) — multiply by your expected call volume before over-indexing on it.

*Methodology footnote: all numbers captured with N and stdev. Regex-based instruction-following is strict by design ("follow the literal instruction"); nothing is graded on intent.*
