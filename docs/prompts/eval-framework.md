# Eval Framework -- Design Sketch

> *"Before you tune a prompt, you have to be able to score it. Today we
> cannot. Tomorrow we should."* -- Maestro

This is a **design**, not an implementation. The harness described here is
not built. The point of this document is to lock the seam so a future
episode can build it without re-litigating the shape.

A deeper, ship-ready spec for the prompt-eval harness lives in
[`eval-harness.md`](./eval-harness.md) -- that doc covers fixture format,
runner shape, and regression gates. This document is the *lighter* design
sketch the orchestrator asked for in S02E18: the minimum viable framework
for measuring whether a prompt change is good or bad before merging it.

---

## What we would evaluate

Three axes, in priority order:

1. **Prompt accuracy on a small held-out set.** Did the model produce an
   answer that satisfies the case's `expected_traits` (and avoids the
   `forbidden_traits`)? Pass / fail per case; aggregate as a percentage.
   This is the headline metric.
2. **Token economy.** Total input + output tokens per case. A "better"
   prompt is one that gets the right answer in fewer tokens. Reported
   as median + p95 across cases.
3. **Latency.** Wall-clock time per case, observed end-to-end at the
   client. Reported as median + p95. Useful as a tripwire -- a 5x
   latency jump on the same prompt usually means the model is looping
   or producing far more output than intended.

We are deliberately not evaluating: response *fluency*, *style*,
*creativity*, or anything that would require an LLM-as-judge. Those
are real but expensive, and the trait-judge approach in
[`eval-harness.md`](./eval-harness.md) covers the next tier when we
need it.

---

## Inputs format

A small JSON case file per prompt under `tests/eval-cases/<prompt-id>.json`.
The `<prompt-id>` matches the IDs in [`library.md`](./library.md).

```json
{
  "prompt_id": "squad-security",
  "model": "gpt-5",
  "temperature": 0.2,
  "max_tokens": 2000,
  "cases": [
    {
      "id": "sec-001-sql-injection-obvious",
      "input": "Review this code for security issues: ...",
      "expected_traits": [
        "mentions SQL injection",
        "cites line number",
        "classifies as Critical or High",
        "suggests parameterised query as remediation"
      ],
      "forbidden_traits": [
        "comments on code style",
        "fabricates a CVE identifier"
      ],
      "max_latency_ms": 30000,
      "max_total_tokens": 3000
    }
  ]
}
```

Field notes:

- `prompt_id` is the join key against the library inventory.
- `model`, `temperature`, `max_tokens` pin the run -- a prompt eval
  without these pins is meaningless.
- `expected_traits` and `forbidden_traits` are short prose strings.
  The harness checks each one against the model output; the *how* of
  that check is left open in this sketch (substring, regex, or a
  trait-judge call -- see eval-harness.md for the deeper take).
- `max_latency_ms` and `max_total_tokens` are budgets, not hard caps.
  Exceeding them does not invalidate the case; it shows up as a
  budget regression in the scorecard.

---

## Outputs format

A per-prompt scorecard, one JSON file per run under
`tests/eval-results/<prompt-id>-<timestamp>.json`:

```json
{
  "prompt_id": "squad-security",
  "model": "gpt-5",
  "temperature": 0.2,
  "run_at": "2026-04-22T14:30:00Z",
  "git_sha": "abc1234",
  "cases_total": 12,
  "cases_passed": 11,
  "pass_rate": 0.917,
  "tokens": { "median": 1850, "p95": 2740, "total": 23400 },
  "latency_ms": { "median": 4200, "p95": 9100 },
  "budget_violations": [
    { "case_id": "sec-007-deep-recursion", "axis": "latency_ms", "limit": 30000, "actual": 41200 }
  ],
  "failures": [
    {
      "case_id": "sec-004-cors-misconfig",
      "missing_traits": ["classifies as Critical or High"],
      "violated_traits": []
    }
  ]
}
```

A console summary -- one line per prompt -- is the human-readable
companion to the JSON:

```text
squad-security    pass 11/12 (91.7%)  tokens p50=1850 p95=2740  latency p50=4.2s p95=9.1s  [1 budget violation]
```

---

## How a contributor adds a new case

1. Pick the prompt ID from [`library.md`](./library.md) (or add a new
   one to the library if the prompt did not exist before).
2. Open `tests/eval-cases/<prompt-id>.json` (create if absent).
3. Append a case object to the `cases` array. Give it a descriptive
   `id` -- `<prompt-shortname>-NNN-<slug>` is the convention.
4. Write 2-5 `expected_traits`. Each trait is one short prose
   assertion. If you find yourself writing more than five, the case
   is doing too much -- split it.
5. Write 0-3 `forbidden_traits`. Use these for known failure modes
   you specifically want to gate against (e.g. "fabricates a CVE
   identifier" for the security persona).
6. Set `max_latency_ms` and `max_total_tokens` to roughly 1.5x what
   you observed during a manual sanity run. Tight enough to catch
   regressions, loose enough to not flake.
7. Run the harness locally (when it exists) and check the scorecard.
8. Commit the case file. The PR description should explain *why*
   this case was added -- a bug it would have caught, a behaviour
   you want to lock in, etc.

No code change to the harness should be required for a new case.
That is the point of the JSON format.

---

## Why we are NOT implementing it this episode

Scope discipline. S02E18 is **inventory and design** -- the artefacts
this episode ships are the prompt library, this design sketch, and the
temperature-cookbook context. Building the runner means:

- A new test project or a new sub-command (`az-ai eval`).
- A trait-checker implementation -- substring, regex, or trait-judge.
  Each has trade-offs that deserve their own episode.
- A CI integration (when does the eval run? on every PR? nightly?).
- Cost-budgeting hooks (Morty wants to see token spend before he
  approves a CI workflow that calls the model on every PR).
- Fixture seed data for at least the five Squad personas plus the
  agent-mode appendix and the Ralph appendix.

Each of those is a meaningful design decision in its own right. Doing
them in the same episode that codifies the prompts they would test
would conflate "what do we have" with "how do we measure what we
have", and the second question deserves its own writers' room.

The seam is locked here -- prompt IDs, case JSON shape, scorecard
JSON shape. A future episode (proposed: S03E0x "The Runner") picks up
the runner. When it does, the inputs and outputs above are the
contract.

---

## See also

- [`library.md`](./library.md) -- the prompt inventory this harness
  would test against.
- [`eval-harness.md`](./eval-harness.md) -- the deeper, ship-ready
  spec for fixture format, runner shape, and regression gates.
- [`ab-testing.md`](./ab-testing.md) -- how to compare two versions
  of a prompt once the harness exists.
- [`change-management.md`](./change-management.md) -- the contract
  every prompt change must satisfy. The eval harness is the
  enforcement mechanism.

-- *Maestro. With an M.*
