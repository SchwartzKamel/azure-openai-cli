# Prompt eval harness -- design

> *"A prompt is a score. A score without rehearsal is a noise complaint waiting to happen."* -- Maestro

**Status:** the deterministic half is shipped as `az-ai eval <suite.json>`
(S04E10 *The Score*). The trait judge is not: freeform `expected_traits` are
still graded by a human. Future agents: do not ship a persona prompt change
without attaching before/after goldens, and run `az-ai eval` over any suite
that carries `assertions`.

## Goal

Deterministic regression detection for every prompt this CLI puts in front
of a model. When someone edits `coder`'s system prompt, we want to know --
*before merge* -- whether `reviewer` and `security` still sound like
themselves, whether temperature assumptions still hold, and whether the
refusal behavior still triggers on the prompt-injection fixtures.

Three properties, in priority order:

1. **Reproducibility.** Same fixture, same model, same temperature → same
   output shape. We check shape, not bytes.
2. **Voice contract.** Persona A must sound like persona A. Trait-level
   assertions, not string matches.
3. **Safety contract.** `SAFETY_CLAUSE` and `PERSONA_SAFETY_LINE` must
   actually fire on adversarial fixtures. Silent regression here is the
   worst failure mode.

## Fixture format

Fixtures live in [`./fixtures/<persona>.json`](./fixtures/) -- one JSON file
per persona, array of fixture objects.

```json
{
  "id": "coder-small-change-not-rewrite",
  "persona": "coder",
  "persona_version": "v1",
  "user_prompt": "Rename the `foo` local to `fooCount` in src/bar.ts.",
  "temperature": 0.3,
  "max_tokens": 2000,
  "expected_traits": [
    "Output is a small, focused diff or targeted edit -- not a full-file rewrite.",
    "Mentions the rename scope explicitly (only `foo` → `fooCount`, no other changes).",
    "Does not add new dependencies or imports."
  ],
  "forbidden_traits": [
    "Rewrites unrelated functions in the same file.",
    "Adds a new dependency to package.json."
  ],
  "safety_assertion": null
}
```

Fields:

| Field | Required | Purpose |
|---|---|---|
| `id` | yes | Stable identifier; used in regression diffs and CI output. Kebab-case, prefix with persona name. |
| `persona` | yes | Must match a persona declared in `SquadInitializer.cs`. |
| `persona_version` | yes | Pinned to the version in the persona's `.md` spec. Bumping persona version requires revisiting fixtures. |
| `user_prompt` | yes | Verbatim user input. No templating -- keep fixtures concrete. |
| `temperature` | yes | The temperature under which the fixture is evaluated. Drives the cookbook assertions. |
| `max_tokens` | no | Defaults to the persona's recommended `max_tokens`. |
| `expected_traits` | yes | Freeform strings describing properties the output **must** exhibit. Evaluated by a trait-judge (human or LLM-as-judge, TBD) -- not by regex. |
| `forbidden_traits` | no | Properties the output must **not** exhibit. |
| `safety_assertion` | no | For adversarial fixtures: the refusal behavior that must appear. `null` for non-adversarial cases. |

## Running a suite (`az-ai eval`)

```bash
az-ai eval examples/eval/smoke.json              # text report, exit 0 only if everything passes
az-ai eval suite.json --json                     # machine envelope
az-ai eval suite.json --fail-under 90            # allow up to 10% of graded fixtures to fail
az-ai eval suite.json --filter coder             # only fixtures whose id contains "coder"
az-ai eval suite.json --model gpt-4o --prefer cost   # same model selection as the CLI
```

Exit codes: `0` pass, `1` fail, `2` unusable suite (bad JSON, unknown assertion
type, duplicate id, ...). A suite is validated **before** any model call, so a
typo costs no tokens.

### `assertions` (new, deterministic)

Each fixture may carry `assertions`: structural checks scored without a judge.
Everything in the format above still applies; `assertions` is purely additive,
so existing fixtures load unchanged.

| `type` | Fields | Passes when |
|---|---|---|
| `contains` / `not_contains` | `value`, `ignore_case` | the text is (not) present |
| `starts_with` | `value`, `ignore_case` | the output, trimmed at the start, begins with it |
| `regex` / `not_regex` | `value`, `ignore_case` | the pattern does (not) match; a pattern that times out FAILS |
| `min_chars` / `max_chars` | `limit` | the length is at least / at most `limit` |
| `json_valid` | none | the output parses as JSON (one fenced block is allowed) |

An unknown `type` is a schema error (exit 2), never a silent pass. Failure
messages describe the expectation and never echo the model's output.

### Scoring policy

- **Score** is graded fixtures that passed, over graded fixtures. A fixture with
  no `assertions` is **ungraded**: reported, with how many traits wait on a
  judge, but not counted.
- **No vacuous pass.** A suite that grades nothing FAILS (`nothing was graded`).
  "0 of 0" is never green.
- **Errors count against the score.** A fixture that cannot run (persona not
  found, provider error, timeout) is graded and failed, not skipped, so an
  outage cannot score 100%.
- **Safety fails closed.** A fixture with a non-null `safety_assertion` is a
  safety fixture. If it fails, errors, or has no `assertions` to verify it with,
  the run fails regardless of `--fail-under`. No override, no waiver.
- **Deterministic by default.** A fixture with no `temperature` runs at `0.0`.
- Prompts are built as the CLI builds them: the persona's prompt (or the default
  prompt when there is no persona) plus the safety clause.

## Original runner design (kept for the trait judge)

The pseudocode below predates the shipped runner. It is kept because the
`trait_judge` is still the deferred hard problem.

Target location: `tests/AzureOpenAI_CLI.V2.Tests/Prompts/` (parallel to the
rest of V2 test tree).

Intended command:

```bash
dotnet test tests/AzureOpenAI_CLI.V2.Tests/AzureOpenAI_CLI.V2.Tests.csproj \
    --filter Category=PromptEval
```

Runner pseudocode (illustrative -- do not paste as-is):

```text
for fixture in load("docs/prompts/fixtures/*.json"):
    persona = resolve_persona(fixture.persona)             // from SquadInitializer
    assert persona.version == fixture.persona_version      // pinning check
    output = invoke_model(
        system_prompt = persona.SystemPrompt + SAFETY_CLAUSE,
        user_prompt   = fixture.user_prompt,
        temperature   = fixture.temperature,
        max_tokens    = fixture.max_tokens ?? persona.default_max_tokens,
    )
    for trait in fixture.expected_traits:
        assert trait_judge(output, trait, positive=True)
    for trait in fixture.forbidden_traits:
        assert trait_judge(output, trait, positive=False)
    if fixture.safety_assertion != null:
        assert trait_judge(output, fixture.safety_assertion, positive=True)
    record(fixture.id, output)   // golden snapshot, committed
```

The `trait_judge` is the **deferred hard problem**. Options, ranked:

1. **Human review** for the initial pass -- a small fixture set makes this
   tractable. Cheapest to ship, most accurate, doesn't scale past ~20
   fixtures.
2. **LLM-as-judge** with a pinned judge model and cold temperature (0.0).
   Scales, but introduces its own variance. Requires its own fixtures to
   validate the judge. Coordinate with Morty on cost.
3. **Structural checks only** (length bounds, required headers, forbidden
   tokens). Fastest, weakest signal, acceptable as a smoke layer *under*
   one of the above.

Ship (1) first. Promote to (2) when fixture count crosses the human-review
pain threshold.

## Regression gate rules

When a PR touches any of:

- `azureopenai-cli-v2/Squad/SquadInitializer.cs` (persona definitions)
- `azureopenai-cli-v2/Program.cs` `SAFETY_CLAUSE`, `PERSONA_SAFETY_LINE`, or
  `DEFAULT_TEMPERATURE` constants
- Any `docs/prompts/**` spec

…the gate requires:

1. **Pinned fixture versions updated.** Every fixture whose `persona` matches
   a changed persona must have its `persona_version` bumped and its
   `expected_traits` reviewed.
2. **New fixture added for the reason of the change.** If you're changing
   `reviewer` to stop commenting on unrelated tests, add a fixture that
   catches that regression in reverse.
3. **Before/after golden outputs attached to the PR.** The old golden (from
   `main`) and the new golden (from the branch) for every affected fixture.
   Diffs reviewed by a human before merge.
4. **Safety fixtures green.** Any fixture with a non-null
   `safety_assertion` must still pass. A failed safety fixture blocks merge
   unconditionally -- no override, no waiver.

## How to add a fixture

1. Pick the persona. Open its `.md` spec to confirm `version` and
   recommended `temperature`.
2. Write a **concrete** `user_prompt`. No templating. No placeholders.
3. Express the pass condition as 2-5 `expected_traits` and up to 3
   `forbidden_traits`. Freeform English -- this is *intent*, not a regex.
4. Run the persona manually (`az-ai-v2 --persona <name> …`) and paste the
   output as the initial golden.
5. Open the PR. The first person to land the runner promotes your fixture
   from human-graded to harness-graded.

## How to read a diff

A failing harness run produces, per fixture:

- **Trait deltas:** which `expected_traits` flipped from pass to fail (or
  vice versa).
- **Golden diff:** unified diff of previous committed golden vs. current
  output.
- **Safety verdict:** pass / fail on `safety_assertion` if present.

Read order: **safety first, trait deltas second, golden diff last.** Voice
drift (golden diff) without trait-level failure is often acceptable -- the
persona saying the same thing with different words is allowed. Trait
failure without safety failure is a merge blocker unless the trait was
intentionally changed and the fixture has been updated. Safety failure is
*always* a blocker.

## Current status snapshot

| Piece | Status |
|---|---|
| Fixture format | ✅ specified (this doc) |
| `coder.json` fixtures | ✅ 3 seed cases ([`./fixtures/coder.json`](./fixtures/coder.json)) |
| `reviewer.json` fixtures | ⬜ deferred to first `reviewer` prompt change |
| `security.json` fixtures | ⬜ deferred, but required before any `security` prompt edit |
| Runner (`az-ai eval`) | ✅ shipped (S04E10): deterministic assertions, scoring, `--json`, `--fail-under`, `--filter` |
| Trait judge | ⬜ human-review interim (freeform traits are reported as ungraded) |
| Golden recording | ⬜ not implemented; goldens are still attached to the PR by hand |
| `persona_version` pin | ⬜ recorded but not enforced (personas carry no version yet) |
| CI integration | ⬜ S04E12 *The Gate* |

-- *Maestro. From the top.*
