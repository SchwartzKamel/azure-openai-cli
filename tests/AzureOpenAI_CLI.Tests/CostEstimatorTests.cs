using System.Text.Json;
using AzureOpenAI_CLI.Observability;

namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// FR-015: cost-estimator tests. No API call is made — the estimator is a
/// pure function over the prompt length + hardcoded price table.
/// </summary>
[Collection("ConsoleCapture")]
public class CostEstimatorTests
{
    private static (int ExitCode, string StdOut, string StdErr) RunEstimate(string[] args)
    {
        var origOut = Console.Out;
        var origErr = Console.Error;
        var outWriter = new StringWriter();
        var errWriter = new StringWriter();
        try
        {
            Console.SetOut(outWriter);
            Console.SetError(errWriter);
            var opts = Program.ParseArgs(args);
            int code = Program.RunEstimate(opts);
            return (code, outWriter.ToString(), errWriter.ToString());
        }
        finally
        {
            Console.SetOut(origOut);
            Console.SetError(origErr);
        }
    }

    // ── Unit-level: EstimateInputTokens ──────────────────────────────

    [Fact]
    public void EstimateInputTokens_EmptyOrWhitespace_ReturnsZero()
    {
        Assert.Equal(0, CostEstimator.EstimateInputTokens(null));
        Assert.Equal(0, CostEstimator.EstimateInputTokens(""));
        Assert.Equal(0, CostEstimator.EstimateInputTokens("   "));
    }

    [Fact]
    public void EstimateInputTokens_UsesCharsPerFourApproximation()
    {
        // 400 chars / 4 = 100 tokens
        var prompt = new string('x', 400);
        Assert.Equal(100, CostEstimator.EstimateInputTokens(prompt));
    }

    [Fact]
    public void EstimateInputTokens_RoundsUpAndFloorsAtOne()
    {
        // Any non-empty prompt → at least 1 token.
        Assert.Equal(1, CostEstimator.EstimateInputTokens("a"));
        // 5 chars / 4 = 1.25 → ceil = 2
        Assert.Equal(2, CostEstimator.EstimateInputTokens("hello"));
    }

    // ── Unit-level: Estimate() ───────────────────────────────────────

    [Fact]
    public void Estimate_DefaultModel_ReturnsPositiveUsd()
    {
        // gpt-4o-mini @ $0.00015 per 1K input tokens
        var r = CostEstimator.Estimate("gpt-4o-mini", "hello world, this is a prompt.", outputMaxTokens: null);
        Assert.NotNull(r);
        Assert.Equal("gpt-4o-mini", r!.Model);
        Assert.True(r.InputTokensEst > 0);
        Assert.True(r.InputUsd > 0);
        Assert.Null(r.OutputMaxTokens);
        Assert.Null(r.OutputUsdMax);
        // With no output cap the total equals the input cost.
        Assert.Equal(r.InputUsd, r.TotalUsdMax, precision: 12);
    }

    [Fact]
    public void Estimate_WithOutputCap_IncludesWorstCaseOutput()
    {
        var r = CostEstimator.Estimate("gpt-4o-mini", "hi", outputMaxTokens: 1000);
        Assert.NotNull(r);
        Assert.Equal(1000, r!.OutputMaxTokens);
        // $0.60 per 1M output = $0.00060 per 1K => 1000 tokens = $0.00060
        Assert.Equal(0.00060, r.OutputUsdMax!.Value, precision: 8);
        Assert.True(r.TotalUsdMax > r.InputUsd);
    }

    [Fact]
    public void Estimate_UnknownModel_ReturnsNull()
    {
        var r = CostEstimator.Estimate("totally-fake-model-9000", "hi", outputMaxTokens: null);
        Assert.Null(r);
    }

    // ── End-to-end via RunEstimate ───────────────────────────────────

    [Fact]
    public void RunEstimate_DefaultModel_ExitsZeroWithPositiveUsd()
    {
        var (code, stdout, _) = RunEstimate(["--estimate", "Summarize the last-quarter board deck"]);
        Assert.Equal(0, code);
        Assert.Contains("Cost estimate", stdout);
        Assert.Contains("gpt-4o-mini", stdout);
        // Human-mode output includes a non-zero six-decimal USD figure.
        Assert.Matches(@"\$0\.0+[1-9]", stdout);
    }

    [Fact]
    public void RunEstimate_JsonFlag_EmitsValidJsonMatchingSchema()
    {
        var (code, stdout, _) = RunEstimate([
            "--estimate", "--json", "--model", "gpt-4o-mini",
            "--estimate-with-output", "500",
            "Write a haiku about frugality"
        ]);
        Assert.Equal(0, code);

        using var doc = JsonDocument.Parse(stdout);
        var root = doc.RootElement;
        Assert.Equal("gpt-4o-mini", root.GetProperty("model").GetString());
        Assert.True(root.GetProperty("input_tokens_est").GetInt32() > 0);
        Assert.True(root.GetProperty("input_usd").GetDouble() > 0);
        Assert.Equal(500, root.GetProperty("output_max_tokens").GetInt32());
        Assert.True(root.GetProperty("output_usd_max").GetDouble() > 0);
        Assert.True(root.GetProperty("total_usd_max").GetDouble() > 0);
        // Schema contract: every documented field must be present.
        Assert.True(root.TryGetProperty("approximation", out _));
    }

    [Fact]
    public void RunEstimate_JsonFlag_NoOutputCap_OmitsOrNullsOutputFields()
    {
        var (code, stdout, _) = RunEstimate([
            "--estimate", "--json", "--model", "gpt-4o-mini",
            "hello there"
        ]);
        Assert.Equal(0, code);
        using var doc = JsonDocument.Parse(stdout);
        var root = doc.RootElement;
        // WhenWritingNull: missing properties → absent. Treat absent-or-null as
        // the contract for "unknown until generation".
        static bool AbsentOrNull(JsonElement root, string name)
            => !root.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null;
        Assert.True(AbsentOrNull(root, "output_max_tokens"));
        Assert.True(AbsentOrNull(root, "output_usd_max"));
        // Total must equal input (no output contribution).
        Assert.Equal(root.GetProperty("input_usd").GetDouble(),
                     root.GetProperty("total_usd_max").GetDouble(), precision: 12);
    }

    [Fact]
    public void RunEstimate_RawFlag_OutputIsJustTheNumber()
    {
        var (code, stdout, _) = RunEstimate([
            "--estimate", "--raw", "--model", "gpt-4o-mini",
            "a short prompt"
        ]);
        Assert.Equal(0, code);
        var trimmed = stdout.Trim();
        // Exactly a six-decimal number, nothing else. No "$", no prose.
        Assert.Matches(@"^\d+\.\d{6}$", trimmed);
        Assert.DoesNotContain("$", trimmed);
        Assert.DoesNotContain("cost", trimmed, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RunEstimate_UnknownModel_ReturnsExitOneWithClearError()
    {
        var (code, _, stderr) = RunEstimate([
            "--estimate", "--model", "fictional-model-v99",
            "any prompt text"
        ]);
        Assert.Equal(1, code);
        Assert.Contains("Unknown model", stderr);
        Assert.Contains("fictional-model-v99", stderr);
        // Include the known-model list so users can self-serve.
        Assert.Contains("gpt-4o-mini", stderr);
    }

    [Fact]
    public void RunEstimate_NoPrompt_ReturnsExitOne()
    {
        var (code, _, stderr) = RunEstimate(["--estimate", "--model", "gpt-4o-mini"]);
        Assert.Equal(1, code);
        Assert.Contains("No prompt provided", stderr);
    }
}
