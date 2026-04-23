# Skill: changelog-append

**Run when your change is user-visible and earns a line in `CHANGELOG.md`.** Defines where the line goes, what it looks like, and -- equally important -- what does **not** belong in CHANGELOG at all.

## Where the line goes

The `[Unreleased]` section near the top of `CHANGELOG.md` has five subsections, [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) order:

| Subsection   | What goes here                                                  |
|--------------|------------------------------------------------------------------|
| **Added**    | New features, new commands, new flags, new docs that ship a capability |
| **Changed**  | Modifications to existing behavior (defaults, output format, prompts) |
| **Fixed**    | Bug fixes -- something used to be wrong, now it is right         |
| **Removed**  | Deletions: dropped flags, removed dependencies, retired features |
| **Security** | Vulnerability fixes, hardening work, supply-chain bumps with a CVE |

Create the subsection if it does not yet exist under `[Unreleased]`. Keep them in the order above.

## Bullet format

One bullet per change, present tense, scoped, terse. Cross-link to file or PR/episode where useful.

```markdown
### Added
- **feat(scope):** one-line summary of what shipped, why it matters,
  with a `path/to/file.cs` or PR link. ([s02eNN-the-thing])
```

Conventions in this repo (read `CHANGELOG.md` head before adding):

- Lead with a bold `**type(scope):**` mirroring the commit prefix.
- Wrap at ~72 cols; nested lines indented two spaces.
- Episode tag in trailing parens (`([s02e28-the-style-guide])`) when the change came from an episode.

## What does NOT belong in CHANGELOG

CHANGELOG is for **users of the CLI**. The following are **not** user-visible and do **not** earn a bullet:

- **Process documentation** -- skills, agent archetypes, AGENTS.md, the writers' bible itself. The rules engine does not change what the CLI does; it changes how we work.
- **Episode reports** under `docs/exec-reports/`. They are the production log; users do not consume them.
- **Internal refactors** with no observable effect (renaming a private helper, reorganizing a `Tools/` subdirectory).
- **CI / build plumbing** that does not change the shipped binary (workflow tweaks, Makefile targets, Dockerfile layer reordering with identical output).
- **Test-only changes** (new test cases for existing behavior).

When in doubt: would a user upgrading from the previous release notice or care? If no, skip.

## Serialization via push timing

Parallel sub-agents appending to `[Unreleased]` rarely conflict because each appends its own bullet to its own subsection. The push order serializes the merge.

If your push hits a non-fast-forward:

```bash
git pull --rebase origin main
# git will replay your CHANGELOG bullet on top of the new tip;
# resolve any conflict by keeping BOTH bullets (yours and theirs)
git push origin main
```

If two agents added bullets to the **same** subsection and Git flags a conflict, the resolution is mechanical: both bullets stay, ordered by author preference (or alphabetical -- pick one and move on). Do not drop a peer's bullet to "win" the rebase.

## ASCII rule

`CHANGELOG.md` is **excluded** from the smart-quote scan upstream (see [`ascii-validation`](ascii-validation.md)). Historical entries may contain em-dashes from before the rule landed.

- **Do not** mass-rewrite history. The blame churn is not worth it; the file is excluded for a reason.
- **Do** keep your **new** bullets ASCII for consistency. Use `--`, `-`, `'`, `"` -- the same six replacements documented in `ascii-validation`.

## Release-time handoff

When Mr. Lippman cuts a release (cf. `docs/exec-reports/s02e10-*` if landed):

1. The `[Unreleased]` section is renamed to `[X.Y.Z] - YYYY-MM-DD`.
2. A fresh empty `[Unreleased]` block is added at the top with the same five subsections (or as `## [Unreleased]` with subsections added on first use).
3. The `<Version>` element in `azureopenai-cli/AzureOpenAI_CLI.csproj` is bumped in the same commit.
4. A git tag `vX.Y.Z` is pushed.

Until that handoff, every appender targets `[Unreleased]`. After the cut, the new `[Unreleased]` is the target.

## Cross-refs

- [`preflight`](preflight.md) -- runs before any code commit; CHANGELOG-only edits skip it (see `docs-only-commit`)
- [`docs-only-commit`](docs-only-commit.md) -- a CHANGELOG-only commit is docs-only
- [`commit`](commit.md) -- commit-message format; the `**type(scope):**` in your bullet should mirror the commit subject
- [`ascii-validation`](ascii-validation.md) -- CHANGELOG is excluded, but new bullets stay ASCII
