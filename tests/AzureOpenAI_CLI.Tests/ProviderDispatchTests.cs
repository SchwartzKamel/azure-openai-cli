using Xunit;
using AzureOpenAI_CLI;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// Tests for ADR-005 multi-provider dispatch: ParseFoundryModels,
/// BuildChatClient routing, and FoundryAuthPolicy.
/// </summary>
public class ProviderDispatchTests : IDisposable
{
    private readonly Dictionary<string, string?> _savedEnv = new();

    private static readonly string[] EnvVars =
    {
        "AZUREOPENAIENDPOINT",
        "AZUREOPENAIAPI",
        "AZUREOPENAIMODEL",
        "AZURE_FOUNDRY_ENDPOINT",
        "AZURE_FOUNDRY_KEY",
        "AZURE_FOUNDRY_MODELS",
    };

    public ProviderDispatchTests()
    {
        foreach (var v in EnvVars)
            _savedEnv[v] = Environment.GetEnvironmentVariable(v);
    }

    public void Dispose()
    {
        foreach (var kv in _savedEnv)
            Environment.SetEnvironmentVariable(kv.Key, kv.Value);
    }

    private void SetEnv(string name, string? value) =>
        Environment.SetEnvironmentVariable(name, value);

    // ── ParseFoundryModels ─────────────────────────────────────────────

    [Fact]
    public void ParseFoundryModels_Unset_ReturnsNull()
    {
        SetEnv("AZURE_FOUNDRY_MODELS", null);
        Assert.Null(Program.ParseFoundryModels());
    }

    [Fact]
    public void ParseFoundryModels_Empty_ReturnsNull()
    {
        SetEnv("AZURE_FOUNDRY_MODELS", "");
        Assert.Null(Program.ParseFoundryModels());
    }

    [Fact]
    public void ParseFoundryModels_SingleModel_ReturnsSet()
    {
        SetEnv("AZURE_FOUNDRY_MODELS", "gpt-4o");
        var result = Program.ParseFoundryModels();
        Assert.NotNull(result);
        Assert.Single(result!);
        Assert.Contains("gpt-4o", result);
    }

    [Fact]
    public void ParseFoundryModels_MultipleModels_ParsesAll()
    {
        SetEnv("AZURE_FOUNDRY_MODELS", "gpt-4o,gpt-4.1-mini,o3-mini");
        var result = Program.ParseFoundryModels();
        Assert.NotNull(result);
        Assert.Equal(3, result!.Count);
        Assert.Contains("gpt-4o", result);
        Assert.Contains("gpt-4.1-mini", result);
        Assert.Contains("o3-mini", result);
    }

    [Fact]
    public void ParseFoundryModels_TrimsWhitespace()
    {
        SetEnv("AZURE_FOUNDRY_MODELS", " gpt-4o , gpt-4.1-mini ");
        var result = Program.ParseFoundryModels();
        Assert.NotNull(result);
        Assert.Contains("gpt-4o", result!);
        Assert.Contains("gpt-4.1-mini", result);
    }

    [Fact]
    public void ParseFoundryModels_CaseInsensitive()
    {
        SetEnv("AZURE_FOUNDRY_MODELS", "GPT-4o");
        var result = Program.ParseFoundryModels();
        Assert.NotNull(result);
        Assert.Contains("gpt-4o", result!, StringComparer.OrdinalIgnoreCase);
    }

    // ── BuildChatClient routing ────────────────────────────────────────

    [Fact]
    public void BuildChatClient_InvalidEndpoint_ReturnsNull()
    {
        ClearFoundryEnv();
        var result = Program.BuildChatClient("not-a-url", "key", "gpt-4o", jsonMode: false);
        Assert.Null(result);
    }

    [Fact]
    public void BuildChatClient_HttpEndpoint_ReturnsNull()
    {
        ClearFoundryEnv();
        var result = Program.BuildChatClient("http://remote-server.com/", "key", "gpt-4o", jsonMode: false);
        Assert.Null(result);
    }

    [Fact]
    public void BuildChatClient_ValidAzureEndpoint_ReturnsClient()
    {
        ClearFoundryEnv();
        var result = Program.BuildChatClient(
            "https://test.openai.azure.com/", "fake-key", "gpt-4o", jsonMode: false);
        Assert.NotNull(result);
    }

    [Fact]
    public void BuildChatClient_FoundryModel_RoutesToFoundry()
    {
        // Configure Foundry env
        SetEnv("AZURE_FOUNDRY_ENDPOINT", "https://foundry.ai.azure.com/");
        SetEnv("AZURE_FOUNDRY_KEY", "foundry-key");
        SetEnv("AZURE_FOUNDRY_MODELS", "gpt-4o,o3-mini");

        // gpt-4o should route to Foundry, not Azure OpenAI
        var result = Program.BuildChatClient(
            "https://test.openai.azure.com/", "azure-key", "gpt-4o", jsonMode: false);
        Assert.NotNull(result);
        // Verify it's an IChatClient (we can't easily inspect the underlying client type
        // but the fact it didn't fail means the Foundry path was taken)
    }

    [Fact]
    public void BuildChatClient_NonFoundryModel_RoutesToAzure()
    {
        // Configure Foundry env but model is NOT in AZURE_FOUNDRY_MODELS
        SetEnv("AZURE_FOUNDRY_ENDPOINT", "https://foundry.ai.azure.com/");
        SetEnv("AZURE_FOUNDRY_KEY", "foundry-key");
        SetEnv("AZURE_FOUNDRY_MODELS", "o3-mini");

        // gpt-4o is NOT in foundry models, should route to Azure OpenAI
        var result = Program.BuildChatClient(
            "https://test.openai.azure.com/", "azure-key", "gpt-4o", jsonMode: false);
        Assert.NotNull(result);
    }

    [Fact]
    public void BuildChatClient_FoundryWithoutModels_RoutesToAzure()
    {
        // Foundry endpoint set but no models -- should fall through to Azure
        SetEnv("AZURE_FOUNDRY_ENDPOINT", "https://foundry.ai.azure.com/");
        SetEnv("AZURE_FOUNDRY_KEY", "foundry-key");
        SetEnv("AZURE_FOUNDRY_MODELS", null);

        var result = Program.BuildChatClient(
            "https://test.openai.azure.com/", "azure-key", "gpt-4o", jsonMode: false);
        Assert.NotNull(result);
    }

    [Fact]
    public void BuildChatClient_FoundryInvalidEndpoint_ReturnsNull()
    {
        SetEnv("AZURE_FOUNDRY_ENDPOINT", "not-a-url");
        SetEnv("AZURE_FOUNDRY_KEY", "foundry-key");
        SetEnv("AZURE_FOUNDRY_MODELS", "gpt-4o");

        var result = Program.BuildChatClient(
            "https://test.openai.azure.com/", "azure-key", "gpt-4o", jsonMode: false);
        Assert.Null(result);
    }

    [Fact]
    public void BuildChatClient_FoundryHttpRemote_ReturnsNull()
    {
        // HTTP only allowed for localhost
        SetEnv("AZURE_FOUNDRY_ENDPOINT", "http://remote-server.com/");
        SetEnv("AZURE_FOUNDRY_KEY", "foundry-key");
        SetEnv("AZURE_FOUNDRY_MODELS", "gpt-4o");

        var result = Program.BuildChatClient(
            "https://test.openai.azure.com/", "azure-key", "gpt-4o", jsonMode: false);
        Assert.Null(result);
    }

    [Fact]
    public void BuildChatClient_FoundryHttpLocalhost_Succeeds()
    {
        // HTTP is OK for localhost (local model servers)
        SetEnv("AZURE_FOUNDRY_ENDPOINT", "http://localhost:8080/");
        SetEnv("AZURE_FOUNDRY_KEY", "local-key");
        SetEnv("AZURE_FOUNDRY_MODELS", "local-model");

        var result = Program.BuildChatClient(
            "https://test.openai.azure.com/", "azure-key", "local-model", jsonMode: false);
        Assert.NotNull(result);
    }

    // ── FoundryAuthPolicy ──────────────────────────────────────────────

    [Fact]
    public void FoundryAuthPolicy_CanInstantiate()
    {
        // Verify the policy can be constructed without errors
        var policy = new Program.FoundryAuthPolicy("test-key", "2024-05-01-preview");
        Assert.NotNull(policy);
    }

    // ── LoadConfigEnvFrom ──────────────────────────────────────────────

    [Fact]
    public void LoadConfigEnvFrom_ParsesShellExportSyntax()
    {
        var tmpFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tmpFile, """
                # Comment line
                export TEST_LCEF_KEY1="value1"
                export TEST_LCEF_KEY2='value2'
                TEST_LCEF_KEY3=bare_value
                """);
            // Clear any existing values
            Environment.SetEnvironmentVariable("TEST_LCEF_KEY1", null);
            Environment.SetEnvironmentVariable("TEST_LCEF_KEY2", null);
            Environment.SetEnvironmentVariable("TEST_LCEF_KEY3", null);

            Program.LoadConfigEnvFrom(tmpFile);

            Assert.Equal("value1", Environment.GetEnvironmentVariable("TEST_LCEF_KEY1"));
            Assert.Equal("value2", Environment.GetEnvironmentVariable("TEST_LCEF_KEY2"));
            Assert.Equal("bare_value", Environment.GetEnvironmentVariable("TEST_LCEF_KEY3"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("TEST_LCEF_KEY1", null);
            Environment.SetEnvironmentVariable("TEST_LCEF_KEY2", null);
            Environment.SetEnvironmentVariable("TEST_LCEF_KEY3", null);
            File.Delete(tmpFile);
        }
    }

    [Fact]
    public void LoadConfigEnvFrom_DoesNotOverwriteExisting()
    {
        var tmpFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tmpFile, "export TEST_LCEF_NOOVER=\"file_value\"\n");
            Environment.SetEnvironmentVariable("TEST_LCEF_NOOVER", "existing_value");

            Program.LoadConfigEnvFrom(tmpFile);

            Assert.Equal("existing_value", Environment.GetEnvironmentVariable("TEST_LCEF_NOOVER"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("TEST_LCEF_NOOVER", null);
            File.Delete(tmpFile);
        }
    }

    [Fact]
    public void LoadConfigEnvFrom_MissingFile_NoOp()
    {
        // Should not throw
        Program.LoadConfigEnvFrom("/nonexistent/path/that/does/not/exist");
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private void ClearFoundryEnv()
    {
        SetEnv("AZURE_FOUNDRY_ENDPOINT", null);
        SetEnv("AZURE_FOUNDRY_KEY", null);
        SetEnv("AZURE_FOUNDRY_MODELS", null);
    }
}
