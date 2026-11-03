using System.ClientModel.Primitives;
using AzureOpenAI_CLI.Capabilities;
using AzureOpenAI_CLI.Observability;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using OpenAI.Responses;
using Xunit;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

// ResponsesClient is evaluation-only in OpenAI 2.9; the adapter suppresses
// the same diagnostic at its single construction site.
#pragma warning disable OPENAI001

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// S04E24 *The Copilot* -- GitHub Copilot via the OpenAI-compat preset
/// surface (ADR-010: generic knobs, no per-vendor adapter). Covers preset
/// shape, credential precedence (env vars, then <c>gh auth token</c>),
/// static client headers, Responses-vs-Chat dispatch, capability profile,
/// per-model list prices and dispatch telemetry. Pure / in-process: the
/// token command is stubbed and no HTTP request is sent.
/// </summary>
[Collection("ConsoleCapture")]
public class CopilotPresetTests : IDisposable
{
    private readonly Dictionary<string, string?> _savedEnv = new();
    private readonly Func<string[], string?> _savedRunner = OpenAiCompatAdapter.ApiKeyCommandRunner;

    private static readonly string[] EnvVars =
    {
        "AZ_AI_COMPAT_MODELS",
        "AZ_AI_COPILOT_ENDPOINT",
        "AZ_AI_COPILOT_MODEL",
        "AZ_AI_CAPABILITY_OVERRIDES",
        "AZURE_FOUNDRY_MODELS",
        "COPILOT_GITHUB_TOKEN",
        "GH_TOKEN",
        "GITHUB_TOKEN",
    };

    public CopilotPresetTests()
    {
        foreach (var v in EnvVars)
        {
            _savedEnv[v] = Environment.GetEnvironmentVariable(v);
            Environment.SetEnvironmentVariable(v, null);
        }
        // Never shell out to a real `gh` from tests.
        OpenAiCompatAdapter.ApiKeyCommandRunner = _ => null;
    }

    public void Dispose()
    {
        OpenAiCompatAdapter.ApiKeyCommandRunner = _savedRunner;
        foreach (var kv in _savedEnv)
            Environment.SetEnvironmentVariable(kv.Key, kv.Value);
    }

    private static OpenAiCompatPreset Copilot => OpenAiCompatAdapter.BuiltIn["copilot"];

    // -- Preset shape ---------------------------------------------------

    [Fact]
    public void Preset_IsRegistered_WithCopilotEndpointAndDefaults()
    {
        Assert.Equal("https://api.githubcopilot.com/", Copilot.BaseUrl.ToString());
        Assert.Equal("COPILOT_GITHUB_TOKEN", Copilot.ApiKeyEnvVar);
        Assert.Equal(new[] { "GH_TOKEN", "GITHUB_TOKEN" }, Copilot.ApiKeyFallbackEnvVars);
        Assert.Equal(new[] { "gh", "auth", "token" }, Copilot.ApiKeyCommand);
        Assert.Equal("gpt-6-luna", Copilot.DefaultModel);
        Assert.True(Copilot.RequiresApiKey);
    }

    [Fact]
    public void Preset_StaticHeaders_AreInitialized()
    {
        // Regression guard: CopilotHeaders must be initialized before
        // BuiltIn (static initializers run in textual order).
        Assert.NotNull(Copilot.StaticHeaders);
        Assert.Equal("vscode-chat", Copilot.StaticHeaders!["Copilot-Integration-Id"]);
        Assert.True(Copilot.StaticHeaders.ContainsKey("Editor-Version"));
    }

    [Fact]
    public void ExistingPresets_HaveNoCopilotKnobs()
    {
        foreach (var name in new[] { "openai", "groq", "together", "cloudflare", "llamacpp" })
        {
            var p = OpenAiCompatAdapter.BuiltIn[name];
            Assert.Null(p.ApiKeyFallbackEnvVars);
            Assert.Null(p.ApiKeyCommand);
            Assert.Null(p.StaticHeaders);
            Assert.Null(p.ResponsesModelPatterns);
        }
    }

    // -- Credential precedence -----------------------------------------

    [Fact]
    public void ResolveApiKey_PrefersCopilotTokenOverFallbacks()
    {
        Environment.SetEnvironmentVariable("COPILOT_GITHUB_TOKEN", "primary");
        Environment.SetEnvironmentVariable("GH_TOKEN", "gh");
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", "github");
        OpenAiCompatAdapter.ApiKeyCommandRunner = _ => throw new InvalidOperationException("command must not run");

        Assert.Equal("primary", OpenAiCompatAdapter.ResolveApiKey(Copilot));
    }

    [Fact]
    public void ResolveApiKey_FallsBackInOrder()
    {
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", "github");
        Assert.Equal("github", OpenAiCompatAdapter.ResolveApiKey(Copilot));

        Environment.SetEnvironmentVariable("GH_TOKEN", " gh ");
        Assert.Equal("gh", OpenAiCompatAdapter.ResolveApiKey(Copilot));
    }

    [Fact]
    public void ResolveApiKey_UsesCommandWhenNoEnvVar()
    {
        string[]? seen = null;
        OpenAiCompatAdapter.ApiKeyCommandRunner = argv => { seen = argv; return "from-gh\n"; };

        Assert.Equal("from-gh", OpenAiCompatAdapter.ResolveApiKey(Copilot));
        Assert.Equal(new[] { "gh", "auth", "token" }, seen);
    }

    [Fact]
    public void ResolveApiKey_ReturnsNullWhenNothingResolves()
    {
        Assert.Null(OpenAiCompatAdapter.ResolveApiKey(Copilot));
    }

    [Fact]
    public void Build_WithoutCredential_NamesEverySource()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => OpenAiCompatAdapter.Build("gpt-6-luna", Copilot));
        Assert.Contains("COPILOT_GITHUB_TOKEN", ex.Message);
        Assert.Contains("GH_TOKEN", ex.Message);
        Assert.Contains("GITHUB_TOKEN", ex.Message);
        Assert.Contains("gh auth token", ex.Message);
    }

    // -- Responses vs Chat dispatch -------------------------------------

    [Theory]
    [InlineData("gpt-6-luna", true)]
    [InlineData("gpt-6.1-terra", true)]
    [InlineData("gpt-6-astra", true)]
    [InlineData("gpt-5.6-sol", true)]
    [InlineData("gpt-5.4-nano", true)]
    [InlineData("gpt-5.3-codex", true)]
    [InlineData("grok-4.7", true)]
    [InlineData("mai-code-1.1-flash", true)]
    [InlineData("GPT-6-LUNA", true)]
    [InlineData("claude-haiku-5.5", false)]
    [InlineData("claude-opus-5.5", false)]
    [InlineData("gemini-4-argon", false)]
    [InlineData("gpt-4o-2024-11-20", false)]
    [InlineData("mai-code-2", false)]
    public void UsesResponsesApi_MatchesCopilotCatalog(string model, bool expected)
    {
        Assert.Equal(expected, OpenAiCompatAdapter.UsesResponsesApi(Copilot, model));
    }

    [Fact]
    public void UsesResponsesApi_FalseForPresetsWithoutPatterns()
    {
        Assert.False(OpenAiCompatAdapter.UsesResponsesApi(OpenAiCompatAdapter.BuiltIn["openai"], "gpt-6-luna"));
    }

    [Fact]
    public void Build_ResponsesModel_UsesResponsesClient()
    {
        Environment.SetEnvironmentVariable("COPILOT_GITHUB_TOKEN", "test-token");
        using var client = OpenAiCompatAdapter.Build("gpt-6-luna", Copilot);

        Assert.NotNull(client.GetService(typeof(ResponsesClient)));
        Assert.Null(client.GetService(typeof(ChatClient)));
        var meta = client.GetService<ChatClientMetadata>();
        Assert.Equal("gpt-6-luna", meta?.DefaultModelId);
        Assert.Equal("api.githubcopilot.com", meta?.ProviderUri?.Host);
    }

    [Fact]
    public void Build_ChatModel_UsesChatClient()
    {
        Environment.SetEnvironmentVariable("GH_TOKEN", "test-token");
        using var client = OpenAiCompatAdapter.Build("claude-haiku-5.5", Copilot);

        Assert.NotNull(client.GetService(typeof(ChatClient)));
        Assert.Null(client.GetService(typeof(ResponsesClient)));
    }

    [Fact]
    public void Build_BlankModel_UsesDefaultThenModelEnv()
    {
        Environment.SetEnvironmentVariable("COPILOT_GITHUB_TOKEN", "test-token");
        using (var client = OpenAiCompatAdapter.Build("", Copilot))
            Assert.Equal("gpt-6-luna", client.GetService<ChatClientMetadata>()?.DefaultModelId);

        Environment.SetEnvironmentVariable("AZ_AI_COPILOT_MODEL", "claude-haiku-5.5");
        using (var client = OpenAiCompatAdapter.Build("", Copilot))
            Assert.NotNull(client.GetService(typeof(ChatClient)));
    }

    [Fact]
    public void Build_EndpointOverride_IsHonored()
    {
        Environment.SetEnvironmentVariable("COPILOT_GITHUB_TOKEN", "test-token");
        Environment.SetEnvironmentVariable("AZ_AI_COPILOT_ENDPOINT", "https://api.business.githubcopilot.com");
        using var client = OpenAiCompatAdapter.Build("gpt-6-luna", Copilot);

        Assert.Equal("api.business.githubcopilot.com", client.GetService<ChatClientMetadata>()?.ProviderUri?.Host);
    }

    // -- Stateless Responses (Copilot rejects previous_response_id) -------

    [Fact]
    public void Build_ResponsesModel_IsStateless()
    {
        Environment.SetEnvironmentVariable("COPILOT_GITHUB_TOKEN", "test-token");
        using var client = OpenAiCompatAdapter.Build("gpt-6-luna", Copilot);

        Assert.IsType<OpenAiCompatAdapter.StatelessResponsesChatClient>(client);
    }

    [Fact]
    public void Stateless_DisablesStorage_WithoutMutatingCallerOptions()
    {
        var caller = new ChatOptions { Temperature = 0.3f };

        var effective = OpenAiCompatAdapter.StatelessResponsesChatClient.Stateless(caller);

        Assert.Null(caller.RawRepresentationFactory);
        Assert.Equal(0.3f, effective.Temperature);
        var raw = Assert.IsType<CreateResponseOptions>(effective.RawRepresentationFactory!(null!));
        Assert.False(raw.StoredOutputEnabled);
    }

    [Fact]
    public void Stateless_KeepsCallerRawOptions()
    {
        var caller = new ChatOptions
        {
            RawRepresentationFactory = _ => new CreateResponseOptions { StoredOutputEnabled = true, PreviousResponseId = null },
        };

        var raw = (CreateResponseOptions)OpenAiCompatAdapter.StatelessResponsesChatClient.Stateless(caller).RawRepresentationFactory!(null!)!;

        Assert.False(raw.StoredOutputEnabled);
    }

    [Fact]
    public async Task Stateless_IsAppliedToEveryCall()
    {
        var inner = new CapturingClient();
        using var client = new OpenAiCompatAdapter.StatelessResponsesChatClient(inner);

        await client.GetResponseAsync("hi");
        await foreach (var _ in client.GetStreamingResponseAsync("hi")) { }

        Assert.Equal(2, inner.Seen.Count);
        Assert.All(inner.Seen, o =>
            Assert.False(((CreateResponseOptions)o!.RawRepresentationFactory!(null!)!).StoredOutputEnabled));
    }

    private sealed class CapturingClient : IChatClient
    {
        public List<ChatOptions?> Seen { get; } = new();

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Seen.Add(options);
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Seen.Add(options);
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, "ok");
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    // -- Static headers -------------------------------------------------

    [Fact]
    public void StaticHeaderPolicy_SetsEveryHeader()
    {
        var policy = new OpenAiCompatAdapter.StaticHeaderPolicy(OpenAiCompatAdapter.CopilotHeaders);
        var message = ClientPipeline.Create().CreateMessage();

        policy.Apply(message);

        foreach (var kv in OpenAiCompatAdapter.CopilotHeaders)
        {
            Assert.True(message.Request.Headers.TryGetValue(kv.Key, out var value), kv.Key);
            Assert.Equal(kv.Value, value);
        }
    }

    // -- Capability, cost, dispatch telemetry ---------------------------

    [Fact]
    public void Capabilities_CopilotSupportsTools()
    {
        Assert.True(ProviderCapabilities.HasPreset("copilot"));
        var caps = ProviderCapabilities.Get("copilot", "gpt-6-luna");
        Assert.True(caps.ToolCalls);
        Assert.True(caps.Streaming);
    }

    [Fact]
    public void CostRates_PerModelListPrice()
    {
        Assert.True(CompatCostRates.TryGetModelRates("copilot", "gpt-6-luna", out var inP, out var outP));
        Assert.Equal(0.00010, inP, 8);
        Assert.Equal(0.00050, outP, 8);
        Assert.True(CompatCostRates.TryGetModelRates("COPILOT", "Claude-Opus-5.5", out inP, out _));
        Assert.Equal(0.00400, inP, 8);
        Assert.False(CompatCostRates.TryGetModelRates("copilot", "unpriced-model", out _, out _));
        Assert.False(CompatCostRates.TryGetModelRates("openai", "gpt-6-luna", out _, out _));
    }

    [Fact]
    public void CostEstimator_UsesCopilotModelPrice()
    {
        var est = CostEstimator.EstimateForCompatPreset("copilot", "gpt-6.1-terra", new string('x', 4000), 1000);

        Assert.Equal("copilot:gpt-6.1-terra", est.Model);
        Assert.Contains("list price", est.Approximation);
    }

    // -- AZUREOPENAIMODEL allowlist vs routed models ---------------------

    [Fact]
    public void IsModelAllowed_AzureListOnly()
    {
        var azure = new HashSet<string>(new[] { "gpt-5.4-mini", "gpt-5-nano" }, StringComparer.OrdinalIgnoreCase);

        Assert.True(Program.IsModelAllowed("anything", null));
        Assert.True(Program.IsModelAllowed("GPT-5.4-MINI", azure));
        Assert.False(Program.IsModelAllowed("gpt-6-luna", azure));
    }

    [Fact]
    public void IsModelAllowed_CompatRoutedModelPassesAzureList()
    {
        // Regression: a multi-model AZUREOPENAIMODEL used to veto every
        // AZ_AI_COMPAT_MODELS route, so no compat preset was reachable.
        var azure = new HashSet<string>(new[] { "gpt-5.4-mini", "gpt-5-nano" }, StringComparer.OrdinalIgnoreCase);
        Environment.SetEnvironmentVariable("AZ_AI_COMPAT_MODELS", "copilot:gpt-6-luna");

        Assert.True(Program.IsModelAllowed("gpt-6-luna", azure));
        Assert.False(Program.IsModelAllowed("gpt-6.1-terra", azure));
    }

    [Fact]
    public void IsModelAllowed_FoundryRoutedModelPassesAzureList()
    {
        var azure = new HashSet<string>(new[] { "gpt-5.4-mini" }, StringComparer.OrdinalIgnoreCase);
        Environment.SetEnvironmentVariable("AZURE_FOUNDRY_MODELS", "phi-4");

        Assert.True(Program.IsModelAllowed("phi-4", azure));
    }

    [Fact]
    public void IsModelAllowed_MalformedCompatListDoesNotAllow()
    {
        var azure = new HashSet<string>(new[] { "gpt-5.4-mini" }, StringComparer.OrdinalIgnoreCase);
        Environment.SetEnvironmentVariable("AZ_AI_COMPAT_MODELS", "no-colon-here");

        Assert.False(Program.IsModelAllowed("gpt-6-luna", azure));
    }

    [Fact]
    public void Dispatch_CompatModelsRoutesToCopilot()
    {
        Environment.SetEnvironmentVariable("AZ_AI_COMPAT_MODELS", "copilot:gpt-6-luna,copilot:claude-haiku-5.5");

        Assert.Equal(("copilot", "compat-allowlist"), Program.ResolveDispatchInfo("gpt-6-luna"));
        Assert.Equal(("copilot", "compat-allowlist"), Program.ResolveDispatchInfo("claude-haiku-5.5"));
    }
}
