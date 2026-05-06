using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzureOpenAI_CLI.Squad;

/// <summary>
/// Configuration for the Squad persona system.
/// Loaded from .squad.json in the working directory.
/// </summary>
internal sealed class SquadConfig
{
    private const string ConfigFileName = ".squad.json";

    /// <summary>
    /// K-6 (2.0.1): hard cap on the size of .squad.json we'll attempt to
    /// deserialise. 1 MB is ~10× a reasonable squad config and small enough
    /// that a pathological file can't OOM the process on load.
    /// </summary>
    private const long MaxConfigBytes = 1L * 1024 * 1024;

    /// <summary>
    /// F-4 (2.0.1): explicit MaxDepth on JSON deserialisation. System.Text.Json's
    /// default is 64; we tighten to 32 because our schema is shallow (team /
    /// personas / routing — ≤4 levels) and a deeply-nested attacker payload
    /// is a stack-pressure vector with zero legitimate use case.
    /// </summary>
    private const int MaxJsonDepth = 32;

    [JsonPropertyName("team")]
    public TeamConfig Team { get; set; } = new();

    [JsonPropertyName("personas")]
    public List<PersonaConfig> Personas { get; set; } = new();

    [JsonPropertyName("routing")]
    public List<RoutingRule> Routing { get; set; } = new();

    /// <summary>
    /// Load config from .squad.json in the specified directory (default: current dir).
    /// Returns null if file doesn't exist.
    /// </summary>
    public static SquadConfig? Load(string? directory = null)
    {
        var dir = directory ?? Directory.GetCurrentDirectory();
        var path = Path.Combine(dir, ConfigFileName);
        if (!File.Exists(path))
            return null;

        // K-6: refuse pathologically large configs before we read them.
        var size = new FileInfo(path).Length;
        if (size > MaxConfigBytes)
            throw new InvalidOperationException("squad config exceeds 1 MB limit");

        var json = File.ReadAllText(path);

        // F-4: tighten MaxDepth to 32 on a per-load options clone so we don't
        // mutate the source-gen defaults used elsewhere.
        var options = new JsonSerializerOptions(AppJsonContext.Default.Options)
        {
            MaxDepth = MaxJsonDepth,
        };
        var ctx = new AppJsonContext(options);
        var cfg = JsonSerializer.Deserialize(json, ctx.SquadConfig);
        // S03E28 -- The Persona, Multi-Provider (Kramer). Validate persona
        // pins at load time so a bad value in .squad.json is surfaced once,
        // up front, with a clear error -- not at dispatch time when the
        // operator is mid-flow.
        cfg?.Validate(path);
        return cfg;
    }

    /// <summary>
    /// S03E28 -- The Persona, Multi-Provider. Validate every persona's
    /// optional <c>provider</c> pin against
    /// <see cref="PreferencesResolver.IsKnownProvider"/>. Throws
    /// <see cref="InvalidOperationException"/> on the first offender,
    /// pointing at the persona name and the bad value, and listing the
    /// known providers. <c>model</c> is free-form (no allowlist) -- the
    /// dispatch path validates against AZUREOPENAIMODEL / AZ_AI_COMPAT_MODELS.
    /// Idempotent + cheap; safe to call from Load() and from tests that
    /// hand-construct a config.
    /// </summary>
    public void Validate(string? sourcePath = null)
    {
        foreach (var p in Personas)
        {
            if (string.IsNullOrWhiteSpace(p.Provider)) continue;
            if (!PreferencesResolver.IsKnownProvider(p.Provider))
            {
                var src = sourcePath is null ? ".squad.json" : sourcePath;
                throw new InvalidOperationException(
                    "Invalid persona pin in " + src + ": persona '" + p.Name
                    + "' pins provider '" + p.Provider + "' which is not a known provider. "
                    + "Known providers: [" + PreferencesResolver.KnownProvidersList() + "]. "
                    + "Fix the 'provider' field on this persona, or remove it to fall back to the "
                    + "global resolution chain.");
            }
        }
    }

    /// <summary>
    /// Get a persona by name (case-insensitive).
    /// </summary>
    public PersonaConfig? GetPersona(string name) =>
        Personas.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// List all available persona names.
    /// </summary>
    public IReadOnlyList<string> ListPersonas() =>
        Personas.Select(p => p.Name).ToList();

    /// <summary>
    /// Save config to .squad.json.
    /// </summary>
    public void Save(string? directory = null)
    {
        var dir = directory ?? Directory.GetCurrentDirectory();
        var path = Path.Combine(dir, ConfigFileName);
        var json = JsonSerializer.Serialize(this, AppJsonContext.Default.SquadConfig);
        File.WriteAllText(path, json);
    }
}

internal sealed class TeamConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "Default Squad";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";
}

internal sealed class PersonaConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("system_prompt")]
    public string SystemPrompt { get; set; } = "";

    [JsonPropertyName("tools")]
    public List<string> Tools { get; set; } = new();

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>
    /// S03E28 -- The Persona, Multi-Provider (Kramer). Optional provider
    /// pin. When non-null, this persona invocation routes through the
    /// pinned provider (subject to the global precedence chain: cli &gt;
    /// env &gt; profile &gt; persona &gt; default). Must be one of the providers
    /// understood by <see cref="PreferencesResolver.IsKnownProvider"/>;
    /// validated at <see cref="SquadConfig.Load"/>.
    /// </summary>
    [JsonPropertyName("provider")]
    public string? Provider { get; set; }
}

internal sealed class RoutingRule
{
    [JsonPropertyName("pattern")]
    public string Pattern { get; set; } = "";

    [JsonPropertyName("persona")]
    public string Persona { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";
}
