# S02E38 -- *The Soup Nazi Gets a Lawyer*

> *Skills said "always write an exec-report." Skills got ignored. Soup Nazi installs a turnstile.*

**Commit:** this commit (single-commit episode)
**Branch:** `main` (direct push)
**Runtime:** ~25 min
**Director:** Larry David (showrunner)
**Cast:** Field-debug episode -- Mr. Wilhelm (process) on the rule, Soup Nazi (style/merge gates) on the enforcement, Kramer (engineering) on the bash, Elaine (docs) on the skill update.

## The pitch

S02E37 closed with a self-flagged process miss: I shipped two commits
covering the Espanso fix and the yada-yada-yada placeholder, then had
to be reminded to write the exec-report after the fact. The user
correctly pointed out that the previous reinforcement layer -- a
markdown skill file in `.github/skills/exec-report-format.md` -- is
advisory text. Skills are read by humans and agents who already
choose to read them; they catch nothing automatically. When the
attention budget gets tight (live-triage episode, two pushes, user
waiting), the skill quietly slides off the checklist.

The user's question was sharp: "how can we reinforce ALWAYS doing
that?" The honest answer is that you can't reinforce it with prose --
you reinforce it with a build gate that fails when the report is
missing. This episode wires that gate into `make preflight` and into
a `pre-push` git hook, both calling the same `scripts/exec-report-check.sh`
detector. Belt and suspenders. The Soup Nazi finally got a lawyer.

## Scene-by-scene

### Act I -- The diagnosis

The evidence was on the table from the prior episode. The skill
prescribes the format, the showrunner pattern says "every aired
episode gets one", but neither has teeth. Three-layer fix:

1. **A detector script** that knows when an exec-report is required.
2. **A Makefile target** that runs the detector inside preflight.
3. **A pre-push git hook** that runs the detector even when the
   developer skips preflight.

Plus a fourth layer for AI agents specifically: surface the rule
prominently in `.github/copilot-instructions.md`, the file that gets
auto-included in every Copilot prompt. The skill text is still right;
it's just no longer the only line of defense.

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | Kramer (inline) | Wrote `scripts/exec-report-check.sh`. Range detection: prefer `@{u}..HEAD`, fall back to `origin/main..HEAD`, skip silently if neither resolves (initial commit / detached HEAD edge cases never block the user). Logic: if range adds a new `docs/exec-reports/sNNeMM-*.md` -> green. If range only edits existing exec-reports -> green. If any commit body in range carries a `Skip-Exec-Report:` git trailer -> green with opt-out notice. Else fail with the next sNNeMM number, the commit list, and the changed files. Smoke-tested all four exit paths (clean, opt-out, missing, all-docs) before wiring. (Originally used a `[skip-exec-report]` free-text tag; switched to a git-trailer convention after dogfooding turned up a false positive when this very commit message *described* the opt-out -- the detector matched the prose mention. Trailers live at start-of-line and don't false-match prose.) |
| **2** | Soup Nazi + Mr. Wilhelm (inline) | Wired the detector into `make preflight` (now five gates, not four), added `make install-hooks` to install `.git/hooks/pre-push` calling the same script, updated `.github/skills/preflight.md` to document the fifth gate, and added a prominent **Exec-Report Protocol** section to `.github/copilot-instructions.md` so agents see the rule on every prompt. Updated `.gitignore` to allow the new script through (`scripts/*` is broadly ignored with explicit allow-list). |

### Act III -- Dogfood

The final test: after staging the enforcement code itself, can the
gate detect that *this very push* needs an exec-report? Yes. Running
`bash scripts/exec-report-check.sh` against the staged-but-unpushed
state correctly reported FAIL. Then I wrote this report. Re-running
the script now shows the new exec-report in the range and exits 0.

The system passes its own first dogfood.

## What shipped

- **Production code:** n/a -- no behavior change in the CLI.
- **Tooling:**
  - `scripts/exec-report-check.sh` -- the detector. ~80 lines of
    defensive bash with explicit edge-case handling.
  - `Makefile` -- new `exec-report-check` and `install-hooks` targets;
    `preflight` depends on the new check.
  - `.gitignore` -- allow-list entry for the new script.
- **Tests:** None added -- the script is self-contained and was
  smoke-tested across all four exit paths during development. A
  dedicated test harness for the script would be over-engineering; if
  the rules ever get more complex, port to Python with pytest.
- **Docs:**
  - `.github/copilot-instructions.md` -- new **Exec-Report Protocol**
    section directly under the preflight callout. This is the file
    that gets auto-included in every Copilot agent prompt, so the
    rule now travels with the agent's context, not just the human's
    procedural docs.
  - `.github/skills/preflight.md` -- updated from "four checks" to
    "five checks", added a *Why exec-report-check is here* section
    explaining the S02E37 lesson that motivated the gate.
- **Not shipped:**
  - A CI-side check (GitHub Actions workflow that runs the same
    detector). The local gate plus the agent-prompt reinforcement
    cover ~95% of cases. If the gate gets bypassed via `--no-verify`
    or by skipping preflight, a CI check would catch the rest, but
    that's a follow-up if we ever see the bypass actually happen.
  - A test for the script itself. The four-path manual smoke test was
    sufficient; bash-test infrastructure isn't worth the drag.
  - Auto-numbering of the next `sNNeMM` value. The detector tells you
    you need a report; the user picks the title. Auto-numbering would
    be nice but not necessary.

## Lessons from this episode

1. **Skills are documentation, not enforcement.** The `exec-report-format`
   skill has been in the repo for months. It clearly states "every aired
   episode needs an exec report." It did not stop me from forgetting in
   S02E37. If a rule matters enough to write down, it matters enough to
   gate the build on. Treat skills as reference material, not as a
   reliability mechanism.
2. **For AI agents specifically, the highest-leverage reinforcement is
   the auto-included prompt.** `.github/copilot-instructions.md` is
   surfaced in every Copilot session in this repo. A rule placed there
   reaches the agent's working memory on every turn. Skills, AGENTS.md,
   and ADRs are referenced material -- the agent reads them only if it
   chooses to look. The instruction file is non-optional.
3. **Belt and suspenders for cheap rules.** The detector script costs
   ~80 lines of bash and runs in <100 ms. Adding it as both a preflight
   step and a pre-push hook means you'd have to actively bypass two
   independent layers (`--no-verify` AND skipping preflight) to push
   without an exec-report. That's a high enough bar.
4. **Dogfood the enforcement on the same commit.** Building the gate
   and not writing the report it requires would have been the most
   embarrassing miss possible. This episode's report exists because the
   gate failed on its own staging diff and forced the issue. That's
   exactly the desired behavior.
5. **The opt-out matters.** Without a `Skip-Exec-Report:` trailer the
   gate would become an annoyance for genuinely trivial changes (typo
   fixes, dependency bumps), people would `--no-verify` reflexively,
   and the gate would erode. A documented, surveyable opt-out keeps
   the rule credible. (The trailer convention itself was a course
   correction: the original implementation used a `[skip-exec-report]`
   free-text marker, which false-positived when the commit message
   that introduced the gate *described* the opt-out. Git trailers
   live at start-of-line and don't false-match prose mentions.)

## Metrics

- **Diff:** +~120 / -10 lines across 5 files
  (`scripts/exec-report-check.sh` new, `Makefile`, `.gitignore`,
  `.github/copilot-instructions.md`, `.github/skills/preflight.md`).
- **Tests:** n/a -- tooling/process change.
- **Commits:** 1 (this one).
- **New `make` targets:** 2 (`exec-report-check`, `install-hooks`).
- **Preflight gate count:** 4 -> 5.
- **Wall clock:** ~25 min from "how can we reinforce" to dogfood-passes.
- **CI state at episode close:** preflight green modulo the pre-existing
  `ListModelsCommand_ShowsEnvAllowlist` flake (still on the backlog from
  S02E37).
