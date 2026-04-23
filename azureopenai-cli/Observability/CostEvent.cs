using System.Text.Json.Serialization;

namespace AzureOpenAI_CLI.Observability;

/// <summary>
/// Morty-approved FinOps cost schema for a single LLM request.
/// Emitted as a single JSON line on stderr when <c>--telemetry</c> (or
/// <c>AZ_TELEMETRY=1</c>) is set. Schema is stable — consumers
/// (dashboards, log shippers, Morty's spreadsheet) depend on field names.
/// </summary>
/// <remarks>
/// AOT-safe: serialized via <see cref="AppJsonContext"/>.
/// Wire format example:
/// <code>
/// {"ts":"2026-04-20T12:34:56.789Z","kind":"cost","model":"gpt-4o-mini","input_tokens":1200,"output_tokens":340,"usd":0.000384,"mode":"standard"}
/// </code>
/// </remarks>
internal record CostEvent(
    [property: JsonPropertyName("ts")] string Timestamp,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("input_tokens")] int InputTokens,
    [property: JsonPropertyName("output_tokens")] int OutputTokens,
    [property: JsonPropertyName("usd")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    double? Usd,
    [property: JsonPropertyName("mode")] string Mode
);
