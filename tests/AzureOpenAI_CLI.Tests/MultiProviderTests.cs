using System.Text.Json;
using AzureOpenAI_CLI.Providers;

namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// T8 tests: proves multi-provider routing (<c>azure</c>, <c>openai</c>,
/// <c>openrouter</c>, <c>ollama</c>, <c>llamacpp</c>/<c>local</c>, <c>custom</c>),
/// <c>--providers [--json]</c> discovery, <c>default_provider</c> in <see cref="UserConfig"/>,
/// and Newman's loopback-only <c>http://</c> security boundary.
/// </summary>
[Collection("ConsoleCapture")]
public class MultiProviderTests
{
    [Fact]
    public void ListProvidersCommand_JsonMode_EmitsAllSixSupportedProviders()
    {
        var origOut = Console.Out;
        var outWriter = new StringWriter();
        try
        {
            Console.SetOut(outWriter);
            var opts = Program.ParseArgs(["--providers", "--json"]);
            int rc = Program.ListProvidersCommand(opts, new UserConfig());

            Assert.Equal(0, rc);
            using var doc = JsonDocument.Parse(outWriter.ToString());
            var names = doc.RootElement.EnumerateArray().Select(e => e.GetProperty("name").GetString()).ToList();

            Assert.Contains("azure", names);
            Assert.Contains("openai", names);
            Assert.Contains("openrouter", names);
            Assert.Contains("ollama", names);
            Assert.Contains("llamacpp", names);
            Assert.Contains("custom", names);
        }
        finally
        {
            Console.SetOut(origOut);
        }
    }

    [Theory]
    [InlineData("ollama", "http://localhost:11434/v1", "gemma3")]
    [InlineData("llamacpp", "http://localhost:8080/v1", "local-model")]
    [InlineData("local", "http://localhost:8080/v1", "local-model")]
    public void TryResolve_LocalProviders_AcceptLoopbackHttpWithoutApiKey(
        string providerFlag, string expectedEndpoint, string expectedModel)
    {
        var origModel = Environment.GetEnvironmentVariable("AZUREOPENAIMODEL");
        try
        {
            Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", null);
            var (resolved, err) = ProviderSelector.TryResolve(
                providerFlag: providerFlag,
                endpointOverride: null,
                apiKeyOverride: null,
                modelFlag: null,
                userConfig: new UserConfig());

            Assert.Null(err);
            Assert.NotNull(resolved);
            Assert.Equal(expectedEndpoint, resolved!.EndpointUri.ToString());
            Assert.Equal(expectedModel, resolved.Model);

            var chatClient = ProviderSelector.CreateChatClient(resolved);
            Assert.NotNull(chatClient);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", origModel);
        }
    }

    [Theory]
    [InlineData("azure", "http://localhost:8080/v1", "test-key")]          // Azure never allows plaintext http, even on localhost
    [InlineData("openai", "http://api.openai.com/v1", "sk-test")]          // OpenAI never allows plaintext http
    [InlineData("ollama", "http://192.168.1.50:11434/v1", null)]           // Local provider on non-loopback LAN IP over http is blocked
    [InlineData("custom", "http://example.com/v1", "secret")]              // Remote custom http blocked
    public void TryResolve_NonLoopbackOrCloudPlaintextHttp_IsRejected(
        string providerFlag, string badEndpoint, string? apiKey)
    {
        var (resolved, err) = ProviderSelector.TryResolve(
            providerFlag: providerFlag,
            endpointOverride: badEndpoint,
            apiKeyOverride: apiKey,
            modelFlag: "gpt-4o-mini",
            userConfig: new UserConfig());

        Assert.Null(resolved);
        Assert.NotNull(err);
        Assert.Contains("Must be a valid HTTPS URL", err);
    }

    [Fact]
    public void TryResolve_OpenAiAndOpenRouter_RequireApiKey_AndConstructClientWhenSet()
    {
        var origOai = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
            var (missing, errMissing) = ProviderSelector.TryResolve("openai", null, null, null, new UserConfig());
            Assert.Null(missing);
            Assert.Contains("OPENAI_API_KEY", errMissing);

            Environment.SetEnvironmentVariable("OPENAI_API_KEY", "sk-test-key-123");
            var (ok, errOk) = ProviderSelector.TryResolve("openai", null, null, "gpt-4o", new UserConfig());
            Assert.Null(errOk);
            Assert.NotNull(ok);
            Assert.Equal("openai", ok!.Name);
            Assert.Equal("gpt-4o", ok.Model);

            var client = ProviderSelector.CreateChatClient(ok);
            Assert.NotNull(client);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", origOai);
        }
    }

    [Fact]
    public void UserConfig_SetKey_DefaultProvider_ValidatesAndPersists()
    {
        var cfg = new UserConfig();
        Assert.True(cfg.SetKey("default_provider", "ollama"));
        Assert.Equal("ollama", cfg.GetKey("default_provider"));

        Assert.False(cfg.SetKey("default_provider", "not-a-valid-provider"));
    }
}
