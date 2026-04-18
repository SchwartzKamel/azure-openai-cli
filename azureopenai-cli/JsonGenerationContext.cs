using System.Text.Json;
using System.Text.Json.Serialization;
using AzureOpenAI_CLI.Squad;

namespace AzureOpenAI_CLI;

// ── CLI JSON response records (AOT-safe replacements for anonymous types) ───

/// <summary>JSON response for standard (non-agent) chat mode.</summary>
internal record ChatJsonResponse(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("response")] string Response,
    [property: JsonPropertyName("duration_ms")] long DurationMs,
    [property: JsonPropertyName("input_tokens")] int? InputTokens = null,
    [property: JsonPropertyName("output_tokens")] int? OutputTokens = null
);

/// <summary>JSON response for agent mode.</summary>
internal record AgentJsonResponse(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("response")] string Response,
    [property: JsonPropertyName("duration_ms")] long DurationMs,
    [property: JsonPropertyName("agent")] AgentInfo Agent,
    [property: JsonPropertyName("input_tokens")] int? InputTokens = null,
    [property: JsonPropertyName("output_tokens")] int? OutputTokens = null
);

/// <summary>Agent metadata nested inside <see cref="AgentJsonResponse"/>.</summary>
internal record AgentInfo(
    [property: JsonPropertyName("rounds")] int Rounds,
    [property: JsonPropertyName("tools_called")] int ToolsCalled
);

/// <summary>JSON error response emitted to stdout in --json mode.</summary>
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
///   JsonSerializer.Serialize(obj, AppJsonContext.Default.UserConfig);
///   JsonSerializer.Deserialize(json, AppJsonContext.Default.UserConfig);
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
// ── User configuration ──────────────────────────────────────────
[JsonSerializable(typeof(UserConfig))]
// ── Squad types ─────────────────────────────────────────────────
[JsonSerializable(typeof(SquadConfig))]
[JsonSerializable(typeof(TeamConfig))]
[JsonSerializable(typeof(PersonaConfig))]
[JsonSerializable(typeof(RoutingRule))]
// ── CLI JSON response types ─────────────────────────────────────
[JsonSerializable(typeof(ChatJsonResponse))]
[JsonSerializable(typeof(AgentJsonResponse))]
[JsonSerializable(typeof(AgentInfo))]
[JsonSerializable(typeof(ErrorJsonResponse))]
// ── Collection types used by the above ──────────────────────────
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<PersonaConfig>))]
[JsonSerializable(typeof(List<RoutingRule>))]
internal partial class AppJsonContext : JsonSerializerContext
{
}
