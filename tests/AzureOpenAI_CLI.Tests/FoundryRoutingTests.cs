using AzureOpenAI_CLI;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// ADR-005 — Azure AI Foundry endpoint routing.
///
/// These tests exercise the routing decision logic in Program.ResolveRoute
/// and Program.ValidateConfiguration. They cover both positive matches
/// (Foundry allowlist, case-insensitive) and negative cases (fallback to
/// Azure OpenAI when the env var is missing, when the model is unknown,
/// or when credentials are missing entirely).
/// </summary>
public class FoundryRoutingTests
{
    private const string AzureEndpoint = "https://sierrahackingco.cognitiveservices.azure.com/";
    private const string FoundryEndpoint = "https://sierrahackingco.services.ai.azure.com/models";
    private const string ApiKey = "test-api-key-unused";

    private static UserConfig ConfigFor(string model) => new()
    {
        ActiveModel = model,
        AvailableModels = new List<string> { model },
    };

    // ── ResolveRoute: positive + negative allowlist checks ─────────────

    [Theory]
    [InlineData("Phi-4-mini-instruct")]
    [InlineData("Phi-4-mini-reasoning")]
    [InlineData("DeepSeek-V3.2")]
    public void ResolveRoute_FoundryEndpointSet_AndFoundryModel_RoutesToFoundry(string model)
    {
        var route = Program.ResolveRoute(FoundryEndpoint, model);
        Assert.Equal(Program.ChatRoute.AzureFoundry, route);
    }

    [Theory]
    [InlineData("phi-4-mini-instruct")]
    [InlineData("PHI-4-MINI-INSTRUCT")]
    [InlineData("deepseek-v3.2")]
    public void ResolveRoute_IsCaseInsensitive(string model)
    {
        var route = Program.ResolveRoute(FoundryEndpoint, model);
        Assert.Equal(Program.ChatRoute.AzureFoundry, route);
    }

    [Theory]
    [InlineData("gpt-5.4-nano")]
    [InlineData("gpt-4o-mini")]
    [InlineData("o1-preview")]
    [InlineData("")]
    public void ResolveRoute_FoundryEndpointSet_ButNonFoundryModel_FallsBackToAzureOpenAI(string model)
    {
        var route = Program.ResolveRoute(FoundryEndpoint, model);
        Assert.Equal(Program.ChatRoute.AzureOpenAI, route);
    }

    [Fact]
    public void ResolveRoute_NoFoundryEndpoint_AlwaysAzureOpenAI_EvenForFoundryModel()
    {
        Assert.Equal(Program.ChatRoute.AzureOpenAI, Program.ResolveRoute(null, "Phi-4-mini-instruct"));
        Assert.Equal(Program.ChatRoute.AzureOpenAI, Program.ResolveRoute("", "Phi-4-mini-instruct"));
        Assert.Equal(Program.ChatRoute.AzureOpenAI, Program.ResolveRoute("   ", "Phi-4-mini-instruct"));
    }

    // ── ValidateConfiguration: full pipeline (route + endpoint + error) ─

    [Fact]
    public void ValidateConfiguration_FoundrySet_AndFoundryModel_ReturnsFoundryEndpoint()
    {
        var (endpoint, apiKey, deployment, route, err) = Program.ValidateConfiguration(
            AzureEndpoint, ApiKey, ConfigFor("Phi-4-mini-instruct"), FoundryEndpoint);

        Assert.Null(err);
        Assert.Equal(Program.ChatRoute.AzureFoundry, route);
        Assert.Equal(new Uri(FoundryEndpoint), endpoint);
        Assert.Equal(ApiKey, apiKey);
        Assert.Equal("Phi-4-mini-instruct", deployment);
    }

    [Fact]
    public void ValidateConfiguration_FoundrySet_ButNonFoundryModel_ReturnsAzureOpenAIEndpoint()
    {
        var (endpoint, _, _, route, err) = Program.ValidateConfiguration(
            AzureEndpoint, ApiKey, ConfigFor("gpt-5.4-nano"), FoundryEndpoint);

        Assert.Null(err);
        Assert.Equal(Program.ChatRoute.AzureOpenAI, route);
        Assert.Equal(new Uri(AzureEndpoint), endpoint);
    }

    [Fact]
    public void ValidateConfiguration_OnlyAzureEndpoint_ReturnsAzureOpenAIPath()
    {
        var (endpoint, _, _, route, err) = Program.ValidateConfiguration(
            AzureEndpoint, ApiKey, ConfigFor("gpt-5.4-nano"), foundryEndpoint: null);

        Assert.Null(err);
        Assert.Equal(Program.ChatRoute.AzureOpenAI, route);
        Assert.Equal(new Uri(AzureEndpoint), endpoint);
    }

    [Fact]
    public void ValidateConfiguration_CaseInsensitiveModelMatch_RoutesFoundry()
    {
        // Lower-cased deployment name must still route to Foundry.
        var (endpoint, _, deployment, route, err) = Program.ValidateConfiguration(
            AzureEndpoint, ApiKey, ConfigFor("phi-4-mini-instruct"), FoundryEndpoint);

        Assert.Null(err);
        Assert.Equal(Program.ChatRoute.AzureFoundry, route);
        Assert.Equal(new Uri(FoundryEndpoint), endpoint);
        Assert.Equal("phi-4-mini-instruct", deployment);
    }

    // ── Negative / error paths ─────────────────────────────────────────

    [Fact]
    public void ValidateConfiguration_MissingApiKey_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Program.ValidateConfiguration(
            AzureEndpoint, azureOpenAiApiKey: null, ConfigFor("gpt-5.4-nano"), FoundryEndpoint));
        Assert.Throws<ArgumentNullException>(() => Program.ValidateConfiguration(
            AzureEndpoint, azureOpenAiApiKey: "   ", ConfigFor("gpt-5.4-nano"), FoundryEndpoint));
    }

    [Fact]
    public void ValidateConfiguration_MissingBothEndpoints_Throws()
    {
        // Neither Azure OpenAI nor Foundry endpoint set → the existing error path.
        Assert.Throws<ArgumentNullException>(() => Program.ValidateConfiguration(
            azureOpenAiEndpoint: null, ApiKey, ConfigFor("gpt-5.4-nano"), foundryEndpoint: null));
    }

    [Fact]
    public void ValidateConfiguration_FoundryModel_ButFoundryEndpointMissing_FallsBackToAzure()
    {
        // User set AZUREOPENAIMODEL=Phi-4-mini-instruct but forgot AZURE_FOUNDRY_ENDPOINT.
        // Should fall back to Azure OpenAI (the deployment name lookup there will fail
        // at runtime, but routing-layer must not crash).
        var (endpoint, _, _, route, err) = Program.ValidateConfiguration(
            AzureEndpoint, ApiKey, ConfigFor("Phi-4-mini-instruct"), foundryEndpoint: null);

        Assert.Null(err);
        Assert.Equal(Program.ChatRoute.AzureOpenAI, route);
        Assert.Equal(new Uri(AzureEndpoint), endpoint);
    }

    [Fact]
    public void ValidateConfiguration_InvalidFoundryUrl_ReturnsError()
    {
        var (_, _, _, route, err) = Program.ValidateConfiguration(
            AzureEndpoint, ApiKey, ConfigFor("Phi-4-mini-instruct"),
            foundryEndpoint: "http://not-https.example/models");

        // http:// fails the https check → error returned, route was Foundry.
        Assert.NotNull(err);
        Assert.Contains("Invalid endpoint URL", err);
        Assert.Equal(Program.ChatRoute.AzureFoundry, route);
    }

    [Fact]
    public void ValidateConfiguration_InvalidAzureUrl_ReturnsError_OnAzurePath()
    {
        var (_, _, _, route, err) = Program.ValidateConfiguration(
            azureOpenAiEndpoint: "not-a-url", ApiKey, ConfigFor("gpt-5.4-nano"),
            foundryEndpoint: null);

        Assert.NotNull(err);
        Assert.Contains("Invalid endpoint URL", err);
        Assert.Equal(Program.ChatRoute.AzureOpenAI, route);
    }

    // ── Client construction smoke test (no network) ────────────────────

    [Fact]
    public void BuildFoundryChatClient_ReturnsNonNull_WithoutThrowing()
    {
        // Constructor must succeed offline; actual request is not made.
        var client = Program.BuildFoundryChatClient(
            new Uri(FoundryEndpoint), ApiKey, "Phi-4-mini-instruct");
        Assert.NotNull(client);
    }

    [Fact]
    public void FoundryRoutedModels_IncludesAllAdr005Models()
    {
        // Regression guard: if someone edits the allowlist, we notice.
        Assert.Contains("Phi-4-mini-instruct", Program.FoundryRoutedModels);
        Assert.Contains("Phi-4-mini-reasoning", Program.FoundryRoutedModels);
        Assert.Contains("DeepSeek-V3.2", Program.FoundryRoutedModels);
        Assert.Equal(3, Program.FoundryRoutedModels.Length);
    }

    [Fact]
    public void FoundryApiVersion_MatchesAdr005()
    {
        Assert.Equal("2024-05-01-preview", Program.FoundryApiVersion);
    }
}
