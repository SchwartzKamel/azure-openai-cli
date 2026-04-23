# Skill: fleet-dispatch

**The pre-flight checklist the orchestrator runs before issuing any sub-agent dispatch.** Owned by Mr. Wilhelm. Lifted from `larry-david.agent.md` so any agent reasoning about dispatch -- not just Larry -- cites the same source.

A bad brief produces a bad episode, and bad episodes cost real time: shared-file collisions, missing exec reports, scope creep, and re-shoots. The checklist below is the price of admission.

## Pre-flight checklist

Before issuing any dispatch:

- [ ] Episode has a name (`*The <Noun>*`) and a number (`SNNeMM`).
- [ ] Lead is cast and is *not* the previous episode's lead.
- [ ] Guest(s) are cast and complementary to the lead (offense/defense, code/docs, product/learner).
- [ ] Scope fits one or two waves of work. If it needs three, split the episode.
- [ ] "Did not do" / `MUST NOT touch` list is explicit -- with a WHY for each entry.
- [ ] Deliverables are listed by path, marked NEW vs EDIT.
- [ ] Orchestrator-owned files are excluded from the sub-agent's diff. See [`shared-file-protocol`](shared-file-protocol.md).
- [ ] Exec report path is specified (`docs/exec-reports/sNNeMM-kebab-title.md`) and follows [`exec-report-format`](exec-report-format.md).
- [ ] Commit conventions stated -- link to [`commit`](commit.md), restate the trailer and `-c commit.gpgsign=false`.
- [ ] Push instruction included (or "stop at commit" if you want to stage manually). Note rebase-on-non-fast-forward when other agents are pushing concurrently.
- [ ] Preflight requirement stated for `*.cs` / `*.csproj` / `*.sln` / workflow changes -- link to [`preflight`](preflight.md).
- [ ] On-completion: SQL todo update + summary shape.

If any box is unchecked, fix the brief before you dispatch. Use [`episode-brief`](episode-brief.md) as the structural template.

## Dispatch rules (non-negotiable)

### Never dispatch a single background sub-agent solo

A single background agent gives you no parallelism and no insulation: if it stalls, you are blocked with no other work in flight. Either run **one sync sub-agent** (when you need its output to make the next decision in the same turn) or dispatch **multiple background sub-agents in the same turn**. One-and-done background dispatch is a smell.

### Wave when collision risk is high

Two sub-agents writing the same file is a known hazard. Wave (sequence) the dispatch when:

- Multiple episodes touch `CHANGELOG.md`, `AGENTS.md`, `README.md`, or any orchestrator-owned file.
- Multiple episodes touch the same C# file or the same test class.
- Episodes have semantic dependencies (episode B reads a doc episode A is writing).

Examples:

```text
WAVE -- two security episodes both bumping a dep version:
  Wave 1: Newman audits, lands the bump.
  Wave 2: FDR runs the chaos pass against the bumped build.

PARALLEL -- two independent docs episodes:
  Same turn: Elaine on persona docs, Peterman on launch copy.
  Disjoint files. No collision.
```

### Tell each sub-agent the full brief

Sub-agents are stateless. They do not remember the last episode, the casting rationale, or the orchestrator's mental model. The brief must be self-contained -- lead, guest, theme, research, deliverables, file boundaries, validation, commit, push, on-completion. Use [`episode-brief`](episode-brief.md).

## Failure modes (paid for in real episodes)

- **Vague file boundaries** -- two sub-agents both edit `AGENTS.md`, second push fails on non-fast-forward, one re-shoot required.
- **Missing exec-report path** -- sub-agent invents a path; episode index points to the wrong file.
- **Missing on-completion SQL** -- writers' room shows episode in-flight; orchestrator double-dispatches.
- **Solo background dispatch** -- agent stalls, no other work in flight, the entire turn is wasted.
- **No "did not do" list** -- sub-agent over-scopes, touches files that belonged to the next episode.

## Anti-patterns

- **"They'll figure it out."** They will not. Brief explicitly.
- **Skipping the wave to ship faster.** Collisions cost more time than waves do.
- **Dispatching from memory.** Always run the checklist; the one item you skip is the one that bites.

## Escalation

If a dispatch fails for a reason not covered here, document the failure mode in this skill in the same PR as the fix. The checklist is a living debt register.
