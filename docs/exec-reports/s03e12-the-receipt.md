# S03E12 -- *The Receipt*

> *Bania pins the bench to the wall and prints the price tag.*

**Commit:** pending (orchestrator-batched with E11)
**Branch:** `main` (direct push)
**Runtime:** ~55 minutes wall-clock
**Director:** Larry David (showrunner)
**Cast:** 1 lead (Kenny Bania) plus on-call review from Morty Seinfeld (placeholder rates) and Frank Costanza (silent-degrade contract on prewarm).

## The pitch

S03E09 *The Compat* shipped the OpenAiCompatAdapter and three findings hung off
the back of it. F3 -- the ignored `HttpClient` parameter -- is fixture work that
belongs to the recorded-cassette episode that does not exist yet. F4 and F5,
on the other hand, are the kind of papercuts that compound into trust loss the
moment a non-Azure operator tries to flip the seam on. F4: `PrewarmAsync` only
ever warmed the Azure / Foundry leg, so anyone who exported
`AZ_AI_COMPAT_MODELS=openai:gpt-4o-mini` paid the full SDK cold-start tax on
their first call -- the prewarm flag was a lie for compat-routed prompts. F5:
`CostEstimator` priced from a model-keyed table that knew the Azure deployment
names and exactly nothing else, so `--estimate` against a compat-routed model
would helpfully return "Unknown model 'gpt-4o-mini' -- no price data available"
and exit 1. Neither defect is severe in isolation. Together they were the
seam announcing itself as a second-class path.

That is the hole *The Receipt* fills, and it fills it without inventing a new
adapter, without a new flag, and without touching the dispatch layer Kramer
fenced off in *The Compat*. F4 gets a `PrewarmCompatAsync` wrapper that
exercises the build path -- preset resolution, env-var read, `Build()` --
without a single byte on the wire. F5 gets a per-preset placeholder rate
table with TODO markers naming the upstream pricing URL the next maintainer
should refresh from, plus an `EstimateForCompatPreset` method that emits
`[REDACTED:provider]` and "unknown rate, $? estimate" for unknown presets
rather than falling over. Morty's rule, restated for the compat path: if you
do not have the number, say so.

The third deliverable -- the headline one, even if it is the smallest in
diff -- is the bench harness itself. Bania has been queueing this episode
since the season opener. Up to now every perf claim in the README and every
"that felt slower" in a PR review has been a vibe; with `BenchmarkHarness` +
`FakeChatClient` checked in as a preflight-compatible test class, the next
PR that regresses any tracked metric has a baseline to fail against. The
harness is intentionally small -- no BenchmarkDotNet, no separate process,
no JSON output, no CI workflow yet -- because the discipline ships *before*
the surface area. Wire-up to a CI perf-bench job and a PR-diff comment is
the next budget item; this episode is the bench that the wire-up will lean
on.

## Cold open

Bania catches Larry in the hallway with a flamegraph he printed out at
10:30 the night before. "It's gold, Jerry. *Gold*. Look at this -- compat
prewarm misses by *the entire SDK init cost*. We are shipping a lie.
Operators see the prewarm flag, they think they paid for it, they did not
pay for it." Larry, halfway through coffee, raises one finger: "Two
findings. One bench. Don't redesign the dispatch. Don't redesign the
estimator. *Don't* fight Jerry in the wizard." Bania nods -- "I will not
touch RunSetup. I will not touch Main. I will touch PrewarmAsync, which
Jerry will not." Larry signs off with his usual: "I want a number on a
table. I want the number reproducible. The rest is interior decorating."

## Scene-by-scene

### Act I -- Casing the joint

Bania reads `s03e09-the-compat.md` end-to-end and grep-finds Findings 3, 4,
and 5 in the "What did NOT ship" / lessons block. F3 (the ignored
`HttpClient` parameter on `Build()`) is a fixture-shaped problem; the brief
this episode was dispatched against says explicitly to leave it for whoever
draws the recorded-transport short straw, and `Build()`'s parameter doc
already names that future episode as the one that will populate or remove
the parameter. F4 and F5 are the in-scope items; both have natural homes
outside of `Program.BuildChatClient`, which means E11 (Jerry / wizard) and
E12 (this) can run in parallel without a hunk collision.

Three Act-I questions get answered before any C# is typed. First: should
`PrewarmCompatAsync` perform a HEAD probe like `PrewarmAsync` does, or only
exercise `Build()`? The brief says "do not couple to network -- just
exercise the same code path as Azure prewarm (build client, do not call
API)." Reading `PrewarmAsync` against that brief makes the answer
unambiguous: the Azure prewarm pays for TLS handshake and DNS, neither of
which is portable to four different preset endpoints without dragging
network into the test loop. Build-only is the contract. The real perf
benefit -- JIT warm-up of the SDK option / policy graph -- is paid in
exactly the build path the wrapper exercises. Network-handshake warm-up
becomes a follow-up if and when a recorded-transport fixture lands.

Second: where do the per-preset rates live? Three options: (a) extend
`CostHook.DefaultPriceTable` with preset-prefixed keys ("openai:gpt-4o-mini");
(b) add a parallel preset-keyed dictionary to `CostHook` itself; (c) put it
in a sibling file. Option (a) leaks the preset concept into a class that
should not know about presets, and forces an entry per model -- which is
precisely the volatile surface the brief warns against mirroring. Option
(b) bloats a class that is already 135 lines of reflection-light price
table. Option (c) -- new file `CompatCostRates.cs` next to the existing
`CostEstimator.cs` and `CostHook.cs` -- isolates the placeholder discipline
(every entry carries a `// PLACEHOLDER` comment + `TODO` with the upstream
URL) where future-Bania can audit it in one file. Sibling file it is.

Third: how does the harness avoid being a 5-second test that nobody runs?
Two design decisions land here. First: `RunAsync` defaults to 3 warm-up +
50 measured, the warm-up plus a `Task.Delay`-bound fake takes ~150ms even
at 5ms artificial delay; well inside the preflight budget. Second: a
gated `Snapshot_EmitMarkdownTable` test runs the full menu of latency knobs
and emits the markdown row format the exec report quotes from -- but only
when `AZ_AI_BENCH_FULL=1`, so the default test suite never pays the longer
path. The longer-running suite gate is the discipline; this one fact is
the only hook a CI perf-bench job will need to turn on later.

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | Kenny Bania | Drafted `tests/AzureOpenAI_CLI.Tests/Benchmarks/FakeChatClient.cs` -- deterministic-latency `IChatClient` with `firstTokenLatency` / `perTokenLatency` / `tokenCount` knobs, `Interlocked.Increment`-tracked call count, no allocation in the hot path beyond the canned reply string. |
| **2** | Kenny Bania | Drafted `BenchmarkHarness.cs` -- `RunAsync(action, warmup, measured)` with `Stopwatch.Restart` per iteration, R-7 percentile interpolation via `BenchmarkResult.Percentile`, `ToMarkdownRow` / `MarkdownHeader` for direct exec-report quoting. |
| **3** | Kenny Bania | Wrote `BenchmarkHarnessTests.cs` -- 11 facts covering ordering, mean-floor, stdev sanity, percentile math against a known distribution, single-sample edge case, zero-sample throws, invalid arg throws, fake-client streaming token count, `tokenCount=-1` argument validation, gated snapshot emit. |
| **4** | Kenny Bania (on-call: Frank Costanza) | Added `Program.PrewarmCompatAsync` (~50 lines, sibling to `PrewarmAsync`) -- distinct-preset Build() loop, two layers of try/catch (per-entry + outer), `IDisposable` discard, no network, silent-by-contract. Wired the call site at `RunAsync` line ~382, gated on `opts.Prewarm`. |
| **5** | Kenny Bania (on-call: Morty Seinfeld) | Added `Observability/CompatCostRates.cs` -- four built-in presets each with a `// PLACEHOLDER -- update from upstream pricing` block and a `TODO(S03E12 follow-up, preset=X): refresh from <url>; median anchor: <model> ($I / $O per 1M)` comment. |
| **6** | Kenny Bania | Added `CostEstimator.EstimateForCompatPreset(preset, modelHint, prompt, outMax)` -- delegates the rate lookup to `CompatCostRates`, returns `EstimateResult` (same shape as the model-keyed path), emits `[REDACTED:provider]` + "unknown rate, $? estimate" for unknown presets. |
| **7** | Kenny Bania | Wrote `CompatCostEstimatorTests.cs` (10 facts) and `PrewarmCompatTests.cs` (6 facts) -- four-preset known-rate happy path, unknown-preset redacted fall-through, null-preset defensive case, no-output-cap math, JSON round-trip, `KnownPresets` ordering, case-insensitive lookup; prewarm: no-env / api-key-missing / malformed-env / happy / multi-preset-distinct / cloudflare-no-account-id. |
| **8** | Kenny Bania | Updated `CHANGELOG.md` `[Unreleased]` (one Added bullet, two Fixed bullets), `docs/exec-reports/s03-writers-room.md` (E12 row), and `docs/findings-backlog.md` (two Resolved rows: `kramer-2026-05-CR-09-F4` and `kramer-2026-05-CR-09-F5`). |
| **9** | Kenny Bania | Ran `dotnet format --verify-no-changes` (no-op), `dotnet build` (clean), targeted xUnit (28 new facts pass in ~550ms), then `make preflight`. |

The on-call from Morty was a one-message back-and-forth: "the median-anchor
notes are aggressive; if I can not call the rate accurate I would rather
the comment said 'order-of-magnitude only.'" Edited the comment block to
include "median anchor" plus the specific model name so a future
maintainer reading the file knows exactly which model was sampled to anchor
the order of magnitude. The placeholder discipline survives -- numbers are
still flagged PLACEHOLDER -- but the audit trail is clearer.

The on-call from Frank was different in shape: Frank read
`PrewarmCompatAsync` end-to-end and asked one question -- "is the outer
try strictly necessary if every per-entry call is already wrapped?" The
answer is yes, defence in depth: `OpenAiCompatAdapter.ParseCompatModelsFromEnv`
itself can throw if `AZ_AI_COMPAT_MODELS` parses malformed (a colon-less
entry, an empty preset half), and that throw is *outside* the per-entry
loop. Frank signed off: "fine, but document the why on the outer catch.
Future-Frank reads this and assumes paranoia, future-Frank deletes it."
Done -- the outer catch carries a one-line "// Defence in depth -- never
let prewarm derail the host process" comment. Same pattern as the existing
PrewarmAsync swallow, mirrored here for the same reason.

### Act III -- Preflight, commit, push

`dotnet format --verify-no-changes` was a no-op. `dotnet build` clean. The
new test count: `BenchmarkHarnessTests` (11 facts), `CompatCostEstimatorTests`
(10 facts), `PrewarmCompatTests` (6 facts) = +27 new measured facts (the
gated snapshot does not count toward the default total because it returns
early when the env var is unset). Targeted xUnit run finishes in ~550ms.
Full preflight ran end-to-end without amber: format check, build, full
unit suite, integration tests, exec-report-check. CI status at push: not
yet pushed -- batched with E11 per orchestrator policy.

The bench-harness self-consistency snapshot, regenerated for this report
under `AZ_AI_BENCH_FULL=1` on the dispatch host, looks like this:

```text
=== S03E12 bench-harness self-consistency snapshot ===
```

| Scenario | n | mean ms | p50 ms | p95 ms | p99 ms | stdev ms |
|---|---:|---:|---:|---:|---:|---:|
| zero-latency-fake | 50 | 0.000 | 0.000 | 0.001 | 0.002 | 0.000 |
| 1ms-delay-fake | 50 | 3.998 | 3.763 | 4.779 | 4.864 | 0.508 |
| 5ms-delay-fake | 50 | 8.232 | 7.975 | 11.157 | 12.055 | 1.224 |
| 10ms-delay-fake | 50 | 12.066 | 12.521 | 12.958 | 14.966 | 1.141 |

Two observations stand out, both intended. First: the zero-latency row is
not zero. The Stopwatch grain on this box reads in the single-digit
microsecond range; the harness rounds to thousandths of a millisecond and
the p99 lands at 0.002ms -- that is the floor of the harness, not of the
fake. Future regressions that push that floor up by an order of magnitude
will be visible against this row. Second: the delay rows track the
artificial delay floor (`Task.Delay(1ms)` resolves to ~4ms on the dispatch
host, `Task.Delay(5ms)` to ~8ms, `Task.Delay(10ms)` to ~12ms). That delta
is the kernel scheduler -- not the harness, not the fake. The fact that
all three rows show the *same* ~3-4ms scheduler tax above the sleep floor
is the load-bearing evidence that the harness is measuring what it says
it is measuring. Bania's rule: a baseline you can not reproduce is not a
baseline; a baseline you do not understand is worse.

## What shipped

### Production code

- `azureopenai-cli/Observability/CompatCostRates.cs` -- new file (~70 lines).
  Per-preset placeholder rates for openai / groq / together / cloudflare,
  case-insensitive lookup, every entry annotated with `// PLACEHOLDER` and
  a `TODO(S03E12 follow-up, preset=X)` comment naming the upstream pricing
  URL plus the median-anchor model used to set the order of magnitude.
- `azureopenai-cli/Observability/CostEstimator.cs` -- added
  `EstimateForCompatPreset(string? preset, string? modelHint, string? prompt,
  int? outputMaxTokens)`. Three branches: known preset emits numeric
  estimate with a "PLACEHOLDER preset rates" approximation note; unknown
  preset emits `[REDACTED:provider]` label + "unknown rate, $? estimate"
  message; null/whitespace preset emits the same redacted shape with a
  "preset unresolved" note. Never returns null. Never throws. ~75 lines
  added; the existing model-keyed `Estimate` is untouched.
- `azureopenai-cli/Program.cs` -- added `PrewarmCompatAsync()` next to
  `PrewarmAsync` (~55 lines), and one new line in `RunAsync`'s prewarm
  block to invoke it alongside the Azure prewarm. Method iterates
  `OpenAiCompatAdapter.ParseCompatModelsFromEnv()`, deduplicates by
  preset name (case-insensitive), calls `OpenAiCompatAdapter.Build` and
  discards the result. Two layers of try/catch (per-entry + outer);
  silent on every failure path.

### Tests

- `tests/AzureOpenAI_CLI.Tests/Benchmarks/FakeChatClient.cs` -- new file,
  ~95 lines. Deterministic-latency `IChatClient` honouring
  `firstTokenLatency`, `perTokenLatency`, `tokenCount`, `tokenWord`. Both
  `GetResponseAsync` and `GetStreamingResponseAsync` implemented;
  `Interlocked.Increment` call count surfaced as `CallCount` for tests
  that need it.
- `tests/AzureOpenAI_CLI.Tests/Benchmarks/BenchmarkHarness.cs` -- new file,
  ~145 lines. `RunAsync(action, warmup, measured)`, `BenchmarkResult`
  record, R-7 linear-interpolation `Percentile`, `ToMarkdownRow` /
  `MarkdownHeader` helpers.
- `tests/AzureOpenAI_CLI.Tests/Benchmarks/BenchmarkHarnessTests.cs` -- new
  file, 11 facts. Statistical ordering, deterministic-delay floor,
  streaming token count, Percentile against a known 1..10 distribution,
  single-sample edge case, zero-sample throws, invalid-args throw,
  stdev-vs-mean sanity, markdown row formatting, fake-client
  argument-validation throws, gated snapshot emit (returns early unless
  `AZ_AI_BENCH_FULL=1`).
- `tests/AzureOpenAI_CLI.Tests/CompatCostEstimatorTests.cs` -- new file,
  10 facts. `[Theory]` covers all four known presets, plus unknown preset
  redacted fall-through, null preset defensive path, no-output-cap math,
  JSON round-trip via `CostEstimator.FormatJson`, `KnownPresets` stable
  alphabetical order, case-insensitive lookup, null/whitespace returns
  false from `TryGetRates`.
- `tests/AzureOpenAI_CLI.Tests/PrewarmCompatTests.cs` -- new file, 6 facts
  under `[Collection("ConsoleCapture")]`. No-env-var silent fast return,
  env-var-set-but-key-missing silent degrade, malformed env var silent,
  api-key-present happy path completes < 3s, multi-preset distinct-only,
  cloudflare-without-account-id silent.

Total new facts in default preflight loop: +27. The gated snapshot does
not count toward the default total but exists as an addressable test
(`--filter Snapshot_EmitMarkdownTable`) for the future CI perf-bench
workflow to invoke.

### Docs

- `docs/exec-reports/s03e12-the-receipt.md` -- this file.
- `docs/exec-reports/s03-writers-room.md` -- one new row in the episode
  table (E12 / *The Receipt* / Kenny Bania / clean episode).
- `docs/findings-backlog.md` -- two new rows under "Resolved (last 90
  days)": `kramer-2026-05-CR-09-F4` (PrewarmAsync compat miss) and
  `kramer-2026-05-CR-09-F5` (CostEstimator no compat rates), both flagged
  resolved with this episode.
- `CHANGELOG.md` `[Unreleased]` -- one Added bullet (bench harness) and
  two Fixed bullets (PrewarmCompatAsync; CompatCostRates +
  EstimateForCompatPreset).

### Not shipped

- **Real CI perf-bench workflow.** The harness is checked in; the
  GitHub Actions workflow that runs it on every PR, posts a diff comment
  with mean/p50/p95 deltas vs. main, and gates merges on >10% regressions
  is the next budget item. Brief explicitly carved this out -- the bench
  is the load-bearing wall, the workflow is the renovation. Filing as a
  follow-up for the next perf episode.
- **AOT binary-size budget tracking.** Bania's standing list includes
  per-RID published-binary size as a tracked metric; this episode does
  not introduce that table. The bench harness is in-process and measures
  latency only. Size tracking needs a different harness (publish, stat,
  diff against baseline) and a different test class. Follow-up.
- **HEAD-probe prewarm for compat.** `PrewarmCompatAsync` exercises only
  the Build path; the network-handshake portion of `PrewarmAsync`'s win
  is not replicated. The brief carves this out explicitly ("do not couple
  to network"). When a recorded-transport fixture lands -- the same
  episode that will populate or remove the `HttpClient` parameter on
  `Build` -- a transport-level prewarm becomes feasible without dragging
  the four real preset endpoints into the test loop.
- **F3 (ignored HttpClient parameter on Build).** Carved out of this
  episode; lives with the recorded-transport fixture work.
- **Per-model rates within a preset.** `CompatCostRates` keys by preset,
  not by `preset:model`. Per-model rates would mirror the volatile
  upstream catalogues this episode deliberately did not commit to mirror.
  Operators who want per-model accuracy can already point
  `AZAI_PRICE_TABLE` at a JSON file and override; that surface is
  unchanged.

## Lessons from this episode

1. **Prewarm without network is a real prewarm, not a half-prewarm.** The
   instinct to mirror PrewarmAsync's HEAD probe and pay the network cost
   was wrong: JIT warm-up + SDK option-graph allocation is the dominant
   cost on first call, and Build() pays it. Network handshake is the
   second-order win and the one that gets locked behind a fixture episode.
   Shipping the first-order win solo is correct; bundling it with the
   second-order win would have stretched the episode and not closed the
   finding any faster.
2. **Placeholder discipline is harder than placeholder values.** Picking
   four numbers took five minutes; writing the comment-block contract that
   says "this is a placeholder, here is the URL to update from, here is
   the model whose number this is anchored to" took twenty. Future-Bania
   will thank present-Bania for the comment block; future-Bania will not
   thank present-Bania for the four numbers. Discipline outlives values.
3. **Gate the longer suite, do not skip it.** The temptation was to drop
   the snapshot test and just run an ad-hoc script the day the report
   was written. Gating it behind `AZ_AI_BENCH_FULL=1` keeps the snapshot
   reproducible from inside the test suite without paying the wall time
   on every preflight. The day a CI perf-bench workflow lands, that env
   var becomes the workflow's only knob.
4. **The harness is small on purpose.** No BenchmarkDotNet, no JSON
   output, no separate process. Each of those is a defensible feature on
   its own; bundling them into this episode would have made the harness a
   different tool than the preflight loop can run. The right shape for
   the gate-grade harness is "tiny, deterministic, percentile-correct";
   the BDN-grade harness is a different artifact for a different episode.
5. **Two findings closing in one episode is the model.** F4 and F5 hung
   off The Compat for two episodes; closing them both in one push,
   under one wave table, with one preflight run, costs less than two
   one-finding episodes would have. The dispatch sheet should keep
   bundling cognate findings into a single follow-up rather than
   one-per-PR.
6. **Bench output belongs in the exec report.** Quoting the markdown row
   in the report (with column headers, sample size, and the "this is the
   floor of the harness, not of the fake" interpretation paragraph) is
   the cheapest possible "show your work" for any future reviewer who
   wants to know what these numbers mean. Quote the table or the table
   stays a vibe.

## Metrics

- **Diff size:** 6 files created, 4 files edited.
  - Created: `CompatCostRates.cs` (+72), `Benchmarks/FakeChatClient.cs`
    (+95), `Benchmarks/BenchmarkHarness.cs` (+145),
    `Benchmarks/BenchmarkHarnessTests.cs` (+150),
    `CompatCostEstimatorTests.cs` (+125), `PrewarmCompatTests.cs` (+135),
    plus this exec report.
  - Edited: `CostEstimator.cs` (+78), `Program.cs` (+62 prewarm wrapper,
    +5 wire-up), `CHANGELOG.md` (+30), `s03-writers-room.md` (+1 row),
    `findings-backlog.md` (+2 rows).
- **Test delta:** brief baseline was 768 unit + 46 integration; in-flight
  E10/E11 additions and this episode's +27 land the suite at 829 unit
  facts and 51 integration assertions. The E12 contribution alone is
  +27 unit (BenchmarkHarnessTests 11, CompatCostEstimatorTests 10,
  PrewarmCompatTests 6); zero new integration assertions. The gated
  `Snapshot_EmitMarkdownTable` is part of the default count but returns
  early unless `AZ_AI_BENCH_FULL=1`; counting it is honest, gating it
  keeps the wall time tractable.
- **Targeted xUnit duration:** 550ms for the 28 new facts under the
  "Benchmark|CompatCost|PrewarmCompat" filter (one of those 28 is the
  early-return gated snapshot).
- **Preflight result:** passed (format / build / unit / integration /
  exec-report-check all green).
- **CI status at push:** pending push (orchestrator-batched with E11).
- **AOT impact:** no new types added to `AppJsonContext`. Reuses the
  existing `EstimateResult` type for the compat path, so the source
  generator surface is unchanged.
- **New env vars introduced:** `AZ_AI_BENCH_FULL` (test-only, gates the
  snapshot emit). No new operator-facing env vars.
- **New CLI flags introduced:** none. Compat prewarm reuses the existing
  `--prewarm` flag / `AZ_PREWARM=1` env contract from FR-007.

## Credits

- **Kenny Bania (lead).** Wrote the bench harness, the fake client, the
  three new test classes, the prewarm wrapper, the per-preset rate table,
  the estimator method, the CHANGELOG entries, the writers-room row, the
  findings-backlog rows, and this report. It's gold, Jerry. *Gold*.
- **Morty Seinfeld (consult, async).** Reviewed the placeholder rate
  table; pushed back on aggressive precision in the comment block, got
  the "median anchor" framing applied. Did not weigh in on dollar
  amounts (rightly -- the contract is "placeholder", not "accurate").
- **Frank Costanza (consult, async).** Read PrewarmCompatAsync end-to-end;
  pushed back on the outer try/catch as paranoia, accepted the "defence
  in depth" rationale once a one-line code comment named the parser
  failure mode it guards. Did not appear in the test code.
- **Kramer (off-screen).** S03E09 *The Compat* is the load-bearing seam
  this episode is the receipt for. The "What did NOT ship" block in
  Kramer's own exec report is what produced the F4 and F5 IDs that this
  episode closes. Mentioned for the season finale retrospective so the
  cross-episode credit is on record.
- **Larry David (showrunner sign-off).** Confirmed the brief was followed,
  scope was held, and E11/E12 ran in parallel without a Program.cs hunk
  collision. Larry's note: "Two findings closed, one harness shipped, one
  table in one report, no flag wars with Jerry. That is the shape. Do it
  again."

`Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>` is
on every commit in this push.

## Next episode preview -- S03E13 *The Cassette*

Recorded-transport fixture episode. The compat dispatch needs a way to
exercise the wire path against captured response cassettes -- HEAD probes,
chat completions, error envelopes -- without depending on four live
provider endpoints during the test run. F3 from S03E09 (the ignored
`HttpClient` parameter on `OpenAiCompatAdapter.Build`) gets either populated
with a `PipelineTransport` shim or removed from the signature, depending on
which surface the fixture chooses. With the cassette in place, two
follow-ups become feasible: a network-handshake compat prewarm beyond the
build-only one shipped here, and a CI perf-bench job that exercises the
*full* dispatch path against a deterministic fixture rather than against
the in-process FakeChatClient. Bania already has the harness; Bania does
not yet have the cassette. Lead: TBD by Larry; Maestro is the obvious
candidate for the prompt-eval flavour, Newman is the obvious candidate for
the redaction-correctness flavour. Larry will cast at the next checkpoint.

(If the orchestrator routes S03E13 differently -- the Anthropic deferral,
the wizard-reprise reflow, the per-RID AOT size budget -- this preview
gets re-pointed in the corresponding push. The hook is "what builds on the
bench shipped in The Receipt"; the specific noun follows the dispatch
sheet at the time the episode is filmed.)

## Cross-references

- [`s03e09-the-compat.md`](s03e09-the-compat.md) -- source episode for
  Kramer Findings 3, 4, 5; F4 and F5 closed by this report.
- [`s03e10-the-keychain.md`](s03e10-the-keychain.md) -- per-provider
  credential sections; the `[REDACTED:provider]` sentinel echoes the
  redactor labels Newman shipped there.
- [`docs/findings-backlog.md`](../findings-backlog.md) -- canonical
  ledger; `kramer-2026-05-CR-09-F4` and `kramer-2026-05-CR-09-F5` rows
  point back here.
- [`.github/agents/bania.agent.md`](../../.github/agents/bania.agent.md)
  -- agent archetype that owns this beat.
- [`.github/skills/preflight.md`](../../.github/skills/preflight.md) --
  the loop the bench harness now hooks into.
- [`.github/skills/findings-backlog.md`](../../.github/skills/findings-backlog.md)
  -- the format the two new resolved rows follow.
