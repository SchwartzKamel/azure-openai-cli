# S02E19 -- *The Competition*

> Sue Ellen Mischke walks the runway in differentiation as a fashion
> statement. Five competitors picked, three things we wear better,
> three things we deliberately don't. Peterman drafts the catalog copy.

**Commit:** parent `a8fe623` -> two docs commits this episode
**Branch:** `main` (direct push, per `.github/skills/commit.md`)
**Runtime:** ~20 minutes
**Director:** Larry David (showrunner)
**Lead:** Sue Ellen Mischke (Competitive Analysis & Market Positioning)
**Guest:** J. Peterman (catalog narrative for the three differentiators)

## The pitch

The CLI LLM category has matured to the point where every credible
entrant ships a single binary or one-line installer, every credible
entrant supports tool calling, and every credible entrant has an MCP
story. Differentiation is no longer about *having* features; it is
about *fitting a seam*. Most of the field is fighting for the
"interactive terminal copilot" seam. We are not on that runway. This
episode produces an honest, opinionated landscape brief that names
where we lead, where we yield, and the one paragraph each that a
contributor or writer can actually quote without checking notes.

The episode is deliberately small. The long-form research already
exists in `docs/competitive-analysis.md` (April 2026, Costanza / Sue
Ellen / Morty). What was missing was the tight, README-shaped brief
with three differentiators, three accepted gaps, and Peterman copy
sitting next to it for the next time we update the README hero. This
file is that.

## Scene-by-scene

### Act I -- Set selection

Five competitors picked from the long list. The picks represent
distinct points in the design space we could plausibly be confused
with:

- `llm` (Simon Willison) -- the deepest plugin ecosystem.
- `aichat` (sigoden) -- the closest Rust-binary equivalent.
- `mods` (Charmbracelet) -- listed for historical context; archived
  March 2026, succeeded by `crush`.
- OpenAI Python CLI / `azure ai` extension -- the "official" baseline.
- `chatblade` -- the cautionary tale and the upstream pointer.

Cut from the brief: Claude Code, Codex CLI, Gemini CLI, `gh copilot
cli`. They are vertically integrated with their parent platform's
billing and auth and are not substitutes for an Azure-OpenAI-native
binary. The long-form analysis covers them anyway.

### Act II -- Comparison matrix

Seven columns, surgical. Language / runtime, distribution, auth model,
tools / agents, Docker / AOT, license, maintenance signal. The matrix
is intentionally narrow -- this is not the long-form deep dive, this
is the brief.

### Act III -- Three differentiators

Picked the three with the strongest evidence and clearest file
pointers:

1. Per-OS LOLBin credential storage (`Credentials/` -- six adapters,
   no new dependencies). S02E01/E04 lineage.
2. Single-binary AOT with Trivy-clean Alpine image (`csproj`,
   `Dockerfile`, cross-link to `docs/perf/v2.0.5-baseline.md`).
3. First-run wizard for Azure-specific config (`Setup/FirstRunWizard.cs`).
   The Azure deployment-vs-model distinction is the load-bearing thing
   no general-purpose competitor knows.

The fourth candidate -- sub-agent delegation with `MaxDepth=3` --
was cut. Honest read: it is a real feature but the competitive moat
is weaker than the wizard's. Claude Code, Codex CLI, and `gh copilot
cli` all ship multi-agent orchestration; ours is small and bounded
and good for our seam, but we would over-claim if we put it in the
top three.

### Act IV -- Three accepted gaps

Picked for honesty and load-bearing rationale:

1. Azure-OpenAI-only. Every differentiator above is small and tight
   precisely because we solve one provider. Multi-provider doubles
   the wizard, the keystore, and the config surface. Not our user.
2. No interactive TUI. We serve a fire-and-forget seam. A TUI would
   be a different product.
3. No multi-model routing; no prompt template library (the latter
   pending S02E18 Maestro).

### Act V -- Peterman copy

Three drafts, one per differentiator. Catalog-narrative voice. Each
is one paragraph, quotable, marked "draft -- not yet adopted." The
README is orchestrator-owned and waits on Mr. Lippman's release polish
pass before any of these ship.

### Act VI -- Ship

CHANGELOG: one bullet under `[Unreleased] > Added`. Pre-validate
confirmed no smart quotes, em-dashes, or en-dashes in either new
file. Two commits, push to `main`.

## What shipped

**Production code** -- none. Scope discipline.

**Tests** -- none. No code changed.

**Docs:**

- `docs/competitive-landscape.md` (new, ~165 lines).
- `CHANGELOG.md` (one bullet prepended to Unreleased).
- `docs/exec-reports/s02e19-the-competition.md` (this file).

## Did NOT ship (intentional)

- Did NOT update the README hero with Peterman's drafts. Orchestrator-
  owned and waits on Mr. Lippman's E10 release polish pass.
- Did NOT add features in response to gaps. Naming a gap doesn't
  commit us to closing it.
- Did NOT criticize competitors unfairly. The voice is confident, not
  contemptuous. `mods` is named as archived because it is archived;
  `chatblade` is named as effectively archived because upstream
  itself points users at `llm` and `fabric`.
- Did NOT include performance benchmarks. Bania's E05 already shipped
  the cold-start number; cross-linked rather than re-measured.
- Did NOT touch any production code, glossary, user-stories, or other
  episode docs.
- Did NOT modify the existing `docs/competitive-analysis.md`. That is
  the long-form research deliverable; this brief is its README-shaped
  sibling and cross-links to it.

## Lessons from this episode

1. **The most surprising competitor capability we should track:** MCP
   has gone from emerging-standard to table-stakes inside one calendar
   year. Every entrant in the long-form matrix except us has shipped
   it. We named "no MCP" as a known gap in
   `docs/competitive-analysis.md` and FR-013; we did not re-name it
   here because it is a closing-gap, not an accepted-gap. Worth
   flagging to Mr. Pitt for roadmap weighting.
2. **`crush` (Charmbracelet's `mods` successor) is the one to watch.**
   Charm's TUI craft plus MCP plus Go single-binary is the closest
   competitor in our lane that is *also* still actively developed.
   Re-check at the next refresh.
3. **The wizard is a stronger moat than we usually credit.** It came
   out of S02E01 as a usability feature; it shows up here as the
   single competitive advantage no general-purpose tool can match
   without taking on Azure-specific knowledge. Worth remembering when
   prioritizing wizard hardening work.
4. **Picking three differentiators forced an honest cut.** Sub-agent
   delegation with bounded depth almost made the list and got cut on
   the merits. The discipline of "three, with file pointers" is
   sharper than "everything we like about ourselves."

## Metrics

- Diff size: 3 files changed, ~175 insertions, 1 deletion.
  - `docs/competitive-landscape.md` -- new (~165 lines).
  - `CHANGELOG.md` -- +8 / -1 (one bullet prepended).
  - `docs/exec-reports/s02e19-the-competition.md` -- new.
- Test delta: none. No code changed.
- Preflight: not required (docs-only). Pre-validated for smart quotes,
  em-dashes, and en-dashes -- clean.
- CI status at push time: docs-only commits; CI runs build/test/format
  on the unchanged codebase. Expected pass.

## Credits

- **Sue Ellen Mischke** -- lead. Picked the comparison set, wrote the
  matrix, cut the differentiator list from four to three, wrote the
  positioning sentence, owned the voice.
- **J. Peterman** -- guest. Three catalog-narrative paragraphs at the
  bottom of the brief, each one quotable, each one marked "draft --
  not yet adopted." Tallinn, the espresso machine, and the four
  questions Azure actually requires are his.
- **Larry David** -- showrunner. Cast the episode and signed off on
  the scope discipline (no README touch, no new features, no
  re-benching).

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
