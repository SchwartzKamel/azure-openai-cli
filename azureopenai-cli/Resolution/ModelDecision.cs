using System.Text.Json.Serialization;
using AzureOpenAI_CLI.Cli;

namespace AzureOpenAI_CLI.Resolution;

/// <summary>
/// S04E09 -- The Override. The answer to "which model would hopper use, and why?",
/// for <c>--why-this-model</c>. Snake_case keys so it sits next to the other machine
/// envelopes (<c>--json</c>, <c>--jsonl</c>).
/// </summary>
internal sealed record ModelDecision(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("reason_code")] string ReasonCode,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("prefer")] string? Prefer,
    [property: JsonPropertyName("candidates")] string[] Candidates,
    [property: JsonPropertyName("note")] string? Note);

/// <summary>
/// Pure explanation of the model pick. No I/O, no env reads, no clock.
///
/// Contract:
/// <list type="bullet">
///   <item>No second decision-maker. The model and reason code are exactly what
///     <see cref="ResolveSmartDefault.Pick"/> returns, plus the same last-resort chain
///     <c>Program</c> applies when the picker has no answer (config default, then the
///     hard-coded fallback). The four reason codes stay four.</item>
///   <item>It tells the truth about a preference that did nothing. A stock install ships
///     cost tiers only, so <c>--prefer latency</c> has no data to rank and keeps list
///     order. That is said out loud in <see cref="ModelDecision.Note"/>; presenting it as
///     a decision would be a lie.</item>
/// </list>
/// </summary>
internal static class ModelDecisionExplainer
{
    /// <summary>The axes the picker understands, in the order shown to users.</summary>
    internal static readonly string[] Axes = { "cost", "latency", "quality" };

    /// <summary>
    /// Normalize a user-typed axis (trim, lower-case). Returns null for null/blank input.
    /// Does not validate; see <see cref="IsValidAxis"/>.
    /// </summary>
    internal static string? NormalizeAxis(string? raw)
        => string.IsNullOrWhiteSpace(raw) ? null : raw.Trim().ToLowerInvariant();

    internal static bool IsValidAxis(string? axis)
        => axis is not null && Array.IndexOf(Axes, axis) >= 0;

    /// <param name="registry">Model registry (seed or user override).</param>
    /// <param name="explicitModel">Alias-resolved <c>--model</c>, or null.</param>
    /// <param name="preferAxis">Normalized axis, or null.</param>
    /// <param name="allowlist">AZUREOPENAIMODEL entries in typed order.</param>
    /// <param name="configDefault">Config smart default used when the picker is empty.</param>
    /// <param name="hardFallback">Last-resort model name.</param>
    internal static ModelDecision Decide(
        IModelRegistry registry,
        string? explicitModel,
        string? preferAxis,
        string[] allowlist,
        string? configDefault,
        string hardFallback)
    {
        ArgumentNullException.ThrowIfNull(registry);
        allowlist ??= Array.Empty<string>();

        var pick = ResolveSmartDefault.Pick(
            registry, new ResolverInputs(explicitModel, preferAxis, allowlist));

        var model = string.IsNullOrEmpty(pick.Model)
            ? (!string.IsNullOrWhiteSpace(configDefault) ? configDefault! : hardFallback)
            : pick.Model;

        return new ModelDecision(
            Model: model,
            ReasonCode: pick.ReasonCode,
            Reason: pick.HumanReason,
            Prefer: preferAxis,
            Candidates: allowlist,
            Note: BuildNote(registry, explicitModel, preferAxis, allowlist));
    }

    private static string? BuildNote(
        IModelRegistry registry, string? explicitModel, string? axis, string[] allowlist)
    {
        if (axis is null) return null;

        if (!string.IsNullOrWhiteSpace(explicitModel))
        {
            return "--prefer was ignored because --model names the model directly.";
        }

        if (allowlist.Length < 2)
        {
            return "--prefer had nothing to choose between: AZUREOPENAIMODEL lists "
                + allowlist.Length + " model" + (allowlist.Length == 1 ? "" : "s")
                + ". List at least two, comma-separated.";
        }

        var anyData = allowlist.Any(n => ResolveSmartDefault.HasUsableTier(registry, n, axis));
        if (!anyData)
        {
            return "No '" + axis + "' tier data for any model in your list, so list order was "
                + "kept. Add " + TierField(axis) + " to your models in "
                + "~/.config/az-ai/registry.json (that file replaces the built-in list).";
        }

        return null;
    }

    private static string TierField(string axis) => axis switch
    {
        "cost" => "costTier (low, medium, high)",
        "latency" => "latencyTier (fast, medium, slow)",
        _ => "qualityTier (premium, standard, basic)",
    };

    /// <summary>
    /// Plain-text rendering for a terminal: labeled lines, ASCII, no color, no glyphs, so it
    /// reads cleanly in a screen reader and in a log. Echoed names are scrubbed.
    /// </summary>
    internal static string[] Render(ModelDecision d)
    {
        var lines = new List<string>
        {
            "Model: " + CapabilityRejection.Scrub(d.Model),
            "Reason code: " + d.ReasonCode,
            "Why: " + CapabilityRejection.Scrub(d.Reason),
            "Prefer: " + (d.Prefer ?? "(none)"),
            "Candidates: " + (d.Candidates.Length == 0
                ? "(none; AZUREOPENAIMODEL is not set)"
                : string.Join(", ", d.Candidates.Select(CapabilityRejection.Scrub))),
        };
        if (!string.IsNullOrEmpty(d.Note))
        {
            lines.Add("Note: " + d.Note);
        }
        return lines.ToArray();
    }
}
