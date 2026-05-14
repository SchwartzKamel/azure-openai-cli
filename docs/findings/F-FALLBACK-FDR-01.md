# F-FALLBACK-FDR-01: RetryEnvelope WARN does not scrub the model name before stderr

**Filed by:** FDR (Franklin Delano Romanowski)
**Date:** 2026-06-04
**Severity:** LOW (defense-in-depth)
**Episode:** S04E07 The Fallback, Wave 3 adversarial review
**Subject:** `azureopenai-cli/Resilience/RetryEnvelope.cs` (`EmitWarn`,
lines 479-490) and the matching test in
`tests/AzureOpenAI_CLI.Tests/FallbackChainChaosTests.cs`
(`WarnLine_AsciiOnly_NoAnsi_ContainsModelAttemptsOutcome`, lines 442-471).

## What I observed

`EmitWarn` concatenates `_model` straight into the WARN line:

```csharp
_warn(
    "[WARN] fallback: model=" + _model
    + " attempts=" + attempts.ToString(CultureInfo.InvariantCulture)
    + " outcome=" + outcome
    + " last_error=" + errLabel
    + " elapsed_ms=" + elapsedMs.ToString(CultureInfo.InvariantCulture));
```

There is no `ScrubForDisplay` (the helper introduced in F-S04E04-04 to
neutralise `ModelRegistry` stderr paths) on the model-name interpolation.
The chaos-suite test that "verifies" ASCII-only WARN output (lines 442-471)
uses a hard-coded clean model name (`"gpt-test"`) and a single 503-then-success
path. It establishes that the **format string** is ASCII, not that the
**inputs** are scrubbed.

## Why the current code is *probably* safe

The registry path (`ModelRegistry.ValidateEntries`, lines 188-235) rejects
any model name containing `'`, `"`, `\`, C0 (`<= 0x1F`), or C1 (`0x7F-0x9F`)
bytes with `Environment.Exit(99)` at load time. So any model name that
reaches `RetryEnvelope` **via the registry** is already control-byte-free,
and the WARN line is safe by upstream invariant.

## Why it still warrants a fix

The invariant is implicit, not enforced at the emission site. Three
realistic ways it could break:

1. A future code path constructs an `IChatClient` for a model that did **not**
   come through `ModelRegistry.ValidateEntries` (e.g. a `--model` value
   piped past validation, a synthetic fallback name like
   `"legacy:" + rawArg`, or a test fixture).
2. The `_model` field is set from `ChatOptions.ModelId` higher up the
   stack (search `_model =` in `RetryEnvelope.cs`); a streaming response
   that mutates `options.ModelId` mid-flight (some MAF middleware does)
   could carry attacker-controlled bytes.
3. The registry rejection list is **shell-hostile**, not **terminal-hostile**.
   It blocks quotes and backslashes but it does **not** block, for example,
   `0x08` BACKSPACE within the `<= 0x1F` range -- actually that one *is*
   blocked. It also blocks 0x7F-0x9F. So the current registry filter is
   tighter than I first thought. But the point stands: the WARN call site
   trusts an upstream filter it cannot see.

Defense-in-depth says the emission site that writes to stderr should not
trust the loader.

## Suggested fix (not patched in this wave -- W3 is docs-only)

Mirror the F-S04E04-04 pattern: introduce a private `ScrubForDisplay`
helper in `RetryEnvelope.cs` (or, better, lift the existing one to a shared
location) and wrap the `_model` interpolation in `EmitWarn`. Also extend
`WarnLine_AsciiOnly_NoAnsi_ContainsModelAttemptsOutcome` (or add a sibling
test) to feed a model name containing `\x1B[31m` (ANSI red) and assert the
emitted WARN line contains `?` substitutes, not the raw escape.

Estimated effort: ~10 lines of code, ~15 lines of test, no design discussion
required. Good first issue for a Wave-4 polish pass or for Kramer to fold
into S04E08.

## Status

Open. Linked from ADR-015 adversarial appendix (scenario 6).
