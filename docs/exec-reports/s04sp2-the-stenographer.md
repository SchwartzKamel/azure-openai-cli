# S04SP2 -- *The Stenographer*

> *Audit the paperwork after SP1's retag; lock the artifact contract so v2.3.0 ships clean.*

**Commit:** `<sha-pending>` (single docs-only commit)
**Branch:** `main` (direct push)
**Runtime:** ~25 min wall-clock
**Director:** Larry David (showrunner)
**Cast:** 1 sub-agent across 1 dispatch wave (Mr. Lippman, solo audit)

## The pitch

S04SP2 is the middle special of the three-special trilogy that diagnosed
why no releases had shipped since v2.2.0. SP1 *The Reruns* pulled the
flaky `osx-x64/macos-13` leg out of the release matrix and force-moved
the v2.3.0 tag to the corrected commit. SP3 *The Pre-Push* is wiring
markdownlint into the preflight gate in parallel.

SP2 sits between them. With the matrix change shipped, the next question
is: does the rest of the paperwork agree? Release notes template,
README install pointers, packaging metadata, and the `[Unreleased]`
section of the CHANGELOG all carry their own copy of the artifact
contract. If any of them still promised an `osx-x64` tarball, the next
release would look incoherent the moment a user opened the GitHub
Release page.

This special is audit-only. No workflow refactor, no version bump, no
new artifact. The deliverable is a clean confirmation that what SP1
removed stays removed everywhere it matters, plus a CHANGELOG entry
recording that the audit happened.

## Scene-by-scene

### Act I -- Survey the surface

SP1's commit `ffd2c1a` already updated `.github/workflows/release.yml`
in two places: the strategy matrix (no `osx-x64` row) and the artifact
table inside the `release-body.md` heredoc (no `macOS Intel` row).
SP1's exec-report close (`1c8c787`) also seeded the matching
`[2.3.0] ### Changed` entry in `CHANGELOG.md`. The retag was clean --
no published Release object existed at the old SHA, so no contract
was broken on the way out.

Three doc surfaces still had to be checked:

1. `README.md` -- the install instructions and download table.
2. `docs/release/` -- artifact-inventory, semver-policy, release
   runbook, pre-release-checklist.
3. `packaging/` -- Homebrew formulae, Scoop manifests, Nix flake,
   tarball staging script.

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | Mr. Lippman (solo audit) | README, release docs, and packaging surfaces audited; one stale-but-historical hot spot in `docs/release/artifact-inventory.md` filed as a finding; CHANGELOG `[Unreleased] ### Changed` entry added. |

### Act III -- Commit, preflight, push, CI

Single docs-only commit. Docs-lint (markdownlint-cli2) and the smart-
punct grep gates run cleanly against the new exec-report and the
CHANGELOG hunk. The diff is markdown plus this exec-report; per the
`docs-only-commit` skill the full preflight suite is not gated on
docs-only pushes, but the docs-side gates (docs-lint, ascii-check,
exec-report-check) were run explicitly.

Release run `25829203297` for tag `v2.3.0` was in-progress at SP2
push time -- six-leg matrix, no `macos-13` leg present. Final
conclusion captured in the Metrics section below.

## What shipped

### Production code

n/a -- audit-only special. No code, no workflow YAML, no packaging
manifest edits. SP1 had already corrected the contract; SP2's job
is to confirm and document.

### Tests

n/a -- no behavior change, no new tests required. Existing release
workflow's CI gate (the `ci` job invoked from `release.yml`) still
runs the full unit suite plus integration tests on the tagged commit.

### Docs

- `CHANGELOG.md` -- new `### Changed` subsection in `[Unreleased]`
  with the SP2 audit entry. `[Unreleased]` keeps its S04E01 and
  S04E02 entries intact (no orphans found; full scan in findings
  table below).
- `docs/exec-reports/s04sp2-the-stenographer.md` -- this report.

### Not shipped

- **`docs/release/artifact-inventory.md` table refresh.** Section 1
  still reads "Supported RIDs (v2, as of 2.0.4)" and lists four legs
  (`linux-x64`, `linux-musl-x64`, `osx-arm64`, `win-x64`). The
  actual v2.3.0 shipping matrix is six legs (adds `linux-arm64` and
  `win-arm64` per S03E30 *The Audit Trilogy*). The omission predates
  SP1 -- it is not an SP1 regression. Refreshing the table is
  doc-scope big enough to warrant its own episode beat (E04 *Reading
  Room* or an explicit Elaine pass). Filed in Lessons below; do
  not silently retitle the section.
- **`packaging/README.md` line 238 example.** The G6-cutover example
  loop still iterates `linux-x64 osx-x64 osx-arm64` with
  `VERSION=2.0.1`. The block is historical (it documents how digests
  were fetched during the v2.0.1 cutover ritual). Leaving as-is;
  the version pin makes it unambiguously archival.
- **`packaging/tarball/stage.sh` RID allow-list.** Keeps `osx-x64`
  in the case statement (line 26) and usage header (line 8).
  Intentional: local source builds for Intel Macs still flow
  through `stage.sh osx-x64`. The CI matrix is the only place the
  leg was retired, not the build tooling itself.
- **`[2.3.0] ### Added` prose.** Still describes "7 legs ... `osx-x64`
  on `macos-13`". SP1 elected to leave the original `### Added`
  language and add a `### Changed` row recording the retag rather
  than rewrite the Added bullet. Honest changelog history; slightly
  noisy. Not blocking and not SP2's call to overturn.

## Lessons from this episode

1. **The release-body heredoc and the strategy matrix are two
   surfaces; both have to agree.** SP1 edited both. SP2 confirms.
   Any future RID add/drop needs the same dual edit -- a
   `make release-notes-preview` against `[Unreleased]` should be
   eyeballed alongside the matrix diff. Candidate for a release-
   precheck gate in a future special.
2. **Force-moving a tag is safe only when no Release object exists
   at the old SHA.** SP1 confirmed this before retagging. Worth
   formalising in `docs/process/release.md` as a precondition --
   if a Release was published at the old commit, the path is a
   new tag (`v2.3.1`), not a retag.
3. **Stale-but-historical references are not stale references.**
   `packaging/homebrew/Formula/az-ai@2.0.0.rb` and friends keep
   their `osx-x64` URLs because that is what shipped at 2.0.0;
   rewriting them would falsify the package archive. The audit
   distinguishes "historical record" from "live contract" -- only
   live contract was checked for drift.
4. **`[Unreleased]` was clean.** Full scan of the section confirmed
   all entries are S04E01 or S04E02 attributable. No content
   inadvertently parked there during v2.0.x-v2.2.0 cycles.
5. **Audit-only specials still need an exec-report.** The
   pre-push hook does not exempt no-code pushes; the report is
   the audit trail. Skipping it would defeat the SP3 deliverable
   (markdownlint-in-preflight) it sits next to.
6. **Working-tree discipline during fleet-parallel work.** SP2 had
   to redo a CHANGELOG edit after a parallel `git reset --hard`
   from SP1's close wiped uncommitted state. Lesson: when working
   alongside another special on the same repo clone, stage and
   commit small slices early. The `shared-file-protocol` skill
   already says this for orchestrator-owned files; the same
   discipline applies to any file two specials might touch
   (CHANGELOG.md being the obvious one).

## Findings (audit surface)

| Surface | Status | Notes |
|---------|--------|-------|
| `.github/workflows/release.yml` matrix (lines 44-68) | clean | 6 legs, no `osx-x64`, no `macos-13`. |
| `.github/workflows/release.yml` release-body table (lines 216-223) | clean | 6 rows, no `macOS Intel`. |
| `README.md` line 435 v2.0.4 note | clean | Intel-Mac users directed to Docker / source build. |
| `README.md` install table | clean | No `osx-x64` artifact URL anywhere. |
| `CHANGELOG.md [Unreleased]` | clean | S04E01 + S04E02 entries intact; no orphans. |
| `CHANGELOG.md [2.3.0]` | mixed | `### Added` text still describes "7 legs ... `osx-x64` on `macos-13`"; SP1 recorded the retag in `### Changed` rather than rewriting history. Honest record, slightly noisy. Not blocking. |
| `docs/release/artifact-inventory.md` Section 1 | stale | "as of 2.0.4" / 4-leg table. Pre-dates SP1. Filed under Not Shipped. |
| `docs/release/semver-policy.md` Section 5.2 | historical | v2.0.4 case study; archival. |
| `docs/runbooks/release-runbook.md` | clean | One-line historical reference to the v2.0.4 cut. |
| `packaging/homebrew/Formula/az-ai.rb` | clean | Live formula has no `on_intel` block. |
| `packaging/homebrew/Formula/az-ai@2.0.{0,1,2}.rb` | historical | Pinned manifests for past releases; do not edit. |
| `packaging/nix/flake.nix` | clean | Comment block explains the v2.0.4 drop; `osx-x64` keys present only in `pinnedHashes` for 2.0.0-2.0.3. |
| `packaging/scoop/versions/az-ai@2.0.4.json` | historical | Pinned 2.0.4 manifest. |
| `packaging/tarball/stage.sh` | clean | Build tool still supports `osx-x64` for local source builds; matrix simply does not invoke it. |
| `packaging/README.md` line 238 | stale | `VERSION=2.0.1` example loop iterates `osx-x64`; block is historical (G6 cutover). Filed under Not Shipped. |

No findings escalated to Newman or Frank Costanza -- nothing in the
audit surface is a security or reliability defect.

## Metrics

- Diff size: 1 file modified (`CHANGELOG.md`, ~12 insertions, 0
  deletions) plus 1 new exec-report file.
- Test delta: n/a (docs-only audit).
- Preflight result: docs-only path per `docs-only-commit` skill;
  docs-lint (markdownlint-cli2) clean on `docs/exec-reports/s04sp2-
  the-stenographer.md` and `CHANGELOG.md`; ASCII grep clean on the
  exec-report; `make exec-report-check` clean.
- CI status at push time: release run `25829203297` (tag `v2.3.0`)
  was in-progress -- six-leg matrix, no `macos-13` leg present.
  Final conclusion recorded on the GitHub Release page; if it
  failed, fix-forward owned by Jerry under the `ci-triage` skill.
  SP2 did not retouch the workflow.

## Credits

- **Mr. Lippman** -- audit, CHANGELOG entry, exec-report
  (this file). Solo special.
- **Co-authored-by: Copilot** trailer confirmed on the commit.

Adjacent specials in the trilogy:

- **S04SP1 *The Reruns*** -- Jerry (matrix drop) + Larry David
  (retag + exec-report). Shipped immediately before SP2.
- **S04SP3 *The Pre-Push*** -- Soup Nazi + Wilhelm (markdownlint
  into preflight). Running in parallel; report will land
  independently.

## References

- `docs/exec-reports/s04sp1-the-reruns.md` (predecessor special)
- `docs/exec-reports/s04-blueprint.md` (S04 running order)
- `.github/workflows/release.yml` (audited surface)
- `CHANGELOG.md` `[Unreleased]` and `[2.3.0]` entries
- `README.md` line 435 (v2.0.4 Intel-Mac note)
- `docs/release/artifact-inventory.md` (filed-finding surface)
- `docs/process/release.md` (release runbook)
