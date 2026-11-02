using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AzureOpenAI_CLI.Eval;

// S04E10 -- The Score. The shipping fixture schema for `hopper eval`.
//
// It is a strict superset of the design in docs/prompts/eval-harness.md, so the existing
// fixtures (docs/prompts/fixtures/coder.json) load unchanged. What is new is
// `assertions`: deterministic, structural checks that can be scored without a judge.
// Freeform `expected_traits` / `forbidden_traits` stay as documentation of intent and are
// reported as UNGRADED; the trait judge (human or LLM) is still the deferred hard problem
// and this runner does not pretend otherwise.

/// <summary>One deterministic check against a model's output.</summary>
internal sealed class EvalAssertion
{
    /// <summary>One of <see cref="EvalAssertions.Types"/>.</summary>
    [JsonPropertyName("type")] public string Type { get; set; } = "";

    /// <summary>Text for contains / not_contains / starts_with / regex / not_regex.</summary>
    [JsonPropertyName("value")] public string? Value { get; set; }

    /// <summary>Character count for min_chars / max_chars.</summary>
    [JsonPropertyName("limit")] public int? Limit { get; set; }

    /// <summary>Case-insensitive match for the text and regex types.</summary>
    [JsonPropertyName("ignore_case")] public bool IgnoreCase { get; set; }
}

internal sealed class EvalFixture
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";

    /// <summary>Persona from .squad.json. Omit to evaluate the default CLI behavior.</summary>
    [JsonPropertyName("persona")] public string? Persona { get; set; }

    /// <summary>Recorded for the human reader; nothing enforces it yet (personas carry no version).</summary>
    [JsonPropertyName("persona_version")] public string? PersonaVersion { get; set; }

    [JsonPropertyName("user_prompt")] public string UserPrompt { get; set; } = "";

    /// <summary>Defaults to 0.0 when omitted, so a fixture is deterministic unless it opts out.</summary>
    [JsonPropertyName("temperature")] public float? Temperature { get; set; }

    [JsonPropertyName("max_tokens")] public int? MaxTokens { get; set; }

    [JsonPropertyName("expected_traits")] public List<string> ExpectedTraits { get; set; } = new();
    [JsonPropertyName("forbidden_traits")] public List<string> ForbiddenTraits { get; set; } = new();

    /// <summary>Non-null marks a safety fixture: a failure blocks regardless of --fail-under.</summary>
    [JsonPropertyName("safety_assertion")] public string? SafetyAssertion { get; set; }

    [JsonPropertyName("assertions")] public List<EvalAssertion> Assertions { get; set; } = new();

    internal bool IsSafety => !string.IsNullOrWhiteSpace(SafetyAssertion);
}

/// <summary>Object form of a suite file: <c>{ "name": ..., "fixtures": [...] }</c>.</summary>
internal sealed class EvalSuiteFile
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("fixtures")] public List<EvalFixture> Fixtures { get; set; } = new();
}

internal sealed class EvalSuite
{
    public string Name { get; init; } = "";
    public List<EvalFixture> Fixtures { get; init; } = new();
}

internal static class EvalSuiteLoader
{
    /// <summary>Suite files are small hand-written JSON; anything bigger is a mistake.</summary>
    internal const long MaxSuiteBytes = 1_048_576;

    private const int MaxErrorsListed = 10;

    /// <summary>
    /// Load and fully validate a suite. On failure <paramref name="error"/> lists up to
    /// <see cref="MaxErrorsListed"/> problems so one run fixes them all.
    /// Accepts a bare JSON array of fixtures (the existing design doc) or the object form.
    /// </summary>
    internal static bool TryLoad(string path, out EvalSuite? suite, out string? error)
    {
        suite = null;
        error = null;

        if (string.IsNullOrWhiteSpace(path)) { error = "no suite file given"; return false; }
        if (!File.Exists(path)) { error = $"suite file not found: {path}"; return false; }

        var length = new FileInfo(path).Length;
        if (length > MaxSuiteBytes)
        {
            error = $"suite file is {length} bytes; the limit is {MaxSuiteBytes}";
            return false;
        }

        string text;
        try { text = File.ReadAllText(path); }
        catch (IOException ex) { error = $"cannot read suite file: {ex.Message}"; return false; }
        catch (UnauthorizedAccessException ex) { error = $"cannot read suite file: {ex.Message}"; return false; }

        return TryParse(text, Path.GetFileNameWithoutExtension(path), out suite, out error);
    }

    /// <summary>
    /// Parse and fully validate suite JSON from text. Shared by file suites and the embedded
    /// corpora, so a built-in corpus is held to exactly the rules a user's suite is.
    /// <paramref name="fallbackName"/> names the suite when the JSON does not.
    /// </summary>
    internal static bool TryParse(string text, string fallbackName, out EvalSuite? suite, out string? error)
    {
        suite = null;
        error = null;

        List<EvalFixture> fixtures;
        string? name = null;
        try
        {
            using var doc = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

            switch (doc.RootElement.ValueKind)
            {
                case JsonValueKind.Array:
                    fixtures = JsonSerializer.Deserialize(
                        doc.RootElement.GetRawText(), AppJsonContext.Default.ListEvalFixture) ?? new();
                    break;
                case JsonValueKind.Object:
                    var file = JsonSerializer.Deserialize(
                        doc.RootElement.GetRawText(), AppJsonContext.Default.EvalSuiteFile);
                    fixtures = file?.Fixtures ?? new();
                    name = file?.Name;
                    break;
                default:
                    error = "a suite must be a JSON array of fixtures or an object with a \"fixtures\" array";
                    return false;
            }
        }
        catch (JsonException ex)
        {
            error = $"suite is not valid JSON: {ex.Message}";
            return false;
        }

        var problems = Validate(fixtures);
        if (problems.Count > 0)
        {
            var shown = problems.Take(MaxErrorsListed).ToList();
            if (problems.Count > MaxErrorsListed) shown.Add($"... and {problems.Count - MaxErrorsListed} more");
            error = string.Join("\n", shown);
            return false;
        }

        suite = new EvalSuite
        {
            Name = string.IsNullOrWhiteSpace(name) ? fallbackName : name.Trim(),
            Fixtures = fixtures,
        };
        return true;
    }

    internal static List<string> Validate(List<EvalFixture> fixtures)
    {
        var problems = new List<string>();
        if (fixtures.Count == 0)
        {
            problems.Add("the suite has no fixtures");
            return problems;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < fixtures.Count; i++)
        {
            var f = fixtures[i];
            var where = string.IsNullOrWhiteSpace(f.Id) ? $"fixture #{i + 1}" : $"fixture '{f.Id}'";

            if (string.IsNullOrWhiteSpace(f.Id)) problems.Add($"{where}: \"id\" is required");
            else if (f.Id.Any(char.IsControl)) problems.Add($"{where}: \"id\" must not contain control characters");
            else if (!seen.Add(f.Id)) problems.Add($"{where}: duplicate id (ids are compared case-insensitively)");

            if (string.IsNullOrWhiteSpace(f.UserPrompt)) problems.Add($"{where}: \"user_prompt\" is required");
            if (f.Temperature is < 0f or > 2f) problems.Add($"{where}: \"temperature\" must be between 0.0 and 2.0");
            if (f.MaxTokens is <= 0) problems.Add($"{where}: \"max_tokens\" must be positive");

            for (var a = 0; a < f.Assertions.Count; a++)
            {
                var problem = EvalAssertions.Validate(f.Assertions[a]);
                if (problem is not null) problems.Add($"{where}: assertion #{a + 1}: {problem}");
            }
        }
        return problems;
    }
}
