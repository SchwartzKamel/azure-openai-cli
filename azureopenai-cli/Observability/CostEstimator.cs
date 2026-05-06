using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzureOpenAI_CLI.Observability;

/// <summary>
/// FR-015: pre-flight cost estimator. No API call, no network, no tokens burned.
/// Uses a naive chars/4 approximation (BPE tokenizers average ~4 chars/token for
/// English text). This is a *rough* estimate — always honest about the approximation.
/// Actual counts differ for code, non-Latin scripts, and structured data.
///
/// Morty: "You paid HOW much for a pair of sneakers? At least now you'll see the
/// price tag *before* we ring it up."
/// </summary>
internal static class CostEstimator
{
    /// <summary>
    /// Average characters per BPE token used for the rough estimate.
    /// English prose lands around 3.8–4.2; 4 is a safe, widely-cited default.
    /// </summary>
    public const double CharsPerTokenApprox = 4.0;

    /// <summary>
    /// Estimate input token count from a prompt string using the chars/4 rule.
    /// Returns at least 1 for any non-empty prompt to avoid $0 estimates that
    /// mask a non-trivial request. Empty / whitespace prompts return 0.
    /// </summary>
    public static int EstimateInputTokens(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return 0;
        int chars = prompt.Length;
        int tokens = (int)Math.Ceiling(chars / CharsPerTokenApprox);
        return Math.Max(1, tokens);
    }

    /// <summary>
    /// Compute an estimate for <paramref name="model"/> given the prompt and an
    /// optional output-token cap. Returns null if the model is unknown — the
    /// caller should surface a clean error (Morty's rule: no faked numbers).
    /// </summary>
    public static EstimateResult? Estimate(string model, string? prompt, int? outputMaxTokens)
    {
        if (!CostHook.TryGetRates(model, out var inputPer1K, out var outputPer1K))
        {
            return null;
        }

        int inputTokens = EstimateInputTokens(prompt);
        double inputUsd = (inputTokens / 1000.0) * inputPer1K;

        int? outMax = outputMaxTokens;
        double? outUsdMax = null;
        double totalUsdMax = inputUsd;

        if (outMax.HasValue && outMax.Value > 0)
        {
            outUsdMax = (outMax.Value / 1000.0) * outputPer1K;
            totalUsdMax = inputUsd + outUsdMax.Value;
        }

        return new EstimateResult(
            Model: model,
            InputTokensEst: inputTokens,
            InputUsd: inputUsd,
            OutputMaxTokens: outMax,
            OutputUsdMax: outUsdMax,
            TotalUsdMax: totalUsdMax,
            Approximation: $"chars/{CharsPerTokenApprox:0.#} — rough, actual tokens vary (code, non-Latin, JSON all differ)"
        );
    }

    /// <summary>
    /// S03E12 -- *The Receipt*. Closes Kramer Finding 5 from S03E09: the
    /// model-keyed <see cref="CostHook"/> table cannot price requests routed
    /// through <c>OpenAiCompatAdapter</c>, because the operator-facing knob
    /// for those is the <i>preset</i> name (openai / groq / together /
    /// cloudflare), not the upstream model id. This method consults the
    /// preset-keyed placeholder table in <see cref="CompatCostRates"/> and
    /// returns an <see cref="EstimateResult"/> shaped identically to the
    /// model-keyed path so renderers do not branch.
    ///
    /// Behaviour:
    ///   <list type="bullet">
    ///     <item>Preset known with rates -- numeric estimate, model field set
    ///           to <c>"&lt;preset&gt;:&lt;modelOrEmpty&gt;"</c> for traceability.</item>
    ///     <item>Preset null/empty -- returns an estimate carrying the literal
    ///           <c>[REDACTED:provider]</c> in the model field, zero rates,
    ///           and an "unknown rate" approximation note.</item>
    ///     <item>Preset non-empty but unknown -- same redacted shape, with the
    ///           approximation note carrying "unknown rate, $? estimate" so
    ///           the operator sees the actionable message in <c>FormatText</c>.</item>
    ///   </list>
    /// Never returns null. Never throws. The compat path must not be the
    /// place an operator first learns we have not priced their preset.
    /// </summary>
    public static EstimateResult EstimateForCompatPreset(string? preset, string? modelHint, string? prompt, int? outputMaxTokens)
    {
        int inputTokens = EstimateInputTokens(prompt);
        string label;
        string approxNote;
        double inputPer1K = 0;
        double outputPer1K = 0;

        if (string.IsNullOrWhiteSpace(preset))
        {
            // Defensive: caller did not resolve a preset. Surface the
            // [REDACTED:provider] sentinel so downstream log-scrubbers and
            // operator eyeballs both see "we did not bill this honestly."
            label = "[REDACTED:provider]";
            approxNote = "unknown rate, $? estimate (preset unresolved -- emitting [REDACTED:provider])";
        }
        else if (CompatCostRates.TryGetRates(preset, out inputPer1K, out outputPer1K))
        {
            var modelTail = string.IsNullOrWhiteSpace(modelHint) ? "" : $":{modelHint!.Trim()}";
            label = $"{preset.Trim()}{modelTail}";
            var known = string.Join(", ", CompatCostRates.KnownPresets());
            approxNote = $"chars/{CharsPerTokenApprox:0.#} -- PLACEHOLDER preset rates (known: {known}); update from upstream pricing.";
        }
        else
        {
            // Known shape, unknown preset. Both messages the brief asked for
            // get emitted -- the [REDACTED:provider] in the label, the
            // "unknown rate, $? estimate" in the approximation note.
            label = "[REDACTED:provider]";
            var known = string.Join(", ", CompatCostRates.KnownPresets());
            approxNote = $"unknown rate, $? estimate -- preset '{preset}' not in known list ({known}); emitting [REDACTED:provider].";
        }

        double inputUsd = (inputTokens / 1000.0) * inputPer1K;
        int? outMax = outputMaxTokens;
        double? outUsdMax = null;
        double totalUsdMax = inputUsd;
        if (outMax.HasValue && outMax.Value > 0)
        {
            outUsdMax = (outMax.Value / 1000.0) * outputPer1K;
            totalUsdMax = inputUsd + outUsdMax.Value;
        }

        return new EstimateResult(
            Model: label,
            InputTokensEst: inputTokens,
            InputUsd: inputUsd,
            OutputMaxTokens: outMax,
            OutputUsdMax: outUsdMax,
            TotalUsdMax: totalUsdMax,
            Approximation: approxNote);
    }

    /// <summary>
    /// Render a human-friendly multi-line estimate for stdout.
    /// </summary>
    public static string FormatText(EstimateResult r)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Cost estimate (model: {r.Model}) — NO API CALL MADE");
        sb.AppendLine($"  input tokens (est):  ~{r.InputTokensEst}  [approx: {r.Approximation}]");
        sb.AppendLine($"  input cost:          ${r.InputUsd:F6}");
        if (r.OutputMaxTokens.HasValue)
        {
            sb.AppendLine($"  output tokens (cap): {r.OutputMaxTokens.Value}");
            sb.AppendLine($"  output cost (max):   ${r.OutputUsdMax!.Value:F6}");
            sb.AppendLine($"  total (worst-case):  ${r.TotalUsdMax:F6}");
        }
        else
        {
            sb.AppendLine("  output cost:         unknown until generation (pass --estimate-with-output <max> for a worst-case bound)");
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Render the result as a single-line USD number for --raw consumers.
    /// Prints the worst-case total when an output cap is known, otherwise
    /// the input-only cost. Six-decimal fixed-point; no currency symbol.
    /// </summary>
    public static string FormatRaw(EstimateResult r)
    {
        return r.TotalUsdMax.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Render the result as structured JSON (AOT-safe via AppJsonContext).
    /// </summary>
    public static string FormatJson(EstimateResult r)
    {
        return JsonSerializer.Serialize(r, AppJsonContext.Default.EstimateResult);
    }
}

/// <summary>
/// FR-015 JSON schema for <c>--estimate --json</c>. Output rate × max tokens is
/// only included when the caller supplied an output cap — otherwise those
/// fields are null so consumers can tell "unknown" from "zero".
/// </summary>
internal record EstimateResult(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("input_tokens_est")] int InputTokensEst,
    [property: JsonPropertyName("input_usd")] double InputUsd,
    [property: JsonPropertyName("output_max_tokens")] int? OutputMaxTokens,
    [property: JsonPropertyName("output_usd_max")] double? OutputUsdMax,
    [property: JsonPropertyName("total_usd_max")] double TotalUsdMax,
    [property: JsonPropertyName("approximation")] string Approximation
);
