# S04E30 -- *The Hash Sync*

> *The manifests said "latest". Latest was a file that did not exist.*

**Commit:** `(this commit)` -- one commit, the follow-up to the v2.4.0 release
**Branch:** `release/s04e30-the-hash-sync` (PR, off `main`)
**Runtime:** one session, ninth of the gap-fixing batch
**Director:** Larry David (showrunner seat, filled by a single orchestrator)
**Lead:** Bob Sacamano (packaging: Homebrew, Scoop, Nix)
**Cast:** 0 sub-agents, inline passes by one orchestrator. Bob (LEAD), Jerry (the workflow job), Puddy (guest: the survivors), Newman (guest: what a hash is for)

## The pitch

v2.4.0 is published. ADR-016's order, and the legion's advice, put the manifests next: they move to the new names only after the
assets exist, so none of them ever points at something that is not there. The work is a hash-sync: take the published digests and put
them in the Homebrew formula, the Scoop manifest and the Nix flake.

## Scene-by-scene

### Act I -- What "latest" actually pointed at

The manifests were last synced at v2.0.6, five releases ago, because the tap and bucket they were for never existed and nothing
checked them. That was known. What was not known: v2.0.6's real assets are named `az-ai-v2-2.0.6-<rid>`, and the unversioned
"latest" Homebrew formula and Scoop manifest pinned `az-ai-2.0.6-<rid>`, **a name that never existed**. The "latest" manifests have been
pointing at 404s since v2.0.6. The versioned pins I kept use the right names. Nobody saw it because nobody ran an install.

### Act II -- A checker, so a hash is never hand-copied again

`scripts/check-manifests.py` takes each manifest's pins and requires: the URL is `.../download/v<ver>/hopper-<ver>-<rid>.<ext>`, with the
extension right for the platform and one version per manifest (and, for Scoop, the manifest's version and `extract_dir` agree with it);
the file named is in the release's own `digests.txt`; and the pinned sha256 equals the published one (Nix's `sha256-<base64>` form is
decoded first). A manifest that yields no pins fails, because a check that finds nothing to check is not a pass. Run against the real
v2.4.0 release: 7 pins in 3 manifests, all match. A new `manifests` job in the rehearsal workflow runs it, so a manifest bumped before its
release exists fails on the pull request.

### Act III -- Generate, do not type

The new manifests were generated from the downloaded `digests.txt`, not copied by hand: `hopper.rb`, `hopper.json`, and the Nix
digests converted to SRI form from the same bytes. Homebrew gets an `az-ai` link and an `Aliases/az-ai` entry; Scoop shims both names;
Nix installs `hopper` with an `az-ai` link. linux-arm64 is added to Homebrew, win-arm64 to Scoop.

### Act IV -- Scoop was broken in two more ways

The old manifest had no `extract_dir`, though the zip extracts to a top-level directory, and its autoupdate read a per-file `$url.sha256`
that the release never published (it publishes one `digests.txt`). The new one sets `extract_dir`, and autoupdates from `digests.txt` with
`$sha256\s+$basename`. I cannot run Scoop here, so both are from its documentation and are tested only as data.

### Act V -- A real install, and a refusal

Homebrew is installed on this machine, so the formula got what the release checklist asked for and nobody ever did: a fresh install, from a
throwaway local tap, of the real published v2.4.0 asset. It installed, `hopper --version` and `az-ai --version` both report 2.4.0, and the
formula's own `test` block passes. Then the negative control: with one digit of the sha256 changed, Homebrew refused ("reports different
checksum") and installed nothing. `brew audit --strict` found one thing, a redundant `version` line, which is gone. The tap and the install
were removed afterwards.

### Act VI -- Survivors again

The first mutation pass left two: the file-extension rule and the mixed-versions rule. Both were untested because my broken fixtures also
tripped the missing-asset rule first, which hid them. The fixtures now put the "wrong" asset in the digests with a matching hash, so only the
rule under test can object. Then both were caught.

### Act VII -- A gate my preflight does not have

The pull request's `docs-lint` failed: two Markdown links in `docs/v2-cutover-checklist.md` pointed at the two manifests I had deleted. CI's
broken-link check lives only inline in the workflow, so none of my local gates run it, and the gap only showed when a file that docs link to
was removed. The two links are retargeted to the v2.0.0 pins (what those files were when that checklist was written), the living runbooks
that named the old files now name the new ones, and I ran CI's own check locally (extracted from the workflow) before pushing. The gap
itself is `wilhelm-2026-11-W-6`.

## What shipped

- `hopper.rb` (with `Aliases/az-ai`), `hopper.json` and the updated `nix/flake.nix`; the superseded unversioned `az-ai.rb` and `az-ai.json`
  removed (the `az-ai@<version>` pins stay).
- `scripts/check-manifests.py`, the `manifests` job in `packaging-rehearsal.yml`, and `tests/helpers/make-manifests.py` (14 kinds).
- 22 integration cases. **Mutation-checked:** 18 mutations, each caught, each verified applied, after the two survivors above were fixed.
- `packaging/README.md` and the two sub-READMEs, CHANGELOG, the findings backlog and this report.

**Not shipped, and not verified**

- **The Nix flake and the Scoop manifest were never run.** There is no Nix or Scoop on the build machine, so both are checked only for their
  pins. The flake's `sourceRoot = "."` with archives that have a top-level directory looks wrong and has probably never worked; I changed
  only data and names. That is `bob-2026-11-B-4`.
- **No tap or bucket exists**, so `brew install hopper` and `scoop install hopper` still have nowhere to come from (`bob-2026-11-B-3`).
- The Nix flake does not add `aarch64-linux`: the frozen pins have no `linux-arm64` digest, so adding the system would break their evaluation.

## Lessons from this episode

1. **A manifest nobody installs from rots silently.** "Latest" pointed at a name that never existed for five releases.
2. **Generate the numbers, then check them against the source of truth.** The checker compares with the release itself.
3. **Do the real install, then try to break it.** The negative control is what shows the checksum is actually enforced.
4. **A fixture that trips two rules hides one.** Each broken input must be wrong in exactly one way.
5. **Say what you could not run.** Two of three manifests were checked as data only.
6. **A local preflight is only as good as the checks it copies.** The link check existed only in CI.

## Metrics

- **Online check:** 7 pins, 3 manifests, all match the published v2.4.0 digests.
- **Test delta:** +22 integration; unit unchanged.
- **Mutations:** 18 caught after fixing 2 survivors.
- **Preflight:** format, 0 warnings, unit, integration, `ascii-check`, `markdownlint`, `exec-report-check`, `scripts-ignore-check`.
- **CI status at push time:** pending at commit time; the new `manifests` job and the rehearsal run on the pull request.

## Credits

Bob led because the subject is the manifests. Jerry's lens is the workflow job, Puddy's is the survivors, and Newman's is what a hash is for.
Commits carry `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.
