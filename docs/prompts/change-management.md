# Persona prompt change management

> *"'Trust me, I only tweaked one word' is the sentence that precedes every
> regression I have ever seen in this library. Again -- from the top, with
> documentation."* -- Maestro

## The trust-me-bro problem

Persona system prompts are **production text**. They ship to the model on
every invocation. A seemingly trivial edit -- swapping "always" for "often",
reordering two clauses, dropping a sentence that "felt redundant" -- can
silently change:

- **Voice.** Users rely on persona X sounding like persona X.
- **Refusal behavior.** Removing a clause, even by accident, can open a
  prompt-injection hole.
- **Temperature assumptions.** Prompts written for `0.2` degrade at `0.7`,
  and vice versa.
- **Tool-call fidelity.** A loosened prompt makes the model hallucinate
  tool schemas.

"I only changed one word" is not a review. It is an assertion without
evidence. This document exists so that assertion gets evidence attached.

## The contract

Any PR that modifies a persona system prompt -- defined as any change to the
`SystemPrompt` field of a persona in
`azureopenai-cli-v2/Squad/SquadInitializer.cs`, or any change to
`SAFETY_CLAUSE` / `PERSONA_SAFETY_LINE` in `azureopenai-cli-v2/Program.cs`
-- **must** do all four of the following:

### 1. Version-bump the persona

Open the persona's `.md` spec in `docs/prompts/personas/<name>.md`. Bump
the `Version:` header (`v1` → `v2`, etc.). The version is git-tracked; the
bump gives reviewers a single line in the diff that says "this prompt is
not the same prompt anymore."

Minor grammatical fixes that demonstrably don't change semantics (typo, a
missing article) may keep the version but must still pass items 2-4.

### 2. Update the fixture file

Open `docs/prompts/fixtures/<persona>.json`. Do **one** of:

- **Add a new fixture** that encodes the reason for the change. If you're
  tightening `reviewer` to stop commenting on unrelated tests, add a
  fixture whose `expected_traits` explicitly check for that behavior.
- **Update `expected_traits` / `forbidden_traits`** on an existing fixture
  if the change modifies what an existing scenario should produce.
- **Update `persona_version`** on all fixtures for that persona to match
  the new version in (1). Fixtures pinned to an old version are stale
  and the harness will flag them.

A persona prompt change with *no fixture change* means you are either
asserting the change is invisible (in which case -- why make it?) or you
are skipping the test (in which case -- no).

### 3. Pass the eval harness

Once the harness exists (see [`./eval-harness.md`](./eval-harness.md)):

```bash
dotnet test tests/AzureOpenAI_CLI.V2.Tests/AzureOpenAI_CLI.V2.Tests.csproj \
    --filter Category=PromptEval
```

Must be green. A failed safety fixture is **unconditionally blocking** --
no waivers, no "it's a flake," no "we'll fix it after merge."

Until the harness lands, this step is satisfied by item 4 (before/after
goldens reviewed by a human).

### 4. Attach before/after golden outputs to the PR

For every fixture whose `persona` matches the changed persona, attach:

- **Before:** the output produced by running the fixture against the
  persona **as it exists on `main`**.
- **After:** the output produced by running the fixture against the
  persona **as it exists on this branch**.

Both runs at the fixture's declared `temperature` and `max_tokens`, against
the same model. Paste the two outputs into the PR description or attach as
files. A reviewer compares them.

This is the single most important step. Voice drift is legal; trait drift
is suspicious; safety drift is a blocker.

## The review checklist

A reviewer of a persona prompt change must tick all of:

- [ ] Persona `Version:` in `docs/prompts/personas/<name>.md` is bumped.
- [ ] Fixture file `docs/prompts/fixtures/<name>.json` is updated.
- [ ] `persona_version` in fixtures matches the new spec version.
- [ ] Before/after goldens are present and I have read them.
- [ ] No `safety_assertion` fixture regressed.
- [ ] Temperature in fixtures still matches the cookbook recommendation --
      or the cookbook recommendation has been updated in the same PR with
      a one-line rationale.

Missing any box → request changes.

## Special cases

- **Removing a persona.** Delete the persona, delete its spec, delete its
  fixture. Add an entry to `docs/prompts/README.md` noting the removal and
  the commit/date. Changelog-worthy.
- **Adding a persona.** Ship the spec, the fixture file (≥3 fixtures,
  including at least one `safety_assertion`), and the `PersonaConfig`
  change in the same PR. No one-step-at-a-time -- the spec without the
  fixtures is half a contract.
- **Changing `SAFETY_CLAUSE` or `PERSONA_SAFETY_LINE`.** All personas are
  affected. Every persona's fixtures must re-run. Requires Newman-level
  review. No docs-only edits that change wording.
- **Changing `DEFAULT_TEMPERATURE`.** Not a prompt change per se, but
  affects every inherited-default fixture. Update
  [`./temperature-cookbook.md`](./temperature-cookbook.md) in the same PR.

## Why this exists

Because without it, persona prompts drift. Silent drift. The kind where
three weeks later someone says "the reviewer used to catch this" and
no-one knows what changed or when. A prompt library with no change-
management discipline is a diary, not a spec.

No eval, no merge. No goldens, no eval. No fixture update, no goldens. No
version bump, no fixture update. The chain is the point.

-- *Maestro. With an M.*
