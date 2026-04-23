# S02E28 -- *The Style Guide*

> The Soup Nazi codifies three validation rules every doc-touching episode kept reinventing. The line stops moving.

**Commit:** `<pending>`
**Branch:** `main` (direct push)
**Runtime:** ~10 minutes
**Director:** Larry David (showrunner)
**Cast:** 2 (lead + guest), 0 dispatch waves -- off-roster special

## The pitch

Every doc-touching episode this season has inlined three near-identical procedures: the smart-quote grep that mirrors `docs-lint.yml`, the "what counts as docs-only and what can I skip" decision tree, and the CHANGELOG `[Unreleased]` append protocol. Each episode's version drifted slightly from the others -- different exclusion lists, different opinions on whether process docs earn a CHANGELOG bullet, different one-liners for the grep. The drift is invisible until two sub-agents working in parallel disagree on the rules and one of them ships a smart-quote that hard-fails CI.

This episode collapses the three procedures into canonical skills under `.github/skills/`. Same format and tone as `preflight.md`, `commit.md`, `ci-triage.md`. The Soup Nazi's archetype gets a "Skills you enforce" pointer near the top so reviewers cite the skill files instead of re-deriving the rules in PR comments.

Net effect: the validation spine is one source of truth. Future episodes link to the skills; they do not inline the procedures.

## Scene-by-scene

### Act I -- Planning

Read pass: `preflight.md`, `commit.md`, `ci-triage.md` for format / tone, `.github/workflows/docs-lint.yml` for the actual exclusion list, `CHANGELOG.md` head for the live `[Unreleased]` shape, `soup-nazi.agent.md` for archetype voice.

Decisions locked:

- **Exclusion list = workflow, not brief.** The episode brief mentioned `THIRD_PARTY_NOTICES.md` as upstream-excluded; the workflow does not exclude it. Document the workflow's actual list and call out the brief's drift in the skill itself.
- **Markdownlint stays out of the local loop.** Tool OOMs on this tree; the upstream workflow is the authority. Documented in `docs-only-commit`.
- **Process docs do not earn CHANGELOG bullets.** Restated in `changelog-append` to harden the rule against future drift.
- **Sed cheatsheet uses byte sequences,** not literal smart quotes -- the file documenting the ban must not contain the banned characters.

### Act II -- Solo write

No dispatch. Off-roster special, lead + guest only.

| Step | File | Outcome |
|------|------|---------|
| 1 | `.github/skills/ascii-validation.md` | new -- canonical grep, exclusion list, fix-it sed cheatsheet |
| 2 | `.github/skills/docs-only-commit.md` | new -- decision tree, the trap, edge cases |
| 3 | `.github/skills/changelog-append.md` | new -- subsection placement, serialization, what NOT to log |
| 4 | `.github/agents/soup-nazi.agent.md` | append "Skills you enforce" section near the top |

### Act III -- Ship

Self-validation: ran the canonical grep against all five touched files. Zero hits (the sed cheatsheet uses `\xE2\x80\x..` byte forms, not literal smart quotes -- deliberately, so this file passes its own gate). Commit on `main`, direct push.

## What shipped

**Production code** -- none. Off-roster docs episode.

**Tests** -- none. Skills are documentation, not code.

**Docs**:

- `.github/skills/ascii-validation.md` -- the U+2018/19/1C/1D/13/14 grep, the upstream exclusion list verified against `docs-lint.yml`, an ASCII fix-it sed cheatsheet, copy-paste one-liner.
- `.github/skills/docs-only-commit.md` -- definition of docs-only, what to skip (preflight, dotnet *, markdownlint locally), what to still run (ascii-validation, git status, link sanity), the `.cs`-sneaking-in trap, edge cases for assets and workflow files.
- `.github/skills/changelog-append.md` -- `[Unreleased]` subsection placement (Added / Changed / Fixed / Removed / Security), bullet format mirroring repo convention, push-timing serialization, the explicit non-list (process docs, exec reports, internal refactors, CI plumbing, test-only changes), ASCII rule despite upstream exclusion, release-time handoff to Mr. Lippman.
- `.github/agents/soup-nazi.agent.md` -- new "Skills you enforce" block near the top with one-line summaries and links to the three new skills plus the existing three (preflight, commit, ci-triage).

**Not shipped** (intentional follow-ups):

- `AGENTS.md` skills table update -- explicitly out of scope, owned by the orchestrator batch.
- `CHANGELOG.md` bullet -- defining the protocol does not earn a bullet (eat your own dog food: process docs do not go in CHANGELOG).
- Sweep of existing exec reports for inlined-and-drifted validation steps -- handed off to orchestrator, see "Lessons" below.

## Lessons from this episode

1. **The brief drifted from the workflow.** The dispatch brief listed `THIRD_PARTY_NOTICES.md` as upstream-excluded; `docs-lint.yml` does not exclude it. Skills now match the workflow, not the brief. Future episode briefs that quote exclusion lists should be cross-checked against the workflow at write time.
2. **The skill that documents the ban must pass its own ban.** Used `\xE2\x80\x..` byte sequences in the sed cheatsheet rather than literal smart quotes. This is a small thing that would have looked very silly in CI.
3. **Three skills, one shape.** Mirrored `preflight.md` heading order (When / Checks / If-fails / Cross-refs) so future authors have a template. The Soup Nazi voice ("the line does not move") is reserved for the archetype file; skill files stay neutral and procedural.

## Drift to sweep (orchestrator follow-up)

A targeted sweep of existing `docs/exec-reports/s02e*.md` would likely find:

- Inlined smart-quote greps with subtly different exclusion-dir lists.
- "Skip preflight because docs-only" assertions that do not name what to still run.
- CHANGELOG bullets for process documentation, in violation of the now-canonical rule.

These should be left in place (do not retro-edit history) but flagged so the next sweep episode standardizes them by linking to the new skills rather than by rewriting the inline copies.

## Metrics

- Diff size: 5 files, ~270 insertions, ~1 deletion (one-line replacement in soup-nazi archetype).
- Test delta: 0 / 0 (no test changes).
- Preflight: skipped per [`docs-only-commit`](../../.github/skills/docs-only-commit.md) -- diff is `.github/skills/*.md`, `.github/agents/*.md`, `docs/exec-reports/*.md` only.
- ASCII validation: clean on all five files.
- CI status at push time: pending.

## Credits

- **The Soup Nazi** (lead) -- archetype voice, line-on-the-counter framing, "what does NOT belong in CHANGELOG" hard list.
- **Newman** (guest) -- security framing on the docs-lint rule (banned characters as a supply-chain hygiene signal, not just typography), kept the ASCII rule from going soft on CHANGELOG.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
