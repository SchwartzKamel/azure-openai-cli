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

    /// <summary>
    /// S03E28 -- The Persona, Multi-Provider (Kramer). Fold a persona's
    /// optional <c>provider</c> / <c>model</c> pins into a base
    /// <see cref="ResolutionInputs"/> by populating the Persona rung
    /// (<see cref="ResolutionInputs.PersonaName"/>,
    /// <see cref="ResolutionInputs.PersonaProvider"/>,
    /// <see cref="ResolutionInputs.PersonaModel"/>). The Persona rung sits
    /// between profile and default in the precedence ladder, so a CLI
    /// flag, an <c>AZ_PROVIDER</c> / <c>AZ_MODEL</c> env, or a
    /// <c>--profile</c> pin still wins.
    ///
    /// <para>
    /// If the persona pins a provider whose credential env var is missing
    /// (per <see cref="PreferencesResolver.GetCredEnvVarName"/>) the pin
    /// is dropped, a single non-fatal warning is sent to <paramref name="warnSink"/>
    /// (silent under <c>--raw</c> / <c>--json</c> when callers pass a null
    /// sink), and the resolver falls through to the global default. The
    /// persona's memory file still loads -- the operator just doesn't get
    /// the pinned provider for this dispatch.
    /// </para>
    ///
    /// <para>
    /// Pure: no env reads outside <paramref name="env"/>, no Console writes
    /// (warnings go to <paramref name="warnSink"/>). When the persona has
    /// no pins set, returns <paramref name="baseInputs"/> unchanged --
    /// zero behaviour change for the non-persona-pin path.
    /// </para>
    /// </summary>
    public static ResolutionInputs ApplyPersonaPin(
        ResolutionInputs baseInputs,
        PersonaConfig persona,
        IReadOnlyDictionary<string, string?> env,
        Action<string>? warnSink)
    {
        ArgumentNullException.ThrowIfNull(baseInputs);
        ArgumentNullException.ThrowIfNull(persona);
        ArgumentNullException.ThrowIfNull(env);

        var pinnedProvider = string.IsNullOrWhiteSpace(persona.Provider) ? null : persona.Provider!.Trim();
        var pinnedModel = string.IsNullOrWhiteSpace(persona.Model) ? null : persona.Model!.Trim();

        // Fast path: nothing to fold in.
        if (pinnedProvider is null && pinnedModel is null)
        {
            return baseInputs;
        }

        // Missing-creds check: if the persona pins a provider but the env
        // doesn't carry creds for it, drop the provider pin and warn. Model
        // pin survives (a model-only pin is valid: e.g. the persona always
        // wants gpt-4o on whatever provider the global chain selects).
        if (pinnedProvider is not null)
        {
            var credEnv = PreferencesResolver.GetCredEnvVarName(pinnedProvider);
            if (credEnv is not null)
            {
                env.TryGetValue(credEnv, out var credValue);
                if (string.IsNullOrWhiteSpace(credValue))
                {
                    warnSink?.Invoke(
                        "[persona:" + persona.Name + "] pinned provider '" + pinnedProvider
                        + "' has no credentials in " + credEnv
                        + "; falling through to the global default-provider chain.");
                    pinnedProvider = null;
                }
            }
        }

        return baseInputs with
        {
            PersonaName = persona.Name,
            PersonaProvider = pinnedProvider,
            PersonaModel = pinnedModel,
        };
    }
}
