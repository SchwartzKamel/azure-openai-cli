using System.Reflection;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// Regression tests for SECURITY-AUDIT-001 findings that were fixed in v1 and
/// must not regress in the v2 port.
///
/// <list type="bullet">
///   <item>MEDIUM-001 — stdin must be bounded to 1 MB (v1 Program.cs:578-590).</item>
///   <item>MEDIUM-002 — endpoint must be HTTPS before client construction
///   (v1 Program.cs:383-387).</item>
/// </list>
/// </summary>
[Collection("ConsoleCapture")]
public class SecurityRegressionTests
{
    private static readonly MethodInfo MainMethod =
        typeof(Program).GetMethod("Main", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
        ?? throw new InvalidOperationException("Could not locate Program.Main method");

    private static (int ExitCode, string StdOut, string StdErr) InvokeMainWithOutput(string[] args)
    {
        var origOut = Console.Out;
        var origErr = Console.Error;
        var outWriter = new StringWriter();
        var errWriter = new StringWriter();
        try
        {
            Console.SetOut(outWriter);
            Console.SetError(errWriter);
            var result = MainMethod.Invoke(null, new object[] { args });
            int exitCode = result is Task<int> taskResult
                ? taskResult.GetAwaiter().GetResult()
                : throw new InvalidOperationException($"Main returned {result?.GetType().Name ?? "null"} instead of Task<int>");
            return (exitCode, outWriter.ToString(), errWriter.ToString());
        }
        finally
        {
            Console.SetOut(origOut);
            Console.SetError(origErr);
        }
    }

    // ── MEDIUM-001: Bounded stdin read ──────────────────────────────

    [Fact]
    public void TryReadBoundedStdin_OversizedInput_ReturnsExit1AndErrorMessage()
    {
        // Arrange — 1 MB + 1 byte of content simulates a malicious/unbounded pipe.
        var oversized = new string('a', Program.MAX_STDIN_BYTES + 1);
        var origIn = Console.In;
        var origErr = Console.Error;
        var errWriter = new StringWriter();
        try
        {
            Console.SetIn(new StringReader(oversized));
            Console.SetError(errWriter);

            // Act
            int rc = Program.TryReadBoundedStdin(out var content);

            // Assert
            Assert.Equal(1, rc);
            Assert.Null(content);
            Assert.Contains("exceeds 1 MB limit", errWriter.ToString());
        }
        finally
        {
            Console.SetIn(origIn);
            Console.SetError(origErr);
        }
    }

    [Fact]
    public void TryReadBoundedStdin_ExactlyAtCap_SucceedsAndReturnsContent()
    {
        // Arrange — exactly 1 MB fits and must not trigger the overflow path.
        var atCap = new string('b', Program.MAX_STDIN_BYTES);
        var origIn = Console.In;
        try
        {
            Console.SetIn(new StringReader(atCap));

            // Act
            int rc = Program.TryReadBoundedStdin(out var content);

            // Assert
            Assert.Equal(0, rc);
            Assert.NotNull(content);
            Assert.Equal(Program.MAX_STDIN_BYTES, content!.Length);
        }
        finally
        {
            Console.SetIn(origIn);
        }
    }

    [Fact]
    public void MaxStdinBytes_MatchesV1Cap()
    {
        // Guard against drift: v1 sets the cap at 1 MiB (1,048,576) in Program.cs:23.
        Assert.Equal(1_048_576, Program.MAX_STDIN_BYTES);
    }

    // ── MEDIUM-002: HTTPS-only endpoint guard ───────────────────────

    [Fact]
    public void Main_HttpEndpoint_ReturnsExit1WithHttpsErrorMessage()
    {
        // Arrange — plaintext http:// endpoint would leak API keys on the wire.
        var origEndpoint = Environment.GetEnvironmentVariable("AZUREOPENAIENDPOINT");
        var origApi = Environment.GetEnvironmentVariable("AZUREOPENAIAPI");
        try
        {
            Environment.SetEnvironmentVariable("AZUREOPENAIENDPOINT", "http://example.com/");
            Environment.SetEnvironmentVariable("AZUREOPENAIAPI", "test-key-not-real");

            // Act — provide a positional prompt so stdin is not consulted.
            var (exitCode, _, stderr) = InvokeMainWithOutput(new[] { "hello" });

            // Assert — exit 1 and v1-identical message.
            Assert.Equal(1, exitCode);
            Assert.Contains("Invalid endpoint URL", stderr);
            Assert.Contains("Must be a valid HTTPS URL", stderr);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZUREOPENAIENDPOINT", origEndpoint);
            Environment.SetEnvironmentVariable("AZUREOPENAIAPI", origApi);
        }
    }

    [Theory]
    [InlineData("ftp://example.com/")]
    [InlineData("file:///etc/passwd")]
    [InlineData("not-a-url")]
    public void Main_NonHttpsEndpointSchemes_AllRejected(string badEndpoint)
    {
        var origEndpoint = Environment.GetEnvironmentVariable("AZUREOPENAIENDPOINT");
        var origApi = Environment.GetEnvironmentVariable("AZUREOPENAIAPI");
        try
        {
            Environment.SetEnvironmentVariable("AZUREOPENAIENDPOINT", badEndpoint);
            Environment.SetEnvironmentVariable("AZUREOPENAIAPI", "test-key-not-real");

            var (exitCode, _, stderr) = InvokeMainWithOutput(new[] { "hello" });

            Assert.Equal(1, exitCode);
            Assert.Contains("Must be a valid HTTPS URL", stderr);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZUREOPENAIENDPOINT", origEndpoint);
            Environment.SetEnvironmentVariable("AZUREOPENAIAPI", origApi);
        }
    }
}
