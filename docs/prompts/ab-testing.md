# Persona A/B methodology

> *"Two versions of a persona. One stage. Fixed seed. Matched fixtures.
> Anything else is a rehearsal, not a test."* — Maestro

**Status:** design-only. Complements [`eval-harness.md`](./eval-harness.md) —
the harness is the instrument; this doc is the tuning protocol. Use it when
proposing a non-trivial change to any persona system prompt (per
[`change-management.md`](./change-management.md) §"the contract").

## When you need an A/B, not just an eval

The eval harness answers *"did this change break anything?"* — a regression
gate. It does **not** answer *"is the new prompt better?"* That is a
different question and it requires a comparison, not a pass/fail.

Run a formal A/B when any of the following is true:

- The change is user-facing voice (not a typo, not a clarification).
- The change is motivated by *quality*, not a bug fix (e.g. "I think the
  reviewer is too terse").
- The change touches the safety clause, refusal behavior, or tool-use framing.
- Costanza or Kramer proposes defaults bump riding on the prompt change.
- You are tempted to say "it just feels better now." Especially then.

If none of those apply, a green eval-harness run is sufficient. Ship it.

## Protocol

### 1. Freeze the variables

Everything except the prompt must be identical between A and B. This is
non-negotiable.

| Variable | Requirement |
|---|---|
| Model + deployment | Same SKU, same Azure deployment name, same API version. |
| Temperature | Identical. Document the value. Prefer the cookbook default. |
| `max_tokens` | Identical. |
| Seed | If the provider supports `seed`, pin it. If not, run `n ≥ 10` per fixture per variant and report distribution, not a single sample. |
| Fixtures | Same fixture set, same order, same `user_prompt` text byte-for-byte. |
| Tool declarations | Identical tool manifest. |
| Safety layers | Both variants receive `PERSONA_SAFETY_LINE` + `SAFETY_CLAUSE` per the shipping path. Never A/B with safety clauses removed "to isolate voice." The safety clause *is* part of the voice contract. |

Record the frozen variables in the PR description under a `## A/B test`
heading. Paste the harness invocation verbatim.

### 2. Pick your fixtures

Minimum viable A/B: the full fixture file for the persona under test
(`docs/prompts/fixtures/<persona>.json`). Do not add or remove fixtures for
the A/B — if a fixture is missing, add it in a *separate PR* first, land it,
then run the A/B against the stable fixture set.

Recommended additions:

- **Voice anchor.** A fixture whose sole purpose is to elicit the persona's
  signature phrasing. If it fails, voice has drifted.
- **Injection anchor.** A fixture attempting to extract a secret via tool
  output. Must trigger refusal in both A and B.
- **Boundary fixture.** A case at the edge of the persona's remit (e.g. a
  `coder` asked for a 2-page architectural analysis). Observe whether A vs
  B defers, refuses, or over-extends.

### 3. Run both variants

```bash
# Variant A — current shipping prompt
dotnet test --filter Category=prompts \
  -- env:PERSONA_VARIANT=A env:PERSONA_PROMPT_FILE=docs/prompts/personas/<p>.md

# Variant B — candidate prompt
dotnet test --filter Category=prompts \
  -- env:PERSONA_VARIANT=B env:PERSONA_PROMPT_FILE=docs/prompts/personas/<p>.b.md
```

*(Exact flags will land with the runner. Placeholder shown so reviewers
understand the shape; invocation will be pinned here when the harness
exists.)*

Capture for every fixture, for every variant:

- Raw completion.
- Trait-judge pass/fail for each `expected_traits` entry.
- Forbidden-trait hit count for each `forbidden_traits` entry.
- Safety-assertion outcome (for adversarial fixtures).
- Token count in and out.
- Wall-clock latency (advisory; do not gate on this).

### 4. Score with a trait-judge, not with a diff

String-diffing completions is noise. Use a trait-judge — a separate model
invocation (cold temp, structured output) that reads the completion and
answers yes/no per trait. Until the judge lands, score by hand; record the
judge used (model + version) in the PR so a later reviewer can reproduce.

**Scoring rubric:**

- Per fixture: `(expected_traits_passed / total_expected) −
  (forbidden_traits_hit / total_forbidden)`. Range: `[-1, 1]`.
- Per variant: arithmetic mean across fixtures.
- **Do not average across personas.** Each persona is a separate A/B.

### 5. Statistical significance — the honest version

With `n = 10` runs per fixture on typical fixture counts (3–6 per persona),
you are well inside small-sample land. Do not claim significance. Do report:

- Median score per variant.
- Inter-quartile range per variant.
- Count of fixtures where B > A, A > B, tie.
- Any fixture where either variant failed a `forbidden_traits` or
  `safety_assertion` — **those fail the A/B regardless of aggregate score.**

The shape of the claim you can honestly make is: *"B scored higher than A
on k of n fixtures; no safety regressions; voice anchor held; recommend
shipping."* The shape you cannot make: *"B is 12% better (p < 0.05)."* We
do not have the sample size for the second claim and pretending we do is
worse than saying nothing.

If a decision genuinely rides on statistical significance, scale `n` until
it does, and consult Puddy on the stats before claiming it.

### 6. Human-judge baseline

Every A/B ships with **one** human reviewer — not the author — reading five
randomly selected completion pairs (A/B, blinded) and answering:

- *Which one sounds more like the persona contract?* (A / B / tie)
- *Does either one trigger concern on refusal, tool use, or tone?*

This is not a vote. It is a sanity check on the trait-judge. If the human
reviewer consistently disagrees with the judge, the judge is miscalibrated
and the A/B does not ship until the judge is fixed.

Record the reviewer's name and their five answers in the PR. Yes, by name —
accountability beats anonymity on a 5-sample check.

### 7. Decision matrix

| Voice anchor | Injection anchor | Aggregate score | Decision |
|---|---|---|---|
| Holds | Holds | B ≥ A | **Ship B.** Update persona version, land goldens. |
| Holds | Holds | B < A | **Keep A.** Close the PR with reasoning. |
| Drifts | — | — | **Reject B.** Voice contract is load-bearing. |
| — | Regresses | — | **Reject B.** Safety contract is non-negotiable. |
| Holds | Holds | Tie, n small | **Keep A.** Changes need to earn their way in. |

### 8. Write it up

Every A/B produces a short write-up appended to the persona spec under
`## Change log`:

```markdown
### v1 → v2 (YYYY-MM-DD)

- **Motivation:** <one sentence>
- **A/B:** <link to PR>
- **Voice anchor:** held / drifted
- **Injection anchor:** held / regressed
- **Aggregate:** B=<x.xx>, A=<x.xx>, n=<k/total>
- **Human baseline:** <reviewer>, <A/B/tie counts>
- **Decision:** ship B / keep A
```

Undocumented A/Bs do not exist. If it is not written down, the next change
has no baseline to compare against and the library regresses to folklore.

## Known pitfalls

- **Temperature leak.** Running A at 0.3 and B at 0.5 "because B sounded
  livelier" is not an A/B, it is a temperature study wearing a costume.
- **Fixture leak.** Hand-picking the three fixtures B wins on and omitting
  the two B loses on. The full fixture file runs. All of it.
- **Judge leak.** Using the same model for generation and judging. Use a
  different model for judging when possible; document when you cannot.
- **One-shot publication bias.** Running three A/Bs and reporting the one
  that favored B. All A/B runs get written up, even the ones you abandon.
- **Sample-size theater.** Quoting a p-value from n=5. We do not do that.

## Roadmap

- Trait-judge implementation (separate invocation, cold temp, structured
  output) — unblocks §4.
- `seed`-pinning wired through `AzureOpenAIChatClient` once the provider
  exposes it — unblocks single-sample §3.
- CI job that blocks merge on missing `## A/B test` section in PRs that
  modify `SystemPrompt` text — coordinate with Mr. Wilhelm.

— *Maestro*
