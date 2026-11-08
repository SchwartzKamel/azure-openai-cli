# S04E29 -- *The Rename Ships*

> *The release pipeline had never been run by anyone but the release.*

**Commit:** `(this commit)` -- the first of two commits in the release PR (the second cuts v2.4.0)
**Branch:** `release/s04e29-the-rename-ships` (PR, off `main`)
**Runtime:** one session, eighth of the gap-fixing batch
**Director:** Larry David (showrunner seat, filled by a single orchestrator)
**Lead:** Bob Sacamano (packaging) with Jerry (CI) and Mr. Lippman (the release)
**Cast:** 0 sub-agents, inline passes by one orchestrator. Instruments, listed separately: smith's legion (planner, reviewer, oracle, one round) advised on the order of the remaining rename stages. Bob (LEAD), Jerry (the workflows), Lippman (the release notes and body), Puddy (guest: the survivors)

## The pitch

ADR-016 Stage 2 says the release archives move from `az-ai-<version>-<rid>` to `hopper-<version>-<rid>`, together with the
release that carries the rename. The edit itself is small. What stopped me from just making it was a question: how would I know it
worked? Nothing in the repository tests `stage.sh`, `release.yml`, the release body or the manifests. A comment in the workflow
mentions a "tag-time rehearsal" that does not exist. The first time any of it would run is on the public tag, which cannot be
quietly taken back. So the episode is the rename *and* the means to check it.

## Scene-by-scene

### Act I -- Ask the pipeline what it already does wrong

Before changing it I looked at what it had published. Three things were already false:

- v2.3.0's assets are `az-ai-2.3.0-<rid>`, with no `v`, but the release-body table printed `az-ai-$REF-...` where `$REF` is
  `v2.3.0`. The filenames in the table, and the `gh attestation verify` example, named files that do not exist.
- The `docker pull` example used the repository's mixed-case name, and image references must be lowercase.
- The body told users to install from a Homebrew tap and a Scoop bucket. Neither repository exists, and no release has had one.

I am the one creating this release body, so it stops saying those things.

### Act II -- Three scripts, because three things need to be testable

- **`scripts/verify-archive.py`** checks a built archive: the name, exactly one top-level directory named after the archive, no
  member that escapes it, the required files and nothing else, the `az-ai` alias, the executable bits, and with `--run` it extracts
  the archive and **runs** `hopper --version` and the alias. It also compares the version in the archive's name with the tag, so
  tagging v2.4.0 with a csproj still at 2.3.0 fails the build job, unpublished.
- **`scripts/release-notes.sh`** prints the CHANGELOG section for exactly one version, and fails if there is none or it is empty.
  The old workflow took `[Unreleased]`, which silently publishes someone else's notes, or none.
- **`scripts/release-body.sh`** builds the body, from the version without its `v`, with a lowercase image and a table of the files
  the release matrix really produces. The 15 `shellcheck` notes in the old inline `printf` block go away with it.

### Act III -- Windows gets a shim, not a second binary

A tarball can hard-link `az-ai` to `hopper`. A zip cannot, so the Windows archive carried a second 7 MB copy. It now carries a
two-line `az-ai.cmd` that forwards its arguments and propagates the exit code, so direct-download users keep the alias and the
download does not double. The verifier checks that the shim forwards and is small.

### Act IV -- A rehearsal that runs before the tag

`.github/workflows/packaging-rehearsal.yml` stages and verifies all six runtime identifiers on any pull request that touches
packaging, or on demand, using the same pinned actions and the same matrix as the release job. The release job now also runs the
verifier before it attests or uploads anything. A test holds the two matrices identical.

### Act V -- A real archive, and survivors

On this machine, `stage.sh linux-x64` produced a 7.5 MB `hopper-2.3.0-linux-x64.tar.gz` in 64 seconds, the verifier passed it, and
running `hopper` and `az-ai` from inside the extracted archive worked. The alias is a hard link. That is the one RID that can be
built here; the other five (musl, ARM, macOS, both Windows) run for the first time in this pull request's rehearsal, which is the
point of having one.

The mutation check was not clean on the first pass, and that is the useful part. Of about 36 deliberate breakages, three survived
and one did not even apply:

- *The body script's tag check:* a branch name is refused anyway, because the notes script rejects it with the same exit code. The
  input that tells them apart is a version without the `v`.
- *Two pipeline mutants:* my test only searched `release.yml` for the text `verify-archive.py`, which also appears in a comment. It
  now parses the workflow and requires a step that **runs** the verifier **before** the attestation and the upload.
- *One mutation of mine did not apply* (a wrong anchor); the harness refused to count it and I retried it.

### Act VI -- Two traps found on the way

`scripts/*` is in `.gitignore`, so all three new scripts were silently ignored; the real release job would have failed on a missing
file. `check-ignored-scripts` caught it. And the test helper that builds broken archives had a bug in its first run (a Windows kind
built under a Linux name), found by running every kind against the verifier before trusting any of them.

### Act VII -- What the council said, and what was adopted

I had proposed renaming the repository first. All three legion members recommended renaming it last, with the better argument, and
that order is recorded in an ADR-016 addendum: Stage 2, release, manifest hash-sync, then Stage 3. The council was also shallow on
this repository (it asked for a `go.mod` audit in a .NET project), so it was one signal, verified rather than followed.

### Act VIII -- The rehearsal's first catch

The pull request's rehearsal ran six legs for the first time. Five passed, including both Windows legs (the PowerShell zip and the
`.cmd` shim, which I could not run locally) and linux-arm64. The sixth, `osx-arm64`, was cancelled in the same second it started, with no
steps: `The configuration 'macos-14-arm64-us-default' is not supported`. GitHub no longer provides the `macos-14` label, and the release
workflow's own `osx-arm64` leg used it. Because the `release` job needs every build leg, **the v2.4.0 tag would have failed after the tag
was already public, publishing nothing.** That is the failure the rehearsal was built to move onto a pull request, and on its first run
it did. The leg moves to `macos-15` (pinned rather than floating on `macos-latest`, which is now macOS 26, since a binary built on the
newest image can inherit a higher minimum macOS version), in a separate commit so the catch stays visible.

## What shipped

- `stage.sh` (the new archive and directory names, the Windows shim), `release.yml` (globs, the verify step, the body script, a
  `run` flag per matrix leg), `packaging-rehearsal.yml`, and the three scripts, whitelisted in `.gitignore`.
- 39 integration cases and `tests/helpers/make-archive.py` (20 kinds of synthetic archive). **Mutation-checked:** 36 mutations, each
  caught, each verified applied, after the three survivors above were fixed. `actionlint` on `release.yml` goes from 15 findings to 0.
- The README install table and `packaging/README.md` (status and the release checklist), the ADR-016 addendum, CHANGELOG, the
  findings backlog and this report.

**Not shipped**

- The manifests (Homebrew, Scoop, Nix): they move in a separate hash-sync after the release is published, so none points at an
  asset that does not exist yet.
- A published Homebrew tap and Scoop bucket (`bob-2026-11-B-1`).
- Stage 3, the repository rename, which is last on purpose.

## Lessons from this episode

1. **If you cannot test the step, test the means of testing it.** Three small scripts and a rehearsal made an untestable release testable, and the rehearsal caught a runner label that would have failed the public tag on its first run.
2. **Check what the pipeline already published.** The file-name bug, the lowercase bug and the nonexistent tap were all there.
3. **A test that greps for text is weak.** A comment satisfied it; parsing the workflow did not.
4. **Look for the input that tells two checks apart.** A redundant check hides its own removal.
5. **Move the risky, irreversible step last, and do not combine it with a second change.**
6. **Treat an advisor's output as one signal.** The council was right about the order and wrong about the repository.

## Metrics

- **Test delta:** +39 integration; unit unchanged.
- **Mutations:** 36 caught after fixing 3 survivors.
- **Preflight:** format, 0 warnings, unit, integration, `ascii-check`, `markdownlint`, `exec-report-check`, `scripts-ignore-check`.
- **CI status at push time:** pending at commit time; the rehearsal runs on the pull request.

## Credits

Bob led because the subject is packaging. Jerry's lens is the workflows, Lippman's is the release notes and body, and Puddy's is the
survivors. Smith's legion advised on the order. Commits carry `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.
