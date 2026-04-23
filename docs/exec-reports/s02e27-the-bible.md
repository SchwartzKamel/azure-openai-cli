# S02E27 -- *The Bible*

> *Wilhelm and Elaine codify the writers' bible: four process skills the orchestrator stops re-typing.*

**Commit:** `f3046e1` (skills + Larry edit, sibling-shipped); exec-report commit `<filled at push>`
**Branch:** `main` (direct push)
**Runtime:** ~25 minutes
**Director:** Larry David (showrunner)
**Cast:** 1 sub-agent across 1 dispatch wave (Wilhelm leading, Elaine on prose polish)

## The pitch

Every fleet-mode dispatch brief Larry David wrote was carrying the same boilerplate: file boundaries, exec-report shape, pre-flight checklist, shared-file protocol. The repetition was a cohesion debt. The moment one brief drifted, the show drifted with it: a sub-agent would skip the ASCII-punctuation grep, or touch `AGENTS.md` mid-wave, or invent its own exec-report shape.

This episode promotes that boilerplate into four citable skills under `.github/skills/`. Briefs now link to the skill instead of re-explaining the procedure. The orchestrator's archetype gains a "Skills you cite, not re-explain" section so the discipline is one click away from where Larry actually works.

The shape of the show is now formally documented. Next-season blueprints can lean on these without re-deriving them.

## Scene-by-scene

### Act I -- Planning

Showrunner brief from Larry David: codify the process spine, four skills, no production code, no test changes, leave orchestrator-owned files alone for the orchestrator's later batch. Decisions locked:

- Match the existing skill format (H1, intent, numbered procedure, examples, anti-patterns, escalation).
- Tone: direct, plain-spoken, slightly aggrieved -- consistent with `preflight.md` and `commit.md`.
- The exec-report skill **overrides** `_template.md` on conflict. The template is a starter; the skill is the spec.
- Cross-link work limited to `larry-david.agent.md` -- one append section, no other archetype touched.

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | wilhelm (lead) + elaine (prose) | Four new skills written, Larry David archetype cross-linked, exec report drafted. |

### Act III -- Ship

ASCII-punctuation grep run against all six touched files -- clean. No code changed; preflight not required. Single commit, Conventional Commits format, Copilot trailer, `-c commit.gpgsign=false`. Push to `main` with rebase-on-non-fast-forward in case other agents pushed concurrently.

## What shipped

**Production code** -- n/a (process scaffolding, no runtime change).

**Tests** -- n/a (no code touched).

**Docs** -- four new skills under `.github/skills/`:

- `episode-brief.md` -- canonical sub-agent dispatch prompt structure (11 required sections + worked example).
- `exec-report-format.md` -- spec for `sNNeMM-*.md` exec reports, with a reviewer checklist; explicitly overrides `_template.md` on conflict.
- `fleet-dispatch.md` -- orchestrator pre-flight checklist, the "never solo background dispatch" rule, the "wave on collision risk" guidance, and the failure-mode catalog.
- `shared-file-protocol.md` -- orchestrator-owned files table (eight entries), the sub-agent protocol, the orchestrator batch protocol, and `CHANGELOG.md` append rules.

Plus one cross-link: `.github/agents/larry-david.agent.md` gains a "Skills you cite, not re-explain" section near the top, listing each new skill with a one-line summary.

**Not shipped** (intentional follow-ups for the orchestrator's batch):

- `AGENTS.md` skills table needs four new rows (`episode-brief`, `exec-report-format`, `fleet-dispatch`, `shared-file-protocol`). Orchestrator-owned -- not touched.
- `.github/copilot-instructions.md` mirror needs the same four rows -- orchestrator-owned.
- `docs/exec-reports/README.md` needs the S02E27 row -- TV-guide, orchestrator-owned.
- `docs/exec-reports/s02-writers-room.md` needs the S02E27 episode marked aired and cast distribution updated -- orchestrator-owned.
- `episode-brief.md` references `ascii-validation` skill that belongs to S02E28 -- inline grep used until that skill lands; cross-link will tighten then.
- `shared-file-protocol.md` references `mr-lippman` agent for the release-cut step; the link is intentionally bare since the archetype file lives at a stable path.

## Lessons from this episode

1. **The cohesion debt was real.** Writing the four skills exposed at least three places where Larry's archetype already encoded the procedure inline -- the pre-flight checklist most prominently. Lifting it into `fleet-dispatch.md` means it can now be cited from any agent's reasoning, not just Larry's.
2. **The `_template.md` vs skill split needed a tiebreaker.** Without an explicit "skill wins" clause, two sources of truth would have drifted within a season. Caught early.
3. **Forward references are a planning hazard.** `episode-brief.md` wants to point at `ascii-validation`, which is a future episode's deliverable. Left a note inline rather than dangling-link the doc; orchestrator can tighten on follow-up.

## Metrics

- Diff size: 5 process files (~290 insertions) shipped under sibling commit `f3046e1`; this exec report commit adds 1 file. A parallel S02E27 dispatch beat this one to the push -- both wrote functionally equivalent skill content; the sibling's versions were accepted to avoid clobbering merged work. Sibling commit landed under a mislabeled subject (`docs(market): 2026 competitive refresh`) -- noted for the orchestrator's batch.
- Test delta: n/a (docs-only).
- Preflight: not required (no `*.cs` / `*.csproj` / `*.sln` / workflow changes); ASCII grep run instead -- clean.
- CI status at push time: docs-only path, expected green.

## Credits

- **Mr. Wilhelm** (lead) -- structure, procedure, denylist rationales, anti-pattern catalogs.
- **Elaine** (guest) -- prose polish; the skills read as instructions, not lawyer-speak.
- **Larry David** (orchestrator) -- conceived the episode, named it, wrote the brief, will batch the orchestrator-owned follow-ups.

Commits associated with this episode carry the `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>` trailer per [`commit`](../../.github/skills/commit.md).
