# S02E03 -- *The Warn-Only Lie*

> *A CI step with a convincing alibi. Labels itself warn-only. Fails
> the build anyway. Three red runs before anyone read the label.*

**Commit:** `b36ec19` (workflow fix) + this report
**Branch:** `main`
**Runtime:** ~10 minutes
**Director:** Copilot (fleet orchestrator)
**Cast:** 2 agents (Kramer, Elaine), no background dispatches

## The pitch

`docs-lint.yml` had a dramatic-irony bug. The Summary step printed a
table labeling `markdownlint` as `warn-only` and closed with a
paragraph promising those warn-only checks would be flipped to
hard-fail "once the follow-up sweep todos close." Twelve lines up,
the markdownlint step's own header comment said the opposite:
"HARD-FAIL since Wave 6 (Soup Nazi baseline): zero-violation baseline
established. Do not reintroduce `continue-on-error`." The step had
no `continue-on-error`. It exited non-zero on violations. Three
commits this session proved it empirically -- `main` went red each
time the sweep missed a rule.

S02E02 flagged this as a lesson ("warn-only flags in CI are a lie if
the step exits non-zero") and deferred the cleanup. This episode
closes it.

## Scene-by-scene

### Act I -- Audit

Read `docs-lint.yml` end to end. Seven lint steps plus Summary. Three
bits of staleness:

1. File header: "All three core rules ship WARN-ONLY on first run"
   -- false since Wave 5 landed.
2. `markdownlint` row in the Summary table: labeled warn-only --
   false since Wave 6 landed.
3. Summary closing paragraph: promises a future flip that already
   happened.

The `filename-convention` step *is* genuinely warn-only and has
`continue-on-error: true`. It's a style preference, not a correctness
gate, and that's fine. The Summary never mentioned it.

### Act II -- `fix(docs-lint)` (`b36ec19`)

One commit, four edits, one file:

- File header comment rewritten to describe current state (four
  hard-fail checks, one warn-only by design).
- `filename-convention` step: added `id: filename-convention` and
  tightened "Warn-only initially" to "Warn-only by design."
- Summary table: flipped `markdownlint` row to `hard-fail`; added a
  `filename-convention (warn-only)` row so the table is complete and
  honest.
- Summary closing paragraph: replaced with a single accurate sentence
  -- "All checks except filename-convention fail the job on violation."

### Act III -- Ship

Committed, pushed, polled the Actions API. `docs-lint` green on
`b36ec19` within ~30 seconds of the push.

## What shipped

**Production code** -- none. Workflow-only change.

**Tests** -- none added. The workflow itself is the test; a green run
on the changed file proves the Summary still renders.

**Docs** -- this episode.

**Not shipped** (intentional follow-ups) -- none. Closes the only
debt S02E02 filed against `docs-lint.yml`.

## Lessons from this episode

1. **Stale comments in CI are worse than no comments.** They
   actively mislead the next person who reads them. A line that was
   true in Wave 4 and false in Wave 6 is a time bomb.
2. **When a step flips from warn-only to hard-fail, grep the whole
   file.** The rollout commit updated the step but missed the header
   and the Summary. A `rg warn-only .github/workflows/docs-lint.yml`
   would have caught it in 200ms.
3. **"The summary table is optional" is also a lie.** It's the first
   thing a failing-build reader sees. Wrong labels there burn more
   contributor-minutes than wrong labels in the step bodies.

## Metrics

- **Diff size:** 10 insertions, 8 deletions across 1 file (workflow).
- **Test delta:** 0.
- **Preflight:** not applicable (no `.cs` / `.csproj` / `.sln`
  changes; preflight skill is scoped to code commits).
- **CI at push time:** `docs-lint` green on `b36ec19`.

## Credits

- **Kramer** -- the workflow surgery.
- **Elaine** -- this writeup and the comment rewrites.
- **The Soup Nazi** -- noticed in S02E02 that the label was a lie.
  Three episodes later, we finally listened.
- **Co-author trailer:**
  `Copilot <223556219+Copilot@users.noreply.github.com>`

*-- end of episode --*
