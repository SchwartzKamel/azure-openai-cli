using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzureOpenAI_CLI_V2.Squad;

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
        return JsonSerializer.Deserialize(json, ctx.SquadConfig);
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
