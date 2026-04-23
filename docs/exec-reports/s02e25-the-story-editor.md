# S02E25 -- *The Story Editor*

> *Elaine builds the docs map a new contributor was missing. No prose rewrites; signposts only.*

**Commit:** `<filled-on-push>`
**Branch:** `main` (direct push)
**Runtime:** ~25 min wall-clock
**Director:** Larry David (showrunner)
**Cast:** 3 agents in 1 wave (Elaine lead; Lloyd Braun + Mickey Abbott guests)

## The pitch

After 31 aired episodes, `docs/` had grown to 200+ files across 30+
subdirectories. Top-level `README.md` linked perhaps a dozen of them,
the rest were reachable only by `find` or by knowing they exist.
A new contributor landing on the repo had no map -- no single place
that said "here are the architecture docs, here are the runbooks,
here is the episode log, here are the proposals." The cost was real:
proposal threads cited stale paths, Lloyd-class onboarders asked the
same five questions per cohort, and orphan docs (good content, no
inbound link) accumulated faster than they were referenced.

This episode is a *consolidation* pass, not a content pass. We added
the entry point (`docs/README.md`), wired sibling cross-links across
`docs/process/` and the season blueprints, and pointed the project
README at the new map with one line. No exec report was edited
(immutable post-air). No proposal body was rewritten. No content
moved. The structure that was already there is now *findable*.

## Scene-by-scene

### Act I -- Planning

- Confirmed scope: docs-only, surgical cross-links, no content rewrites, no docs-site generator.
- Confirmed orchestrator-owned denylist: `AGENTS.md`, `.github/copilot-instructions.md`, `docs/exec-reports/README.md`, `docs/exec-reports/s02-writers-room.md`. Read-only.
- Inventoried `docs/`: 226 files total, 234 markdown files counting top-level. 16 immediate subdirectories under `docs/`.
- Decision: single new entry point at `docs/README.md` (not a multi-file site). Each section caps at 2-5 bullets so the map stays scannable.

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | Elaine (lead), Lloyd Braun (junior-dev lens), Mickey Abbott (a11y) | New `docs/README.md` map. Sibling cross-links added to all four `docs/process/*.md` files. Prev/next blueprint footers added to S03-S06. One-line link in top-level `README.md`. |

Lloyd and Mickey ran their passes on Elaine's drafts in the same wave;
no separate dispatch needed for a docs-consolidation episode this size.

### Act III -- Ship

- ASCII grep on every authored or touched file: clean.
- `git diff --cached --name-only` verified against the deliverable list before commit.
- Single docs-only commit. Per [`docs-only-commit`](../../.github/skills/docs-only-commit.md), full preflight skipped; ASCII grep run.
- Pushed to `main`.

## What shipped

**Production code** -- n/a (docs-only episode).

**Tests** -- n/a (docs-only episode).

**Docs**:

- **NEW** `docs/README.md` -- the docs-tree map. Sections: getting started, architecture, operating the CLI, process, exec reports, proposals, security, performance, release/ops/migration, specialist trees, conventions, provenance.
- **EDIT** `README.md` (top-level) -- one line in the existing `## Documentation` section pointing at `docs/README.md`. No restructure.
- **EDIT** `docs/process/change-management.md`, `docs/process/adr-stewardship.md`, `docs/process/cab-lite.md`, `docs/process/retrospective-cadence.md` -- appended `## Sibling process docs` footer to each (one section, four bullets, links only).
- **EDIT** `docs/exec-reports/s03-blueprint.md`, `s04-blueprint.md`, `s05-blueprint.md`, `s06-blueprint.md` -- appended `## Adjacent blueprints` prev/next footer to each. No body edits.
- **NEW** `docs/exec-reports/s02e25-the-story-editor.md` -- this report.

**Not shipped** (intentional follow-ups):

- ADR -> parent FR back-links: not added this episode. Most ADRs do not have a single clean parent FR (e.g., ADR-005 spans FR-018/019/020), and inventing a back-link risks misattribution. Queued as a finding.
- Redundancy collapse (e.g., `accessibility.md` vs `accessibility-review-v2.md` vs `accessibility/` subdir; `cost-optimization.md` vs `cost/pricing-sourcing.md`; `competitive-analysis.md` vs `competitive-landscape.md`; `i18n.md` vs `i18n-audit.md` vs `i18n/`). Out of scope -- consolidation is content work, not signposting. Queued.
- Em-dash / smart-quote sweep across docs Elaine did not touch. Explicitly out of scope per the brief; future Soup Nazi episode.
- Top-level `README.md` Documentation section is currently a flat list whose curation predates the new map. Could be slimmed now that `docs/README.md` exists. Owner: orchestrator + Peterman (README is shared); surfaced here, not edited.
- `docs/exec-reports/README.md` ("the TV guide") is missing rows for E09, E10, E24, E25, E26 and S03+ blueprints don't appear in the seasons table with full status. Orchestrator-owned -- surfaced here, not edited.

## Lessons from this episode

1. **Lloyd's friction passes find the gaps no expert sees.** Three concrete frictions surfaced (see findings below) that Elaine, who knew where every doc lived, would have walked past.
2. **A docs map costs nothing to maintain if every new doc earns its bullet at PR time.** Without that discipline, this map will rot to the same orphan-rate within a season. Surfacing this convention in `docs/README.md` itself.
3. **"Surgical" cross-links beat "comprehensive" restructures.** Adding a four-bullet footer to four process docs took five minutes and made the directory navigable. A graph rewrite would have taken a day and merged worse.
4. **The `docs/exec-reports/README.md` row-add discipline is a known orchestrator batch step, but it has lagged.** Five episode rows missing from the TV guide is a smell; orchestrator should sweep on next writers' room update.

## Findings to log (orchestrator: please append to writers' room backlog)

- `e25-tv-guide-row-lag` [smell, one-line-fix] -- `docs/exec-reports/README.md` is missing rows for S02E09, E10, E24, E25, E26. Orchestrator-owned file; surface for next batch.
- `e25-adr-fr-backlinks-gap` [gap, b-plot] -- ADRs do not link back to the FR proposals that motivated them. Most ADRs span multiple FRs (e.g., ADR-005 -> FR-018/019/020), so a single-parent back-link is wrong; needs a one-line "Implements / informs" footer convention. Queue for an ADR-stewardship episode.
- `e25-accessibility-doc-redundancy` [smell, b-plot] -- `docs/accessibility.md`, `docs/accessibility-review-v2.md`, and `docs/accessibility/` subdir overlap. Same applies to `i18n.md` / `i18n-audit.md` / `i18n/`. Either consolidate or designate one as canonical and the others as historical. Mickey's call.
- `e25-cost-doc-split` [smell, one-line-fix] -- `docs/cost-optimization.md` (user-facing) and `docs/cost/pricing-sourcing.md` (sourcing methodology) live in different roots. One should link to the other; today neither does.
- `e25-competitive-doc-duplication` [smell] -- `docs/competitive-analysis.md` and `docs/competitive-landscape.md` are not obviously distinct. Need one-line front-matter on each clarifying scope, or merge.
- `e25-launch-dir-no-index` [gap, one-line-fix] -- `docs/launch/` has 14 files and no `README.md` index. Lloyd hit this directly. Add a one-line index file, owner Mr. Lippman.
- `e25-orphan-docs` [smell] -- `docs/aot-trim-investigation.md`, `docs/dogfooding-baseline-2026-04.md`, `docs/why-az-ai.md`, `docs/persona-guide.md` (the file, not its README link), and several others have no inbound link from any other markdown file. Now linked from the new map; convention going forward must keep the map current.
- `e25-readme-documentation-section-flat` [smell] -- top-level `README.md` "Documentation" section is a curated flat list whose entries predate the new map. Could collapse to a pointer + a curated three-bullet shortlist. Orchestrator + Peterman own; surfaced for review.

## Lloyd's friction list (junior-dev lens)

Three friction points Lloyd flagged while reading `docs/README.md`, then `CONTRIBUTING.md`, then a random episode (S02E13 *The Inspector*):

1. **"What is a 'fleet dispatch wave'?"** The phrase appears in the new map's Provenance section (and in dozens of exec reports) without a glossary entry. `glossary.md` defines Ralph mode and Squad but not the dispatch vocabulary (wave, episode, showrunner, brief). Fixed inline by phrasing the map to not require the term; queued as a glossary expansion finding.
2. **"What is `az-ai`? Is it the same as `azure-openai-cli`?"** Top-level README opens with the binary name without a one-line "this binary is the project" tie-in. Lloyd had to grep the README to confirm. Out of scope to fix here (orchestrator-owned README); surfaced.
3. **"Where do I file a bug?"** No prominent link to GitHub Issues from the new map or from `CONTRIBUTING.md`'s top section. Both reference the issue tracker only midway through. Surfaced; one-line CONTRIBUTING addition would suffice (orchestrator-owned).

## Mickey's a11y findings

Mickey's pass on the files Elaine authored or touched (the new map plus the eight cross-link footers):

1. **Heading hierarchy.** `docs/README.md` is H1 -> H2 only (no skipped levels). Process and blueprint footers use H2 only, no nesting. Pass.
2. **Link text.** All links in authored content are descriptive (file paths or doc titles). No "click here", no "this", no bare URLs. Pass.
3. **Code-fence language tags.** No code fences in authored content (intentionally; this is a map, not a guide). Pass by absence.
4. **Screen-reader friendliness.** Section headings are noun phrases, not sentence fragments. Bullets are short and parallel-structured. Pass.

Mickey did NOT audit files outside this episode's diff (out of scope; queued as a future a11y sweep).

## Metrics

- Diff size: 1 new file (`docs/README.md`, ~135 lines), 8 cross-link footer appends (5-7 lines each), 1 README.md two-line addition, 1 new exec report. Total: ~2 files NEW + ~9 files EDIT.
- Test delta: n/a (docs-only).
- Preflight result: skipped per [`docs-only-commit`](../../.github/skills/docs-only-commit.md). ASCII validation grep run on every authored/touched file -- clean.
- CI status at push time: `docs-lint` workflow expected green (all touched files ASCII-clean and outside the upstream exclusion list).

## Credits

- **Elaine Benes** (lead) -- inventory, map authorship, cross-link surgery, exec report.
- **Lloyd Braun** (guest) -- friction pass on the map and onboarding flow; surfaced three frictions.
- **Mickey Abbott** (guest) -- a11y review of authored content; heading / link-text / language-tag check.

`Co-authored-by: Copilot` trailer present on the commit.

## Cross-links

- The new map: [`../README.md`](../README.md).
- Skills cited: [`docs-only-commit`](../../.github/skills/docs-only-commit.md), [`ascii-validation`](../../.github/skills/ascii-validation.md), [`shared-file-protocol`](../../.github/skills/shared-file-protocol.md), [`findings-backlog`](../../.github/skills/findings-backlog.md), [`exec-report-format`](../../.github/skills/exec-report-format.md).
- Adjacent episodes: previous aired -- [`s02e23-the-adversary.md`](s02e23-the-adversary.md); subsequent in arc -- TBD by orchestrator.
