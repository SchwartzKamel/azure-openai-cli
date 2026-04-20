# Proposal Status Audit (2026-04-20)

> **Synthesizer:** Costanza. **Inputs:** `triage-core.md` (FR-001..010, complete); FR-011..020 + SECURITY-AUDIT-001 derived via direct code inspection of `azureopenai-cli/` (v1) and `azureopenai-cli-v2/` (v2) on 2026-04-20. **Authority:** Overrides `docs/proposals/README.md` where they disagree.
>
> **Caveat flagged per memo-synthesis rules:** The "platform" and "providers" audit memos referenced in the brief were not present in session state — only `triage-core.md` was supplied complete. FR-011..020 and SECURITY-AUDIT-001 sections below are built from primary evidence (grep + view against the actual tree), not from a companion triage memo. Treat any claim here without a file:line cite as provisional.

## TL;DR

- **Critical path to v2.0.0 cutover is gated by two silent security regressions** — v2 ships `Console.In.ReadToEndAsync` unbounded (`azureopenai-cli-v2/Program.cs:181`) and accepts `http://` endpoints (`azureopenai-cli-v2/Program.cs:197`, no `Scheme` check). Both were closed in v1 (`Program.cs:584`, `Program.cs:384`) and have **regressed in the v2 port.** Cannot cut v2.0.0 with these open.
- **FR-017 is only half-shipped.** v1 calls `SetNewMaxCompletionTokensPropertyEnabled(true)` at `azureopenai-cli/Program.cs:728, 1693`; v2's call site has no equivalent — modern `gpt-5.x` / `o1` deployments will HTTP 400 against v2 today.
- **Top 3 quick wins** (all XS/S, independent): FR-007 TLS prewarm (3–5 h, 200–300 ms), FR-010 model aliases (4–6 h), FR-011 agent-mode streaming polish (v1 already streams via `CompleteChatStreamingAsync` at Program.cs:1491 — remaining work is per-round stderr status + `--raw`/`--json` compat).
- **Surprise-shipped, not reflected in README priority matrix:** FR-016 (AOT reflection hotfix, v1.9.1), FR-017 v1 leg (`max_completion_tokens`, v1.9.1), FR-015 *infrastructure* (rate card exists at `azureopenai-cli-v2/Observability/CostHook.cs:25` — user-facing `--estimate` flag does not).
- **Critical-path multi-week chain is FR-014 → FR-018 → FR-020.** FR-018 has a hard dependency on FR-014's preferences file; FR-020 has a hard dependency on FR-018's `IModelProvider` contract. Nothing in that chain exists yet.

## Corrected Priority Matrix

This table overrides `docs/proposals/README.md`. Status column reflects verified code state on 2026-04-20, not the README's historical claim.

| ID | Title | Priority | Effort | Actual status (verified) | Recommendation | Blocks / Blocked by |
|----|-------|----------|--------|--------------------------|----------------|----------------------|
| FR-001 | Stdin Pipe & Context Injection | P0 | S | ✅ SHIPPED v1 (`Program.cs:578,584`). ⚠️ v2 PARTIAL — no combined mode, **no buffer cap** (regression). | Port combined mode + cap to v2. | Blocks v2.0.0 cutover (security). |
| FR-002 | Interactive Chat Mode (REPL) | P0 | M | ❌ UNSHIPPED both trees. No `--chat` flag, no REPL loop. | KEEP-PLANNED — v1.3 or v2.1. | Soft dep on FR-003 Phase 2. |
| FR-003 | Local User Preferences | P1 | S-M | ✓ PHASE 1 SHIPPED v1 (env vars + inline flags, `Program.cs:452`). Phase 2 (`--config set/get/reset`) NOT shipped — only `--config show` exists. v2 has CliOptions fields only. | README says "IN PROGRESS" — it is **Phase 1 done, Phase 2 not started.** Implement Phase 2. | Blocks FR-009, FR-010. |
| FR-004 | Latency & Startup Optimization | P0 | Phased | ✅ Phase 1 (spinner) + Phase 2a (AOT) SHIPPED v1. Daemon (Phase 2b) + distribution (Phase 3) unshipped. | Ship as-is. Defer daemon to post-v2. | — |
| FR-005 | Shell Integration | P1 | M | ✓ PARTIAL — `--json`/`--raw` shipped; `--code`/`--shell`/markdown not. | Correctly claimed. Plan `--code`/`--shell` for v1.3. | — |
| FR-006 | Unblock Native AOT | P0 | S | ✅ SHIPPED v1 (v1.8.0). ~5.4 ms cold start. | Done. | Unblocks FR-008, FR-011. |
| FR-007 | Parallel Startup + TLS Prewarm | P1 | S | ❌ UNSHIPPED. v1 startup is strictly sequential. | **QUICK WIN** — ship v1.2. | Independent. |
| FR-008 | Prompt Response Cache | P1 | M | ❌ UNSHIPPED. | KEEP-PLANNED v1.3. | Blocked by FR-006 (done). |
| FR-009 | `--config set` + Per-Dir Overrides | P1 | M | ❌ UNSHIPPED. | Implement after FR-003 Phase 2. | Blocks FR-010. |
| FR-010 | Model Aliases & Smart Defaults | P2 | S | ❌ UNSHIPPED. | **QUICK WIN** after FR-009 lands. | Blocked by FR-009. |
| FR-011 | Agent Mode Streaming Output | P0 | S-M | ✓ INFRA-PRESENT — `RunAgentLoop` already uses `CompleteChatStreamingAsync` (`Program.cs:1491`). Polish (per-round stderr status, `--raw`/`--json` compat matrix) outstanding. | **QUICK WIN** — finish the polish layer. | — |
| FR-012 | Plugin/Tool Registry | P2 | M-L | 📝 DRAFT. Not implemented. | Defer to v2.1+. | Blocked by FR-009. |
| FR-013 | MCP Client + Server | P1 | L | 📝 DRAFT. Zero code. No `ModelContextProtocol` anywhere in tree. | Schedule for v2.1 or v2.2. | Multi-week. Not on critical path. |
| FR-014 | Local Prefs + Multi-Provider | P1 | M | 📝 DRAFT. Subsumes FR-003/009/010. | **Critical path** — land before FR-018. | Blocks FR-018, FR-020. |
| FR-015 | Pattern Library + Cost Estimator | P2 | S-M | ⚠️ INFRA-PARTIAL — rate card exists (`azureopenai-cli-v2/Observability/CostHook.cs:25-29`, `PriceEntry` map). User-facing `--estimate` / `az-ai budget` not wired. Pattern library does not exist. | **QUICK WIN** for the estimator-UX half; library is independent S-M. | Independent. |
| FR-016 | AOT Reflection Regression Hotfix | P0 | S | ✅ SHIPPED v1.9.1 (SDK upgrade to `Azure.AI.OpenAI 2.9.0-beta.1`). | **Archive.** | — |
| FR-017 | `max_completion_tokens` Compatibility | P0 | S | ✅ v1 SHIPPED (v1.9.1, `Program.cs:728, 1693`). ❌ v2 **NOT PORTED** — call site in v2 has no `SetNewMaxCompletionTokensPropertyEnabled`. | **v2 port is P0 blocker for v2.0.0.** | Blocks v2.0.0 cutover. |
| FR-018 | Local-Model Provider (llama.cpp / Ollama) | P2 | M | 📝 DRAFT. | On critical path. Blocks FR-019, FR-020. | Blocked by FR-014. |
| FR-019 | gemma.cpp Direct Adapter | P3 | M | 📝 DRAFT. | Defer — niche. | Blocked by FR-018. |
| FR-020 | NVIDIA NIM + Per-Trigger Routing | P2 | M (S in README, disputed) | 📝 DRAFT. | Ships release N+1 after FR-018. | Blocked by FR-018. |

**Disagreement flagged:** README labels FR-020 effort as "Small"; FR-020's own proposal text describes ADR-006 scope, per-trigger routing, Bania perf gates, Newman SSRF review, and Frank reliability gates — that reads as **Medium** at minimum. Audit sides with the FR body over the README summary.

## P0 — v2.0.0 cutover blockers

These must close before v2 can replace v1 as the shipping target.

1. **SEC-REGRESSION-1 — Unbounded stdin in v2** (`azureopenai-cli-v2/Program.cs:181`)
   ```csharp
   prompt = await Console.In.ReadToEndAsync(cts.Token);
   ```
   v1 fixed this via a 1 MB `char[]` buffer at `azureopenai-cli/Program.cs:584` per SECURITY-AUDIT-001 MEDIUM-001. v2 port dropped the cap. **Port the cap before cutover.**
2. **SEC-REGRESSION-2 — HTTPS-only check missing in v2** (`azureopenai-cli-v2/Program.cs:143-197`)
   v1 rejects non-HTTPS endpoints at `azureopenai-cli/Program.cs:384` (`endpoint.Scheme != "https"`). v2 constructs `new AzureOpenAIClient(new Uri(endpoint), ...)` with zero scheme validation. SECURITY-AUDIT-001 MEDIUM-002 closed in v1 by this check; v2 regresses it.
3. **FR-017 v2 port** — v2 call site has no `SetNewMaxCompletionTokensPropertyEnabled(true)`. v1 has it at Program.cs:728 (standard) and 1693 (Ralph iter). Without it, `gpt-5.x` / `o1` / `o3` deployments return HTTP 400. v2 cannot ship as the default binary.
4. **FR-001 v2 parity gap (non-security)** — v2 has no combined-mode merging of stdin + args. Not a cutover blocker on its own, but a UX regression users will file against v2.0.0.

## Ready-to-ship quick wins (XS/S, isolated)

Order is by ROI and independence. Each is self-contained — no dependency on the critical-path chain.

| # | Item | Effort | Gain | Dep |
|---|------|--------|------|-----|
| 1 | **FR-007** parallel startup + TLS prewarm | 3–5 h | 200–300 ms TTFT | None |
| 2 | **FR-011 polish** — per-round stderr status + `--raw`/`--json` compat verification (infra already streams) | 4–8 h | Restores AOT perceived-latency win inside `--agent` | None |
| 3 | **FR-010** model aliases + env var normalization (`AZURE_OPENAI_ENDPOINT` alongside legacy) | 4–6 h | DX polish | Blocked by FR-009 unless we accept a temp hardcoded alias map |
| 4 | **FR-015 (estimator half)** — wire `--estimate` / `az-ai budget` to existing `CostHook` rate card | ~1 day | Turns FinOps annex into a shipping feature; Morty-visible | None |
| 5 | **FR-003 Phase 2** — `--config set/get/reset` handler | 4–8 h | Unblocks FR-009 → FR-010 | Phase 1 done |

Ship order recommendation: (1) → (5) → (3) → (2) → (4). #1 and #5 are the highest ROI; #2 is the highest-visibility.

## Critical path (multi-week features)

```
FR-014 (local prefs + multi-provider)           [2 weeks]
   │   subsumes FR-003 / FR-009 / FR-010
   ▼
FR-018 (llama.cpp / Ollama adapter)             [2–3 weeks]
   │   establishes IModelProvider contract
   ├──► FR-019 (gemma.cpp direct — optional)    [2 weeks, niche]
   ▼
FR-020 (NVIDIA NIM + per-trigger routing)       [2–3 weeks, implements ADR-006]
```

Parallel tracks, independent of the above chain:

- **FR-002 chat REPL** — standalone. Medium (2–3 days Phase 1). Retention driver.
- **FR-013 MCP client + server** — standalone. Large (4–5 engineer-weeks for both roles). Biggest distribution vector we are currently leaving on the table per competitive matrix.

## Surprise-shipped (archive these)

Move to a "Shipped" section of the README priority matrix; stop counting them as open work:

- **FR-016** — AOT reflection regression. Fixed in **v1.9.1** via `Azure.AI.OpenAI 2.1.0 → 2.9.0-beta.1` upgrade (CHANGELOG.md L22–27). AOT binary 8.9 MB, zero new trim warnings.
- **FR-017 v1 leg** — `max_completion_tokens`. Fixed in **v1.9.1** (`Program.cs:728, 1693`; CHANGELOG.md L12–20). v2 port still open (see P0 #3).
- **FR-015 infrastructure** — Cost rate card shipped incidentally as `azureopenai-cli-v2/Observability/CostHook.cs` (PriceEntry map, L25–29). User-facing estimator UX still owed — now an XS/S quick win instead of a Medium greenfield.

## Stale README claims (correct these)

Line references are to `docs/proposals/README.md` as of 2026-04-20:

- **L3** — "Last updated: 2026-04-08 — After v1.1.0 release." Out of date. v1.8.0 and v1.9.1 have shipped since. Bump to 2026-04-20.
- **L13** — FR-003 listed as "🔧 IN PROGRESS — Phase 1." Misleading. Phase 1 **shipped in v1.1.0**; Phase 2 (`--config set/get/reset`) is **not started**. Status should read "✓ Phase 1 SHIPPED; Phase 2 planned."
- **L21** — FR-011 listed as "📋 PLANNED — v1.9.0 top priority." Infra already present (`CompleteChatStreamingAsync` at Program.cs:1491); only polish remains.
- **Missing rows** — README priority matrix omits FR-013, FR-014, FR-015, FR-016, FR-017. All five exist as proposal files in `docs/proposals/`. FR-016 and FR-017 have shipped.
- **L126–151 "Shipping Timeline"** — Targets v1.2, v1.3, v1.4. Reality: v1.8.0 and v1.9.1 have already shipped with a different feature set (AOT, hotfixes). Timeline is fictional; either rewrite or drop.

## SECURITY-AUDIT-001 status

Audit filed 2026-04-08 against v1.1.0. Status as of 2026-04-20:

| ID | Severity | v1 status | v2 status | Notes |
|----|----------|-----------|-----------|-------|
| MEDIUM-001 | Unbounded stdin | ✅ CLOSED (`Program.cs:584`, 1 MB cap) | ❌ **REGRESSED** (`v2/Program.cs:181`) | P0 for v2.0.0. |
| MEDIUM-002 | HTTP endpoint accepted | ✅ CLOSED (`Program.cs:384`, Scheme check) | ❌ **REGRESSED** (`v2/Program.cs:197`) | P0 for v2.0.0. |
| MEDIUM-003 | GH Actions unpinned | ⚠️ Open | ⚠️ Open | Jerry owns. Dependabot + SHA pins. |
| MEDIUM-004 | Docker base unpinned | ⚠️ Open | ⚠️ Open | Dockerfile still uses mutable tags. |
| LOW-005 | CI permissions scope | ⚠️ Open | ⚠️ Open | One-line `permissions: contents: read`. |
| LOW-006 | `.env` in Docker context | ⚠️ Open | ⚠️ Open | One-line `.dockerignore` add. |
| LOW-007 | Config file TOCTOU | ⚠️ Open | ⚠️ Open | Low risk; defer. |
| LOW-008 | Windows ACL unhandled | ⚠️ Open | ⚠️ Open | Low risk; defer. |
| LOW-009 | Exception message leak | ⚠️ Open | ⚠️ Open | Defer. |
| LOW-010 | Makefile ARGS quoting | ⚠️ Accepted | n/a | Accepted risk per audit. |

**Net:** Priority-1 items (MEDIUM-001, MEDIUM-002) closed in v1 and **both regressed in v2 port.** Priority-2 items (003–006) untouched on both trees. No critical items.

## Recommended next 3–5 implementation targets

Ranked by blocking impact. Rationale is one line each.

1. **Port SEC-REGRESSION-1 + SEC-REGRESSION-2 to v2** (stdin cap + HTTPS-only). Non-negotiable before v2.0.0 cutover. ~2 hours.
2. **Port FR-017 to v2** (`SetNewMaxCompletionTokensPropertyEnabled(true)` at v2 call site). Without it, v2 is DOA on every deployment v1.9.1 was built to support. ~1 hour.
3. **FR-007 TLS prewarm.** 3–5 hours, 200–300 ms TTFT gain, zero risk, fully independent. Highest ROI ready-to-ship work in the tree.
4. **FR-015 estimator UX wiring.** Rate card and `CostHook` already exist; add `--estimate` / `--dry-run-cost` flag + `az-ai budget` subcommand. ~1 day. Turns Morty's FinOps annex into a shipping feature.
5. **FR-014 kickoff.** Critical-path dependency for FR-018 → FR-020. Nothing downstream can start until the preferences-file + provider-profile schema lands. ~1.5–2 engineer-weeks.

Everything else (FR-002, FR-013, FR-018..020) should queue behind items 1–5 or run as parallel medium-term tracks.

## Appendix A — Memo pointers

- Core memo (FR-001..010): `/home/tweber/.copilot/session-state/d523060c-417c-45ce-bf84-9edc83d5c37b/files/triage-core.md` (525 lines, author: Costanza, 2025-04-08).
- Platform memo (FR-011..017): **not found in session state** on 2026-04-20. FR-011..017 sections above derived from direct code inspection.
- Providers memo (FR-018..020 + SECURITY-AUDIT-001): **not found in session state** on 2026-04-20. FR-018..020 sections above derived from reading the proposal files; SECURITY-AUDIT-001 status derived from re-reading the audit against current code.
- Primary proposal files: `docs/proposals/FR-001` … `FR-020`, `docs/proposals/SECURITY-AUDIT-001.md`, `CHANGELOG.md`.

---

*End of audit. This document supersedes the priority-matrix status column in `docs/proposals/README.md` until the next audit pass.*
