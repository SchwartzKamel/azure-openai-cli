using Xunit;
using AzureOpenAI_CLI;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// Tests for the AZUREOPENAIMODEL comma-separated allowlist feature.
/// ParseModelEnv reads the env var and returns (default, allowed set).
/// When multiple models are listed, the resolved model must be in the set.
/// </summary>
public class ModelAllowlistTests : IDisposable
{
    private readonly string? _originalEnv;

    public ModelAllowlistTests()
    {
        _originalEnv = Environment.GetEnvironmentVariable("AZUREOPENAIMODEL");
    }

    public void Dispose()
    {
        if (_originalEnv != null)
            Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", _originalEnv);
        else
            Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", null);
    }

    // ── ParseModelEnv ──────────────────────────────────────────────────

    [Fact]
    public void ParseModelEnv_Unset_ReturnsNulls()
    {
        Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", null);
        var (defaultModel, allowed) = Program.ParseModelEnv();
        Assert.Null(defaultModel);
        Assert.Null(allowed);
    }

    [Fact]
    public void ParseModelEnv_Empty_ReturnsNulls()
    {
        Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", "");
        var (defaultModel, allowed) = Program.ParseModelEnv();
        Assert.Null(defaultModel);
        Assert.Null(allowed);
    }

    [Fact]
    public void ParseModelEnv_SingleModel_ReturnsDefaultOnly()
    {
        Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", "gpt-4o-mini");
        var (defaultModel, allowed) = Program.ParseModelEnv();
        Assert.Equal("gpt-4o-mini", defaultModel);
        Assert.Null(allowed); // No restriction when only one model
    }

    [Fact]
    public void ParseModelEnv_MultipleModels_ReturnsDefaultAndSet()
    {
        Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", "gpt-5.4-nano,gpt-4o,gpt-4o-mini");
        var (defaultModel, allowed) = Program.ParseModelEnv();
        Assert.Equal("gpt-5.4-nano", defaultModel);
        Assert.NotNull(allowed);
        Assert.Equal(3, allowed!.Count);
        Assert.Contains("gpt-5.4-nano", allowed);
        Assert.Contains("gpt-4o", allowed);
        Assert.Contains("gpt-4o-mini", allowed);
    }

    [Fact]
    public void ParseModelEnv_TrimsWhitespace()
    {
        Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", " gpt-5.4-nano , gpt-4o , gpt-4o-mini ");
        var (defaultModel, allowed) = Program.ParseModelEnv();
        Assert.Equal("gpt-5.4-nano", defaultModel);
        Assert.NotNull(allowed);
        Assert.Contains("gpt-4o", allowed!);
    }

    [Fact]
    public void ParseModelEnv_CaseInsensitiveLookup()
    {
        Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", "GPT-4o,gpt-4o-mini");
        var (_, allowed) = Program.ParseModelEnv();
        Assert.NotNull(allowed);
        Assert.Contains("gpt-4o", allowed!);
        Assert.Contains("GPT-4O", allowed!);
    }

    [Fact]
    public void ParseModelEnv_IgnoresEmptyEntries()
    {
        Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", "gpt-4o,,gpt-4o-mini,");
        var (defaultModel, allowed) = Program.ParseModelEnv();
        Assert.Equal("gpt-4o", defaultModel);
        Assert.NotNull(allowed);
        Assert.Equal(2, allowed!.Count);
    }

    // ── ListModelsCommand shows env allowlist ──────────────────────────

    [Fact]
    public void ListModelsCommand_ShowsEnvAllowlist()
    {
        Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", "gpt-5.4-nano,gpt-4o");
        var config = new UserConfig();
        var stdout = new StringWriter();
        Console.SetOut(stdout);
        try
        {
            Program.ListModelsCommand(config);
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        }
        var output = stdout.ToString();
        Assert.Contains("Allowed models", output);
        Assert.Contains("gpt-5.4-nano", output);
        Assert.Contains("gpt-4o", output);
    }

    [Fact]
    public void ListModelsCommand_WarnsAliasNotInAllowlist()
    {
        Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", "gpt-5.4-nano,gpt-4o");
        var config = new UserConfig();
        config.Models["test"] = "not-allowed-model";
        var stdout = new StringWriter();
        Console.SetOut(stdout);
        try
        {
            Program.ListModelsCommand(config);
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        }
        var output = stdout.ToString();
        Assert.Contains("NOT IN ALLOWLIST", output);
    }
}
