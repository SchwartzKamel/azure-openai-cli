# Skill: exec-report-format

**The canonical shape of every `docs/exec-reports/sNNeMM-*.md` file.** Owned by Mr. Wilhelm (process), prose-polished by Elaine. The starter scaffold lives at [`docs/exec-reports/_template.md`](../../docs/exec-reports/_template.md); this skill is the spec. **If the template and this skill disagree, the skill wins** -- the template is a head start, not a contract.

Every aired episode needs an exec report at `docs/exec-reports/sNNeMM-kebab-title.md`. The shape below is what reviewers, future-you, and the season finale retrospective rely on.

## Required sections, in order

### Front matter (after the H1)

```text
# SNNeMM -- *The <Noun>*

> *One-line log line, TV Guide style. Under 20 words.*

**Commit:** `<sha>` (or list if multi-commit)
**Branch:** `<branch>` (direct push / PR #N)
**Runtime:** `<rough wall-clock>`
**Director:** Larry David (showrunner)
**Cast:** N sub-agents across M dispatch waves
```

All five fields are mandatory. `Director` may name a different orchestrator for pre-Larry episodes (canonical history -- do not retro-edit).

### The pitch

Two or three paragraphs. The problem, why now, what changed. If a teammate cannot summarize the episode after reading this section alone, rewrite it.

### Scene-by-scene

Three acts. Act II must include the wave table:

```text
### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | agent-a, agent-b  | one-line summary |
| **2** | agent-c           | one-line summary |
```

Act I covers planning and pivots. Act III covers commit / preflight / push / CI state.

### What shipped

Four sub-blocks, every one present (use "n/a" if truly empty -- but interrogate that first):

- **Production code** -- files, responsibilities, key design choices.
- **Tests** -- counts and coverage notes.
- **Docs** -- what was written or updated.
- **Not shipped** -- intentional follow-ups, with why.

### Lessons from this episode

Numbered list. Blind spots caught (and by whom), process misses, patterns worth keeping. If "Lessons" is empty, the report is incomplete -- every episode teaches something.

### Metrics

- Diff size (insertions / deletions / files)
- Test delta (new tests, coverage shifts -- "n/a" allowed for docs-only)
- Preflight result (passed / skipped-with-reason)
- CI status at push time (link the run if red or pending)

### Credits

Which agents contributed and to what. Always reaffirm the `Co-authored-by: Copilot` trailer was on the commits.

## Reviewer checklist

Run this against any draft before merge:

- [ ] H1 matches `SNNeMM -- *The <Noun>*`.
- [ ] Log line under 20 words, italicized blockquote.
- [ ] All five front-matter fields present.
- [ ] Pitch is 2-3 paragraphs, not a bullet dump.
- [ ] Scene-by-scene has three acts; Act II has the wave table.
- [ ] What-shipped has all four sub-blocks.
- [ ] Lessons section is not empty.
- [ ] Metrics include preflight + CI state.
- [ ] Credits names the cast and confirms the trailer.
- [ ] ASCII punctuation only (run the grep -- see [`episode-brief`](episode-brief.md)).

## Anti-patterns

- **Skipping "Not shipped".** Hidden follow-ups become hidden debt. Name them.
- **Vague metrics ("small diff", "tests pass").** Numbers or it didn't happen.
- **Editing `_template.md` to change the shape.** That changes the starter, not the spec. Edit this skill instead, then propagate to the template.
- **Retro-editing director attribution.** Pre-S02E07 episodes were directed by Copilot before Larry was cast; that is canonical history.

## Cross-links

- Starter scaffold: [`_template.md`](../../docs/exec-reports/_template.md)
- Dispatch brief that produced the episode: [`episode-brief`](episode-brief.md)
- Commit conventions used at the bottom of Act III: [`commit`](commit.md)
