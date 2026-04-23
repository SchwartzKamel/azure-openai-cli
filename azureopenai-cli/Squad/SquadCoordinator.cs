namespace AzureOpenAI_CLI.Squad;

/// <summary>
/// Routes tasks to the best persona based on configured routing rules.
/// Uses keyword matching against the task prompt.
/// </summary>
internal sealed class SquadCoordinator
{
    private readonly SquadConfig _config;

    public SquadCoordinator(SquadConfig config)
    {
        _config = config;
    }

    // Generic persona names collide with task vocabulary (the noun "security" maps
    // to both a persona name AND its routing pattern). Direct-name precedence is
    // therefore restricted to NON-generic names -- cast personas, kebab-case names,
    // and any custom personas the user adds. The 5 stock generics still route via
    // keyword scoring, preserving documented surprises in PersonaBehaviorTests.
    private static readonly HashSet<string> GenericNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "coder", "reviewer", "architect", "writer", "security",
    };

    /// <summary>
    /// Select the best persona for a given task prompt.
    /// Direct persona-name matches (excluding the 5 stock generics) take precedence:
    /// "kramer code review" routes to <c>kramer</c>, not <c>coder</c>. Otherwise falls
    /// through to keyword scoring across the configured routing rules.
    /// Returns null if no routing match is found.
    /// </summary>
    public PersonaConfig? Route(string taskPrompt)
    {
        if (string.IsNullOrWhiteSpace(taskPrompt) || _config.Personas.Count == 0)
            return _config.Personas.FirstOrDefault();

        var prompt = taskPrompt.ToLowerInvariant();

        // 1. Direct-name precedence (cast + custom personas only).
        var tokens = TokenizeForNameMatch(prompt);
        var directMatch = _config.Personas
            .Where(p => !GenericNames.Contains(p.Name))
            .FirstOrDefault(p => tokens.Contains(p.Name.ToLowerInvariant()));
        if (directMatch != null)
            return directMatch;

        if (_config.Routing.Count == 0)
            return _config.Personas.FirstOrDefault();

        // 2. Keyword scoring across routing rules.
        var bestMatch = _config.Routing
            .Select(rule => new
            {
                Rule = rule,
                Score = rule.Pattern.ToLowerInvariant()
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Count(keyword => prompt.Contains(keyword))
            })
            .Where(m => m.Score > 0)
            .OrderByDescending(m => m.Score)
            .FirstOrDefault();

        if (bestMatch != null)
        {
            return _config.GetPersona(bestMatch.Rule.Persona);
        }

        // Fallback: first persona
        return _config.Personas.FirstOrDefault();
    }

    /// <summary>
    /// Alias for <see cref="Route"/>. Named for tests and call-sites that want to
    /// emphasize the keyword/name routing semantics over the bare verb "Route".
    /// </summary>
    public PersonaConfig? RouteByKeyword(string taskPrompt) => Route(taskPrompt);

    private static HashSet<string> TokenizeForNameMatch(string prompt)
    {
        // Split on whitespace and most punctuation, but PRESERVE '-' so kebab-case
        // persona names ("larry-david", "frank-costanza") survive as single tokens.
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = new System.Text.StringBuilder();
        foreach (var ch in prompt)
        {
            if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
            {
                current.Append(ch);
            }
            else
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
        }
        if (current.Length > 0)
            tokens.Add(current.ToString());
        return tokens;
    }
}
