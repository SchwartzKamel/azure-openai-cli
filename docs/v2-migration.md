# v2 Migration: Microsoft Agent Framework Adoption

**Living document. Updated as phases complete.**

Version: 0.1  
Last updated: 2026-04-20  
Status: Phases 0-4 complete, Phase 5 in flight, Phase 6-7 pending  
Decision: [ADR-004](adr/ADR-004-agent-framework-adoption.md)

---

## Why v2

The azure-openai-cli has grown from a single-shot prompt tool into a multi-agent autonomous system with tool-calling, persistent memory, self-correcting loops, and 25 named personas. Every new capability requires us to maintain hand-rolled implementations of primitives that Microsoft now ships as first-party framework components.

Microsoft consolidated Semantic Kernel and AutoGen into **Microsoft Agent Framework** (MAF) in April 2026. MAF provides enterprise-grade abstractions for chat agents (`ChatClientAgent`), streaming completions (`RunStreamingAsync`), workflow graphs with checkpointing (`Workflow` + `CheckpointManager`), persistent sessions (`AgentSession` + `AIContextProvider`), telemetry hooks (OpenTelemetry via `ILogger`), and multi-provider authentication (`Azure.Identity` integration). These directly replace ~2200 lines of custom harness code we maintain today in `Program.cs` and the squad subsystem.

Adopting MAF enables Azure AI Foundry integration with proper authentication, native support for persistent agents and MCP clients, and built-in observability. It reduces our maintenance burden without changing the CLI surface that Espanso and AutoHotkey users depend on. See [ADR-004](adr/ADR-004-agent-framework-adoption.md) for the full rationale and speed-gating criteria.

The v1 → v2 transition is structured as eight distinct phases with controlled risk and incremental delivery. Phase 0 benchmarks validated that MAF meets all performance thresholds and introduces zero regressions to the hot path (cold start, TTFT, streaming throughput). The v2 branch will remain isolated until Phase 6 cutover, preserving main as a stable base for v1.x hotfixes.

## Recent progress (2026-04-20)

| Commit | Phase | Milestone |
|--------|-------|-----------|
| `78b4fd5` | Phase 0 pt 2 | Tool round-trip benchmarking complete; Foundry endpoint integration validated |
| `ad613c7` | Phase 1 | v2 core CLI skeleton merged — hot-path contract (`--raw`, streaming) verified |
| `7af5b07` | Phases 2–4 | Tools port, Ralph workflow, and personas coordinated merge — all built-in tools migrated to AF function tools, Ralph loop ported to `Workflow` + `CheckpointManager`, squad personas wired to `AgentSession` |
| `32f7ce0` | Phase 3 | Ralph workflow integration — multi-iteration planning now uses MAF graph primitives |

**Next**: Phase 5 (observability + OTel cost hooks) in flight with Frank Costanza and Morty Seinfeld.

## Non-goals

What v2 will **not** change:

- **CLI flag surface** — all existing flags (`--raw`, `--agent`, `--ralph`, `--persona`, `--system`, etc.) remain unchanged
- **`--raw` contract** — clean stdout-only output preserved for Espanso/AHK
- **`.squad/` on-disk format** — persona memory files remain byte-identical (`AIContextProvider` reads/writes the same markdown format)
- **Espanso/AHK integration** — text expansion behavior is unaffected
- **Native AOT compilation** — binary size and cold-start latency are speed-gated (≤10% regression thresholds enforced)
- **Docker image name** — `ghcr.io/schwartzkamel/azure-openai-cli:latest` stays the same
- **Security hardening** — tool blocklists, SSRF protection, shell-injection defenses, and file-read restrictions migrate intact

Users upgrading from v1.9.x to v2.0.0 should see zero behavioral changes unless they explicitly opt into new MAF-powered features via `--agent-runtime af` (if that flag ships) or new authentication modes.

## Phase table

| Phase | Name | Status | Owner(s) | Key deliverable | SQL todo ID |
|-------|------|--------|----------|-----------------|-------------|
| 0 | AF + AOT spike | ✅ Done | Kramer | Benchmark MAF vs handrolled on hot path; validate AOT compatibility | `v2-spike` |
| 0 pt 2 | Tool round-trip + Foundry path | ✅ Done | Kramer | Wire AF function tool and re-bench; confirm Foundry endpoint integration | `v2-spike-pt2` |
| 1 | v2 core skeleton | ✅ Done | Kramer | New `azureopenai-cli-v2/` project; CLI entrypoint + standard/streaming/`--raw` preserve flag contract | `v2-core-skeleton` |
| 2 | Tools port | ✅ Done | Kramer + Newman | Migrate 6 tools to AF function tools; preserve Newman's hardening | `v2-tools-port` |
| 3 | Ralph workflow | ✅ Done | Kramer + Costanza | Port Ralph multi-iteration loop to AF `Workflow` graph; `CheckpointManager` for retries | `v2-ralph-workflow` |
| 4 | Personas | ✅ Done | Kramer + Jerry | Migrate squad personas to `AgentSession` + `AIContextProvider`; byte-identical `.squad/` format | `v2-personas` |
| 5 | Observability | 🟡 In progress | Frank + Morty | OTel integration + FinOps cost hook; opt-in telemetry flag | `v2-observability` |
| 6 | Cutover | ⏸️ Pending | Wilhelm + Lippman | Rename `v2` → `main`, `main` → `v1.x`; bump to 2.0.0; update CHANGELOG/CI/Docker | `v2-cutover` |
| 7 | Dogfood | ⏸️ Pending | FDR + Bania + all | User-supplied `.env` testing; recursive tool-use stress tests; cost benchmarks | `v2-dogfood` |

**Legend**: ✅ Done | 🟡 In progress | ⏸️ Pending | 🔴 Blocked

## Per-phase briefs

### Phase 0: AF + AOT spike ✅

**Goal**: Validate that MAF meets all speed thresholds and works with Native AOT.

**Inputs**: v1.9.0-alpha.1 baseline benchmarks, `Microsoft.Agents.AI.AzureAI` 1.0.0-rc5 package.

**Outputs**: Benchmark data in `docs/spikes/af-benchmarks.md`; ADR-004 acceptance decision.

**Owner**: Kramer.

**Risk**: AOT trim warnings or runtime crashes would have blocked adoption. Mitigated: Phase 0 passed all gates.

**Status**: ✅ Complete (2026-04-20). Cold start +7.6%, TTFT overhead <5ms, zero AOT crashes. Binary size 9MB → 19MB flagged for trim follow-up.

---

### Phase 0 pt 2: Tool round-trip + Foundry path ✅

**Goal**: Complete the benchmark matrix with tool invocation latency; confirm Foundry endpoint routing works or fails cleanly.

**Inputs**: Phase 0 validated hot path; real Foundry endpoint credentials staged in `.env`.

**Outputs**: Tool round-trip latency (≤5ms regression threshold); Foundry path either wired or documented as `NotImplementedException` stub with migration plan.

**Owner**: Kramer.

**Risk**: Foundry endpoint may have undocumented quirks (model-catalog format, api-version requirements) that require workarounds. If substantial, defer to Phase 4 and document as known limitation.

**Status**: ✅ Complete (2026-04-20, commit `78b4fd5`). Tool latency +2.1ms within threshold. Foundry endpoint routing validated end-to-end.

---

### Phase 1: v2 core skeleton ✅

**Goal**: Establish the v2 branch with a minimal CLI that preserves the hot-path contract (standard, streaming, `--raw`).

**Inputs**: Phase 0 pt 2 complete; MAF packages approved; v2 branch created from main.

**Outputs**: New `azureopenai-cli-v2/AzureOpenAI_CLI_V2.csproj` with `ChatClientAgent` replacing hand-rolled chat loop; `--raw`, `--system`, `--temperature`, `--max-tokens` flags work identically to v1; AOT build green; `make publish-aot` produces working binary. Zero tools, zero Ralph, zero personas at this phase.

**Owner**: Kramer.

**Risk**: MAF's streaming API may not expose line-by-line control needed for `--raw` clean stdout. Mitigation: Phase 0 already validated `RunStreamingAsync` compatibility; fallback is dual-runtime mode (`--agent-runtime native|af`).

**Status**: ✅ Complete (2026-04-20, commit `ad613c7`). Core skeleton merged to v2 branch. Hot-path contract validated.

---

### Phase 2: Tools port ✅

**Goal**: Migrate all 6 built-in tools to MAF function tools while preserving Newman's security hardening.

**Inputs**: Phase 1 complete (v2 CLI runs without tools); Newman's hardening code in `Tools/` namespace.

**Outputs**: `ShellExec`, `ReadFile`, `WebFetch`, `GetClipboard`, `GetDateTime`, `DelegateTask` implemented as AF function tools; Newman's denylist/allowlist/timeout/SSRF logic wraps each tool unchanged; `--agent` flag works; all `AzureOpenAI_CLI.Tests/Tools/` tests pass.

**Owner**: Kramer (porting) + Newman (security review).

**Risk**: MAF function tool interface may lack hooks for pre-call validation (denylist checks). Mitigation: wrap in a thin adapter layer that calls Newman's validators before invoking the actual tool.

**Status**: ✅ Complete (2026-04-20, commit `7af5b07`). All 6 tools migrated. Newman's security policies integrated.

---

### Phase 3: Ralph workflow ✅

**Goal**: Port Ralph's self-correcting autonomous loop to MAF's `Workflow` + `CheckpointManager` primitives.

**Inputs**: Phase 2 complete (tools work); existing Ralph logic in `Program.cs` (~1544 LOC of plan-act-validate-retry).

**Outputs**: `--ralph` flag triggers MAF workflow graph; `CheckpointManager` persists retry state; `--validate` script still invoked as exit condition; workflow halts on pass or max-iterations; same CLI output format as v1.

**Owner**: Kramer (workflow graph) + Costanza (UX validation).

**Risk**: `Workflow` API may not support custom validators or may enforce opinionated retry strategies. If blocked, implement as a custom workflow node or defer to post-v2.0 if non-critical.

**Status**: ✅ Complete (2026-04-20, commit `32f7ce0`). Ralph multi-iteration loop now uses MAF graph primitives.

---

### Phase 4: Personas ✅

**Goal**: Migrate squad personas to `AgentSession` + `AIContextProvider` while keeping `.squad/` files byte-identical.

**Inputs**: Phase 1 complete (v2 CLI foundation); existing `.squad/history/<name>.md` format; persona definitions in `.squad/config.json`.

**Outputs**: `--persona <name>` loads system prompt + memory via `AIContextProvider`; memory reads/writes to `.squad/history/<name>.md` unchanged; multi-turn sessions persist correctly; all persona-related tests pass.

**Owner**: Kramer (session wiring) + Jerry (modernization review).

**Risk**: `AIContextProvider` may expect a different persistence format. Mitigation: implement a custom provider that translates MAF's internal state to/from our markdown format.

**Status**: ✅ Complete (2026-04-20, commit `7af5b07` — coordinated merge with Phase 2). Squad personas wired to `AgentSession`.

---

### Phase 5: Observability 🟡

**Goal**: Add OpenTelemetry integration and FinOps cost tracking via MAF's telemetry hooks.

**Inputs**: Phases 1-4 complete (all features migrated); MAF's `ILogger` integration documented.

**Outputs**: `--telemetry` flag (opt-in) exports OTel spans to stdout or OTLP endpoint; cost hook logs input/output tokens + model ID per request; Morty-approved schema for cost analysis; zero telemetry when flag is off.

**Owner**: Frank Costanza (SRE/telemetry) + Morty (FinOps cost schema).

**Risk**: OTel package dependencies may bloat AOT binary. Mitigation: conditional compilation (`#if TELEMETRY_ENABLED`) or runtime feature flag to exclude OTel types from trim.

**Status**: 🟡 In progress (2026-04-20). Frank Costanza and Morty Seinfeld actively building OTel hooks and cost schema.

---

### Phase 6: Cutover ⏸️

**Goal**: Promote v2 branch to main and release as 2.0.0.

**Inputs**: Phases 1-5 complete; all tests green on v2 branch; dogfood period (Phase 7 partial) validates no regressions.

**Outputs**: `v2` branch renamed to `main`; old `main` becomes `v1.x` maintenance branch; CHANGELOG.md updated with breaking-change section (MAF dependency now public for library consumers); CI workflows updated; Docker image tags include `2.0.0` + `latest`; Homebrew/Scoop/Nix formulas updated (Bob Sacamano); GitHub Release with migration notes.

**Owner**: Wilhelm (change mgmt) + Mr. Lippman (release mgmt).

**Risk**: Unanticipated breaking changes discovered post-cutover. Mitigation: Phase 7 dogfood period must include at least 1 week of daily use by core contributors on real tasks; rollback plan documented (see below).

---

### Phase 7: Dogfood ⏸️

**Goal**: Stress-test v2 with real user workloads before public release.

**Inputs**: Phase 6 cutover complete; v2 on main.

**Outputs**: All 25 agents run successfully on v2; FDR's chaos tests pass (partial-stream failures, recursive tool loops, edge-case inputs); Bania's cost benchmarks confirm no regression vs v1.9.1; user-supplied `.env` files work (not just maintainer creds).

**Owner**: FDR (chaos eng) + Bania (benchmarks) + all agents (usage validation).

**Risk**: Low-probability edge cases only surface in production use. Mitigation: this phase is continuous — even after 2.0.0 ships, dogfood findings inform 2.0.1+ hotfixes.

---

## Cutover semantics

### Branch strategy

- **Pre-cutover**: Work happens on a `v2` branch forked from `main` after v1.9.1. The `main` branch remains stable for v1.x hotfixes.
- **At cutover (Phase 6)**: The `v2` branch is renamed to `main`. The old `main` is renamed to `v1.x` and enters maintenance mode (critical security fixes only).
- **Post-cutover**: All new work targets `main` (which is now v2). The `v1.x` branch receives hotfixes via cherry-pick if necessary but no new features.

### SemVer bump

v1.9.1 → **2.0.0** because MAF is a public dependency change. If external consumers exist (unlikely — this is primarily a CLI), they will see `Microsoft.Agents.AI.*` packages in the dependency graph. For CLI-only users, the change is behaviorally non-breaking (see Non-goals).

### Migration guide for users

**For most users**: Upgrade as you would any minor version. The CLI contract is preserved.

**For library consumers** (if any): Update project files to reference `Microsoft.Agents.AI.AzureAI` and related packages. Review ADR-004 for API surface changes if you were consuming internal classes.

**For Espanso/AHK users**: Zero action required. `--raw` output format is unchanged.

**For `.squad/` users**: Persona files remain compatible. No migration script needed.

## Rollback plan

If MAF adoption proves untenable:

- **Pre-cutover (Phases 1-5)**: Simply abandon the `v2` branch. No cutover means no rollback needed — `main` remains untouched.
- **Post-cutover (after Phase 6)**: Revert the branch rename: `main` → `v2-reverted`, `v1.x` → `main`. Publish v1.9.2 from the restored main. Document the decision in ADR-004 as "Rejected post-implementation" with rationale.

The cutover is designed to be reversible until Phase 7 completes and 2.0.0 ships to public release channels (Homebrew, GHCR, GitHub Releases).

## Open questions

As of 2026-04-20:

1. **Foundry model-catalog quirk**: Will MAF's Azure provider handle Foundry's non-standard deployment-name format natively, or do we need a custom `IModelCatalog` implementation?
2. **AOT binary size**: Phase 0 showed 9MB → 19MB growth. Can trim analysis reduce this to <15MB, or is the MAF package graph inherently larger?
3. **Persistence layer coexistence**: Does `AgentSession`'s built-in persistence replace `.squad/` files entirely, or do we maintain both? Preference: keep `.squad/` as the source of truth and treat `AgentSession` as ephemeral.
4. **AAD + Foundry auth**: Does `DefaultAzureCredential` work cleanly with Foundry endpoints, or do we need a custom token provider?
5. **Dual-runtime maintenance cost**: If `--agent-runtime native|af` ships in v2.0, how long do we maintain both paths? Tentative answer: retire the slower runtime at v2.1 if benchmarks clearly favor one.
6. **Tool hardening adapter**: Does MAF's function-tool interface provide pre-call hooks, or do we wrap every tool in a validator adapter?
7. **OTel package size**: Can we conditionally compile OTel dependencies to avoid AOT size bloat when telemetry is disabled?

These will be resolved during Phases 1-5 and documented in per-phase commit messages or follow-up ADRs.

## Glossary

**MAF (Microsoft Agent Framework)**: First-party agent orchestration framework from Microsoft, consolidating Semantic Kernel and AutoGen. Provides `ChatClientAgent`, `Workflow`, `AgentSession`, and related primitives. GA April 2026.

**AgentSession**: MAF's abstraction for a persistent multi-turn conversation. Manages message history, context, and state. Replaces our hand-rolled session logic in the squad subsystem.

**Workflow**: MAF's graph-based orchestration primitive. Nodes are actions (tool calls, LLM invocations, validators); edges define control flow. Replaces Ralph's imperative plan-act-validate-retry loop.

**AIContextProvider**: MAF interface for loading/saving agent memory from external storage. We implement this to read/write `.squad/history/<name>.md` files in their current format.

**PersistentAgentsClient**: MAF client for Azure AI Foundry's persistent agents API. Handles deployment-specific routing, model-catalog queries, and session continuity across invocations.

**Foundry**: Azure AI Foundry — Microsoft's managed AI service platform. Hosts models like Phi-4-mini-instruct at `services.ai.azure.com/models`. Requires different auth and endpoint routing than Azure OpenAI.

**Ralph**: Autonomous self-correcting agent mode (`--ralph`). Repeatedly invokes tools and validators until a success condition is met or max iterations is reached. Named after Ralph Cifaretto (plan, act, validate, retry).

**Squad**: The 25-agent persona system. Each agent has a system prompt, optional tools, and persistent memory in `.squad/history/<name>.md`. Defined in `.squad/config.json`. Examples: Kramer (C#/Azure), Newman (security), Elaine (docs).

**Hot path**: The critical latency path from CLI invocation to first token streamed. Includes arg parse, auth, API call, TLS handshake, and model response. Speed-gated in v2 adoption.

**Cold path**: Features that do not affect the hot path: multi-iteration loops (Ralph), telemetry export, AAD auth handshake, persona memory load. MAF is adopted freely on the cold path.

---

**Maintained by**: Elaine (docs), Wilhelm (change mgmt)  
**Next review**: After Phase 0 pt 2 completes (update benchmark results)  
**Questions/feedback**: File an issue or ping Costanza (product) / Kramer (engineering)
