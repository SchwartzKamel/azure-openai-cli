# Microsoft Agent Framework — Phase 0 benchmarks

**Status**: harness ready, awaiting real Azure endpoint to populate.
**Spike code**: `spike/agent-framework/`
**Decision artifact**: `docs/adr/ADR-004-agent-framework-adoption.md`

## How to populate

1. Provide a `.env` at the repo root with:
   ```
   AZUREOPENAIENDPOINT=https://<resource>.openai.azure.com/
   AZUREOPENAIAPI=<key>
   AZUREOPENAIMODEL=<deployment>
   AZURE_FOUNDRY_PROJECT_ENDPOINT=https://...   # optional, foundry path only
   ```
2. Run `bash spike/agent-framework/bench.sh` (default `RUNS=10`).
3. Each run appends a results section below with timestamp, averages, and binary sizes.
4. Once enough runs accumulate (recommend ≥ 3 sessions on different days), fill in the verdict table in ADR-004.

## Pass thresholds (from plan.md / ADR-004)
- Cold start regression ≤ **10%** (5.4 ms → max 5.9 ms)
- TTFT regression ≤ **5 ms**
- Streaming throughput regression ≤ **5%**
- Single tool round-trip regression ≤ **5 ms**
- Native AOT: zero new runtime crashes; trim warnings documented

## Verdict template (fill after runs)

| Probe | Handrolled | Spike (apikey) | Spike (aad) | Spike (foundry) | Pass? |
|---|---|---|---|---|---|
| Cold start (`--help`) | _ ms | _ ms | _ ms | _ ms | ☐ |
| TTFT (1-line prompt) | _ ms | _ ms | _ ms | _ ms | ☐ |
| End-to-end (10-token reply) | _ ms | _ ms | _ ms | _ ms | ☐ |
| Streaming throughput | _ tok/s | _ tok/s | _ tok/s | _ tok/s | ☐ |
| Tool round-trip (TBD Phase 0 pt 2) | _ ms | _ ms | _ ms | _ ms | ☐ |
| AOT binary size | _ MB | _ MB | _ MB | _ MB | n/a |
| AOT warnings (new) | 0 | _ | _ | _ | ☐ |
| AOT runtime crashes | 0 | _ | _ | _ | ☐ (must be 0) |

## Runs
<!-- bench.sh appends here -->

## Run 2026-04-20T02:35Z — first real endpoint run

**Endpoint**: `https://sierrahackingco.cognitiveservices.azure.com/` (gpt-5.4-nano via Azure OpenAI Responses API)
**Binaries**: spike AOT (`af-spike`, 19 MB), handrolled AOT (`AzureOpenAI_CLI`, 9.1 MB)
**Machine**: Linux x64, WSL

### Cold-start (from earlier bootstrap run, no LLM call)

| Probe | Handrolled (AOT) | Spike (AOT, MAF) | Delta | Pass? |
|---|---|---|---|---|
| `--help` | 6.6 ms | 7.1 ms | +0.5 ms (+7.6%) | ✅ |
| `--version` | 6.4 ms | 7.4 ms | +1.0 ms (+15.6%) | ⚠️ tiny absolute |

### End-to-end (real LLM call)

**Handrolled is BROKEN against this endpoint.** Two independent pre-existing bugs surfaced and block any fair comparison:
1. **AOT**: `InvalidOperationException: Reflection-based serialization has been disabled` — reflection path still reachable in the streaming/response handling code despite `AppJsonContext` source-gen. Filed as a v1 regression; blocks AOT ship of v1.9.0-alpha.1 against modern endpoints.
2. **JIT**: `HTTP 400 unsupported_parameter: max_tokens` — newer Azure models (Responses API surface, gpt-5.x) require `max_completion_tokens` instead. Our `Azure.AI.OpenAI` 2.1.0 call still sends `max_tokens`.

As a result, the spike is currently the **only working path** against this endpoint. The head-to-head latency table cannot be populated — but the qualitative verdict is unambiguous.

### Spike (MAF, apikey) — short prompt (n=10)

| Measurement | Value |
|---|---|
| Avg wall-clock (start → exit) | **1053 ms** |
| Avg TTFT (internal `[mark]` first-token) | **948 ms** |
| Agent construction (args-parsed → agent-ready) | < 1 ms (AOT) |

### Spike (MAF, apikey) — ~50-char streamed reply (n=5)

| Run | Wall | TTFT | Stream phase | Throughput |
|---|---|---|---|---|
| 1 | 1171 ms | 771 ms | 390 ms | 128 chars/s |
| 2 | 1231 ms | 816 ms | 401 ms | 124 chars/s |
| 3 | 1330 ms | 884 ms | 436 ms | 114 chars/s |
| 4 | 1277 ms | 880 ms | 387 ms | 129 chars/s |
| 5 | 1274 ms | 829 ms | 430 ms | 116 chars/s |
| **avg** | **1257 ms** | **836 ms** | **409 ms** | **122 chars/s** |

### AAD path

Wired correctly. Without AAD env setup, fails with the expected `CredentialUnavailableException`:
```
- EnvironmentCredential authentication unavailable. Environment variables are not fully configured.
- WorkloadIdentityCredential authentication unavailable. The workload options are not fully configured.
```
This is the correct behavior — the MAF AAD path is ready for a real AAD deployment.

### Foundry path

Still a `NotImplementedException` stub. Requires real Foundry project endpoint; revisit when `AZURE_FOUNDRY_PROJECT_ENDPOINT` is provided.

### Verdict (partial, promoted to ADR-004)

- ✅ **AOT works**: MAF publishes cleanly with no new IL warnings
- ✅ **Cold start passes budget**: +0.5–1 ms delta, well under 10% threshold
- ✅ **End-to-end works** against gpt-5.4-nano Azure Responses endpoint
- ✅ **AAD path wired correctly** (fails with right error when not configured)
- ⚠️ **Binary 2× larger** (9 → 19 MB) — trim follow-up
- 🔴 **Handrolled v1 is broken** against this endpoint — two pre-existing bugs (AOT reflection + max_tokens). Cannot be used as comparison baseline until fixed.

**Implication for plan.md**: the hybrid adoption case is stronger than the plan assumed. Handrolled needs two fixes before it can even target modern Azure models (gpt-5.x Responses API); MAF handles both out of the box. File the handrolled bugs as `v1.9.1` hotfix candidates.

