# S04SP10 -- *The Housekeeping*

> *Three loose ends, each small, each closed by looking at the thing and not by saying so.*

**Commit:** `(this commit)` -- docs plus a new check script and its tests
**Branch:** `chore/s04sp10-housekeeping` (PR, off `main`)
**Runtime:** ~1.5 hours wall-clock, in a separate jj workspace
**Director:** Larry David (showrunner seat, filled by a single orchestrator)
**Lead:** Mr. Wilhelm (process and change management)
**Cast:** 0 sub-agents, inline passes by one orchestrator. Wilhelm (LEAD), The Soup Nazi (co-lead: owns the gitignore trap), Jerry (guest: the AOT build), Elaine (guest: the proposals index)

## The pitch

Three open findings were each too small for an episode and each easy to leave open for ever:

- `jerry-2026-11-JE-1`: the embedded eval corpora had only been exercised in a normal build, never
  in a Native AOT publish, where embedded resources and JSON can go missing.
- `wilhelm-2026-11-W-3`: the proposals index had no rows for FR-021, FR-022 or FR-023.
- `soup-nazi-2026-11-SN-1`: `.gitignore` carries `scripts/*`, so a new script is silently ignored.

## Scene-by-scene

### Act I -- JE-1: run it, do not reason about it

The binary installed by S04E14's `make install` is a real Native AOT build: an ELF executable with
no `hopper.dll` beside it. `hopper --list-corpora` read all three corpora from its embedded
resources and parsed them (fixture counts, safety counts, byte sizes). Then the integration
suite's own fake model server, extracted verbatim, let the AOT binary run each corpus end to end:
smoke 7 of 7, the refusal fixtures passing, persona-voice scoring low against the fake server's
canned reply (as expected; the gate was `--fail-under 0`). Embedding, deserialisation and the
assertion engine all work under AOT. No code changed; the finding closes on evidence.

### Act II -- W-3: the status has to be true

The three missing rows were easy to write and easy to write wrongly. FR-022 and FR-023 are
marked "Proposed" in their own documents, but the wizard they describe exists. The commit history
and the code show what shipped and what did not: the `--setup` wizard (S03E11) and
`--rotate-creds` (S03E25) did; the per-OS credential storage (Keychain, DPAPI, libsecret) did not,
because keys are plaintext files with mode 0600. So both are **partial**, not "proposed" and not
"shipped". FR-021 shipped in 2.0.1 (`e07697e`).

Checking those turned up a worse one: FR-007 (connection prewarming) is listed as "PLANNED" while
`--prewarm` and its tests exist. The index header claimed its rows were verified status. It now
says only what is true: FR-021 to FR-023 and FR-007 are verified today, the rest were last checked
on 2026-04-24 and may be stale. That is a new finding, `W-4`, not a silent mass edit.

### Act III -- SN-1: find the signal that is not noise

`scripts/*` is not a mistake. It sits under the `# roo` heading and hides scratch files that
agent tools leave there. So removing it would trade one trap for clutter, and a check that warned
about every ignored script would be ignored by everyone within a week.

The signal that separates a real script from scratch is that **something tracked asks for it**.
`check-ignored-scripts.sh` fails when an untracked, ignored file under `scripts/` is referenced by
a tracked build file: the Makefile, a workflow, a test, another script. A clean checkout would
break, so it is a hard failure with the exact fix in the message (`!scripts/<name>`). Docs are
excluded as evidence on purpose, because they mention scripts that were never meant to be
committed. It runs in `make preflight` and in the pre-push hook, before the range logic, since
it does not depend on what is being pushed.

### Act IV -- Tests, and a harness that refuses to lie

Six integration cases run in throwaway git repos (so they do not depend on this repository's
state): the referenced case fails and names the fix; whitelisting clears it; an unreferenced
scratch script is left alone; a docs-only mention does not count; a workflow reference does; and
this repository is clean. Five mutations were each caught. The harness checks that every edit
actually applied before running, because in S04E16 an edit that silently did not apply looked
exactly like a mutation that survived.

### Act V -- A flake, and not claiming a fix

While this batch was in flight, S04E16's CI run failed once in a test it had nothing to do with
(`ProviderDispatchTests`: `BuildChatClient` returned null) and passed on rerun. The code points at
a cause: `BuildChatClient` returns null when offline mode or the endpoint allowlist rejects the
endpoint, `EndpointAllowlistTests` and `OfflineModeTests` set those environment variables, and
`ProviderDispatchTests` and `EndpointAllowlistTests` mutate the process environment with no
`[Collection]`. So it is a plausible race.

It could not be reproduced: 0 failures in 77 local runs, including the whole suite pinned to two
cores to imitate a CI runner. A fix that cannot be shown to fix anything is a guess, so none was
made. The finding (`puddy-2026-11-P-3`) records the evidence, the suspected cause and what would
close it, and the tests are unchanged.

A small repair rode along: `.github/skills/preflight.md` said "The seven checks" and listed eight.
It now says nine, with the new gate in its place, and `CONTRIBUTING.md` lists it too.

## What shipped

**Tooling**

- `scripts/check-ignored-scripts.sh` (new), `make scripts-ignore-check`, wired into `make preflight`
  and the pre-push hook, with its own whitelist line in `.gitignore`.

**Tests**

- 6 integration cases, mutation-checked. Integration 163 to 169, 2 skipped. No unit test changed.

**Docs**

- `docs/proposals/README.md`: FR-021 to FR-023 added, FR-007 corrected, the header made honest.
- `.github/skills/preflight.md`, `CONTRIBUTING.md`, the findings backlog and this report.

**Not shipped**

- A verification pass over the other proposal rows (`wilhelm-2026-11-W-4`).
- A fix for the `ProviderDispatchTests` flake (`puddy-2026-11-P-3`): a cause is suspected but not shown.
- Any change to the `scripts/*` rule itself. It does its job; the failure mode is now visible.

## Lessons from this episode

1. **Close a finding with evidence, not reasoning.** Running the AOT binary took a minute and
   removed the doubt for good.
2. **A status line is a claim.** Check the code before you write "shipped" or "proposed", and say
   so when you checked only some rows.
3. **Do not remove a rule you do not understand; make its failure visible.** The `scripts/*` rule
   had a purpose.
4. **A check that cries wolf is worse than none.** Requiring a reference from tracked build files
   is what keeps this one quiet.
5. **A harness that cannot fail teaches nothing.** Refuse to run when the mutation did not apply.
6. **Do not fix what you cannot reproduce.** Record the evidence and the suspect, and leave the
   tests alone until a failure can be shown to go away.

## Metrics

- **Diff:** 1 new script, a Makefile target, the pre-push hook, `.gitignore` (one line), the
  integration tests (6 cases), 4 markdown files, this report, the index row and the backlog.
- **Test delta:** integration 163 to 169 (+6), 2 skipped; unit unchanged at 2,183.
- **Preflight:** format, 0 warnings, unit, integration, `scripts-ignore-check`, `ascii-check`,
  `markdownlint`, `exec-report-check`.
- **CI status at push time:** pending at commit time; link in the PR.

## Credits

Wilhelm led, because the common thread is that a statement in a document should be checkable.
The Soup Nazi's finding is the best of the three: it named a trap and left the design to whoever
fixed it. Jerry's lens is the AOT check. Elaine's is why the index rows say what the code does.
Commits carry `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.
