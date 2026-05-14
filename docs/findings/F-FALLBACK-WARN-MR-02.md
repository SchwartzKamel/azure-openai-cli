# F-FALLBACK-WARN-MR-02: Model name flows into WARN line unsanitised

**Filed by:** Mickey Abbott
**Date:** 2026-06-05
**Severity:** LOW (NIT, defense-in-depth)
**Episode:** S04E07 The Fallback, Wave 3 review
**Subject:** azureopenai-cli/Resilience/RetryEnvelope.cs line 485 (`"... model=" + _model`)

## What I observed

```csharp
// azureopenai-cli/Resilience/RetryEnvelope.cs:484-489
_warn(
    "[WARN] fallback: model=" + _model
    + " attempts=" + attempts.ToString(CultureInfo.InvariantCulture)
    + " outcome=" + outcome
    + " last_error=" + errLabel
    + " elapsed_ms=" + elapsedMs.ToString(CultureInfo.InvariantCulture));
```

`_model` is interpolated into the stderr WARN line with no filtering. The
value is plumbed in by the caller (Program.cs, post-resolver) and
ultimately sourced from `AZUREOPENAIMODEL` -- an operator-set env var.

The test at
`tests/AzureOpenAI_CLI.Tests/FallbackChainChaosTests.cs:443-471`
(`WarnLine_AsciiOnly_NoAnsi_ContainsModelAttemptsOutcome`) asserts the
emitted line is ASCII-only and contains no ESC bytes. **But the test
constructs the envelope with the literal model name `"gpt-test"`.** It
proves the emitter does not *add* ANSI; it does not prove the emitter
*strips* ANSI when the input model name contains it.

## a11y impact

### Screen-reader pronunciation

If `_model` ever contained C0 control bytes (BEL 0x07, BS 0x08, ESC 0x1B,
CSI sequences), screen readers behave inconsistently: some skip silently,
some announce *"control B"*, some -- on the BEL byte -- play a system
sound, which is genuinely jarring for blind operators using terminal
speech. Same class of problem terminal multiplexers had to fix in the
2010s.

### ANSI / control-byte scrubbing

In the **current** threat model this is operator-self-attack only --
`AZUREOPENAIMODEL` is set by the same human running the CLI. So the real
risk is **accidental** rather than adversarial: a model name copy-pasted
from a portal with a trailing soft-hyphen (U+00AD), a stray CR (0x0D), or
a zero-width space (U+200B). The ASCII test in `FallbackChainChaosTests`
will not catch a stray ZWSP in a real `AZUREOPENAIMODEL` value because
that test never exercises a model name with one.

### NO_COLOR / locale

N/A for this finding.

## Recommendation

Add a small `ScrubForDisplay` helper (or reuse one if it already exists in
the codebase under another name) and apply it to `_model` in `EmitWarn`:

```csharp
// Replace any byte < 0x20 (except tab) and DEL (0x7F) with '?'. Strip
// known invisible Unicode (soft hyphen, ZWSP, ZWNJ, ZWJ, BOM).
private static string ScrubForDisplay(string s) { ... }

_warn(
    "[WARN] fallback: model=" + ScrubForDisplay(_model)
    + ...);
```

And -- this is the test gap -- add a parameterised Puddy test that
constructs the envelope with hostile model names (`"gpt-\x1B[31mred"`,
`"gpt-\u200Btest"`, `"gpt-\x07test"`) and asserts the emitted WARN line
is still strictly `[0x20..0x7E] | \t`. That test would have caught any
future regression where someone "helpfully" switches the emission to
`Console.Error.WriteLine` formatted with interpolated user state.

Defense in depth -- the same instinct that gave us
`ReadFileTool`'s NFKC path normalisation. Cheap, harmless, future-proof.

## Verdict

**DEFER-TO-NEXT-EPISODE.** Not a W1 blocker. File on the findings
backlog as a NIT for whoever picks up the WARN format work at W4 (per
sibling finding F-FALLBACK-WARN-MR-01). The current threat model does
not make this exploitable; the recommendation is purely about robustness
to accidental garbage in env vars.

## References

* `azureopenai-cli/Resilience/RetryEnvelope.cs:484-489` -- the
  unsanitised interpolation.
* `tests/AzureOpenAI_CLI.Tests/FallbackChainChaosTests.cs:443-471` --
  ASCII / no-ANSI test that only covers literal `"gpt-test"`.
* `azureopenai-cli/Tools/ReadFileTool.cs` -- precedent for NFKC
  normalisation of operator-supplied strings.
* Sibling finding: `F-FALLBACK-WARN-MR-01` (format consolidation).
