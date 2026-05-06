using AzureOpenAI_CLI.Capabilities;
using AzureOpenAI_CLI.Cli;
using AzureOpenAI_CLI.Net;
using Xunit;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// S03E17 *The Server* (file slot 21) -- llama.cpp llama-server adapter via
/// OpenAI-compat preset. Mirrors the spirit of <see cref="OpenAiCompatAdapterTests"/>
/// but stays scoped to the new <c>llamacpp</c> preset surface: preset shape,
/// runtime endpoint override, optional API key, default-model fallback,
/// capability=Conservative, allowlist loopback gate, and ProviderDoctor probe.
/// Pure / in-process; no real HTTP.
/// </summary>
[Collection("ConsoleCapture")]
public class LlamaCppPresetTests : IDisposable
{
    private readonly Dictionary<string, string?> _savedEnv = new();

    private static readonly string[] EnvVars =
    {
        "AZ_AI_COMPAT_MODELS",
        "AZ_AI_LLAMACPP_ENDPOINT",
        "AZ_AI_LLAMACPP_MODEL",
        "AZ_AI_LLAMACPP_API_KEY",
        "AZ_AI_LOCAL_PROVIDERS",
        "AZ_AI_OFFLINE",
        "AZ_AI_CAPABILITY_OVERRIDES",
        "OPENAI_API_KEY",
        "AZUREOPENAIENDPOINT",
        "AZUREOPENAIAPI",
        "AZUREOPENAIMODEL",
        "AZURE_FOUNDRY_ENDPOINT",
        "AZURE_FOUNDRY_KEY",
        "AZURE_FOUNDRY_MODELS",
    };

    public LlamaCppPresetTests()
    {
        foreach (var v in EnvVars)
            _savedEnv[v] = Environment.GetEnvironmentVariable(v);
        foreach (var v in EnvVars)
            Environment.SetEnvironmentVariable(v, null);
    }

    public void Dispose()
    {
        foreach (var kv in _savedEnv)
            Environment.SetEnvironmentVariable(kv.Key, kv.Value);
    }

    // -- Preset shape ---------------------------------------------------

    [Fact]
    public void BuiltIn_LlamaCppPreset_IsRegistered()
    {
        Assert.True(OpenAiCompatAdapter.BuiltIn.ContainsKey("llamacpp"));
    }

    [Fact]
    public void BuiltIn_LlamaCppPreset_HasExpectedShape()
    {
        var p = OpenAiCompatAdapter.BuiltIn["llamacpp"];
        Assert.Equal("llamacpp", p.Name);
        Assert.Equal("http://localhost:8080/v1", p.BaseUrl.OriginalString);
        Assert.Equal("AZ_AI_LLAMACPP_API_KEY", p.ApiKeyEnvVar);
        Assert.Equal("AZ_AI_LLAMACPP_ENDPOINT", p.EndpointEnvVar);
        Assert.Equal("AZ_AI_LLAMACPP_MODEL", p.ModelEnvVar);
        Assert.Equal("llamacpp", p.DefaultModel);
        Assert.False(p.RequiresApiKey);
        Assert.Equal("Bearer", p.AuthScheme);
        Assert.Null(p.OrgEnvVar);
    }

    [Theory]
    [InlineData("llamacpp")]
    [InlineData("LlamaCpp")]
    [InlineData("LLAMACPP")]
    public void Resolve_LlamaCpp_CaseInsensitive(string name)
    {
        var p = OpenAiCompatAdapter.Resolve(name);
        Assert.NotNull(p);
        Assert.Equal("llamacpp", p!.Name);
    }

    [Fact]
    public void ResolveOrThrow_LlamaCpp_ListedInUnknownPresetMessage()
    {
        var ex = Assert.Throws<ArgumentException>(() => OpenAiCompatAdapter.ResolveOrThrow("nope"));
        Assert.Contains("llamacpp", ex.Message, StringComparison.Ordinal);
    }

    // -- ResolveModel helper --------------------------------------------

    [Fact]
    public void ResolveModel_RequestedWins_OverEnvAndDefault()
    {
        Environment.SetEnvironmentVariable("AZ_AI_LLAMACPP_MODEL", "qwen-coder");
        var p = OpenAiCompatAdapter.BuiltIn["llamacpp"];
        Assert.Equal("explicit-model", OpenAiCompatAdapter.ResolveModel(p, "explicit-model"));
    }

    [Fact]
    public void ResolveModel_BlankRequested_FallsBackToEnv()
    {
        Environment.SetEnvironmentVariable("AZ_AI_LLAMACPP_MODEL", "qwen-coder");
        var p = OpenAiCompatAdapter.BuiltIn["llamacpp"];
        Assert.Equal("qwen-coder", OpenAiCompatAdapter.ResolveModel(p, null));
        Assert.Equal("qwen-coder", OpenAiCompatAdapter.ResolveModel(p, "   "));
    }

    [Fact]
    public void ResolveModel_NoEnv_FallsBackToDefault()
    {
        var p = OpenAiCompatAdapter.BuiltIn["llamacpp"];
        Assert.Equal("llamacpp", OpenAiCompatAdapter.ResolveModel(p, null));
    }

    [Fact]
    public void ResolveModel_PresetWithNoFallbacks_ReturnsNull()
    {
        // Synthetic preset with neither ModelEnvVar nor DefaultModel.
        var p = new OpenAiCompatPreset(
            "test", new Uri("https://example.invalid/v1"), "EXAMPLE_KEY");
        Assert.Null(OpenAiCompatAdapter.ResolveModel(p, null));
        Assert.Null(OpenAiCompatAdapter.ResolveModel(p, ""));
    }

    // -- Build() -- API key optional, endpoint override, blank model ----

    [Fact]
    public void Build_LlamaCpp_NoApiKey_LoopbackOptedIn_Succeeds()
    {
        Environment.SetEnvironmentVariable("AZ_AI_LOCAL_PROVIDERS", "1");
        var p = OpenAiCompatAdapter.BuiltIn["llamacpp"];
        var client = OpenAiCompatAdapter.Build("llamacpp", p);
        Assert.NotNull(client);
    }

    [Fact]
    public void Build_LlamaCpp_BlankModel_FallsBackToDefault()
    {
        Environment.SetEnvironmentVariable("AZ_AI_LOCAL_PROVIDERS", "1");
        var p = OpenAiCompatAdapter.BuiltIn["llamacpp"];
        // Blank model is allowed because preset.DefaultModel is set.
        var client = OpenAiCompatAdapter.Build("", p);
        Assert.NotNull(client);
    }

    [Fact]
    public void Build_LlamaCpp_LoopbackWithoutOptIn_Throws()
    {
        // Default: AZ_AI_LOCAL_PROVIDERS unset -> loopback blocked.
        var p = OpenAiCompatAdapter.BuiltIn["llamacpp"];
        var ex = Assert.Throws<ArgumentException>(
            () => OpenAiCompatAdapter.Build("llamacpp", p));
        Assert.Contains("compat preset 'llamacpp'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Refusing to dispatch", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_LlamaCpp_EndpointOverride_HonoredAtBuild()
    {
        Environment.SetEnvironmentVariable("AZ_AI_LOCAL_PROVIDERS", "1");
        Environment.SetEnvironmentVariable("AZ_AI_LLAMACPP_ENDPOINT", "http://127.0.0.1:8123/v1");
        var p = OpenAiCompatAdapter.BuiltIn["llamacpp"];
        var client = OpenAiCompatAdapter.Build("llamacpp", p);
        Assert.NotNull(client);
    }

    [Fact]
    public void Build_LlamaCpp_EndpointOverride_Malformed_Throws()
    {
        Environment.SetEnvironmentVariable("AZ_AI_LOCAL_PROVIDERS", "1");
        Environment.SetEnvironmentVariable("AZ_AI_LLAMACPP_ENDPOINT", "not a url");
        var p = OpenAiCompatAdapter.BuiltIn["llamacpp"];
        var ex = Assert.Throws<InvalidOperationException>(
            () => OpenAiCompatAdapter.Build("llamacpp", p));
        Assert.Contains("AZ_AI_LLAMACPP_ENDPOINT", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_LlamaCpp_EndpointOverride_NonLoopbackHttp_Refused()
    {
        // HTTPS-only for non-loopback hosts is a baseline rule. An operator
        // who tries to point llamacpp at a public-IP HTTP endpoint must be
        // rejected even with AZ_AI_LOCAL_PROVIDERS=1 (opt-in only relaxes
        // private-range gating, not the HTTPS posture).
        Environment.SetEnvironmentVariable("AZ_AI_LOCAL_PROVIDERS", "1");
        Environment.SetEnvironmentVariable("AZ_AI_LLAMACPP_ENDPOINT", "http://example.com/v1");
        var p = OpenAiCompatAdapter.BuiltIn["llamacpp"];
        Assert.Throws<InvalidOperationException>(
            () => OpenAiCompatAdapter.Build("llamacpp", p));
    }

    [Fact]
    public void Build_LlamaCpp_WithApiKeySet_UsesIt()
    {
        // Operators who run llama-server with --api-key can still configure
        // the env var. The preset accepts it without complaint.
        Environment.SetEnvironmentVariable("AZ_AI_LOCAL_PROVIDERS", "1");
        Environment.SetEnvironmentVariable("AZ_AI_LLAMACPP_API_KEY", "sk-llamacpp-demo");
        var p = OpenAiCompatAdapter.BuiltIn["llamacpp"];
        var client = OpenAiCompatAdapter.Build("llamacpp", p);
        Assert.NotNull(client);
    }

    // -- Capability gate ------------------------------------------------

    [Fact]
    public void Capabilities_LlamaCpp_IsConservative()
    {
        var caps = ProviderCapabilities.Get("llamacpp", "llamacpp");
        Assert.False(caps.ToolCalls);
        Assert.True(caps.Streaming);
        Assert.False(caps.Vision);
        Assert.False(caps.JsonMode);
    }

    [Fact]
    public void Capabilities_HasPreset_LlamaCpp_True()
    {
        // Important: HasPreset==true suppresses the "unknown capability
        // profile" warning during Build(); a missing registry entry would
        // spam stderr on every llamacpp dispatch.
        Assert.True(ProviderCapabilities.HasPreset("llamacpp"));
    }

    [Fact]
    public void Capabilities_LlamaCpp_OverrideAllowsToolCalls()
    {
        // Documented escape hatch: an operator running a tool-calling
        // capable local model can flip the flag on.
        var caps = ProviderCapabilities.Get(
            "llamacpp",
            "qwen2.5-coder",
            rawOverrides: "llamacpp:qwen2.5-coder:tool_calls=true",
            warnSink: null);
        Assert.True(caps.ToolCalls);
    }

    // -- Endpoint allowlist gate ----------------------------------------

    [Fact]
    public void Allowlist_LlamaCppDefault_BlockedWithoutOptIn()
    {
        var verdict = EndpointAllowlist.Check(
            new Uri("http://localhost:8080/v1"), localProvidersOptIn: false);
        Assert.Equal(AllowlistVerdict.BlockLoopback, verdict);
    }

    [Fact]
    public void Allowlist_LlamaCppDefault_AllowedWithOptIn()
    {
        var verdict = EndpointAllowlist.Check(
            new Uri("http://localhost:8080/v1"), localProvidersOptIn: true);
        Assert.Equal(AllowlistVerdict.Allow, verdict);
    }

    [Fact]
    public void Allowlist_LlamaCppOverride_AlternatePort_AllowedWithOptIn()
    {
        var verdict = EndpointAllowlist.Check(
            new Uri("http://127.0.0.1:8123/v1"), localProvidersOptIn: true);
        Assert.Equal(AllowlistVerdict.Allow, verdict);
    }

    // -- Provider doctor probe ------------------------------------------

    [Fact]
    public void ProviderDoctor_CollectProviders_IncludesLlamaCppRow()
    {
        Environment.SetEnvironmentVariable("AZ_AI_COMPAT_MODELS", "llamacpp:llamacpp");
        var probes = ProviderDoctor.CollectProviders();
        Assert.Contains(probes, x => string.Equals(x.Name, "compat:llamacpp", StringComparison.Ordinal));
        var row = probes.First(x => string.Equals(x.Name, "compat:llamacpp", StringComparison.Ordinal));
        Assert.Equal("http://localhost:8080/v1", row.Endpoint);
        Assert.Equal("AZ_AI_LLAMACPP_API_KEY", row.CredEnvVar);
    }

    [Fact]
    public void ProviderDoctor_CollectProviders_NoCompatModels_NoLlamaCppRow()
    {
        // Guard the inverse: with no compat models exported the doctor must
        // not synthesize a phantom llamacpp row.
        var probes = ProviderDoctor.CollectProviders();
        Assert.DoesNotContain(probes, x => x.Name.StartsWith("compat:llamacpp", StringComparison.Ordinal));
    }
}
