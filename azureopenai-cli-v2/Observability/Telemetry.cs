using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AzureOpenAI_CLI_V2.Observability;

/// <summary>
/// OpenTelemetry ActivitySource and Meter for v2 observability (Phase 5).
/// Zero overhead when disabled (no listeners = no-op ActivitySource/Meter calls;
/// <see cref="IsEnabled"/> / <see cref="EmitCostToStderr"/> short-circuit emission).
///
/// Enable via:
///   --telemetry       (umbrella: enables tracing, metrics, and stderr cost events)
///   --otel            (tracing only)
///   --metrics         (meters only, includes stderr cost events)
///   AZ_TELEMETRY=1    (env var, equivalent to --telemetry)
///
/// Output channels (NEVER stdout — <c>--raw</c> must stay clean for Espanso/AHK):
///   * spans + metrics → OTLP endpoint (<c>OTEL_EXPORTER_OTLP_ENDPOINT</c>, default <c>localhost:4317</c>)
///   * cost events     → stderr as a single-line JSON <see cref="CostEvent"/>, one per request
///
/// See <c>docs/observability.md</c> for the opt-in model and schema.
/// </summary>
internal static class Telemetry
{
    public const string ServiceName = "azureopenai-cli-v2";
    public const string ServiceVersion = "2.0.0-alpha.1";

    // ActivitySource for distributed tracing
    public static readonly ActivitySource ActivitySource = new(ServiceName, ServiceVersion);

    // Meter for metrics
    public static readonly Meter Meter = new(ServiceName, ServiceVersion);

    // Metric instruments
    public static readonly Histogram<double> ChatDuration = Meter.CreateHistogram<double>(
        "azai.chat.duration",
        unit: "s",
        description: "Duration of chat call");

    public static readonly Counter<long> InputTokens = Meter.CreateCounter<long>(
        "azai.tokens.input",
        unit: "tokens",
        description: "Input tokens consumed");

    public static readonly Counter<long> OutputTokens = Meter.CreateCounter<long>(
        "azai.tokens.output",
        unit: "tokens",
        description: "Output tokens generated");

    public static readonly Histogram<double> CostUsd = Meter.CreateHistogram<double>(
        "azai.cost.usd",
        unit: "USD",
        description: "Cost per LLM call in USD");

    public static readonly Histogram<int> RalphIterations = Meter.CreateHistogram<int>(
        "azai.ralph.iterations",
        unit: "iterations",
        description: "Ralph loop iteration count");

    public static readonly Counter<long> ToolInvocations = Meter.CreateCounter<long>(
        "azai.tool.invocations",
        unit: "invocations",
        description: "Tool invocation count");

    private static TracerProvider? _tracerProvider;
    private static MeterProvider? _meterProvider;
    private static bool _enabled;
    private static bool _emitCostToStderr;

    /// <summary>
    /// True when telemetry has been initialized with OTel and/or metrics enabled.
    /// Call sites gate expensive metric recording on this to keep the zero-overhead
    /// contract when no listener is registered.
    /// </summary>
    public static bool IsEnabled => _enabled;

    /// <summary>
    /// Reads <c>AZ_TELEMETRY</c> env var. Treats "1", "true", "yes" (case-insensitive) as on.
    /// Used as the env-var fallback for the <c>--telemetry</c> umbrella flag.
    /// </summary>
    public static bool IsTelemetryEnvOn()
    {
        var v = Environment.GetEnvironmentVariable("AZ_TELEMETRY");
        if (string.IsNullOrWhiteSpace(v)) return false;
        return v.Equals("1", StringComparison.Ordinal)
            || v.Equals("true", StringComparison.OrdinalIgnoreCase)
            || v.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True when stderr cost events should be emitted (enabled by <c>--telemetry</c>
    /// or <c>--metrics</c>; OFF for <c>--otel</c>-only so span-only users don't get
    /// extra stderr noise).
    /// </summary>
    public static bool EmitCostToStderr => _emitCostToStderr;

    /// <summary>
    /// Stderr writer override (for tests). Defaults to <see cref="Console.Error"/>.
    /// </summary>
    internal static TextWriter StderrWriter { get; set; } = Console.Error;

    /// <summary>
    /// Record a completed LLM request: emits input/output token counters and a
    /// USD cost histogram (via <see cref="CostHook.CalculateCost"/>), tagged by
    /// model + mode (standard|agent|ralph). When <see cref="EmitCostToStderr"/>
    /// is true, also writes a single-line <see cref="CostEvent"/> JSON to stderr
    /// for Morty's FinOps audit trail.
    /// No-op when <see cref="IsEnabled"/> is false. Never writes to stdout.
    /// </summary>
    public static void RecordRequest(string model, int inputTokens, int outputTokens, string mode)
    {
        if (!_enabled) return;

        var modelTag = new KeyValuePair<string, object?>("model", model);
        var modeTag = new KeyValuePair<string, object?>("mode", mode);
        if (inputTokens > 0) InputTokens.Add(inputTokens, modelTag, modeTag);
        if (outputTokens > 0) OutputTokens.Add(outputTokens, modelTag, modeTag);
        var cost = CostHook.CalculateCost(model, inputTokens, outputTokens);
        if (cost.HasValue) CostUsd.Record(cost.Value, modelTag, modeTag);

        if (_emitCostToStderr)
        {
            try
            {
                var evt = new CostEvent(
                    Timestamp: DateTime.UtcNow.ToString("O"),
                    Kind: "cost",
                    Model: model,
                    InputTokens: inputTokens,
                    OutputTokens: outputTokens,
                    Usd: cost,
                    Mode: mode);
                var json = JsonSerializer.Serialize(evt, AppJsonContext.Default.CostEvent);
                // Collapse indentation → single JSON line for log-shipper friendliness.
                json = json.Replace("\r", "").Replace("\n", "").Replace("  ", "");
                StderrWriter.WriteLine(json);
            }
            catch
            {
                // Telemetry must never break the request path.
            }
        }
    }

    /// <summary>
    /// Initialize OpenTelemetry exporters based on CLI flags (Phase 5).
    /// Must be called before any Activities or Metrics are emitted.
    /// </summary>
    /// <param name="enableOtel">Enable OTLP span export (<c>--otel</c>).</param>
    /// <param name="enableMetrics">Enable OTLP metrics + stderr cost events (<c>--metrics</c>).</param>
    /// <param name="enableTelemetry">Umbrella opt-in (<c>--telemetry</c> or <c>AZ_TELEMETRY=1</c>): promotes both of the above and enables stderr cost events.</param>
    public static void Initialize(bool enableOtel, bool enableMetrics, bool enableTelemetry = false)
    {
        if (enableTelemetry)
        {
            enableOtel = true;
            enableMetrics = true;
        }

        // Cost events go to stderr whenever the user asked for "numbers they can spreadsheet"
        // (i.e. --metrics or the umbrella --telemetry). Pure --otel stays quiet on stderr.
        _emitCostToStderr = enableTelemetry || enableMetrics;

        if (!enableOtel && !enableMetrics)
        {
            // Zero overhead: no listeners, ActivitySource/Meter calls are no-ops
            _enabled = false;
            return;
        }
        _enabled = true;

        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(ServiceName, serviceVersion: ServiceVersion);

        if (enableOtel)
        {
            _tracerProvider = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(resourceBuilder)
                .AddSource(ServiceName)
                .AddOtlpExporter(options =>
                {
                    // Default OTLP endpoint (localhost:4317)
                    // Override via OTEL_EXPORTER_OTLP_ENDPOINT env var
                    var endpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
                    if (!string.IsNullOrWhiteSpace(endpoint))
                    {
                        options.Endpoint = new Uri(endpoint);
                    }
                })
                .Build();
        }

        if (enableMetrics)
        {
            _meterProvider = Sdk.CreateMeterProviderBuilder()
                .SetResourceBuilder(resourceBuilder)
                .AddMeter(ServiceName)
                .AddOtlpExporter(options =>
                {
                    var endpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
                    if (!string.IsNullOrWhiteSpace(endpoint))
                    {
                        options.Endpoint = new Uri(endpoint);
                    }
                })
                .Build();
        }
    }

    /// <summary>
    /// Shutdown and flush telemetry providers.
    /// Call before application exit to ensure all data is exported.
    /// </summary>
    public static void Shutdown()
    {
        _tracerProvider?.Dispose();
        _meterProvider?.Dispose();
        _tracerProvider = null;
        _meterProvider = null;
        _enabled = false;
        _emitCostToStderr = false;
    }
}
