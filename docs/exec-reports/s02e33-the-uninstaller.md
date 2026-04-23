# S02E33 -- *The Uninstaller*

> *A dev upgrades to v2. A 2024 alias silently wins PATH. One command finds it; a second, gated, removes it.*

**Commit:** `<pending>`
**Branch:** `main` (direct push)
**Runtime:** ~25 min wall-clock
**Director:** Larry David (showrunner)
**Cast:** 1 sub-agent (Jerry lead), 1 guest voice (Lloyd Braun on the
migration-doc subsection)

## The pitch

v2 installs the AOT binary at `~/.local/bin/az-ai`. That is a clean landing
spot - except when it isn't. A v1 user who ran `make alias` back in 2024,
or who dropped a `docker run --rm ghcr.io/schwartzkamel/azure-openai-cli:1.9.1`
one-liner into `~/.bashrc` and forgot about it, now has a shell alias that
silently shadows the new binary. They invoke `az-ai`, the alias wins, docker
spins up a v1.9.1 image, and v2 looks broken. It isn't. The alias is.

This episode ships two Makefile targets that make that situation
self-diagnosing and self-correcting: `make migrate-check` (read-only scan,
safe to run any time, exits 1 if it finds anything stale so CI can gate on
it) and `make migrate-clean` (dry-run by default, `FORCE=1` to apply, with
timestamped rc-file backups). Plus a short Lloyd-voice pointer in the v1->v2
migration doc so users actually find them.

Jerry's lead-floor corrective for S02: the season owed a second Jerry-led
episode and this is it. Pure DevOps - no C#, no Dockerfile, no csproj touched.

## Scene-by-scene

### Act I -- Planning

- Read [`shared-file-protocol`](../../.github/skills/shared-file-protocol.md) first.
  Working tree was clean at `b913617`, no concurrent agents in flight, so
  exclusive Makefile access - still staged explicit paths, never `git add -A`.
- Two design decisions worth naming:
  1. **Detection heuristic for shell rc files.** Match lines that are
     `alias az-ai=...`, `az-ai() {...}`, `function az-ai ...`, or an
     `export ...AZ[_-]AI...` env line. Kept as a Make variable
     (`MIGRATE_ALIAS_PATTERN`) so `migrate-check` and `migrate-clean` stay
     in lockstep - if the regex drifts between them, one will grep hits the
     other can't sed out. One source of truth.
  2. **"Binary is stale if version does not match 2.x."** Cheap and correct:
     runs `<path> --version 2>/dev/null || echo unknown` and greps for `2.`
     in the output. An unknown or v1 version flags. `--version` has been
     stable across v1.x and v2.x (see migration doc section 2).
- **`FORCE=1` gate rationale.** Dry-run by default means a nervous user can
  run `make migrate-clean` with zero risk and see exactly what would change.
  `FORCE=1` is a single, obvious, unambiguous environment variable - no
  `--apply`, no `-y`, no `--yes-i-really-mean-it`. It matches the style
  already established in other cautious-action targets in this codebase.
- **Why `~/.local/bin/az-ai` is never auto-removed.** That is where v2
  installs. If the user has v1.9.1 sitting there because they never re-ran
  `make install` post-upgrade, the check flags it as stale - but the clean
  pass refuses to delete it. They can either `make install` (overwrites) or
  `rm` it themselves. Nuking the v2 install path automatically is a foot-gun.
- **Why Docker images are print-only, never auto-removed.** `docker rmi` on
  a tag can cascade if an image ID is shared across tags. Cross-tag risk is
  real. Print the command, let the user run it.

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | jerry (lead), lloyd-braun (guest voice on `docs/migration-v1-to-v2.md` section 4 copy only) | Both Makefile targets, help + PHONY updates, doc cross-link, exec report. One commit. |

Single wave. The episode is small enough that splitting it would be ceremony.

### Act III -- Ship

Verification commands (run pre-commit, paste verbatim):

```text
$ make -n migrate-check >/dev/null && echo OK-dryrun
OK-dryrun

$ make help | grep -E 'migrate-(check|clean)'
  make migrate-check - Scan shell rc files, binaries, and Docker images for stale v1 'az-ai' leftovers (read-only)
  make migrate-clean - Remove stale v1 leftovers. Dry-run by default; re-run with FORCE=1 to apply.

$ grep -E '^\.PHONY' Makefile | head -1 ; grep -E 'migrate-check migrate-clean' Makefile | head -1
.PHONY: all build dotnet-build run clean alias scan test integration-test ...
	migrate-check migrate-clean \

$ make migrate-check
== Shell aliases ==
  (0 hit(s))

== Installed binaries ==
  /home/tweber/.local/bin/az-ai: Azure OpenAI CLI v1.9.1 (STALE)
  (1 stale)

== Docker images ==
  (docker not on PATH - skipped)
  (0 stale)

Stale artifacts found: 1
(exit 2 from make's wrapping; recipe exit was 1, as specified)

$ make migrate-clean    # dry-run
Dry-run. Re-run with `FORCE=1 make migrate-clean` to apply.
... (migrate-check output) ...
Stale artifacts found: 1
(exit 0)
```

Positive-path functional test run against a disposable `HOME` seeded with a
realistic `.bashrc` (alias + function + unrelated `export PATH=` line):
`FORCE=1 make migrate-clean` removed the alias and function lines, left the
unrelated `export PATH=...` untouched, created `~/.bashrc.bak-azai-<stamp>`,
and a follow-up `migrate-check` reported `Clean - nothing to migrate.` (exit
0). Test fixture torn down.

No preflight required per [`preflight.md`](../../.github/skills/preflight.md) -
zero `.cs` / `.csproj` / `.sln` / workflow / Dockerfile touches. `make help`
parses cleanly.

Commit + push to `origin/main` with `-c commit.gpgsign=false` and the
`Co-authored-by: Copilot` trailer per [`commit.md`](../../.github/skills/commit.md).
Explicit paths only: `Makefile docs/migration-v1-to-v2.md
docs/exec-reports/s02e33-the-uninstaller.md`.

## What shipped

**Production code** -- n/a. This episode is DevOps / Makefile.

- `Makefile`: two new targets (`migrate-check`, `migrate-clean`), one new
  Make variable (`MIGRATE_ALIAS_PATTERN`) shared between them, two new
  `.PHONY` entries, two new `make help` lines under the Native-install
  section. No existing targets modified.

**Tests** -- n/a (no unit-test harness for Makefile logic in this repo).
Verified by hand against a disposable `$HOME` fixture with alias + function
+ unrelated shell line; confirmed the detection regex catches what it
should and leaves unrelated `export PATH=...` alone.

**Docs** --

- `docs/migration-v1-to-v2.md`: new section 4 *Cleaning up v1 leftovers*
  (Lloyd-voice, plain, un-scary, explicit opt-out for users with nothing to
  clean). Downstream sections renumbered 5-8.
- `docs/exec-reports/s02e33-the-uninstaller.md`: this file.

**Not shipped** (intentional follow-ups, backlog material for Larry) --

- **Windows PowerShell counterpart.** Out of scope per brief. Natural
  off-roster episode: detect stale v1 entries in `$PROFILE`, scan
  `%LOCALAPPDATA%\Microsoft\WindowsApps` and scoop shims, same `FORCE=1`
  gate. Lead candidate: Kramer (Windows eye) with Jerry as guest for the
  `FORCE=1` pattern match.
- **Auto-remove Docker images when safe.** Could be done with
  `docker image inspect` to check for multi-tag references before calling
  `docker rmi`. Deferred - print-and-let-user-run is the low-risk default
  and the brief was explicit about it.
- **The v1.9.1 binary sitting at `~/.local/bin/az-ai` on the dev box.**
  `migrate-check` correctly flags it as stale, and by design
  `migrate-clean` refuses to delete it (that is the v2 install path). The
  dev needs to re-run `make install` post-upgrade; no code change needed.
  Noted here so Larry sees it in the writers' room.

## Lessons from this episode

1. **Share the regex, not the copy-paste.** The first draft had the
   detection regex duplicated between check and clean. That is a timebomb:
   someone eventually tweaks one, the other drifts, and `migrate-clean`
   leaves behind lines that `migrate-check` still flags. Lifting it into
   `MIGRATE_ALIAS_PATTERN` is a one-line hygiene fix that pays off forever.
2. **Recursive `$(MAKE)` inside a shell line leaks stderr even with
   `|| true`.** The sub-make's `make[1]: *** Error 1` noise goes to the
   parent's stderr before the shell-level `|| true` gets a chance. Fix is
   `2>/dev/null` on the sub-make specifically - safe here because the
   recipe's real output is all on stdout. Worth adding to a future
   `makefile-patterns` skill if this pattern comes up again.
3. **Dry-run-by-default + single-env-var gate is the right shape for
   destructive targets.** No flags, no confirmation prompts, no interactive
   TTY assumptions (which would break in CI and Espanso). `FORCE=1` reads
   at a glance and composes with `make -n` / `make help` cleanly.
4. **Lloyd's one-liner earns its keep.** "If you never touched `make install`
   or your shell rc, you have nothing to clean - skip this section." That
   sentence is the whole reason this section won't scare upgraders. Cheap
   win, worth naming the guest credit for it.

## Metrics

- **Diff size:** 135 insertions / 4 deletions / 3 files
  (`Makefile`: +111/-0; `docs/migration-v1-to-v2.md`: +24/-4;
  `docs/exec-reports/s02e33-the-uninstaller.md`: NEW).
- **Test delta:** n/a (no automated tests added; manual functional test
  against a disposable `$HOME` fixture passed).
- **Preflight result:** skipped per [`preflight.md`](../../.github/skills/preflight.md)
  (no `.cs` / `.csproj` / `.sln` / workflow / Dockerfile changes). `make help`
  parse-check green; `make -n migrate-check` dry-run green.
- **CI status at push time:** n/a - docs/makefile-only; CI should stay
  green.

## Credits

- **Jerry** (lead, modernization & DevOps) - Makefile authoring, detection
  regex, `FORCE=1` gate design, functional test, exec report.
- **Lloyd Braun** (guest, junior-migrator lens) - voice on the
  `docs/migration-v1-to-v2.md` section 4 copy; specifically the
  "if you never touched ..." opt-out sentence.
- **Copilot** - co-authored per `commit.md`; trailer on the commit.
