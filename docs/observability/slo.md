# Reliability SLOs and Telemetry Charter

> *Owner: Frank Costanza (SRE / observability). Companion to the
> `findings-backlog` skill -- when an SLO is breached, an entry lands here
> as the runbook anchor and in `findings-backlog.md` as the bug.*

**Status: PROPOSED.** No monitoring infrastructure ships with v2.x. These
SLOs describe the observable contract once a downstream consumer
(operator's own observability stack, CI perf job, log aggregator) is in
place. The opt-in telemetry shipped in S03E13 *The Telemetry* is the
primitive the charter is written against; the targets, budgets, and alert
thresholds below are intentionally proposed-not-enforced.

The episode that lands a real alert pipeline is owned by Frank Costanza +
Jerry (CI). Until that episode ships, the document is descriptive: it
states what *should* be true and how an operator would observe it, not
what the binary itself enforces.

## 1. Scope

The compat-dispatch hot path -- everything between the start of
`Program.RunAsync` chat dispatch and the return of the streaming loop --
plus the failure paths in the surrounding `try/catch`. Image generation,
agent / Ralph / persona modes, the wizard, and the prompt cache are out of
scope for v1 of this charter; each gets its own SLO row when the surface
calls for it.

## 2. SLI catalog (compat dispatch)

Each SLI is computed off the structured telemetry events emitted to
stderr by `TelemetryEmitter` when `AZ_AI_TELEMETRY=1`.

| SLI                  | Computation                                                          | Notes                                               |
|----------------------|----------------------------------------------------------------------|-----------------------------------------------------|
| dispatch.success     | events with `outcome == "success"` divided by total events           | The headline availability number                    |
| dispatch.latency.p95 | p95 of `latency_ms_bucket` -- bucket midpoint approximation          | Bucketed on emission; aggregator chooses summary    |
| dispatch.error_rate  | events with `outcome in {client_error, server_error, unknown_error}` | Cancelled is a separate bucket -- not user error    |
| dispatch.cancel_rate | events with `outcome == "cancelled"`                                 | Distinct from errors; user-driven or timeout        |

Bucket midpoints (for p95 reconstruction): bucket "10" -> 5ms, "50" ->
30ms, "100" -> 75ms, "250" -> 175ms, "500" -> 375ms, "1000" -> 750ms,
"2500" -> 1750ms, "5000" -> 3750ms, "10000" -> 7500ms, "+inf" -> 30000ms.
These are approximations -- the bucket is the source of truth, not the
midpoint.

## 3. Proposed SLOs

The following targets are PROPOSED. They are calibrated against Bania's
S03E12 *The Receipt* bench numbers (FakeChatClient + BenchmarkHarness)
and the live-API smoke patterns -- not against operator-collected
production telemetry, because there is no production telemetry yet.

| SLO                            | Target            | Window  | Error budget      | Status   |
|--------------------------------|-------------------|---------|-------------------|----------|
| dispatch.success               | >= 99.0%          | 28 days | 1.0% (~6.7 hours) | PROPOSED |
| dispatch.latency.p95 (azure)   | <= 1000ms bucket  | 28 days | -- (perf, not SLO budgetary) | PROPOSED |
| dispatch.latency.p95 (compat)  | <= 2500ms bucket  | 28 days | -- (perf, not SLO budgetary) | PROPOSED |
| binary startup latency p95     | <= 10ms           | 28 days | per Bania harness | PROPOSED |

Notes:

- The latency targets are bucket-level, not millisecond-level, by
  design. A bucket boundary is the contract; a millisecond is noise.
- The compat-path target is looser than the Azure target because the
  network leg routes through whichever upstream the preset names
  (openai / groq / together / cloudflare) and varies by region.
- Startup latency is observed by Bania's harness in CI, not by
  `TelemetryEmitter`. Both surfaces must agree before a startup-latency
  regression blocks a PR.

## 4. Error-budget policy

Until the alert pipeline lands:

1. The 28-day window is observational. No automated freeze.
2. When an episode introduces a feature that breaches a PROPOSED target
   on the bench harness or a manual smoke run, the episode's exec-report
   names it explicitly under "Lessons from this episode".
3. Once the alert pipeline ships (future episode -- candidate S03E14+
   Frank-led), the policy hardens: a breached budget pauses feature
   work in the affected surface until the surface is back inside its
   target.

## 5. Alert thresholds (proposed)

For the future alert pipeline. Not wired today.

- **page** -- dispatch.success drops below 95% over a rolling 1-hour
  window. This is the "endpoint is down" signal.
- **ticket** -- dispatch.success drops below 99% over a rolling 24-hour
  window. This is the "burn rate is faster than the budget allows"
  signal.
- **info** -- p95 latency bucket worsens by two buckets vs the prior
  7-day median. This is a regression notice, not a page.

The two-bucket-step rule is deliberately coarse so a single anomalous
500-event run does not page anyone at 3am. Frank's on-call rule:
"the number has to mean something or the page is just shouting".

## 6. Privacy charter

The opt-in telemetry surface (`AZ_AI_TELEMETRY=1`) is the only
observability primitive this episode ships. Privacy posture, in plain
English:

### 6.1 What is emitted

The full schema is defined by
`AzureOpenAI_CLI.Observability.TelemetryEvent`. Eight fields, each of
fixed and bounded type. One JSON line per dispatch, written to stderr:

| Field             | Type   | Notes                                                                          |
|-------------------|--------|--------------------------------------------------------------------------------|
| event_id          | string | GUID, freshly generated per emission                                           |
| ts                | string | ISO-8601 UTC, millisecond precision                                            |
| model             | string | Alias as the user typed it -- already on the allowlist before this point      |
| provider          | string | One of: azure / foundry / openai / groq / together / cloudflare / unknown      |
| dispatch_path     | string | One of: azure-default / foundry-allowlist / compat-allowlist / unknown         |
| latency_ms_bucket | string | One of: 10 / 50 / 100 / 250 / 500 / 1000 / 2500 / 5000 / 10000 / +inf          |
| outcome           | string | One of: success / client_error / server_error / cancelled / unknown_error      |
| error_class       | string | nullable; on non-success, redacted (SecretRedactor) and truncated at 200 chars |

### 6.2 What is NEVER emitted

There is no parameter on `TelemetryEvent` for any of the following.
The privacy guarantee is enforced by the compiler, not by reviewer
vigilance:

- prompt content (user text, system prompt, persona prompt, history)
- completion content (model output, streaming chunks, schema fills)
- token counts (input, output, cached, reasoning)
- API keys, bearer tokens, api-key headers, x-api-key headers
- endpoint URLs, hostnames, account IDs
- file paths, environment-file contents, working directory
- exception stack traces
- user names, machine names, OS identifiers, IP addresses

`error_class` is the one field that carries text from upstream. It is
run through `SecretRedactor.Redact()` (the same scrubber that protects
every log path) and truncated at 200 characters. Even so, the
recommendation is: if you do not want strings on stderr, leave the env
var unset.

### 6.3 Opt-in posture

- Default is OFF. Unset env var = OFF.
- Only the literal string `"1"` enables. Not `"true"`, not `"yes"`,
  not `"TRUE"`, not `"1 "` with trailing whitespace. One alias is one
  alias too many for a privacy surface.
- No persistence. The emitter never writes a file, never opens a
  socket, never spawns a child. It writes to stderr. What the
  operator's tooling does with stderr is the operator's concern.
- Reversible. Unset the env var; nothing is emitted on the next call.
  There is no flush queue, no buffered batch, no retry.

### 6.4 Retention guidance

The CLI does not retain telemetry. There is no on-disk buffer, no log
rotation, no upload. If you pipe stderr to a file or an aggregator,
that file or aggregator is now the system of record and you own its
retention. Recommendation: if you turn telemetry on, also turn on a
retention policy that matches your privacy expectations. Do not log
forever by default.

### 6.5 Audit

A user who wants to see exactly what would be sent before turning
telemetry on:

```bash
AZ_AI_TELEMETRY=1 az-ai "test" 2>telemetry.ndjson
cat telemetry.ndjson | jq .
```

One line per dispatch. The schema is the same eight fields above; if
a future episode adds a field, this charter and the Bania CI bench job
both need to update before the field ships.

## 7. Upstream-pricing review cadence (S03E13 pin)

Companion to `azureopenai-cli/Observability/CompatCostRates.cs`, which
ships PLACEHOLDER per-preset rates. Cost reporting that leans on those
numbers is only as honest as the refresh cadence behind the table.

**Cadence:** quarterly (Q1 / Q2 / Q3 / Q4 calendar quarters,
first business week).

**Owners:** Morty Seinfeld (FinOps lead) + Frank Costanza
(observability second). Morty drives the comparison; Frank validates
the diff and opens the finding if the table moves.

**Per-preset checklist:**

1. Open the canonical pricing URL for each preset:

   - openai     -- https://openai.com/api/pricing
   - groq       -- https://groq.com/pricing
   - together   -- https://www.together.ai/pricing
   - cloudflare -- https://developers.cloudflare.com/workers-ai/platform/pricing/

2. Identify the median anchor model named in `CompatCostRates.cs`
   (each entry's TODO comment points at the anchor).

3. Diff the listed input / output per-1M-token rate against the
   in-source per-1K rate (multiply by 1000).

4. If the absolute delta on either input or output exceeds **10%**,
   open a `findings-backlog` entry tagged `costrates-<YYYY-Qn>` and
   open a follow-up PR that updates the rates and bumps the comment
   block. Owner of the PR: Morty.

5. If the delta is under 10% on every preset, log the date of the
   review in this file under "Review log" below. No PR required.

6. If a preset's anchor model is sunset or replaced upstream, that is
   a finding regardless of the delta -- the table's claim ("median
   anchor") is wrong until the comment block is fixed.

### Review log

| Quarter | Date       | Outcome                                  | Reviewer |
|---------|------------|------------------------------------------|----------|
| 2026-Q2 | 2026-05-09 | Initial pin (S03E13 *The Telemetry*)     | Frank    |

## 8. Cross-references

- `azureopenai-cli/Observability/TelemetryEmitter.cs` -- the emitter
- `azureopenai-cli/Observability/CompatCostRates.cs` -- placeholder
  preset rates the cadence guards
- `azureopenai-cli/SecretRedactor.cs` -- the redactor that runs on
  `error_class`
- `tests/AzureOpenAI_CLI.Tests/TelemetryEmitterTests.cs` -- the
  schema, IsEnabled, bucket, and redaction tests
- `tests/AzureOpenAI_CLI.Tests/Benchmarks/BenchmarkHarness.cs` --
  Bania's S03E12 *The Receipt* harness; the latency-bucket
  boundaries above were chosen to align with the harness vocabulary
- `docs/exec-reports/s03e12-the-receipt.md` -- the bench-harness
  episode this charter builds on top of
- `docs/exec-reports/s03e13-the-telemetry.md` -- the episode that
  lands the emitter and this charter
- `docs/findings-backlog.md` -- where review-cadence findings get
  filed

## 9. Frank's standing rules (the "no" list)

1. **No default-on telemetry.** If the user did not say yes, we do not
   collect.
2. **No content fields.** Prompts and completions never appear on a
   telemetry surface, even with redaction. Redaction is a defense, not
   a license.
3. **No third-party sinks shipped in the CLI.** Stderr is the contract.
   Operators wire the rest.
4. **No SLO without an SLI.** A target with no measurement is a slogan.
5. **Error budgets are real budgets.** When the budget is blown,
   feature work in the affected surface pauses. Once the alert
   pipeline ships, this is enforced by the on-call rotation, not the
   honor system.
