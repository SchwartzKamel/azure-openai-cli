# Skill: episode-brief

**The canonical structure of a sub-agent dispatch prompt.** Owned by Mr. Wilhelm (process). Cited by Larry David (orchestrator) on every dispatch. If a brief is missing one of the sections below, the episode is at risk before the cameras roll.

A bad brief is a bad episode. The cost is concrete: missing file boundaries cause merge collisions; missing deliverables produce vague exec reports; missing validation steps put non-ASCII punctuation into shipped docs. Every line below is paid for in a prior incident.

## Required sections, in order

1. **Casting** -- name the lead, name the guest(s), and explain the pairing. One sentence each. "Newman + FDR (offense paired with defense)" is enough; "Kramer leads because" with no rationale is not.
2. **Theme** -- one paragraph. What problem this episode solves and why it has to ship now.
3. **Required research** -- explicit file paths and URLs the sub-agent must read before acting. Stateless agents do not know what you know; tell them what to load.
4. **Deliverables** -- every file the agent will create or edit, marked **NEW** or **EDIT**. No "and any related cleanup."
5. **Required structure** -- if a deliverable is a doc, give the H1/H2 skeleton. Spec the shape, not the prose.
6. **Files MAY touch** -- the allowlist. Anything not on this list is out of scope.
7. **Files MUST NOT touch** -- the denylist. For every entry, one short clause of WHY (orchestrator-owned, in flight elsewhere, separate episode, etc.). See [`shared-file-protocol`](shared-file-protocol.md).
8. **Validation step** -- the ASCII-punctuation grep, build/test gates if code changed, and any episode-specific check. See [`ascii-validation`](ascii-validation.md) once it lands; until then, inline the grep.
9. **Commit conventions** -- link [`commit`](commit.md). Restate the trailer and the `-c commit.gpgsign=false` flag because sub-agents always forget.
10. **Push instruction** -- explicit. `git push origin main`. Note: "rebase on non-fast-forward; other agents push concurrently."
11. **On-completion** -- the SQL todo update (`UPDATE todos SET status = 'done' WHERE id = '<episode-id>';`) and the shape of the summary the sub-agent should return (commit SHA, deliverable list, gaps).

## Worked example (anonymized)

```text
You are filming **S0Xe0Y *The <Noun>*** for `azure-openai-cli`. Working
dir `/home/tweber/tools/azure-openai-cli`, branch `main`.

## Casting
- Lead: <Agent A> -- <one-line specialty>.
- Guest: <Agent B> -- <complementary specialty>.

## Theme
<One paragraph: the problem, the cost of leaving it, the shape of the fix.>

## Read FIRST
1. <path/to/relevant-file-1>
2. <path/to/relevant-file-2>
3. <URL if external context is needed>

## Deliverables
- NEW  <path/to/new-file.md>
- EDIT <path/to/existing-file.cs>  (one section only: "<heading>")

## Required structure (for the new doc)
H1: <Title>
H2: Intent
H2: Procedure (numbered)
H2: Anti-patterns

## Files MAY touch
<allowlist>

## Files MUST NOT touch
- AGENTS.md (orchestrator batches)
- docs/exec-reports/README.md (TV guide -- orchestrator-owned)
- <other in-flight episode targets>

## Validation
grep -nP '[\u2018\u2019\u201C\u201D\u2013\u2014]' <new-files...>
make preflight   # only if code changed

## Commit
See .github/skills/commit.md. Use -c commit.gpgsign=false. Trailer required.

## Push
git push origin main  (rebase on non-fast-forward)

## On completion
UPDATE todos SET status = 'done' WHERE id = 's0Xe0Y-<slug>';
Return: commit SHA, deliverables, any gaps you noticed but did not fix.
```

## Anti-patterns

- **"Use your judgment on related cleanup."** Sub-agents will use it -- and touch the file your other in-flight agent owns. Be explicit.
- **Skipping the denylist with a `WHY`.** Without a reason, the next brief author drops the entry as redundant.
- **Implicit deliverables ("update docs as needed").** If the doc isn't named, it won't be written -- or worse, the wrong one will be.
- **Forgetting the on-completion SQL.** Then the writers' room shows the episode as still in flight and Larry double-dispatches.

## Escalation

If you find yourself writing the same paragraph into three consecutive briefs, that paragraph belongs in a skill. Promote it. Cite it. Stop re-typing it.
