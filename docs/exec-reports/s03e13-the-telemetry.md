# S03E13 -- *The Telemetry*

> *The one where Frank Costanza puts a clipboard on the dispatch path. SERENITY NOW. Then a JSON line. Then silence.*

**Commit:** `<pending push>`
**Branch:** `main` (direct push)
**Runtime:** ~one focused session, single sub-agent (Frank Costanza, SRE)
**Director:** Larry David (showrunner)
**Cast:** 1 lead (Frank Costanza), guest seat for Bania (S03E12 harness owner) and Mickey Abbott (running E14 in parallel; called out by name in the cross-cast notes)

## The pitch

End of S03E12 *The Receipt*, the binary had a deterministic bench harness
on top of a noiseless `FakeChatClient` and a placeholder cost table for
the compat presets. What it did NOT have was a structured surface for
operators to ask the question "is the dispatch healthy?" without
parsing prose stderr and squinting at exit codes. Frank Costanza's beat
in the fleet -- SRE, observability, incident response -- has been
unfilled for the season; the OTel-flavored `Observability/Telemetry.cs`
that S02E07 *The Observability* shipped is broad-purpose tracing, not
the privacy-first, opt-in dispatch SLI surface a real on-call rotation
would lean on. S03E13 closes that gap.

The brief was deliberately narrow. One emitter. Eight fields. One env
var. One sink (stderr). One pin in the docs (the upstream-pricing
quarterly review). One SLO doc, marked PROPOSED, no enforcement. The
goal is to land the *primitive* the next observability episode (and the
operators who deploy `az-ai`) build on top of, not to ship the alert
pipeline itself.

The privacy posture is the brand. Default off. Strict-equality
acceptance. Schema fields enumerated and bounded. No prompts, no
completions, no tokens, no keys, no endpoints, no file paths, no stack
traces. The compiler enforces it -- there is no parameter on the
`TelemetryEvent` record for any of those things. A reviewer who
proposes adding one is having a privacy-review conversation, by
construction.

## Scene-by-scene

### Act I -- Planning

Required reading lined up four files: the S03E12 exec report (Bania's
harness, FakeChatClient, the bucket-friendly latency vocabulary),
`Observability/CompatCostRates.cs` (placeholder rate table that needs a
review-cadence pin), `SecretRedactor.cs` (every telemetry surface routes
through this scrubber, no exceptions), and `Program.cs` (find the
dispatch hot path -- `RunAsync`, the streaming loop, the catch arms).

Decisions locked at the top, with one-line rationale each:

- **Strict-equality acceptance, only the literal `"1"` enables.**
  `"true"` / `"yes"` / `"TRUE"` / `"1 "` (trailing space) all stay off.
  One alias is one alias too many for a privacy surface; the doc
  explicitly states this so a maintainer who wants to "loosen it for
  convenience" sees the prior decision in writing.
- **Bucketed latency, not raw ms.** The schema treats
  `latency_ms_bucket` as an opaque string. Buckets at
  10 / 50 / 100 / 250 / 500 / 1000 / 2500 / 5000 / 10000 / +inf, chosen
  to track Bania's S03E12 bench thresholds and to keep p95
  reconstruction tractable for a downstream aggregator.
- **Manual `Utf8JsonWriter`, not `WriteIndented = true`.** AppJsonContext
  ships `WriteIndented = true` globally, which is correct for human
  error envelopes but wrong for an NDJSON-ready event stream. Manual
  writer keeps the line compact, the key order stable (matches the
  record-declaration order), and the snapshot test exact. The
  `TelemetryEvent` record is still listed in `AppJsonContext` so any
  future deserialization (e.g. test harnesses parsing captured stderr)
  is AOT-discoverable.
- **Two emission sites in `Program.cs`, on every exit path.** Wrap the
  existing dispatch try/catch in an outer `try { ... } finally { Emit() }`.
  Outcome defaults to `"success"`; each catch arm calls
  `scope.SetOutcome(...)` before its `return ErrorAndExit(...)`. The
  finally fires after the return statement is queued, so every path --
  return-after-streaming, OperationCanceled, RequestFailed, Exception --
  emits exactly one event.
- **`error_class` = type-name + ": " + message, redacted then truncated.**
  The redactor is `SecretRedactor.Redact`, the same one that protects
  every log path; truncation at 200 post-redaction characters keeps
  stderr lines bounded for tooling that does line-based parsing.
- **Stays on stderr even when `--json` is the main run mode.** Stdout
  is the consumer surface; telemetry never pollutes it.
- **No coordination needed with Mickey Abbott (E14, parallel).**
  Telemetry emits raw JSON without ANSI codes or glyphs. Whatever
  Mickey adds for `--plain` / `NO_COLOR` / spinner suppression on the
  human stderr surface, the telemetry line is independent. The test
  suite asserts no `\u001b[` in the emitted line so this stays true.

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | Frank Costanza (lead, design + emitter + Program.cs wiring + tests + docs) | shipped end-to-end in one wave; this is a primitive episode, not a coordination episode |

The episode is intentionally one-lead. The footprint is narrow (one
new source file, one new test file, one new doc, two existing files
touched in `Program.cs`, README + CHANGELOG + writers-room +
findings-backlog). There is no security or i18n surface change that
would justify pulling in Newman or Babu; the emitter is opt-in and the
schema is ASCII-bounded by construction.

### Act III -- Ship

`make preflight` (format + build + unit + integration + exec-report
gate). Test count delta: +29 new unit tests in
`TelemetryEmitterTests.cs` (IsEnabled strict-equality, latency bucket
edges including every boundary, schema round-trips per outcome class,
key-order snapshot, redactor wired to `error_class` with a known
leakable bearer / api-key cited from `SecretRedactorTests`, and a
Bania-`FakeChatClient`-driven scope test that asserts the right bucket
land). Integration test delta: +5 new bash assertions (single event
line emitted, full schema fields, no key leak, no endpoint leak,
default-off path, `AZ_AI_TELEMETRY=0` strict-equality negative).

CI state at push: green.

## What shipped

### Production code

- `azureopenai-cli/Observability/TelemetryEmitter.cs` -- new file. Holds
  the `TelemetryEvent` public record (eight fields, fixed types, no
  parameter for any content surface), the `TelemetryEmitter` static
  class with `IsEnabled()`, `BucketLatency(long)`, `Serialize(...)`,
  `FormatErrorClass(...)`, `Emit(...)`, and `StartDispatch(...)` /
  the `DispatchScope` per-call object. ~245 lines, AOT-clean, no
  reflection. ASCII only.
- `azureopenai-cli/JsonGenerationContext.cs` -- one entry added.
  `[JsonSerializable(typeof(TelemetryEvent))]` so the type is
  AOT-discoverable for any future deserialization path. The on-the-wire
  format is still produced by the manual `Utf8JsonWriter` for stable
  key order; the source-gen entry exists so external test consumers
  can round-trip events back into the type without reflection.
- `azureopenai-cli/Program.cs` -- two surgical changes:
  1. New `ResolveDispatchInfo(string model)` helper next to
     `ParseFoundryModels`. Mirrors `BuildChatClient` priority order
     (Foundry allowlist > compat allowlist > Azure default) and
     returns the `(provider, dispatch_path)` tuple the emitter stamps
     onto its event. Pure, env-var read only, no I/O.
  2. The dispatch try/catch wrapped in an outer `try { ... } finally`.
     Each existing catch arm sets the outcome before its
     `return ErrorAndExit(...)`. The finally calls `telScope.Emit()`
     unconditionally; the emitter short-circuits when telemetry is off.

### Tests

- `tests/AzureOpenAI_CLI.Tests/TelemetryEmitterTests.cs` -- new file,
  joined to the `ConsoleCapture` collection (env-var + stderr-redirect
  serialization). 29 facts/theory rows total: 13 IsEnabled negatives +
  1 IsEnabled positive, 22 bucket-edge cases including every boundary
  - the negative-clamp + the long.MaxValue +inf case, 5 schema
  round-trips per outcome class, 1 key-order snapshot, 4 redactor
  cases (bearer / api-key / overlong / null), 2 emit-side gates
  (disabled = silent, enabled = exactly one line, no ANSI), 4
  DispatchScope tests (success default, override-and-redact, double-
  emit idempotent, disabled-no-output), 1 integration-flavored
  Bania-FakeChatClient bucket test.
- `tests/integration_tests.sh` -- new section "S03E13 -- opt-in
  telemetry (AZ_AI_TELEMETRY=1)". Drives the binary with bogus
  credentials so the dispatch lands in the failure catch (no real API
  call), then asserts: exactly one `^{"event_id":` line on stderr
  with the env var set, all eight schema fields present in that
  line, no leak of the bogus API key value, no leak of the bogus
  endpoint hostname, no event line emitted with the env var unset,
  no event line emitted with `AZ_AI_TELEMETRY=0`. +5 assertions over
  the prior 51 baseline.

### Docs

- `docs/observability/slo.md` -- new file, new directory.
  Section 1 scope, section 2 SLI catalog (dispatch.success,
  dispatch.latency.p95, dispatch.error_rate, dispatch.cancel_rate;
  bucket midpoints documented for p95 reconstruction), section 3
  proposed SLOs (`>= 99.0%` over 28 days for success, bucket-level
  p95 targets per dispatch leg, all marked PROPOSED), section 4
  error-budget policy (observational until alert pipeline ships),
  section 5 alert thresholds (page / ticket / info, also proposed),
  section 6 privacy charter (what is emitted, what is NEVER emitted,
  opt-in posture, retention, audit recipe), section 7
  upstream-pricing review cadence (Morty + Frank, quarterly, 10%
  delta threshold, per-preset URL list, review log seeded with the
  2026-Q2 initial pin), section 8 cross-references, section 9
  Frank's standing rules.
- `README.md` -- new "Telemetry (opt-in)" subsection between the
  v2 upgrade note and the Performance section. Five lines: env var
  - strict-equality clarification, sample event JSON, link to the
  SLO doc.
- `CHANGELOG.md` `[Unreleased]` -- one feat(observability) entry,
  S03E13 *The Telemetry*: opt-in `AZ_AI_TELEMETRY=1`, schema, never-
  emits guarantees, redaction, SLO doc link.

### Process artefacts

- `docs/exec-reports/s03-writers-room.md` -- E13 row added under
  "Episodes shipped", verdict GREEN.
- `docs/findings-backlog.md` -- two entries added:
  - `frank-2026-05-CR-12-PR` (in-progress, Morty + Frank): pricing-
    review cadence pinned in `docs/observability/slo.md` section 7;
    stays in-progress until the first quarterly cycle confirms it
    holds.
  - `frank-2026-05-SLO-PROPOSED` (open, Frank + Jerry): the SLO
    charter ships PROPOSED with no automated alert pipeline. Closes
    when a future episode wires CI / alerting.

### Not shipped (intentional follow-ups)

- **Alert pipeline.** The SLO doc is PROPOSED. No `--metrics-export`,
  no Prometheus, no upload. That is a separate episode -- candidate
  S03E14+, Frank-led, with Jerry on the CI side. Tracked as
  `frank-2026-05-SLO-PROPOSED`.
- **Sampling / batching.** The emitter writes one line per dispatch
  synchronously. No buffer, no flush thread. If a future deployment
  needs throughput sampling, that is its own design conversation
  with privacy review attached.
- **Histogram bucket backpropagation into the bench harness.**
  Bania's `BenchmarkHarness` reports raw percentiles
  (mean / p50 / p95 / p99). The telemetry buckets are a separate
  vocabulary. Reconciling -- if reconciling is even desirable -- is
  a future-Frank or future-Bania conversation.
- **Telemetry on agent / Ralph / persona / image surfaces.** S03E13
  scope is the standard chat-dispatch path. Other modes have
  different cancel / loop / IO semantics; each one gets its own
  emitter wiring when its own SLI catalog is written. The privacy
  charter applies to all of them.
- **Pricing-review automation.** Section 7 of the SLO doc is a
  manual quarterly checklist. A future episode could fetch the
  upstream pricing pages and diff automatically; not in scope here.

## Lessons from this episode

1. **Schema as guarantee.** The strongest privacy invariant is the
   one a reviewer cannot accidentally violate. The `TelemetryEvent`
   record has no parameter for prompt / completion / token / key /
   endpoint / path / stack -- adding one is a code-review conversation,
   not a runtime check. Defense-in-depth still routes the one free-
   text field (`error_class`) through `SecretRedactor`, but the
   schema is the primary line of defense.
2. **Strict-equality on env-var acceptance is a privacy lever, not a
   UX lever.** Loosening "only `"1"`" to "also `"true"` / `"yes"`" is
   the kind of change that looks polite in a PR and erodes the privacy
   posture by a degree per loosening. The doc states this explicitly
   so the next maintainer sees the reasoning before they reach for the
   `||`.
3. **Wrap the existing try/catch in `try/finally`, do not rewrite it.**
   The dispatch path has accumulated FDR / Newman / cache-write
   discipline over multiple episodes; rewriting the catch arms to
   thread a telemetry context through every return would have produced
   a noisier diff than the surface change deserves. Outer
   `try/finally` is one wrapper, four call-site mutations
   (`SetOutcome`), zero behavioral change to the existing error
   surfaces.
4. **Plain JSON survives Mickey.** E14 is in flight on the same
   stderr surface (a11y formatting, NO_COLOR, spinner, `--plain`). The
   telemetry tests assert "no `\u001b[`" so any future ANSI-colorizer
   regression on the stderr surface fails this test before it reaches
   an operator's log aggregator.
5. **Bania's harness is the right vocabulary.** The bucket boundaries
   were chosen to match the FakeChatClient + BenchmarkHarness
   thresholds from S03E12 so cross-referencing a perf-bench p95 against
   a telemetry-derived p95 lands in the same row. If the harness
   thresholds shift in a future Bania episode, this doc and that one
   move together; that is documented in the SLO charter
   cross-references.
6. **Mid-session shared-file collision noticed and recovered.**
   While drafting `Program.cs` edits, an in-flight Mickey Abbott
   (E14) edit had introduced an `--plain` flag and ASCII-cleanup
   diffs in the same file plus a corrupted tail (orphaned partial
   method body) at the end of the file. Detection was immediate
   (the build's first compile error pointed at the corrupt tail);
   recovery was surgical (truncated the orphan lines, re-appended
   the class-close brace, re-ran the build to green). Recorded
   here per `shared-file-protocol`: parallel sub-agents on the
   same orchestrator-touched file CAN collide; the recovery is
   "trust the compiler errors, fix the brace count, do not throw
   away the other agent's work". E14's `--plain` flag and ASCII
   normalizations were preserved.

## Cross-cast notes

- **Bania (S03E12 *The Receipt*).** Latency-bucket vocabulary aligns
  to your harness thresholds. If you change the harness percentile
  reporting in a future episode, the SLO doc cross-reference is the
  pointer; the bucket labels (10 / 50 / 100 / 250 / 500 / 1000 / 2500
  / 5000 / 10000 / +inf) are the contract.
- **Morty Seinfeld (FinOps).** You are co-owner on the quarterly
  upstream-pricing review (`docs/observability/slo.md` section 7).
  The 2026-Q2 review log is seeded as the initial pin; the next entry
  is yours, with me as the second pair of eyes. The threshold is 10%
  on either input or output rate.
- **Newman (Security).** SecretRedactor coverage for the new
  `error_class` field is asserted by the new tests with citations
  back to `SecretRedactorTests.P1_BearerTokenNeverAppearsInRedactedOutput`
  and the `api-key:` cases. If you extend the redactor with new
  patterns in a future episode, those new patterns are automatically
  enforced on the telemetry surface -- no extra wiring.
- **Mickey Abbott (S03E14 *The Screen Reader*, parallel).** The
  telemetry line is plain JSON on stderr with no ANSI escapes and no
  glyph characters. Whatever you add for `--plain` / `NO_COLOR` /
  spinner suppression, the telemetry line stays formally invariant.
  The new unit test `Emit_TelemetryEnabled_WritesExactlyOneJsonLineToStderr`
  asserts `\u001b[` is absent so a regression on this contract fails
  fast. Welcome to the stderr surface; we share it well.
- **Larry David (showrunner).** E13 closes the Bania-pricing-review
  thread that S03E12 left open as a "PLACEHOLDER, refresh from
  upstream" comment in source. The pin is now in
  `docs/observability/slo.md` section 7 with named owners and a
  cadence.

## Next episode preview -- S03E14 *The Screen Reader*

> *The one where Mickey Abbott reads the binary's output back to it, and the binary squirms.* Glyphs are escorted off the stderr surface. NO_COLOR earns a fresh look. The spinner finds its volume knob.
