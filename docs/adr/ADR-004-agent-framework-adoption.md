# ADR-004 — Speed-gated hybrid adoption of Microsoft Agent Framework

**Status**: 📋 Proposed (Phase 0 spike pending — see `spike/agent-framework/`)
**Date**: TBD (will be set when Phase 0 benchmarks complete)
**Deciders**: Costanza (product), Kramer (engineering), Newman (security), Bania (benchmarks), Maestro (orchestration)
**Supersedes**: none
**Related**: ADR-001 (Native AOT), ADR-002 (Squad personas), ADR-003 (BDD)

## Context

Microsoft consolidated Semantic Kernel + AutoGen into **Microsoft Agent Framework** (`Microsoft.Agents.AI`), GA April 2026. It offers first-party primitives that overlap our hand-rolled implementations:

| Primitive | Hand-rolled (today) | MAF equivalent |
|---|---|---|
| Chat client | `Azure.AI.OpenAI` 2.1.0 raw | `ChatClientAgent` |
| Agent loop (tools) | `Program.cs` ~630 LOC | implicit when tools registered |
| Streaming | `CompleteChatStreamingAsync` | `RunStreamingAsync` |
| Multi-iter (Ralph) | `Program.cs` ~1544 LOC | `Workflow` + `CheckpointManager` |
| Persona memory | `.squad/history/*.md` files | `AIContextProvider` |
| Telemetry | none | first-party OpenTelemetry |
| Multi-provider auth | api-key only | `Azure.Identity` + provider abstraction |
| MCP client | none | `McpClientToolProvider` |

Continuing to maintain our own harness for *every* primitive is technical debt. But az-ai's primary use case is **Espanso/AHK text injection**, where every millisecond of cold-start and time-to-first-token (TTFT) is user-visible. The current AOT build delivers **9 MB / 5.4 ms cold start** — which we cannot regress.

## Decision

**Adopt Microsoft Agent Framework as a speed-gated hybrid**, not a wholesale rewrite.

1. The **hot path** (CLI parse → auth → first chat call → first token → streaming → single-tool round-trip) stays hand-rolled **unless MAF beats hand-rolled in benchmarks** (Phase 0).
2. **Cold-path** components (Ralph multi-iteration, telemetry export, MCP, persona memory load/save, AAD/Foundry auth handshake) **adopt MAF freely** — they don't touch the default Espanso path.
3. A `--agent-runtime {native|af}` flag lets users opt into the MAF runtime per invocation; the hand-rolled runtime remains reachable through v2.x at minimum.
4. Multi-auth (`--auth {apikey|aad|foundry}`) ships standalone as **v1.10.0** independent of the MAF refactor, because adopting `Azure.Identity` is a near-zero hot-path cost when api-key remains the default.

## Pass thresholds (set before spike runs to avoid bias)

For MAF to displace hand-rolled on the hot path:
- Cold start regression ≤ **10%** (5.4 ms → max 5.9 ms)
- TTFT regression ≤ **5 ms**
- Streaming throughput regression ≤ **5%**
- Single tool round-trip regression ≤ **5 ms**
- Native AOT: **zero new runtime crashes**; trim warnings documented and quantified

If MAF fails any of these on the hot path, the hot path stays hand-rolled. Cold-path adoption proceeds regardless.

## Spike scope (Phase 0)

Code lives in `spike/agent-framework/`. Throwaway. Not in main test suite. Compares:
- 3 auth paths: `apikey`, `aad`, `foundry`
- vs current handrolled `az-ai` AOT binary
- on identical prompts, same Azure model, same endpoint

Bench harness: `spike/agent-framework/bench.sh` writes to `docs/spikes/af-benchmarks.md`.

## Consequences

### Positive
- **Speed preserved**: zero risk to Espanso users by gating hot-path adoption on benchmarks.
- **Free leverage**: cold-path features (Ralph workflow checkpointing, OTel, MCP, AAD/Foundry auth, AIContextProvider) come from a Microsoft-maintained framework instead of our custom code.
- **Multi-auth ships fast**: v1.10.0 carries the enterprise win without waiting for the MAF decision.
- **Reversible**: `--agent-runtime=native` always works.
- **Hardening intact**: Newman's tool hardening (env scrub, blocklists, SSRF, curl restrictions, safety clause) wraps as AF function tools; the security code itself never ports.

### Negative
- **Two runtimes to maintain** during transition. Mitigated by retiring the slower one (whichever loses) at v2.x+1 if benchmarks confirm a clear winner.
- **AOT build matrix complexity**: MAF packages may carry trim/AOT warnings. Mitigated by Phase 0 trip-wire and documented exception list.
- **Spike work** is throwaway if AOT fails. Cost: ~400 LOC + bench harness.

### Neutral
- Persona on-disk format (`.squad/history/<name>.md`) is **byte-identical** before and after — `AIContextProvider` reads/writes the existing files.
- CLI flag contract is **fully preserved** for Espanso users.

## Open questions (resolved during Phase 0)

1. Does `Microsoft.Agents.AI.OpenAI` 1.1.0 publish AOT clean against .NET 10? → benchmark
2. What is the actual `Microsoft.Agents.AI.AzureAI` 1.0.0-rc5 surface for Foundry persistent agents? → confirm against real endpoint
3. Does AAD `DefaultAzureCredential` add measurable cold-start cost when **not** the chosen path? → benchmark with apikey default
4. How does MAF's `RunStreamingAsync` compare to raw `CompleteChatStreamingAsync` in token latency? → bench

## References
- [Microsoft Agent Framework overview](https://learn.microsoft.com/en-us/agent-framework/overview/)
- [`microsoft/agent-framework` GitHub](https://github.com/microsoft/agent-framework)
- `plan.md` (session-state) — Phase 0/1/2 details
- ADR-001 — Native AOT recommendation (constraint we must preserve)
