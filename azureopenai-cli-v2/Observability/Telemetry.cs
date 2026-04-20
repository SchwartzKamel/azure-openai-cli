using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AzureOpenAI_CLI_V2.Observability;

/// <summary>
/// OpenTelemetry ActivitySource and Meter for v2 observability.
/// Zero overhead when disabled (no listeners = no-op).
/// Opt-in via --otel (traces) and --metrics (meters).
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

    /// <summary>
    /// Initialize OpenTelemetry exporters based on CLI flags.
    /// Must be called before any Activities or Metrics are emitted.
    /// </summary>
    public static void Initialize(bool enableOtel, bool enableMetrics)
    {
        if (!enableOtel && !enableMetrics)
        {
            // Zero overhead: no listeners, ActivitySource/Meter calls are no-ops
            return;
        }

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
    }
}
