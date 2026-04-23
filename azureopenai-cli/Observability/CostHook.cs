using System.Text.Json;

namespace AzureOpenAI_CLI.Observability;

/// <summary>
/// FinOps cost estimator for LLM calls.
/// Hardcoded price table from docs/cost-optimization.md §3.
/// Override via AZAI_PRICE_TABLE env (path to JSON).
/// Unknown models emit null cost (no faked numbers).
/// </summary>
internal static class CostHook
{
    /// <summary>
    /// Price entry: input and output token costs per 1K tokens (USD).
    /// </summary>
    private record PriceEntry(double InputPer1K, double OutputPer1K);

    // Hardcoded price table from docs/cost-optimization.md §3
    // Prices are USD per 1K tokens (divide from per-1M in docs)
    private static readonly Dictionary<string, PriceEntry> DefaultPriceTable = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gpt-4o-mini"] = new PriceEntry(0.00015, 0.00060),       // $0.15/$0.60 per 1M
        ["gpt-5.4-nano"] = new PriceEntry(0.00020, 0.00125),      // $0.20/$1.25 per 1M
        ["gpt-4o"] = new PriceEntry(0.00250, 0.01000),            // $2.50/$10.00 per 1M
        ["gpt-4.1"] = new PriceEntry(0.00300, 0.01200),           // $3.00/$12.00 per 1M (estimated)
        ["Phi-4-mini-instruct"] = new PriceEntry(0.000075, 0.000300),   // $0.075/$0.300 per 1M
        ["Phi-4-mini-reasoning"] = new PriceEntry(0.000080, 0.000320),  // $0.080/$0.320 per 1M
        ["DeepSeek-V3.2"] = new PriceEntry(0.00058, 0.00168),     // $0.58/$1.68 per 1M
        ["o1-mini"] = new PriceEntry(0.00300, 0.01200),           // $3.00/$12.00 per 1M (estimated)
    };

    private static Dictionary<string, PriceEntry>? _customPriceTable;
    private static bool _loadAttempted = false;

    /// <summary>
    /// Calculate cost for a given model and token usage.
    /// Returns null if model is unknown (no faked numbers).
    /// </summary>
    public static double? CalculateCost(string model, int inputTokens, int outputTokens)
    {
        LoadCustomPriceTableIfNeeded();

        var priceTable = _customPriceTable ?? DefaultPriceTable;

        if (!priceTable.TryGetValue(model, out var price))
        {
            // Unknown model: don't fake numbers
            return null;
        }

        // Cost = (input_tokens / 1000) * input_price_per_1k + (output_tokens / 1000) * output_price_per_1k
        var inputCost = (inputTokens / 1000.0) * price.InputPer1K;
        var outputCost = (outputTokens / 1000.0) * price.OutputPer1K;
        return inputCost + outputCost;
    }

    /// <summary>
    /// Load custom price table from AZAI_PRICE_TABLE env var if set.
    /// JSON format: {"model_name": {"inputPer1K": 0.00015, "outputPer1K": 0.00060}, ...}
    /// </summary>
    private static void LoadCustomPriceTableIfNeeded()
    {
        if (_loadAttempted)
            return;

        _loadAttempted = true;

        var customPath = Environment.GetEnvironmentVariable("AZAI_PRICE_TABLE");
        if (string.IsNullOrWhiteSpace(customPath) || !File.Exists(customPath))
            return;

        try
        {
            var json = File.ReadAllText(customPath);
            var parsed = JsonSerializer.Deserialize(json, global::AzureOpenAI_CLI.AppJsonContext.Default.DictionaryStringPriceTableEntry);
            if (parsed == null)
                return;

            _customPriceTable = new Dictionary<string, PriceEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in parsed)
            {
                _customPriceTable[kvp.Key] = new PriceEntry(kvp.Value.InputPer1K, kvp.Value.OutputPer1K);
            }
        }
        catch
        {
            // Ignore load failures, fall back to default table
            _customPriceTable = null;
        }
    }

    /// <summary>
    /// FR-015: expose input/output per-1K rates for a model for the
    /// pre-flight estimator. Returns false for unknown models — callers
    /// must NOT fabricate a default rate (Morty's rule: no faked numbers).
    /// </summary>
    public static bool TryGetRates(string model, out double inputPer1K, out double outputPer1K)
    {
        LoadCustomPriceTableIfNeeded();
        var priceTable = _customPriceTable ?? DefaultPriceTable;
        if (priceTable.TryGetValue(model, out var price))
        {
            inputPer1K = price.InputPer1K;
            outputPer1K = price.OutputPer1K;
            return true;
        }
        inputPer1K = 0;
        outputPer1K = 0;
        return false;
    }

    /// <summary>
    /// FR-015: enumerate known model names (default + any custom overrides).
    /// Used by the estimator to produce a helpful "unknown model" error.
    /// </summary>
    public static IReadOnlyCollection<string> KnownModels()
    {
        LoadCustomPriceTableIfNeeded();
        var priceTable = _customPriceTable ?? DefaultPriceTable;
        return priceTable.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Format cost for stderr display.
    /// Returns "cost=$0.000123" or "cost=unknown" if null.
    /// </summary>
    public static string FormatCost(double? cost)
    {
        if (cost.HasValue)
        {
            return $"cost=${cost.Value:F6}";
        }
        return "cost=unknown";
    }
}
