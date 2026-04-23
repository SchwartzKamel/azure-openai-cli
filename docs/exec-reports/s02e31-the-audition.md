# S02E31 -- *The Audition*

> *Puddy auditions the existing five before twelve more take the
> stage. Either it works or it doesn't.*

**Commit:** `<filled at push>`
**Branch:** `main` (direct push)
**Runtime:** ~25 minutes
**Director:** Larry David (showrunner)
**Cast:** 2 sub-agents -- David Puddy (lead), The Maestro (guest)

## The pitch

S02E30 *The Cast* is in flight to add 12 Seinfeld-themed personas
to the runtime menu. Before that menu triples in size, the
existing five generics (`coder`, `reviewer`, `architect`, `writer`,
`security`) deserved an adversarial behavior pass. If the menu we
have today has rough edges, the menu we ship tomorrow inherits
them.

Puddy ran the audition. Twelve adversarial categories, twenty-six
xUnit cases, no Squad code touched. Where a test exposed a real
gap, the case was marked `[Fact(Skip = "...")]` with a finding
name and the gap logged to `s02e31-findings.md` for the
orchestrator to integrate into the writers' room backlog. Maestro
sat in for prompt-engineering judgment on the system-prompt
content checks (out-of-character refusal clauses, tool-availability
contradictions).

The headline find: routing is substring-based with stable-sort
tiebreak, and the natural English phrase "review the security of
this code" three-way-ties at score 1 and routes to `coder` --
because coder's rule was declared first. We pinned that surprise
with a passing test (the user expectation is broken, but the code
behavior is consistent), and logged the substring-overshadow as a
real bug for a future episode to fix.

## Scene-by-scene

### Act I -- Reading the room

Read the Squad sources (`SquadConfig`, `SquadCoordinator`,
`PersonaMemory`, `SquadInitializer`), the existing
`SquadTests.cs` (44 cases) and `SquadInitializerTests.cs` for
style, and the five generic personas' system prompts. Cross-
referenced S02E18 *The Maestro*'s prompt inventory -- specifically
the `ralph-mode-appendix` finding `e18-ralph-mode-temperature-
inheritance` -- to seed the adversarial categories.

Pivot during read: discovered that `azureopenai-cli/Squad/
SquadInitializer.cs` was in a broken intermediate state in the
working tree (E30's parallel sub-agent had landed a
`AddCastPersonas` call without yet defining the helper).
Procedure: stash E30's WIP for the duration of preflight, run
clean preflight against the unmodified HEAD source, restore E30's
WIP afterward. (E30 made further progress during preflight; their
newer state arrived clean and built. Stash superseded and dropped.
File-boundary discipline preserved -- S02E31 never edited
`Squad/*`.)

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | David Puddy, The Maestro | 26 adversarial cases written; 21 pass, 5 Skipped with finding refs; 9 findings logged |

Single wave. No code under test was modified.

### Act III -- Preflight, commit, push

- **Preflight:** `make preflight` -> 150 passed, 3 skipped, all gates green.
- **ASCII validation:** new test file + new exec report + new findings file all clean of smart quotes / em-dashes (grep'd).
- **Commit:** single commit, three files staged explicitly (no `git add -A`).
- **Push:** direct to `main`, fast-forward expected.

## What shipped

### Production code

n/a -- this is a test-only audit episode. Per file boundaries,
`azureopenai-cli/Squad/*` was read-only.

### Tests

`tests/AzureOpenAI_CLI.Tests/Squad/PersonaBehaviorTests.cs` (NEW),
26 cases across:

- 4 cases on per-persona prompt sanity (5 generic personas + 1
  coder-specific check; theory expanded inline).
- 1 case on out-of-character coder prompt presence (passes; 2
  Skipped on stay-in-character clause -- finding `e31-personas-no-
  stay-in-character-clause`).
- 2 cases on persona-name-collision routing (both pass; logged as
  findings `e31-routing-substring-coder-overshadow` and
  `e31-write-not-in-writer-keywords`).
- 2 cases on empty / whitespace persona name (both pass).
- 2 cases on unknown persona handling (both pass; logged
  `e31-auto-routing-silent-fallback`).
- 1 case on 32 KB memory cap with exact-bound assertion (passes).
- 4 theory rows on casing normalization (all pass) + 1 Skipped on
  kebab/snake normalization for multi-word names (`e31-persona-
  name-no-kebab-snake-normalization`).
- 1 case on routing keyword-overlap tiebreak determinism (passes
  -- input order wins).
- 1 case on empty system prompt loadability (passes; logged
  `e31-persona-empty-system-prompt-not-validated`).
- 1 case on persona-tool-vs-mode contradiction (passes; logged
  `e31-persona-tool-availability-contradiction`).
- 1 case on concurrent memory isolation (passes).
- 2 Skipped on persona + ralph / persona + agent composition
  (`e31-persona-ralph-composition-untested`,
  `e31-persona-agent-tool-override-untested`).

**Counts:** 26 total -> 21 passed, 5 Skipped, 0 failed.

### Docs

- `docs/exec-reports/s02e31-the-audition.md` -- this report.
- `docs/exec-reports/s02e31-findings.md` -- 9 findings in canonical
  format, staged for orchestrator integration into
  `s02-writers-room.md` -> "Findings backlog". Per the brief's
  alternative staging path (cleaner attribution than direct
  append-only edits to the writers' room).

### Not shipped (orchestrator follow-up)

- **Writers' room integration.** Nine findings staged in
  `s02e31-findings.md` need to land in the active "Findings
  backlog" section of `s02-writers-room.md`. Suggested b-plot
  pairings: `e31-routing-substring-coder-overshadow` and
  `e31-write-not-in-writer-keywords` could fold into a single
  one-line-fix episode that adds word-boundary matching and seeds
  writer's pattern with `write,draft,compose`.
- **No Squad code edits.** Per scope: this episode tests current
  behavior; fixing it is out of scope. Future episode candidates
  are flagged in each finding's disposition.
- **No CHANGELOG entry.** Adversarial test additions are not
  user-visible, per the brief.
- **No persona-guide doc.** S02E30 territory; do not preempt.

## Lessons from this episode

1. **Substring routing has a sharp edge.** "review the security
   of this code" three-way-ties on coder/reviewer/security and
   routes to coder. Word-boundary matching would have routed it
   to either reviewer or security and let the score break the
   tie -- both more intuitive answers. Logged as
   `e31-routing-substring-coder-overshadow` (bug, not smell:
   the user's natural phrasing produces a wrong-feeling answer).
2. **The pre-cast audit was worth doing.** Without S02E31, the
   substring-overshadow bug would have shipped invisibly with
   12 more personas, each adding new substring collision
   surface. The same is true for the kebab/snake normalization
   gap and the empty-system-prompt validation gap -- all three
   are amplified by E30's expansion.
3. **Shared working trees with parallel sub-agents need a
   protocol.** E30's WIP broke my baseline build. The
   stash-isolate-restore-or-drop maneuver worked, but it is
   not in `shared-file-protocol.md`. Recommend extending that
   skill with a "shared working tree" section: when two
   sub-agents have overlapping edits in the same uncommitted
   working tree, the second agent isolates the first's WIP via
   stash for the duration of preflight. (Logging this as a
   process recommendation, not a finding -- it's a skill
   addition for orchestrator to consider.)
4. **Skipped-with-finding-name is the right idiom for
   audit-only episodes.** Five tests are Skipped, each with a
   `Skip = "Behavior gap; tracked as finding e31-..."` payload
   that is greppable, traceable, and survives unrelated test
   refactors. When a future episode fixes the underlying gap,
   the un-Skip is one-line, the finding closes, the loop closes.
5. **Extracting a pure helper for prompt composition is the
   blocker on testing persona+ralph and persona+agent.** Two of
   the five Skipped tests sit on this. A small refactor
   (`RalphSystemPromptBuilder.Compose(personaPrompt, safety,
   history) -> string` static helper) would make both
   testable in isolation. Filed as part of
   `e31-persona-ralph-composition-untested`.

## Metrics

- Diff size: 1 NEW test file (~330 lines), 2 NEW doc files
  (~120 + ~150 lines). 0 deletions. 0 production code touched.
- Test delta: +26 (21 pass, 5 Skipped). Total project tests
  unchanged in pass count but broader behavioral coverage of
  the Squad module.
- Preflight: passed -- 150 unit tests + integration tests, all
  gates green. Stash-isolate-restore maneuver applied to
  preserve S02E30's parallel WIP.
- ASCII validation: clean (grep returned zero hits for U+2018
  / U+2019 / U+201C / U+201D / U+2013 / U+2014).
- CI status at push time: expected green (test code only,
  preflight passed locally with a clean tree of HEAD source).

## Credits

- **David Puddy** -- adversarial test design, all 26 xUnit
  cases, finding identification, exec report. Stoic throughout.
- **The Maestro** -- prompt-engineering judgment on the
  out-of-character refusal-clause tests and the tool-
  availability contradiction. His S02E18 prompt inventory
  was the seed for the persona+ralph and persona+agent
  composition tests.
- **Larry David** -- showrunner, scope discipline (no Squad
  code edits), shared-tree protocol call (stash-isolate the
  parallel agent's WIP rather than fix-forward).

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
