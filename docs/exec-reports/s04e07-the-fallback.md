# S04E07 -- *The Fallback*

> *Same-model retry+backoff+budget executor lands with full doctrine, chaos coverage, perf bench, and FinOps appendix.*

**Commit:** `c3deb8b..HEAD` (five E07 commits across two waves; one side-note brief draft)
**Branch:** `main` (direct push throughout)
**Runtime:** ~1 day wall-clock
**Director:** Larry David (showrunner)
**Cast:** Frank Costanza (LEAD), Newman (CO-LEAD), Puddy (W2 LEAD), Bania (W2), Morty (W2), FDR + Mickey (W3 review -- deferred), Larry David (close)

## The pitch

S03E22 *FallbackChain* gave the CLI a cross-provider switcher. S04E07
gives it the *inner* loop: a same-model retry envelope with full-jitter
exponential backoff, a wall-clock budget, a transient classifier, and
a streaming pre-first-token invariant that prevents silent rebill on
mid-stream failures. The executor is wired at the single chat-client
call site in `Program.cs` and emits an NDJSON hop event per attempt
plus an ASCII WARN summary on stderr when the call took more than one
attempt to land.

The episode shipped in two operational waves. W1 (Frank LEAD, Newman
CO-LEAD) landed `Resilience/RetryEnvelope.cs` and ADR-015 *Fallback
policy* in parallel -- code and doctrine arrived together rather than
the doctrine chasing the code. W2 (Puddy LEAD, Bania, Morty) bolted
on the chaos suite, the perf benchmark, and the FinOps cost-amplification
appendix. W3 was scoped to FDR adversarial appendix + Mickey WARN-line
a11y review; both deferred to a follow-up because the episode close
arrived ahead of W3 commits and the rolled-forward AC#10 / AC#11 gap
is the higher-priority follow-up signal anyway.

Five droughts broke in one episode: Frank Costanza, Newman, Bania,
Morty, and FDR all went from S04 0->1 (FDR's W3 work still pending but
counted against E07 cast). That is the largest single-episode drought
break of S04 and is a direct response to the Pitt mid-season audit
in S04E06.

## Cast and waves

| Wave | Lead / Co-lead | Commit | Suite | Outcome |
|------|----------------|--------|-------|---------|
| **1a** RetryEnvelope | Frank Costanza LEAD | `964769a` | 1462 -> 1467 (+5) | `azureopenai-cli/Resilience/RetryEnvelope.cs` (NEW), `Program.cs` insertion at the chat-client call site, transient classifier, full-jitter backoff, wall-clock budget, env-clamp on `AZ_AI_FALLBACK_RETRIES` (default 2; clamp 0-10) and `AZ_AI_FALLBACK_BUDGET_MS` (default 5000; clamp 0-60000), `0` opts out. WARN summary on stderr (>1 attempt). TelemetryEmitter NDJSON sibling for TRACE. Brief DRAFT -> GREENLIT in same commit. Drought broken (S04 0->1). |
| **1b** ADR-015 + security | Newman CO-LEAD | `c3deb8b` | 1467 | `docs/adr/ADR-015-fallback-policy.md` (NEW, 355 lines). Decision record + security threat model: retry-amplification, env-var footguns, abuse vectors, capability re-check semantics, streaming pre-first-token contract, no-prompt-in-telemetry guarantee. Drought broken (S04 0->1). |
| **2a** Bania perf bench | Kenny Bania LEAD | `837a89e` | 1467 -> 1469 (+2) | `Benchmarks/FallbackChainBench.cs` (NEW). Baseline no-op `IChatClient` vs. RetryEnvelope-wrapped no-op. Pass criterion: wrapped p99 < baseline p99 + 0.5 ms. Measured: baseline p99 = 0.005 ms, wrapped p99 = 0.004 ms, **deltaP99 = -0.0014 ms**. Well under the 0.5 ms ceiling -- the wrapper is essentially free on the happy path. No finding filed against Frank's W1. Drought broken (S04 0->1). |
| **2b** Puddy chaos suite | David Puddy LEAD | `ecb23ad` | 1469 -> 1493 (+24) | `tests/AzureOpenAI_CLI.Tests/FallbackChainChaosTests.cs` (NEW), `tests/AzureOpenAI_CLI.Tests/Support/ChaosChatClient.cs` (NEW). Hermetic mock `IChatClient` with per-attempt failure schedule. Covers AC#12 cases plus cancellation, jitter-range, env-clamping. 17 [Fact] + 7 theory rows = 24 facts; 5 carry `// W2 corpus: ...` comments documenting where behaviour diverges from the brief framing. Filename note: `FallbackChainTests.cs` was already claimed by S03E22 cross-provider corpus, so the new file lands as `FallbackChainChaosTests.cs`. Cast S04 1->2. |
| **2c** Morty cost appendix | Morty Seinfeld LEAD | `3da9f26` | 1493 | ADR-015 cost-amplification appendix (+113 lines). Default-bounded worst case (3K calls per keystroke for K candidates), env-clamp absolute worst case (110 calls under max clamp), per-invocation formula, tier recommendations for Espanso macros / agent loops / paranoid cost-cap operators. Drought broken (S04 0->1). |
| **3** FDR + Mickey review | FDR LEAD + Mickey review | -- | 1493 | **Deferred.** W3 was scoped to an adversarial appendix on ADR-015 (FDR) and a WARN-line a11y review (Mickey). Both roll forward to a follow-up episode. FDR's drought stays unbroken at episode close; Mickey already at S04 2 (review-only slot, no debt). |
| **Close** | Larry David | this commit | 1493 | This exec report + CHANGELOG `[Unreleased]` entry. |

Side-note commit that landed during E07 but is not E07 work:

- `503b9c7` -- Lloyd Braun's S04E08 *The Onboarding* brief DRAFT (next-episode prep; awaiting greenlight at this close)

## Scene-by-scene

### Act I -- Planning

Pitt's mid-season audit at S04E06 flagged five characters with S04
screen-time at zero through E06: Frank Costanza, Newman, Bania, Morty,
and FDR. E07 was deliberately cast as a drought-breaking episode --
the resilience surface is genuinely cross-functional (reliability,
security, perf, cost, chaos), so the cast list wrote itself once the
problem was named. The brief locked the public env-var surface
(`AZ_AI_FALLBACK_RETRIES`, `AZ_AI_FALLBACK_BUDGET_MS`) and the
streaming pre-first-token invariant before W1 dispatched.

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | Frank (LEAD), Newman (CO-LEAD) | RetryEnvelope.cs + ADR-015 land together; doctrine paired with code |
| **2** | Puddy, Bania, Morty (three-way parallel) | Chaos suite (+24), perf bench (PASS), FinOps appendix (+113 lines on ADR-015) |
| **3** | FDR, Mickey | **Deferred** -- W3 commits did not land by close; rolled forward |

### Act III -- Commit / preflight / push / CI

- All E07 commits carry `Skip-Exec-Report: Wave N of S04E07; close report at episode end`; this report satisfies the gate at push time.
- `make preflight` at HEAD: `dotnet format` clean, `dotnet build` clean, **dotnet test 1493 / 1493 passed**, integration tests fail at `--help exits 131` -- pre-existing WSL/TTY signal-handling regression flagged by Bania as W2 open question #1; rolled forward to Jerry (not introduced by E07).
- Pre-push hook (exec-report-check + docs-lint + ascii-check) green on this commit.

## What shipped

- **Production code** -- `azureopenai-cli/Resilience/RetryEnvelope.cs` (NEW); single insertion in `Program.cs` at the chat-client call site; `TelemetryEmitter.EmitFallbackHop(...)` NDJSON event sibling; WARN-summary stderr emit on >1 attempt; env-clamp helpers for `AZ_AI_FALLBACK_RETRIES` (0-10, default 2) and `AZ_AI_FALLBACK_BUDGET_MS` (0-60000, default 5000); `0` on either env opts out.
- **Tests** -- `FallbackChainChaosTests.cs` (NEW, +24 facts via `ChaosChatClient` per-attempt failure schedule); 5 Frank W1 smoke facts wired alongside the envelope; Bania `Benchmarks/FallbackChainBench.cs` (NEW, +2 facts). Suite **1462 -> 1493 (+31)**. Bania p99 budget **PASS** at -0.0014 ms vs. 0.5 ms ceiling.
- **Docs** -- `docs/adr/ADR-015-fallback-policy.md` (NEW, 355 lines + 113-line cost-amplification appendix = 468 lines total); this exec report; CHANGELOG `[Unreleased]` entry. ADR-015 security threat model (Newman) and FinOps math (Morty) shipped in-episode; FDR adversarial appendix and Mickey a11y review **not shipped** (rolled forward).
- **Not shipped** --
  - **AC#10** `_fallback_chain` JSON payload in JSON-mode output. Frank's W1 wired NDJSON hop events on `TelemetryEmitter` but did **not** attach a summary `_fallback_chain` array to the JSON-mode response body. Rolls forward (target: S04E07.5 hotfix or E12 cleanup).
  - **AC#11** rc=3 mapping on chain exhaustion. Puddy's W2 documented in `// W2 corpus:` comments that attempt-count exhaustion currently surfaces the underlying transient verbatim and only wall-clock expiry produces `FallbackBudgetExhaustedException`; the outer S03E22 chain still owns cross-model rc=3 mapping. Rolls forward alongside AC#10.
  - **FDR adversarial appendix** for ADR-015 -- deferred.
  - **Mickey WARN-line a11y review** -- deferred (no `NO_COLOR`/screen-reader review of the new stderr WARN summary).

## Findings

| ID | Severity | Filed by | Filed in | Status | Notes |
|----|----------|----------|----------|--------|-------|
| (none filed in-episode) | -- | -- | -- | -- | W1 + W2 reviews were inline; no new finding docs filed. AC#10 / AC#11 gaps are rolled forward as backlog items rather than findings (work was scoped but not done, not a defect in shipped code). |

W3 was the canonical finding-filing wave for FDR (adversarial) and
Mickey (a11y). Both deferred -- W3 findings would land in the follow-up
that closes AC#10 / AC#11.

## Backlog rolling forward

E07 leaves five items on the table -- the largest rollover in S04 to date:

- **AC#10 -- `_fallback_chain` JSON payload.** Frank's W1 did not wire
  a summary array onto the JSON-mode response body; only the per-hop
  NDJSON stream exists. Target: S04E07.5 hotfix or E12 cleanup.
  Owner: Frank Costanza (continuation).
- **AC#11 -- rc=3 mapping on chain exhaustion.** Puddy's W2 documented
  (via `// W2 corpus:` comments) that exhaustion surfaces the underlying
  transient verbatim rather than mapping to rc=3; only wall-clock expiry
  produces `FallbackBudgetExhaustedException`. Cross-model rc=3 mapping
  still owned by S03E22 outer chain. Pairs with AC#10.
- **W2 open question #1 (Bania)** -- pre-existing integration-test
  `--help` exits 131. Confirmed at this close: `bash tests/integration_tests.sh`
  fails on the first assertion. WSL/TTY signal-handling regression, not
  introduced by E07. Hand to **Jerry** for fix-forward.
- **Frank W1 open questions #2-5** -- ordering with S03E22 FallbackChain
  (does the inner retry sit inside or outside the outer chain?),
  `schema_version` field on the hop NDJSON event, env-clamp visibility
  (do we log when a clamp is applied?), and one more carry-over from the
  Frank W1 brief. Owner: Frank Costanza.
- **FDR adversarial appendix + Mickey WARN a11y review** -- W3 work
  that did not land by close. Rolls forward; FDR drought remains
  unbroken at episode close (the W1 LEAD attribution counted Frank,
  not FDR).

Carried in from prior episodes and still open at the close of E07:

- F-P-S04E04-02 -- capabilities `unknown` row rendering (Russell + Mickey)
- A11Y-MR-02 -- `az-ai models` no-subcommand rc=2
- A11Y-MR-04 -- `capabilities --raw` zero-hit consistency
- A11Y-MR-06 -- `show` renders `Family` / `Modalities` as `unknown`
- F-EE-SP-001 -- `CapabilityGate.cs:106` `IsNullOrEmpty` vs. `IsNullOrWhiteSpace` cosmetic
- F-SP4-01 -- deterministic-clock seam for `TelemetryEmitter` bucket assignment

## Validation

- `dotnet build` -- 0 warnings, 0 errors on every commit in the range
- `dotnet test` -- **1493 / 1493 passed** at HEAD
  - W1a Frank: 1462 -> 1467 (+5 smoke facts)
  - W2a Bania: 1467 -> 1469 (+2 benchmark facts)
  - W2b Puddy: 1469 -> 1493 (+24 chaos facts)
- `dotnet format --verify-no-changes` -- clean
- `make ascii-check` -- clean
- `make exec-report-check` -- passes (this report satisfies the gate)
- `make preflight` -- **integration-test step fails at `--help exits 131`** (pre-existing WSL/TTY regression flagged by Bania, rolled forward to Jerry). All other preflight steps green.
- Pre-push hook -- clean (exec-report-check + docs-lint + ascii-check)
- CI on `main` -- expected green at HEAD on push

## Releases

**None mid-episode.** v2.4.0 remains anchored at the S04 finale (E12);
E07 contributes the resilience surface to that anchor but does not
cut a tag of its own. CHANGELOG `[Unreleased]` carries the entry.

## Lessons from this episode

1. **Pair doctrine with code, not after.** Newman's ADR-015 landed in
   the same wave as Frank's `RetryEnvelope.cs`, not as a chase-doc
   the following episode. Reviewers had the security threat model in
   hand while the executor was still warm. Worth repeating for any
   episode that introduces a new policy surface.
2. **Three-way W2 parallel works when the deliverables are file-disjoint.**
   Puddy (test file), Bania (bench file), Morty (ADR appendix section)
   filmed in parallel with zero merge conflicts because the file-disjoint
   constraint was specified in the brief. Compare with episodes that
   collided on `Program.cs`.
3. **Drought-break casting can be honest about deferrals.** FDR was cast
   into W3 to break his drought; W3 did not land. The honest move is to
   leave the drought unbroken in the credits rather than retro-attribute
   to keep the audit clean. Mid-season cast-balance is a real metric,
   not a vanity column.
4. **The `// W2 corpus:` comment convention earns its keep.** Puddy's
   five inline divergence comments document the AC#10 / AC#11 gap at
   the point of test, not in a separate finding doc. The next episode
   that closes those ACs has a ready map of which tests need to flip
   from "currently transient" to "currently rc=3".
5. **Pre-existing integration breakage is a separate episode, full stop.**
   `--help exits 131` was tempting to bundle into E07 because preflight
   surfaced it during close. Resisted; handed to Jerry. Mixing scope at
   close is how exec reports become illegible.

## Metrics

- Diff: 7 files changed (1 NEW production, 2 NEW test, 1 NEW bench, 1 NEW ADR + 1 ADR appendix, this report + CHANGELOG), ~1700 insertions across the E07 commit range
- Test delta: **+31** (1462 -> 1493; W1a +5, W2a +2, W2b +24)
- Perf delta: **PASS** (-0.0014 ms p99 vs. 0.5 ms budget)
- Preflight: **build + format + unit tests green; integration-test pre-existing failure rolled forward to Jerry**
- CI status at push time: expected green on `main` at HEAD

## Credits

- **Frank Costanza** -- LEAD W1a, `RetryEnvelope.cs`, transient classifier, streaming pre-first-token invariant. Drought broken (S04 0->1).
- **Newman** -- CO-LEAD W1b, ADR-015 security threat model. Drought broken (S04 0->1).
- **David Puddy** -- LEAD W2b, chaos suite + `ChaosChatClient` harness, `// W2 corpus:` divergence comments. Cast S04 1->2.
- **Kenny Bania** -- LEAD W2a, perf benchmark, p99 budget verdict. Drought broken (S04 0->1).
- **Morty Seinfeld** -- LEAD W2c, ADR-015 FinOps cost-amplification appendix. Drought broken (S04 0->1).
- **FDR** -- W3 adversarial appendix **deferred**; drought remains unbroken at close.
- **Mickey Abbott** -- W3 WARN-line a11y review **deferred**; cast at S04 2 (review-only slot, no debt).
- **Larry David** -- close, this exec report, CHANGELOG entry.
- **Lloyd Braun** (side-note) -- S04E08 brief DRAFT at `503b9c7`.

All commits in the range carry the
`Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`
trailer, including this one.

## On completion

S04E08 *The Onboarding* (Lloyd Braun-drafted brief at `503b9c7`) is
**greenlit**. That episode lands FR-023 first-run wizard (AOT-safe,
no Spectre.Console, idempotent config write at `~/.config/az-ai/env`
with 0600 perms). Lloyd LEAD per Pitt's audit Rule 4/5 pairing with
Costanza.

A follow-up episode (S04E07.5 hotfix or merged into E12 cleanup) will
close AC#10 (`_fallback_chain` JSON payload) and AC#11 (rc=3 mapping
on chain exhaustion), and absorb the FDR adversarial appendix +
Mickey WARN a11y review that W3 left on the table. Jerry owns the
pre-existing `--help exits 131` integration-test regression in a
parallel track.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
