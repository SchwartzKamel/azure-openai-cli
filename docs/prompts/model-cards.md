# Model card policy

> *"If we're going to tell a user this model is appropriate for their task,
> we owe them the card. Intended use. Failure modes. Known unknowns. All of
> it."* -- Maestro

**Status:** policy + template. The card inventory (one card per
default-eligible model) lands incrementally; see [roadmap](#roadmap).

## What a model card is, and is not

A **model card** is a short, structured document that captures what a model
is *for*, what it is *not for*, how it fails, and what we have and have not
measured. It is written from our integration's perspective -- we are not
re-publishing the upstream vendor's card, we are describing how this CLI
uses this model.

A model card is **not**:

- A benchmark report. Benchmarks live in `model-matrix.md`
  (once it lands) and `docs/benchmarks/`.
- A marketing page. We do not quote vendor hype.
- A FAQ. User-facing "which model should I pick?" belongs in
  `docs/cost-optimization.md` and the model matrix.

The card exists so that when we pin a default (or when a user asks "is this
model OK for security review?"), the answer is not a Slack thread.

## When to write a card

Write a model card when **any** of the following is true:

1. The model is (or is a candidate to be) a CLI default -- `DEFAULT_MODEL`,
   a Ralph-mode override, or a persona-declared default.
2. The model is referenced by an ADR as a routing target (Foundry, NIM,
   NVFP4).
3. The model was evaluated in a shipped A/B (per [`ab-testing.md`](./ab-testing.md))
   -- whether it won or lost.
4. The model is used for a *safety-sensitive* task in the shipped product
   (security persona, refusal behavior, schema-constrained output).

For every other model a user *could* configure -- skip it. We do not card
the long tail. Azure has a catalog; we do not duplicate it.

## Card location

- Per-model cards: `docs/prompts/cards/<model-slug>.md`
  - Slug is lowercase, hyphen-separated, vendor-qualified. Examples:
    `azure-gpt-4o-mini.md`, `azure-gpt-5.4-nano.md`, `foundry-phi-4-mini-instruct.md`.
- Cards are linked from [`README.md`](./README.md) (prompt library index)
  and from `model-matrix.md` (once landed).
- When a model is deprecated as a default, the card moves to
  `docs/prompts/cards/archive/` with a deprecation note -- do not delete it.

## Required sections

Every card has these sections, in this order. Missing sections fail the
card review.

1. **Header** -- model name, vendor, deployment slug, date written, author.
2. **Intended use in this CLI** -- the tasks we route to this model today.
3. **Not intended for** -- explicit negative space. Where we route elsewhere.
4. **Failure modes** -- observed, not theoretical. One row per mode.
5. **Fairness & representation notes** -- what we have and have not tested.
6. **Known unknowns** -- what we *do not know* and would need to run to find
   out. Honesty here is load-bearing.
7. **Evaluation evidence** -- links to benchmarks, A/Bs, ADRs that justify
   shipping this as a default (or rejecting it).
8. **Change log** -- one line per material change (new deployment, default
   swap, deprecation).

## Template

A copy-pasteable skeleton -- start here for every new card.

```markdown
# Model card -- <vendor>/<model> (<deployment-slug>)

> *One-sentence positioning: what we use this model for, in one breath.*

**Vendor:** Azure OpenAI | Foundry | NIM | Other
**Deployment:** `<deployment-name>` (the string passed to `--deployment`)
**API version:** `<api-version>`
**Card written:** YYYY-MM-DD
**Author:** <persona or contributor>
**Status:** default | candidate | routed-to-by-ADR-NNN | deprecated

## Intended use in this CLI

- <Task 1 -- e.g. Espanso text-fix via `--persona writer`>
- <Task 2 -- e.g. Ralph validator at temp 0.1>
- <Task 3>

Cite the code path that routes here: `<file>:<line>`.

## Not intended for

- <Task A -- with one-line reason and the model we route there instead>
- <Task B>
- <Task C>

## Failure modes

Observed in our harness or in production. Do not list modes you have not
observed -- that is the "known unknowns" section.

| Mode | Symptom | Mitigation |
|---|---|---|
| <e.g. schema drift at temp > 0.3> | <what it looks like> | <what we do> |
| <e.g. truncation on long tool outputs> | <symptom> | <mitigation> |

## Fairness & representation notes

We do not run bias benchmarks in-house. This section captures:

- **Known vendor-published evaluations:** <link or "none we rely on">
- **Our observed issues (if any):** <e.g. "persona voice degrades on
  non-English inputs per Babu's i18n audit, 2026-MM-DD">
- **Scope of testing:** <languages, domains, persona set we exercise>
- **What we have not tested:** be explicit.

Ethical review owned by Rabbi Kirschbaum; this section captures what we
know, not what we wish were true.

## Known unknowns

The honest list. Each item is something a reviewer could run to answer.

- <e.g. "Tool-call accuracy on nested schemas -- never measured">
- <e.g. "Refusal stability across 10 runs at temp 0 -- measured once">
- <e.g. "Latency P99 under sustained Ralph loop -- unknown">

## Evaluation evidence

- Benchmarks: `docs/benchmarks/<file>.md`
- A/Bs: <PR links>
- ADRs: `docs/adr/ADR-NNN-*.md`
- Temperature cookbook band: <from `temperature-cookbook.md`>

## Change log

### v1 -- YYYY-MM-DD -- <author>

- Initial card. Status: <default | candidate | etc>.
- Evidence: <short list>.
```

## Review requirements

A PR that lands or materially updates a card needs:

- Sign-off from Maestro (prompt library owner).
- Sign-off from Morty when the card's status is `default` (cost impact).
- Sign-off from Newman when the card covers a safety-sensitive persona.
- Sign-off from Rabbi Kirschbaum when the fairness section changes
  substantively (not for typo fixes).

## Anti-patterns

- **Vendor-quoting.** Do not paste the upstream card. Link it, summarize
  what we rely on, own the rest.
- **Aspirational failure modes.** "May hallucinate under load" -- if you
  have not observed it, it goes in known unknowns, not failure modes.
- **Stale benchmarks.** A card citing a benchmark older than the deployment
  API version is worse than no citation. Re-run or remove.
- **Silent default swaps.** A card's `Status: default` changes *in the same
  PR* as the code change that makes it the default. No stealth promotions.

## Roadmap

- [ ] Write card for `gpt-4o-mini` (current `DEFAULT_MODEL`) -- blocking
      `model-matrix.md` M4.
- [ ] Write card for `gpt-5.4-nano` (Foundry candidate per ADR-005).
- [ ] Write card for `Phi-4-mini-instruct` (NIM routing per ADR-006).
- [ ] Wire `maestro-preflight` to verify every model referenced in code has
      a card (or is explicitly marked "long-tail, no card").

-- *Maestro*
