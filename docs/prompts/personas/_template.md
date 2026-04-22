# `<name>` persona -- prompt spec

<!--
  Template: copy this file to `docs/prompts/personas/<name>.md` and fill in
  every `<placeholder>`. This template IS the contract -- a persona spec
  missing any of these sections does not ship. See the three reference
  implementations: `coder.md`, `reviewer.md`, `security.md`.

  Do not edit this template as part of adding a persona. Edit your copy.
  The template itself is version-controlled as the canonical skeleton.
-->

> *"<One-line epigraph -- ideally in-character for the persona, or a line
> from the Maestro that frames the persona's stake in the ensemble.>"* --
> <Attribution>

**Version:** v1 (initial cut -- bumps required per [`../change-management.md`](../change-management.md))
**Source:** `<file>:<line-range>` (the `SystemPrompt` string location)
**Fixture:** `../fixtures/<name>.json`

## Intent

<One to three sentences. Name the *kind* of work this persona is optimized
for, in contrast to other personas. Call out what this persona is *not*
for -- explicit negative space is load-bearing.>

## System prompt (v1)

```
<Paste the exact string literal as it appears in source, including
whitespace and trailing `PERSONA_SAFETY_LINE` placeholder.>
<PERSONA_SAFETY_LINE>
```

`PERSONA_SAFETY_LINE` is the per-persona refusal baked in at
`SquadInitializer.cs` -- see [`../safety-clause.md`](../safety-clause.md) for
the defense-in-depth rationale.

## Inputs

- **User prompt:** <what kinds of prompts this persona is designed to receive>.
- **Tools declared:** <list: `shell`, `file`, `web`, `datetime`, `clipboard`, or "none">.
- **Agent mode:** <explicit / implicit via tool declaration / never>.
- **Expected fixtures attached:** `../fixtures/<name>.json`.

## Expected output shape

- <Shape item 1 -- e.g. "Output begins with a one-line summary.">
- <Shape item 2 -- e.g. "Findings grouped under Critical/High/Medium/Low.">
- <Shape item 3 -- e.g. "Code blocks use fenced Markdown with language tag.">
- <Shape item 4 -- e.g. "No sweeping recommendations unsolicited.">

Each shape item should be *testable*. If a reviewer cannot write a trait
judge for it, rewrite it until they can.

## Temperature

**Recommended: `<x.y>`** (<band name from the
[cookbook](../temperature-cookbook.md)>).

Rationale: <one to two sentences -- why this band, why not colder, why not
warmer>.

<Describe the wiring: does the persona inherit the global default today,
or does `PersonaConfig.Temperature` declare it? Cite the code location if
the declaration exists.>

## Safety clause

- `PERSONA_SAFETY_LINE` baked into prompt: **<yes | no>**.
- `SAFETY_CLAUSE` appended at invocation: **<yes -- always | yes -- agent/Ralph only | no>**.
- Defense in depth: <both layers present / single layer, justify>.

## Known failure modes

Observed in fixtures, harness, or dogfooding. Aspirational modes go in
"Known unknowns" below, not here.

| Symptom | Likely cause | Fix |
|---|---|---|
| <Observed symptom 1> | <Root cause> | <Mitigation -- prompt edit, fixture, temp change> |
| <Observed symptom 2> | <Root cause> | <Mitigation> |
| <Observed symptom 3> | <Root cause> | <Mitigation> |

## Known unknowns

<Optional but encouraged. The honest list -- things we would need to run
the harness (or an A/B) to answer. Mirrors the model-card convention.>

- <e.g. "Behavior on non-English inputs -- never exercised in fixtures.">
- <e.g. "Tool-call accuracy when `shell` and `file` are both declared --
  only tested with `file` alone.">

## Change-management rule

Any edit to the `<name>` `SystemPrompt` in `<file>` requires:

1. Bump the version header above (`v1` → `v2`).
2. Update `../fixtures/<name>.json` -- either a
   new fixture covering the reason for the change, or an updated
   `expected_traits` row on an existing fixture.
3. Attach before/after golden outputs to the PR.
4. If the change is voice-motivated or safety-adjacent, follow the
   [A/B methodology](../ab-testing.md).
5. Pass the eval harness once it exists (see [`../eval-harness.md`](../eval-harness.md)).
6. Add a `## Change log` entry below.

No eval, no merge. See [`../change-management.md`](../change-management.md).

## Change log

<!-- One entry per version bump. Oldest at top or bottom -- pick one and
keep it consistent across personas. Current shipping convention: oldest
at top (monotonically appending). -->

### v1 (YYYY-MM-DD)

- Initial spec. Source: `<file>:<line-range>`. No prior version.

---

-- *<Persona name or Maestro>*
