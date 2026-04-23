// CostAccounting.cs -- the receipt.
//
// "If you don't print the receipt, you didn't pay for the meal." -- Morty
//
// Maintained by Morty Seinfeld (FinOps watchdog). Holds the model->price
// table (per-1K input/output tokens, USD), an immutable per-call cost
// record, and an accumulator for multi-call modes (agent rounds, Ralph
// iterations). Renders a single-line receipt suitable for stderr.
//
// Honesty rule: token counts are always printed when known; dollar
// estimates are printed ONLY when the deployment name maps to a row in
// the price table. Unknown model => tokens-only line, no fabricated $.
//
// The price table is a snapshot. Source of truth is
// docs/cost-optimization.md and the Azure pricing page linked there.
// Snapshot date is in PriceTableAsOf below; bump it when you edit.
//
// Out of scope (intentional, see s02e09): live pricing fetch, budget
// caps / spend-blocking, regional / PTU pricing variants. Hard-coded
// global PAYG snapshot only.

using System.Globalization;

namespace AzureOpenAI_CLI;

/// <summary>
/// Per-call token usage and (when known) dollar cost.
/// </summary>
internal readonly record struct CostEntry(int InputTokens, int OutputTokens, decimal? UsdCost)
{
    public int TotalTokens => InputTokens + OutputTokens;
}

/// <summary>
/// Price-per-1K-tokens for a single deployment / model family.
/// </summary>
internal readonly record struct ModelPrice(decimal InputPer1K, decimal OutputPer1K);

/// <summary>
/// Static price table + per-call helpers + a tiny accumulator for
/// multi-call modes. No I/O, no network, no clock dependency.
/// </summary>
internal static class CostAccounting
{
    /// <summary>
    /// Snapshot date for the table below. Bump when prices change.
    /// Cross-reference docs/cost-optimization.md §3 for source links.
    /// </summary>
    public const string PriceTableAsOf = "2026-04";

    // Prices are USD per 1,000 tokens (NOT per 1M -- divide by 1000 from
    // the public pricing page). Keys are matched case-insensitively against
    // the deployment name AND against a longest-prefix of the deployment
    // name (so "gpt-4o-mini-2024-07-18" still resolves to gpt-4o-mini).
    //
    // Source of truth: docs/cost-optimization.md (which itself cites the
    // Azure pricing page). Numbers below mirror that table as of the
    // snapshot date. Estimates -- confirm before quoting in a customer
    // conversation. The receipt printer writes "(~$x @ model)" with the
    // tilde to flag the estimate-ness.
    private static readonly Dictionary<string, ModelPrice> Prices = new(StringComparer.OrdinalIgnoreCase)
    {
        // OpenAI family on Azure (global PAYG, USD per 1K tokens)
        ["gpt-4o-mini"] = new(0.00015m, 0.00060m),
        ["gpt-4o"] = new(0.00250m, 0.01000m),
        ["gpt-4.1"] = new(0.00300m, 0.01200m),
        ["gpt-4.1-mini"] = new(0.00040m, 0.00160m),
        ["gpt-4.1-nano"] = new(0.00010m, 0.00040m),
        ["gpt-5.4-nano"] = new(0.00020m, 0.00125m),
        ["o1-mini"] = new(0.00300m, 0.01200m),
        ["o3-mini"] = new(0.00110m, 0.00440m),
        // Foundry serverless (Microsoft + DeepSeek SLMs)
        ["phi-4-mini-instruct"] = new(0.000075m, 0.000300m),
        ["phi-4-mini-reasoning"] = new(0.000080m, 0.000320m),
        ["deepseek-v3.2"] = new(0.000580m, 0.001680m),
    };

    /// <summary>
    /// Look up a price row for a deployment name. Returns null if the
    /// deployment is not in the table -- callers MUST treat null as
    /// "tokens-only, no dollars" rather than substituting a guess.
    /// </summary>
    internal static ModelPrice? LookupPrice(string? deploymentName)
    {
        if (string.IsNullOrWhiteSpace(deploymentName)) return null;
        if (Prices.TryGetValue(deploymentName, out var exact)) return exact;

        // Longest-prefix match: "gpt-4o-mini-2024-07-18" -> "gpt-4o-mini".
        // Iterate from longest key downward so "gpt-4o-mini" beats "gpt-4o".
        foreach (var key in Prices.Keys.OrderByDescending(k => k.Length))
        {
            if (deploymentName.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                return Prices[key];
        }
        return null;
    }

    /// <summary>
    /// True if this deployment is in the price table (dollars are honest).
    /// </summary>
    internal static bool HasPrice(string? deploymentName) => LookupPrice(deploymentName) is not null;

    /// <summary>
    /// Compute USD cost for a single call. Returns null if model unknown.
    /// </summary>
    internal static decimal? ComputeUsd(string? deploymentName, int inputTokens, int outputTokens)
    {
        var price = LookupPrice(deploymentName);
        if (price is null) return null;
        return inputTokens * price.Value.InputPer1K / 1000m
             + outputTokens * price.Value.OutputPer1K / 1000m;
    }

    /// <summary>
    /// Build a CostEntry for a single completion. Negative inputs are
    /// clamped to zero defensively (the SDK has been observed to return
    /// 0 / nulls on stream errors; we never want a negative receipt).
    /// </summary>
    internal static CostEntry Entry(string? deploymentName, int? inputTokens, int? outputTokens)
    {
        int inp = Math.Max(0, inputTokens ?? 0);
        int outp = Math.Max(0, outputTokens ?? 0);
        return new CostEntry(inp, outp, ComputeUsd(deploymentName, inp, outp));
    }

    /// <summary>
    /// Format a single-line receipt for stderr. Always tokens; dollars
    /// only when the price-table lookup succeeded.
    ///
    /// Examples:
    ///   [cost] in=1234 out=567 total=1801 tokens (~$0.0042 @ gpt-4o)
    ///   [cost] in=1234 out=567 total=1801 tokens (model 'foo' not in price table)
    /// </summary>
    internal static string FormatReceipt(CostEntry entry, string? deploymentName)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("[cost] in=").Append(entry.InputTokens)
          .Append(" out=").Append(entry.OutputTokens)
          .Append(" total=").Append(entry.TotalTokens)
          .Append(" tokens");

        if (entry.UsdCost is decimal usd && !string.IsNullOrWhiteSpace(deploymentName))
        {
            sb.Append(" (~$").Append(usd.ToString("0.0000", CultureInfo.InvariantCulture))
              .Append(" @ ").Append(deploymentName).Append(')');
        }
        else if (!string.IsNullOrWhiteSpace(deploymentName))
        {
            sb.Append(" (model '").Append(deploymentName).Append("' not in price table)");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Format a multi-call running total receipt -- used by agent and Ralph
    /// modes to show what the whole loop cost, not just the last call.
    /// </summary>
    internal static string FormatTotalReceipt(CostAccumulator acc, string? deploymentName, string label)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("[cost] ").Append(label).Append(": calls=").Append(acc.Calls)
          .Append(" in=").Append(acc.InputTokens)
          .Append(" out=").Append(acc.OutputTokens)
          .Append(" total=").Append(acc.TotalTokens)
          .Append(" tokens");

        if (acc.HasAnyKnownCost && !string.IsNullOrWhiteSpace(deploymentName))
        {
            sb.Append(" (~$").Append(acc.UsdCost.ToString("0.0000", CultureInfo.InvariantCulture))
              .Append(" @ ").Append(deploymentName).Append(')');
        }
        else if (!string.IsNullOrWhiteSpace(deploymentName))
        {
            sb.Append(" (model '").Append(deploymentName).Append("' not in price table)");
        }
        return sb.ToString();
    }
}

/// <summary>
/// Tiny mutable accumulator for multi-call modes (agent loop rounds,
/// Ralph iterations). Not thread-safe -- caller serialises via the
/// outer await loop.
/// </summary>
internal sealed class CostAccumulator
{
    public int Calls { get; private set; }
    public int InputTokens { get; private set; }
    public int OutputTokens { get; private set; }
    public decimal UsdCost { get; private set; }
    public bool HasAnyKnownCost { get; private set; }

    public int TotalTokens => InputTokens + OutputTokens;

    public void Add(CostEntry entry)
    {
        Calls++;
        InputTokens += entry.InputTokens;
        OutputTokens += entry.OutputTokens;
        if (entry.UsdCost is decimal usd)
        {
            UsdCost += usd;
            HasAnyKnownCost = true;
        }
    }
}
