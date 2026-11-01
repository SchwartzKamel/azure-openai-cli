using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AzureOpenAI_CLI.Patterns;

/// <summary>
/// FR-015: Reusable prompt pattern definition (built-in or loaded from
/// <c>~/.config/azureopenai-cli/patterns/*.json</c>).
/// Serialized via <see cref="AppJsonContext"/> (100% Native AOT compatible).
/// </summary>
internal sealed record PatternTemplate(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("system_prompt")] string SystemPrompt,
    [property: JsonPropertyName("temperature")] float? Temperature = null,
    [property: JsonPropertyName("source")] string Source = "builtin"
);

/// <summary>
/// FR-015: Registry of built-in and user-defined prompt patterns for instant
/// shell/automation workflows (<c>--pattern &lt;name&gt;</c>, <c>--patterns [--json]</c>,
/// and MCP <c>prompts/list</c> / <c>prompts/get</c>).
/// </summary>
internal static class PatternRegistry
{
    private static readonly Regex ValidPatternNameRegex =
        new(@"^[a-zA-Z][a-zA-Z0-9_-]{0,63}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    internal static readonly IReadOnlyList<PatternTemplate> BuiltInPatterns = new[]
    {
        new PatternTemplate(
            Name: "summarize",
            Description: "Summarize input text into concise bullet points focusing on key takeaways and action items.",
            SystemPrompt: "You are a precision summarizer. Summarize the provided input into concise, high-signal bullet points. Highlight key facts, decisions, and action items. Omit filler.",
            Temperature: 0.25f),
        new PatternTemplate(
            Name: "fix-grammar",
            Description: "Correct grammar, spelling, and awkward phrasing while preserving the original meaning and tone.",
            SystemPrompt: "Fix grammar, spelling, and punctuation in the user's text. Preserve the original tone and meaning. Output ONLY the corrected text without commentary.",
            Temperature: 0.2f),
        new PatternTemplate(
            Name: "commit-message",
            Description: "Generate a Conventional Commits message (type(scope): subject + concise body) from a git diff or summary.",
            SystemPrompt: "You are a git commit message generator. Write a clean Conventional Commits message (e.g., feat:, fix:, refactor:, docs:, test:) under 72 characters for the subject line, followed by a concise bulleted body if needed. Output ONLY the commit message.",
            Temperature: 0.2f),
        new PatternTemplate(
            Name: "explain-code",
            Description: "Explain how a piece of code works, its invariants, complexity, and edge cases.",
            SystemPrompt: "Explain the provided code clearly and concisely. Cover its purpose, execution flow, key invariants, time/space complexity, and any subtle edge cases or bugs.",
            Temperature: 0.3f),
        new PatternTemplate(
            Name: "shell-oneliner",
            Description: "Produce a safe, portable POSIX/bash shell one-liner for the requested task.",
            SystemPrompt: "You are a Unix shell expert. Output ONLY the exact POSIX/bash command or pipeline that accomplishes the user's request. Do not include markdown fences or explanations.",
            Temperature: 0.1f),
        new PatternTemplate(
            Name: "json-extract",
            Description: "Extract structured entities and facts from unstructured input as valid JSON.",
            SystemPrompt: "Extract the key structured data from the user's input and return ONLY valid, well-structured JSON without markdown code fences or prose.",
            Temperature: 0.1f),
        new PatternTemplate(
            Name: "code-review",
            Description: "Review code or a diff for correctness, concurrency, performance, and maintainability bugs.",
            SystemPrompt: "You are a staff engineer performing a high-signal code review. Identify concrete bugs, race conditions, edge-case failures, and maintainability issues. Cite exact lines/symbols and propose minimal fixes.",
            Temperature: 0.2f),
        new PatternTemplate(
            Name: "security-audit",
            Description: "Audit code, configs, or scripts for OWASP, injection, SSRF, secret leakage, and privilege-escalation risks.",
            SystemPrompt: "You are an application security auditor. Analyze the input for concrete vulnerabilities (command injection, SSRF, path traversal, hardcoded secrets, insecure defaults) and provide actionable remediations.",
            Temperature: 0.15f),
    };

    /// <summary>
    /// Returns all available patterns (built-in patterns plus any user-defined
    /// <c>*.json</c> patterns in <c>$XDG_CONFIG_HOME/azureopenai-cli/patterns</c>
    /// or <c>~/.config/azureopenai-cli/patterns</c>). User patterns with the same
    /// name as a built-in override the built-in so operators can customize defaults.
    /// </summary>
    public static IReadOnlyList<PatternTemplate> ListAll(string? customPatternDir = null)
    {
        var map = new Dictionary<string, PatternTemplate>(StringComparer.OrdinalIgnoreCase);
        foreach (var b in BuiltInPatterns)
        {
            map[b.Name] = b;
        }

        foreach (var dir in GetUserPatternDirectories())
        {
            LoadFromDirectory(dir, "user-pattern", map);
        }

        if (!string.IsNullOrWhiteSpace(customPatternDir))
        {
            LoadFromDirectory(customPatternDir, "custom-pattern", map);
        }

        return map.Values
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static PatternTemplate? Resolve(string? name, string? customPatternDir = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return ListAll(customPatternDir)
            .FirstOrDefault(p => p.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> GetUserPatternDirectories()
    {
        var dirs = new List<string>();
        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrWhiteSpace(xdg))
        {
            dirs.Add(Path.Combine(xdg, "azureopenai-cli", "patterns"));
        }

        var home = Environment.GetEnvironmentVariable("HOME");
        if (string.IsNullOrWhiteSpace(home))
        {
            home = Environment.GetEnvironmentVariable("USERPROFILE");
        }
        if (string.IsNullOrWhiteSpace(home))
        {
            home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        if (!string.IsNullOrWhiteSpace(home))
        {
            var dotConfig = Path.Combine(home, ".config", "azureopenai-cli", "patterns");
            if (!dirs.Contains(dotConfig, StringComparer.Ordinal))
            {
                dirs.Add(dotConfig);
            }
        }
        return dirs;
    }

    private static void LoadFromDirectory(
        string dirPath,
        string sourceTag,
        Dictionary<string, PatternTemplate> target)
    {
        if (string.IsNullOrWhiteSpace(dirPath) || !Directory.Exists(dirPath))
            return;

        string[] files;
        try
        {
            files = Directory.GetFiles(dirPath, "*.json");
            Array.Sort(files, StringComparer.Ordinal);
        }
        catch
        {
            return;
        }

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var parsed = JsonSerializer.Deserialize(json, AppJsonContext.Default.PatternTemplate);
                if (parsed is null
                    || string.IsNullOrWhiteSpace(parsed.Name)
                    || !ValidPatternNameRegex.IsMatch(parsed.Name)
                    || string.IsNullOrWhiteSpace(parsed.SystemPrompt))
                {
                    continue;
                }

                target[parsed.Name] = parsed with
                {
                    Description = string.IsNullOrWhiteSpace(parsed.Description) ? parsed.Name : parsed.Description,
                    Source = sourceTag,
                };
            }
            catch
            {
                // Ignore malformed pattern files
            }
        }
    }
}
