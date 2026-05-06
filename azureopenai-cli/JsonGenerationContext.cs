using System.Text.Json;
using System.Text.Json.Serialization;
using AzureOpenAI_CLI.Cache;
using AzureOpenAI_CLI.Squad;

namespace AzureOpenAI_CLI;

/// <summary>
/// JSON error envelope emitted on <b>stderr</b> in <c>--json</c> mode (per
/// Puddy 2026 audit — happy-path results stay on stdout, errors go to stderr).
/// </summary>
internal record ErrorJsonResponse(
    [property: JsonPropertyName("error")] bool Error,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("exit_code")] int ExitCode
);

/// <summary>Structured envelope for the unknown-flag parse error (Scope 3).</summary>
internal record UnknownFlagJsonError(
    [property: JsonPropertyName("error")] UnknownFlagDetail Error
);

/// <summary>Inner object for <see cref="UnknownFlagJsonError"/>.</summary>
internal record UnknownFlagDetail(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("flag")] string Flag
);

/// <summary>
/// JSON envelope emitted by <c>--config export-env --json</c>. Contains the
/// resolved Azure OpenAI credentials in a machine-readable form so callers can
/// pipe through <c>jq</c> instead of parsing KV lines. Same security caveats
/// apply: the API key is plaintext.
/// </summary>
internal record ExportEnvJson(
    [property: JsonPropertyName("AZUREOPENAIENDPOINT")] string Endpoint,
    [property: JsonPropertyName("AZUREOPENAIAPI")] string ApiKey,
    [property: JsonPropertyName("AZUREOPENAIMODEL")] string Model
);

/// <summary>
/// JSON envelope emitted by <c>--config show --json</c> (FR-014 / S03E06).
/// Surfaces the resolved provider/endpoint/model/profile alongside their
/// source-layer labels so callers can diff config without parsing prose.
/// Secrets (API keys) are NEVER included.
/// </summary>
internal record ConfigShowJson(
    [property: JsonPropertyName("resolved")] Dictionary<string, ConfigShowResolvedField> Resolved,
    [property: JsonPropertyName("preferences_path")] string? PreferencesPath,
    [property: JsonPropertyName("preferences_loaded")] bool PreferencesLoaded,
    [property: JsonPropertyName("providers")] List<string> Providers,
    [property: JsonPropertyName("profiles")] List<string> Profiles
);

/// <summary>One resolved field + its source layer (env, profile, default, ...).</summary>
internal record ConfigShowResolvedField(
    [property: JsonPropertyName("value")] string? Value,
    [property: JsonPropertyName("source")] string Source
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
[JsonSerializable(typeof(UnknownFlagJsonError))]
[JsonSerializable(typeof(UnknownFlagDetail))]
[JsonSerializable(typeof(ExportEnvJson))]
[JsonSerializable(typeof(AzureOpenAI_CLI.Observability.EstimateResult))]
// ── FR-008 prompt/response cache ────────────────────────────────
[JsonSerializable(typeof(CachedResponse))]
// ── Primitives used directly by PromptCache.ComputeKey canonicaliser ──
[JsonSerializable(typeof(string))]
// ── Observability (Phase 5) ─────────────────────────────────────
[JsonSerializable(typeof(AzureOpenAI_CLI.Observability.CostEvent))]
// ── User configuration (FR-003 / FR-009 / FR-010) ───────────────
[JsonSerializable(typeof(UserConfig))]
[JsonSerializable(typeof(UserDefaults))]
// ── Preferences (FR-014 / S03E06) ────────────────────────────────
[JsonSerializable(typeof(Preferences))]
[JsonSerializable(typeof(ProviderEntry))]
[JsonSerializable(typeof(ProfileEntry))]
[JsonSerializable(typeof(Dictionary<string, ProviderEntry>))]
[JsonSerializable(typeof(Dictionary<string, ProfileEntry>))]
[JsonSerializable(typeof(ConfigShowJson))]
[JsonSerializable(typeof(ConfigShowResolvedField))]
// ── Squad types ─────────────────────────────────────────────────
[JsonSerializable(typeof(SquadConfig))]
[JsonSerializable(typeof(TeamConfig))]
[JsonSerializable(typeof(PersonaConfig))]
[JsonSerializable(typeof(RoutingRule))]
// ── Observability price table (M3: folded from PriceTableJsonContext) ───
[JsonSerializable(typeof(AzureOpenAI_CLI.Observability.PriceTableEntry))]
[JsonSerializable(typeof(Dictionary<string, AzureOpenAI_CLI.Observability.PriceTableEntry>))]
// ── Collection types used by the above ──────────────────────────
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<PersonaConfig>))]
[JsonSerializable(typeof(List<RoutingRule>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class AppJsonContext : JsonSerializerContext
{
}
