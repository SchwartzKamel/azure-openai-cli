using System.Text;
using AzureOpenAI_CLI.Cli;

namespace AzureOpenAI_CLI.Resolution;

/// <summary>
/// S04E08 -- The Onboarding. Turns the S04E05 picker's decision into a short,
/// plain-language "why this model" panel for the end of the setup wizard.
///
/// Contract:
/// <list type="bullet">
///   <item>Pure: no I/O, no env reads, no clock. The caller passes the registry
///     and the models the user just typed; the caller prints the lines.</item>
///   <item>No second decision-maker. The default model is whatever
///     <see cref="ResolveSmartDefault.Pick"/> returns for the same input, so the
///     panel cannot disagree with what hopper will actually do on the next run.</item>
///   <item>No new reason code. The four locked codes stay four; this class only
///     branches on them to choose wording.</item>
///   <item>Screen-reader friendly: ASCII only, no color, no box drawing, no
///     reliance on glyphs, every line at most <see cref="MaxLineWidth"/> columns.</item>
/// </list>
///
/// Why not print <see cref="ResolutionResult.HumanReason"/> directly? It names
/// the env var and says "head of AZUREOPENAIMODEL" -- accurate, but jargon to a
/// first-time user. Its FALLBACK wording ("not in registry; see --doctor or fix
/// AZUREOPENAIMODEL") also reads as an error, when for an Azure deployment name
/// it is routine: deployment names are chosen by the user and the capability
/// gate deliberately passes unregistered models through (S04E03).
/// </summary>
internal static class WizardModelExplainer
{
    /// <summary>Widest line the panel will emit for ordinary input.</summary>
    internal const int MaxLineWidth = 78;

    /// <summary>
    /// Explain which of <paramref name="azureModelsCsv"/> becomes the default and
    /// why. Returns an empty array when there is nothing to explain (no models,
    /// or the picker returned no model).
    /// </summary>
    internal static string[] Explain(IModelRegistry registry, string? azureModelsCsv)
    {
        ArgumentNullException.ThrowIfNull(registry);

        var models = (azureModelsCsv ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (models.Length == 0)
        {
            return Array.Empty<string>();
        }

        // The one and only decision. Same inputs the runtime uses: no explicit
        // model, no axis, the allowlist in the order the user typed it.
        var pick = ResolveSmartDefault.Pick(
            registry,
            new ResolverInputs(ExplicitModel: null, PreferAxis: null, Allowlist: models));
        if (string.IsNullOrEmpty(pick.Model))
        {
            return Array.Empty<string>();
        }

        var name = CapabilityRejection.Scrub(pick.Model);
        var lines = new List<string>
        {
            "Azure default model: " + name,
            "  Why: it is first in your list, and the first model is the default.",
        };

        // FALLBACK with a non-empty model means "the head is not in the built-in
        // registry". Explain it as the normal case it is, not as an error.
        if (pick.ReasonCode == ResolutionReason.FALLBACK)
        {
            lines.AddRange(Wrap(
                "Note: '" + name + "' is not in hopper's built-in model list. That is "
                + "normal for a custom deployment name. hopper still uses it, but skips "
                + "capability checks for names it does not know.",
                indent: "  "));
        }

        if (models.Length > 1)
        {
            var others = string.Join(", ", models.Skip(1).Select(CapabilityRejection.Scrub));
            lines.AddRange(Wrap("Also in your list: " + others + ".", indent: "  "));
            lines.Add("  Use one for a single call with: --model <name>");
        }

        lines.Add("  To change the default, re-run 'hopper --setup' and list that model first.");
        return lines.ToArray();
    }

    /// <summary>
    /// Greedy word wrap to <see cref="MaxLineWidth"/>. A single word longer than
    /// the width is left whole on its own line (never split mid-token: a split
    /// model name would be copied wrongly).
    /// </summary>
    private static List<string> Wrap(string text, string indent)
    {
        var result = new List<string>();
        var line = new StringBuilder(indent);
        var lineHasWord = false;

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var needed = (lineHasWord ? 1 : 0) + word.Length;
            if (lineHasWord && line.Length + needed > MaxLineWidth)
            {
                result.Add(line.ToString());
                line.Clear().Append(indent);
                lineHasWord = false;
            }

            if (lineHasWord)
            {
                line.Append(' ');
            }
            line.Append(word);
            lineHasWord = true;
        }

        if (lineHasWord)
        {
            result.Add(line.ToString());
        }
        return result;
    }
}
