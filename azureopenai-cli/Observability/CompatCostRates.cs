namespace AzureOpenAI_CLI.Observability;

/// <summary>
/// S03E12 -- *The Receipt*. Closes Kramer Finding 5 from S03E09 *The Compat*:
/// the existing <see cref="CostHook"/> price table only knows Azure OpenAI /
/// Foundry deployment names, so any prompt routed through
/// <c>OpenAiCompatAdapter</c> (openai / groq / together / cloudflare) hit the
/// "unknown model" branch in <see cref="CostEstimator"/>. This file is the
/// preset-keyed companion table -- pre-flight rates per <i>preset</i>, not
/// per model, because the upstream catalogues are too volatile to mirror
/// model-by-model in source control. The numbers are PLACEHOLDERS; the
/// commit that lands them is the contract that says "swap me out for real
/// pricing before any operator-facing claim leans on this number."
///
/// Morty: "I am not paying retail. I am also not paying made-up. Either
/// look it up or admit you are guessing."
/// Bania: "It is not a bench. It is a baseline. Same rule for the receipt."
/// </summary>
internal static class CompatCostRates
{
    /// <summary>
    /// Per-preset placeholder rate (USD per 1K tokens). All four entries are
    /// flagged for refresh -- the per-preset rate is the median of a small
    /// sampled-model set chosen to keep the order-of-magnitude honest, not
    /// the precise quote. Every entry carries a TODO with the upstream
    /// pricing URL the next maintainer should pull from.
    /// </summary>
    private static readonly Dictionary<string, (double InputPer1K, double OutputPer1K)> Rates =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // PLACEHOLDER -- update from upstream pricing
            // TODO(S03E12 follow-up, preset=openai):
            //   refresh from https://openai.com/api/pricing
            //   median anchor: gpt-4o-mini ($0.150 / $0.600 per 1M tokens).
            ["openai"] = (0.00015, 0.00060),

            // PLACEHOLDER -- update from upstream pricing
            // TODO(S03E12 follow-up, preset=groq):
            //   refresh from https://groq.com/pricing
            //   median anchor: llama-3.1-8b-instant ($0.05 / $0.08 per 1M).
            ["groq"] = (0.00005, 0.00008),

            // PLACEHOLDER -- update from upstream pricing
            // TODO(S03E12 follow-up, preset=together):
            //   refresh from https://www.together.ai/pricing
            //   median anchor: mixtral-8x7b-instruct (~$0.60 / $0.60 per 1M).
            ["together"] = (0.00060, 0.00060),

            // PLACEHOLDER -- update from upstream pricing
            // TODO(S03E12 follow-up, preset=cloudflare):
            //   refresh from https://developers.cloudflare.com/workers-ai/platform/pricing/
            //   median anchor: @cf/meta/llama-3-8b-instruct (~$0.11 / $0.33 per 1M).
            ["cloudflare"] = (0.00011, 0.00033),
        };

    /// <summary>
    /// Look up placeholder rates for an OpenAI-compatible preset. Returns
    /// <c>false</c> for unknown presets -- callers must surface that as an
    /// "unknown rate, $? estimate" message rather than fabricating numbers
    /// (Morty's rule, restated for the compat path).
    /// </summary>
    public static bool TryGetRates(string? preset, out double inputPer1K, out double outputPer1K)
    {
        inputPer1K = 0;
        outputPer1K = 0;
        if (string.IsNullOrWhiteSpace(preset)) return false;
        if (Rates.TryGetValue(preset.Trim(), out var rate))
        {
            inputPer1K = rate.InputPer1K;
            outputPer1K = rate.OutputPer1K;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Enumerate known compat preset names in stable, case-insensitive order.
    /// Used by the estimator to render an actionable "Known: ..." list when
    /// an unknown preset is requested.
    /// </summary>
    public static IReadOnlyCollection<string> KnownPresets()
        => Rates.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
}
