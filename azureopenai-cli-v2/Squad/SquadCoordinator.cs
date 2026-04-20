namespace AzureOpenAI_CLI_V2.Squad;

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

    /// <summary>
    /// Select the best persona for a given task prompt.
    /// Returns null if no routing match is found.
    /// </summary>
    public PersonaConfig? Route(string taskPrompt)
    {
        if (string.IsNullOrWhiteSpace(taskPrompt) || _config.Routing.Count == 0)
            return _config.Personas.FirstOrDefault();

        var prompt = taskPrompt.ToLowerInvariant();

        // Score each routing rule by keyword match count
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
}
