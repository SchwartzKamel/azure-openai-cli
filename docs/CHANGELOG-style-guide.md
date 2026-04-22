# CHANGELOG style guide

> "No commit dumps. Every line reads like prose a user can
> understand." -- Mr. Lippman

Audience: anyone touching `CHANGELOG.md`. This guide codifies the
house style we've actually been following through the 1.x and 2.x
lines; it is not a rewrite, it is a rulebook for keeping the next
entry consistent with what's already there.

Base format: [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/).
SemVer contract: [`release/semver-policy.md`](release/semver-policy.md).
Gate: [`release/pre-release-checklist.md`](release/pre-release-checklist.md) row 10.

---

## 1. File anatomy

```
# Changelog

All notable changes to Azure OpenAI CLI will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
### Changed
### Deprecated
### Removed
### Fixed
### Security

## [X.Y.Z] -- YYYY-MM-DD

> Optional one-paragraph banner explaining the headline change …

### Added
### Changed
### Deprecated
### Removed
### Fixed
### Security
### Packaging        # optional; post-publish hash-sync landed with this tag
### Notes            # optional; context that isn't a change (e.g. cancelled predecessor)
```

- The preamble (lines 1-6) is fixed. Do not rewrite it per release.
- `## [Unreleased]` lives **immediately** under the preamble and is
  always present -- even if empty, keep the skeleton subheaders (M-3).
- Newest release header sits directly below `[Unreleased]`. Entries
  are reverse-chronological.
- Do not delete old entries. `CHANGELOG.md` is append-only; obsolete
  detail can be summarized but never removed.

---

## 2. Version headers

Format: `## [X.Y.Z] -- YYYY-MM-DD`

- Use the Unicode em-dash `--` (U+2014), **not** `-` or `--`.
- `YYYY-MM-DD` is UTC, ISO 8601, zero-padded.
- For tagged-but-not-yet-published releases, use
  `## [X.Y.Z] -- unreleased` (lowercase, no date) until the release
  publishes. Replace with the publish date in the same commit that
  publishes.
- For cancelled tags that never published, keep the header with
  `-- YYYY-MM-DD (cancelled)` and a `### Notes` subsection explaining
  what superseded them. Do not delete the entry.
- Every `[X.Y.Z]` header must have a matching link-reference at the
  bottom of the file if you add link-references (we currently don't;
  if this changes, do it in a dedicated PR).

---

## 3. Section order

Fixed, per Keep a Changelog, in this order when present:

1. `### Added` -- new capability.
2. `### Changed` -- existing capability, different behavior.
3. `### Deprecated` -- still works, will be removed later. Name the
   target version.
4. `### Removed` -- gone. Matches a prior `### Deprecated` whenever
   possible.
5. `### Fixed` -- bug fix, no new capability.
6. `### Security` -- vulnerability fix. Include CVE / advisory link
   if public.

Project-specific sections (**optional**, appended after the standard
six in this order):

7. `### Performance` -- measurable perf delta. Include baseline vs.
   new numbers and a pointer to the benchmark doc.
8. `### Tests` -- test-suite changes users should know about (new
   gates, baseline counts).
9. `### Verified` -- platforms / configurations actually validated
   this release.
10. `### Packaging` -- post-publish hash-sync work landed on this tag
    (Homebrew / Nix / Scoop / GHCR digests).
11. `### Notes` -- context that is not itself a change (cancelled
    predecessor, known issues carried over, migration pointer).

Do **not** invent new top-level subheaders. If a change doesn't fit,
it goes in the closest standard section with a prose label:

> **Telemetry.** Added `ralph.iteration.count` attribute …

lives under `### Added`, not a new `### Telemetry` section.

---

## 4. Entry prose -- the house style

### 4.1 Tense and voice

- **Past tense, active voice.**
  - Good: "Dropped `osx-x64` from the release matrix."
  - Good: "Added `--raw` gate to `UserConfig.Load`."
  - Bad: "This change drops …" (present).
  - Bad: "`osx-x64` has been dropped …" (passive).
- Subject is the project, implied -- start the bullet with the verb.

### 4.2 Bullets are prose, not commit messages

- Never paste a commit subject line. `git log --pretty=%s` is not a
  CHANGELOG.
- One bullet per user-observable change. If a single change spans
  five files, it's still one bullet -- describe the change, not the
  patches.
- Length: 1-6 lines per bullet. If you need more, start with a
  short summary line and indent the detail.
- Cite the relevant commit, audit finding, or issue in parentheses
  at the end of the first line when it materially helps a reader:
  `(audit C-1)`, `(commit 4842b6a)`, `(#421)`.

### 4.3 What a user is

When deciding whether to include a line, ask: "Would a user running
`az-ai-v2` or an integrator consuming the tarball care?" If no, the
line does not belong here -- it goes in the PR description or a
code comment.

- User-facing: flags, config, output, exit codes, personas, image
  tags, artifact names, perf.
- Not user-facing: internal refactors, test harness plumbing, build
  system tweaks that don't change the shipped binary.

### 4.4 Breaking changes

- Every MAJOR release has a one-paragraph banner quote (`> …`)
  immediately under the version header summarizing the break.
- Every breaking bullet starts with **Bold-label.** naming the
  contract that moved:

  > **CLI flag `--legacy-foo` removed.** Use `--foo` (added in 2.0.0).
  > Migration: `sed -i 's/--legacy-foo/--foo/g'` your wrapper scripts.

- Include a copy-paste migration snippet or a link to one. No
  naked "this breaks X" without a path forward.

### 4.5 Formatting

- Code, filenames, flags, env vars: backticks. `AZUREOPENAIAPI`,
  `--raw`, `Program.cs:1550`, `packaging/tarball/stage.sh`.
- Bold for the **label** that opens a multi-line bullet.
- Italics sparingly -- only to contrast terms (*contract* vs
  *implementation*).
- Line length: wrap at ~72 columns to match the rest of the file.
- Lists inside bullets use `1. / 2.` numbered when order matters,
  `-` when not.

### 4.6 Dates, versions, counts

- Dates: `YYYY-MM-DD`, UTC.
- Version refs: `v2.0.4` with the `v` when talking about the tag /
  release; `2.0.4` (no `v`) when talking about the numeric version
  in a filename, csproj, or manifest.
- Test counts: cite the exact baseline when it moves, in the form
  `v1 1025/1025 + v2 490/490 = 1515 green`.
- Run IDs: `run 24789065975`, without quoting.

---

## 5. Linking policy

- **In-repo links** -- relative paths in backticks, wrapped in a
  Markdown link:

  `[`docs/audits/docs-audit-2026-04-22-lippman.md`](audits/docs-audit-2026-04-22-lippman.md)`

  Use the full relative path so the link works from both the repo
  root (GitHub web UI) and inside `CHANGELOG.md`.
- **Commits** -- backtick the short SHA (`` `4842b6a` ``). Do not
  hyperlink; GitHub renders commit links automatically in the Release
  body, and `CHANGELOG.md` stays portable.
- **Issues / PRs** -- `#421` style, no autolinking. GitHub's web UI
  renders these for free.
- **External refs** -- Markdown link with the URL visible in context
  ("see [Keep a Changelog](https://keepachangelog.com/…)"). Never
  a bare URL on its own line.
- **Audit findings** -- parenthetical form: `(audit C-1)`,
  `(audit finding H-2)`. The audits live under
  `docs/audits/` and are append-only.

---

## 6. `[Unreleased]` rules

- Always present, never deleted.
- Immediately after cutting a release, the release commit (or the
  first commit after it) resets `[Unreleased]` to the bare skeleton:

  ```md
  ## [Unreleased]

  ### Added
  ### Changed
  ### Deprecated
  ### Removed
  ### Fixed
  ### Security
  ```

- Contributors add their entry under the correct subheader as part
  of the PR that lands the change. Do not batch at release time.
- At release cut, Lippman moves the populated subheaders into the
  new `[X.Y.Z]` block, dated, and re-resets `[Unreleased]`.
- Empty subheaders are dropped from the released block (only present
  ones ship). `[Unreleased]` keeps all six skeleton subheaders even
  when empty.

---

## 7. Special cases we've actually hit

### 7.1 Cancelled / unpublished tags

Entry stays. Header is `## [X.Y.Z] -- YYYY-MM-DD (cancelled)` (or
`-- unreleased` if it was never tagged). Include a `### Notes`
subsection explaining what superseded it. Reference the diagnostic
doc in `docs/launch/` if one exists. Do not renumber.

### 7.2 Fix-forward releases

The "fix-forward" context goes in the banner quote above
`### Fixed`. Explicitly name the predecessor tag and what about it
failed. Don't leave a reader guessing why `2.0.2` exists after
`2.0.1` didn't publish.

### 7.3 Post-publish hash-sync

Hash-sync work that lands on the same tag goes under
`### Packaging (post-publish, hash-sync)`. Include the SHA256 digest
for every RID, the tag / run ID, and any known-issues (filename
drift, missing RID coverage). This is the only section that can be
added to a `[X.Y.Z]` block **after** the initial release commit;
amend only this section, never the others.

### 7.4 Known issues shipped with the release

If we ship with a known bug that will be fixed in the next release,
call it out explicitly under `### Notes` or at the bottom of
`### Fixed` with a `**Known issue -- …**` label. v2.0.4's C-1 filename
drift is the reference example.

---

## 8. What not to do

- ❌ Don't paste `git log` output.
- ❌ Don't write "various fixes" or "misc improvements".
- ❌ Don't use future tense ("this will …").
- ❌ Don't link to internal Slack, issue trackers outside GitHub, or
  any URL that will 404 for a reader six months from now.
- ❌ Don't list every file touched. List every user-observable change.
- ❌ Don't retroactively edit a published release's entry except for
  typos, broken links, or the `### Packaging` post-publish block.
- ❌ Don't put the CHANGELOG entry in the release PR description
  only -- it must land in `CHANGELOG.md` in the release commit.

---

## 9. Review checklist (for CHANGELOG diffs)

Reviewers apply this to every PR that touches `CHANGELOG.md`:

- [ ] Version header matches SemVer policy + release PR bump.
- [ ] Date is UTC, ISO 8601, populated at publish time.
- [ ] Section order is the one in §3.
- [ ] Every bullet is prose, past tense, active voice.
- [ ] Breaking changes have a banner quote + migration path.
- [ ] Links are relative in-repo, full path, backtick-wrapped.
- [ ] `[Unreleased]` skeleton is intact and empty (for release PRs)
      or has the new entry under the right subheader (for feature
      PRs).
- [ ] No commit subjects, no `git log` output, no future tense.

-- Mr. Lippman, release management
