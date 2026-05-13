using System.Reflection;
using AzureOpenAI_CLI;
using AzureOpenAI_CLI.Capabilities;
using AzureOpenAI_CLI.Registry;
using Xunit;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// S04E03 Wave 2 (Mickey Abbott): accessibility regression suite for the
/// capability-gate rejection message that Bookman's <c>CapabilityRejection</c>
/// builder produces and <c>CapabilityGate.Check</c> returns. Wave 1 (Bookman)
/// tested the builder in isolation; this file drives the gate end-to-end with
/// a synthetic <see cref="Program.CliOptions"/> against a synthetic
/// no-tool-calls registry entry, captures the returned rejection string, and
/// asserts the a11y contract the user sees.
///
/// Contracts locked here (the user-visible string that reaches Espanso popups,
/// screen-reader output, AHK tooltips, pipe-to-grep workflows, and CI logs):
///
///   1. Zero ANSI escape bytes anywhere in the message. (NO_COLOR is honored
///      by construction: there are no escapes to suppress.)
///   2. Zero TAB characters. Screen readers announce TAB as "tab"; spaces
///      reflow predictably across braille / 80-col / piped surfaces.
///   3. Zero carriage returns. Single-line invariant.
///   4. Every byte is printable ASCII (0x20..0x7E). The rejection vocabulary
///      is intentionally English-only per ADR-013 / Bookman's contract.
///   5. The "prefix" portion (everything before the suggestion tail) is
///      &lt;= 240 chars regardless of suggestion-list length.
///   6. The message begins with the literal token <c>model '</c> so
///      grep / awk / shell pipelines have a deterministic anchor.
///   7. Quotability for shell pipelines: with a well-formed registry the
///      message is safe to wrap in single quotes (no bare apostrophes
///      inside the quoted region break the shell). A hostile registry
///      override carrying <c>'</c> in the model name would leak through
///      Bookman's Scrub (apostrophe is 0x27, inside the printable-ASCII
///      whitelist) -- documented as A11Y-CG-01 in the review doc.
///
/// Important pitfall (S04E02 Puddy paid for this one): never use
/// <c>Assert.DoesNotContain("\u001B", str, ...)</c> to check for ANSI
/// escapes. The "needle" is a single C0 byte that some xUnit/runtime
/// combinations render as an empty string, which then "matches" everywhere
/// and false-passes. Use <c>str.IndexOf('\u001B') &lt; 0</c> or
/// <c>str.All(c =&gt; c != '\u001B')</c> instead -- those are byte-level
/// comparisons with no rendering in the loop.
/// </summary>
[Collection("ConsoleCapture")]
public sealed class CapabilityGateAccessibilityTests
{
    // -----------------------------------------------------------------
    // Test harness.
    //
    // CapabilityGate.Check reads Program.RegistryEntries (a static cache)
    // and ModelRegistry.ModelsWithCapability (which also reads the same
    // cache). We override that cache for the duration of the test with
    // a synthetic entry whose Capabilities array intentionally OMITS the
    // capability under test, restore the prior cache in a finally, and
    // assert against the returned rejection string. Mirrors the seam
    // pattern in DoctorRegistryAccessibilityTests (S04E02 Mickey).
    // -----------------------------------------------------------------
    private static string InvokeGate(
        string resolvedModel,
        string? toolsValue,
        ModelRegistryEntry[] entries)
    {
        var entriesProp = typeof(Program).GetProperty(
            "RegistryEntries",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var prev = entriesProp.GetValue(null);
        try
        {
            entriesProp.SetValue(null, entries);

            var opts = BuildOptions(tools: toolsValue);
            var result = CapabilityGate.Check(resolvedModel, opts, allowlist: null);
            Assert.NotNull(result);
            return result!;
        }
        finally
        {
            entriesProp.SetValue(null, prev);
        }
    }

    private static Program.CliOptions BuildOptions(string? tools)
    {
        // Mirrors Program.DefaultOptions(); private, so we duplicate the
        // safe-defaults seed verbatim for the few fields the gate reads.
        return new Program.CliOptions(
            Model: null,
            Temperature: 0.55f,
            MaxTokens: 10000,
            TimeoutSeconds: 120,
            SystemPrompt: "You are a secure, concise CLI assistant. Keep answers factual, no fluff.",
            Raw: false,
            ShowHelp: false,
            ShowVersion: false,
            AgentMode: false,
            Tools: tools,
            SquadInit: false,
            Persona: null,
            ListPersonas: false,
            RalphMode: false,
            ValidateCommand: null,
            TaskFile: null,
            MaxIterations: 10,
            Prompt: null,
            ParseError: false,
            EnableOtel: false,
            EnableMetrics: false,
            EnableTelemetry: false,
            Estimate: false,
            EstimateOutputMax: null,
            Json: false,
            VersionShort: false,
            Schema: null,
            MaxRounds: Program.DEFAULT_MAX_AGENT_ROUNDS,
            ConfigPath: null,
            CompletionsShell: null,
            ListModels: false,
            CurrentModel: false,
            SetModelSpec: null,
            ConfigSubcommand: null,
            ConfigKey: null,
            ConfigValue: null,
            Prewarm: false,
            CacheEnabled: false,
            CacheTtlHours: 24,
            ParseErrorExitCode: 1,
            UnknownFlag: null,
            Setup: false,
            ImageMode: false,
            OutputPath: null,
            ImageSize: null,
            ConfirmPrintSecret: false,
            Plain: false,
            Offline: false,
            Provider: null,
            Profile: null);
    }

    // A "no tool_calls" registry: gpt-5.4-nano is stripped of tool_calls;
    // gpt-4o-mini and llama-local keep it (so the suggestion tail is
    // non-empty and deterministic).
    private static ModelRegistryEntry[] NanoLacksToolCalls() =>
    [
        new ModelRegistryEntry(
            Name: "gpt-5.4-nano",
            Provider: "azure",
            Capabilities: new[] { "json_mode", "streaming", "system_prompt" },
            ContextWindow: 128000,
            CostTier: "low",
            CardPath: null),
        new ModelRegistryEntry(
            Name: "gpt-4o-mini",
            Provider: "azure",
            Capabilities: new[] { "tool_calls", "json_mode", "streaming", "system_prompt" },
            ContextWindow: 128000,
            CostTier: "low",
            CardPath: null),
        new ModelRegistryEntry(
            Name: "llama-local",
            Provider: "local",
            Capabilities: new[] { "tool_calls", "streaming" },
            ContextWindow: 8192,
            CostTier: "unknown",
            CardPath: null),
    ];

    // -----------------------------------------------------------------
    // Assertion 1 -- No ANSI escape bytes (NO_COLOR is honored by
    // construction). Byte-level check; see class-comment pitfall note.
    // -----------------------------------------------------------------
    [Fact]
    public void Rejection_ContainsZeroAnsiEscapeBytes()
    {
        var msg = InvokeGate("gpt-5.4-nano", toolsValue: "shell", entries: NanoLacksToolCalls());

        // Byte-level: avoid Assert.DoesNotContain("\u001B", ...) -- a single
        // C0 needle can render as empty and false-pass. IndexOf is a raw
        // char comparison with no rendering involved.
        Assert.True(
            msg.IndexOf('\u001B') < 0,
            "rejection message must contain zero ESC bytes (NO_COLOR by construction); "
            + "found at index " + msg.IndexOf('\u001B') + " in: " + msg);
    }

    // -----------------------------------------------------------------
    // Assertion 2 -- No tab characters. Spaces only.
    // -----------------------------------------------------------------
    [Fact]
    public void Rejection_ContainsZeroTabCharacters()
    {
        var msg = InvokeGate("gpt-5.4-nano", toolsValue: "shell", entries: NanoLacksToolCalls());

        Assert.True(
            msg.IndexOf('\t') < 0,
            "rejection message must contain zero TAB chars; found at index "
            + msg.IndexOf('\t') + " in: " + msg);
    }

    // -----------------------------------------------------------------
    // Assertion 3 -- No carriage returns. Single-line invariant.
    // -----------------------------------------------------------------
    [Fact]
    public void Rejection_ContainsZeroCarriageReturnsAndNewlines()
    {
        var msg = InvokeGate("gpt-5.4-nano", toolsValue: "shell", entries: NanoLacksToolCalls());

        Assert.True(msg.IndexOf('\r') < 0, "rejection must be single-line (no CR)");
        Assert.True(msg.IndexOf('\n') < 0, "rejection must be single-line (no LF)");
    }

    // -----------------------------------------------------------------
    // Assertion 4 -- ASCII printable only (0x20..0x7E). The rejection
    // vocabulary is English-only by Bookman's contract; non-ASCII bytes
    // would either be hostile (CSI smuggling) or a typo. Either way: out.
    // -----------------------------------------------------------------
    [Fact]
    public void Rejection_IsAsciiPrintableOnly()
    {
        var msg = InvokeGate("gpt-5.4-nano", toolsValue: "shell", entries: NanoLacksToolCalls());

        for (var i = 0; i < msg.Length; i++)
        {
            var ch = msg[i];
            Assert.True(
                ch >= 0x20 && ch <= 0x7E,
                "rejection char at index " + i + " is 0x" + ((int)ch).ToString("X4")
                + " (outside printable ASCII): " + msg);
        }
    }

    // -----------------------------------------------------------------
    // Assertion 5 -- Prefix portion (up to and including the period that
    // closes "(required by --{flag}).") is <= 240 chars regardless of
    // how many suggestions follow. Screen-reader users who hear the
    // suggestion list as a comma sequence still get the actionable
    // diagnosis in the first breath.
    // -----------------------------------------------------------------
    [Fact]
    public void Rejection_PrefixBeforeSuggestionTail_IsAtMost240Chars()
    {
        var msg = InvokeGate("gpt-5.4-nano", toolsValue: "shell", entries: NanoLacksToolCalls());

        // Boundary marker is ").", per Bookman's format spec.
        var period = msg.IndexOf(").", System.StringComparison.Ordinal);
        Assert.True(period > 0, "expected '(required by --flag).' marker in: " + msg);
        var prefixLen = period + 2; // include ')' and '.'

        Assert.True(
            prefixLen <= 240,
            "prefix was " + prefixLen + " chars, ceiling is 240; full msg: " + msg);
    }

    // -----------------------------------------------------------------
    // Assertion 6 -- Deterministic prefix anchor "model '" so awk/grep
    // pipelines have a stable column-0 token.
    // -----------------------------------------------------------------
    [Fact]
    public void Rejection_StartsWithModelQuoteAnchor()
    {
        var msg = InvokeGate("gpt-5.4-nano", toolsValue: "shell", entries: NanoLacksToolCalls());

        Assert.StartsWith("model '", msg, System.StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------
    // Assertion 7 -- Pipe-to-grep contract: with a well-formed registry
    // (no apostrophe in model name) the rejection is safe to wrap in
    // single quotes for shell. We assert that the model token between
    // the FIRST pair of single quotes contains no embedded apostrophe.
    //
    // NOTE: a hostile registry override carrying <c>'</c> inside the
    // model name would slip through Bookman's Scrub (0x27 is inside
    // the printable-ASCII whitelist). Filed as A11Y-CG-01 in
    // docs/model-cards/REVIEW-capability-rejection.md. This test pins
    // the BENIGN case so a future regression that *adds* apostrophe-
    // escaping does not silently change the format here.
    // -----------------------------------------------------------------
    [Fact]
    public void Rejection_ModelToken_IsSingleQuoteSafe_ForBenignNames()
    {
        var msg = InvokeGate("gpt-5.4-nano", toolsValue: "shell", entries: NanoLacksToolCalls());

        var first = msg.IndexOf('\'');
        Assert.True(first >= 0, "expected opening single quote around model name");
        var second = msg.IndexOf('\'', first + 1);
        Assert.True(second > first, "expected closing single quote around model name");

        var modelToken = msg.Substring(first + 1, second - first - 1);
        Assert.Equal("gpt-5.4-nano", modelToken);
        Assert.True(
            modelToken.IndexOf('\'') < 0,
            "benign model name must not contain an apostrophe inside its quoted token");
    }
}
