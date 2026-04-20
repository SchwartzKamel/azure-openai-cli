namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// Tests for ErrorAndExit helper method.
/// </summary>
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
    public void ErrorAndExit_JsonMode_WritesValidJson()
    {
        // Redirect stdout to capture output
        using var sw = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(sw);

        try
        {
            var exitCode = Program.ErrorAndExit("JSON error test", 99, jsonMode: true);
            var output = sw.ToString();

            // Verify it's valid JSON
            var parsed = System.Text.Json.JsonDocument.Parse(output);
            var root = parsed.RootElement;

            // Property names match JsonPropertyName attributes (snake_case)
            Assert.True(root.TryGetProperty("error", out var errorProp) && errorProp.GetBoolean());
            Assert.True(root.TryGetProperty("message", out var msgProp) && msgProp.GetString() == "JSON error test");
            Assert.True(root.TryGetProperty("exit_code", out var codeProp) && codeProp.GetInt32() == 99);
            Assert.Equal(99, exitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
