# S02E22 -- *The Process*

> *Wilhelm finds the Penske file. It was on his desk the whole time.*

**Commit:** `<process-docs sha>` (single docs-only commit; this exec report appended in a follow-up if needed, otherwise same commit)
**Branch:** `main` (direct push)
**Runtime:** ~30 min wall-clock
**Director:** Larry David (showrunner)
**Cast:** Mr. Wilhelm (lead, Process & Change Management), Soup Nazi (guest, style / merge-gate enforcement), Jerry (guest, CI / release pipeline)

## The pitch

Change-management discipline on this project has always existed -- in
agent prompts, in the PR template, in the seven `.github/skills/`
verbs, in `CONTRIBUTING.md`, and most of all in the showrunner's
head. That is fragile. The next maintainer (or sub-agent) who joins
the project does not get to read the showrunner's head; they get to
read the docs.

This episode codifies the *governance layer* in real, citable docs.
Where skills are verbs (how to perform a recurring procedure),
process docs are the rules around the verbs: when a procedure is
required, what review surfaces a change earns by class, when a
finding becomes an ADR vs. a backlog entry, how the season-finale
retrospective bridges into next season's writers' room. Wilhelm
gives the gate sequence a citable home; Soup Nazi audits the
enforcement language; Jerry maps each gate to the workflow that
already runs it.

No code shipped. No tests added. Five new markdown files in
`docs/process/`, all cross-referenced to the existing skills, ADRs,
and agent archetypes they depend on.

## Scene-by-scene

### Act I -- Planning

Read the brief. Confirmed scope boundary: this is governance, not
skills (S02E27 / E28 / E29 own the skills layer). Inventoried
existing process content scattered across:

- [`.github/agents/wilhelm.agent.md`](../../.github/agents/wilhelm.agent.md)
  -- the archetype that names the deliverables but does not produce
  them.
- [`.github/PULL_REQUEST_TEMPLATE.md`](../../.github/PULL_REQUEST_TEMPLATE.md)
  -- the PR-time checklist; references skills but does not codify
  classes or stage gates.
- [`CONTRIBUTING.md`](../../CONTRIBUTING.md) -- contributor-facing,
  references preflight and Conventional Commits but does not name
  CAB-lite, ADR triggers, or retrospective cadence.
- [`docs/adr/README.md`](../adr/README.md) -- ADR format spec, but
  not the *process* around ADRs (when to write one, supersession
  rules, audit cadence).
- The seven skills under `.github/skills/` -- the procedural layer.

Decision: produce four governance docs plus an index README, all
under `docs/process/`. Each doc has a single owner archetype
(Wilhelm), with named consult roles (Soup Nazi for style,
Jerry for CI plumbing, Elaine for prose, Costanza for product /
architecture review where applicable). No edits to orchestrator-owned
files (`AGENTS.md`, `CONTRIBUTING.md`, `CHANGELOG.md`, writers'
room file, exec-reports README).

Pivot considered and rejected: Wilhelm's archetype lists `docs/process.md`
(singular) as a deliverable. The brief asked for `docs/process/*.md`
(four files plus an index). Went with the brief -- a single-file
process doc would have been ~1500 lines, harder to navigate, and
collapse the four distinct concerns into one wall of text. The
archetype's deliverable wording can drift in a future Wilhelm
maintenance pass; out of scope here.

### Act II -- Fleet dispatch

Single-agent episode. Wilhelm wrote everything; Soup Nazi reviewed
the enforcement language inline; Jerry confirmed the gate-to-workflow
mapping in [`change-management.md`](../process/change-management.md)
section 3 against the actual `.github/workflows/` files.

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | Wilhelm (lead, drafting all five docs); Soup Nazi (inline review of enforcement language); Jerry (gate-to-workflow mapping confirm) | Five new files in `docs/process/`, ASCII-clean, internal cross-refs verified |

### Act III -- Ship

ASCII validation grep run on all five new files (zero hits). Internal
link spot-check: every relative path used in a cross-reference
section was verified against `ls` of the target. No code touched, so
preflight is correctly skipped per
[`docs-only-commit`](../../.github/skills/docs-only-commit.md). PR
template not edited (the brief allowed an edit only if alignment
required it; on inspection, the template already references the
skills correctly and a process-doc cross-reference would be a nice
addition but is non-required, so deferred to a follow-up).

Single commit, explicit-path staging, Conventional Commit subject,
Copilot trailer. Direct push to `main`.

## What shipped

**Production code** -- n/a (docs-only, per
[`docs-only-commit`](../../.github/skills/docs-only-commit.md)).

**Tests** -- n/a.

**Docs** -- five new files in `docs/process/`:

- [`docs/process/README.md`](../process/README.md) -- index page;
  diagrams how process docs relate to skills, agents, ADRs, and exec
  reports.
- [`docs/process/change-management.md`](../process/change-management.md)
  -- change classification (cosmetic / additive / breaking /
  security), review requirements per class, the eight-stage gate
  sequence, the ADR-vs-backlog decision tree, the end-to-end PR /
  direct-push flow, branch-protection posture, anti-patterns.
- [`docs/process/adr-stewardship.md`](../process/adr-stewardship.md)
  -- when a change earns an ADR (vs. a backlog entry, vs. just a
  commit), the Nygard template, status lifecycle (Proposed ->
  Accepted -> Deprecated / Superseded), indexing discipline, audit
  cadence (per-episode, mid-season E12, season-finale E24). Cites
  ADR-001, ADR-002, ADR-003 as worked examples.
- [`docs/process/cab-lite.md`](../process/cab-lite.md) -- the
  lightweight Change Advisory Board: triggers, standing role-set
  (15 archetypes), how a consult is requested (in exec report, PR
  comment, or commit body), how the decision is recorded, escalation
  path. Worked examples drawn from S02E07, S02E13, and a
  hypothetical breaking-flag change.
- [`docs/process/retrospective-cadence.md`](../process/retrospective-cadence.md)
  -- the three retrospective cadences (season-finale, monthly,
  post-incident), required sections per cadence, the carry-forward
  bridge into the next season's writers' room, action-item
  discipline. Cites S02E24 *The Finale* as the next instance of the
  season-finale cadence and the `180d64f` failure as the canonical
  post-incident retro example.

**Not shipped** (intentional follow-ups):

- **`.github/PULL_REQUEST_TEMPLATE.md` cross-link to
  `docs/process/`.** The PR template currently links to skills but
  not to the new process docs. A one-line addition (e.g., "For
  governance and stage-gate detail see `docs/process/`") would
  thread the navigation. Brief allowed the edit only if
  alignment-required; alignment is not strictly broken without it,
  so deferred. Surface this for the next PR-template maintenance
  pass.
- **`.github/agents/wilhelm.agent.md` deliverables block update.**
  The archetype lists `docs/process.md` (singular). Now that
  `docs/process/` exists as a directory, the archetype's deliverable
  wording should update. Out of scope per the brief's MUST NOT list
  (this is an agent-prompt edit, separate concern). Surface for
  Larry David's next agent-prompt maintenance pass.
- **`AGENTS.md` skills / docs index.** The "process docs" bucket is
  new; the cast roster file does not yet name `docs/process/`.
  Orchestrator-owned per
  [`shared-file-protocol`](../../.github/skills/shared-file-protocol.md);
  surfaced here for the showrunner's next batch update.
- **`CONTRIBUTING.md` cross-link.** Same logic as the PR template:
  contributor docs could point to `docs/process/` for "want to
  understand how a change moves through the project?" Orchestrator-owned
  per the shared-file protocol.
- **CHANGELOG bullet.** Per
  [`docs-only-commit`](../../.github/skills/docs-only-commit.md) +
  [`shared-file-protocol`](../../.github/skills/shared-file-protocol.md):
  process docs are internal scaffolding, not user-visible, and do
  not earn a CHANGELOG bullet. Confirmed in the brief.

## Lessons from this episode

1. **Wilhelm's archetype already had the spec.** The five focus
   areas in [`.github/agents/wilhelm.agent.md`](../../.github/agents/wilhelm.agent.md)
   (PR process, stage gates, change-advisory review, ADR
   stewardship, retrospective cadence) mapped 1:1 to four of the
   five deliverables here. The archetype was the brief; this
   episode just transcribed it. Lesson: when an archetype has been
   sitting on a deliverables list for months, that list is the
   episode brief in waiting.
2. **Skills vs. process is a real distinction worth defending.**
   Mid-draft, drift toward "let me just add this procedural detail
   to the change-management doc" was real. Held the line: if it is
   *how* to do a recurring procedure, it goes in
   `.github/skills/`; if it is the *rule about when* the procedure
   is required, it goes here. Soup Nazi's enforcement instinct
   helped -- "no soup for you" applies to scope creep too.
3. **The CAB-lite role-set is large (15 archetypes) but the
   discipline is small.** The cost of *naming* a consult, even in
   single-maintainer mode, is one line in the exec report. The
   benefit is that future readers can grep for "Newman consult" and
   find every decision touching tools, network, or supply chain.
4. **Retrospective cadence has a carry-forward path the project
   was missing.** Until this doc, "what carries from S02 into S03"
   lived implicitly in `docs/exec-reports/s03-blueprint.md` (long
   horizon) but had no near-horizon mechanism (the
   `s0(N+1)-writers-room.md` pre-seed). Naming that bridge is the
   single highest-leverage paragraph in the four docs.
5. **Process changes are themselves changes.** Wilhelm's archetype
   says it; this episode honored it. The four docs cite themselves
   as the meta-target -- a future change to the gate sequence in
   `change-management.md` section 3 must itself walk
   `change-management.md` section 3. That is intentional, and the
   anti-patterns sections call it out.

**Findings to log** (for showrunner to append to writers' room
backlog):

- `e22-pr-template-process-doc-crosslink` [smell, one-line-fix] --
  `.github/PULL_REQUEST_TEMPLATE.md` does not reference
  `docs/process/`. One-line addition, opportunistic pickup.
- `e22-wilhelm-archetype-deliverables-drift` [lint] --
  `.github/agents/wilhelm.agent.md` lists `docs/process.md`
  (singular file); now `docs/process/` is a directory of five
  files. Archetype wording should update on next agent-prompt
  maintenance pass.
- `e22-agents-md-process-bucket-missing` [gap] -- `AGENTS.md` does
  not yet enumerate the process docs bucket. Orchestrator
  follow-up; mentioned here per
  [`shared-file-protocol`](../../.github/skills/shared-file-protocol.md).

## Metrics

- **Diff size:** 5 files added, 0 modified, 0 deleted. Approx. 770
  lines added across the five docs. Insertions only; no deletions.
- **Test delta:** n/a (docs-only).
- **Preflight result:** skipped (docs-only per
  [`docs-only-commit`](../../.github/skills/docs-only-commit.md));
  ASCII-validation grep run instead, zero hits across the five new
  `.md` files.
- **CI status at push time:** docs-lint workflow expected to pass
  (ASCII grep clean locally; no other gate is triggered by a
  docs-only `docs/process/**` change). Will watch.

## Credits

- **Mr. Wilhelm** (lead) -- drafted all five docs; structured the
  classification table, the gate sequence, the ADR-vs-backlog
  decision tree, the CAB-lite trigger table, and the
  retrospective-cadence three-tier model. Inhabited the
  archetype's deliverables list and produced what was promised.
- **The Soup Nazi** (guest) -- inline review of enforcement
  language; ensured every "do not skip" was unambiguous; vetoed
  three softening passes that would have turned "must" into
  "should." No soup for ambiguity.
- **Jerry** (guest) -- confirmed the gate-to-workflow mapping in
  `change-management.md` section 3 reflects the actual
  `.github/workflows/` jobs (preflight gate -> the local target;
  ASCII grep -> `docs-lint.yml`; security audit -> the Newman
  archetype's checklist; etc.). Mapped each gate to its
  authoritative reference.

`Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`
trailer present on the commit.
