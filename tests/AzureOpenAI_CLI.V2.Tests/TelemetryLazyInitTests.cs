using Xunit;
using AzureOpenAI_CLI_V2.Observability;

namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// Tests for bania-v2-01 (lazy-init OTel/Metrics exporters).
///
/// <para>The OTLP SDK pipeline must NOT be constructed when no collector
/// endpoint (<c>OTEL_EXPORTER_OTLP_ENDPOINT</c>) is configured — building a
/// pipeline that exports into the void cost ~2.7ms / ~4.2ms of cold-start tax
/// on the reference rig (see <c>docs/perf/v2.0.5-baseline.md</c> §4).</para>
///
/// <para>When an endpoint IS configured, the pipeline MUST initialize so that
/// the first span/metric is exported without a first-emission stall.</para>
/// </summary>
[Collection(TelemetryGlobalStateCollection.Name)]
public class TelemetryLazyInitTests
{
    private const string EndpointVar = "OTEL_EXPORTER_OTLP_ENDPOINT";

    [Fact]
    public void Initialize_OtelFlag_NoEndpoint_SkipsTracerProviderConstruction()
    {
        var prev = Environment.GetEnvironmentVariable(EndpointVar);
        Environment.SetEnvironmentVariable(EndpointVar, null);
        Telemetry.Shutdown();
        try
        {
            Telemetry.Initialize(enableOtel: true, enableMetrics: false, enableTelemetry: false);

            // Flag-gated state still reflects the user's opt-in...
            Assert.True(Telemetry.IsEnabled);
            // ...but the expensive SDK pipeline was never constructed.
            Assert.False(Telemetry.TracerProviderConstructed);
            Assert.False(Telemetry.MeterProviderConstructed);
        }
        finally
        {
            Telemetry.Shutdown();
            Environment.SetEnvironmentVariable(EndpointVar, prev);
        }
    }

    [Fact]
    public void Initialize_OtelFlag_WithEndpoint_ConstructsTracerProvider()
    {
        var prev = Environment.GetEnvironmentVariable(EndpointVar);
        // Syntactically valid URI; we never actually connect during Initialize.
        Environment.SetEnvironmentVariable(EndpointVar, "http://127.0.0.1:4317");
        Telemetry.Shutdown();
        try
        {
            Telemetry.Initialize(enableOtel: true, enableMetrics: false, enableTelemetry: false);

            Assert.True(Telemetry.IsEnabled);
            Assert.True(Telemetry.TracerProviderConstructed);
            Assert.False(Telemetry.MeterProviderConstructed);
        }
        finally
        {
            Telemetry.Shutdown();
            Environment.SetEnvironmentVariable(EndpointVar, prev);
        }
    }

    [Fact]
    public void Initialize_MetricsFlag_NoEndpoint_SkipsMeterProviderButKeepsStderrEmission()
    {
        var prev = Environment.GetEnvironmentVariable(EndpointVar);
        Environment.SetEnvironmentVariable(EndpointVar, null);
        Telemetry.Shutdown();
        try
        {
            Telemetry.Initialize(enableOtel: false, enableMetrics: true, enableTelemetry: false);

            Assert.True(Telemetry.IsEnabled);
            // No OTLP meter pipeline...
            Assert.False(Telemetry.MeterProviderConstructed);
            // ...but the stderr FinOps cost-event channel is independent of OTLP
            // and MUST keep working (Morty's audit trail doesn't need a collector).
            Assert.True(Telemetry.EmitCostToStderr);
        }
        finally
        {
            Telemetry.Shutdown();
            Environment.SetEnvironmentVariable(EndpointVar, prev);
        }
    }

    [Fact]
    public void Initialize_MetricsFlag_WithEndpoint_ConstructsMeterProvider()
    {
        var prev = Environment.GetEnvironmentVariable(EndpointVar);
        Environment.SetEnvironmentVariable(EndpointVar, "http://127.0.0.1:4317");
        Telemetry.Shutdown();
        try
        {
            Telemetry.Initialize(enableOtel: false, enableMetrics: true, enableTelemetry: false);

            Assert.True(Telemetry.IsEnabled);
            Assert.True(Telemetry.MeterProviderConstructed);
            Assert.False(Telemetry.TracerProviderConstructed);
        }
        finally
        {
            Telemetry.Shutdown();
            Environment.SetEnvironmentVariable(EndpointVar, prev);
        }
    }

    [Fact]
    public void Initialize_TelemetryUmbrella_WithEndpoint_ConstructsBothProviders()
    {
        var prev = Environment.GetEnvironmentVariable(EndpointVar);
        Environment.SetEnvironmentVariable(EndpointVar, "http://127.0.0.1:4317");
        Telemetry.Shutdown();
        try
        {
            Telemetry.Initialize(enableOtel: false, enableMetrics: false, enableTelemetry: true);

            Assert.True(Telemetry.IsEnabled);
            Assert.True(Telemetry.TracerProviderConstructed);
            Assert.True(Telemetry.MeterProviderConstructed);
        }
        finally
        {
            Telemetry.Shutdown();
            Environment.SetEnvironmentVariable(EndpointVar, prev);
        }
    }

    [Fact]
    public void Initialize_NoFlags_NeverConstructsProviders_RegardlessOfEndpoint()
    {
        var prev = Environment.GetEnvironmentVariable(EndpointVar);
        Environment.SetEnvironmentVariable(EndpointVar, "http://127.0.0.1:4317");
        Telemetry.Shutdown();
        try
        {
            Telemetry.Initialize(enableOtel: false, enableMetrics: false, enableTelemetry: false);

            Assert.False(Telemetry.IsEnabled);
            Assert.False(Telemetry.TracerProviderConstructed);
            Assert.False(Telemetry.MeterProviderConstructed);
        }
        finally
        {
            Telemetry.Shutdown();
            Environment.SetEnvironmentVariable(EndpointVar, prev);
        }
    }
}
