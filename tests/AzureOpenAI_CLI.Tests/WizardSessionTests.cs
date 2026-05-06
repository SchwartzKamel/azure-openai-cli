using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using AzureOpenAI_CLI;
using Xunit;

namespace AzureOpenAI_CLI.Tests;

// S03E11 -- The Wizard, Reprise (Jerry).
//
// Hermetic tests for the prompt-sequence builder. No Console, no stdin,
// no real env file. The interactive layer (SetupWizard.RunAsync) drives
// these pure functions via canned ProviderAnswer records; tests do the
// same and assert on the resulting bytes.
[Collection("ConsoleCapture")]
public class WizardSessionTests : IDisposable
{
    private readonly string _tmpDir;
    private static readonly DateTimeOffset _ts = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    public WizardSessionTests()
    {
        _tmpDir = Path.Combine(Path.GetTempPath(),
            "az-ai-wizard-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tmpDir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tmpDir)) Directory.Delete(_tmpDir, recursive: true); }
        catch { /* best effort */ }
    }

    // ── Provider canonicalisation ─────────────────────────────────────────

    [Theory]
    [InlineData("azure", "azure")]
    [InlineData("AZURE", "azure")]
    [InlineData("OpenAI", "openai")]
    [InlineData(" groq ", "groq")]
    [InlineData("Together", "together")]
    [InlineData("CloudFlare", "cloudflare")]
    public void TryCanonicalize_KnownProvider_Lowercases(string input, string expected)
    {
        Assert.True(WizardProviders.TryCanonicalize(input, out var canon));
        Assert.Equal(expected, canon);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ollama")]
    [InlineData("foundry")] // foundry is an env-section provider but NOT a wizard option
    public void TryCanonicalize_UnknownProvider_ReturnsFalse(string input)
    {
        Assert.False(WizardProviders.TryCanonicalize(input, out _));
    }

    [Fact]
    public void IsCompat_AzureFalse_OthersTrue()
    {
        Assert.False(WizardProviders.IsCompat("azure"));
        Assert.True(WizardProviders.IsCompat("openai"));
        Assert.True(WizardProviders.IsCompat("groq"));
        Assert.True(WizardProviders.IsCompat("together"));
        Assert.True(WizardProviders.IsCompat("cloudflare"));
    }

    // ── ValidateCompatModels (delegates to E09 ParseCompatModels) ─────────

    [Theory]
    [InlineData("openai", "gpt-4o")]
    [InlineData("openai", "gpt-4o,gpt-4o-mini")]
    [InlineData("groq", "llama3-70b-8192")]
    [InlineData("together", "meta-llama/Meta-Llama-3.1-8B-Instruct-Turbo")]
    public void ValidateCompatModels_Valid_ReturnsNull(string provider, string models)
    {
        Assert.Null(WizardSession.ValidateCompatModels(provider, models));
    }

    [Theory]
    [InlineData("openai", "")]
    [InlineData("openai", "  ")]
    public void ValidateCompatModels_Empty_ReturnsRejection(string provider, string models)
    {
        var rej = WizardSession.ValidateCompatModels(provider, models);
        Assert.NotNull(rej);
        Assert.Contains("required", rej, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateCompatModels_Azure_AlwaysOk()
    {
        // Azure is not validated through ParseCompatModels.
        Assert.Null(WizardSession.ValidateCompatModels("azure", "gpt-4o-mini"));
    }

    // ── BuildEnvFileContent shape ─────────────────────────────────────────

    [Fact]
    public void BuildEnvFileContent_AzureOnly_EmitsDefaultSectionExports()
    {
        var answers = new List<ProviderAnswer>
        {
            new(WizardProviders.Azure, "azkey-0123456789abcdef",
                "gpt-4o-mini,gpt-4o", Endpoint: "https://x.openai.azure.com"),
        };
        var s = WizardSession.BuildEnvFileContent(answers, "azure", _ts);

        Assert.Contains("export AZUREOPENAIENDPOINT=\"https://x.openai.azure.com\"", s, StringComparison.Ordinal);
        Assert.Contains("export AZUREOPENAIAPI=\"azkey-0123456789abcdef\"", s, StringComparison.Ordinal);
        Assert.Contains("export AZUREOPENAIMODEL=\"gpt-4o-mini,gpt-4o\"", s, StringComparison.Ordinal);
        Assert.DoesNotContain("AZ_AI_COMPAT_MODELS", s, StringComparison.Ordinal);
        Assert.DoesNotContain("[provider:azure]", s, StringComparison.Ordinal);
        Assert.Contains("# Default provider: azure", s, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildEnvFileContent_OpenAIOnly_EmitsCompatAndProviderSection()
    {
        var answers = new List<ProviderAnswer>
        {
            new(WizardProviders.OpenAI, "sk-test-0123456789abcdef",
                "gpt-4o-mini,gpt-4o"),
        };
        var s = WizardSession.BuildEnvFileContent(answers, "openai", _ts);

        Assert.Contains("export AZ_AI_COMPAT_MODELS=\"openai:gpt-4o-mini,openai:gpt-4o\"", s, StringComparison.Ordinal);
        Assert.Contains("[provider:openai]", s, StringComparison.Ordinal);
        Assert.Contains("API_KEY=sk-test-0123456789abcdef", s, StringComparison.Ordinal);
        Assert.DoesNotContain("AZUREOPENAIENDPOINT", s, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildEnvFileContent_Cloudflare_UsesApiTokenAndAccountId()
    {
        var answers = new List<ProviderAnswer>
        {
            new(WizardProviders.Cloudflare, "cf-token-0123456789abcdef",
                "@cf/meta/llama-3.1-8b-instruct", AccountId: "acct-abc-123"),
        };
        var s = WizardSession.BuildEnvFileContent(answers, "cloudflare", _ts);

        // Cloudflare's E09 preset reads CLOUDFLARE_API_TOKEN, not _API_KEY,
        // so the section uses bare "API_TOKEN" (loader namespaces it).
        Assert.Contains("[provider:cloudflare]", s, StringComparison.Ordinal);
        Assert.Contains("API_TOKEN=cf-token-0123456789abcdef", s, StringComparison.Ordinal);
        Assert.Contains("export CLOUDFLARE_ACCOUNT_ID=\"acct-abc-123\"", s, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildEnvFileContent_MultiProvider_AggregatesCompatModels()
    {
        var answers = new List<ProviderAnswer>
        {
            new(WizardProviders.Azure, "azkey-0123456789abcdef", "gpt-4o-mini",
                Endpoint: "https://x.openai.azure.com"),
            new(WizardProviders.OpenAI, "sk-0123456789abcdef", "gpt-4o,gpt-4o-mini"),
            new(WizardProviders.Groq, "gsk_0123456789abcdef", "llama-3.1-70b-versatile"),
        };
        var s = WizardSession.BuildEnvFileContent(answers, "azure", _ts);

        // AZ_AI_COMPAT_MODELS aggregates compat providers in order.
        Assert.Contains(
            "AZ_AI_COMPAT_MODELS=\"openai:gpt-4o,openai:gpt-4o-mini,groq:llama-3.1-70b-versatile\"",
            s, StringComparison.Ordinal);
        Assert.Contains("[provider:openai]", s, StringComparison.Ordinal);
        Assert.Contains("[provider:groq]", s, StringComparison.Ordinal);
        Assert.DoesNotContain("[provider:azure]", s, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildEnvFileContent_DefaultProviderNotInAnswers_Throws()
    {
        var answers = new List<ProviderAnswer>
        {
            new(WizardProviders.OpenAI, "sk-test-0123456789abcdef", "gpt-4o"),
        };
        var ex = Assert.Throws<ArgumentException>(
            () => WizardSession.BuildEnvFileContent(answers, "azure", _ts));
        Assert.Contains("not among", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildEnvFileContent_UnknownDefaultProvider_Throws()
    {
        var answers = new List<ProviderAnswer>
        {
            new(WizardProviders.OpenAI, "sk-test-0123456789abcdef", "gpt-4o"),
        };
        Assert.Throws<ArgumentException>(
            () => WizardSession.BuildEnvFileContent(answers, "ollama", _ts));
    }

    [Fact]
    public void BuildEnvFileContent_EmptyAnswers_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => WizardSession.BuildEnvFileContent(new List<ProviderAnswer>(), "openai", _ts));
    }

    [Fact]
    public void BuildEnvFileContent_EscapesShellMetaInDoubleQuotes()
    {
        // Endpoint shouldn't ever contain $ or `, but model alias names
        // could in theory; verify the escape logic does the right thing.
        var answers = new List<ProviderAnswer>
        {
            new(WizardProviders.Azure, "abc$123`xyz\"end",
                "gpt-4o-mini", Endpoint: "https://x.openai.azure.com"),
        };
        var s = WizardSession.BuildEnvFileContent(answers, "azure", _ts);
        Assert.Contains("AZUREOPENAIAPI=\"abc\\$123\\`xyz\\\"end\"", s, StringComparison.Ordinal);
    }

    // ── Idempotency: same answers in => identical body sans timestamp ────

    [Fact]
    public void BuildEnvFileContent_SameAnswers_IdenticalModuloTimestamp()
    {
        var answers = new List<ProviderAnswer>
        {
            new(WizardProviders.OpenAI, "sk-stable-0123456789abcdef",
                "gpt-4o-mini,gpt-4o"),
        };
        var t1 = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var t2 = new DateTimeOffset(2026, 6, 2, 13, 30, 0, TimeSpan.Zero);
        var a = WizardSession.BuildEnvFileContent(answers, "openai", t1);
        var b = WizardSession.BuildEnvFileContent(answers, "openai", t2);

        Assert.NotEqual(a, b); // timestamps differ
        Assert.Equal(
            WizardSession.StripTimestampComment(a),
            WizardSession.StripTimestampComment(b));
    }

    // ── WriteEnvFile: backup + idempotency on disk ───────────────────────

    [Fact]
    public void WriteEnvFile_NewFile_NoBackupReturned()
    {
        var path = Path.Combine(_tmpDir, "env");
        var answers = new List<ProviderAnswer>
        {
            new(WizardProviders.OpenAI, "sk-0123456789abcdef", "gpt-4o-mini"),
        };
        var content = WizardSession.BuildEnvFileContent(answers, "openai", _ts);
        var backup = WizardSession.WriteEnvFile(path, content, _ts);

        Assert.Null(backup);
        Assert.True(File.Exists(path));
        Assert.Equal(content, File.ReadAllText(path));
    }

    [Fact]
    public void WriteEnvFile_SameAnswersTwice_NoBackupTaken()
    {
        var path = Path.Combine(_tmpDir, "env");
        var answers = new List<ProviderAnswer>
        {
            new(WizardProviders.OpenAI, "sk-stable-0123456789abcdef",
                "gpt-4o-mini"),
        };
        var t1 = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var t2 = new DateTimeOffset(2026, 6, 2, 13, 0, 0, TimeSpan.Zero);

        WizardSession.WriteEnvFile(path, WizardSession.BuildEnvFileContent(answers, "openai", t1), t1);
        var backup = WizardSession.WriteEnvFile(path, WizardSession.BuildEnvFileContent(answers, "openai", t2), t2);

        Assert.Null(backup);
        // No .bak.* siblings sit next to the env file.
        var siblings = Directory.GetFiles(_tmpDir, "env.bak.*");
        Assert.Empty(siblings);
    }

    [Fact]
    public void WriteEnvFile_DifferentAnswers_BackupCreated()
    {
        var path = Path.Combine(_tmpDir, "env");
        var first = new List<ProviderAnswer>
        {
            new(WizardProviders.OpenAI, "sk-old-0123456789abcdef", "gpt-4o-mini"),
        };
        var second = new List<ProviderAnswer>
        {
            new(WizardProviders.OpenAI, "sk-new-0123456789abcdef", "gpt-4o"),
        };
        var t1 = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var t2 = new DateTimeOffset(2026, 6, 2, 13, 0, 0, TimeSpan.Zero);

        WizardSession.WriteEnvFile(path, WizardSession.BuildEnvFileContent(first, "openai", t1), t1);
        var backup = WizardSession.WriteEnvFile(path, WizardSession.BuildEnvFileContent(second, "openai", t2), t2);

        Assert.NotNull(backup);
        Assert.True(File.Exists(backup));
        Assert.Contains(".bak.", backup, StringComparison.Ordinal);
        // Backup contains the OLD content (the API key from `first`).
        Assert.Contains("sk-old-", File.ReadAllText(backup), StringComparison.Ordinal);
        // Live file contains the NEW content.
        Assert.Contains("sk-new-", File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteEnvFile_OnUnix_FileIsMode0600()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return; // POSIX-only

        var path = Path.Combine(_tmpDir, "env");
        var answers = new List<ProviderAnswer>
        {
            new(WizardProviders.OpenAI, "sk-perms-0123456789abcdef", "gpt-4o-mini"),
        };
        WizardSession.WriteEnvFile(path, WizardSession.BuildEnvFileContent(answers, "openai", _ts), _ts);

        var mode = File.GetUnixFileMode(path);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
    }

    // ── DefaultEnvFilePath honours XDG_CONFIG_HOME on Unix ───────────────

    [Fact]
    public void DefaultEnvFilePath_HonoursXdgConfigHome()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        var orig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        try
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", _tmpDir);
            var p = WizardSession.DefaultEnvFilePath();
            Assert.Equal(Path.Combine(_tmpDir, "az-ai", "env"), p);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", orig);
        }
    }
}
