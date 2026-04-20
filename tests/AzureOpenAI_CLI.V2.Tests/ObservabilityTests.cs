using Xunit;
using AzureOpenAI_CLI_V2.Observability;

namespace AzureOpenAI_CLI.V2.Tests;

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
        // $0.15 per 1M input = $0.00015 per 1K => 1000 tokens = $0.15
        // $0.60 per 1M output = $0.00060 per 1K => 500 tokens = $0.30
        // Total = $0.45
        Assert.Equal(0.45, cost.Value, precision: 6);
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
        // $0.20 per 1M input = $0.00020 per 1K => 2000 tokens = $0.40
        // $1.25 per 1M output = $0.00125 per 1K => 1000 tokens = $1.25
        // Total = $1.65
        Assert.Equal(1.65, cost.Value, precision: 6);
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
        // $0.075 per 1M input = $0.000075 per 1K => 10000 tokens = $0.75
        // $0.300 per 1M output = $0.000300 per 1K => 5000 tokens = $1.50
        // Total = $2.25
        Assert.Equal(2.25, cost.Value, precision: 6);
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
        // $0.080 per 1M input = $0.000080 per 1K => 10000 tokens = $0.80
        // $0.320 per 1M output = $0.000320 per 1K => 5000 tokens = $1.60
        // Total = $2.40
        Assert.Equal(2.40, cost.Value, precision: 6);
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
        // $2.50 per 1M input = $0.00250 per 1K => 1000 tokens = $2.50
        // $10.00 per 1M output = $0.01000 per 1K => 500 tokens = $5.00
        // Total = $7.50
        Assert.Equal(7.50, cost.Value, precision: 6);
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
        var opts = AzureOpenAI_CLI_V2.Program.ParseArgs(args);

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
        var opts = AzureOpenAI_CLI_V2.Program.ParseArgs(args);

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
        var opts = AzureOpenAI_CLI_V2.Program.ParseArgs(args);

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
        var opts = AzureOpenAI_CLI_V2.Program.ParseArgs(args);

        // Assert
        Assert.False(opts.EnableOtel);
        Assert.False(opts.EnableMetrics);
    }
}
