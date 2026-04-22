# Feature Requests -- Index & Verified Status

> Last updated: 2026-04-24 -- After v1.9.1 release, FR-014 design landed, FR-003/009/010 superseded.
>
> This document is the **single source of truth** for FR status. Each row in the priority matrix reflects verified code state; the Status Details and v2.0.0 cutover-blocker sections below are evidence-based (grep + view against `azureopenai-cli/` (v1) and `azureopenai-cli-v2/` (v2)).

> **v2 migration in progress**: See [v2-migration.md](../v2-migration.md) for the Microsoft Agent Framework transition plan (v1 → v2.0). Feature requests below are v1.x scope unless noted.

---

## Priority Matrix

| ID | Title | Priority | Effort | Status | Blocks / Blocked by |
|----|-------|----------|--------|--------|---------------------|
| [FR-001](FR-001-stdin-pipe-context-injection.md) | Stdin Pipe & Context Injection | P0 | S | ✅ SHIPPED v1 (v1.1.0, `Program.cs:578,584`). ⚠️ v2 PARTIAL -- no combined mode, **no buffer cap (regression)** | Blocks v2.0.0 cutover (security) |
| [FR-002](FR-002-interactive-chat-mode.md) | Interactive Chat Mode (REPL) | P0 | M | 📋 PLANNED -- v1.3 / v2.1 | Soft dep on FR-014 profiles |
| [FR-003](FR-003-local-user-preferences.md) | Local User Preferences & Config Command | P1 | S-M | 🪦 **SUPERSEDED by [FR-014](FR-014-local-preferences-and-multi-provider.md)** -- Phase 1 (env vars + inline flags) shipped v1.1.0; Phase 2 subsumed into FR-014 | -- |
| [FR-004](FR-004-latency-and-startup-optimization.md) | Latency & Startup Optimization | P0 | Phased | ✅ LARGELY SHIPPED -- Spinner (v1.1.0) + AOT (v1.8.0); daemon (Phase 2b) + distribution (Phase 3) deferred | -- |
| [FR-005](FR-005-shell-integration-and-output-intelligence.md) | Shell Integration & Output Intelligence | P1 | M | 🔄 PARTIAL -- `--json`, `--raw` shipped; `--code` / `--shell` / markdown pending | -- |
| [FR-006](FR-006-unblock-native-aot-compilation.md) | Unblock Native AOT Compilation | P0 | S | ✅ SHIPPED v1.8.0 -- ~5.4 ms cold start at ship; current v2.0.6: 10.7 ms p50 ([baseline](../perf/v2.0.5-baseline.md)) | Unblocked FR-008, FR-011 |
| [FR-007](FR-007-parallel-startup-and-connection-prewarming.md) | Parallel Startup & Connection Pre-warming | P1 | S | 📋 PLANNED -- **quick win, 3-5 h, 200-300 ms TTFT** | Independent |
| [FR-008](FR-008-prompt-response-cache.md) | Prompt Response Cache | P1 | M | 📋 PLANNED -- Espanso/AHK use case | -- |
| [FR-009](FR-009-config-set-and-directory-overrides.md) | `--config set` Commands & Per-Directory Overrides | P1 | M | 🪦 **SUPERSEDED by [FR-014](FR-014-local-preferences-and-multi-provider.md)** -- directory overrides subsumed into FR-014 precedence chain | -- |
| [FR-010](FR-010-model-aliases-and-smart-defaults.md) | Model Aliases & Smart Defaults | P2 | S | 🪦 **SUPERSEDED by [FR-014](FR-014-local-preferences-and-multi-provider.md)** -- aliases + env-var normalization subsumed into FR-014 schema | -- |
| [FR-011](FR-011-agent-streaming-output.md) | Agent Mode Streaming Output | P0 | S-M | ✓ INFRA PRESENT -- v1 streams via `CompleteChatStreamingAsync` (`Program.cs:1491`); per-round stderr status + `--raw`/`--json` compat polish outstanding | -- |
| [FR-012](FR-012-plugin-tool-registry.md) | Plugin/Tool Registry System | P2 | M-L | 📝 DRAFT -- v2.1+ | Blocked by FR-014 |
| [FR-013](FR-013-mcp-client-and-server-support.md) | MCP Client and Server Support | P1 | L | 📝 DRAFT -- largest distribution-vector gap; v2.1 / v2.2 | -- |
| [FR-014](FR-014-local-preferences-and-multi-provider.md) | Local Preferences + Multi-Provider Profiles | P1 | M | 📝 DESIGN (Costanza, 2026-04-24) -- **critical path**; absorbs FR-003/009/010 | Blocks FR-018, FR-019, FR-020 |
| [FR-015](FR-015-pattern-library-and-cost-estimator.md) | Curated Pattern Library + Pre-Flight Cost Estimator | P2 | S-M | ⚠️ INFRA PARTIAL -- rate card in `azureopenai-cli-v2/Observability/CostHook.cs:25`; `--estimate` / `az-ai budget` UX unshipped | Independent |
| [FR-016](FR-016-aot-reflection-regression-hotfix.md) | AOT Reflection Regression Hotfix | P0 | S | ✅ SHIPPED v1.9.1 -- archive | -- |
| [FR-017](FR-017-max-completion-tokens-compatibility.md) | `max_completion_tokens` Compatibility | P0 | S | ✅ v1 SHIPPED v1.9.1 (`Program.cs:728,1693`). ❌ **v2 NOT PORTED -- P0 v2.0.0 blocker** | Blocks v2.0.0 cutover |
| [FR-018](FR-018-local-model-provider-llamacpp.md) | Local-Model Provider (llama.cpp / Ollama) | P2 | M | 📝 DRAFT -- v2.1 | Blocked by FR-014; blocks FR-019, FR-020 |
| [FR-019](FR-019-gemma-cpp-direct-adapter.md) | gemma.cpp Direct Adapter | P3 | M | 📝 DRAFT -- niche | Blocked by FR-018 |
| [FR-020](FR-020-nvidia-nim-provider-per-trigger-routing.md) | NVIDIA NIM Provider with Per-Trigger Routing (2B-first) | P2 | M | 📝 DRAFT -- implements ADR-006 | Blocked by FR-018 |

Legend: ✅ shipped · 🔄 partial · 📋 planned · 📝 draft · 🪦 superseded · ⚠️ regression or partial-infra

---

## P0 -- v2.0.0 cutover blockers

These must close before v2 can replace v1 as the shipping target.

1. **SEC-REGRESSION-1 -- Unbounded stdin in v2** (`azureopenai-cli-v2/Program.cs:181`)
   ```csharp
   prompt = await Console.In.ReadToEndAsync(cts.Token);
   ```
   v1 fixed this via a 1 MB `char[]` buffer at `azureopenai-cli/Program.cs:584` per [SECURITY-AUDIT-001](SECURITY-AUDIT-001.md) MEDIUM-001. v2 port dropped the cap. **Port the cap before cutover.**
2. **SEC-REGRESSION-2 -- HTTPS-only check missing in v2** (`azureopenai-cli-v2/Program.cs:143-197`)
   v1 rejects non-HTTPS endpoints at `azureopenai-cli/Program.cs:384` (`endpoint.Scheme != "https"`). v2 constructs `new AzureOpenAIClient(new Uri(endpoint), ...)` with zero scheme validation. SECURITY-AUDIT-001 MEDIUM-002 was closed in v1 by this check; v2 regresses it.
3. **FR-017 v2 port** -- v2 call site has no `SetNewMaxCompletionTokensPropertyEnabled(true)`. v1 has it at `Program.cs:728` (standard) and `Program.cs:1693` (Ralph iter). Without it, `gpt-5.x` / `o1` / `o3` deployments return HTTP 400. v2 cannot ship as the default binary.
4. **FR-001 v2 parity gap (non-security)** -- v2 has no combined-mode merging of stdin + args. Not a cutover blocker on its own, but a UX regression users will file against v2.0.0.

---

## Ready-to-ship quick wins (XS/S, isolated)

Ordered by ROI and independence. Each is self-contained -- no dependency on the critical-path chain.

| # | Item | Effort | Gain | Dep |
|---|------|--------|------|-----|
| 1 | **FR-007** parallel startup + TLS prewarm | 3-5 h | 200-300 ms TTFT | None |
| 2 | **FR-011 polish** -- per-round stderr status + `--raw`/`--json` compat verification (infra already streams) | 4-8 h | Restores AOT perceived-latency win inside `--agent` | None |
| 3 | **FR-015 (estimator half)** -- wire `--estimate` / `az-ai budget` to existing `CostHook` rate card | ~1 day | Turns FinOps annex into a shipping feature | None |
| 4 | **FR-014 Phase 1** -- preferences schema + loader + legacy reader (absorbs FR-003 Phase 2) | ~2 days | Unblocks FR-018/019/020 critical path | None |

Ship order recommendation: (1) → (4) → (2) → (3). #1 is highest immediate ROI; #4 unblocks the multi-provider chain.

---

## Critical path (multi-week features)

```
FR-014 (local prefs + multi-provider)           [~2 weeks]
   │   absorbs FR-003 / FR-009 / FR-010
   ▼
FR-018 (llama.cpp / Ollama adapter)             [2-3 weeks]
   │   establishes IModelProvider contract
   ├──► FR-019 (gemma.cpp direct -- optional)    [2 weeks, niche]
   ▼
FR-020 (NVIDIA NIM + per-trigger routing)       [2-3 weeks, implements ADR-006]
```

Parallel tracks, independent of the above chain:

- **FR-002 chat REPL** -- standalone. Medium (2-3 days Phase 1). Retention driver.
- **FR-013 MCP client + server** -- standalone. Large (4-5 engineer-weeks for both roles). Biggest distribution vector currently unclaimed per competitive matrix.

---

## Status Details

### ✅ FR-001: Stdin Pipe & Context Injection -- SHIPPED v1.1.0

Fully implemented on 2026-04-08. Key capabilities delivered:

- **Stdin piping:** `echo "question" | az-ai` reads from stdin when input is redirected
- **Combined mode:** `cat file.py | az-ai "summarize this"` merges piped content with prompt argument
- **Input validation:** 32K character prompt limit with clear error messaging
- **No regressions:** `az-ai "hello"` continues to work when stdin is a TTY

**v2 gaps:** no combined mode; no buffer cap (see cutover blocker SEC-REGRESSION-1). `--file <path>` flag deferred.

### 📋 FR-002: Interactive Chat Mode -- PLANNED

No implementation started. Phases:
1. **REPL mode** with `--chat` flag and `/exit`, `/clear`, `/help`, `/model` slash commands
2. **Session persistence** with `--continue` and `--sessions`
3. **Context window management** with token tracking

Soft dependency on FR-014 profiles (chat UX is better when profiles exist).

### 🪦 FR-003 / FR-009 / FR-010 -- SUPERSEDED by FR-014

The flag surface and legacy `~/.azureopenai-cli.json` reader shipped in v1.1.0 (FR-003 Phase 1). FR-014 absorbs the remaining scope:

- **FR-003 Phase 2** (`--config set/get/reset`) → FR-014 §6 config commands
- **FR-009** (directory overrides) → FR-014 §3 precedence chain (`./.az-ai/preferences.json` walking up to root)
- **FR-010** (model aliases + env-var normalization) → FR-014 §5 schema + §7 env-var surface

The original proposal docs remain in-tree as historical context for the v2 port.

### ✅ FR-004: Latency & Startup Optimization -- PARTIAL→LARGELY SHIPPED

- ✅ **Phase 1 (v1.1.0):** Braille spinner on stderr; clears on first token; pipe-safe.
- ✅ **Phase 2a (v1.8.0):** Native AOT via FR-006 -- ~5.4 ms cold start at v1.8.0 ship (current v2.0.6: 10.7 ms p50, see [`docs/perf/v2.0.5-baseline.md`](../perf/v2.0.5-baseline.md)).
- 📋 **Phase 2b (deferred):** Daemon container mode + Unix socket.
- 📋 **Phase 3 (deferred):** Native install via `dotnet tool install`, GitHub Releases, Homebrew (partial progress via [Bob Sacamano's packaging scaffolds](../../packaging/)).

### 🔄 FR-005: Shell Integration & Output Intelligence -- PARTIAL

- ✅ `--json` output mode (v1.1.0)
- ✅ `--raw` flag for forced plain-text output (v1.7.0)

**Still pending:** markdown-aware TTY rendering, `--shell` execute mode, `--code` code-block extraction, clipboard integration.

### ✅ FR-006: Unblock Native AOT -- SHIPPED v1.8.0

`OutputJsonError` replaced with source-generated `ErrorJsonResponse`; `SquadConfig.Load/Save/Initialize` migrated off reflection-based `JsonSerializer`. `make publish-aot` produces a self-contained single-file binary (~9 MB / ~5.4 ms cold start at v1.8.0 ship; ~13 MiB / 10.7 ms p50 on current v2.0.6 -- see [`docs/perf/v2.0.5-baseline.md`](../perf/v2.0.5-baseline.md)).

### 📋 FR-007: Parallel Startup & TLS Pre-warming -- PLANNED

Sequential startup today: DotEnv → UserConfig → arg parsing → Azure client → TLS handshake. The TLS handshake (~200-500 ms) can start in parallel with config loading. Uses a shared `SocketsHttpHandler` so the Azure SDK reuses the pre-warmed connection. Independent of all other FRs.

### 📋 FR-008: Prompt Response Cache -- PLANNED

File-based SHA256-keyed cache in `~/.cache/azureopenai-cli/` for Espanso/AHK repeat-prompt workflows. TTL + LRU + `--no-cache` + `--cache-clear`. Bypassed in agent/Ralph modes.

### ✓ FR-011: Agent Mode Streaming Output -- INFRA PRESENT

v1 already streams agent output via `CompleteChatStreamingAsync` (`Program.cs:1491`). Remaining polish: per-round stderr status ticker, verified `--raw` / `--json` compat matrix inside `--agent`.

### 📝 FR-012: Plugin/Tool Registry -- DRAFT

Blocked by FR-014 (plugin manifests live under `./.az-ai/` per FR-014 §3 directory-override convention).

### 📝 FR-013: MCP Client + Server -- DRAFT

Zero code in tree (`ModelContextProtocol` not referenced anywhere). Largest distribution-vector gap per competitive analysis. Schedule v2.1 / v2.2.

### 📝 FR-014: Local Preferences + Multi-Provider -- DESIGN (2026-04-24)

Critical path. Single-file preferences (`~/.config/az-ai/preferences.json`, XDG), legacy-path fallback, directory overrides, profiles, provider abstraction. JSON (not TOML) to preserve AOT story. See the FR for the full schema, precedence chain, and migration plan. Absorbs FR-003/009/010.

### ⚠️ FR-015: Pattern Library + Cost Estimator -- INFRA PARTIAL

Rate card exists (`azureopenai-cli-v2/Observability/CostHook.cs:25-29`, `PriceEntry` map). User-facing `--estimate` / `az-ai budget` UX unshipped. Pattern library (curated prompts) does not exist yet. Estimator half is now a quick win.

### ✅ FR-016: AOT Reflection Regression Hotfix -- SHIPPED v1.9.1

Fixed via `Azure.AI.OpenAI 2.1.0 → 2.9.0-beta.1` upgrade (CHANGELOG.md L22-27). AOT binary 8.9 MB, zero new trim warnings. **Archive.**

### ✅/❌ FR-017: `max_completion_tokens` Compatibility -- v1 SHIPPED, v2 BLOCKER

`SetNewMaxCompletionTokensPropertyEnabled(true)` at `azureopenai-cli/Program.cs:728` (standard) and `1693` (Ralph). **v2 port is missing** -- see cutover blocker #3. Without it, `gpt-5.x` / `o1` / `o3` deployments return HTTP 400.

### 📝 FR-018 / FR-019 / FR-020: Multi-Provider Adapters -- DRAFT

All three blocked by FR-014 (`IModelProvider` contract + preferences schema). FR-018 is the spine; FR-019 (gemma.cpp) is niche/optional; FR-020 (NIM) implements [ADR-006](../adr/).

> **Correction against FR-018/020 draft text:** those proposals reference a `preferences.toml`. FR-014 §2 formalized the decision to use JSON (AOT story). Treat any `.toml` reference in the FR-018/020 bodies as a pre-014 artifact -- the canonical path is `~/.config/az-ai/preferences.json`.

---

## SECURITY-AUDIT-001 status

Audit filed 2026-04-08 against v1.1.0. Status as of 2026-04-20:

| ID | Severity | v1 status | v2 status | Notes |
|----|----------|-----------|-----------|-------|
| MEDIUM-001 | Unbounded stdin | ✅ CLOSED (`Program.cs:584`, 1 MB cap) | ❌ **REGRESSED** (`v2/Program.cs:181`) | P0 for v2.0.0 |
| MEDIUM-002 | HTTP endpoint accepted | ✅ CLOSED (`Program.cs:384`, Scheme check) | ❌ **REGRESSED** (`v2/Program.cs:197`) | P0 for v2.0.0 |
| MEDIUM-003 | GH Actions unpinned | ⚠️ Open | ⚠️ Open | Jerry owns. Dependabot + SHA pins |
| MEDIUM-004 | Docker base unpinned | ⚠️ Open | ⚠️ Open | Dockerfile still uses mutable tags |
| LOW-005 | CI permissions scope | ⚠️ Open | ⚠️ Open | One-line `permissions: contents: read` |
| LOW-006 | `.env` in Docker context | ⚠️ Open | ⚠️ Open | One-line `.dockerignore` add |
| LOW-007 | Config file TOCTOU | ⚠️ Open | ⚠️ Open | Low risk; defer |
| LOW-008 | Windows ACL unhandled | ⚠️ Open | ⚠️ Open | Low risk; defer |
| LOW-009 | Exception message leak | ⚠️ Open | ⚠️ Open | Defer |
| LOW-010 | Makefile ARGS quoting | ⚠️ Accepted | n/a | Accepted risk per audit |

**Net:** Priority-1 items (MEDIUM-001, MEDIUM-002) closed in v1 and **both regressed in v2 port.** Priority-2 items (003-006) untouched on both trees. No critical items.

---

## Recommended next 3-5 implementation targets

Ranked by blocking impact.

1. **Port SEC-REGRESSION-1 + SEC-REGRESSION-2 to v2** (stdin cap + HTTPS-only). Non-negotiable before v2.0.0 cutover. ~2 hours.
2. **Port FR-017 to v2** (`SetNewMaxCompletionTokensPropertyEnabled(true)` at v2 call site). Without it, v2 is DOA on every deployment v1.9.1 was built to support. ~1 hour.
3. **FR-007 TLS prewarm.** 3-5 hours, 200-300 ms TTFT gain, zero risk, fully independent. Highest ROI ready-to-ship work in the tree.
4. **FR-015 estimator UX wiring.** Rate card and `CostHook` already exist; add `--estimate` / `--dry-run-cost` flag + `az-ai budget` subcommand. ~1 day.
5. **FR-014 kickoff.** Critical-path dependency for FR-018 → FR-020. Nothing downstream can start until the preferences-file + provider-profile schema lands. ~1.5-2 engineer-weeks.

Everything else (FR-002, FR-013, FR-018..020) queues behind items 1-5 or runs as a parallel medium-term track.

---

## Design Principles

These proposals follow a shared philosophy:

1. **Pipes over prompts** -- CLI tools that compose with other tools win.
2. **Speed is the feature** -- every millisecond of startup is a decision point where the user considers ChatGPT.
3. **Progressively disclosed complexity** -- simple by default, powerful when needed.
4. **Azure-native is the moat** -- lean into enterprise identity, multi-model management, and security posture.
5. **Demo-able in 15 seconds** -- if you can't GIF it, it won't go viral.

---

## Appendix -- Document history

- **2026-04-24** -- Merged `STATUS-AUDIT.md` into this index as the single source of truth. Marked FR-003/009/010 as Superseded by FR-014. Retired the fictional v1.2/1.3/1.4 shipping timeline (reality: v1.8.0 + v1.9.1 shipped with a different feature set).
- **2026-04-20** -- Status audit (Costanza) against v1.9.1 and v2 tree; surfaced v2 cutover blockers.
- **2026-04-08** -- Initial proposal set (FR-001..FR-005) from product review; v1.1.0 ship.
