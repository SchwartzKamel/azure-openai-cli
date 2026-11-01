// Kramer: Ralph `--validate <cmd>` validation loop must default to a low
// sampling temperature (RALPH_VALIDATE_TEMPERATURE = 0.15f) when the
// operator has not explicitly pinned temperature. High creative temperature
// makes the validation verdict oscillate across iterations. Precedence:
// CLI > env > validate default > DEFAULT_TEMPERATURE.
//
// Pass the pass, FAIL the fail — every test here exercises both a positive
// behavior (the new default applies) and a negative behavior (it does NOT
// override explicit operator intent).
namespace AzureOpenAI_CLI.V2.Tests;

[Collection("ConsoleCapture")]
public class ValidationTemperatureTests
{
    [Fact]
    public void Validate_NoExplicitTemperature_AppliesLowDefault()
    {
        // Guard against a polluted env from a parallel test run.
        Environment.SetEnvironmentVariable("AZURE_TEMPERATURE", null);

        var opts = Program.ParseArgs(
            ["--ralph", "--validate", "dotnet test", "--task-file", "task.md", "prompt"]);

        Assert.False(opts.ParseError);
        Assert.Equal("dotnet test", opts.ValidateCommand);
        Assert.Equal(Program.RALPH_VALIDATE_TEMPERATURE, opts.Temperature);
        Assert.Equal(0.15f, opts.Temperature);
    }

    [Fact]
    public void Validate_ExplicitCliTemperature_WinsOverValidateDefault()
    {
        Environment.SetEnvironmentVariable("AZURE_TEMPERATURE", null);

        var opts = Program.ParseArgs(
            [
                "--ralph",
                "--validate", "dotnet test",
                "--task-file", "task.md",
                "--temperature", "0.7",
                "prompt",
            ]);

        Assert.False(opts.ParseError);
        Assert.Equal(0.7f, opts.Temperature);
        // Fail the fail: validate default must NOT clobber explicit flag.
        Assert.NotEqual(0.15f, opts.Temperature);
    }

    [Fact]
    public void Validate_EnvTemperature_WinsOverValidateDefault()
    {
        Environment.SetEnvironmentVariable("AZURE_TEMPERATURE", "0.9");
        try
        {
            var opts = Program.ParseArgs(
                ["--ralph", "--validate", "dotnet test", "--task-file", "task.md", "prompt"]);

            Assert.False(opts.ParseError);
            Assert.Equal(0.9f, opts.Temperature);
            // Fail the fail: env pin must NOT be shadowed by validate default.
            Assert.NotEqual(0.15f, opts.Temperature);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZURE_TEMPERATURE", null);
        }
    }

    [Fact]
    public void NoValidate_TemperatureFallsBackToGeneralDefault()
    {
        Environment.SetEnvironmentVariable("AZURE_TEMPERATURE", null);

        var opts = Program.ParseArgs(["prompt"]);

        Assert.False(opts.ParseError);
        Assert.Null(opts.ValidateCommand);
        Assert.Equal(0.55f, opts.Temperature);
        // Fail the fail: low validate default must not leak into non-validate runs.
        Assert.NotEqual(0.15f, opts.Temperature);
    }

    [Fact]
    public void Ralph_WithoutValidate_DoesNotTriggerLowDefault()
    {
        // --ralph alone (no --validate) is NOT a validation loop and must
        // keep the normal creative default.
        Environment.SetEnvironmentVariable("AZURE_TEMPERATURE", null);

        var opts = Program.ParseArgs(["--ralph", "--task-file", "task.md", "prompt"]);

        Assert.False(opts.ParseError);
        Assert.True(opts.RalphMode);
        Assert.Null(opts.ValidateCommand);
        Assert.Equal(0.55f, opts.Temperature);
    }
}
