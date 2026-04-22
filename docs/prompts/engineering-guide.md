# Prompt engineering guide

> *"A prompt is not a pep talk. It is a score. If you find yourself writing
> 'please' three times, you are not prompting -- you are negotiating. Rewrite
> it."* -- Maestro

**Audience:** contributors editing any prompt that ships in this CLI --
persona `SystemPrompt` strings in `SquadInitializer.cs`, `DEFAULT_SYSTEM_PROMPT`
and `SAFETY_CLAUSE` in `Program.cs`, the Ralph overlay in
`RalphWorkflow.cs`, and any future tool `[Description]` copy.

**Scope:** principles, anti-patterns, a review checklist. This doc does not
tell you *which* prompt to write (that is persona spec authorship). It
tells you how to write one that holds up under evaluation.

## Principles

### 1. Structure over exhortation

A prompt telling the model *what shape* of answer to produce outperforms a
prompt telling the model *how hard to try*. "Respond with a bulleted list
of findings, one per line, prefixed by a severity tag" beats "please be
thorough and detailed." The former is a contract; the latter is a wish.

**Good:**

```text
Output exactly these sections, in order:
1. Summary (one paragraph, 2-4 sentences)
2. Findings (bulleted; each line starts with [Critical|High|Medium|Low])
3. Recommended next step (one sentence)
```

**Bad:**

```text
Please provide a comprehensive and detailed analysis covering all
important aspects of the code, being sure to flag anything serious.
```

The second prompt contains no testable assertion. The first one does.

### 2. Schema over freeform

If the output feeds downstream code (parsing, routing, tool arguments),
use a schema. Structured outputs (`--schema`) at low temperature beat
regex-scraping freeform prose every time. When a schema is impractical,
specify the exact delimiters and section headers the parser will look for.

Corollary: **if the output is user-facing prose, do not over-schema it.**
An Elaine-persona doc review chopped into JSON keys reads like a form. Use
schema where machines read; use prose where humans read.

### 3. Few-shot only when warranted

Few-shot examples cost tokens and lock the model into surface-level
mimicry. Use them when:

- The task has a non-obvious output format the model will not guess.
- The desired *voice* is hard to describe but easy to demonstrate (persona
  voice anchors, for example).
- The failure mode you have observed is "the model returned something
  plausible but shaped wrong."

Do **not** use few-shot when:

- Zero-shot already meets the bar in harness evals. Extra examples are
  tokens you pay for and a ceiling you impose on the model.
- The examples are near-duplicates (paste the same example twice is not
  few-shot, it is filler).
- The examples are aspirational. If your "good" example is something you
  made up rather than something a model actually produced, it may be out of
  distribution.

**Count discipline:** zero, one, or three examples. Two reads as
"incomplete list," more than three is diminishing returns except for voice
contracts where five to seven is sometimes warranted.

### 4. Name the audience and the constraints

The model performs best when it knows who it is talking to and what rails
it is on. Three short lines beat three paragraphs of flourish:

```text
You are <role>. The reader is <audience with expertise level>.
You may <capability list>. You must not <hard constraints>.
Prefer <tiebreaker heuristic>.
```

The `coder` persona ("expert software engineer", "follow existing project
conventions", "prefer small focused changes") is a good example. The
audit's L1 complaint about `writer` ("avoid details that rot quickly") is
a bad example -- vague, not testable, model interprets it inconsistently.

### 5. One prompt, one job

If a system prompt is trying to be a generalist *and* a tool-caller *and*
a stylist, pick one. Compose via mode (agent vs standard), persona, and
temperature, not by stuffing the system prompt. `DEFAULT_SYSTEM_PROMPT`
("secure, concise CLI assistant") is 15 words and does one thing. That is
intentional.

### 6. Safety belongs in the prompt *and* downstream

The `SAFETY_CLAUSE` is appended at the invocation layer. Persona prompts
still bake in `PERSONA_SAFETY_LINE`. This is defense-in-depth, not
redundancy -- a future code path that forgets the downstream concat still
has the persona-level refusal. Write safety text as if the downstream
layer might not exist.

### 7. Temperature is part of the prompt

The same words at 0.2 and 0.8 are two different prompts. A prompt author
who does not declare a temperature is shipping an unfinished score. See
the [temperature cookbook](./temperature-cookbook.md) -- pick a band, cite
it in the persona spec, test at the declared value.

### 8. Write for the harness you will have to pass

If you cannot imagine a fixture that distinguishes "your new prompt
works" from "your new prompt silently regressed," you have not finished
writing the prompt. Work backward from the test: *what would change if the
prompt were broken?* That observable change is what your fixture asserts.

## Common anti-patterns

### The pep-talk

```text
You are an expert! Be thorough! Think step by step! Don't rush! Be
careful to consider all the edge cases! Take your time!
```

Wall-of-exclamation adjectives. Specifies nothing. Tests nothing. Deletes
cleanly with zero measurable quality loss.

### The apology prefix

```text
I'm an AI assistant, and while I'll do my best to help, please note...
```

Never in a system prompt. If the model needs to hedge, let the model hedge
in its output. System prompts are directives, not disclaimers.

### The runaway list

```text
1. Be accurate. 2. Be concise. 3. Be helpful. 4. Be honest. 5. Be kind.
6. Be thorough. 7. Be brief. 8. Be careful.
```

Contradictions inside a single list ("concise" and "thorough") force the
model to pick, silently, run by run. Pick one and commit. If two properties
genuinely both matter, say which breaks the tie: *"Prefer brevity; when
brevity would omit a critical warning, include it."*

### The leaky persona

```text
You are Jerry Seinfeld. Also be factual. Also be security-conscious. Also
use tools correctly. Also...
```

Every "also" dilutes the character. If you need a Jerry-voiced security
audit, write a specialized persona that fuses voice and remit; do not
append security lines to a comedy persona.

### The aspirational schema

```text
Respond in JSON.
```

Without specifying keys, types, and whether extra keys are allowed, this
produces plausible-looking garbage that breaks downstream parsing on the
first model update. Use a real schema (`--schema` flag) or specify fields
by name and type in the prompt.

### The "just one word" edit

Any edit framed as "just one word" or "just a clarification" without a
fixture, an A/B, or a golden refresh. See
[`change-management.md`](./change-management.md). Persona text has no such
thing as a trivial edit.

### The ignored cookbook

Shipping a reviewer persona at temperature 0.8 "to make it more engaging."
The cookbook bands exist because we measured them. Deviate with a
one-line rationale committed alongside the change.

## Review checklist

Paste this into the PR description (or have `maestro-preflight` do it for
you, once the hook lands) for any change to a prompt string:

```markdown
## Prompt review checklist

- [ ] Prompt change is reflected in the matching `docs/prompts/**/<name>.md` spec.
- [ ] Persona version bumped if `SystemPrompt` text changed (per change-management.md).
- [ ] Fixtures updated or a new fixture added that would fail on the old prompt.
- [ ] Temperature and max_tokens declared and within the cookbook band.
- [ ] Safety line present where persona prompt is shipped standalone.
- [ ] No contradictions in the instruction list (e.g. "concise" + "thorough" without tiebreaker).
- [ ] Structure specified where the output is parsed; schema used where practical.
- [ ] Few-shot examples, if any, are justified (and counted 0, 1, or 3; or 5-7 for voice).
- [ ] No exhortation-only lines ("be careful", "try hard", "do your best").
- [ ] A/B methodology followed if the change is voice-motivated or safety-adjacent
      (per `docs/prompts/ab-testing.md`).
- [ ] Change log entry added to the persona spec under `## Change log`.
```

If you ticked every box and the harness (when it exists) is green -- ship.
If you ticked every box and the harness is red -- the harness wins.

## Further reading

- [`change-management.md`](./change-management.md) -- the contract every
  persona prompt change must satisfy.
- [`eval-harness.md`](./eval-harness.md) -- fixture format and regression
  gates.
- [`ab-testing.md`](./ab-testing.md) -- how to compare two versions of a
  persona.
- [`temperature-cookbook.md`](./temperature-cookbook.md) -- per-task
  recommended temperatures.
- [`safety-clause.md`](./safety-clause.md) -- the clause, the layers, the
  override behavior.
- [`personas/_template.md`](./personas/_template.md) -- the copy-paste
  skeleton for a new persona spec.

-- *Maestro*
