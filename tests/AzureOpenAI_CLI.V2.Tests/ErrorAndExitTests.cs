namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// Tests for ErrorAndExit helper method.
/// </summary>
[Collection("ConsoleCapture")]
public class ErrorAndExitTests
{
    [Fact]
    public void ErrorAndExit_NonJsonMode_ReturnsCorrectExitCode()
    {
        var exitCode = Program.ErrorAndExit("Test error", 42, jsonMode: false);

        Assert.Equal(42, exitCode);
    }

    [Fact]
    public void ErrorAndExit_JsonMode_ReturnsCorrectExitCode()
    {
        var exitCode = Program.ErrorAndExit("Test error", 7, jsonMode: true);

        Assert.Equal(7, exitCode);
    }

    [Fact]
    public void ErrorAndExit_JsonMode_WritesValidJsonToStderr()
    {
        // Scope 2 (Puddy finding): JSON errors must land on stderr so consumers
        // piping stdout into jq don't see them. Stdout must remain empty for
        // error paths in --json mode.
        using var errCapture = new StringWriter();
        using var outCapture = new StringWriter();
        var originalErr = Console.Error;
        var originalOut = Console.Out;
        Console.SetError(errCapture);
        Console.SetOut(outCapture);

        try
        {
            var exitCode = Program.ErrorAndExit("JSON error test", 99, jsonMode: true);
            var errOutput = errCapture.ToString();
            var stdOutput = outCapture.ToString();

            // Stdout MUST be empty for JSON error path.
            Assert.Equal(string.Empty, stdOutput);

            // Verify stderr carries valid JSON with the expected envelope.
            var parsed = System.Text.Json.JsonDocument.Parse(errOutput);
            var root = parsed.RootElement;

            Assert.True(root.TryGetProperty("error", out var errorProp) && errorProp.GetBoolean());
            Assert.True(root.TryGetProperty("message", out var msgProp) && msgProp.GetString() == "JSON error test");
            Assert.True(root.TryGetProperty("exit_code", out var codeProp) && codeProp.GetInt32() == 99);
            Assert.Equal(99, exitCode);
        }
        finally
        {
            Console.SetError(originalErr);
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void ErrorAndExit_NonJsonMode_WritesPrefixedMessageToStderr()
    {
        // Negative-path assertion: non-JSON mode must NOT emit JSON and must
        // write the [ERROR]-prefixed message to stderr (not stdout).
        using var errCapture = new StringWriter();
        using var outCapture = new StringWriter();
        var originalErr = Console.Error;
        var originalOut = Console.Out;
        Console.SetError(errCapture);
        Console.SetOut(outCapture);

        try
        {
            Program.ErrorAndExit("plain error", 7, jsonMode: false);
            Assert.Equal(string.Empty, outCapture.ToString());
            Assert.Contains("[ERROR] plain error", errCapture.ToString());
            Assert.DoesNotContain("{", errCapture.ToString());
        }
        finally
        {
            Console.SetError(originalErr);
            Console.SetOut(originalOut);
        }
    }
}
