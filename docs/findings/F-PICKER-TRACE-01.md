# F-PICKER-TRACE-01: Resolver TRACE emission uses simple env-gate, not TelemetryEmitter

**Filed by:** Frank Costanza
**Date:** 2026-05-13
**Severity:** LOW
**Episode:** S04E05 The Picker, Wave 2 review
**Subject:** azureopenai-cli/Resolution/ResolveSmartDefault.cs (emission site
in `azureopenai-cli/Program.cs` lines 499-506)

## What I observed

The Wave 1 TRACE emission is wired into `Program.cs` immediately after the
`ResolveSmartDefault.Pick(...)` call:

```csharp
if (string.Equals(Environment.GetEnvironmentVariable("AZ_AI_TRACE"), "1", StringComparison.Ordinal))
{
    Console.Error.WriteLine(
        "[TRACE] resolver model=" + pickerResult.Model
        + " reason_code=" + pickerResult.ReasonCode
        + " allowlist_size=" + pickerAllowlist.Length
        + " human_reason=" + pickerResult.HumanReason);
}
```

The line is plaintext `key=value`, not NDJSON. It is gated by a separate env
var (`AZ_AI_TRACE`) that does not exist anywhere else in the codebase --
`grep -rn "AZ_AI_TRACE" azureopenai-cli/` returns the one site above. It does
not go through `AzureOpenAI_CLI.Observability.TelemetryEmitter`, and it does
not honor `AZ_AI_TELEMETRY` (the existing, documented telemetry gate, see
`Observability/TelemetryEmitter.cs` lines 57-83 and `docs/observability/slo.md`).

## What the brief asked for

`docs/episode-briefs/s04e05-the-picker.md` AC #13 (lines 134-137):

> TRACE log line is emitted via the existing telemetry surface (Frank's
> `TelemetryEmitter`) with structured fields: `model`, `reason_code`,
> `human_reason`, `allowlist_size`. Off by default; no behavior change for
> users who do not opt into telemetry.

Two binding requirements in one bullet:

1. *Via the existing telemetry surface* -- `TelemetryEmitter`, not a new env
   var and not raw `Console.Error.WriteLine`.
2. *Structured fields* -- the surrounding telemetry charter
   (TelemetryEmitter.cs lines 10-27) defines that as compact NDJSON with
   stable key order, not plaintext `key=value`.

Wave 1 satisfies the spirit of "off by default" but misses both the surface
requirement and the structured-output requirement.

## Verdict

**WAVE-3-FIX**

The brief's "may use simple fallback" allowance was a hedge against
`TelemetryEmitter`'s schema being too rigid to fit resolver semantics. That
turns out to be a real obstacle (the existing `TelemetryEvent` record has
no `reason_code` or `human_reason` field and per its docstring "adding any
such field requires a privacy review") but Wave 1 did not document that it
hit the obstacle, did not file an ADR or finding explaining the deviation,
and did not open the privacy review the charter requires. Silent fallback
is the failure mode this finding exists to flag.

Severity is LOW, not MEDIUM, because:

- The emission is off by default and no behavior changes for any user who
  does not set `AZ_AI_TRACE=1`.
- The fields emitted (`model`, `reason_code`, `allowlist_size`, the bounded
  ASCII `human_reason`) are non-sensitive by construction -- the resolver
  is pure and never sees prompts or completions.
- Fix-forward in Wave 3 is small and well-scoped (see Recommendation).

## Reliability implications

- **Observability through existing channels:** the TRACE line cannot be
  ingested by anything that subscribes to the `AZ_AI_TELEMETRY` NDJSON
  stream on stderr. Operators who turned telemetry on expecting one
  consistent format now have to parse two formats from the same stream,
  or set two env vars. That is exactly the fragmentation the
  `TelemetryEmitter` surface was built to prevent.
- **Off-by-default semantics:** holds. `string.Equals(..., "1", Ordinal)`
  matches the strict-equality rule in `TelemetryEmitter.IsEnabled()`
  (TelemetryEmitter.cs lines 78-82). Any value other than the literal
  `"1"` is treated as off, including `"true"`, `"yes"`, `"1 "`, unset.
  This part is fine.
- **Privacy / sensitive-field exposure:** acceptable for now. `model` is
  a deployment name (already surfaced in `--doctor` output and CLI errors);
  `reason_code` is one of four constants; `allowlist_size` is a small int;
  `human_reason` is ASCII-only and capped at 120 chars per AC #11. None of
  the four fields can carry user prompt content because the resolver does
  not see prompt content. So I am not blocking on a privacy review -- but
  the charter still says any new field on the telemetry surface needs one,
  and the right time to do that paperwork is Wave 3, before the surface
  gets reused by E09 (`--prefer`) and E11 (corpus).
- **Performance impact of always-on env check:** negligible. One
  `Environment.GetEnvironmentVariable` call per process invocation on a
  cold path that already does several. Not a concern.

## Recommendation

Wave 3 should add a `TelemetryEmitter.EmitResolverDecision(...)` static
method alongside the existing `EmitFallbackAttempt` / `EmitFallbackOutcome`
pattern (TelemetryEmitter.cs lines 274 and 292). Sketch:

1. Add a sibling record `ResolverDecisionEvent(string EventId, string Ts,
   string Model, string ReasonCode, string HumanReason, int AllowlistSize)`
   in `Observability/TelemetryEmitter.cs`. Source-generate its JsonContext
   entry in `JsonGenerationContext.cs` to keep AOT clean.
2. Gate the new method on the same `AZ_AI_TELEMETRY=1` strict-equality
   check via `IsEnabled()`. Retire `AZ_AI_TRACE` entirely -- one telemetry
   gate, one surface, one NDJSON wire format. Update `Program.cs` lines
   499-506 to call
   `TelemetryEmitter.EmitResolverDecision(pickerResult, pickerAllowlist.Length)`.
3. Update `docs/observability/slo.md` to declare the new event type and
   record the privacy review (the four fields are demonstrably non-PII;
   this is paperwork, not a redesign).
4. Add an emitter snapshot test under
   `tests/AzureOpenAI_CLI.Tests/TelemetryEmitterTests.cs` asserting the
   key order matches the wire format used by the existing
   `EmitFallbackOutcome` snapshot tests.

Estimated effort: 60-90 minutes of Maestro time, no AOT delta risk
(record + one method, both narrow). Owner: Frank Costanza lead, Maestro
implements. Block on this before E09 wires `--prefer` because E09 will
want to log the chosen axis through the same surface and we should not
ship a third env var.

If Wave 3 cannot fit this work, downgrade the verdict to ACCEPT-AS-IS but
file an ADR documenting the two-surface state and committing to
consolidation before S05.

## References

- `docs/episode-briefs/s04e05-the-picker.md` AC #13 (lines 134-137)
- `azureopenai-cli/Program.cs` lines 499-506 (the deviating emission)
- `azureopenai-cli/Observability/TelemetryEmitter.cs` lines 10-27 (charter),
  57-83 (gate semantics), 274-310 (sibling-method pattern)
- `docs/observability/slo.md` (telemetry charter)
- commit 0d7d303 (`feat(resolution): S04E05 W1 ResolveSmartDefault + reason codes`)

**Status:** CLOSED in 66e8cf8
