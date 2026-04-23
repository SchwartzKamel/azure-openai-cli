# S02E18 -- *The Maestro*

> *Twelve prompts in production. None of them written down in one place. Maestro grabs the baton and writes the score.*

**Commit:** `5b502bd` (docs commit) + this report
**Branch:** `main` (direct push)
**Runtime:** ~30 minutes
**Director:** Larry David (showrunner)
**Cast:** The Maestro (lead, prompt engineering / LLM research); Cosmo Kramer (guest, on standby for code wiring -- not called)

## The pitch

Codify the system prompts the CLI uses today, set up the seam for a small
eval framework (design only), and document the temperature cookbook so
future prompt changes are deliberate rather than ambient.

The CLI ships at least twelve distinct prompt artefacts -- the default
system prompt, the safety clause, the agent-mode appendix, the Ralph
appendix, the persona-memory appendix, five Squad personas, the tool
descriptions, and the (empty-by-design) delegate-task envelope. Until
this episode, they were inventoried only in the source code, with no
single page that said "here is every string we put in front of a model
and why". That gap is what this episode closes.

The eval framework stays a design sketch. Building the runner would
have meant fixture seeds, a trait-checker implementation, CI
integration, and a Morty cost-budget review -- each of which deserves
its own writers' room. Locking the seam (prompt IDs, case JSON,
scorecard JSON) is enough for this episode.

## Scene-by-scene

### Act I -- Inventory

Walked `azureopenai-cli/Program.cs`, `azureopenai-cli/Squad/`, and
`azureopenai-cli/Tools/`. Found:

- `DEFAULT_SYSTEM_PROMPT` at `Program.cs:28`.
- `SAFETY_CLAUSE` at `Program.cs:38-39`, applied at `:1507` (agent) and `:1721` (Ralph).
- Agent-mode appendix at `Program.cs:1504-1507` -- composes tool names dynamically.
- Ralph-mode appendix at `Program.cs:1717-1721` -- built fresh per iteration.
- Persona-memory appendix at `Program.cs:727-732` -- conditional on stored history.
- Five Squad personas at `Squad/SquadInitializer.cs:65-119`.
- Six tool descriptions at `Tools/<Name>Tool.cs`.
- `delegate_task` runs an empty envelope -- noted as designed-empty, not a gap.

Twelve prompt IDs total.

### Act II -- Library

Wrote `docs/prompts/library.md`. Per ID: file pointer, composition
(static / composed / runtime-conditional), current text or summary,
intent paragraph, known sensitivities. Cross-linked to existing
deeper specs (`personas/coder.md`, `personas/reviewer.md`,
`personas/security.md`, `safety-clause.md`).

### Act III -- Temperature cookbook

Found `docs/prompts/temperature-cookbook.md` already exists -- 153
lines, prior Maestro work, more thorough than the brief. Preserved
as-is. The orchestrator brief listed it as "(new)" but the existing
content satisfies every bullet of the requested guide and adds known
gaps (Ralph validator inheriting `0.55`) the brief did not specify.
Overwriting would have been destructive.

### Act IV -- Eval framework design

Wrote `docs/prompts/eval-framework.md`. Lighter sibling to the
existing `eval-harness.md` -- focused specifically on the three axes
the brief named (accuracy, latency, token economy), the case JSON
shape, the scorecard JSON shape, and the contributor workflow for
adding a case. Explicit "we are not implementing it this episode"
section names the follow-up work and why it does not fit here.

### Act V -- Ship

CHANGELOG bullet under `[Unreleased] > Added`. Markdown ASCII-only
validated. Two commits per the brief.

## What shipped

**Production code** -- none. Prompt text was inventoried, never edited.

**Tests** -- none. Docs-only.

**Docs:**

- `docs/prompts/library.md` (new, ~430 lines) -- twelve-prompt
  inventory with intent and sensitivities per entry.
- `docs/prompts/eval-framework.md` (new, ~170 lines) -- design sketch
  for the future small eval harness.
- `CHANGELOG.md` -- one bullet under `[Unreleased] > Added`.
- `docs/exec-reports/s02e18-the-maestro.md` (this file).

**Not shipped** (intentional follow-ups):

- Did NOT change any prompt text.
- Did NOT implement the eval harness (designed only).
- Did NOT change any temperature in code.
- Did NOT add a `--temperature` flag.
- Did NOT touch model selection logic.
- Did NOT touch glossary, user-stories, or other episode-owned docs.
- Did NOT overwrite the existing `docs/prompts/temperature-cookbook.md`
  (superior prior work; brief's "(new)" tag was the orchestrator
  unaware of it).

## Lessons from this episode

1. **Most fragile prompt: `ralph-mode-appendix`.** Three jobs in three
   sentences (announce the loop, demand fixing, demand verification)
   and it inherits the wrong temperature default. A model that
   "forgets" any one of those instructions either loops forever or
   declares premature victory. Tracked in cookbook's known-gap
   section.
2. **Most over-engineered: the agent-mode appendix's "rather than
   guessing" phrase.** Added to suppress a hallucinated `ls` in an
   older model; modern models do not need it. Cheap to leave in,
   should be revisited if the appendix ever grows.
3. **Most under-engineered: `delegate-task-context`.** Empty by design
   today. A future episode could add a delegation envelope ("you are
   a sub-agent, your parent asked X, do not delegate further unless
   necessary") without breaking the depth-cap contract.
4. **Process miss avoided:** the brief listed
   `temperature-cookbook.md` as new. Checking the directory first
   prevented a destructive overwrite. Generalisable lesson: when the
   brief says "(new)", verify before writing.
5. **Pattern worth keeping:** the prompt library uses stable IDs
   (`squad-coder`, `ralph-mode-appendix`) that match the eval case
   filenames. That join key is the only reason a future runner can
   tie cases back to prompts without manual mapping.

## Metrics

- Diff size: 3 new files (~600 lines combined), 1 surgical CHANGELOG insert.
- Test delta: 0 (docs-only).
- Preflight: skipped per brief (docs-only).
- ASCII validation: clean (no smart quotes, em / en dashes).
- CI status at push time: green (docs-only changes do not invoke build / test workflows).

## Credits

- **The Maestro** -- inventory, library, eval-framework design,
  exec report. Insisted on the title throughout.
- **Cosmo Kramer** -- on standby for code wiring; not called (no
  prompt text was edited, so no source change required).
- **Larry David** -- showrunner, episode framing, scope discipline
  ("design now, build later").

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
