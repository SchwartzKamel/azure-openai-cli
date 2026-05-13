# A11Y review: capability-gate rejection message

**Episode:** S04E03 *The Capabilities* -- Wave 2
**Reviewer:** Mickey Abbott
**Surface:** the single-line rejection string built by
`AzureOpenAI_CLI.Cli.CapabilityRejection.Build` and returned by
`AzureOpenAI_CLI.Capabilities.CapabilityGate.Check`, routed to stderr via
`Program.ErrorAndExit(..., rc=2, ...)`.
**Status:** GREEN with one finding (A11Y-CG-01, low severity).
**Cross-link:** [ADR-013 Accessibility review section](../adr/ADR-013-capability-gate.md#accessibility-review-mickey-abbott-s04e03-wave-2)

## What the user sees

With suggestions:

```text
[ERROR] model 'gpt-5.4-nano' does not support tool_calls (required by --tools). Try: gpt-4o-mini, llama-local
```

Without suggestions (no configured model carries the capability):

```text
[ERROR] model 'gpt-5.4-nano' does not support tool_calls (required by --tools). no configured model supports this; see --doctor
```

The `[ERROR]` prefix is added by `Program.ErrorAndExit`; the rest is
Bookman's builder.

## Read-aloud transcript

Mentally simulated against JAWS / NVDA on Windows, VoiceOver on macOS, Orca on
Linux. Punctuation is announced at the verbose setting most a11y users keep:

> "Error. model, quote, g p t five point four dash nano, quote, does not
> support tool underscore calls, open paren, required by dash dash tools,
> close paren, period. Try colon, g p t four o dash mini, comma, llama dash
> local."

`--tools` is announced as "dash dash tools" on NVDA/Orca and "minus minus
tools" on JAWS at default. Either reading round-trips through `grep --tools`
correctly; both are acceptable.

The full line is ~96 characters with two suggestions. Comfortably inside an
80-column reflow when the trailing comma list wraps; the actionable
diagnosis ("model X does not support cap Y, required by flag Z") is in the
first breath before any wrap.

## NO_COLOR honored

Confirmed by `rg '\\u001[Bb]|\\x1[Bb]|\\e\\['
azureopenai-cli/Cli/CapabilityRejection.cs azureopenai-cli/Capabilities/CapabilityGate.cs`
(zero matches in either file). Bookman's `Scrub` whitelists `0x20..0x7E`,
which excludes `0x1B` (ESC) entirely, so any escape that *was* present in a
hostile registry-override card is collapsed to `?` before it can reach
stderr. `NO_COLOR` is honored *by construction*: there is no color to
suppress.

Regression guard:
`tests/AzureOpenAI_CLI.Tests/CapabilityGateAccessibilityTests.cs`
`Rejection_ContainsZeroAnsiEscapeBytes`.

## Colorblind-safe

N/A. No color is used in the rejection path. The `[ERROR]` prefix that
`ErrorAndExit` adds is plain ASCII; no red, no bold, no underline. State
("rejection") is conveyed by the `[ERROR]` token + non-zero exit code, never
by color alone.

## Keyboard-only workflow

N/A. The rejection is a single line of plain text written to stderr followed
by `rc=2`. There are no interactive elements, no prompts, no spinners. A
keyboard-only user, a screen-reader user, and a `2>&1 | tee` script all see
the identical stream.

## Pipe-to-other-tools

The shape is grep-friendly. Tested by inspection of
`CapabilityRejectionTests.Build_WithSuggestions_EmitsCommaJoinedTryTail`
output:

```bash
# Grab the suggestion list, comma-split:
az-ai --tools shell "..." 2>&1 \
  | grep -oE 'Try: .*$' \
  | sed 's/^Try: //; s/, /\n/g'
```

```bash
# Detect the "no configured model" case:
az-ai --tools shell "..." 2>&1 | grep -F 'no configured model supports this'
```

```bash
# Pull the rejected model name (column 3 with single-quote field separator):
az-ai --schema '{...}' "..." 2>&1 | awk -F"'" '/^\[ERROR\] model /{print $2}'
```

The `model '` prefix and `' does not support` infix are both stable
deterministic anchors covered by Wave 1 unit tests
(`Build_Contains_Model_Capability_And_DashDashFlag`) and Wave 2 a11y test
`Rejection_StartsWithModelQuoteAnchor`.

## Truncation behavior

Bookman's builder enforces a 240-char ceiling on the **prefix** (everything
up to and including `).` that closes `(required by --{flag}).`) and
**throws** `ArgumentException` if the inputs would overflow it. Suggestion
lists are *not* truncated: a registry with twenty tool-call-capable models
emits twenty comma-joined names, however long the line ends up.

For the gate's well-formed inputs (capability tags from
`ModelCapability.AllowedTags`, flag names from a four-element static table,
model names from the registry) the prefix lands at ~85 chars. The 240-char
budget exists to absorb future flag/capability expansions, not to handle
hostile inputs.

**A11Y-CG-02** (low, see Findings) -- the suggestion tail can grow
unboundedly with the registered model count. Screen-reader users hear the
entire comma list before the line ends. Mitigation deferred to S04E04
*Reading Room* (Elaine), tracked under the brief's "word-boundary
truncation" open question. Overlaps with FDR's F-EE-AR-09.

## Findings

### A11Y-CG-01 -- Bare apostrophe in model name escapes shell quoting

- **Severity:** low
- **State:** open, deferred to S04E04 / FDR Wave 3 follow-ups
- **Location:** `azureopenai-cli/Cli/CapabilityRejection.cs` `Scrub` method
  (lines 122-138)
- **Symptom:** Bookman's `Scrub` whitelists every printable ASCII byte
  `0x20..0x7E`, which **includes** the apostrophe `'` (`0x27`). A registry
  entry whose `name` field contains an apostrophe -- e.g. a hostile override
  card seeding `name: "ev'il-model"` -- emits a rejection of the form

  ```text
  [ERROR] model 'ev'il-model' does not support tool_calls (required by --tools). Try: ...
  ```

  Wrapping that string in single quotes for shell propagation (`grep "$msg"`,
  `notify-send "$msg"`, Espanso `replace: "$msg"`) breaks at the embedded
  `'`. The user-visible failure surfaces as a downstream shell parse error,
  not a capability diagnosis.
- **Affected users:** anyone piping `az-ai` stderr through a shell-quoted
  variable; Espanso/AHK users whose expansion engines re-parse the captured
  string; screen-reader users whose announce-text helper wraps in quotes.
- **Mitigations considered:**
  1. Reject `'` in the `Scrub` whitelist (replace with `?`).
  2. Backslash-escape `'` inside the model-token quotes.
  3. Switch the model-token wrapper from `'...'` to `"..."` (would conflict
     with the JSON-double-quote convention `ErrorAndExit` uses for `--json`
     mode).
  4. Validate the registry at load time to reject names containing `'`
     (cleanest; aligns with `ModelCapability.AllowedTags`-style closed-set
     discipline).
- **Recommendation:** option (4), tracked for FDR's adversarial review
  (ADR-013 Wave 3 Adversarial review section). Out of scope for Wave 2.
- **Regression guard today:**
  `Rejection_ModelToken_IsSingleQuoteSafe_ForBenignNames` pins the
  benign-case format so any future fix that *removes* apostrophes from the
  output will not regress this contract silently.

### A11Y-CG-02 -- Unbounded suggestion-list length

- **Severity:** low
- **State:** open, deferred to S04E04 *Reading Room* (Elaine)
- **Symptom:** the suggestion tail is comma-joined with no per-line cap. A
  registry with 20+ tool-call-capable models produces a single line wider
  than any reasonable terminal, which screen-readers announce as one
  uninterrupted comma sequence.
- **Recommendation:** word-boundary truncation at ~3-5 suggestions with a
  "; see --doctor for full list" tail. Already in S04E04's open-questions
  list; this finding cross-references it. Overlaps with FDR's F-EE-AR-09.

## Test coverage

7 new assertions in
`tests/AzureOpenAI_CLI.Tests/CapabilityGateAccessibilityTests.cs`:

1. `Rejection_ContainsZeroAnsiEscapeBytes`
2. `Rejection_ContainsZeroTabCharacters`
3. `Rejection_ContainsZeroCarriageReturnsAndNewlines`
4. `Rejection_IsAsciiPrintableOnly`
5. `Rejection_PrefixBeforeSuggestionTail_IsAtMost240Chars`
6. `Rejection_StartsWithModelQuoteAnchor`
7. `Rejection_ModelToken_IsSingleQuoteSafe_ForBenignNames`

All seven exercise `CapabilityGate.Check` end-to-end via a synthetic
`Program.RegistryEntries` cache (mirroring the seam in
`DoctorRegistryAccessibilityTests`), not the builder in isolation -- so a
future refactor that changes which layer assembles the string still gets
caught.
