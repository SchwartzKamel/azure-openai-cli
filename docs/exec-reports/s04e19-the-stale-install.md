# S04E19 -- *The Stale Install*

> *"Install newest" copied a new timestamp onto an old binary, and said so in a cheerful green line.*

**Commit:** `(this commit)` -- one build-system commit plus the episode report
**Branch:** `fix/s04e19-the-stale-install` (PR, off `main`)
**Runtime:** ~1 hour wall-clock, at the start of the gap-fixing batch
**Director:** Larry David (showrunner seat, filled by a single orchestrator)
**Lead:** Mr. Lippman (release manager: what an install is supposed to deliver)
**Cast:** 0 sub-agents, inline passes by one orchestrator. Lippman (LEAD), Jerry (co-lead: the Makefile), Puddy (the dry-run tests)

## The pitch

The owner said: install newest, then fix the gaps. `make install` ran, printed `Installed to ~/.local/bin/hopper`, and
the binary on disk had a new timestamp. It was the build from the day before.

## Scene-by-scene

### Act I -- A check that proved nothing

The first attempt at confirming the install was `strings hopper | grep ShellCommandScanner`, which returned zero. That
looked like a failure and was meaningless: Native AOT strips type names, so the count would be zero for a correct
build too. The check that does prove something is behavioural. The installed binary's own shell tool was asked to run
the bypasses from S04E15 and S04E17 against a harmless victim file:

| Command | Old install | After a real build |
|---|---|---|
| `env rm FILE` | file deleted | refused |
| `bash -c 'coproc rm FILE'` | file deleted | refused |
| `taskset -c 0 rm FILE` | file deleted | refused |
| `echo ok` | runs | runs |

### Act II -- Why `make install` lied

The rule that builds the binary was `dist/aot/hopper:` with **no prerequisites**, so make treated the file as up to date
whenever it existed. After any source change, `install` copied the old file to `~/.local/bin` and reported success. The
file's timestamp changed because `cp` sets it, which is exactly what made it look new.

It had been invisible because the first install on a clean tree builds. The second one, after a pull, does not.

### Act III -- The fix

`dist/aot/hopper` now depends on every file under `azureopenai-cli/` (excluding `obj/` and `bin/`) and on the
solution-wide MSBuild files, so make rebuilds when anything that can change the binary is newer than it. Editing a source
file, pulling a branch, or switching one all make the binary stale; a clean rebuild with nothing changed does not.

### Act IV -- Tests, and a failure that hid behind another

Three dry-run tests (so no one-minute AOT build is needed): an up-to-date binary is not rebuilt, a newer source file
triggers a rebuild, and build output in `obj/` and `bin/` does not count as a source change. The rebuild case fails
without the fix.

Mutation-checking found two things. First, my initial assertion looked for `dotnet publish`, a line that is never
reached in a throwaway directory because the recursive make cannot find a Makefile there; the planned step is `make
publish-aot`. Second, the "always rebuild" mutation was caught only by an *older* test, and then the whole suite died:
that older test writes into a directory the failed install never created, and `set -e` aborts. So a single failure was
hiding every test after it. The directory is now created first, and under the same mutation four tests report and the
suite finishes.

## What shipped

- `Makefile`: the AOT binary depends on its sources; the stale comment is corrected.
- `tests/integration_tests.sh`: 3 dry-run cases (integration 180 to 183), and the older install test no longer aborts the
  suite when an install fails. **Mutation-checked:** 3 mutations (no source tracking, `obj`/`bin` counted as source,
  always rebuild), each caught, and each edit verified to have applied.
- CHANGELOG, the findings backlog and this report.

**Not shipped**

- `make install` still runs `dotnet publish` for a full minute when something changed. That is the price of being correct.

## Lessons from this episode

1. **Verify an install by what it does, not by what it contains.** An AOT binary hides its names, so a string search
   proves nothing either way.
2. **A new timestamp is not a new build.** `cp` sets it.
3. **A rule with no prerequisites is a promise to never rebuild.** List what the output is made of.
4. **Test with a dry run.** It shows what make would do without paying for it.
5. **When a test passes under a mutation, ask which test caught it.** Here the answer exposed a suite that aborted on its first
   failure.
6. **Look for the line the test never reaches.** The assertion looked for output that could not occur in that directory.

## Metrics

- **Diff:** the Makefile, the integration tests, CHANGELOG, the findings backlog, the report and the index row.
- **Test delta:** integration 180 to 183 (+3), 2 skipped; unit unchanged at 2,235.
- **Preflight:** format, unit, integration, `ascii-check`, `markdownlint`, `exec-report-check`, `scripts-ignore-check`.
- **CI status at push time:** pending at commit time; link in the PR.

## Credits

Lippman led because the question is what an install delivers. Jerry owns the Makefile and the rule that had nothing to
depend on. Puddy's lens is the dry-run test, and the question of which test caught which mutation. Commits carry
`Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.
