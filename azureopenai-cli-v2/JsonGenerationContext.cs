using System.Text.Json;
using System.Text.Json.Serialization;

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
[JsonSerializable(typeof(ErrorJsonResponse))]
internal partial class AppJsonContext : JsonSerializerContext
{
}
