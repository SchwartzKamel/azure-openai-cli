using System.Reflection;

namespace AzureOpenAI_CLI.Eval;

/// <summary>
/// S04E11 -- The Corpus. The eval suites that ship inside the binary, run with
/// <c>az-ai eval --corpus &lt;name&gt;</c> so no file path is needed (a Homebrew install has no repo).
///
/// Policy, each line earned by a failure mode:
/// <list type="bullet">
///   <item><b>Bounded.</b> Each corpus and the total have a hard byte cap, enforced by a test, so a
///     growing corpus cannot quietly bloat the binary or the AOT image.</item>
///   <item><b>Synthetic only.</b> No user prompts, no real credentials, hosts or addresses. A corpus
///     that is compiled into a distributed binary is published to everyone; anything real in it leaks.</item>
///   <item><b>Same rules as a user suite.</b> Loaded through <see cref="EvalSuiteLoader.TryParse"/>, so
///     a built-in corpus cannot pass validation a user suite would fail.</item>
/// </list>
/// </summary>
internal static class Corpora
{
    /// <summary>Hard cap per corpus file, in bytes.</summary>
    internal const int MaxCorpusBytes = 16 * 1024;

    /// <summary>Hard cap on all corpora together, in bytes.</summary>
    internal const int MaxTotalBytes = 48 * 1024;

    private const string ResourcePrefix = "AzureOpenAI_CLI.Eval.Corpora.";

    /// <summary>Corpus names in the order shown to users.</summary>
    internal static readonly string[] Names = { "smoke", "persona-voice", "refusal" };

    internal static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        ["smoke"] = "Does this deployment work at all? Short factual and format-following prompts.",
        ["persona-voice"] = "Does each built-in persona still sound like itself? Checked structurally.",
        ["refusal"] = "Does the safety clause fire? Adversarial prompts; every fixture is a safety fixture.",
    };

    internal static bool IsKnown(string? name) => name is not null && Array.IndexOf(Names, name) >= 0;

    /// <summary>Raw bytes of an embedded corpus, or null when the name is unknown or missing.</summary>
    internal static byte[]? ReadBytes(string name)
    {
        if (!IsKnown(name)) return null;
        using var stream = typeof(Corpora).Assembly.GetManifestResourceStream(ResourcePrefix + name + ".json");
        if (stream is null) return null;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>Load and validate an embedded corpus by name.</summary>
    internal static bool TryLoad(string name, out EvalSuite? suite, out string? error)
    {
        suite = null;
        if (!IsKnown(name))
        {
            error = $"unknown corpus '{name}'. Available: {string.Join(", ", Names)}";
            return false;
        }

        var bytes = ReadBytes(name);
        if (bytes is null)
        {
            error = $"corpus '{name}' is missing from this build";
            return false;
        }
        if (bytes.Length > MaxCorpusBytes)
        {
            error = $"corpus '{name}' is {bytes.Length} bytes; the limit is {MaxCorpusBytes}";
            return false;
        }

        return EvalSuiteLoader.TryParse(System.Text.Encoding.UTF8.GetString(bytes), name, out suite, out error);
    }
}
