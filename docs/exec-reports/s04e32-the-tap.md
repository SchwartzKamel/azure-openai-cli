# S04E32 -- *The Tap*

> *Every release since v2.0 told people to `brew install` from a tap that did not exist.*

**Commit:** `(this commit)` -- one commit
**Branch:** `release/s04e32-the-tap` (PR, off `main`)
**Runtime:** one session, eleventh of the gap-fixing batch
**Director:** Larry David (showrunner seat, filled by a single orchestrator)
**Lead:** Bob Sacamano (packaging and ecosystem)
**Cast:** 0 sub-agents, inline passes by one orchestrator. Bob (LEAD), Jerry (guest: the publish being safe to repeat), Puddy (guest: the partial publish)

## The pitch

S04E29 stopped the release body from telling users to install from a Homebrew tap and a Scoop bucket that did not exist. The honest fix
for the underlying gap (`bob-2026-11-B-3`) is to make them exist: two public repositories, mirrors of `packaging/`, published after each
release. The risk is the opposite of the old one. A mirror that publishes a wrong hash is worse than no mirror.

## Scene-by-scene

### Act I -- Names

The publish runbook described repositories named for the old tool (`homebrew-az-ai`, `scoop-az-ai`), which were never created. The old
release body used the generic `schwartzkamel/tap`. The generic names win: `SchwartzKamel/homebrew-tap` and `SchwartzKamel/scoop-bucket`.
`brew install schwartzkamel/tap/hopper` is a one-liner that taps on the fly, and the names survive any future rename of the tool.

### Act II -- A script that refuses

`scripts/publish-taps.sh` mirrors `packaging/homebrew` into the tap and `packaging/scoop` into the bucket. Before anything else it runs
`scripts/check-manifests.py`, and a pin that is not the release's published digest stops it with nothing published. A managed directory is
replaced wholesale, so a file removed from `packaging/` is removed from the mirror. Running it twice changes nothing, and `--dry-run` shows the
diff and pushes nothing. Its repository URLs and its packaging directory come from the environment, so every behaviour is tested against local
bare repositories and a synthetic packaging directory, never the real ones.

### Act III -- The partial publish

The first version pushed the tap, then prepared the bucket. A test removed the last frozen pin from `packaging/scoop/versions/`, and the bucket
step died on an empty glob, **after the tap had already been pushed**: one repository updated, the other stale. Two fixes. An empty pins
directory is a valid state. And publishing is now two-phase: clone, replace and commit both repositories locally, and only then push both, so
a failure while preparing the second can no longer strand the first. A test makes the bucket unreachable and requires the tap to stay empty.

### Act IV -- Publish, then install it as a user would

Both repositories were created, a dry run was done against the real (empty) repositories, and the publish ran. The Homebrew alias arrived as a
real symlink (git mode 120000). Then the check that matters: `brew install schwartzkamel/tap/hopper` from GitHub, with no local files. It
tapped the repository, installed `hopper` and an `az-ai` link to the same binary (both report 2.4.0), and the formula's `test` passes; the old
name `schwartzkamel/tap/az-ai` resolves to the same formula. The tap and the install were removed afterwards.

### Act V -- A release body that says what is true

The body now names `brew install schwartzkamel/tap/hopper`, the Scoop bucket commands and the Nix command, with the caveats written down: the
Scoop manifest has not been exercised on Windows and the Nix flake has not been run. Five mutations of that text, such as dropping a command or
hiding a caveat, are each caught.

## What shipped

- Two public repositories (`homebrew-tap`, `scoop-bucket`), published at hopper 2.4.0, with a README and the licence each.
- `scripts/publish-taps.sh`, two README templates, and the updated release-body script.
- 13 integration cases for the script (12 new, and the body's tap case rewritten as two). **Mutation-checked:** 20 mutations, each caught,
  each verified applied: 15 on the script and 5 on the body text.
- The runbook, `packaging/README.md`, the root README, CHANGELOG, the findings backlog and this report.

**Not shipped**

- **No CI automation.** Publishing is a manual step after each release. Automating it needs a token that can push to the two repositories,
  which `GITHUB_TOKEN` cannot; that is a decision for the maintainer (`bob-2026-11-B-5`).
- **Windows is unverified.** Scoop was not run; the bucket's manifest is checked only for its pins.

## Lessons from this episode

1. **A mirror must refuse to publish a wrong pin.** The check runs first, every time.
2. **Prepare everything, then push everything.** A failure in the middle must not leave half published.
3. **Test the failure that empties a directory.** Removing the last pin is the case the first version missed.
4. **Install it as a user would, from the real place.** A local tap proves the formula; only the real one proves the publish.
5. **Say what was not run.** The release body names the Scoop and Nix caveats instead of implying they were tested.

## Metrics

- **Real check:** `brew install schwartzkamel/tap/hopper` from GitHub; hopper and az-ai both 2.4.0; `brew test` passes.
- **Test delta:** +13 integration cases; unit unchanged.
- **Mutations:** 20 caught.
- **Preflight:** format, 0 warnings, unit, integration, `ascii-check`, `markdownlint`, `exec-report-check`, `scripts-ignore-check`.
- **CI status at push time:** pending at commit time; link in the PR.

## Credits

Bob led because the subject is the tap and the bucket. Jerry's lens is a publish that can be repeated safely, and Puddy's is the partial
publish. Commits carry `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.
