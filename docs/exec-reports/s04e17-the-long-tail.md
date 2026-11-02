# S04E17 -- *The Long Tail*

> *We shipped a guard, measured it at zero bypasses, and then asked a different model to try. It found one in an afternoon.*

**Commit:** `(this commit)` -- one code commit plus the episode report
**Branch:** `fix/s04e17-the-long-tail` (PR, off `main`)
**Runtime:** ~2 hours wall-clock, inside a longer field investigation of what hopper can do on this server
**Director:** Larry David (showrunner seat, filled by a single orchestrator)
**Lead:** FDR (adversarial red team)
**Cast:** 0 sub-agents, inline passes by one orchestrator. FDR (LEAD), Newman (co-lead), Kramer (the fix), Puddy (the lists). **Instrument, not cast:** hopper itself, driving `gpt-5.2-chat` and `gpt-5.4-mini` as independent reviewers

## The pitch

S04E15 rebuilt the `shell_exec` guard and measured it: 30 of 32 bypasses before, 0 after. That
claim was true of the list it was measured against, and it was written by the same author as the
guard. The S04E15 report said a denylist is never complete. This episode is the proof.

While surveying what hopper can do on this server, one of the experiments was to use it as an
independent reviewer. The code under review was the scanner. A different author, a different model
lineage, no shared assumptions: the closest thing available to the "non-author reviewer" that wgm's
review step asks for.

## Scene-by-scene

### Act I -- Grade the reviewer before trusting it

A cheap reviewer is only useful if you know how often it is wrong, so its claims were graded
mechanically. The model was asked for structured claims (an exact input, and whether it was a
`bypass` or a `false_positive`), and each input was run through the real scanner.

| Model | Claims | Consistent with the real scanner |
|---|---|---|
| `gpt-5.2-chat` | 10 | 10 (100%) |
| `gpt-5.4-mini` | 15 | 7 (47%) |

An earlier free-text review by `gpt-5.4-mini` had scored 1 correct finding in 5, and one of its
five findings described its own claim and then talked itself out of it. Both models scored 18 of
18 on an easy seeded-bug benchmark, so **the easy benchmark did not predict deep-review quality at
all.**

Most of what the better model reported was already documented: general-purpose interpreters
(`python -c`, `perl -e`, `awk system()`), script files, git aliases. Two claims (process
substitution) were refused by an earlier check in the full guard, so they were not bypasses. One
was new: `coproc rm FILE`.

### Act II -- Verify against a real shell, then widen

`coproc` is a bash keyword that runs a command in the background, and the scanner did not know it.
On this host `/bin/sh` is dash, so a bare `coproc` fails, but `bash -c 'coproc rm FILE; wait'`
reaches bash, deleted the file, and the tool returned "(no output)". Confirmed through the full
guard against a victim file.

One confirmed hole suggested the neighbours, so the family was measured the same way, each against
its own victim file through `ShellExecTool.ExecuteAsync`. Seven routes deleted the file:
`coproc`, `trap 'rm x' EXIT`, `taskset`, `chrt`, `strace`, `systemd-run` and `script -c`. Sourcing a
script file also did, which is the limit already documented. `unshare` and `setpriv` ran but needed
privileges.

### Act III -- The fix, and the cost

These commands do not share an argument shape, so modelling them option by option would have been a
second tower of special cases. They share one property: **they can run another command somewhere in
their arguments**. So the scanner got a second class, "exec-capable", for which every later word is
checked both as a command position and as a command line (a `trap` and a `script -c` take the command
as a string). `at` and `batch` joined the blocklist, because what arrives on their stdin runs later
where nothing can read it.

The price is stated and tested: `trap 'kill $pid' EXIT`, a common cleanup idiom, is now refused
because `kill` is blocked and the string is read. The workaround is a function.

## What shipped

**Production code**

- `ShellCommandScanner.cs`: the exec-capable class (coproc, trap, taskset, chrt, strace, ltrace,
  unshare, nsenter, setpriv, systemd-run, parallel, script).
- `ShellExecTool.cs`: `at` and `batch` on the blocklist.

**Tests**

- `ShellCommandScannerLongTailTests.cs`: 49 tests: 23 blocked forms, 15 ordinary uses that must still
  run, the stated fail-closed cost, and 9 end-to-end cases where the victim file must survive.
  Without the fix 34 of the 49 fail; with it all pass. **Mutation-checked:** 7 mutations, all caught,
  each edit verified to have applied, including one that refuses every exec-capable command (14 tests
  turn red, which is what the allowed list is for).
- Unit 2,183 to 2,232. Integration 169, unchanged.

**Docs**

- `SECURITY.md` and CHANGELOG, the findings backlog, this report.

**Not shipped**

- Containment. This is the same denylist with a longer list. `fdr-2026-11-F-4` stays open, and this
  episode is the second piece of evidence for it.

## Lessons from this episode

1. **A measurement you wrote is a measurement you are biased toward.** "0 of 32" was honest and
   incomplete, because the 32 were mine.
2. **Use a different model as the reviewer, and grade it.** The better model was right every time it
   made a checkable claim; the cheaper one was wrong more often than right. An easy benchmark could not
   tell them apart.
3. **Verify every claim against the system, not against the model.** Four of the five in the first
   review were wrong, and the one that mattered was only believed because a real shell deleted a file.
4. **One confirmed hole is a hypothesis about its neighbours.** Measure the family, not the example.
5. **Prefer a shared property to a table of shapes.** "Can run a command in its arguments" covered
   eleven tools with one rule.
6. **State what a fix costs.** The `trap` cleanup idiom is now refused, and a test says so.

## Metrics

- **Diff:** 2 production files, 1 new test file, `SECURITY.md`, CHANGELOG, the report, the index row
  and the findings backlog.
- **Bypass routes found by measurement:** 7 (plus the one documented limit); **after:** 0 of those 7.
- **Test delta:** unit 2,183 to 2,232 (+49). Integration 169, 2 skipped.
- **Preflight:** format, 0 warnings, unit, integration, `ascii-check`, `markdownlint`,
  `exec-report-check`.
- **CI status at push time:** pending at commit time; link in the PR.

## Credits

FDR led, because the finding is the red team's: a guard is only as good as the attempt to break it.
Newman owns the guard. Kramer's lens is the single rule that replaced a table. Puddy's is the allowed
list that kept "refuse everything" from passing. The reviewer model did the part nobody on the cast
could: it did not share the author's assumptions. Commits carry
`Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.
