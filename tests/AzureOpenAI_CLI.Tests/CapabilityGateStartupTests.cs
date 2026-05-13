// S04E03 -- The Capabilities (Puddy, Wave 2).
//
// Integration-style tests for the S04E03 startup gate
// (AzureOpenAI_CLI.Capabilities.CapabilityGate.Check). This file is
// disjoint from CapabilityRejectionTests.cs (Bookman owns the builder's
// unit tests) and from CapabilityGateTests.cs which is the legacy S03E18
// CapabilityDescriptor/ProviderCapabilities suite -- different gate,
// same word.
//
// 12 facts from the brief + 2 bonus (determinism, ASCII-only). Either it
// works or it doesn't.
//
// Test seam:
//   CapabilityGate.Check and ModelRegistry.ModelsWithCapability both read
//   Program.RegistryEntries (a static internal property with a private
//   setter). RegistryScope reflects the property and swaps in a synthetic
//   ModelRegistryEntry[] for the duration of the scope, then restores the
//   prior value. Same pattern DoctorRegistryAccessibilityTests already
//   uses -- no new test infra.
//
// What we do NOT do:
//   - mutate the embedded registry.json
//   - write ~/.config/az-ai/registry.json (other concurrent xunit workers
//     would step on us; reflection seam is hermetic)
//   - touch CapabilityGate.cs, CapabilityRejection.cs, ModelRegistry.cs,
//     or Program.cs (Wave 1's surface, out of scope for Wave 2)

using System.Reflection;
using AzureOpenAI_CLI;
using AzureOpenAI_CLI.Capabilities;
using AzureOpenAI_CLI.Registry;

namespace AzureOpenAI_CLI.Tests.CapabilityGateStartup;

[Collection("ConsoleCapture")]
public class CapabilityGateStartupTests
{
    // ── Test infrastructure ────────────────────────────────────────────────

    // Mirrors Program.DEFAULT_SYSTEM_PROMPT (private const). The gate's
    // DefaultSystemPromptSentinel is a private mirror of the same literal,
    // so this third copy lives here intentionally: when any of the three
    // drifts, this test breaks loudly. (Brief acceptance: "regression
    // test that pins the pair" -- CapabilityGate.cs:54-56.)
    private const string DefaultSystemPromptMirror =
        "You are a secure, concise CLI assistant. Keep answers factual, no fluff.";

    private static readonly PropertyInfo RegistryEntriesProp =
        typeof(Program).GetProperty(
            "RegistryEntries",
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException(
            "Program.RegistryEntries property not found via reflection.");

    /// <summary>
    /// Swaps <see cref="Program.RegistryEntries"/> for the supplied
    /// synthetic set for the lifetime of the scope; restores on dispose.
    /// </summary>
    private sealed class RegistryScope : IDisposable
    {
        private readonly object? _previous;

        public RegistryScope(params ModelRegistryEntry[] entries)
        {
            _previous = RegistryEntriesProp.GetValue(null);
            RegistryEntriesProp.SetValue(null, entries);
        }

        public void Dispose() => RegistryEntriesProp.SetValue(null, _previous);
    }

    private static ModelRegistryEntry Entry(string name, params string[] caps) =>
        new(
            Name: name,
            Provider: "azure",
            Capabilities: caps,
            ContextWindow: 8192,
            CostTier: "unknown",
            CardPath: null);

    // Builds a CliOptions populated with the same defaults DefaultOptions()
    // would produce, with only the named overrides flipped. Kept as a
    // local helper rather than reflecting Program.DefaultOptions so a
    // future field addition to CliOptions breaks compilation here loudly
    // (instead of silently passing whatever reflection returns).
    private static Program.CliOptions Opts(
        string? tools = null,
        bool agentMode = false,
        bool ralphMode = false,
        string? schema = null,
        string systemPrompt = DefaultSystemPromptMirror)
        => new(
            Model: null,
            Temperature: 0.7f,
            MaxTokens: 1024,
            TimeoutSeconds: 60,
            SystemPrompt: systemPrompt,
            Raw: false,
            ShowHelp: false,
            ShowVersion: false,
            AgentMode: agentMode,
            Tools: tools,
            SquadInit: false,
            Persona: null,
            ListPersonas: false,
            RalphMode: ralphMode,
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
            Schema: schema,
            MaxRounds: 5,
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

    private static ISet<string> Allowlist(params string[] names) =>
        new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);

    // ── Test 1 -- happy path ───────────────────────────────────────────────

    [Fact]
    public void Check_RegisteredModelAllCapsNoFlags_ReturnsNull()
    {
        using var _ = new RegistryScope(
            Entry("happy-model", "tool_calls", "json_mode", "streaming", "system_prompt"));

        var result = CapabilityGate.Check("happy-model", Opts(), allowlist: null);

        Assert.Null(result);
    }

    // ── Test 2 -- --tools on model without tool_calls ─────────────────────

    [Fact]
    public void Check_ToolsFlagOnModelWithoutToolCalls_ReturnsRejection()
    {
        using var _ = new RegistryScope(
            Entry("plain-model", "streaming"),
            Entry("tool-model", "tool_calls"));

        var result = CapabilityGate.Check(
            "plain-model",
            Opts(tools: "shell_exec"),
            allowlist: null);

        Assert.NotNull(result);
        Assert.Contains("plain-model", result);
        Assert.Contains("tool_calls", result);
        Assert.Contains("--tools", result);
        Assert.Contains("tool-model", result);
    }

    // ── Test 2b -- agent mode is equivalent to --tools for the gate ───────

    [Fact]
    public void Check_AgentModeOnModelWithoutToolCalls_ReturnsRejection()
    {
        using var _ = new RegistryScope(
            Entry("plain-model", "streaming"),
            Entry("tool-model", "tool_calls"));

        var result = CapabilityGate.Check(
            "plain-model",
            Opts(agentMode: true),
            allowlist: null);

        Assert.NotNull(result);
        Assert.Contains("tool_calls", result);
        Assert.Contains("--tools", result);
    }

    // ── Test 3 -- --tools on a model that supports tool_calls ─────────────

    [Fact]
    public void Check_ToolsFlagOnModelWithToolCalls_ReturnsNull()
    {
        using var _ = new RegistryScope(
            Entry("tool-model", "tool_calls", "json_mode", "streaming", "system_prompt"));

        var result = CapabilityGate.Check(
            "tool-model",
            Opts(tools: "shell_exec"),
            allowlist: null);

        Assert.Null(result);
    }

    // ── Test 4 -- --schema on a model without json_mode ───────────────────

    [Fact]
    public void Check_SchemaFlagOnModelWithoutJsonMode_ReturnsRejection()
    {
        using var _ = new RegistryScope(
            Entry("notjson-model", "tool_calls", "streaming"),
            Entry("json-model", "json_mode"));

        var result = CapabilityGate.Check(
            "notjson-model",
            Opts(schema: "{\"type\":\"object\"}"),
            allowlist: null);

        Assert.NotNull(result);
        Assert.Contains("json_mode", result);
        Assert.Contains("--schema", result);
        Assert.Contains("json-model", result);
    }

    // ── Test 5 -- --schema on a model with json_mode ──────────────────────

    [Fact]
    public void Check_SchemaFlagOnModelWithJsonMode_ReturnsNull()
    {
        using var _ = new RegistryScope(
            Entry("json-model", "json_mode", "streaming", "system_prompt"));

        var result = CapabilityGate.Check(
            "json-model",
            Opts(schema: "{\"type\":\"object\"}"),
            allowlist: null);

        Assert.Null(result);
    }

    // ── Test 6 -- first-miss-wins ordering ────────────────────────────────
    //
    // Iteration order from CapabilityGate.cs:97-108 is
    //   tool_calls -> json_mode -> streaming -> system_prompt
    // When a model lacks both tool_calls AND json_mode and the user passes
    // BOTH --tools AND --schema, the rejection must name tool_calls (the
    // earlier slot in the table), not json_mode.
    [Fact]
    public void Check_BothToolsAndSchemaOnModelMissingBoth_ReturnsToolCallsRejectionFirst()
    {
        using var _ = new RegistryScope(
            Entry("bare-model", "streaming"),
            Entry("tool-model", "tool_calls"),
            Entry("json-model", "json_mode"));

        var result = CapabilityGate.Check(
            "bare-model",
            Opts(tools: "shell_exec", schema: "{\"type\":\"object\"}"),
            allowlist: null);

        Assert.NotNull(result);
        Assert.Contains("tool_calls", result);
        Assert.Contains("--tools", result);
        // json_mode/--schema must NOT surface -- first miss wins.
        Assert.DoesNotContain("json_mode", result);
        Assert.DoesNotContain("--schema", result);
    }

    // ── Test 7 -- unregistered model passes through ───────────────────────

    [Fact]
    public void Check_UnregisteredModelWithAnyFlag_ReturnsNull()
    {
        using var _ = new RegistryScope(
            Entry("known-model", "tool_calls", "json_mode"));

        // "mystery-model" is not in the registry at all -- decision 1
        // (CapabilityGate.cs:20-24): pass-through. Even with every gate-
        // tripping flag set, the gate must stay out of the way.
        var result = CapabilityGate.Check(
            "mystery-model",
            Opts(tools: "shell_exec",
                 schema: "{\"type\":\"object\"}",
                 agentMode: true,
                 ralphMode: true,
                 systemPrompt: "custom"),
            allowlist: null);

        Assert.Null(result);
    }

    // ── Test 8 -- suggestion intersection with allowlist ──────────────────

    [Fact]
    public void Check_SuggestionsFilteredByAllowlistIntersection_ReturnsOnlyAllowedNames()
    {
        // 5 models with tool_calls in the registry, but the operator's
        // AZUREOPENAIMODEL allowlist only carries 2 of them.
        using var _ = new RegistryScope(
            Entry("bare-model", "streaming"),
            Entry("tc-1", "tool_calls"),
            Entry("tc-2", "tool_calls"),
            Entry("tc-3", "tool_calls"),
            Entry("tc-4", "tool_calls"),
            Entry("tc-5", "tool_calls"));

        var result = CapabilityGate.Check(
            "bare-model",
            Opts(tools: "shell_exec"),
            allowlist: Allowlist("tc-2", "tc-4"));

        Assert.NotNull(result);
        Assert.Contains("tc-2", result);
        Assert.Contains("tc-4", result);
        Assert.DoesNotContain("tc-1", result);
        Assert.DoesNotContain("tc-3", result);
        Assert.DoesNotContain("tc-5", result);
    }

    // ── Test 9 -- empty intersection -> no-suggestions tail ───────────────

    [Fact]
    public void Check_EmptyAllowlistIntersection_ReturnsNoConfiguredModelTail()
    {
        using var _ = new RegistryScope(
            Entry("bare-model", "streaming"),
            Entry("tc-1", "tool_calls"),
            Entry("tc-2", "tool_calls"));

        // The allowlist exists but contains no model that carries
        // tool_calls. CapabilityRejection.Build emits the
        // "no configured model supports this; see --doctor" tail.
        var result = CapabilityGate.Check(
            "bare-model",
            Opts(tools: "shell_exec"),
            allowlist: Allowlist("bare-model", "some-other-model"));

        Assert.NotNull(result);
        Assert.Contains("no configured model supports this; see --doctor", result);
        Assert.DoesNotContain("Try:", result);
    }

    // ── Test 10 -- gate fires regardless of how the model was resolved ────
    //
    // The brief calls this the "persona-pinned" case. From the gate's
    // viewpoint the model name is just a string; the gate runs at
    // Program.cs:751-753, downstream of the persona-pin re-check at
    // Program.cs:739. We exercise the same code path by passing a
    // registered, capability-missing model name as if a persona pin had
    // resolved to it -- the gate must still return non-null.
    [Fact]
    public void Check_RegisteredModelMissingCapability_GateFiresRegardlessOfResolutionSource()
    {
        using var _ = new RegistryScope(
            Entry("persona-pinned", "streaming"),
            Entry("tool-model", "tool_calls"));

        var result = CapabilityGate.Check(
            "persona-pinned",
            Opts(agentMode: true),
            allowlist: null);

        Assert.NotNull(result);
        Assert.Contains("persona-pinned", result);
        Assert.Contains("tool_calls", result);
    }

    // ── Test 11 -- default-system-prompt sentinel is NOT "set" ────────────

    [Fact]
    public void Check_DefaultSystemPromptSentinel_NotConsideredSet_ReturnsNull()
    {
        // Model has NO system_prompt capability. If the gate naively
        // treated any non-empty SystemPrompt as "user-supplied", every
        // single invocation would trip the gate because Program.cs
        // pre-fills SystemPrompt with DEFAULT_SYSTEM_PROMPT. The
        // DefaultSystemPromptSentinel decision (CapabilityGate.cs:50-56)
        // exempts the canonical literal.
        using var _ = new RegistryScope(
            Entry("no-sysprompt", "tool_calls", "json_mode", "streaming"));

        var result = CapabilityGate.Check(
            "no-sysprompt",
            Opts(systemPrompt: DefaultSystemPromptMirror),
            allowlist: null);

        Assert.Null(result);
    }

    // ── Test 12 -- non-default system prompt on no-sysprompt model ────────

    [Fact]
    public void Check_NonDefaultSystemPromptOnModelWithoutCapability_ReturnsRejection()
    {
        // Model carries tool_calls, json_mode, streaming -- everything
        // EXCEPT system_prompt. With a non-sentinel system prompt the
        // gate must reach the 4th row in the mapping table.
        using var _ = new RegistryScope(
            Entry("no-sysprompt", "tool_calls", "json_mode", "streaming"),
            Entry("sp-model", "system_prompt"));

        var result = CapabilityGate.Check(
            "no-sysprompt",
            Opts(systemPrompt: "You are a custom assistant. Be verbose."),
            allowlist: null);

        Assert.NotNull(result);
        Assert.Contains("system_prompt", result);
        Assert.Contains("--system-prompt", result);
        Assert.Contains("sp-model", result);
    }

    // ── Test 12b -- empty SystemPrompt is also "not set" ──────────────────
    //
    // Negative branch coverage for the IsNullOrEmpty short-circuit in
    // CapabilityGate.cs:106. An empty string must not trigger the
    // system_prompt check (no user intent expressed).
    [Fact]
    public void Check_EmptySystemPromptOnModelWithoutCapability_ReturnsNull()
    {
        using var _ = new RegistryScope(
            Entry("no-sysprompt", "tool_calls", "json_mode", "streaming"));

        var result = CapabilityGate.Check(
            "no-sysprompt",
            Opts(systemPrompt: ""),
            allowlist: null);

        Assert.Null(result);
    }

    // ── First-miss-wins parameterized matrix ──────────────────────────────
    //
    // Brief asks for a [Theory] documenting the mapping-table iteration
    // order. Cases: model has no capabilities at all; user requests every
    // possible pair of flags. Expected: the earliest-listed capability
    // among the requested ones surfaces in the rejection.
    [Theory]
    [InlineData(true, true, "tool_calls")]    // tools+schema  -> tool_calls
    [InlineData(true, false, "tool_calls")]    // tools only    -> tool_calls
    [InlineData(false, true, "json_mode")]     // schema only   -> json_mode
    public void Check_FirstMissWins_ReturnsEarliestCapability(
        bool requestTools,
        bool requestSchema,
        string expectedCapability)
    {
        using var _ = new RegistryScope(
            Entry("nada", /* no capabilities */ Array.Empty<string>()),
            Entry("tc-x", "tool_calls"),
            Entry("json-x", "json_mode"));

        var result = CapabilityGate.Check(
            "nada",
            Opts(
                tools: requestTools ? "shell_exec" : null,
                schema: requestSchema ? "{\"type\":\"object\"}" : null),
            allowlist: null);

        Assert.NotNull(result);
        Assert.Contains(expectedCapability, result);
    }

    // ── Bonus -- determinism ──────────────────────────────────────────────

    [Fact]
    public void Check_SameInputs_ReturnsIdenticalOutput_100x()
    {
        using var _ = new RegistryScope(
            Entry("bare", "streaming"),
            Entry("tc-1", "tool_calls"),
            Entry("tc-2", "tool_calls"),
            Entry("tc-3", "tool_calls"));

        var allow = Allowlist("tc-1", "tc-3");
        var first = CapabilityGate.Check("bare", Opts(tools: "shell_exec"), allow);
        Assert.NotNull(first);

        for (var i = 0; i < 100; i++)
        {
            var next = CapabilityGate.Check("bare", Opts(tools: "shell_exec"), allow);
            Assert.Equal(first, next);
        }
    }

    // ── Bonus -- ASCII invariant on rejection output ──────────────────────

    private static bool IsAscii(string s)
    {
        foreach (var ch in s)
        {
            if (ch > 0x7E || ch < 0x20) return false;
        }
        return true;
    }

    [Fact]
    public void Check_RejectionMessage_IsAsciiOnly()
    {
        using var _ = new RegistryScope(
            Entry("bare", "streaming"),
            Entry("tc-1", "tool_calls"));

        var result = CapabilityGate.Check("bare", Opts(tools: "shell_exec"), allowlist: null);

        Assert.NotNull(result);
        Assert.True(
            IsAscii(result!),
            "Rejection message must be ASCII-only (printable 0x20-0x7E). Got: " + result);
    }
}
