# S02E11 -- *The Spec*

> *Costanza gets his first lead. Six episodes of features, twelve
> paragraphs of plain English, one PM who is sure this is going to be
> his breakout role.*

**Commit:** `8854588` (docs) + this report
**Branch:** `main` (direct push, solo-led repo per `.github/skills/commit.md`)
**Runtime:** ~20 minutes
**Director:** Larry David (showrunner)
**Cast:** George Costanza (lead, PM), Lloyd Braun (guest, junior dev),
Elaine Benes (guest, technical writer)

## The pitch

Six S02 episodes shipped real product. Six exec reports documented the
engineering. Zero documents existed that explained, in user terms,
what any of it actually does. A non-engineer reading the README could
tell the CLI was fast and hardened. They could not tell what problem
each shipped feature solved for them or why they should care.

This episode adds `docs/user-stories.md`: one paragraph per shipped
feature, in the As-a / I-want / so-that frame, grouped by user role.
The point is not to redesign anything -- it is to force the team to
say, in plain English, who each feature is for. Stories that resist
plain English are product smells worth surfacing.

Costanza took the lead because he had been missing from the original
S02 arc plan and the showrunner promoted him on the spot. Lloyd Braun
guest-starred to flag the stories where the underlying feature still
assumes too much, and Elaine guest-starred to keep the prose tight and
the structure consistent.

## Scene-by-scene

### Act I -- Inventory

Read the six S02 exec reports end-to-end:

- E01 (wizard + LOLBin storage)
- E02 (whitespace-key guard)
- E03 (docs-lint honesty)
- E04 (libsecret)
- E05 (bench-quick + CI canary)
- E06 (NO_COLOR / FORCE_COLOR + wizard a11y)

E01 produced two stories (wizard UX is one feature, OS-native storage
is another). E05 produced two (the local Make target and the CI
canary serve different roles). All others mapped one-to-one. Net: 9
stories from S02.

Pulled three pre-S02 catch-up stories from the load-bearing surface:
core `az-ai "prompt"` invocation, `--config show`, and `--raw`. These
predate the exec-report era but are the features any new user touches
first.

### Act II -- Draft (Costanza voice)

Wrote 12 stories using the brief's strict format. Costanza's intro
paragraph went in at the top with the "Costanza notes" callout
underneath it. Every story names exactly one primary user role; the
role-grouped table at the end allows multi-role secondary mappings.

### Act III -- Lloyd flags pass

Lloyd read each story cold and flagged two:

- **US-010 (core invocation):** the v1-vs-v2 binary distinction is
  assumed knowledge. A new contributor running `az-ai` does not know
  which binary they are getting or why two exist.
- **US-011 (`--config show`):** documents the precedence rule
  (env vars beat stored config) but the printed output does not show
  provenance. A user reading the output cannot tell which source any
  given value came from.

Both flags carry forward as B-plot. Per scope discipline, neither was
fixed in this episode.

### Act IV -- Elaine consistency pass

Elaine swept for:

- Smart quotes, em-dashes, en-dashes -- none introduced.
- Heading levels (every story is `##`, every section inside is bold
  inline, no `###` drift).
- Code-fence language tags -- no fences in this doc, nothing to label.
- List blank-line spacing.
- Sentence length: cut three sentences that ran past 30 words.

### Act V -- Ship

Two commits: the doc + CHANGELOG, then this report. Direct push to
`main`.

## What shipped

**Production code** -- none. Docs-only episode by design.

**Tests** -- none. Docs-only episode.

**Docs**

- `docs/user-stories.md` (new, 12 stories, ~290 lines).
- `CHANGELOG.md` -- one bullet under `[Unreleased] > Added`.
- `docs/exec-reports/s02e11-the-spec.md` (this file).

**Not shipped (intentional)**

- Did NOT touch product code.
- Did NOT define a roadmap (Mr. Pitt's job).
- Did NOT redesign any feature based on the user-story exercise.
  Lloyd's two flags carry forward as B-plot for future episodes.
- Did NOT formalize the user-role taxonomy beyond the five roles in
  the brief.
- Did NOT touch the glossary (Babu's E08 owns it).
- Did NOT touch any orchestrator-owned files (episode index, writers
  room, AGENTS.md, copilot-instructions, top-level README, agent
  roster, telemetry, runbooks, i18n audit).

## Lessons from this episode

1. **Features that resist plain English.** Two of the nine S02
   stories needed a Lloyd flag, and both flags pointed at gaps in the
   feature itself rather than the writing. US-010 (core invocation)
   exposed that the v1-vs-v2 binary split is unexplained anywhere a
   new contributor will look. US-011 (`--config show`) exposed that
   the precedence rule is documented but not surfaced where it would
   be most useful (the command's own output). These are product
   smells, not doc smells. Worth a future episode each.
2. **The four S02 stories that wrote themselves** (US-001 wizard,
   US-002 OS-native storage, US-003 whitespace guard, US-004 docs-lint
   honesty, US-005 libsecret, US-006 bench-quick, US-007 bench-canary,
   US-008 NO_COLOR, US-009 wizard a11y) all share a property: each
   solves one named problem for one named role. That is the bar the
   product desk should hold every new feature to before it ships.
3. **The format itself is the gate.** "If you cannot write the user
   story, the feature is not done." Adding that line to the doc's
   footer turns the user-stories file into a soft merge gate without
   any new tooling.
4. **Costanza's voice held up.** Opinionated intro, no waffle, lands
   the point in three short paragraphs. PM voice does not have to be
   diplomatic to be useful; it has to be specific.

## Metrics

- Diff size: ~304 insertions across 2 files (commit 1) + this report
  (commit 2).
- Test delta: none (docs-only).
- Preflight: not required (docs-only); ASCII-cleanliness check passed
  (`grep -nP '[\x{2018}...]'` returned no matches).
- CI status at push time: pending; expected green (docs-lint should
  be the only relevant gate).

## Credits

- **George Costanza** -- product lead, wrote the intro and all twelve
  stories.
- **Lloyd Braun** -- gap-flagging pass; two B-plots logged for future
  episodes.
- **Elaine Benes** -- structural and prose consistency pass.
- **Larry David** -- cast Costanza in the lead, defined the scope and
  the "did NOT do" list.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
