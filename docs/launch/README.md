# `docs/launch/` -- the launch locker

This directory holds everything you reach for when you are cutting a release
or landing one: playbooks, announcement copy, social drafts, conference
pitches, and post-tag diagnostics. If you just inherited a launch and have
no idea where to start, start here.

Per-file map, in the order a new release manager would typically touch them:

- [`release-v2-playbook.md`](release-v2-playbook.md) -- the exact ritual for cutting a `v2.x` tag under the tag-pattern-gated `release.yml`. Read this first.
- [`bob-tap-handoff.md`](bob-tap-handoff.md) -- the Homebrew tap + Scoop bucket handoff. Read before you promise anyone `brew install az-ai`.
- [`v2-conference-cfp.md`](v2-conference-cfp.md) -- Keith Hernandez's CFP pitch for v2.0.0. Use as a starting template for any future conference submission.
- [`v2.0.0-announcement.md`](v2.0.0-announcement.md) -- the long-form v2.0.0 announcement. Tone reference for future majors.
- [`v2.0.0-blog-draft.md`](v2.0.0-blog-draft.md) -- J. Peterman's blog draft that accompanied the announcement. Use for the long-read channel.
- [`v2.0.0-contributor-thanks.md`](v2.0.0-contributor-thanks.md) -- the attribution block. Source of truth for the release-notes thank-you and `CONTRIBUTORS.md`.
- [`v2.0.0-github-discussion.md`](v2.0.0-github-discussion.md) -- the GitHub Discussions post draft. Publish after the workflow reports green.
- [`v2.0.0-readme-patch.md`](v2.0.0-readme-patch.md) -- the README install-section patch for v2.0.0. Do not apply until the tap and bucket are live.
- [`README-splash-suggestion.md`](README-splash-suggestion.md) -- proposed splash banner for the top-level `README.md` announcing the release.
- [`v2.0.0-release-body.md`](v2.0.0-release-body.md) -- the GitHub Release body you paste into `gh release edit`.
- [`v2.0.0-social-posts.md`](v2.0.0-social-posts.md) -- short-form social drafts (character-count-verified against the 280-char limit).
- [`social-snippets.md`](social-snippets.md) -- additional short copy that links the release URL. Re-usable across channels.
- [`v2.0.6-release-notes.md`](v2.0.6-release-notes.md) -- the release notes for v2.0.6, the first actually-published v2 line. Use as the template for subsequent patches.
- [`v2-tag-rehearsal-report.md`](v2-tag-rehearsal-report.md) -- resolved rehearsal report for the v2.0.0 tag process. Retained as historical record.
- [`v2-release-attempt-1-diagnostic.md`](v2-release-attempt-1-diagnostic.md) -- the v2.0.0 attempt that failed (missing `zip` on `windows-latest`; AOT cross-libc mismatch). Read before you re-derive either failure.
- [`v2.0.1-release-attempt-diagnostic.md`](v2.0.1-release-attempt-diagnostic.md) -- resolved v2.0.1 attempt diagnostic. Superseded by v2.0.4; retained for the macOS-runner backlog context.
- [`v2.0.2-release-attempt-diagnostic.md`](v2.0.2-release-attempt-diagnostic.md) -- resolved v2.0.2 attempt diagnostic. The `workflow_dispatch` HTTP 422 trap it documents is still live guidance (now consolidated in [`../runbooks/release-runbook.md`](../runbooks/release-runbook.md) §5.2).
- [`v2.0.2-publish-handoff.md`](v2.0.2-publish-handoff.md) -- resolved v2.0.2 publish handoff. The `gh run rerun --failed` recovery recipe is still valid and now lives in [`../runbooks/release-runbook.md`](../runbooks/release-runbook.md) §5.

For the current, canonical release procedure (as opposed to per-version
artifacts), see [`../runbooks/release-runbook.md`](../runbooks/release-runbook.md).
For the docs-wide index, go back to [`../README.md`](../README.md).
