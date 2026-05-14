# F-FALLBACK-WARN-MR-01: WARN format -- canonical example vs. W1 emission, and the screen-reader-hostile arrow

**Filed by:** Mickey Abbott
**Date:** 2026-06-05
**Severity:** MEDIUM (advisory; FIX-FORWARD recommendation for the outer chain, not a W1 blocker)
**Episode:** S04E07 The Fallback, Wave 3 review
**Subject:** azureopenai-cli/Resilience/RetryEnvelope.cs lines 479-490 (`EmitWarn`) vs. the brief's canonical example

## What I observed

The W1 emission in `RetryEnvelope.EmitWarn` produces a single line of the form:

```text
[WARN] fallback: model=gpt-5.4-mini attempts=2 outcome=ok last_error=429 elapsed_ms=2143
```

(constructed at `azureopenai-cli/Resilience/RetryEnvelope.cs:485-489`). The
S04E07 brief, however, gives the canonical example as:

```text
[WARN] fallback: gpt-5.4-mini (429 x2) -> gpt-4o-mini (ok) in 2143 ms
```

These are not the same line. Frank's open question #1 surfaces this. The two
formats are not equivalent in shape, in grep-ability, or in screen-reader
behaviour, and they disagree about which information matters.

I want to be explicit: I do **not** think Frank shipped the wrong line. The
brief's canonical example describes a **multi-hop** event (model A failed,
fell back to model B which succeeded). That is the **outer** `FallbackChain`
from S03E22. Frank's W1 envelope is the **inner** retry loop -- always one
model, never a hop to a different deployment. The brief conflates the two
layers into one example. The mismatch is a brief-vs-implementation seam, not
a Frank bug.

But it does leave us with a real a11y problem that lands at W4 when the
outer chain gets its WARN: **the arrow `->` in the canonical example is
screen-reader hostile.**

## a11y impact

### Screen-reader pronunciation

I tested both lines mentally against NVDA/Orca/VoiceOver defaults:

* `attempts=2 outcome=ok last_error=429 elapsed_ms=2143` (W1 actual):
  reads as *"attempts equals two, outcome equals okay, last underscore error
  equals four-two-nine, elapsed underscore m s equals two-one-four-three"*.
  Verbose, but **every token is pronounceable** and the equals signs
  reliably get read as "equals" (or skipped) by every major screen reader.
  An operator listening to this in a terminal-with-speech setup can act on
  it.
* `gpt-5.4-mini (429 x2) -> gpt-4o-mini (ok) in 2143 ms` (brief canonical):
  NVDA pronounces `->` as *"dash greater than"*. Orca often silently skips
  it. VoiceOver renders it as *"hyphen greater"*. None of them say
  *"arrow"* or *"then"*, which is the semantic the author wanted. A blind
  operator hearing *"gpt five four mini four twenty-nine x two dash
  greater than gpt four o mini"* has to mentally reconstruct the causal
  chain from punctuation noise. **This is the central a11y failure.**

The W1 line wins this comparison decisively. The proposed canonical line
loses.

### NO_COLOR semantics

`EmitWarn` writes a literal string with no ANSI sequences -- no SGR codes,
no CSI, no OSC. `NO_COLOR` is therefore N/A by construction (nothing to
suppress). The comment at line 483 asserts this; the test at
`tests/AzureOpenAI_CLI.Tests/FallbackChainChaosTests.cs:464`
(`Assert.DoesNotContain('\x1B', line)`) enforces it. **PASS.**

### ANSI / control-byte scrubbing

The `_model` field is interpolated directly into the WARN line with no
sanitisation. In the current threat model `AZUREOPENAIMODEL` is an
operator-set env var, so injection of ESC (0x1B) or other C0 control bytes
would require the operator to attack themselves. **Low risk.** But the
test at `FallbackChainChaosTests.cs:457-461` only proves ASCII-cleanliness
for the literal model name `"gpt-test"` -- it does not prove the emitter
**makes** the line ASCII-clean. See sibling finding
`F-FALLBACK-WARN-MR-02` for the scrubbing recommendation.

### Locale / Unicode

`attempts.ToString(CultureInfo.InvariantCulture)` and
`elapsedMs.ToString(CultureInfo.InvariantCulture)` are both explicit at
lines 486 and 489. Combined with `<InvariantGlobalization>true</...>` in
`azureopenai-cli/AzureOpenAI_CLI.csproj:15`, an elapsed time of 1234 ms
will render as `1234`, never as `1,234` (en-US) or `1.234` (de-DE).
**PASS.** Verified by reading; no per-locale test required.

## Recommendation

Two-part:

1. **W1 (this episode): ACCEPT Frank's format as-is.** It is a11y-correct
   for the inner envelope. The mismatch with the brief is the brief's
   problem, not the code's.

2. **W4 / next episode (when the S03E22 outer `FallbackChain` adds its
   own WARN for true multi-hop events): do NOT adopt the brief's `->`
   notation.** Extend Frank's `key=value` schema instead so multi-hop
   pronounces cleanly and remains grep-stable. Concrete sketch:

   ```text
   [WARN] fallback: chain=gpt-5.4-mini,gpt-4o-mini hops=2 attempts=3 outcome=ok last_error=429 elapsed_ms=2143
   ```

   * `chain=` is a comma-separated ordered list of deployments tried.
     Screen readers pronounce comma as a short pause -- *"chain equals
     gpt five four mini, gpt four o mini"* -- which preserves the
     ordering semantic the brief's arrow was trying to express.
   * `hops=N` makes the cardinality machine-readable.
   * `attempts=N` keeps W1's existing field meaning (total HTTP
     attempts across all hops).
   * Grep stays trivial: `grep '\[WARN\] fallback:'` keeps working.
   * No arrow, no parentheses-as-syntax, no `x2` shorthand that requires
     a screen reader to know it means "times two".

   This also degenerates correctly to the single-hop case: `hops=1
   chain=gpt-5.4-mini` is unambiguous and the W1 line is a strict subset
   of the W4 line (sans `chain=` and `hops=`), so log-aggregation regex
   written today will still parse W4 output.

## Verdict

**FIX-FORWARD-AT-W4** for the format-consolidation recommendation above
(the outer chain's WARN must not adopt the brief's arrow). **ACCEPT-AS-IS**
for Frank's W1 envelope emission -- it already meets every a11y bar I
care about.

## References

* `azureopenai-cli/Resilience/RetryEnvelope.cs:479-490` -- the emission
  site.
* `tests/AzureOpenAI_CLI.Tests/FallbackChainChaosTests.cs:442-471` --
  Puddy's ASCII / no-ANSI / required-token assertions.
* S04E07 brief, Wave 3 row for Mickey Abbott (the canonical example).
* `azureopenai-cli/AzureOpenAI_CLI.csproj:15` --
  `<InvariantGlobalization>true</...>` mitigates locale-aware integer
  formatting at the framework level.
* Sibling finding: `F-FALLBACK-WARN-MR-02` (model-name scrubbing).
