using System.Text.Json;
using System.Text.Json.Serialization;
using AzureOpenAI_CLI.Cache;
using AzureOpenAI_CLI.Commander;
using AzureOpenAI_CLI.Eval;
using AzureOpenAI_CLI.Mcp;
using AzureOpenAI_CLI.Patterns;
using AzureOpenAI_CLI.Registry;
using AzureOpenAI_CLI.Resolution;
using AzureOpenAI_CLI.Squad;
using AzureOpenAI_CLI.Tools;

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
/// S03E15 -- The Probe. JSON envelope for <c>az-ai --doctor --json</c>.
/// One entry per configured provider plus a roll-up boolean. Never carries
/// credential values; <c>creds_present</c> is the only credential signal.
/// </summary>
internal record ProviderDoctorReport(
    [property: JsonPropertyName("providers")] List<ProviderDoctorEntry> Providers,
    [property: JsonPropertyName("all_healthy")] bool AllHealthy
);

/// <summary>One row in <see cref="ProviderDoctorReport"/>.</summary>
internal record ProviderDoctorEntry(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("endpoint")] string Endpoint,
    [property: JsonPropertyName("dns")] string Dns,
    [property: JsonPropertyName("creds_present")] bool CredsPresent,
    [property: JsonPropertyName("models_configured")] int ModelsConfigured,
    [property: JsonPropertyName("healthy")] bool Healthy
);

/// <summary>
/// Happy-path JSON response envelope emitted on <b>stdout</b> when <c>--json</c>
/// is passed on a chat, agent, or Ralph invocation.
/// </summary>
internal record ChatJsonResponse(
    [property: JsonPropertyName("response")] string Response,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("input_tokens")] int? InputTokens,
    [property: JsonPropertyName("output_tokens")] int? OutputTokens,
    [property: JsonPropertyName("total_tokens")] int? TotalTokens,
    [property: JsonPropertyName("duration_ms")] long DurationMs,
    [property: JsonPropertyName("cached")] bool Cached
);

/// <summary>
/// Single-line NDJSON streaming event emitted on stdout when <c>--jsonl</c> is active.
/// </summary>
internal record JsonlStreamEvent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] string? Text = null,
    [property: JsonPropertyName("response")] string? Response = null,
    [property: JsonPropertyName("model")] string? Model = null,
    [property: JsonPropertyName("mode")] string? Mode = null,
    [property: JsonPropertyName("input_tokens")] int? InputTokens = null,
    [property: JsonPropertyName("output_tokens")] int? OutputTokens = null,
    [property: JsonPropertyName("total_tokens")] int? TotalTokens = null,
    [property: JsonPropertyName("duration_ms")] long? DurationMs = null
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
[JsonSerializable(typeof(ChatJsonResponse))]
[JsonSerializable(typeof(ModelDecision))]
[JsonSerializable(typeof(EvalSuiteFile))]
[JsonSerializable(typeof(List<EvalFixture>))]
[JsonSerializable(typeof(EvalReport))]
[JsonSerializable(typeof(ExportEnvJson))]
[JsonSerializable(typeof(AzureOpenAI_CLI.Observability.EstimateResult))]
// ── FR-008 prompt/response cache ────────────────────────────────
[JsonSerializable(typeof(CachedResponse))]
// ── Primitives used directly by PromptCache.ComputeKey canonicaliser ──
[JsonSerializable(typeof(string))]
// ── Observability (Phase 5) ─────────────────────────────────────
[JsonSerializable(typeof(AzureOpenAI_CLI.Observability.CostEvent))]
// ── S03E13 opt-in telemetry (Frank Costanza) ────────────────────
// TelemetryEmitter uses a manual Utf8JsonWriter for the wire format so
// key order is stable and the line is compact (NDJSON-ready). This entry
// keeps the type AOT-discoverable for any future deserialization path
// (e.g. test harnesses parsing captured stderr lines).
[JsonSerializable(typeof(AzureOpenAI_CLI.Observability.TelemetryEvent))]
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
// -- S03E15 The Probe -----------------------------------------------------
[JsonSerializable(typeof(ProviderDoctorReport))]
[JsonSerializable(typeof(ProviderDoctorEntry))]
[JsonSerializable(typeof(List<ProviderDoctorEntry>))]
// ── S04E01 The Registry --------------------------------------------------
[JsonSerializable(typeof(ModelRegistryEntry))]
[JsonSerializable(typeof(ModelRegistryEntry[]))]
// ── S04E04 Reading Room (Elaine) ----------------------------------------
// Three DTOs supporting `az-ai models {list,show,capabilities} --json`.
// JSON property names are pinned with [JsonPropertyName] on the records so
// the CamelCase default policy does not creep in and break the shape.
[JsonSerializable(typeof(AzureOpenAI_CLI.Cli.ModelListEntryJson))]
[JsonSerializable(typeof(AzureOpenAI_CLI.Cli.ModelListEntryJson[]))]
[JsonSerializable(typeof(AzureOpenAI_CLI.Cli.ModelShowJson))]
[JsonSerializable(typeof(Dictionary<string, string[]>))]
[JsonSerializable(typeof(string[]))]
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
// ── FR-012 Manifest Plugin & Tool Introspection types ───────────
[JsonSerializable(typeof(ToolManifest))]
[JsonSerializable(typeof(ToolDescriptor))]
[JsonSerializable(typeof(List<ToolDescriptor>))]
// ── FR-015 Pattern Library types ────────────────────────────────
[JsonSerializable(typeof(PatternTemplate))]
[JsonSerializable(typeof(List<PatternTemplate>))]
[JsonSerializable(typeof(QueueTaskItem))]
[JsonSerializable(typeof(List<QueueTaskItem>))]
[JsonSerializable(typeof(WorkQueueFile))]
[JsonSerializable(typeof(QueueStatusSummary))]
[JsonSerializable(typeof(McpToolInfo))]
[JsonSerializable(typeof(List<McpToolInfo>))]
internal partial class AppJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Compact single-line AOT JSON serializer context for MCP stdio JSON-RPC 2.0
/// framing (newline-delimited JSON-RPC requires WriteIndented = false).
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false
)]
[JsonSerializable(typeof(McpJsonRpcResponse))]
[JsonSerializable(typeof(McpJsonRpcRequest))]
[JsonSerializable(typeof(McpInitializeRequestParams))]
[JsonSerializable(typeof(McpClientCapabilities))]
[JsonSerializable(typeof(McpCallToolRequestParams))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(McpJsonRpcError))]
[JsonSerializable(typeof(McpInitializeResult))]
[JsonSerializable(typeof(McpCapabilities))]
[JsonSerializable(typeof(McpToolsCapability))]
[JsonSerializable(typeof(McpPromptsCapability))]
[JsonSerializable(typeof(McpResourcesCapability))]
[JsonSerializable(typeof(McpServerInfo))]
[JsonSerializable(typeof(McpToolInfo))]
[JsonSerializable(typeof(List<McpToolInfo>))]
[JsonSerializable(typeof(McpToolsListResult))]
[JsonSerializable(typeof(McpContentItem))]
[JsonSerializable(typeof(List<McpContentItem>))]
[JsonSerializable(typeof(McpCallToolResult))]
[JsonSerializable(typeof(McpPromptArgument))]
[JsonSerializable(typeof(List<McpPromptArgument>))]
[JsonSerializable(typeof(McpPromptInfo))]
[JsonSerializable(typeof(List<McpPromptInfo>))]
[JsonSerializable(typeof(McpPromptsListResult))]
[JsonSerializable(typeof(McpPromptMessage))]
[JsonSerializable(typeof(List<McpPromptMessage>))]
[JsonSerializable(typeof(McpGetPromptResult))]
[JsonSerializable(typeof(McpResourceInfo))]
[JsonSerializable(typeof(List<McpResourceInfo>))]
[JsonSerializable(typeof(McpResourcesListResult))]
[JsonSerializable(typeof(McpResourceContent))]
[JsonSerializable(typeof(List<McpResourceContent>))]
[JsonSerializable(typeof(McpReadResourceResult))]
[JsonSerializable(typeof(JsonlStreamEvent))]
internal partial class McpJsonContext : JsonSerializerContext
{
}
