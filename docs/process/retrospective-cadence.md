# Retrospective cadence

> *"I thought we agreed in the last retro... didn't we?"* -- Mr. Wilhelm

Owned by **Mr. Wilhelm** (process). Facilitated by the showrunner
(Larry David). Action-item triage delegated to **Mr. Pitt** (program
management). Post-incident retros co-led with **Frank Costanza**
(SRE / incident response).

A retrospective that produces no carry-forward is a meeting that
produced no value. This doc names the cadences the project actually
runs, the format of each, and the path each retro's output takes
into the next iteration's plan -- specifically, the next season's
writers' room.

---

## 1. The three cadences

| Cadence | Trigger | Lead | Output |
|---------|---------|------|--------|
| **Season-finale retro** | Last episode of every season (next instance: **S02E24 *The Finale***). | Mr. Pitt + showrunner | Finale exec report + S0(N+1) blueprint update |
| **Monthly mini-retro** | First Monday of every month (or the working day closest). | Mr. Wilhelm | Bullet list appended to the current writers' room file |
| **Post-incident retro** | Any red-CI streak ≥ 2 runs, any production regression caught after merge, any CAB-lite `blocked-pending-fix` that almost slipped through. | Frank Costanza + Wilhelm | Findings backlog entries + (if architectural) a new ADR |

Each cadence has a different audience and a different artifact. Do
not collapse them; the season finale is not a substitute for the
monthly retro, and the monthly retro is not a substitute for an
incident retro.

---

## 2. Season-finale retro (canonical: S02E24 *The Finale*)

The finale exec report is the season retro. Mr. Pitt leads; the full
ensemble is on call.

**Required sections** (per [`exec-report-format`](../../.github/skills/exec-report-format.md),
extended for the finale):

1. **The pitch** -- what the season set out to do; whether it did it.
2. **Scene-by-scene** -- one wave per row, every aired episode listed
   with its lead and one-line outcome.
3. **What shipped (season-aggregate)** -- production code (commits,
   files touched, lines added / removed), tests (count delta, coverage
   shift), docs (page count delta), CI incidents (count and MTTR),
   perf deltas (AOT size, p50 / p99 cold-start), `--help` surface
   delta, persona / agent count delta.
4. **Lessons from the season** -- numbered, attributed by episode.
   At least one per main-cast member. Includes Lloyd Braun's
   junior-lens read for blind spots that the senior cast walked past.
5. **Carry-forward** -- the explicit bridge into the next season's
   writers' room (see section 5).
6. **Cast distribution check** -- the cast-distribution table from
   the writers' room, audited against actual aired episodes. Gaps
   noted (e.g., "Russell led zero episodes -- queue him for S03").
7. **Findings backlog audit** -- every open backlog entry classified
   as `closed-this-season`, `carry-to-next-season`, or `wontfix`
   with rationale.
8. **Metrics** -- per [`exec-report-format`](../../.github/skills/exec-report-format.md)
   plus season-aggregate diff stats.
9. **Credits** -- every cast member who appeared at least once,
   ordered by lead-count then guest-count.

The finale's "Carry-forward" section is the input to the next
season's writers' room (`s0(N+1)-writers-room.md`).

**Next instance:** S02E24 *The Finale*. The dispatch brief for E24
should cite this doc and use sections 2 and 5 as the structural
spine.

---

## 3. Monthly mini-retro

Lower stakes, faster turnaround, recurring discipline.

**Format:**

- **When:** First working day of the month. If the showrunner is on
  vacation, the next maintainer-on-call runs it.
- **Where:** Appended to the current season's writers' room file
  (`docs/exec-reports/s0N-writers-room.md`) under a `## Monthly
  retros` heading, with date-stamped sub-headings (`### YYYY-MM`).
- **Who:** Wilhelm leads. Whichever cast members were active in the
  trailing 30 days are referenced; their input is the exec reports
  they shipped.
- **Shape (≤ 200 words per month):**
  1. **Shipped this month.** One bullet per aired episode.
  2. **What worked.** One or two bullets.
  3. **What slipped.** One or two bullets, with the slip cause and
     the proposed mitigation.
  4. **Action items.** Numbered, each with an owner archetype and a
     target episode for resolution. Orphaned action items from prior
     months are surfaced here until they close.

**Anti-pattern:** the monthly retro becomes a status report. Status
goes in the writers' room arc plan. The retro is for *what changed
in our process* this month, not what episodes shipped.

---

## 4. Post-incident retro

Triggered, not scheduled. Co-led by Frank (incident specifics) and
Wilhelm (process implications).

**Triggers (any one):**

- Red-CI streak of ≥ 2 consecutive runs on `main` (per
  [`ci-triage`](../../.github/skills/ci-triage.md)).
- A regression caught after merge that required a fix-forward
  commit within 24 hours.
- A CAB-lite consult outcome of `blocked-pending-fix` that was
  initially missed and only caught on second review.
- Any `### Security` CHANGELOG bullet -- automatic retro on the
  detection path, not just the fix.

**Format (a section in the next exec report or a standalone
`docs/exec-reports/incident-YYYY-MM-DD-slug.md`):**

1. **Timeline.** UTC timestamps. Detection -> diagnosis -> mitigation
   -> resolution -> closeout.
2. **Root cause.** One paragraph. The actual cause, not the
   symptom. ("Preflight skipped" not "tests failed.")
3. **Why the gates did not catch it.** Walk
   [`change-management.md`](change-management.md) section 3 gates;
   identify which one should have blocked, and why it did not.
4. **Findings.** Each finding logged to the backlog per
   [`findings-backlog`](../../.github/skills/findings-backlog.md).
5. **Process changes.** If the gate sequence, a skill, or an agent
   prompt needs to change, propose the change here and (in a
   separate commit) make it. Cite this incident from the change
   commit body.
6. **ADR check.** If the incident invalidated an Accepted ADR's
   Consequence, add a "Consequences observed in practice" sub-block
   to that ADR per [`adr-stewardship`](adr-stewardship.md) section 3.

**Reference incident:** the `180d64f` failure mode (five red runs on
`main` because preflight was skipped) is the canonical worked
example. The fix produced [`preflight.md`](../../.github/skills/preflight.md);
the [`ci-triage`](../../.github/skills/ci-triage.md) skill exists
because of it.

---

## 5. Carry-forward into the next season's writers' room

The bridge. This is what makes retrospectives matter.

**The carry-forward path:**

```text
   Season finale retro
       |
       | "Carry-forward" section
       v
   S0(N+1) writers' room file (e.g., s03-writers-room.md)
       |
       | Pre-seeded sections:
       |  - Open findings backlog (from finale audit, section 2.7)
       |  - Cast under-utilization gaps (from finale audit, section 2.6)
       |  - Process changes proposed by post-incident retros
       |  - ADRs that need a refresh or supersession
       v
   S0(N+1) episode greenlight
       |
       v
   First wave of S0(N+1) addresses the highest-priority
   carry-forward items as B-plots or dedicated episodes.
```

**Rules of the bridge:**

1. **Every carry-forward item has an owner archetype.** No
   "TBD"-owned items make it across the season boundary.
2. **Every carry-forward item has a target wave or episode in
   S0(N+1).** Vague intentions ("address eventually") rot; named
   targets ship.
3. **The writers' room file for S0(N+1) is initialized by the
   finale**, not by the first episode of the new season. The
   showrunner commits the seeded file as part of the finale wave
   so the next season starts with the carry-forward already on the
   board.

**Reference:** the existing season blueprints
([`docs/exec-reports/s03-blueprint.md`](../exec-reports/s03-blueprint.md),
[`s04-blueprint.md`](../exec-reports/s04-blueprint.md), and beyond)
are the long-horizon arc plans. The S0(N+1) writers' room file is
the *near-horizon* pre-seed that the finale produces.

---

## 6. Action-item discipline

Every retrospective produces action items. Without discipline, those
action items rot in the document that birthed them.

**The minimum action-item shape:**

```text
- [ ] <description>  (owner: <archetype>, target: <episode or date>)
```

**Lifecycle:**

1. **Created.** In the retro document.
2. **Surfaced.** Re-listed in the next monthly retro until closed.
3. **Closed.** Checkbox marked in the originating retro AND the
   close referenced in the exec report or commit that closed it.
4. **Audited.** At the season finale, every still-open action item
   from the season's monthly retros is either closed, carry-forwarded,
   or explicitly marked `wontfix` with rationale.

**Orphan rule:** an action item that has been re-surfaced for three
consecutive months without progress is escalated to Mr. Pitt for a
disposition call (greenlight an episode, fold into a planned
episode, or `wontfix`).

---

## 7. Anti-patterns

- **Skipping the monthly retro because "nothing happened this
  month."** Something happened; you shipped or you did not, and
  either deserves one paragraph.
- **A finale exec report with no Lessons section.** That is a
  status report, not a retro. The Lessons section is the entire
  point.
- **Action items with no owner.** "We should improve test
  coverage" with no owner archetype is wishful thinking.
- **Post-incident retros that name a person, not a gate.** The
  retro audits the process; the person is the symptom of a missing
  or skipped gate.
- **Carry-forward sections that do not name target waves.** "Do
  this in S03" with no wave assignment will be forgotten by E03.
- **Editing the finale exec report after season-end** to "add
  lessons we learned later." Retroactive lessons go in the next
  season's retros, not back into the finale. Canonical history is
  canonical.

---

## 8. Cross-references

- Companion process docs:
  [`change-management.md`](change-management.md),
  [`adr-stewardship.md`](adr-stewardship.md),
  [`cab-lite.md`](cab-lite.md).
- Skills:
  [`exec-report-format`](../../.github/skills/exec-report-format.md),
  [`findings-backlog`](../../.github/skills/findings-backlog.md),
  [`ci-triage`](../../.github/skills/ci-triage.md),
  [`shared-file-protocol`](../../.github/skills/shared-file-protocol.md),
  [`preflight`](../../.github/skills/preflight.md).
- Agents:
  [`wilhelm`](../../.github/agents/wilhelm.agent.md),
  [`mr-pitt`](../../.github/agents/mr-pitt.agent.md),
  [`frank`](../../.github/agents/frank.agent.md),
  [`larry-david`](../../.github/agents/larry-david.agent.md).
- Anchor episodes:
  [`s02-writers-room.md`](../exec-reports/s02-writers-room.md)
  (current arc plan; finale will seed `s03-writers-room.md`),
  [`s03-blueprint.md`](../exec-reports/s03-blueprint.md) (long-horizon).
