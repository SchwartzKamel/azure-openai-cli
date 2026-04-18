using System.Text.Json;
using System.Text.Json.Serialization;
using AzureOpenAI_CLI;

namespace AzureOpenAI_CLI.Squad;

/// <summary>
/// Configuration for the Squad persona system.
/// Loaded from .squad.json in the working directory.
/// </summary>
internal sealed class SquadConfig
{
    private const string ConfigFileName = ".squad.json";

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

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize(json, AppJsonContext.Default.SquadConfig);
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
