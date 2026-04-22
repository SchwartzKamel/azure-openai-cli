# Docs Audit — Testing Segment (Puddy)

- **Date:** 2026-04-22
- **Auditor:** Puddy (QA)
- **Scope:** everything a contributor reads to learn *how we test*
- **Baseline:** v2.0.4 (`afa95fd`) — v1 **1025** tests + v2 **485** tests = **1510** green
- **Method:** every numeric claim grepped, every "how to run" cross-checked against `Makefile` / `.github/workflows/ci.yml` / actual `*.cs` fact counts
- **Verdict (one line):** Either the docs match the tests or they don't. **They don't.**

---

## 0. Headline

The testing docs were frozen at the **925-test v1-only era** and never caught up to the v2 split. `tests/README.md` still advertises "555+ tests"; reality is 1510. `make test` runs **only** the v1 project; CI runs both. New contributors who follow the docs literally will ship untested v2 code and think the suite is green.

Also: zero skip markers, zero `Trait("type","slow")` despite the BDD guide advertising them, three ship-blocker chaos findings (F1/F2/F3) flagged "NOT READY for v2.0.0" yet v2.0.4 is out — nothing documents how those were resolved.

## 1. Summary by severity

| Severity       | Count |
| -------------- | ----- |
| CRITICAL       | 2     |
| HIGH           | 6     |
| MEDIUM         | 7     |
| LOW            | 5     |
| Informational  | 4     |

Total: **24 findings**.

---

## 2. Test–doc alignment table (claim → reality → drift)

| # | Doc claim | File:line | Current reality | Drift |
|---|-----------|-----------|-----------------|-------|
| 1 | "454+ unit tests … 101+ integration tests … Total: 555+ tests" | `tests/README.md:3,12,15` | 1025 v1 + 485 v2 xUnit = **1510**; bash ≈ **174** assertions | **CRITICAL** — off by ~1000, omits entire v2 project |
| 2 | "**538 passing tests**" | `README.md:20` | 1510 | **HIGH** — user-facing landing page |
| 3 | "current baseline: 541 passing" | `docs/runbooks/release-runbook.md:33` | 1510 | **HIGH** — release checklist lies |
| 4 | "xUnit test suite has grown to **925 passing tests** across 19 files" | `docs/adr/ADR-003-behavior-driven-development.md:12` | 1025 v1 across 28 files + 485 v2 across 38 files | **MEDIUM** — ADR context is dated (accepted 2026-04-20) |
| 5 | "we do not mass-rename 925 methods" | `docs/adr/ADR-003:92` | same drift as #4 | **MEDIUM** |
| 6 | "All 19 `*.cs` files under `tests/AzureOpenAI_CLI.Tests/` (925 tests, ~8k LOC)" | `docs/testing/test-sanity-audit.md:4` | 28 `*.cs` in v1 + 38 in v2 (66 total); 1510 tests | **MEDIUM** — audit scope declaration itself is stale |
| 7 | `SecurityToolTests.cs` "104" tests | `tests/README.md:26` | 45 `[Fact]`/`[Theory]` methods (theory expansion may reach 104 — unverifiable without runner output) | **LOW** (ambiguous counting) |
| 8 | `ToolHardeningTests.cs` "33" tests | `tests/README.md:35` | 53 `[Fact]`/`[Theory]` | **MEDIUM** — 20-test undercount |
| 9 | `JsonSourceGeneratorTests.cs` "16" tests | `tests/README.md:34` | 23 `[Fact]`/`[Theory]` | **MEDIUM** |
| 10 | `RetryTests.cs` "36" tests | `tests/README.md:28` | 28 `[Fact]`/`[Theory]` methods | **LOW** (may be theory-expanded) |
| 11 | `DelegateTaskToolTests.cs` "16" tests | `tests/README.md:31` | 17 | **LOW** — off by 1 |
| 12 | `StreamingAgentLoopTests.cs` "21" tests | `tests/README.md:29` | 22 | **LOW** — off by 1 |
| 13 | `ParallelToolExecutionTests.cs` "11" tests | `tests/README.md:27` | 11 | ✅ matches |
| 14 | `PublishTargetTests.cs` "19" tests | `tests/README.md:30` | 19 | ✅ matches |
| 15 | `RalphModeTests.cs` "28" tests | `tests/README.md:32` | 28 | ✅ matches |
| 16 | `SecurityDocValidationTests.cs` "36" tests | `tests/README.md:33` | 36 | ✅ matches |

---

## 3. Findings

### CRITICAL

#### C1 — `tests/README.md` is a v1-only relic; v2 project is invisible
**File:** `tests/README.md:1-88`
**Problem:** The directory contains **two** test projects (`AzureOpenAI_CLI.Tests/`, `AzureOpenAI_CLI.V2.Tests/`). The README mentions only v1. 485 v2 tests — 32 % of the suite — have no documentation entry point. Ten v1 files (`CliParserTests`, `CliParserPropertyTests`, `CancellationTests`, `FoundryRoutingTests`, `SquadTests`, `SquadInitializerTests`, `Bdd/`, …) are also absent from the "Unit Test Files" table despite existing on disk.
**Proposed fix:** Rewrite the table with two sections (v1 / v2), regenerate counts from `dotnet test --list-tests` output, commit a script (`scripts/count-tests.sh`) that contributors can re-run. Top-line banner becomes "1510 xUnit tests (1025 v1 + 485 v2) + ~174 bash integration assertions". Delete the per-file count column entirely if nobody is willing to automate it — static numbers will drift again within a week.
**Severity:** CRITICAL

#### C2 — `make test` runs v1 only; docs treat it as the canonical command
**File:** `Makefile:176-178`, `tests/README.md:43-48`, `CONTRIBUTING.md:40,46`
**Problem:** `make test` = `dotnet test tests/AzureOpenAI_CLI.Tests/...`. v2 is never invoked. The preflight gate (`Makefile:218`) and `all-tests` (`Makefile:226`) inherit the defect. `CONTRIBUTING.md` Quickstart literally says "this is your 'is my tree sane?' check" and points at the same v1-only command. CI (`.github/workflows/ci.yml:55-59`) runs **both** projects — local green can be CI red with no warning.
**Proposed fix:** Change `test` target to run both projects (or add `test-v1` / `test-v2` / `test: test-v1 test-v2`). Update `CONTRIBUTING.md` Quickstart. Until the Makefile is fixed, add a banner to `tests/README.md` warning that `make test` does not cover v2.
**Severity:** CRITICAL

---

### HIGH

#### H1 — `README.md:20` advertises "538 passing tests" on the project landing page
**File:** `README.md:20`
**Problem:** 1510 actual vs 538 claimed. Anyone sizing the project via the README sees 1/3 the real coverage. Also: no mention that v1 and v2 are separate suites.
**Proposed fix:** "1510 passing tests (1025 v1 + 485 v2) + ~174 integration assertions". Or drop the number and link to `tests/README.md`.
**Severity:** HIGH

#### H2 — Release runbook baseline is 960 tests stale
**File:** `docs/runbooks/release-runbook.md:33`
**Problem:** "current baseline: 541 passing". Any release manager (Mr. Lippman) following this checklist will cheerfully pass a run that regressed 969 tests.
**Proposed fix:** Either delete the number (baseline = "whatever CI last reported on `main`") or auto-insert via `make` target that greps CI log. Static numbers in release runbooks are a category error.
**Severity:** HIGH

#### H3 — Preflight gate misses v2, so "preflight green" ≠ "CI green"
**File:** `Makefile:216-219`, `CONTRIBUTING.md:58-71`, `.github/skills/preflight.md` (by reference)
**Problem:** `preflight: format-check color-contract-lint dotnet-build test integration-test`. `test` is v1-only (see C2). CONTRIBUTING calls preflight "non-negotiable" and says "if it's green locally, CI will be green." Not true.
**Proposed fix:** Fix preflight to cover both projects. Audit `.github/skills/preflight.md` for the same drift.
**Severity:** HIGH

#### H4 — Chaos drill ship-blockers (F1/F2/F3) have no resolution record
**File:** `docs/chaos-drill-v2.md:14-59`
**Problem:** The drill declares **🔴 NOT READY for v2.0.0 cutover** based on three live reproducers in `PersonaMemory.cs`. v2.0.0, 2.0.1, 2.0.2, 2.0.3, and **2.0.4** have all shipped. No follow-up audit, no "closed by commit X" annotation, no regression tests cited. Future readers can't tell whether the findings were fixed, accepted, or forgotten.
**Proposed fix:** Add a "Resolution" section to the chaos drill with commit SHAs and test file:line pointers for F1/F2/F3 (regression coverage in `PersonaMemoryHardeningTests.cs` — verify it's there). If not fixed, the doc should say "deferred to vX.Y". Track in the chaos drill post-mortem or a dedicated `docs/audits/chaos-drill-v2-resolution.md`.
**Severity:** HIGH

#### H5 — Test-sanity audit follow-ups (C1, C2, C3, H2, H3, H6, H7) have no issue-tracker link or status
**File:** `docs/testing/test-sanity-audit.md:187-202`
**Problem:** Five follow-up items are listed as "issues to file" but no issue numbers, no PR references, no status column. Eight weeks later it's unknowable which were addressed. C1 (UserConfigTests mutating `~/.azureopenai-cli.json`) is a running-tests-destroys-your-config time bomb.
**Proposed fix:** Add a status table (open/in-progress/closed + PR SHA). If no issues were filed, file them now (Newman + Kramer). Add a CI lint for `Directory.SetCurrentDirectory` outside `[Collection("FilesystemCwd")]` as C2 proposed — enforcement was never added.
**Severity:** HIGH

#### H6 — BDD guide advertises `[Trait("type","slow")]` — zero tests actually use it
**File:** `docs/testing/bdd-guide.md:178-190`
**Problem:** The guide says CI filters on `type=slow`, `type=behavior`, `type=property`. Grep of the entire test tree shows only `type=behavior` (used) and `type=property` (used); **no** test uses `type=slow`, and no CI step filters by it. `RetryTests.WithRetryAsync_BackoffTimingIsExponential` (the 2.5s wall-clock test the audit H2 flagged) should be tagged `slow` per the guide's own recommendation and isn't.
**Proposed fix:** Either tag the slow tests (start with the sleep-based ones in `RetryTests`, `CancellationTests`) and wire a `make test-fast` target, or delete the `slow` row from the guide so it stops promising infrastructure that doesn't exist.
**Severity:** HIGH

---

### MEDIUM

#### M1 — `integration_tests.sh` claim "101+" is stale; real count ~174
**File:** `tests/integration_tests.sh` (header comment claims none, but `tests/README.md:12` does)
**Problem:** 174 `assert_*` / `pass` / `fail` sites today. "101+" undersells by 70 %.
**Proposed fix:** Drop the number; print it at run time (the script already totals `PASS + FAIL + SKIP`).
**Severity:** MEDIUM

#### M2 — ARCHITECTURE.md tree diagram omits v2 test project and 20+ v1 files
**File:** `ARCHITECTURE.md:704-712`
**Problem:** Tree shows `tests/AzureOpenAI_CLI.Tests/` with 5 files (`Program`, `UserConfig`, `Tool`, `JsonSourceGenerator`, `ToolHardening`). Reality: 28 v1 files + 38 v2 files. No mention of `AzureOpenAI_CLI.V2.Tests/` at all.
**Proposed fix:** Either enumerate accurately (churn risk) or collapse to `tests/AzureOpenAI_CLI.Tests/ # 28 files, 1025 tests` and `tests/AzureOpenAI_CLI.V2.Tests/ # 38 files, 485 tests`.
**Severity:** MEDIUM

#### M3 — No `docs/testing/README.md` / index
**File:** missing
**Problem:** `docs/testing/` has two files (`bdd-guide.md`, `test-sanity-audit.md`). No landing page explaining what's here, what's missing, and the testing philosophy. Contributors have to guess.
**Proposed fix:** Add a short `docs/testing/README.md` linking the two existing files + pointing at `tests/README.md`, `docs/chaos-drill-v2.md`, `docs/runbooks/release-runbook.md`, and the per-project `.csproj` locations.
**Severity:** MEDIUM

#### M4 — No TDD / flaky-test / contract-test documentation at all
**File:** missing (`docs/testing/` has zero files on any of these topics)
**Problem:** The Puddy mandate in `.github/agents/puddy.agent.md` commits to flaky-test hunting, regression tests, and contract tests (Ralph validator, tool-call contracts). Nothing under `docs/testing/` documents:
- how to run a flaky test under load to diagnose a race (no `make flake-hunt` target, no runbook)
- the Ralph validator contract (tool-call JSON shape, exit codes)
- how to add a regression test for a closed bug (should be in `CONTRIBUTING.md` but is only a one-liner in the PR checklist)
- what a "negative case" looks like (bdd-guide gestures at it in §7 but no examples of `ShellExec` adversarial contracts)
**Proposed fix:** Three new files under `docs/testing/`: `flaky-playbook.md`, `regression-tests.md`, `contract-tests-ralph.md`. Low-ceremony — 1 page each.
**Severity:** MEDIUM

#### M5 — CI test-gating policy is undocumented
**File:** `docs/runbooks/release-runbook.md`, `CONTRIBUTING.md`, `.github/workflows/ci.yml`
**Problem:** Nowhere is it written *which* test jobs are required merge-gates. Reading `ci.yml` shows `build-and-test` (matrix), `integration-test` (Linux only), more jobs — but no doc tells a contributor "these N jobs must be green to merge" and whether branch protection enforces that.
**Proposed fix:** Add a "CI gates" table to `CONTRIBUTING.md` or `docs/runbooks/release-runbook.md` listing each required check, its file, and what it blocks.
**Severity:** MEDIUM

#### M6 — `tests/chaos/README.md` doesn't list scenarios or run frequency
**File:** `tests/chaos/README.md:1-19`
**Problem:** The file tells you how to run `run_all.sh` but not *what* any of the 11 scripts cover. The scenario catalog exists in `docs/chaos-drill-v2.md` but `tests/chaos/README.md` doesn't link to it. Also: no cadence ("run per release", "run pre-v2.1"), so drills rot.
**Proposed fix:** One-table-per-script listing (`01_argv_injection.sh — argv fuzzing, owner FDR`), link up to `docs/chaos-drill-v2.md`, declare a cadence (pre-release gate? quarterly? FDR calls it).
**Severity:** MEDIUM

#### M7 — Integration test prerequisites are only clear from source code
**File:** `tests/integration_tests.sh:12-18`, `tests/README.md:53-58`
**Problem:** The script takes `V1_BIN` and `V2_BIN` env overrides and calls `dotnet run` if they're missing — prerequisite: **the projects must be built first**. `tests/README.md` and `CONTRIBUTING.md` don't say so; running `bash tests/integration_tests.sh` on a fresh clone will either fail or spend 30s JITting via `dotnet run`.
**Proposed fix:** Document the dependency ("run `make build` or `dotnet publish` first"). Or have the script auto-build if the binaries are missing. Currently, "no Azure credentials needed" is the only precondition documented — that's not enough.
**Severity:** MEDIUM

---

### LOW

#### L1 — Per-file test counts in `tests/README.md` drift silently
**File:** `tests/README.md:26-35`
**Problem:** See table §2 rows 7–12. Even if all of them match after a refresh, they'll re-drift on the next commit. Manual numbers in a README that nobody enforces.
**Proposed fix:** Delete the count column. Or generate from `dotnet test --list-tests` via a CI check.
**Severity:** LOW

#### L2 — `tests/README.md:84-88` CI integration bullet list omits container signing / SBOM / cosign test gates
**File:** `tests/README.md:79-88`
**Problem:** Lists unit/integration/format/audit/Trivy. `ci.yml` has more (workflow jobs beyond `build-and-test`). Not a test concern per se but the doc claims to cover "tests run automatically" — incomplete.
**Proposed fix:** Cross-reference `release.yml` or explicitly scope to `ci.yml`.
**Severity:** LOW

#### L3 — `docs/aot-trim-investigation.md:195` still celebrates "301 tests" + "1025 v1 tests"
**File:** `docs/aot-trim-investigation.md:195`, `:33`
**Problem:** "301 tests" refers to an earlier v2 size. Now 485. The 1025 figure is correct. Mixed-freshness claims read as authoritative.
**Proposed fix:** Either refresh or note "numbers as of commit X". Historical artefact — LOW.
**Severity:** LOW

#### L4 — `docs/launch/v2.0.0-blog-draft.md:87` says "488 / 488 v2 unit tests passing"
**File:** `docs/launch/v2.0.0-blog-draft.md:87`
**Problem:** 488 vs 485 current. Off-by-3 but it's a blog draft — it represents a frozen moment, which is arguably fine. Flag in case the draft gets reused verbatim.
**Proposed fix:** Add a "snapshot date" header; leave numbers alone.
**Severity:** LOW

#### L5 — `docs/announce/v1.8.0-launch.md:53` "538 tests passing" and `CHANGELOG.md:533` match — but neither dates the claim
**File:** see above
**Problem:** Historical announcements carry numbers that were true at the time. No timestamp on the claim inside the file text.
**Proposed fix:** None — announcements are historical records. Leave, but flagged so future audits know to skip them.
**Severity:** LOW

---

### Informational

#### I1 — No skip markers anywhere in the test tree
**Finding:** `grep -rn 'Skip\s*=\|SkippableFact\|SkippableTheory' tests/ --include='*.cs'` returns **zero results**. This is actually good — nothing is skipped long-term — but docs don't state the invariant. Worth a one-liner in `docs/testing/bdd-guide.md` or a new `docs/testing/README.md`: "no test is skipped; flaky ones are fixed or deleted (Puddy rule)."
**Severity:** Informational

#### I2 — `[Trait("type","property")]` is documented; only 1 class uses it
**Finding:** `CliParserPropertyTests.cs` alone. bdd-guide's §4 filter example (`dotnet test --filter 'type=property'`) matches exactly one class and eight tests. If that's the whole property-based coverage, the guide should say so.
**Severity:** Informational

#### I3 — `azureopenai-cli-v2` test project has an implicit convention for `V201*` / `V202*` / `Fr017*` file prefixes
**Finding:** Files like `V201ProgramPatchTests.cs`, `V202FollowupPatchTests.cs`, `Fr017RegressionTests.cs` encode version/FR origin in the class name. Not documented anywhere. New contributors will either mimic randomly or not at all.
**Proposed fix:** Document the naming convention (or abandon it).
**Severity:** Informational

#### I4 — `tests/integration_tests.sh` still has `run_v1_tests` "delete at cutover" marker
**Finding:** `tests/integration_tests.sh:12-18,78x` (final orchestrate line `# <-- delete this line at cutover`). Cutover happened at v2.0.0. v1 is maintenance-only per CONTRIBUTING. The deletion marker is stale guidance — v1 tests still run because v1 ships in releases.
**Proposed fix:** Rephrase the comment from "delete at cutover" to "delete when v1 binary ships its last release."
**Severity:** Informational

---

## 4. Coverage gaps the docs imply are tested

These are places where the docs claim or imply test coverage but the actual tests are absent or thin:

| Gap | Doc implying coverage | Reality |
|-----|----------------------|---------|
| `ShellExec` adversarial / stderr / non-zero-exit / binary output | `tests/README.md:27` ("built-in tool … edge cases"), `CONTRIBUTING.md:117` (test required) | Only `echo hello` success path; test-sanity-audit M8 flags this and has not been closed |
| Ralph validator contract (tool-call JSON shape) | `README.md:40`, `docs/cost-optimization.md:117` | `RalphModeTests`, `RalphExitCodeTests`, `RalphWorkflowTests` cover mode entry + exit codes; no contract-shape tests for the validator's JSON output |
| Console-capture sequencing | `docs/testing/test-sanity-audit.md:43` (C2) | `ConsoleCapture` collection attribute is used but there's no test that asserts capture ordering is deterministic under parallel xUnit — C2 remains open |
| Network failure / rate-limit handling | `.github/agents/puddy.agent.md` (mandate), chaos drill #9 | `docs/chaos-drill-v2.md:140` explicitly defers; no unit coverage of 429 retry-after parsing |
| Concurrent invocation safety | `.github/agents/puddy.agent.md` (mandate), `ParallelToolExecutionTests` | `ParallelToolExecutionTests` has no wall-clock assertion (audit H5 still open); concurrency is claimed, not asserted |
| Windows coverage | `docs/testing/test-sanity-audit.md:70` (H4) | `ci.yml:24-29` dropped `windows-latest`; no `[SkippableFact]` infrastructure exists; 26+ hardcoded POSIX paths unaddressed |
| `UserConfig` HOME isolation | `tests/README.md:25` ("config file loading, file permissions") | `UserConfigTests` still mutates `~/.azureopenai-cli.json` per audit C1; a crash between backup and restore destroys the user's config |

---

## 5. Recommended action order

1. **C2 + H3** — fix Makefile so `make test` and `make preflight` cover v2. One commit.
2. **C1** — rewrite `tests/README.md`. Drop per-file counts; keep totals; link both projects.
3. **H1 + H2** — fix the `README.md` landing number and the release-runbook baseline.
4. **H5** — file issues for the 5 still-open audit follow-ups with SHA references.
5. **H4** — resolution section on the chaos drill.
6. **M3 + M4** — add `docs/testing/README.md` and the three missing playbooks.
7. Everything else — cleanup batch.

---

## 6. Sign-off

**Pre-release QA sign-off for Mr. Lippman:** ❌ **NOT SIGNED.**
Fix C1, C2, H1, H2, H3 before tagging v2.1. The rest are not release blockers but reflect real drift.

*Either it works or it doesn't. Right now the docs say it works differently than it works.*

*— Puddy. High-five.*
