using System.IO;
using AzureOpenAI_CLI;

namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// Tests for the first-run setup wizard: CLI flag plumbing, UserConfig
/// round-trip of the new credential fields, and redaction of the API key
/// in <see cref="UserConfig.ListKeys"/>.
/// </summary>
public class SetupWizardTests
{
    [Fact]
    public void ParseArgs_SetupFlag_IsRecognized()
    {
        var opts = Program.ParseArgs(["--setup"]);

        Assert.True(opts.Setup);
        Assert.False(opts.ParseError);
    }

    [Fact]
    public void ParseArgs_InitWizardAlias_IsRecognized()
    {
        var opts = Program.ParseArgs(["--init-wizard"]);

        Assert.True(opts.Setup);
        Assert.False(opts.ParseError);
    }

    [Fact]
    public void ParseArgs_NoSetupFlag_DefaultsToFalse()
    {
        var opts = Program.ParseArgs([]);
        Assert.False(opts.Setup);
    }

    [Fact]
    public void UserConfig_EndpointAndApiKey_RoundTrip()
    {
        var path = Path.Combine(Path.GetTempPath(), $"az-ai-wizard-rt-{Guid.NewGuid():N}.json");
        try
        {
            var cfg = new UserConfig
            {
                Endpoint = "https://example.openai.azure.com",
                ApiKey = "sk-test-0123456789abcdef-0123456789abcdef",
                DefaultModel = "default",
            };
            cfg.Models["default"] = "gpt-4o-mini";
            cfg.Save(path);

            var loaded = UserConfig.Load(path);

            Assert.Equal("https://example.openai.azure.com", loaded.Endpoint);
            Assert.Equal("sk-test-0123456789abcdef-0123456789abcdef", loaded.ApiKey);
            Assert.Equal("default", loaded.DefaultModel);
            Assert.Equal("gpt-4o-mini", loaded.Models["default"]);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void UserConfig_ListKeys_RedactsApiKey()
    {
        var cfg = new UserConfig
        {
            Endpoint = "https://example.openai.azure.com",
            ApiKey = "this-should-never-appear-in-output",
            DefaultModel = "default",
        };
        cfg.Models["default"] = "gpt-4o-mini";

        var keys = cfg.ListKeys().ToList();

        Assert.Contains("endpoint=https://example.openai.azure.com", keys);
        Assert.Contains("api_key=<redacted>", keys);
        Assert.DoesNotContain(keys, line => line.Contains("this-should-never-appear"));
    }

    [Fact]
    public void UserConfig_SetKey_AcceptsEndpointAndApiKey()
    {
        var cfg = new UserConfig();

        Assert.True(cfg.SetKey("endpoint", "https://example.openai.azure.com"));
        Assert.True(cfg.SetKey("api_key", "abcd1234"));

        Assert.Equal("https://example.openai.azure.com", cfg.Endpoint);
        Assert.Equal("abcd1234", cfg.ApiKey);
    }

    [Fact]
    public void UserConfig_GetKey_ReturnsEndpointAndApiKey()
    {
        var cfg = new UserConfig
        {
            Endpoint = "https://example.openai.azure.com",
            ApiKey = "topsecret",
        };

        Assert.Equal("https://example.openai.azure.com", cfg.GetKey("endpoint"));
        Assert.Equal("topsecret", cfg.GetKey("api_key"));
    }
}
