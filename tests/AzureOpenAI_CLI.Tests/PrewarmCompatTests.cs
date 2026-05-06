namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// S03E12 -- *The Receipt*. Closes Kramer Finding 4 from S03E09 *The
/// Compat*: <c>PrewarmAsync</c> only warmed Azure-OpenAI / Foundry, leaving
/// the OpenAI-compat dispatch path cold on first call. Tests verify
/// <see cref="Program.PrewarmCompatAsync"/> is silent, fast, and never
/// throws -- regardless of whether AZ_AI_COMPAT_MODELS is set, whether the
/// API key is present, or whether the preset is well-formed. NO network is
/// performed.
/// </summary>
[Collection("ConsoleCapture")]
public class PrewarmCompatTests : IDisposable
{
    private static readonly string[] EnvVars =
    {
        "AZ_AI_COMPAT_MODELS",
        "OPENAI_API_KEY",
        "OPENAI_ORG_ID",
        "GROQ_API_KEY",
        "TOGETHER_API_KEY",
        "CLOUDFLARE_API_TOKEN",
        "CLOUDFLARE_ACCOUNT_ID",
    };

    private readonly Dictionary<string, string?> _saved = new();

    public PrewarmCompatTests()
    {
        foreach (var v in EnvVars)
            _saved[v] = Environment.GetEnvironmentVariable(v);
        foreach (var v in EnvVars)
            Environment.SetEnvironmentVariable(v, null);
    }

    public void Dispose()
    {
        foreach (var v in EnvVars)
            Environment.SetEnvironmentVariable(v, _saved[v]);
    }

    private static async Task<(string Stdout, string Stderr, TimeSpan Elapsed)> RunCapture(Func<Task> body)
    {
        var oldOut = Console.Out;
        var oldErr = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        Console.SetOut(stdout);
        Console.SetError(stderr);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await body();
        }
        finally
        {
            sw.Stop();
            Console.SetOut(oldOut);
            Console.SetError(oldErr);
        }
        return (stdout.ToString(), stderr.ToString(), sw.Elapsed);
    }

    [Fact]
    public async Task PrewarmCompatAsync_NoEnvVar_ReturnsSilentlyAndFast()
    {
        var (out_, err, elapsed) = await RunCapture(Program.PrewarmCompatAsync);
        Assert.Empty(out_);
        Assert.Empty(err);
        Assert.True(elapsed < TimeSpan.FromSeconds(1), $"Prewarm took {elapsed.TotalMilliseconds:F1}ms with no env var; expected < 1s.");
    }

    [Fact]
    public async Task PrewarmCompatAsync_EnvVarSetButApiKeyMissing_SilentDegrade()
    {
        // Build() will throw InvalidOperationException because OPENAI_API_KEY
        // is unset; Prewarm must swallow it and stay silent.
        Environment.SetEnvironmentVariable("AZ_AI_COMPAT_MODELS", "openai:gpt-4o-mini");

        var (out_, err, elapsed) = await RunCapture(Program.PrewarmCompatAsync);
        Assert.Empty(out_);
        Assert.Empty(err);
        Assert.True(elapsed < TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task PrewarmCompatAsync_EnvVarMalformed_SilentDegrade()
    {
        // ParseCompatModels will throw on this; outer try must swallow.
        Environment.SetEnvironmentVariable("AZ_AI_COMPAT_MODELS", "no-colon-here");

        var (out_, err, _) = await RunCapture(Program.PrewarmCompatAsync);
        Assert.Empty(out_);
        Assert.Empty(err);
    }

    [Fact]
    public async Task PrewarmCompatAsync_ApiKeyPresent_BuildsAndDiscardsSilently()
    {
        // Happy path: key is present, Build() succeeds, client is disposed.
        // No network: Build() only constructs the SDK option graph.
        Environment.SetEnvironmentVariable("AZ_AI_COMPAT_MODELS", "openai:gpt-4o-mini");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "sk-test-prewarm-only-not-real");

        var (out_, err, elapsed) = await RunCapture(Program.PrewarmCompatAsync);
        Assert.Empty(out_);
        Assert.Empty(err);
        Assert.True(elapsed < TimeSpan.FromSeconds(3), $"Prewarm took {elapsed.TotalMilliseconds:F1}ms; expected < 3s for in-process build.");
    }

    [Fact]
    public async Task PrewarmCompatAsync_MultiplePresets_DistinctOnly()
    {
        // Two entries, same preset. Should not throw, must stay silent. The
        // distinct-preset filter inside PrewarmCompatAsync prevents duplicate
        // builds; we just assert the contract holds.
        Environment.SetEnvironmentVariable("AZ_AI_COMPAT_MODELS", "openai:gpt-4o-mini,openai:gpt-4o");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "sk-test-prewarm-only-not-real");

        var (out_, err, _) = await RunCapture(Program.PrewarmCompatAsync);
        Assert.Empty(out_);
        Assert.Empty(err);
    }

    [Fact]
    public async Task PrewarmCompatAsync_CloudflareWithoutAccountId_SilentDegrade()
    {
        // Cloudflare preset throws InvalidOperationException without the
        // account id; prewarm must swallow it.
        Environment.SetEnvironmentVariable("AZ_AI_COMPAT_MODELS", "cloudflare:@cf/meta/llama-3-8b-instruct");
        Environment.SetEnvironmentVariable("CLOUDFLARE_API_TOKEN", "cf-test-token");
        // CLOUDFLARE_ACCOUNT_ID intentionally unset.

        var (out_, err, _) = await RunCapture(Program.PrewarmCompatAsync);
        Assert.Empty(out_);
        Assert.Empty(err);
    }
}
