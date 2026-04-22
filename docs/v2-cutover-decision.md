# v2.0.0 Cutover -- Go/No-Go Decision

**Decision owner:** Costanza (PM)
**Release manager:** Mr. Lippman
**Baseline:** `a0ca066` on `main` (17 commits since `781741f`)
**Date of ruling:** issued at current `HEAD`, before tag

---

## Decision

> **GO** -- conditional on the [go-time todo list](#go-time-todo-list) below being burned down **before** `git tag v2.0.0`. No code blockers remain. Every outstanding item is documentation, packaging, or wording polish. The fleet captain executes the checklist, then tags.

**One-line rationale:** Every red gate we wrote down two quarters ago is green. The AOT ratio cleared without a waiver, security is zero-red, chaos blockers are closed with reproducer coverage, licensing is clean, docs are split and cross-linked, and the test matrix is 1025 + 374 + 138 green. The remaining work is release hygiene, not engineering risk -- and release hygiene is what a go-time checklist is for. You want a piece of me? FINE. File an FR. Otherwise, ship it.

---

## Precondition matrix

| # | Gate | Evidence | Owner | Status | Verdict |
|---|------|----------|-------|--------|---------|
| 1 | v1 regression suite green | 1025 / 1025 | Puddy | green | **PASS** |
| 2 | v2 unit suite green | 374 / 374 (incl. +33 from `a0ca066`) | Puddy | green | **PASS** |
| 3 | Integration suite green | 138 / 138 (`488aebd`) | Puddy | green | **PASS** |
| 4 | AOT binary size ≤ 1.50× v1 | 12.91 MB = **1.456×** v1 (8.86 MB) | Bania | green | **PASS** (no waiver) |
| 5 | Startup p95 ≤ 1.25× v1 | `--version --short` 1.12×, `--help` 1.23×, parse-heavy 0.93× | Bania | green | **PASS** |
| 6 | RSS ≤ 1.00× v1 | 0.88-1.00× across scenarios | Bania | green | **PASS** |
| 7 | Security review | `docs/security-review-v2.md` -- 0 🔴, 8 🟡 (K-1..K-8, non-blocking) | Newman | green | **PASS** |
| 8 | Chaos drill | `docs/chaos-drill-v2.md` + `tests/chaos/`; 3 🔴 closed by `a0ca066`; 5 🟡 (F4-F8, non-blocking) | FDR | green | **PASS** |
| 9 | Licensing audit | 34 MIT + 4 Apache-2.0 + 1 BSD-3; zero GPL/LGPL/AGPL/MPL/SSPL; `scripts/license-audit.sh` in CI | Jackie Chiles | green | **PASS** |
| 10 | NOTICE / THIRD_PARTY_NOTICES -- tarball + container | `Dockerfile` `COPY`, OCI labels, `tests/docker-licenses.sh` | Jackie + Kramer | green | **PASS** |
| 11 | NOTICE -- Homebrew / Scoop / Nix | Manifests still pinned to v1.8.1; v2 bump deferred | Bob Sacamano | **not landed** | **CONDITIONAL** -- release-notes claim already softened; packaging bump = 2.0.1 work, not cutover blocker |
| 12 | User docs | `persona-guide.md`, `migration-v1-to-v2.md`, `config-reference.md`, sample JSON, espanso-ahk step-2 hardening | Elaine | green | **PASS** |
| 13 | ADRs | ADR-006 split → ADR-006/007/008 with verbatim appendix (`cf7901b`) | Elaine + Wilhelm | green | **PASS** |
| 14 | Proposal hygiene | STATUS-AUDIT merged into proposals README; FR-003/009/010 superseded by FR-014 (`48d48e3`) | Costanza | green | **PASS** |
| 15 | Proposal accuracy -- FR-008 | Doc still reflects pre-ship design (opt-out, 24h TTL, strict LRU) | Costanza + Elaine | **drift** | **CONDITIONAL** -- update before tag (see §Spec deviations) |
| 16 | CHANGELOG + release notes draft | `CHANGELOG.md`, `docs/release-notes-v2.0.0.md` | Mr. Lippman | green (after my wording fixes) | **PASS** |
| 17 | Rollback plan documented | `docs/v2-cutover-checklist.md` §5 (R1-R9) | Wilhelm + Jerry | green | **PASS** |

**Rollup:** 15 PASS, 2 CONDITIONAL, 0 FAIL. The CONDITIONAL items are pure paperwork on the critical path of the go-time list; neither is a code change, neither introduces risk, neither requires a re-review from another captain.

---

## Answers to Mr. Lippman's six open questions

### 1. AOT size waiver wording -- **drop it.**

Bania cleared the gate at **1.456× v1 (12.91 MB vs 8.86 MB)** via `StackTraceSupport=false` + one related ILC property -- no code changes, 301/301 green post-trim. There is no waiver to draft because there is no gate to waive. The release notes' "Performance & size" section and the "Known limitations" section both previously claimed 1.62× and a waiver; I have already rewritten both in-tree to reflect shipped reality. The 2.1 AOT trim pass (residual `Azure.AI.OpenAI` reflection, est. 0.3-0.9 MB headroom) is called out as **non-blocking forward work**, not a known limitation against 2.0.0.

### 2. Dual-binary post-cutover plan -- **confirmed with a wording adjustment.**

Pattern stands: v2 ships as `az-ai` post-cutover; v1 remains installable from the v1.9.1 release channel. However, **Homebrew does not natively resolve `brew install ...@1.9.1`** without a dedicated versioned formula file (e.g., `az-ai@1.9.1.rb` in the tap), and **Scoop does not natively resolve `scoop install ...@1.9.1`** without an `extras`-style versions bucket. Neither is set up today -- `packaging/` contains a single formula and a single manifest, both pinned at v1.8.1.

Release-notes pin instructions rewritten to use direct `brew install --formula <raw.githubusercontent.com URL>` and `scoop install <URL>` against the `v1.9.1` git ref, plus `docker pull ghcr.io/...:1.9.1`. Native versioned-pin UX (`@1.9.1` syntax) is a 2.0.1 packaging deliverable on Bob. No one is being asked to install a URL-style command in a panic; the story is "tarball, container, or the pinned-manifest URL."

### 3. Contributor thanks wording -- **keep.**

`git shortlog 9e74961..488aebd` is genuinely `15  SchwartzKamel` co-authored with Copilot. Lippman's bench-roster thank-you (Costanza PM, Wilhelm, Elaine, Bania, Chiles, Newman, Kramer, Jerry, Puddy, "everyone else on the bench") is on-brand for the project voice, factually correct (every named agent produced a reviewable artifact), and sets expectation for how future releases credit cross-agent work. Unusual in a release body elsewhere in the ecosystem; standard here. **Keep verbatim.**

### 4. "All artifacts include NOTICE" claim -- **option (c), softened.**

Do not block 2.0.0 on Bob. Do not tag claiming brew/scoop/nix parity we have not shipped. I rewrote the Security section of the release notes to state:

> The **tarball and container image** ship NOTICE / THIRD_PARTY_NOTICES / LICENSE in-band at 2.0.0. **Homebrew / Scoop / Nix manifest updates** land in the 2.0.1 packaging sweep; the v1.8.1 manifests currently in `packaging/` remain pinned to v1 until then.

Added to Known Limitations:

> Homebrew / Scoop / Nix manifests lag one release… Bob's v2.0.0 bump lands in 2.0.1.

This is honest, legally defensible (no attribution breach -- no v2 artifact is being distributed through brew/scoop/nix at 2.0.0 to carry an obligation), and keeps 2.0.0 on schedule. Packaging parity becomes a 2.0.1 milestone that Bob owns, Lippman tracks, and Wilhelm gates. **Answer: (c).**

### 5. FDR chaos drill -- **resolved.**

`a0ca066` closed F1/F2/F3 with 33 new tests and flipped each reproducer 🟢. F4-F8 are 🟡 and non-blocking. Nothing outstanding on this gate.

### 6. Date phrasing -- **confirmed.**

"Release window opens 2026-04-20" is already qualified in-body as "commit-cutoff for the 2.0.0 line, not a publication-date commitment." That's exactly the right hedge -- it gives the tap/bucket/channel distributors a calendar anchor without binding us to a publication minute we cannot control. **Keep.**

---

## Spec-deviation rulings

### FR-008 (prompt cache) -- update the proposal **before tag**

Kramer shipped three documented, defensible deviations from the proposal:

| Dimension | Proposal says | Shipped reality | Defensible? |
|-----------|---------------|-----------------|-------------|
| Activation | opt-out | **opt-in** (`--cache` required) | Yes -- privacy-safer default, matches v1's "no hidden state" ethos |
| TTL | 24h | **7 days** | Yes -- Espanso/AHK workflows span a workweek, not a day |
| Eviction | strict access-time LRU | **mtime-based** | Yes -- single `File.GetLastWriteTime` vs tracking an access DB; lower cold-start cost, acceptable fidelity |

All three are correct calls. But the FR-008 doc is the authoritative design record that a future contributor reads when they ask "why does the cache behave this way?" -- shipping a release where the proposal contradicts the code teaches new contributors that proposals are fiction. That's a culture cost we do not pay.

**Ruling: update FR-008 in-place before tag.** Include a short "Shipped deviations from original design" section at the top with the three bullets and the rationale, and fix the inline spec (lines ~131, 139, 179, 254). This is a pure-docs edit, no code risk, 20 minutes of Elaine's time. CHANGELOG note alone is insufficient -- the CHANGELOG is a runtime record, not a design record.

### PersonaMemory `ArgumentException` UX wrap -- **ship, patch in 2.0.1**

`Program.cs:321` calls `personaMemory.ReadHistory(activePersona.Name)` outside the main `try` block. On a hostile persona name, `PersonaMemory` correctly throws `ArgumentException` -- traversal is blocked, the security property holds -- but the stack trace prints unwrapped and ugly. Three-line fix: move the call inside the existing `try`, or add a dedicated `catch (ArgumentException)` that prints a structured error and exits 1.

I **cannot** edit `.cs` under the rules of this decision. More importantly, I **would not** block cutover on this even if I could:

- Security is intact -- this is cosmetic.
- Reachability requires a hostile persona already in `.squad.json`; an attacker who can write that file has already lost you.
- Puddy does not have a regression test for it, which means whoever lands the fix lands the test too -- better paced as a clean 2.0.1 patch than a last-minute shim.

**Ruling: ship 2.0.0.** Add to Known Limitations in release notes ("hostile persona names produce an unhandled `ArgumentException` with a raw stack trace; security is not affected; wrap lands in 2.0.1"). File as FR-021 or track against the existing persona-hardening thread. The fleet captain decides whether release notes copy-edit is worth it; I defer.

---

## Go-time todo list

These items must be **done (or explicitly accepted as deferred) before `git tag v2.0.0`**. None require code changes.

| # | Task | Owner | Scope | Blocking? |
|---|------|-------|-------|-----------|
| G1 | Update `docs/proposals/FR-008-prompt-response-cache.md` to reflect shipped reality (opt-in default, 7-day TTL, mtime eviction) -- add a "Shipped deviations" section at top + fix inline spec | Elaine + Costanza | docs only | **YES** |
| G2 | Verify in-tree release-notes edits already landed by this decision: (a) AOT section rewritten to 1.456× no-waiver, (b) NOTICE claim softened, (c) pin instructions rewritten to `--formula <URL>` / pinned JSON / `ghcr:1.9.1` | Mr. Lippman | review only | **YES** |
| G3 | Add a short "Known Limitations" bullet to release notes for the `ArgumentException` UX wrap -- wording: *"Malicious persona names in `.squad.json` produce an unhandled exception with a raw stack trace. Security (path-traversal blocks, exact-match lookup) is unaffected. UX wrap lands in 2.0.1."* | Mr. Lippman | docs only | **YES** |
| G4 | File tracking issue (FR-021 or equivalent) for the ArgumentException wrap, referencing `Program.cs:321` and the F-series follow-ups | Puddy | tracker only | no -- can trail tag by < 24h |
| G5 | Confirm `scripts/license-audit.sh` is wired into CI required-checks for `main` | Jackie + Wilhelm | CI config | no -- can trail tag by < 24h |
| G6 | Bob Sacamano v2 packaging sweep (Homebrew formula bump, Scoop manifest bump, Nix flake bump, versioned pin scaffolding for `@1.9.1` syntax) -- tracked as **2.0.1 scope**, not cutover blocker | Bob | follow-on release | **NO** (explicitly deferred) |
| G7 | FDR F4-F8 🟡 follow-ups -- tracked for 2.0.x minor line, not blocking | FDR | tracker | **NO** (explicitly deferred) |
| G8 | Newman K-1..K-8 🟡 follow-ups -- tracked for 2.0.x minor line, not blocking | Newman | tracker | **NO** (explicitly deferred) |
| G9 | 2.1 AOT trim pass (residual `Azure.AI.OpenAI` reflection, est. 0.3-0.9 MB) -- tracked for 2.1 | Bania + Kramer | future release | **NO** (explicitly deferred) |
| G10 | Final rehearsal: `make preflight` → gate matrix 1-5 in `docs/v2-cutover-checklist.md` §4 all exit 0; rollback artifact (§3.1 step 3 `ghcr:latest` digest set-aside) confirmed recorded | Jerry + Puddy + Bania + FDR + Soup Nazi | rehearsal | **YES** |

**When G1, G2, G3, G10 are green and G4/G5 have tracking issues filed, Lippman is cleared to `git tag v2.0.0` and trigger the release workflow.**

---

## Rollback plan (summary)

We retain the dual-binary pattern for the full dual-tree window and honor the procedure in `docs/v2-cutover-checklist.md` §5. If a P0/P1 regression hits the hot path, MAF surfaces an unpatchable security finding within 24h, or `.squad/` data corruption is reported, Jerry fast-forwards `v1-legacy` into a `rollback/1.9.2` branch, Kramer applies any single targeted hotfix, Wilhelm gates the revert through the PR process, and Lippman cuts `v1.9.2` with a rollback notice. `ghcr.io/...:latest` re-tags back to the pre-cutover `:1.9.1` digest (set aside per §3.1). Packaging restores from `packaging/legacy/v1.9.1/` snapshot. Target: < 30 minutes from decision to restored `main`. Point-of-no-return is 30 days post-tag or deletion of `v1-legacy`, whichever comes first -- after that, any revert is a new major-version down-migration requiring its own ADR.

---

## Signatures

Each line references the deliverable doc(s) that carry the signoff. Verdicts recorded at `HEAD = a0ca066`.

| Agent | Domain | Verdict | Evidence |
|-------|--------|---------|----------|
| **Newman** | Security | **CLEAR** -- 0 🔴, 8 🟡 non-blocking | `docs/security-review-v2.md`; `3c35ecf` |
| **Kenny Bania** | Performance + AOT size | **CLEAR** -- 1.456× ≤ 1.50× gate, startup p95 inside budget, RSS ≤ v1 | `docs/perf-baseline-v2.md`, `docs/aot-trim-investigation.md`; `3de364a`, `056920f` |
| **FDR** | Chaos / red team | **CLEAR** -- 3 🔴 closed in `a0ca066`, 5 🟡 non-blocking | `docs/chaos-drill-v2.md`, `tests/chaos/`; `835b95e`, `a0ca066` |
| **Jackie Chiles** | Licensing / OSS compliance | **CLEAR** -- zero GPL family; NOTICE + THIRD_PARTY_NOTICES; CI script | `docs/licensing-audit.md`, `NOTICE`, `THIRD_PARTY_NOTICES.md`, `scripts/license-audit.sh`; `81a1e3a`, `0899ef6` |
| **Puddy** | QA | **CLEAR** -- 1025 + 374 + 138 green; 33 new tests from `a0ca066` | v1 suite, v2 suite, `488aebd` integration suite |
| **Elaine** | Docs | **CLEAR post-G1** -- guides + migration + config ref + ADR split; FR-008 update owed at G1 | `docs/persona-guide.md`, `docs/migration-v1-to-v2.md`, `docs/config-reference.md`, `docs/adr/`; `cbcc49b`, `cf7901b`, `a309154`, `2a9018a`, `48d48e3` |
| **Mr. Lippman** | Release | **CLEARED to tag after G1-G3 + G10 land** | `CHANGELOG.md`, `docs/release-notes-v2.0.0.md`, `docs/v2-cutover-checklist.md`; `3d3dcf8` |
| **Wilhelm** | Change management | **CLEAR** -- ADRs in place; rollback plan documented; gate matrix defined | `docs/v2-cutover-checklist.md` §4-§5, `docs/adr/ADR-006`/`-007`/`-008` |
| **Costanza (me)** | PM / product | **GO** -- rationale and conditions above | this document |

---

**Notes on changes left in the working tree (uncommitted, per the rules):**

- `docs/v2-cutover-decision.md` -- **new** (this file).
- `docs/release-notes-v2.0.0.md` -- wording fixes only:
  - "Performance & size" section rewritten to 1.456× / no waiver / +4 MB.
  - "Security" section: NOTICE claim softened to tarball + container in-band; brew/scoop/nix deferred to 2.0.1.
  - "Known limitations" AOT bullet rewritten (gate passes; 2.1 trim is forward work, not a limitation against 2.0.0).
  - "Upgrading / rolling back" pin instructions rewritten -- direct `--formula <URL>` / pinned JSON URL / `ghcr:1.9.1`, because native `@1.9.1` pin syntax requires packaging work that is 2.0.1 scope.

No code files touched. No commits. No tags. The working tree is ready for the fleet captain to review, fold in G1 and G3, and sign off.

-- Costanza
