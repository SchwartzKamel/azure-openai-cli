using AzureOpenAI_CLI.Capabilities;
using Xunit;

namespace AzureOpenAI_CLI.Tests;

// S03E18 -- The Capability Gate.
//
// CapabilityGateTests covers the three layers of the gate:
//
//   1. Registry lookup (preset+model, preset default, fallback to Conservative)
//   2. Override env-var parsing + application
//   3. Mismatch exception shape and message contents
//
// Tests that touch process env / Console.Error are sequentialised under the
// "ConsoleCapture" collection per project convention. Helper scopes mirror
// the EnvScope / StderrScope pattern from TelemetryEmitterTests.

[Collection("ConsoleCapture")]
public class CapabilityGateTests
{
    private sealed class EnvScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _prev;
        public EnvScope(string name, string? value)
        {
            _name = name;
            _prev = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }
        public void Dispose() => Environment.SetEnvironmentVariable(_name, _prev);
    }

    // -- Conservative / Permissive factories ----------------------------------

    [Fact]
    public void Conservative_AllFlagsFalse_ContextNull()
    {
        var c = CapabilityDescriptor.Conservative();
        Assert.False(c.ToolCalls);
        Assert.False(c.Streaming);
        Assert.False(c.Vision);
        Assert.False(c.JsonMode);
        Assert.Null(c.MaxContextTokens);
    }

    [Fact]
    public void Permissive_AllFlagsTrue_ContextNull()
    {
        var p = CapabilityDescriptor.Permissive();
        Assert.True(p.ToolCalls);
        Assert.True(p.Streaming);
        Assert.True(p.Vision);
        Assert.True(p.JsonMode);
        Assert.Null(p.MaxContextTokens);
    }

    // -- Registry resolution --------------------------------------------------

    [Fact]
    public void Get_AzurePreset_AnyModel_AllFlagsTrue()
    {
        var caps = ProviderCapabilities.Get("azure", "any-deployment-name");
        Assert.True(caps.ToolCalls);
        Assert.True(caps.Streaming);
        Assert.True(caps.Vision);
        Assert.True(caps.JsonMode);
    }

    [Fact]
    public void Get_FoundryPreset_PermissiveLikeAzure()
    {
        var caps = ProviderCapabilities.Get("foundry", "Phi-4-mini-instruct");
        Assert.True(caps.ToolCalls);
        Assert.True(caps.Vision);
    }

    [Fact]
    public void Get_PresetIsCaseInsensitive()
    {
        var lower = ProviderCapabilities.Get("openai", "gpt-4o");
        var upper = ProviderCapabilities.Get("OPENAI", "gpt-4o");
        var mixed = ProviderCapabilities.Get("OpenAI", "gpt-4o");
        Assert.Equal(lower, upper);
        Assert.Equal(lower, mixed);
    }

    [Fact]
    public void Get_ModelIsCaseInsensitive()
    {
        var lower = ProviderCapabilities.Get("groq", "llama-3.1-70b-versatile");
        var upper = ProviderCapabilities.Get("groq", "LLAMA-3.1-70B-VERSATILE");
        Assert.Equal(lower.ToolCalls, upper.ToolCalls);
        Assert.True(upper.ToolCalls);
    }

    [Fact]
    public void Get_OpenAi_DefaultModel_HasToolsAndVision()
    {
        var caps = ProviderCapabilities.Get("openai", "gpt-4o-mini");
        Assert.True(caps.ToolCalls);
        Assert.True(caps.Vision);
        Assert.True(caps.JsonMode);
    }

    [Fact]
    public void Get_OpenAi_Gpt35Turbo_VisionFalse()
    {
        var caps = ProviderCapabilities.Get("openai", "gpt-3.5-turbo");
        Assert.True(caps.ToolCalls);
        Assert.False(caps.Vision);
    }

    [Fact]
    public void Get_OpenAi_O1Preview_StreamingFalse()
    {
        var caps = ProviderCapabilities.Get("openai", "o1-preview");
        Assert.False(caps.Streaming);
        Assert.False(caps.JsonMode);
    }

    [Fact]
    public void Get_OpenAi_O1Mini_NoToolsNoStreaming()
    {
        var caps = ProviderCapabilities.Get("openai", "o1-mini");
        Assert.False(caps.ToolCalls);
        Assert.False(caps.Streaming);
    }

    [Fact]
    public void Get_Groq_Llama70bVersatile_ToolsTrue()
    {
        var caps = ProviderCapabilities.Get("groq", "llama-3.1-70b-versatile");
        Assert.True(caps.ToolCalls);
        Assert.True(caps.Streaming);
        Assert.False(caps.Vision);
    }

    [Fact]
    public void Get_Groq_Llama33_70b_ToolsTrue()
    {
        var caps = ProviderCapabilities.Get("groq", "llama-3.3-70b-versatile");
        Assert.True(caps.ToolCalls);
    }

    [Fact]
    public void Get_Groq_8bInstant_ToolsFalse_FallsBackToPresetDefault()
    {
        var caps = ProviderCapabilities.Get("groq", "llama-3.1-8b-instant");
        Assert.False(caps.ToolCalls);
        Assert.True(caps.Streaming);
        Assert.False(caps.Vision);
    }

    [Fact]
    public void Get_Together_DefaultIsConservativeOnTools()
    {
        var caps = ProviderCapabilities.Get("together", "meta-llama-3.1-70b-instruct");
        Assert.False(caps.ToolCalls);
        Assert.True(caps.Streaming);
        Assert.False(caps.Vision);
        Assert.True(caps.JsonMode);
    }

    [Fact]
    public void Get_Cloudflare_OnlyStreaming()
    {
        var caps = ProviderCapabilities.Get("cloudflare", "@cf/meta/llama-3.1-8b-instruct");
        Assert.False(caps.ToolCalls);
        Assert.True(caps.Streaming);
        Assert.False(caps.Vision);
        Assert.False(caps.JsonMode);
    }

    [Fact]
    public void Get_UnknownPreset_FallsBackToConservative()
    {
        var caps = ProviderCapabilities.Get("not-a-real-preset", "some-model");
        Assert.Equal(CapabilityDescriptor.Conservative(), caps);
    }

    [Fact]
    public void Get_EmptyPreset_FallsBackToConservative()
    {
        var caps = ProviderCapabilities.Get("", "anything");
        Assert.Equal(CapabilityDescriptor.Conservative(), caps);
    }

    [Fact]
    public void HasPreset_KnownPresets_ReturnsTrue()
    {
        Assert.True(ProviderCapabilities.HasPreset("openai"));
        Assert.True(ProviderCapabilities.HasPreset("GROQ"));
        Assert.True(ProviderCapabilities.HasPreset("together"));
        Assert.True(ProviderCapabilities.HasPreset("cloudflare"));
        Assert.True(ProviderCapabilities.HasPreset("azure"));
        Assert.True(ProviderCapabilities.HasPreset("foundry"));
    }

    [Fact]
    public void HasPreset_UnknownOrEmpty_ReturnsFalse()
    {
        Assert.False(ProviderCapabilities.HasPreset("nope"));
        Assert.False(ProviderCapabilities.HasPreset(""));
        Assert.False(ProviderCapabilities.HasPreset("   "));
    }

    // -- Override parsing -----------------------------------------------------

    [Fact]
    public void ParseOverrides_NullOrWhitespace_ReturnsEmpty()
    {
        Assert.Empty(ProviderCapabilities.ParseOverrides(null, warnSink: null));
        Assert.Empty(ProviderCapabilities.ParseOverrides("", warnSink: null));
        Assert.Empty(ProviderCapabilities.ParseOverrides("   ", warnSink: null));
    }

    [Fact]
    public void ParseOverrides_SingleEntry_ParsesPresetModelCapability()
    {
        var entries = ProviderCapabilities.ParseOverrides("groq:llama-3.1-8b-instant:tool_calls=true", warnSink: null);
        Assert.Single(entries);
        Assert.Equal("groq", entries[0].Preset);
        Assert.Equal("llama-3.1-8b-instant", entries[0].Model);
        Assert.Equal("tool_calls", entries[0].Capability);
        Assert.True(entries[0].Value);
    }

    [Fact]
    public void ParseOverrides_MultipleEntries_AllParsed()
    {
        var raw = "openai:o1-mini:streaming=true, together:llama-70b:vision=false , groq:m:json_mode=1";
        var entries = ProviderCapabilities.ParseOverrides(raw, warnSink: null);
        Assert.Equal(3, entries.Count);
        Assert.True(entries[0].Value);
        Assert.False(entries[1].Value);
        Assert.True(entries[2].Value); // "1" parses as true
    }

    [Fact]
    public void ParseOverrides_MalformedEntries_AreSkippedAndWarn()
    {
        var warnings = new List<string>();
        var raw = "no-equals-sign,too:few=true,a:b:c=notabool,a:b:unknown_cap=true,openai:m:tool_calls=";
        var entries = ProviderCapabilities.ParseOverrides(raw, warnings.Add);
        Assert.Empty(entries);
        Assert.True(warnings.Count >= 4);
        foreach (var w in warnings)
        {
            Assert.Contains("[capability]", w, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ParseOverrides_GoodAndBadMixed_OnlyGoodReturned()
    {
        var warnings = new List<string>();
        var raw = "openai:gpt-4o:tool_calls=true,bogus=entry,groq:l:streaming=false";
        var entries = ProviderCapabilities.ParseOverrides(raw, warnings.Add);
        Assert.Equal(2, entries.Count);
        Assert.Single(warnings);
    }

    // -- Override application via Get -----------------------------------------

    [Fact]
    public void Get_OverrideFlipsToolCallsTrue_OnTogetherModel()
    {
        var caps = ProviderCapabilities.Get(
            "together",
            "meta-llama-3.1-70b-instruct",
            rawOverrides: "together:meta-llama-3.1-70b-instruct:tool_calls=true",
            warnSink: null);
        Assert.True(caps.ToolCalls);
        // Other flags stay at preset default.
        Assert.True(caps.Streaming);
        Assert.False(caps.Vision);
    }

    [Fact]
    public void Get_OverrideFlipsVisionFalse_OnOpenAi()
    {
        var caps = ProviderCapabilities.Get(
            "openai",
            "gpt-4o",
            rawOverrides: "openai:gpt-4o:vision=false",
            warnSink: null);
        Assert.False(caps.Vision);
        Assert.True(caps.ToolCalls);
    }

    [Fact]
    public void Get_OverrideTargetsDifferentModel_DoesNotApply()
    {
        var caps = ProviderCapabilities.Get(
            "groq",
            "llama-3.1-8b-instant",
            rawOverrides: "groq:llama-3.1-70b-versatile:tool_calls=true",
            warnSink: null);
        Assert.False(caps.ToolCalls);
    }

    [Fact]
    public void Get_OverrideIsCaseInsensitive()
    {
        var caps = ProviderCapabilities.Get(
            "groq",
            "llama-3.1-8b-instant",
            rawOverrides: "GROQ:LLAMA-3.1-8B-INSTANT:TOOL_CALLS=TRUE",
            warnSink: null);
        Assert.True(caps.ToolCalls);
    }

    [Fact]
    public void Get_OverrideEnvVar_AppliesViaProcessEnv()
    {
        using var _ = new EnvScope(
            ProviderCapabilities.OverridesEnvVar,
            "cloudflare:@cf/test:json_mode=true");
        var caps = ProviderCapabilities.Get("cloudflare", "@cf/test");
        Assert.True(caps.JsonMode);
    }

    // -- Mismatch exception ---------------------------------------------------

    [Fact]
    public void Mismatch_BuildsFriendlyMessage_NamesOverrideEnvVar()
    {
        var ex = ProviderCapabilities.Mismatch("groq", "llama-3.1-8b-instant", "tool_calls");
        Assert.Contains("groq:llama-3.1-8b-instant", ex.Message, StringComparison.Ordinal);
        Assert.Contains("does not support tool_calls", ex.Message, StringComparison.Ordinal);
        Assert.Contains(ProviderCapabilities.OverridesEnvVar, ex.Message, StringComparison.Ordinal);
        Assert.Contains("=true", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Mismatch_VisionMessage_NamesCapability()
    {
        var ex = ProviderCapabilities.Mismatch("groq", "llama-3.1-8b-instant", "vision");
        Assert.Contains("does not support vision", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CapabilityMismatchException_ErrorClass_IsStableConstant()
    {
        Assert.Equal("CapabilityMismatch", CapabilityMismatchException.ErrorClass);
    }

    [Fact]
    public void CapabilityMismatchException_CarriesPresetModelCapability()
    {
        var ex = ProviderCapabilities.Mismatch("together", "x", "tool_calls");
        Assert.Equal("together", ex.Preset);
        Assert.Equal("x", ex.Model);
        Assert.Equal("tool_calls", ex.Capability);
    }
}
