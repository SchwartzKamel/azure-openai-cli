# Process docs

> *Wilhelm's filing cabinet. The rules around the verbs.*

This directory is the home for project **governance** -- the policies that
sit one layer above the [`.github/skills/`](../../.github/skills/) verbs.
Skills tell you *how* to perform a recurring procedure (preflight, commit,
ascii-validation, ci-triage). Process docs tell you *when* a procedure is
required, *who* must sign off, *what* class of change earns an ADR, and
*how* findings flow from an exec report into the backlog and back out as
a future episode or a one-line fix.

Owned by **Mr. Wilhelm** (process and change management), with audit
support from **The Soup Nazi** (style and merge gates) and **Jerry**
(CI / release pipeline).

## Index

| Doc | Purpose |
|-----|---------|
| [`change-management.md`](change-management.md) | Change classes (cosmetic / additive / breaking / security), per-class review requirements, the stage-gate sequence, and the PR flow that ties them together. |
| [`adr-stewardship.md`](adr-stewardship.md) | When a change earns an ADR vs. a backlog entry, the ADR template, supersession rules, and the index discipline. |
| [`cab-lite.md`](cab-lite.md) | Lightweight "Change Advisory Board" -- when a change needs cross-cast review (Newman + Jackie + Frank, etc.), how the consult is requested, and how the decision is recorded. |
| [`retrospective-cadence.md`](retrospective-cadence.md) | Season-finale retro format, monthly mini-retros, post-incident retros, and the carry-forward path into the next season's writers' room. |

## How these relate to the rest of the repo

```text
   docs/process/*.md          -- governance: rules, gates, decision frames
        |
        v
   .github/skills/*.md        -- procedures: how to run a gate
        |
        v
   .github/agents/*.agent.md  -- archetypes: who runs which gate
        |
        v
   docs/adr/ADR-*.md          -- decisions: the "why" behind locked-in choices
        |
        v
   docs/exec-reports/*.md     -- episode log: what shipped, what taught us
```

Process docs reference skills; skills reference agents; agents and skills
together enforce process. ADRs are the durable output of the process when
a decision is significant enough to outlive the episode that produced it.

## Provenance

Stood up in **S02E22 *The Process*** -- see
[`docs/exec-reports/s02e22-the-process.md`](../exec-reports/s02e22-the-process.md).
Before this directory existed, change-management discipline lived in
agent prompts ([`.github/agents/wilhelm.agent.md`](../../.github/agents/wilhelm.agent.md)),
the PR template ([`.github/PULL_REQUEST_TEMPLATE.md`](../../.github/PULL_REQUEST_TEMPLATE.md)),
[`CONTRIBUTING.md`](../../CONTRIBUTING.md), and tribal knowledge between
the showrunner and the cast. That is now consolidated here.

## Anti-patterns

- **"Process is overhead, just ship it."** Process is the muscle memory
  that prevents the next `180d64f` (five red runs on `main` because
  preflight got skipped). Read [`preflight`](../../.github/skills/preflight.md)
  if you doubt the cost.
- **Adding a new gate without writing it down here.** A gate that lives
  only in one reviewer's head will be skipped the first time that
  reviewer is on vacation.
- **Editing one process doc without checking the others for drift.**
  These four cross-reference each other; a change to the change-class
  table in `change-management.md` may invalidate a row in `cab-lite.md`.
