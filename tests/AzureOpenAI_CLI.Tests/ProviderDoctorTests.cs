using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AzureOpenAI_CLI;
using AzureOpenAI_CLI.Cli;
using Xunit;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// S03E15 -- The Probe. Tests for <see cref="ProviderDoctor"/>.
/// Env mutation requires the ConsoleCapture collection (sequential).
/// DNS is stubbed via the <c>ProviderDoctor.DnsResolver</c> seam so no
/// test ever touches the real network.
/// </summary>
[Collection("ConsoleCapture")]
public class ProviderDoctorTests : IDisposable
{
    private readonly Dictionary<string, string?> _saved = new(StringComparer.Ordinal);

    private static readonly string[] EnvKeys =
    {
        "AZUREOPENAIENDPOINT",
        "AZUREOPENAIAPI",
        "AZUREOPENAIMODEL",
        "AZURE_FOUNDRY_ENDPOINT",
        "AZURE_FOUNDRY_KEY",
        "AZURE_FOUNDRY_MODELS",
        "AZ_AI_COMPAT_MODELS",
        "OPENAI_API_KEY",
        "GROQ_API_KEY",
        "TOGETHER_API_KEY",
        "CLOUDFLARE_API_TOKEN",
    };

    private readonly Func<string, CancellationToken, Task<bool>> _savedResolver
        = ProviderDoctor.DnsResolver;

    public ProviderDoctorTests()
    {
        foreach (var k in EnvKeys)
        {
            _saved[k] = Environment.GetEnvironmentVariable(k);
            Environment.SetEnvironmentVariable(k, null);
        }
    }

    public void Dispose()
    {
        foreach (var k in EnvKeys)
        {
            Environment.SetEnvironmentVariable(k, _saved[k]);
        }
        ProviderDoctor.DnsResolver = _savedResolver;
        ProviderDoctor.DnsTimeout = TimeSpan.FromSeconds(3);
    }

    private static void StubResolver(bool ok)
        => ProviderDoctor.DnsResolver = (_, _) => Task.FromResult(ok);

    private static void StubResolver(Func<string, bool> map)
        => ProviderDoctor.DnsResolver = (host, _) => Task.FromResult(map(host));

    // - Empty configuration -

    [Fact]
    public void Run_NoProviders_ExitsZeroWithHelp()
    {
        StubResolver(true);
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var rc = ProviderDoctor.Run(jsonMode: false, plain: false, stdout, stderr);

        Assert.Equal(0, rc);
        var s = stdout.ToString();
        Assert.Contains("no providers configured", s, StringComparison.Ordinal);
        Assert.Contains("--setup", s, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_NoProviders_JsonMode_EmitsEmptyArrayAndAllHealthy()
    {
        StubResolver(true);
        var stdout = new StringWriter();

        var rc = ProviderDoctor.Run(jsonMode: true, plain: false, stdout, new StringWriter());

        Assert.Equal(0, rc);
        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.Equal(0, doc.RootElement.GetProperty("providers").GetArrayLength());
        Assert.True(doc.RootElement.GetProperty("all_healthy").GetBoolean());
    }

    // - All-healthy -

    [Fact]
    public void Run_AzureFullyConfigured_AllHealthy_ExitsZero()
    {
        Environment.SetEnvironmentVariable("AZUREOPENAIENDPOINT", "https://example.cognitiveservices.azure.com/");
        Environment.SetEnvironmentVariable("AZUREOPENAIAPI", "secret-key-redacted-by-test");
        Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", "gpt-4o-mini");
        StubResolver(true);

        var stdout = new StringWriter();
        var rc = ProviderDoctor.Run(jsonMode: false, plain: false, stdout, new StringWriter());

        Assert.Equal(0, rc);
        Assert.Contains("azure", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("yes", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("all 1 provider(s) healthy", stdout.ToString(), StringComparison.Ordinal);
    }

    // - Missing creds -

    [Fact]
    public void Run_AzureMissingCreds_ReportsNo_ExitsOne()
    {
        Environment.SetEnvironmentVariable("AZUREOPENAIENDPOINT", "https://example.cognitiveservices.azure.com/");
        Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", "gpt-4o-mini");
        // No AZUREOPENAIAPI.
        StubResolver(true);

        var stdout = new StringWriter();
        var rc = ProviderDoctor.Run(jsonMode: false, plain: false, stdout, new StringWriter());

        Assert.Equal(1, rc);
    }

    [Fact]
    public void Run_AzureMissingCreds_JsonShowsCredsPresentFalse()
    {
        Environment.SetEnvironmentVariable("AZUREOPENAIENDPOINT", "https://example.cognitiveservices.azure.com/");
        Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", "gpt-4o-mini");
        StubResolver(true);

        var stdout = new StringWriter();
        ProviderDoctor.Run(jsonMode: true, plain: false, stdout, new StringWriter());

        using var doc = JsonDocument.Parse(stdout.ToString());
        var azure = doc.RootElement.GetProperty("providers")[0];
        Assert.Equal("azure", azure.GetProperty("name").GetString());
        Assert.False(azure.GetProperty("creds_present").GetBoolean());
        Assert.False(azure.GetProperty("healthy").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("all_healthy").GetBoolean());
    }

    // - Bad DNS -

    [Fact]
    public void Run_DnsUnreachable_ReportsUnreachable_ExitsOne()
    {
        Environment.SetEnvironmentVariable("AZUREOPENAIENDPOINT", "https://example.cognitiveservices.azure.com/");
        Environment.SetEnvironmentVariable("AZUREOPENAIAPI", "k");
        Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", "gpt-4o-mini");
        StubResolver(false);

        var stdout = new StringWriter();
        var rc = ProviderDoctor.Run(jsonMode: false, plain: false, stdout, new StringWriter());

        Assert.Equal(1, rc);
        Assert.Contains("unreachable", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Run_DnsResolverThrows_ReportsError()
    {
        Environment.SetEnvironmentVariable("AZUREOPENAIENDPOINT", "https://example.cognitiveservices.azure.com/");
        Environment.SetEnvironmentVariable("AZUREOPENAIAPI", "k");
        Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", "gpt-4o-mini");
        ProviderDoctor.DnsResolver = (_, _) => throw new InvalidOperationException("boom");

        var stdout = new StringWriter();
        var rc = ProviderDoctor.Run(jsonMode: true, plain: false, stdout, new StringWriter());

        Assert.Equal(1, rc);
        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.Equal("error", doc.RootElement.GetProperty("providers")[0].GetProperty("dns").GetString());
        // Stack trace text MUST NOT appear in output.
        Assert.DoesNotContain("InvalidOperationException", stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("boom", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Run_DnsTimeout_ReportsUnreachable()
    {
        Environment.SetEnvironmentVariable("AZUREOPENAIENDPOINT", "https://example.cognitiveservices.azure.com/");
        Environment.SetEnvironmentVariable("AZUREOPENAIAPI", "k");
        Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", "gpt-4o-mini");
        ProviderDoctor.DnsTimeout = TimeSpan.FromMilliseconds(50);
        ProviderDoctor.DnsResolver = async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
            return true;
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var stdout = new StringWriter();
        var rc = ProviderDoctor.Run(jsonMode: true, plain: false, stdout, new StringWriter());
        sw.Stop();

        Assert.Equal(1, rc);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3), $"doctor must respect timeout; took {sw.Elapsed}");
        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.Equal("unreachable", doc.RootElement.GetProperty("providers")[0].GetProperty("dns").GetString());
    }

    [Fact]
    public void Run_MalformedEndpoint_ReportsError()
    {
        Environment.SetEnvironmentVariable("AZUREOPENAIENDPOINT", "not a url");
        Environment.SetEnvironmentVariable("AZUREOPENAIAPI", "k");
        Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", "gpt-4o-mini");
        StubResolver(true);

        var stdout = new StringWriter();
        ProviderDoctor.Run(jsonMode: true, plain: false, stdout, new StringWriter());

        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.Equal("error", doc.RootElement.GetProperty("providers")[0].GetProperty("dns").GetString());
    }

    // - Multiple providers -

    [Fact]
    public void Run_AzurePlusFoundry_BothEnumerated()
    {
        Environment.SetEnvironmentVariable("AZUREOPENAIENDPOINT", "https://a.example.com/");
        Environment.SetEnvironmentVariable("AZUREOPENAIAPI", "k1");
        Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", "gpt-4o,gpt-4o-mini");
        Environment.SetEnvironmentVariable("AZURE_FOUNDRY_ENDPOINT", "https://b.example.com/");
        Environment.SetEnvironmentVariable("AZURE_FOUNDRY_KEY", "k2");
        Environment.SetEnvironmentVariable("AZURE_FOUNDRY_MODELS", "deepseek");
        StubResolver(true);

        var stdout = new StringWriter();
        ProviderDoctor.Run(jsonMode: true, plain: false, stdout, new StringWriter());

        using var doc = JsonDocument.Parse(stdout.ToString());
        var names = doc.RootElement.GetProperty("providers").EnumerateArray()
            .Select(e => e.GetProperty("name").GetString()).ToList();
        Assert.Contains("azure", names);
        Assert.Contains("foundry", names);
        Assert.True(doc.RootElement.GetProperty("all_healthy").GetBoolean());
    }

    [Fact]
    public void Run_ModelsConfiguredCount_IsCommaSeparatedLength()
    {
        Environment.SetEnvironmentVariable("AZUREOPENAIENDPOINT", "https://a.example.com/");
        Environment.SetEnvironmentVariable("AZUREOPENAIAPI", "k1");
        Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", "gpt-4o,gpt-4o-mini, gpt-4");
        StubResolver(true);

        var stdout = new StringWriter();
        ProviderDoctor.Run(jsonMode: true, plain: false, stdout, new StringWriter());

        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.Equal(3, doc.RootElement.GetProperty("providers")[0].GetProperty("models_configured").GetInt32());
    }

    // - Compat presets -

    [Fact]
    public void Run_CompatPreset_ReportsByPresetName()
    {
        Environment.SetEnvironmentVariable("AZ_AI_COMPAT_MODELS", "openai:gpt-4o-mini,openai:gpt-4o");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "sk-test");
        StubResolver(true);

        var stdout = new StringWriter();
        var rc = ProviderDoctor.Run(jsonMode: true, plain: false, stdout, new StringWriter());

        using var doc = JsonDocument.Parse(stdout.ToString());
        var entries = doc.RootElement.GetProperty("providers").EnumerateArray().ToList();
        Assert.Single(entries);
        Assert.Equal("compat:openai", entries[0].GetProperty("name").GetString());
        Assert.Equal(2, entries[0].GetProperty("models_configured").GetInt32());
        Assert.True(entries[0].GetProperty("creds_present").GetBoolean());
        Assert.Equal(0, rc);
    }

    [Fact]
    public void Run_CompatPresetMalformed_ReportedAsUnhealthy()
    {
        Environment.SetEnvironmentVariable("AZ_AI_COMPAT_MODELS", "this is not valid");
        StubResolver(true);

        var stdout = new StringWriter();
        var rc = ProviderDoctor.Run(jsonMode: true, plain: false, stdout, new StringWriter());

        Assert.Equal(1, rc);
        using var doc = JsonDocument.Parse(stdout.ToString());
        var first = doc.RootElement.GetProperty("providers")[0];
        Assert.False(first.GetProperty("healthy").GetBoolean());
    }

    // - --plain output -

    [Fact]
    public void Run_PlainMode_IsAsciiOnly_AndKeyValue()
    {
        Environment.SetEnvironmentVariable("AZUREOPENAIENDPOINT", "https://a.example.com/");
        Environment.SetEnvironmentVariable("AZUREOPENAIAPI", "k");
        Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", "gpt-4o-mini");
        StubResolver(true);

        var stdout = new StringWriter();
        ProviderDoctor.Run(jsonMode: false, plain: true, stdout, new StringWriter());
        var s = stdout.ToString();

        // ASCII-only.
        foreach (var ch in s) Assert.True(ch < 128, $"non-ASCII char 0x{(int)ch:X4}");
        // Key:value shape.
        Assert.Contains("provider: azure", s, StringComparison.Ordinal);
        Assert.Contains("dns: ok", s, StringComparison.Ordinal);
        Assert.Contains("creds_present: true", s, StringComparison.Ordinal);
        Assert.Contains("models_configured: 1", s, StringComparison.Ordinal);
        Assert.Contains("healthy: true", s, StringComparison.Ordinal);
    }

    // - JSON schema match -

    [Fact]
    public void Run_JsonSchema_HasRequiredFields()
    {
        Environment.SetEnvironmentVariable("AZUREOPENAIENDPOINT", "https://a.example.com/");
        Environment.SetEnvironmentVariable("AZUREOPENAIAPI", "k");
        Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", "m");
        StubResolver(true);

        var stdout = new StringWriter();
        ProviderDoctor.Run(jsonMode: true, plain: false, stdout, new StringWriter());

        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.True(doc.RootElement.TryGetProperty("providers", out _));
        Assert.True(doc.RootElement.TryGetProperty("all_healthy", out _));
        var p = doc.RootElement.GetProperty("providers")[0];
        foreach (var key in new[] { "name", "endpoint", "dns", "creds_present", "models_configured", "healthy" })
        {
            Assert.True(p.TryGetProperty(key, out _), $"missing key {key}");
        }
    }

    // - No credential leakage -

    [Fact]
    public void Run_OutputNeverContainsCredentialValue()
    {
        const string sentinel = "sk-supersecretvalue-12345";
        Environment.SetEnvironmentVariable("AZUREOPENAIENDPOINT", "https://a.example.com/");
        Environment.SetEnvironmentVariable("AZUREOPENAIAPI", sentinel);
        Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", "gpt-4o-mini");
        StubResolver(true);

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        ProviderDoctor.Run(jsonMode: false, plain: false, stdout, stderr);
        ProviderDoctor.Run(jsonMode: true, plain: false, stdout, stderr);
        ProviderDoctor.Run(jsonMode: false, plain: true, stdout, stderr);

        var combined = stdout.ToString() + stderr.ToString();
        Assert.DoesNotContain(sentinel, combined, StringComparison.Ordinal);
    }

    // - Performance: parallel probes complete under cap -

    [Fact]
    public void Run_AllUnreachableProviders_CompletesWithinCap()
    {
        Environment.SetEnvironmentVariable("AZUREOPENAIENDPOINT", "https://a.example.com/");
        Environment.SetEnvironmentVariable("AZUREOPENAIAPI", "k");
        Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", "m");
        Environment.SetEnvironmentVariable("AZURE_FOUNDRY_ENDPOINT", "https://b.example.com/");
        Environment.SetEnvironmentVariable("AZURE_FOUNDRY_KEY", "k");
        Environment.SetEnvironmentVariable("AZURE_FOUNDRY_MODELS", "m");
        Environment.SetEnvironmentVariable("AZ_AI_COMPAT_MODELS", "openai:gpt-4o,groq:llama,together:mixtral");
        ProviderDoctor.DnsTimeout = TimeSpan.FromMilliseconds(200);
        ProviderDoctor.DnsResolver = async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
            return true;
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        ProviderDoctor.Run(jsonMode: true, plain: false, new StringWriter(), new StringWriter());
        sw.Stop();

        // Five probes in parallel under a 200ms cap each must finish < 1s.
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(1), $"parallel probes too slow: {sw.Elapsed}");
    }

    // - Healthy roll-up logic -

    [Fact]
    public void Run_OneUnhealthyAmongMany_AllHealthyFalse()
    {
        Environment.SetEnvironmentVariable("AZUREOPENAIENDPOINT", "https://a.example.com/");
        Environment.SetEnvironmentVariable("AZUREOPENAIAPI", "k");
        Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", "m");
        Environment.SetEnvironmentVariable("AZURE_FOUNDRY_ENDPOINT", "https://b.example.com/");
        Environment.SetEnvironmentVariable("AZURE_FOUNDRY_KEY", "k");
        Environment.SetEnvironmentVariable("AZURE_FOUNDRY_MODELS", "m");
        // Only the foundry host fails.
        StubResolver(host => !host.Contains("b.example.com", StringComparison.Ordinal));

        var stdout = new StringWriter();
        var rc = ProviderDoctor.Run(jsonMode: true, plain: false, stdout, new StringWriter());

        Assert.Equal(1, rc);
        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.False(doc.RootElement.GetProperty("all_healthy").GetBoolean());
    }

    // - Default (TTY) output is ASCII box drawing only -

    [Fact]
    public void Run_DefaultMode_BoxDrawingIsAscii()
    {
        Environment.SetEnvironmentVariable("AZUREOPENAIENDPOINT", "https://a.example.com/");
        Environment.SetEnvironmentVariable("AZUREOPENAIAPI", "k");
        Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", "m");
        StubResolver(true);

        var stdout = new StringWriter();
        ProviderDoctor.Run(jsonMode: false, plain: false, stdout, new StringWriter());
        var s = stdout.ToString();

        foreach (var ch in s) Assert.True(ch < 128, $"non-ASCII char 0x{(int)ch:X4}");
        Assert.Contains("|", s, StringComparison.Ordinal);
        Assert.Contains("+--", s, StringComparison.Ordinal);
        Assert.Contains("provider", s, StringComparison.Ordinal);
        Assert.Contains("endpoint", s, StringComparison.Ordinal);
        Assert.Contains("dns", s, StringComparison.Ordinal);
        Assert.Contains("creds", s, StringComparison.Ordinal);
        Assert.Contains("models", s, StringComparison.Ordinal);
    }

    // - CollectProviders unit (no DNS) -

    [Fact]
    public void CollectProviders_OnlyAzureSet_ReturnsOneEntry()
    {
        Environment.SetEnvironmentVariable("AZUREOPENAIENDPOINT", "https://a.example.com/");
        Environment.SetEnvironmentVariable("AZUREOPENAIAPI", "k");

        var probes = ProviderDoctor.CollectProviders();
        Assert.Single(probes);
        Assert.Equal("azure", probes[0].Name);
        Assert.Equal("AZUREOPENAIAPI", probes[0].CredEnvVar);
    }

    [Fact]
    public void CollectProviders_AllThreeProviderKinds_ReturnsThree()
    {
        Environment.SetEnvironmentVariable("AZUREOPENAIENDPOINT", "https://a.example.com/");
        Environment.SetEnvironmentVariable("AZURE_FOUNDRY_ENDPOINT", "https://b.example.com/");
        Environment.SetEnvironmentVariable("AZ_AI_COMPAT_MODELS", "openai:gpt-4o");

        var probes = ProviderDoctor.CollectProviders();
        Assert.Equal(3, probes.Count);
        Assert.Equal("azure", probes[0].Name);
        Assert.Equal("foundry", probes[1].Name);
        Assert.Equal("compat:openai", probes[2].Name);
    }
}
