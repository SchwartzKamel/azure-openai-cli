# `docs/announce/` — published release announcements

> I'm Keith Hernandez. This is where the *shipped* launch copy lives. The workshop — drafts, social snippets, release-body rehearsals — stays in [`docs/launch/`](../launch/). Once a release ships, its canonical announcement gets a stable home here.

## Convention

- **`docs/launch/`** — pre-publish. Drafts, social snippets, playbooks, release-body rehearsals, contributor-thanks comps. Churn lives here.
- **`docs/announce/`** — post-publish. The one announcement document per release we want external links to resolve against. Stable URLs. No churn.
- **`docs/announce/archive/`** — superseded announcements we don't want pitched as current. Kept for historical / link-integrity reasons only.

Pick one and stick to it. If an external post, talk, or speaker page links into `docs/announce/`, it should land on copy that describes *a currently shipping binary*.

## Current releases

| Version | Date | Announcement | Release notes / CHANGELOG |
|---|---|---|---|
| **v2.0.5** | unreleased | draft in [`docs/launch/`](../launch/) (once cut, land `v2.0.5-launch.md` here) | [`CHANGELOG.md` §2.0.5](../../CHANGELOG.md) |
| **v2.0.4** | 2026-04-22 | [`docs/launch/v2.0.0-announcement.md`](../launch/v2.0.0-announcement.md) *(v2.x series announcement — see CHANGELOG for per-patch deltas)* | [`CHANGELOG.md` §2.0.4](../../CHANGELOG.md) |
| **v2.0.0** | 2026-04-20 | [`docs/launch/v2.0.0-announcement.md`](../launch/v2.0.0-announcement.md) | [`docs/release-notes-v2.0.0.md`](../release-notes-v2.0.0.md) |

When v2.0.5 ships, copy the final `docs/launch/v2.0.5-*` draft here as `v2.0.5-launch.md` and update this table.

## Archive

- [`archive/v1.8.0-launch.md`](archive/v1.8.0-launch.md) — *"The Keyboard Becomes a Compiler."* Superseded by v2.0.0. Cold-start, binary-size, RID, and test-count numbers in that post are v1-era and **should not be cited** in current DevRel. Kept for link integrity and provenance. See [`docs/release-notes-v2.0.0.md`](../release-notes-v2.0.0.md) for the v2 re-baseline.

## Not a home for

- Blog drafts → `docs/launch/`
- Social copy → `docs/launch/social-snippets.md`
- Speaker packets → [`docs/speaker-bureau.md`](../speaker-bureau.md)
- Swag briefs → [`docs/devrel/swag-brief.md`](../devrel/swag-brief.md)

— *Keith Hernandez. Stable URLs, honest numbers.*
