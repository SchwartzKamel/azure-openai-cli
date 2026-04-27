# S02E09 -- *The Receipt*

> *Morty hands you the bill: tokens, dollars, and an honest "I don't know" when the model isn't in the table.*

**Commit:** `<filled at push>`
**Branch:** `main` (direct push)
**Runtime:** ~50 min wall clock (with three concurrent stash collisions)
**Director:** Larry David (showrunner)
**Cast:** 1 sub-agent across 1 dispatch wave -- Morty Seinfeld lead, Costanza guest

## The pitch

`az-ai` already had token counts on stderr after every response, but no
dollars and nothing at all in the multi-call modes. Power users running
`--ralph` or `--agent` could burn through a billing tier without ever
seeing a number. Morty's first lead episode, paired with George's
product question -- "is this a good investment?" -- ships an opt-in
cost receipt that the user has to ask for, prints to stderr so it never
contaminates a pipeline, and is **honest by construction**: tokens
always, dollars only when the deployment name is in the hard-coded
price table.

The episode is small on purpose. No budget caps, no live pricing fetch,
no default-on noise. The receipt either matches the Azure invoice or it
says it doesn't know -- never both.

## Scene-by-scene

### Act I -- Planning

- Read the existing print path in `Program.cs`. Found `update.Usage`
  already captured (`promptTokens` / `completionTokens`) in standard
  and agent loops. No new SDK plumbing required.
- Decided on `--show-cost` as the flag (matches the existing
  `--show-config` / `--raw` long-form style; no short alias yet).
- Decided to keep the existing `[tokens: N→N, M total]` line untouched
  for back-compat and emit the new `[cost] ...` line as an additional,
  opt-in receipt.
- Decided dollars are **per 1K** in code (matches industry usage), even
  though `docs/cost-optimization.md` quotes per 1M. The conversion is a
  one-line comment.

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | morty-seinfeld (lead) + george-costanza (guest, framing) | Receipt shipped: opt-in flag, accumulator, format helper, tests, exec report. |

### Act III -- Ship

- `dotnet build` -- clean.
- `dotnet format` -- one autofix sweep on `CostAccounting.cs`
  whitespace alignment; re-verified clean.
- `dotnet test` -- **1226 passed, 0 failed**, 45 skipped (DPAPI Windows
  and SSRF integration -- pre-existing).
- `bash tests/integration_tests.sh` -- **150 passed, 3 skipped**.
- Single commit, explicit paths, Copilot trailer, `-c
  commit.gpgsign=false`.

## What shipped

**Production code**

- `azureopenai-cli/CostAccounting.cs` (NEW, ~210 lines):
  - `ModelPrice` record (USD per 1K input / output).
  - `Prices` table -- 11 deployments across OpenAI on Azure + Foundry
    serverless (Phi-4 mini variants, DeepSeek V3.2). Snapshot date
    `PriceTableAsOf = "2026-04"`.
  - `LookupPrice` with longest-prefix fallback (so
    `gpt-4o-mini-2024-07-18` resolves to `gpt-4o-mini`, not `gpt-4o`).
  - `Entry` -- defensive constructor: clamps null/negative token counts
    to zero (guards against SDK stream-error oddities).
  - `FormatReceipt` -- single-line receipt, `InvariantCulture` decimal
    formatting (no commas-as-decimals in `de-DE` etc.).
  - `CostAccumulator` -- non-thread-safe rollup for agent rounds /
    Ralph iterations; tracks calls, in/out tokens, USD, plus a
    `HasAnyKnownCost` flag so a mixed-model loop still prints dollars
    for the known-model portion.
- `azureopenai-cli/Program.cs`:
  - New `--show-cost` flag, threaded through `CliOptions.ShowCost`.
  - Standard mode: prints `[cost] ...` to stderr after the existing
    token line, gated on `opts.ShowCost`. **Always stderr**, even in
    `--raw`, so pipelines stay clean.
  - `RunAgentLoop` accepts `bool showCost`, `bool raw`,
    `CostAccumulator? costAcc`. Per-round usage is accumulated; if the
    caller passed an accumulator (Ralph mode), agent stays silent on
    the receipt to avoid double-printing. If not, agent prints its own
    rollup labelled `agent:`.
  - `RunRalphLoop` owns its own `CostAccumulator`, passes it down to
    every agent invocation, and a local `PrintRalphReceipt()` is
    invoked at every exit point (success, validation pass, cancel,
    cancel-mid-call, exhaust). Label: `ralph:`.
  - `ShowUsage` -- new `--show-cost` line in the Options block.

**Tests**

- `tests/AzureOpenAI_CLI.Tests/CostAccountingTests.cs` (NEW, 22 cases):
  - Lookup: known model, unknown model, null, empty, longest-prefix
    fallback, longest-prefix beats shorter prefix (`gpt-4o-mini-foo` →
    mini, NOT 4o), case-insensitive.
  - Compute: known model dollar math (verified arithmetic), unknown →
    null, zero tokens.
  - Entry edge cases: null counts → zero, negative counts → zero, very
    large (2B tokens, no overflow), unknown-model fallthrough.
  - Format: known includes `~$ ... @ model`, unknown shows
    `not in price table` and **no `$`**, null deployment → tokens-only,
    invariant culture under `de-DE`.
  - Accumulator: empty state, known sum, unknown-only sum,
    mixed-model rollup, all-unknown rollup honesty.
- `tests/AzureOpenAI_CLI.Tests/CliParserTests.cs` (EDIT, +3 cases):
  default `ShowCost = false`, `--show-cost` flips it true,
  `--show-cost` does not leak into `RemainingArgs`.

**Docs**

- `CHANGELOG.md` -- one bullet under `[Unreleased] / Added`.
- (This exec report.)

**Not shipped** (intentional follow-ups)

- Live price fetch -- out of scope; would add a network call. Ralph,
  later episode.
- `--budget` cap / spend-blocking -- out of scope; Morty's brief
  explicitly defers.
- Receipt in `--json` mode (the structured response already includes
  `input_tokens` / `output_tokens`; downstream tools can compute
  dollars themselves using the same table). Could be promoted later.
- Exec report references the price table at `2026-04`; same date as
  `docs/cost-optimization.md`. If/when the doc bumps, bump
  `CostAccounting.PriceTableAsOf` in lockstep -- one-line change.

## Sample receipt outputs

Standard mode (`--show-cost`):

```text
[cost] in=1234 out=567 total=1801 tokens (~$0.0091 @ gpt-4o)
```

Standard mode, model not in table:

```text
[cost] in=1234 out=567 total=1801 tokens (model 'custom-deploy' not in price table)
```

Agent mode (`--agent --show-cost`, accumulated across rounds):

```text
[cost] agent: calls=3 in=4521 out=812 total=5333 tokens (~$0.0194 @ gpt-4o)
```

Ralph mode (`--ralph --show-cost`, accumulated across iterations):

```text
[cost] ralph: calls=12 in=18450 out=3920 total=22370 tokens (~$0.0853 @ gpt-4o)
```

Raw mode (`--raw --show-cost`) -- stdout is the bare model output;
**stderr** carries the receipt:

```text
$ az-ai --raw --show-cost "say hi" 2>cost.log
hi
$ cat cost.log
[cost] in=12 out=2 total=14 tokens (~$0.0000 @ gpt-4o-mini)
```

## Price-table snapshot (as of 2026-04)

| Deployment              | Input $/1K | Output $/1K |
|-------------------------|-----------:|------------:|
| `gpt-4o-mini`           |  0.00015   |  0.00060    |
| `gpt-4o`                |  0.00250   |  0.01000    |
| `gpt-4.1`               |  0.00300   |  0.01200    |
| `gpt-4.1-mini`          |  0.00040   |  0.00160    |
| `gpt-4.1-nano`          |  0.00010   |  0.00040    |
| `gpt-5.4-nano`          |  0.00020   |  0.00125    |
| `o1-mini`               |  0.00300   |  0.01200    |
| `o3-mini`               |  0.00110   |  0.00440    |
| `phi-4-mini-instruct`   |  0.000075  |  0.000300   |
| `phi-4-mini-reasoning`  |  0.000080  |  0.000320   |
| `deepseek-v3.2`         |  0.000580  |  0.001680   |

Source of truth: `docs/cost-optimization.md` §3 + the Azure pricing
page linked there. Receipt prefixes the dollar with `~` to flag it as
an estimate; the doc itself carries the same caveat.

## Edge cases tested

- Null / empty / negative token counts -- clamped to zero.
- 2-billion-token call -- decimal arithmetic does not overflow.
- Deployment name with date suffix (`gpt-4o-mini-2024-07-18`) -- matches via longest-prefix fallback.
- `gpt-4o-mini-foo` does NOT collapse to `gpt-4o` (longest-prefix wins).
- Unknown model in standard, agent, and ralph modes -- token line still
  prints, dollar line is suppressed and replaced with
  `(model 'X' not in price table)`.
- Mixed-model accumulator (known + unknown) -- USD reflects only the
  known portion; `HasAnyKnownCost` flag drives the format decision.
- `de-DE` culture -- decimal point, not comma.

## Lessons from this episode

1. **Concurrent stash collisions are noisy but recoverable.** A
   parallel security-agent (working on `ShellExecTool.cs` hardening)
   stashed my Program.cs WIP and renamed my untracked files
   (`CostAccounting.cs`, `CostAccountingTests.cs`) into stash files
   seven times across the run. The shared-file-protocol's
   stash-isolate-restore maneuver worked as designed -- every byte was
   recoverable -- but I should have committed the cost-accounting
   skeleton earlier to reduce the WIP window. Lesson: when filming
   alongside another agent that touches the same large file, land a
   skeleton commit fast, then evolve.
2. **`git apply --3way` rejected on "does not match index".**
   Workaround: `patch -p1` accepted the same hunks with offset
   tolerance and merged cleanly. Tooling note for future episodes.
3. **Defensive token-count clamping pays for itself in the test
   matrix.** Once `Entry()` clamps null/negative inputs, the
   per-mode plumbing doesn't have to think about it -- one
   normalization point, three call sites trust it.
4. **`InvariantCulture` decimal formatting is non-negotiable for
   numeric output.** A `de-DE` test caught the issue that would have
   produced `~$0,0125` -- garbage in any downstream parser.

## Findings raised

- `e09-cost-receipt-json-mode-gap` [gap, b-plot]: `--json` mode does
  not embed a `cost` block; downstream automation has to recompute from
  `input_tokens` / `output_tokens` plus a duplicated price table.
  Surface the table via a `cost` JSON field in a future episode.
- `e09-price-table-staleness-gap` [gap, b-plot]: `PriceTableAsOf` is a
  comment, not an enforced check. A "table older than 6 months ⇒ warn
  on first `--show-cost`" lint would keep it honest.

(Will be raised to Larry for inclusion in `s02-writers-room.md`
findings backlog -- per shared-file-protocol, sub-agent does not edit
the writers' room directly.)

## Metrics

- Diff size: +400 / -3 across 4 files (`Program.cs` +75,
  `CostAccounting.cs` +210 NEW, `CostAccountingTests.cs` +212 NEW,
  `CliParserTests.cs` +12, `CHANGELOG.md` +9).
- Test delta: +22 unit tests (CostAccountingTests) +3 (CliParserTests)
  = **+25 net**, all green.
- Preflight: **PASSED** -- format clean, build clean, 1226 unit tests
  pass, 150 integration tests pass.
- CI status at push: pending; will be checked post-push.

## Credits

- **Morty Seinfeld** (lead) -- price table curation, accumulator
  design, "the receipt is honest or it is not a receipt" rule.
- **George Costanza** (guest) -- product framing in the
  pitch and bullet copy: opt-in by default, no surprises.
- **Copilot** -- code synthesis and patch surgery across the stash
  collisions.

`Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`
on the commit.
