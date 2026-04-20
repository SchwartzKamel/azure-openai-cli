using System.Text.Json;
using System.Text.Json.Serialization;
using AzureOpenAI_CLI_V2.Squad;

namespace AzureOpenAI_CLI_V2;

/// <summary>JSON error response emitted to stdout in JSON mode (future).</summary>
internal record ErrorJsonResponse(
    [property: JsonPropertyName("error")] bool Error,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("exit_code")] int ExitCode
);

/// <summary>
/// System.Text.Json source generator context for AOT-compatible serialization.
/// Covers all types that are serialized/deserialized across the CLI.
///
/// Usage:
///   JsonSerializer.Serialize(obj, AppJsonContext.Default.ErrorJsonResponse);
///   JsonSerializer.Deserialize(json, AppJsonContext.Default.ErrorJsonResponse);
///
/// Adding new serialized types? Add a [JsonSerializable(typeof(YourType))] attribute here.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    PropertyNameCaseInsensitive = true
)]
// ── CLI JSON response types ─────────────────────────────────────
[JsonSerializable(typeof(ErrorJsonResponse))]
// ── Squad types ─────────────────────────────────────────────────
[JsonSerializable(typeof(SquadConfig))]
[JsonSerializable(typeof(TeamConfig))]
[JsonSerializable(typeof(PersonaConfig))]
[JsonSerializable(typeof(RoutingRule))]
// ── Collection types used by the above ──────────────────────────
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<PersonaConfig>))]
[JsonSerializable(typeof(List<RoutingRule>))]
internal partial class AppJsonContext : JsonSerializerContext
{
}
