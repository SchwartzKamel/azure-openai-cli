using Xunit;
using AzureOpenAI_CLI.Observability;

namespace AzureOpenAI_CLI.V2.Tests;

[Collection(TelemetryGlobalStateCollection.Name)]
public class ObservabilityTests
{
    [Fact]
    public void CostHook_CalculateCost_Gpt4oMini_ReturnsCorrectCost()
    {
        // Arrange
        var model = "gpt-4o-mini";
        var inputTokens = 1000;
        var outputTokens = 500;

        // Act
        var cost = CostHook.CalculateCost(model, inputTokens, outputTokens);

        // Assert
        Assert.NotNull(cost);
        // $0.15 per 1M input = $0.00015 per 1K => 1000 tokens = $0.00015
        // $0.60 per 1M output = $0.00060 per 1K => 500 tokens = $0.00030
        // Total = $0.00045
        Assert.Equal(0.00045, cost.Value, precision: 6);
    }

    [Fact]
    public void CostHook_CalculateCost_Gpt54Nano_ReturnsCorrectCost()
    {
        // Arrange
        var model = "gpt-5.4-nano";
        var inputTokens = 2000;
        var outputTokens = 1000;

        // Act
        var cost = CostHook.CalculateCost(model, inputTokens, outputTokens);

        // Assert
        Assert.NotNull(cost);
        // $0.20 per 1M input = $0.00020 per 1K => 2000 tokens = $0.00040
        // $1.25 per 1M output = $0.00125 per 1K => 1000 tokens = $0.00125
        // Total = $0.00165
        Assert.Equal(0.00165, cost.Value, precision: 6);
    }

    [Fact]
    public void CostHook_CalculateCost_Phi4MiniInstruct_ReturnsCorrectCost()
    {
        // Arrange
        var model = "Phi-4-mini-instruct";
        var inputTokens = 10000;
        var outputTokens = 5000;

        // Act
        var cost = CostHook.CalculateCost(model, inputTokens, outputTokens);

        // Assert
        Assert.NotNull(cost);
        // $0.075 per 1M input = $0.000075 per 1K => 10000 tokens = $0.00075
        // $0.300 per 1M output = $0.000300 per 1K => 5000 tokens = $0.00150
        // Total = $0.00225
        Assert.Equal(0.00225, cost.Value, precision: 6);
    }

    [Fact]
    public void CostHook_CalculateCost_Phi4MiniReasoning_ReturnsCorrectCost()
    {
        // Arrange
        var model = "Phi-4-mini-reasoning";
        var inputTokens = 10000;
        var outputTokens = 5000;

        // Act
        var cost = CostHook.CalculateCost(model, inputTokens, outputTokens);

        // Assert
        Assert.NotNull(cost);
        // $0.080 per 1M input = $0.000080 per 1K => 10000 tokens = $0.00080
        // $0.320 per 1M output = $0.000320 per 1K => 5000 tokens = $0.00160
        // Total = $0.00240
        Assert.Equal(0.00240, cost.Value, precision: 6);
    }

    [Fact]
    public void CostHook_CalculateCost_Gpt4o_ReturnsCorrectCost()
    {
        // Arrange
        var model = "gpt-4o";
        var inputTokens = 1000;
        var outputTokens = 500;

        // Act
        var cost = CostHook.CalculateCost(model, inputTokens, outputTokens);

        // Assert
        Assert.NotNull(cost);
        // $2.50 per 1M input = $0.00250 per 1K => 1000 tokens = $0.00250
        // $10.00 per 1M output = $0.01000 per 1K => 500 tokens = $0.00500
        // Total = $0.00750
        Assert.Equal(0.00750, cost.Value, precision: 6);
    }

    [Fact]
    public void CostHook_CalculateCost_UnknownModel_ReturnsNull()
    {
        // Arrange
        var model = "unknown-model-xyz";
        var inputTokens = 1000;
        var outputTokens = 500;

        // Act
        var cost = CostHook.CalculateCost(model, inputTokens, outputTokens);

        // Assert
        Assert.Null(cost);
    }

    [Fact]
    public void CostHook_FormatCost_WithValue_ReturnsFormattedString()
    {
        // Arrange
        var cost = 0.000123;

        // Act
        var formatted = CostHook.FormatCost(cost);

        // Assert
        Assert.Equal("cost=$0.000123", formatted);
    }

    [Fact]
    public void CostHook_FormatCost_Null_ReturnsUnknown()
    {
        // Act
        var formatted = CostHook.FormatCost(null);

        // Assert
        Assert.Equal("cost=unknown", formatted);
    }

    [Fact]
    public void CliOptions_WithOtelFlag_SetsEnableOtel()
    {
        // Arrange
        var args = new[] { "--otel", "test prompt" };

        // Act
        var opts = AzureOpenAI_CLI.Program.ParseArgs(args);

        // Assert
        Assert.True(opts.EnableOtel);
        Assert.False(opts.EnableMetrics);
    }

    [Fact]
    public void CliOptions_WithMetricsFlag_SetsEnableMetrics()
    {
        // Arrange
        var args = new[] { "--metrics", "test prompt" };

        // Act
        var opts = AzureOpenAI_CLI.Program.ParseArgs(args);

        // Assert
        Assert.False(opts.EnableOtel);
        Assert.True(opts.EnableMetrics);
    }

    [Fact]
    public void CliOptions_WithBothFlags_SetsBothFlags()
    {
        // Arrange
        var args = new[] { "--otel", "--metrics", "test prompt" };

        // Act
        var opts = AzureOpenAI_CLI.Program.ParseArgs(args);

        // Assert
        Assert.True(opts.EnableOtel);
        Assert.True(opts.EnableMetrics);
    }

    [Fact]
    public void CliOptions_WithoutFlags_DoesNotSetFlags()
    {
        // Arrange
        var args = new[] { "test prompt" };

        // Act
        var opts = AzureOpenAI_CLI.Program.ParseArgs(args);

        // Assert
        Assert.False(opts.EnableOtel);
        Assert.False(opts.EnableMetrics);
        Assert.False(opts.EnableTelemetry);
    }

    // ── Phase 5: umbrella --telemetry flag + AZ_TELEMETRY env ──────────────

    [Fact]
    public void CliOptions_WithTelemetryFlag_SetsEnableTelemetry()
    {
        var opts = AzureOpenAI_CLI.Program.ParseArgs(new[] { "--telemetry", "hi" });
        Assert.True(opts.EnableTelemetry);
    }

    [Fact]
    public void CliOptions_AzTelemetryEnvVar_EnablesTelemetry()
    {
        var prev = Environment.GetEnvironmentVariable("AZ_TELEMETRY");
        try
        {
            Environment.SetEnvironmentVariable("AZ_TELEMETRY", "1");
            var opts = AzureOpenAI_CLI.Program.ParseArgs(new[] { "hi" });
            Assert.True(opts.EnableTelemetry);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZ_TELEMETRY", prev);
        }
    }

    [Fact]
    public void CliOptions_AzTelemetryEnvVar_Off_LeavesFlagFalse()
    {
        var prev = Environment.GetEnvironmentVariable("AZ_TELEMETRY");
        try
        {
            Environment.SetEnvironmentVariable("AZ_TELEMETRY", "0");
            var opts = AzureOpenAI_CLI.Program.ParseArgs(new[] { "hi" });
            Assert.False(opts.EnableTelemetry);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZ_TELEMETRY", prev);
        }
    }

    [Fact]
    public void Telemetry_Initialize_WithNoFlags_IsDisabled()
    {
        Telemetry.Initialize(enableOtel: false, enableMetrics: false, enableTelemetry: false);
        try
        {
            Assert.False(Telemetry.IsEnabled);
            Assert.False(Telemetry.EmitCostToStderr);
        }
        finally { Telemetry.Shutdown(); }
    }

    [Fact]
    public void Telemetry_Initialize_WithTelemetryFlag_EnablesEmissionAndIsEnabled()
    {
        Telemetry.Initialize(enableOtel: false, enableMetrics: false, enableTelemetry: true);
        try
        {
            Assert.True(Telemetry.IsEnabled);
            Assert.True(Telemetry.EmitCostToStderr);
        }
        finally { Telemetry.Shutdown(); }
    }

    [Fact]
    public void RecordRequest_Disabled_WritesNothingToStderr()
    {
        var sw = new System.IO.StringWriter();
        Telemetry.StderrWriter = sw;
        Telemetry.Shutdown(); // ensure disabled
        try
        {
            Telemetry.RecordRequest("gpt-4o-mini", 100, 200, "standard");
            Assert.Equal(string.Empty, sw.ToString());
        }
        finally
        {
            Telemetry.StderrWriter = Console.Error;
        }
    }

    [Fact]
    public void RecordRequest_TelemetryEnabled_EmitsJsonCostEventToStderr()
    {
        var sw = new System.IO.StringWriter();
        var prevWriter = Telemetry.StderrWriter;
        Telemetry.StderrWriter = sw;
        Telemetry.Initialize(enableOtel: false, enableMetrics: false, enableTelemetry: true);
        try
        {
            Telemetry.RecordRequest("gpt-4o-mini", 1000, 500, "standard");
            var line = sw.ToString().Trim();
            Assert.NotEmpty(line);
            // Single-line JSON
            Assert.DoesNotContain("\n", line.TrimEnd());
            using var doc = System.Text.Json.JsonDocument.Parse(line);
            var root = doc.RootElement;
            Assert.Equal("cost", root.GetProperty("kind").GetString());
            Assert.Equal("gpt-4o-mini", root.GetProperty("model").GetString());
            Assert.Equal(1000, root.GetProperty("input_tokens").GetInt32());
            Assert.Equal(500, root.GetProperty("output_tokens").GetInt32());
            Assert.Equal("standard", root.GetProperty("mode").GetString());
            // usd present and correct: 1000/1000*$0.00015 + 500/1000*$0.00060 = $0.00045
            Assert.Equal(0.00045, root.GetProperty("usd").GetDouble(), precision: 6);
            Assert.True(root.TryGetProperty("ts", out _));
        }
        finally
        {
            Telemetry.Shutdown();
            Telemetry.StderrWriter = prevWriter;
        }
    }

    [Fact]
    public void RecordRequest_UnknownModel_EmitsNullUsd()
    {
        var sw = new System.IO.StringWriter();
        var prevWriter = Telemetry.StderrWriter;
        Telemetry.StderrWriter = sw;
        Telemetry.Initialize(enableOtel: false, enableMetrics: false, enableTelemetry: true);
        try
        {
            Telemetry.RecordRequest("some-unknown-model-xyz", 100, 50, "standard");
            var line = sw.ToString().Trim();
            using var doc = System.Text.Json.JsonDocument.Parse(line);
            var usd = doc.RootElement.GetProperty("usd");
            Assert.Equal(System.Text.Json.JsonValueKind.Null, usd.ValueKind);
        }
        finally
        {
            Telemetry.Shutdown();
            Telemetry.StderrWriter = prevWriter;
        }
    }

    [Fact]
    public void RecordRequest_OtelOnly_DoesNotEmitToStderr()
    {
        // --otel without --metrics/--telemetry → no stderr noise (spans only).
        var sw = new System.IO.StringWriter();
        var prevWriter = Telemetry.StderrWriter;
        Telemetry.StderrWriter = sw;
        Telemetry.Initialize(enableOtel: true, enableMetrics: false, enableTelemetry: false);
        try
        {
            Telemetry.RecordRequest("gpt-4o-mini", 100, 50, "standard");
            Assert.Equal(string.Empty, sw.ToString());
        }
        finally
        {
            Telemetry.Shutdown();
            Telemetry.StderrWriter = prevWriter;
        }
    }
}
