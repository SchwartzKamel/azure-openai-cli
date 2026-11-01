using System.Text.Json;
using System.Text.Json.Serialization;
using AzureOpenAI_CLI.Cache;
using AzureOpenAI_CLI.Commander;
using AzureOpenAI_CLI.Mcp;
using AzureOpenAI_CLI.Patterns;
using AzureOpenAI_CLI.Providers;
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
[JsonSerializable(typeof(ProviderDescriptor))]
[JsonSerializable(typeof(List<ProviderDescriptor>))]
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
