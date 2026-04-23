# Skill: shared-file-protocol

**The list of orchestrator-owned files and the protocol sub-agents follow around them.** Owned by Mr. Wilhelm. Cited by every dispatch brief (see [`episode-brief`](episode-brief.md) and [`fleet-dispatch`](fleet-dispatch.md)).

Some files are shared infrastructure: the cast roster, the episode index, the writers' room, the changelog. When two sub-agents both edit one of these in parallel, the second push fails on non-fast-forward, a re-shoot is required, and the orchestrator's batch update collides on top. The rule is simple: **sub-agents do not touch orchestrator-owned files.** The orchestrator updates them in a single batch after the wave lands.

## Orchestrator-owned files

| File | Why orchestrator-owned | Safe alternative for sub-agents | What the orchestrator commits after the wave |
|------|-----------------------|---------------------------------|----------------------------------------------|
| `AGENTS.md` | Cast roster + skills table -- every episode could plausibly touch it. Concurrent edits collide. | Mention the new agent / skill in the exec report; let the orchestrator harvest. | Roster row, skills-table row, pipeline diagram tweak. |
| `.github/copilot-instructions.md` | Mirror of `AGENTS.md` for the Copilot CLI. Must stay in lockstep with the roster. | Same as above -- do not edit. | Sync edit alongside the `AGENTS.md` batch. |
| `docs/exec-reports/README.md` | The TV guide. Adds a row per aired episode; index ordering matters. | Write your `sNNeMM-*.md` exec report; the orchestrator adds the row. | New row in the aired-episodes table. |
| `docs/exec-reports/s02-writers-room.md` (and future-season equivalents, e.g. `s03-writers-room.md`) | Arc plan + cast distribution table. The orchestrator's working file. | Do not edit; reference it read-only. | Mark the episode aired, update cast distribution, adjust arc plan. |
| `docs/exec-reports/_template.md` | Starter scaffold for new exec reports. Changing it changes every future episode's starting point. | If the spec needs to change, edit [`exec-report-format`](exec-report-format.md) instead. | Propagate spec changes from the skill into the template. |
| `README.md` (top-level) | User-facing project front door. Marketing-tier copy; tone owned by the orchestrator + Peterman. | Propose copy in your exec report's "Not shipped" or in a follow-up issue. | Curate and land the copy change. |
| `CONTRIBUTING.md` | Community-touching governance doc. Tone and policy live with the orchestrator + Uncle Leo. | Propose changes in the exec report. | Land the policy update with cross-links. |
| `CHANGELOG.md` | Append-only release log. Push-timing serialization keeps it safe in practice, but concurrent `[Unreleased]` edits still race. | Note the user-visible change in your exec report under "What shipped." | Append to the correct `[Unreleased]` subsection (`Added`, `Changed`, `Fixed`, `Security`, `Docs`). |

## The protocol for sub-agents

1. **Read** the orchestrator-owned file if you need context. Reading is always safe.
2. **Do not edit** it. Even a one-line tweak collides with another in-flight wave.
3. **Surface the change you would have made** in your exec report -- in "What shipped" if it is user-visible, or in "Not shipped (orchestrator follow-up)" if it is process scaffolding.
4. **Trust the batch.** The orchestrator harvests exec reports after the wave lands and commits the shared-file updates in one pass.

## The protocol for the orchestrator

1. After the wave lands and exec reports are merged, read each report's "What shipped" + "Not shipped" sections.
2. Open all orchestrator-owned files that need updating.
3. Make every shared-file edit in **one commit per file family** (e.g., one commit for `AGENTS.md` + `.github/copilot-instructions.md` together; one commit for the TV guide; one commit appending to `CHANGELOG.md`).
4. Push. If the push rejects on non-fast-forward, rebase and re-push -- never force-push `main`.

## CHANGELOG.md specifics

The append-only protocol:

- Section header is `## [Unreleased]` until [`mr-lippman`] cuts a release.
- Subsections in this order: `### Added`, `### Changed`, `### Fixed`, `### Security`, `### Docs`. Omit empty subsections.
- One bullet per user-visible change. Past tense. Link the PR or commit.
- Process scaffolding (skills, exec reports, agent archetypes) does **not** belong in `CHANGELOG.md`. It is internal.

If two waves both have user-visible changes destined for `CHANGELOG.md`, the orchestrator serializes the appends -- second wave rebases on the first.

## Anti-patterns

- **"It is just one line in `AGENTS.md`."** It is one line that collides with three other one-liners. Defer.
- **Force-pushing `main` to resolve a collision.** Never. Rebase and re-push.
- **Sub-agent appends to `CHANGELOG.md` "to be safe."** Now the orchestrator's batch collides with itself. Surface in the exec report instead.
- **Editing `_template.md` to fix a one-off issue in your exec report.** Fix your report; if the spec is wrong, fix [`exec-report-format`](exec-report-format.md).

## Escalation

If you find a file that *feels* orchestrator-owned but is not on this table, raise it in the exec report. The table is a living register; it grows when collisions teach it to.
