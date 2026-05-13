namespace AzureOpenAI_CLI.Registry;

// S04E01 -- The Registry (Kramer). Allowed capability tag vocabulary.
// Locked for E01. E03 (*The Capabilities*) adds more tags via ADR-012.
// Any unknown tag in a registry entry causes rc=99 at load time.

/// <summary>
/// Allowed capability tag constants and validator. The set is intentionally
/// small for E01; E03 expands it via ADR-012. Unknown tags cause rc=99 at
/// registry load time so typos never silently pass through routing logic.
/// </summary>
internal static class ModelCapability
{
    /// <summary>
    /// The complete set of recognized capability tags for E01.
    /// Changes to this set require a migration note in ADR-012.
    /// </summary>
    public static readonly HashSet<string> AllowedTags = new(StringComparer.Ordinal)
    {
        "tool_calls",
        "vision_in",
        "vision_out",
        "json_mode",
        "streaming",
        "system_prompt",
    };

    /// <summary>Returns true when <paramref name="tag"/> is in the allowed set.</summary>
    public static bool IsValid(string tag) => AllowedTags.Contains(tag);

    /// <summary>
    /// Throws <see cref="ArgumentException"/> on the first unknown tag found.
    /// The exception message names both the offending tag and the entry it
    /// came from so the error message surfaced via rc=99 is actionable.
    /// </summary>
    public static void ValidateOrThrow(IEnumerable<string> tags, string entryName)
    {
        foreach (var tag in tags)
        {
            if (!IsValid(tag))
                throw new ArgumentException(
                    $"Unknown capability tag '{tag}' in registry entry '{entryName}'.");
        }
    }
}
