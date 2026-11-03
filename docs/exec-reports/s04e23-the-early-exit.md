# S04E23 -- *The Early Exit*

> *The check passed. The check failed. Nothing changed in between except how busy the machine was.*

**Commit:** `(this commit)` -- one code commit plus the episode report
**Branch:** `fix/s04e23-the-early-exit` (PR, off `main`)
**Runtime:** one session, fourth of the gap-fixing batch
**Director:** Larry David (showrunner seat, filled by a single orchestrator)
**Lead:** David Puddy (QA: a test that fails by itself is worse than no test)
**Cast:** 0 sub-agents, inline passes by one orchestrator. Puddy (LEAD), Jerry (guest: the CI gate scripts), Soup Nazi (guest: the guard that keeps it from coming back)

## The pitch

Three integration cases had each failed once, for no reason, on a busy machine, and passed on a rerun: bash completions, fish
completions, and the `--help` documentation check. S04E19's report called one a load flake and left it. S04E20 opened
`puddy-2026-11-P-4` as "cause unknown" and made the failure messages print what the binary had printed. The next failure
printed 3,979 characters that plainly contained both strings the check was looking for. The output was right; the check was wrong.

## Scene-by-scene

### Act I -- The output was right

The failure message did its job. The captured text started normally and was the same length as a standalone run, and a
standalone `grep` found both strings. So it was not the binary, and it was not the match. That left how the check ran:

```bash
printf '%s' "$out" | grep -qF -- 'why-this-model' && printf '%s' "$out" | grep -qF -- 'prefer'
```

in a script that sets `set -euo pipefail`.

### Act II -- Why a match can fail

`grep -q` exits the instant it finds a match. If `printf` is still writing, it is killed by SIGPIPE and exits 141, and
`pipefail` reports the whole pipeline as failed, so a successful match reads as a failed check. It only happens when the match
comes early and the output keeps coming, which is exactly the shape of a completions or `--help` check, and it needs the writer
and reader to be scheduled just so, which is why it only showed under load.

I measured it instead of arguing it. With the match on the first line and 24 busy loops on 8 cores, 1,000 trials per size:

| Output size | `grep -q` fails | Draining the input fails |
|---|---|---|
| 300 B | 7 (0.7%) | 0 |
| 3 KB | 11 (1.1%) | 0 |
| 30 KB | 50 (5%) | 0 |
| 120 KB | 886 (89%) | 0 |

With the match on the **last** line there were no failures at any size, because grep has to read everything before it can match:
that is why the bug hid for so long.

On the exact check that failed, with the real `hopper --help` output (12,613 characters): **43 of 2,000 failed on a quiet
machine, 69 and 90 of 2,000 under load, and 0 of 6,000 with the fix.**

### Act III -- An earlier measurement I got wrong

In S04E19 I ran the completions check 90 times, and while working on S04E20 I ran the `--help` check 40 times, and both showed
zero failures. At a 2% failure rate that was not strong evidence, but the worse problem is that those loops ran in a plain
shell without `pipefail`, so they could not have reproduced a pipefail failure at all. "Does not reproduce" was the wrong
conclusion from a test that could not fail. The failure message added in S04E20 is what broke the case open.

### Act IV -- The fix is at the consumer

Three options were rejected. Turning `pipefail` off hides real failures everywhere. Ignoring SIGPIPE makes `printf` report a write
error instead, which pipefail still reports. A here-string works but changes what an empty input means.

The fix is to never let `grep` exit early. In the integration suite a small function,
`grepq() { grep "$@" >/dev/null; }`, takes the arguments of `grep` without `-q` and reads all its input. 176 pipeline uses were
rewritten mechanically (173 plain plus 3 with an `LC_ALL=C` prefix), and the guard then found 5 more that a parallel PR (S04E22)
landed after the rewrite, which were converted the same way; every producer feeding them was checked first
(`printf` of a captured variable, `echo`, `head -1`, one binary's `--help`), because draining a long-lived producer would hang.
In the CI gate scripts the same thing is done at each site by dropping `-q` and discarding the output:
`check-color-contract.sh` (2), `exec-report-check.sh`, `pre-push.sh`, `license-audit.sh` (2), `sbom-generate.sh`.
`exec-report-check.sh` matters most: its input is `git log` over a whole range, so a spurious failure there makes CI demand an
exec report that a `Skip-Exec-Report` trailer had already excused.

### Act V -- Keep it from coming back

A guard case scans the suite and the five gate scripts for the pattern (comment lines excluded) and fails if one returns. It
caught its own message text on the first run, because the message contained the literal pattern, so the message was reworded and
the rule stayed. Another case runs `grepq` against 120 KB with an early match 300 times and requires zero failures, and four
cases pin its contract: a match is exit 0 and silent, no match is exit 1, empty input is no match, flags pass through.

## What shipped

- `grepq` and its use in `tests/integration_tests.sh`; the seven sites in the CI gate scripts.
- Six integration cases (189 to 195).
- **Mutation-checked:** seven mutations, each caught, each edit verified applied: `grepq` going back to `grep -q`, printing its
  match, and always succeeding; and the pattern returning in a gate script, in `check-color-contract.sh`, as an `LC_ALL=C` form,
  and as a plain pipeline in the suite. The mutations ran through a small harness built from the real file each time.
- CHANGELOG, the findings backlog and this report.

**Not shipped**

- The installer and setup scripts (`setup.sh`, `install-nim-gemma-2b.sh`, `uninstall-nim-gemma-2b.sh`) still have the pattern, on
  outputs of a few bytes and outside CI. That is `puddy-2026-11-P-5`, left alone rather than churned.
- The C# flake in `ProviderDispatchTests` (`puddy-2026-11-P-3`) is unrelated and still open.

## Lessons from this episode

1. **An unexplained flake needs better evidence, not a verdict.** The failure message that printed the captured output is what solved it.
2. **A reproduction that cannot fail proves nothing.** My earlier runs had no `pipefail`.
3. **Measure the rate, by size and by load.** "Rare" ranged from 0.7% to 89% depending on one variable.
4. **`pipefail` plus `grep -q` is a trap.** A pipeline's status includes the writer, and `-q` stops the reader early.
5. **Fix the consumer, then guard the pattern.** A guard that catches its own message is working.
6. **Check what feeds a pipe before changing how it is drained.** Draining a producer that never ends would hang.

## Metrics

- **Failure rate on the real check:** 2.2% (quiet) to 4.5% (loaded) before, 0 of 6,000 after.
- **Surface:** 181 uses in the suite (176, plus 5 the guard found after S04E22 merged), 7 in the gate scripts; 10 left in non-CI scripts (P-5).
- **Test delta:** integration 189 to 195 (+6) on its own base, 204 combined with S04E22; unit unchanged.
- **Preflight:** format, 0 warnings, unit, integration, `ascii-check`, `markdownlint`, `exec-report-check`, `color-contract-lint`.
- **CI status at push time:** pending at commit time; link in the PR.

## Credits

Puddy led because the subject is a test that lies. Jerry's lens is the gate scripts CI runs, and the Soup Nazi's is the guard.
Commits carry `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.
