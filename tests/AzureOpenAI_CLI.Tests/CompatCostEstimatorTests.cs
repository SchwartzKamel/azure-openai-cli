using AzureOpenAI_CLI.Observability;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// S03E12 -- *The Receipt*. Closes Kramer Finding 5 from S03E09 *The Compat*:
/// CostEstimator now has placeholder rates for the four OpenAI-compatible
/// presets (openai / groq / together / cloudflare) plus a redacted
/// fall-through for unknown presets. Tests cover all four known presets
/// (rate found, numbers emitted) plus the unknown-preset path
/// ([REDACTED:provider] sentinel + "unknown rate, $? estimate" note).
/// </summary>
[Collection("ConsoleCapture")]
public class CompatCostEstimatorTests
{
    private const string LongPrompt = "estimate me a receipt please, four hundred chars or so should clear chars/4 floor easily and hit the ceiling.";

    [Theory]
    [InlineData("openai")]
    [InlineData("groq")]
    [InlineData("together")]
    [InlineData("cloudflare")]
    public void EstimateForCompatPreset_KnownPreset_ProducesPositiveEstimate(string preset)
    {
        var result = CostEstimator.EstimateForCompatPreset(preset, modelHint: "fake-model", prompt: LongPrompt, outputMaxTokens: 256);

        Assert.NotNull(result);
        Assert.True(result.InputTokensEst > 0, "Input tokens must be > 0 for non-empty prompt.");
        Assert.True(result.InputUsd > 0.0, "Input USD must be > 0 for known preset.");
        Assert.True(result.OutputUsdMax.HasValue && result.OutputUsdMax.Value > 0.0);
        Assert.True(result.TotalUsdMax > 0.0);
        Assert.StartsWith(preset + ":", result.Model, StringComparison.Ordinal);
        Assert.Contains("PLACEHOLDER", result.Approximation, StringComparison.Ordinal);
        // Known list must include all four presets.
        Assert.Contains("openai", result.Approximation, StringComparison.Ordinal);
        Assert.Contains("groq", result.Approximation, StringComparison.Ordinal);
        Assert.Contains("together", result.Approximation, StringComparison.Ordinal);
        Assert.Contains("cloudflare", result.Approximation, StringComparison.Ordinal);
    }

    [Fact]
    public void EstimateForCompatPreset_UnknownPreset_EmitsRedactedAndUnknownRate()
    {
        var result = CostEstimator.EstimateForCompatPreset("anthropic", modelHint: "claude-3", prompt: LongPrompt, outputMaxTokens: 64);

        Assert.NotNull(result);
        Assert.Equal("[REDACTED:provider]", result.Model);
        Assert.Equal(0.0, result.InputUsd);
        Assert.Equal(0.0, result.TotalUsdMax);
        Assert.Contains("unknown rate", result.Approximation, StringComparison.Ordinal);
        Assert.Contains("$?", result.Approximation, StringComparison.Ordinal);
        Assert.Contains("[REDACTED:provider]", result.Approximation, StringComparison.Ordinal);
    }

    [Fact]
    public void EstimateForCompatPreset_NullPreset_EmitsRedactedSentinel()
    {
        var result = CostEstimator.EstimateForCompatPreset(null, modelHint: null, prompt: "hi", outputMaxTokens: null);

        Assert.NotNull(result);
        Assert.Equal("[REDACTED:provider]", result.Model);
        Assert.Equal(0.0, result.InputUsd);
        Assert.Null(result.OutputUsdMax);
        Assert.Equal(0.0, result.TotalUsdMax);
        Assert.Contains("[REDACTED:provider]", result.Approximation, StringComparison.Ordinal);
    }

    [Fact]
    public void EstimateForCompatPreset_KnownPreset_NoOutputCap_TotalEqualsInput()
    {
        var result = CostEstimator.EstimateForCompatPreset("openai", modelHint: null, prompt: LongPrompt, outputMaxTokens: null);

        Assert.Equal("openai", result.Model);
        Assert.True(result.InputUsd > 0.0);
        Assert.Null(result.OutputUsdMax);
        Assert.Equal(result.InputUsd, result.TotalUsdMax, 12);
    }

    [Fact]
    public void EstimateForCompatPreset_FormatJson_RoundTripsClean()
    {
        var result = CostEstimator.EstimateForCompatPreset("groq", modelHint: "llama-3.1-70b", prompt: LongPrompt, outputMaxTokens: 128);
        var json = CostEstimator.FormatJson(result);
        Assert.Contains("\"model\": \"groq:llama-3.1-70b\"", json, StringComparison.Ordinal);
        Assert.Contains("\"input_tokens_est\"", json, StringComparison.Ordinal);
        Assert.Contains("\"total_usd_max\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void CompatCostRates_KnownPresets_StableOrder()
    {
        var presets = CompatCostRates.KnownPresets().ToArray();
        Assert.Equal(new[] { "cloudflare", "groq", "openai", "together" }, presets);
    }

    [Fact]
    public void CompatCostRates_TryGetRates_UnknownPreset_ReturnsFalseZeroed()
    {
        Assert.False(CompatCostRates.TryGetRates("anthropic", out var inP, out var outP));
        Assert.Equal(0.0, inP);
        Assert.Equal(0.0, outP);
    }

    [Fact]
    public void CompatCostRates_TryGetRates_NullOrWhitespace_ReturnsFalse()
    {
        Assert.False(CompatCostRates.TryGetRates(null, out _, out _));
        Assert.False(CompatCostRates.TryGetRates("", out _, out _));
        Assert.False(CompatCostRates.TryGetRates("   ", out _, out _));
    }

    [Fact]
    public void CompatCostRates_TryGetRates_CaseInsensitive()
    {
        Assert.True(CompatCostRates.TryGetRates("OpenAI", out var inP, out var outP));
        Assert.True(inP > 0);
        Assert.True(outP > 0);
    }
}
