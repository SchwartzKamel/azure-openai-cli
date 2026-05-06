using Xunit;
using AzureOpenAI_CLI;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// S03E09 -- OpenAiCompatAdapter (ADR-010). Covers preset resolution,
/// AZ_AI_COMPAT_MODELS parsing, and dispatch routing decisions through
/// Program.BuildChatClient. No real HTTP; all tests are pure / in-process.
/// Uses ConsoleCapture collection because BuildChatClient mutates the env
/// and reads from it -- env-var races bite hard (PreferencesTests precedent).
/// </summary>
[Collection("ConsoleCapture")]
public class OpenAiCompatAdapterTests : IDisposable
{
    private readonly Dictionary<string, string?> _savedEnv = new();

    private static readonly string[] EnvVars =
    {
        "AZ_AI_COMPAT_MODELS",
        "OPENAI_API_KEY",
        "OPENAI_ORG_ID",
        "GROQ_API_KEY",
        "TOGETHER_API_KEY",
        "CLOUDFLARE_API_TOKEN",
        "CLOUDFLARE_ACCOUNT_ID",
        "AZURE_FOUNDRY_ENDPOINT",
        "AZURE_FOUNDRY_KEY",
        "AZURE_FOUNDRY_MODELS",
    };

    public OpenAiCompatAdapterTests()
    {
        foreach (var v in EnvVars)
            _savedEnv[v] = Environment.GetEnvironmentVariable(v);
        // Start from a clean slate.
        foreach (var v in EnvVars)
            Environment.SetEnvironmentVariable(v, null);
    }

    public void Dispose()
    {
        foreach (var kv in _savedEnv)
            Environment.SetEnvironmentVariable(kv.Key, kv.Value);
    }

    // ── Preset resolution ─────────────────────────────────────────────

    [Theory]
    [InlineData("openai")]
    [InlineData("OpenAI")]
    [InlineData("OPENAI")]
    [InlineData("groq")]
    [InlineData("together")]
    [InlineData("Cloudflare")]
    public void Resolve_KnownPreset_CaseInsensitive(string name)
    {
        var preset = OpenAiCompatAdapter.Resolve(name);
        Assert.NotNull(preset);
        Assert.Equal(name.ToLowerInvariant(), preset!.Name, ignoreCase: true);
    }

    [Fact]
    public void Resolve_UnknownPreset_ReturnsNull()
    {
        Assert.Null(OpenAiCompatAdapter.Resolve("anthropic"));
    }

    [Fact]
    public void Resolve_EmptyOrNull_ReturnsNull()
    {
        Assert.Null(OpenAiCompatAdapter.Resolve(""));
        Assert.Null(OpenAiCompatAdapter.Resolve("   "));
    }

    [Fact]
    public void ResolveOrThrow_UnknownPreset_ThrowsActionable()
    {
        var ex = Assert.Throws<ArgumentException>(() => OpenAiCompatAdapter.ResolveOrThrow("bedrock"));
        Assert.Contains("Unknown OpenAI-compatible preset", ex.Message, StringComparison.Ordinal);
        // Actionable: lists every known preset name.
        Assert.Contains("openai", ex.Message, StringComparison.Ordinal);
        Assert.Contains("groq", ex.Message, StringComparison.Ordinal);
        Assert.Contains("together", ex.Message, StringComparison.Ordinal);
        Assert.Contains("cloudflare", ex.Message, StringComparison.Ordinal);
        // Names the env var so the operator knows where to fix it.
        Assert.Contains("AZ_AI_COMPAT_MODELS", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuiltIn_OpenAiPreset_HasExpectedShape()
    {
        var p = OpenAiCompatAdapter.BuiltIn["openai"];
        Assert.Equal("https://api.openai.com/v1", p.BaseUrl.OriginalString);
        Assert.Equal("OPENAI_API_KEY", p.ApiKeyEnvVar);
        Assert.Equal("OPENAI_ORG_ID", p.OrgEnvVar);
        Assert.Equal("Bearer", p.AuthScheme);
    }

    [Fact]
    public void BuiltIn_GroqPreset_HasExpectedShape()
    {
        var p = OpenAiCompatAdapter.BuiltIn["groq"];
        Assert.Equal("https://api.groq.com/openai/v1", p.BaseUrl.OriginalString);
        Assert.Equal("GROQ_API_KEY", p.ApiKeyEnvVar);
        Assert.Null(p.OrgEnvVar);
    }

    [Fact]
    public void BuiltIn_TogetherPreset_HasExpectedShape()
    {
        var p = OpenAiCompatAdapter.BuiltIn["together"];
        Assert.Equal("https://api.together.xyz/v1", p.BaseUrl.OriginalString);
        Assert.Equal("TOGETHER_API_KEY", p.ApiKeyEnvVar);
    }

    [Fact]
    public void BuiltIn_CloudflarePreset_HasAccountIdPlaceholder()
    {
        // Cloudflare is the "needs-account-id" preset -- documented in the
        // adapter, so the placeholder URL is the contract.
        var p = OpenAiCompatAdapter.BuiltIn["cloudflare"];
        Assert.Contains("{account_id}", p.BaseUrl.OriginalString, StringComparison.Ordinal);
        Assert.Equal("CLOUDFLARE_API_TOKEN", p.ApiKeyEnvVar);
    }

    // ── AZ_AI_COMPAT_MODELS parsing ───────────────────────────────────

    [Fact]
    public void ParseCompatModels_Null_ReturnsNull()
    {
        Assert.Null(OpenAiCompatAdapter.ParseCompatModels(null));
    }

    [Fact]
    public void ParseCompatModels_Empty_ReturnsNull()
    {
        Assert.Null(OpenAiCompatAdapter.ParseCompatModels(""));
        Assert.Null(OpenAiCompatAdapter.ParseCompatModels("   "));
    }

    [Fact]
    public void ParseCompatModels_Single_ReturnsOnePair()
    {
        var map = OpenAiCompatAdapter.ParseCompatModels("openai:gpt-4o-mini");
        Assert.NotNull(map);
        Assert.Single(map!);
        Assert.Equal("openai", map!["gpt-4o-mini"]);
    }

    [Fact]
    public void ParseCompatModels_Multiple_ParsesAll()
    {
        var map = OpenAiCompatAdapter.ParseCompatModels("openai:gpt-4o-mini,groq:llama-3.1-70b,together:mixtral-8x7b");
        Assert.NotNull(map);
        Assert.Equal(3, map!.Count);
        Assert.Equal("openai", map["gpt-4o-mini"]);
        Assert.Equal("groq", map["llama-3.1-70b"]);
        Assert.Equal("together", map["mixtral-8x7b"]);
    }

    [Fact]
    public void ParseCompatModels_TolerantToWhitespace()
    {
        var map = OpenAiCompatAdapter.ParseCompatModels("  openai : gpt-4o-mini  ,  groq:llama-3.1-70b  ");
        Assert.NotNull(map);
        Assert.Equal("openai", map!["gpt-4o-mini"]);
        Assert.Equal("groq", map["llama-3.1-70b"]);
    }

    [Fact]
    public void ParseCompatModels_LookupIsCaseInsensitive()
    {
        var map = OpenAiCompatAdapter.ParseCompatModels("openai:GPT-4o-Mini");
        Assert.NotNull(map);
        Assert.True(map!.ContainsKey("gpt-4o-mini"));
        Assert.True(map.ContainsKey("GPT-4O-MINI"));
    }

    [Theory]
    [InlineData("no-colon-here")]
    [InlineData(":model-only")]
    [InlineData("preset-only:")]
    [InlineData("openai:gpt-4o,broken-entry")]
    public void ParseCompatModels_Malformed_Throws(string raw)
    {
        var ex = Assert.Throws<ArgumentException>(() => OpenAiCompatAdapter.ParseCompatModels(raw));
        Assert.Contains("AZ_AI_COMPAT_MODELS", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseCompatModels_EmptyEntriesAreSkipped()
    {
        // Trailing/leading commas are tolerated (RemoveEmptyEntries).
        var map = OpenAiCompatAdapter.ParseCompatModels(",openai:gpt-4o-mini,,groq:llama-3.1-70b,");
        Assert.NotNull(map);
        Assert.Equal(2, map!.Count);
    }

    [Fact]
    public void ParseCompatModelsFromEnv_ReadsEnv()
    {
        Environment.SetEnvironmentVariable("AZ_AI_COMPAT_MODELS", "openai:gpt-4o-mini");
        var map = OpenAiCompatAdapter.ParseCompatModelsFromEnv();
        Assert.NotNull(map);
        Assert.Equal("openai", map!["gpt-4o-mini"]);
    }

    [Fact]
    public void ParseCompatModelsFromEnv_Unset_ReturnsNull()
    {
        Environment.SetEnvironmentVariable("AZ_AI_COMPAT_MODELS", null);
        Assert.Null(OpenAiCompatAdapter.ParseCompatModelsFromEnv());
    }

    // ── Build() behaviour ─────────────────────────────────────────────

    [Fact]
    public void Build_MissingApiKey_Throws()
    {
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
        var preset = OpenAiCompatAdapter.BuiltIn["openai"];
        var ex = Assert.Throws<InvalidOperationException>(
            () => OpenAiCompatAdapter.Build("gpt-4o-mini", preset));
        Assert.Contains("OPENAI_API_KEY", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_BlankModel_Throws()
    {
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "sk-test");
        var preset = OpenAiCompatAdapter.BuiltIn["openai"];
        Assert.Throws<ArgumentException>(() => OpenAiCompatAdapter.Build("   ", preset));
    }

    [Fact]
    public void Build_OpenAi_WithKey_ReturnsClient()
    {
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "sk-test-key");
        var preset = OpenAiCompatAdapter.BuiltIn["openai"];
        var client = OpenAiCompatAdapter.Build("gpt-4o-mini", preset);
        Assert.NotNull(client);
    }

    [Fact]
    public void Build_OpenAi_WithOrg_ReturnsClient()
    {
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "sk-test-key");
        Environment.SetEnvironmentVariable("OPENAI_ORG_ID", "org-abc123");
        var preset = OpenAiCompatAdapter.BuiltIn["openai"];
        var client = OpenAiCompatAdapter.Build("gpt-4o-mini", preset);
        Assert.NotNull(client);
    }

    [Fact]
    public void Build_Groq_NoOrg_ReturnsClient()
    {
        Environment.SetEnvironmentVariable("GROQ_API_KEY", "gsk-test");
        var preset = OpenAiCompatAdapter.BuiltIn["groq"];
        var client = OpenAiCompatAdapter.Build("llama-3.1-70b", preset);
        Assert.NotNull(client);
    }

    [Fact]
    public void Build_Cloudflare_MissingAccountId_Throws()
    {
        Environment.SetEnvironmentVariable("CLOUDFLARE_API_TOKEN", "cf-test");
        Environment.SetEnvironmentVariable("CLOUDFLARE_ACCOUNT_ID", null);
        var preset = OpenAiCompatAdapter.BuiltIn["cloudflare"];
        var ex = Assert.Throws<InvalidOperationException>(
            () => OpenAiCompatAdapter.Build("@cf/meta/llama-3-8b-instruct", preset));
        Assert.Contains("CLOUDFLARE_ACCOUNT_ID", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_Cloudflare_WithAccountId_ReturnsClient()
    {
        Environment.SetEnvironmentVariable("CLOUDFLARE_API_TOKEN", "cf-test");
        Environment.SetEnvironmentVariable("CLOUDFLARE_ACCOUNT_ID", "abc123");
        var preset = OpenAiCompatAdapter.BuiltIn["cloudflare"];
        var client = OpenAiCompatAdapter.Build("@cf/meta/llama-3-8b-instruct", preset);
        Assert.NotNull(client);
    }

    // ── BuildChatClient dispatch routing (S03E09 wiring) ─────────────

    [Fact]
    public void BuildChatClient_CompatModelMatch_RoutesThroughAdapter()
    {
        Environment.SetEnvironmentVariable("AZ_AI_COMPAT_MODELS", "openai:gpt-4o-mini");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "sk-test");

        // The Azure endpoint here would be invalid for AOAI use, but because
        // the model matches the compat allowlist we never touch that path.
        var result = Program.BuildChatClient(
            "https://test.openai.azure.com/", "azure-key", "gpt-4o-mini", jsonMode: false);
        Assert.NotNull(result);
    }

    [Fact]
    public void BuildChatClient_CompatModelMatch_MissingKey_ReturnsNull()
    {
        Environment.SetEnvironmentVariable("AZ_AI_COMPAT_MODELS", "openai:gpt-4o-mini");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);

        // ErrorAndExit emits a [ERROR] line and the function returns null.
        var sw = new StringWriter();
        var prevErr = Console.Error;
        Console.SetError(sw);
        try
        {
            var result = Program.BuildChatClient(
                "https://test.openai.azure.com/", "azure-key", "gpt-4o-mini", jsonMode: false);
            Assert.Null(result);
        }
        finally
        {
            Console.SetError(prevErr);
        }
        Assert.Contains("OPENAI_API_KEY", sw.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void BuildChatClient_CompatModelMiss_FallsThroughToAzure()
    {
        // gpt-4o-mini is in the allowlist; gpt-4o is NOT, so the latter must
        // fall through to the default Azure path.
        Environment.SetEnvironmentVariable("AZ_AI_COMPAT_MODELS", "openai:gpt-4o-mini");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "sk-test");

        var result = Program.BuildChatClient(
            "https://test.openai.azure.com/", "azure-key", "gpt-4o", jsonMode: false);
        Assert.NotNull(result);
    }

    [Fact]
    public void BuildChatClient_FoundryWinsOverCompat()
    {
        // Both allowlists name the same model -- Foundry (priority 1) wins.
        Environment.SetEnvironmentVariable("AZURE_FOUNDRY_ENDPOINT", "https://foundry.ai.azure.com/");
        Environment.SetEnvironmentVariable("AZURE_FOUNDRY_KEY", "foundry-key");
        Environment.SetEnvironmentVariable("AZURE_FOUNDRY_MODELS", "shared-model");

        Environment.SetEnvironmentVariable("AZ_AI_COMPAT_MODELS", "openai:shared-model");
        // Deliberately leave OPENAI_API_KEY unset. If compat won, this would
        // surface as an [ERROR] / null. If Foundry wins (correct), it succeeds.
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);

        var result = Program.BuildChatClient(
            "https://test.openai.azure.com/", "azure-key", "shared-model", jsonMode: false);
        Assert.NotNull(result);
    }

    [Fact]
    public void BuildChatClient_CompatMalformedEnv_ReturnsNull()
    {
        Environment.SetEnvironmentVariable("AZ_AI_COMPAT_MODELS", "no-colon-here");

        var sw = new StringWriter();
        var prevErr = Console.Error;
        Console.SetError(sw);
        try
        {
            var result = Program.BuildChatClient(
                "https://test.openai.azure.com/", "azure-key", "gpt-4o", jsonMode: false);
            Assert.Null(result);
        }
        finally
        {
            Console.SetError(prevErr);
        }
        Assert.Contains("AZ_AI_COMPAT_MODELS", sw.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void BuildChatClient_CompatUnknownPreset_ReturnsNull()
    {
        Environment.SetEnvironmentVariable("AZ_AI_COMPAT_MODELS", "bedrock:claude-3-haiku");

        var sw = new StringWriter();
        var prevErr = Console.Error;
        Console.SetError(sw);
        try
        {
            var result = Program.BuildChatClient(
                "https://test.openai.azure.com/", "azure-key", "claude-3-haiku", jsonMode: false);
            Assert.Null(result);
        }
        finally
        {
            Console.SetError(prevErr);
        }
        Assert.Contains("Unknown OpenAI-compatible preset", sw.ToString(), StringComparison.Ordinal);
    }
}
