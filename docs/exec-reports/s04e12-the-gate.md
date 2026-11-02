# S04E12 -- *The Gate*

> *A score becomes a merge gate, and the gate learns to say "I did not run" instead of going green.*

**Commit:** `(this commit)` -- one code commit plus the episode report
**Branch:** `feat/s04e12-the-gate` (PR, off `main`)
**Runtime:** ~4 hours wall-clock, with a name search running in a parallel workspace
**Director:** Larry David (showrunner seat, filled by a single orchestrator)
**Cast:** 0 sub-agents, 3 inline passes by one orchestrator across 2 jj workspaces. Jerry (LEAD, CI), The Soup Nazi (guest, merge gates and style), Frank Costanza (guest, reliability)

## The pitch

E10 built the scoreboard and E11 stocked it. E12 puts it in front of the merge button.
The blueprint line is "CI integration: `dotnet test` includes `az-ai eval` against
pinned model + temperature for goldens." Two facts reshaped that.

First, **this repository has no secrets**, so CI cannot reach a live model. Anything that
silently did nothing in that situation would be the vacuous-pass failure from E10 again:
a green check that means "nothing was checked." Second, "goldens" needs a concept the
runner did not have: not "the score is high enough" but "nothing that used to pass has
stopped passing."

So E12 is built around one rule, which Jerry states as the episode's thesis: **a gate that
cannot run must say so, and must be able to fail.**

## Scene-by-scene

### Act I -- Three missing pieces, and a rule

1. **`--repeat <n>`** runs each fixture *n* times. A fixture passes only if **every** run
   passes, and one that passes some runs but not all is reported as **flaky**, with how
   many runs passed. This is Frank's piece, and it is the first tool that can answer the
   standing question from E11: how stable is a real model on these corpora? A safety
   fixture that flakes is a safety failure; "refuses most of the time" is not a safety
   property.
2. **Baselines** (`--write-baseline`, `--baseline`). A baseline records per-fixture
   outcomes and nothing else: **never model text**, so it cannot leak output into the
   repository. A **regression** is a fixture that passed then and does not now (including
   failing, erroring, or becoming flaky). A regression fails the run even when the score
   clears `--fail-under`, because a threshold alone lets one fixture fail quietly while
   others improve.
3. **A fail-closed gate.** `scripts/eval-gate.sh`, the policy `.github/eval-gate.json`
   and `.github/workflows/eval-gate.yml`. With `EVAL_GATE_REQUIRED=true` a gate that cannot
   run (no credentials, no pinned model) **fails**. Otherwise it skips with a summary that
   says "nothing was checked. This is not a pass."

The Soup Nazi's contribution is the discipline around the gate itself: the policy lives in
one file, the safety floor (refusal at 100) is pinned by a test so lowering it is a
reviewed code change and not a quiet JSON edit, and every action in the workflow is pinned
to a commit SHA like the rest of the repo's workflows. His no-soup rule for the workflow
file: it must **never** use `pull_request_target`, which runs with secrets against code
from a fork, and it keeps read-only permissions. Both are tests.

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | orchestrator, workspace `gate` | `--repeat`, baselines, policy, script, workflow |
| **1** | orchestrator, workspace `name2` | A second, meaning-first name search ran in the background |
| **2** | orchestrator, workspace `gate` | 46 unit tests, five mutations, 6 integration cases, docs, findings, full gate |

No sub-agents were dispatched. The concurrency was real, and it also bit once (below).

**Proof the guards bite.** All 46 tests passed on the first run, so five deliberate
mutations were applied, each aimed at a different guard, and everything was restored
byte-identical:

| Mutation | What went red |
|----------|---------------|
| A fixture passes if *any* run passes | five tests, including the flaky, safety-flake and regression ones |
| An error is not counted as a regression | `Compare_ClassifiesEveryTransition` |
| Regressions are ignored by the gate | three tests, including the command round trip |
| An empty baseline is accepted | the bad-baseline refusal test |
| The workflow gains `pull_request_target` and the refusal floor drops to 90 | both policy guard tests |

The script was also run by hand against the real binary and the fake model. **Both
control cases behave:** a required gate with no credentials exits 1 with "Refusing to
report green without running", and an optional one exits 0 with a visible SKIPPED notice.
`--repeat` produced exactly the expected request counts (30 for smoke and refusal at two runs, 7x2 + 8x2, then 10 for persona-voice at one run), and
a hand-tampered baseline that claimed nine persona-voice fixtures once passed produced
**nine regressions and a failing exit**.

Four things went sideways:

- **A harness bug that made two tests lie.** `GATE_MODE=baseline ev_out=$(...)` is a
  statement made only of assignments, so the variable persisted and every later call
  stayed in baseline mode. The output literally said "baselines written" in a test that
  expected a comparison. Two tests failed, correctly. The mode is now exported inside the
  command substitution.
- **A shell idiom that misreported a tool.** `shellcheck x && echo clean || echo "no
  shellcheck"` printed "no shellcheck" when shellcheck was present and had found a
  problem, because the `||` fires on any non-zero exit. A leftover unused variable was the
  real finding. Do not use `A && B || C` to mean "if A then B else C".
- **A format failure caught by the gate** (whitespace in a test lambda), again.
- **Parallelism has a cost.** The name search ran three USPTO lookups at once while the
  full build and test gate also ran, and most lookups failed. They were re-run one at a
  time rather than treated as findings.

Two robustness issues in the script were found by reading it, not by a test: a stale
results file from a previous run could pollute a summary, and expanding an empty array
fails under `set -u` on the bash 3.2 that ships with macOS. Both fixed.

### Act III -- What is and is not established

**Established:** the runner logic (46 unit tests across repeat, baselines, the gate and the
policy, each guard mutation-checked), the script's behaviors against the real binary, the workflow
file's syntax (`actionlint` clean; the 15 findings repo-wide are all in `release.yml` and
exist on `main`), and the security properties of the workflow, pinned by tests.

**Not established, stated as plainly:**

- **The live path has never run.** No credentials, no pinned model, no committed baseline.
  The workflow will run in GitHub Actions on this very pull request (the eval code
  changed), and with no secrets it should report SKIPPED. That is the one real execution
  available, and it exercises only the skip path.
- **Real pass and flake rates are still unknown.** `--repeat` is the tool that will
  measure them; nobody has had a key to use it.
- **Cost is not capped.** The policy runs about 75 calls per run, weekly and on eval pull
  requests, with no budget guard.

**Gates.** `dotnet format` clean (after the fix), build 0 warnings, unit 1,937 of 1,937,
integration 149 of 149 (2 skipped, need live Azure credentials), `color-contract-lint`
clean, `ascii-check` clean, `markdownlint` clean across the repo, `actionlint` clean on the
new workflow, `shellcheck` clean on the new script.

## What shipped

**Production code**

- `Eval/EvalRunner.cs`: `--repeat` with flaky detection (a fixture passes only if every run
  passes), report fields for runs, passed runs and flaky count, and a regression gate in
  scoring that sits below the safety reason and above the score threshold.
- `Eval/EvalBaseline.cs` (new): outcome-only baselines, atomic writes, validated loads
  (missing, malformed, empty, oversized and unknown-status files are all refused), and the
  comparison that classifies regressions, improvements, additions and removals.
- `Program.cs`: `--repeat`, `--baseline`, `--write-baseline`; a bad baseline exits 2 before
  any model call; help text and completions for three shells.
- `scripts/eval-gate.sh`, `.github/eval-gate.json`, `.github/workflows/eval-gate.yml`.

**Tests**

- `EvalGateTests.cs`: 46 cases across repeat, flakiness, baselines, the gate, the command,
  and the policy, script and workflow guards (including the two security tests).
- `tests/integration_tests.sh`: 6 cases against the real binary and a loopback fake model.

**Docs**

- `README.md`, `docs/prompts/eval-harness.md`, `eval/baselines/README.md` (which states
  plainly that no baseline is committed and why), `CHANGELOG.md`, and 3 findings (all open).

**Not shipped**

- **A live run and committed baselines** (open, Jerry): needs a maintainer to set secrets
  and `EVAL_GATE_MODEL`.
- **A cost or token ceiling for the live gate** (open, Frank and Morty).
- **A guard that a new `scripts/` file is actually tracked** (open, Soup Nazi): the repo
  ignores `scripts/*`, so every new script needs a whitelist line.
- **The redaction pre-commit check** for contributor corpora: still the next episode.

## Lessons from this episode

1. **A gate that cannot run must say so, and must be able to fail.** Both are controls:
   the required-and-missing case fails, the optional case skips loudly.
2. **A threshold is not a regression gate.** One fixture can fail quietly while the score
   holds. Compare with what used to pass.
3. **Record outcomes, not output.** A baseline of model text is a leak waiting to be
   committed.
4. **Flaky means some runs passed.** Consistent failure is a different, simpler problem.
5. **Pin the files that decide what can merge.** The safety floor and the workflow's
   security posture are tests, so weakening them is a reviewed change.
6. **A statement made of assignments persists.** `VAR=x other=$(...)` does not scope `VAR`.
7. **`A && B || C` is not if-then-else.** It misreports when B fails or A fails loudly.
8. **Parallel work shares an upstream.** Three concurrent lookups against one endpoint
   plus a full build looked like a finding. Errors are not eliminations.
9. **State exactly what ran.** The live path has never executed, and every document that
   mentions the gate says so.

## Metrics

- **Diff:** 15 files changed, 1,349 insertions, 18 deletions (plus this report and the
  index row).
- **Test delta:** +46 unit (1,891 to 1,937), +6 integration (143 to 149).
- **Preflight:** format, build (0 warnings), unit, integration, color-contract,
  ascii-check, markdownlint, actionlint (new workflow) and shellcheck (new script) green;
  `exec-report-check` clean.
- **CI status at push time:** pending at commit time; link in the PR. The new workflow
  triggers on this PR and is expected to report SKIPPED.

## Credits

Jerry led and wrote the thesis. The Soup Nazi's gates are the policy test, the pinned
action SHAs and the ban on `pull_request_target`. Frank's `--repeat` is the first
instrument able to measure how stable a real model is. Cast-balance ledger after this
episode: Jerry holds his first S04 lead (Rule 2: not zero past the E12 line), and the E06
slate holds with E13 *The Redaction* (FDR lead of record, Newman co-lead, Jackie guest)
next. Commits carry `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.
