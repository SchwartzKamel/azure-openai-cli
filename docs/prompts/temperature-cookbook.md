# Temperature Cookbook

> *"Temperature is a decision, not a default."* — Maestro

A quick-reference table for choosing model `--temperature` by task category.
The CLI default is `0.55` (see `azureopenai-cli-v2/Program.cs:19`
`DEFAULT_TEMPERATURE`), a moderate value picked to work passably for general
Q&A. Many tasks benefit from a tuned value. If you are not thinking about
temperature, you are picking `0.55` by accident.

---

## The table

| Task category | Recommended temp | Examples | Rationale |
|---|---:|---|---|
| Validation / linting | **0.0 – 0.1** | Ralph validator, format/lint checks, test-fix loops | Deterministic; the answer does not vary meaningfully between runs. |
| Security audit | **0.1 – 0.2** | Newman persona, `security` Squad persona, threat-model review | Low variance; same input should produce the same findings. Hallucinated CVEs are a liability. |
| Code generation — tests | **0.2 – 0.3** | Puddy property-test scaffolding, unit-test generation | Mild variance OK; correctness dominates over style. |
| Code generation — implementation | **0.3 – 0.5** | Kramer persona, `coder` Squad persona, small refactors | Some idiom variety welcome; multiple reasonable implementations exist. |
| Code review | **0.2 – 0.4** | `reviewer` Squad persona, PR review passes | Findings should be stable run-to-run; prose can vary. |
| Architecture / design | **0.4 – 0.6** | Costanza persona, `architect` Squad persona, ADR drafts | Multiple valid designs; exploration helps surface alternatives. |
| Documentation | **0.5 – 0.7** | Elaine persona, `writer` Squad persona, prose audits | Voice and clarity benefit from variety within a consistent tone. |
| Creative / announce copy | **0.7 – 0.9** | J. Peterman persona, launch announcements, changelog flavor | Flavor is the point. |
| Brainstorming | **0.9 – 1.1** | Proposal ideation, demo scripts, name generation | Quantity of ideas outweighs quality of any single idea. |

---

## Defaults in this codebase

- **CLI default:** `0.55` — `azureopenai-cli-v2/Program.cs:19` (`DEFAULT_TEMPERATURE`). Applied when no `--temperature` flag is passed.
- **Agent mode:** inherits the CLI default. Pass `--temperature` to override per invocation. Tool-calling generally benefits from the cooler end (0.2–0.4) — colder prompts follow tool schemas more reliably.
- **Persona mode:** inherits the CLI default today. `PersonaConfig` has no `temperature` field yet; when it lands, each of the five defaults should declare one from the table above (see audit H3, M1).
- **Ralph validation loop:** currently inherits `0.55`. **This is wrong for a validator.** A validator re-running the same check at 0.55 introduces spurious drift between iterations. See follow-up todo `ralph-low-temp-default`.
- **Structured output (`--schema`):** use `0.0 – 0.2`. Schema violations at higher temperatures are a known failure mode across providers.

---

## When to deviate from the table

- If the same prompt + same inputs yields **inconsistent** results between runs → **lower** temp (step by 0.1).
- If output is **repetitive, bland, or too formulaic** → **raise** temp (step by 0.1).
- If the model is emitting **schema violations or malformed tool calls** → lower temp; cold models follow structure more reliably.
- For **model-in-the-loop workflows** (e.g. Squad coordination, delegate_task) → match the *overall task category*, not the loop primitive. A reviewer delegating to a coder should run the reviewer at 0.2–0.4 and the coder sub-call at 0.3–0.5.
- For **reasoning models** (o1, o3, gpt-5.x with reasoning): the temperature knob has reduced effect — the model's internal sampling dominates. Prefer the low end of each category and tune `max_tokens` instead.

---

## `max_tokens` — a parallel lever

Temperature controls *variability*. `max_tokens` controls *verbosity*. They
trade off: a low temperature with a high `max_tokens` still produces a long
answer, just a more deterministic one.

| Task | `max_tokens` |
|---|---:|
| One-line answers / commit messages | 256 |
| Short code snippets | 1,000 |
| Normal chat / review comments | 2,000 – 4,000 |
| Documentation drafts | 4,000 – 8,000 |
| Full agent loops | 10,000 (CLI default, `DEFAULT_MAX_TOKENS`) |
| Ralph mode with many iterations | 10,000 per iteration, cap total via `--max-iterations` |

---

## Rationale discipline

Every non-zero temperature in committed code or docs gets **one line of
rationale**. Examples:

```csharp
// temp 0.2: reviewer output must be stable across runs; PR diffs shouldn't
// shift because the model felt whimsical.
float reviewerTemp = 0.2f;
```

```bash
# Brainstorm demo ideas — breadth > depth.
az-ai-v2 --temperature 1.0 "give me 20 launch-demo ideas for a CLI agent"
```

No one-line rationale, no committed override. This is a convention, not a
linter rule — yet.

---

## Known failure modes

| Symptom | Likely cause | Fix |
|---|---|---|
| Validator keeps re-finding "new" issues on identical input | Temp too high (inherited 0.55) | Drop to 0.1 |
| Schema violations (JSON malformed) | Temp too high for structured output | Drop to 0.0–0.2 |
| Creative copy reads like a stereo manual | Temp too low (0.2) | Raise to 0.7+ |
| Agent emits the same tool call in a loop | Temp too low AND prompt too restrictive | Raise temp to 0.4 *and* widen the prompt |

---

## See also

- [`safety-clause.md`](./safety-clause.md) — refusal clause applied to agent/ralph/delegate prompts.
- `docs/cost-optimization.md` — temperature interacts with token cost via retry rates (Morty's audit).
- `docs/audits/docs-audit-2026-04-22-maestro.md` H3 — the finding that produced this cookbook.

— *Maestro. With an M.*
