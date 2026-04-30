using AzureOpenAI_CLI;
using Xunit;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// Tests for <c>--config export-env</c> (<see cref="Program.HandleExportEnv"/>).
/// Drives the handler directly with a synthesised <see cref="Program.CliOptions"/>
/// + in-memory <see cref="UserConfig"/>, capturing stdout/stderr to assert on
/// the contract: confirmation flag required, no partial output on failure,
/// exactly three KV lines on success, --raw suppresses the warning.
/// </summary>
[Collection("ConsoleCapture")]
public class ExportEnvTests : IDisposable
{
    private readonly string? _origEndpoint;
    private readonly string? _origApi;
    private readonly string? _origModel;

    public ExportEnvTests()
    {
        // Snapshot any process-level env vars so we can scrub for deterministic
        // resolution and restore on Dispose. xUnit shares a process across
        // tests, so without this a developer's exported AZUREOPENAI* would
        // mask the negative-path assertions.
        _origEndpoint = Environment.GetEnvironmentVariable("AZUREOPENAIENDPOINT");
        _origApi = Environment.GetEnvironmentVariable("AZUREOPENAIAPI");
        _origModel = Environment.GetEnvironmentVariable("AZUREOPENAIMODEL");
        Environment.SetEnvironmentVariable("AZUREOPENAIENDPOINT", null);
        Environment.SetEnvironmentVariable("AZUREOPENAIAPI", null);
        Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("AZUREOPENAIENDPOINT", _origEndpoint);
        Environment.SetEnvironmentVariable("AZUREOPENAIAPI", _origApi);
        Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", _origModel);
    }

    private static Program.CliOptions Opts(bool confirm, bool json = false, bool raw = false) =>
        Program.ParseArgs(new[] { "--config", "export-env" }) with
        {
            ConfirmPrintSecret = confirm,
            Json = json,
            Raw = raw,
        };

    [Fact]
    public void ExportEnv_without_confirmation_flag_errors_and_does_not_print_key()
    {
        var origOut = Console.Out;
        var origErr = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        Console.SetOut(stdout);
        Console.SetError(stderr);
        try
        {
            var cfg = new UserConfig
            {
                Endpoint = "https://example.openai.azure.com/",
                ApiKey = "abc123-secret",
            };
            int rc = Program.HandleExportEnv(Opts(confirm: false), cfg);
            Assert.NotEqual(0, rc);
            Assert.Contains("--i-understand-this-will-print-the-secret", stderr.ToString());
            // Negative-path assertion: NO key/endpoint may leak to stdout.
            Assert.DoesNotContain("AZUREOPENAIAPI=", stdout.ToString());
            Assert.DoesNotContain("abc123-secret", stdout.ToString());
        }
        finally
        {
            Console.SetOut(origOut);
            Console.SetError(origErr);
        }
    }

    [Fact]
    public void ExportEnv_without_endpoint_errors_with_no_partial_stdout()
    {
        var origOut = Console.Out;
        var origErr = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        Console.SetOut(stdout);
        Console.SetError(stderr);
        try
        {
            // No env vars (cleared in ctor), no Endpoint, no ApiKey.
            int rc = Program.HandleExportEnv(Opts(confirm: true), new UserConfig());
            Assert.Equal(1, rc);
            Assert.Contains("[ERROR]", stderr.ToString());
            Assert.Contains("endpoint", stderr.ToString(), StringComparison.OrdinalIgnoreCase);
            // Contract: no half-set env block leaks to stdout.
            Assert.Equal(string.Empty, stdout.ToString());
        }
        finally
        {
            Console.SetOut(origOut);
            Console.SetError(origErr);
        }
    }

    [Fact]
    public void ExportEnv_emits_three_kv_lines_on_success()
    {
        Environment.SetEnvironmentVariable("AZUREOPENAIENDPOINT", "https://test.openai.azure.com/");
        Environment.SetEnvironmentVariable("AZUREOPENAIAPI", "test-key-xyz");
        Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", "gpt-4o-mini");

        var origOut = Console.Out;
        var origErr = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        Console.SetOut(stdout);
        Console.SetError(stderr);
        try
        {
            int rc = Program.HandleExportEnv(Opts(confirm: true), new UserConfig());
            Assert.Equal(0, rc);

            var lines = stdout.ToString()
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.TrimEnd('\r'))
                .ToArray();
            Assert.Equal(3, lines.Length);
            Assert.Equal("AZUREOPENAIENDPOINT=https://test.openai.azure.com/", lines[0]);
            Assert.Equal("AZUREOPENAIAPI=test-key-xyz", lines[1]);
            Assert.Equal("AZUREOPENAIMODEL=gpt-4o-mini", lines[2]);

            // Loud warning lands on stderr in default mode.
            Assert.Contains("[WARNING]", stderr.ToString());
        }
        finally
        {
            Console.SetOut(origOut);
            Console.SetError(origErr);
        }
    }

    [Fact]
    public void ExportEnv_raw_mode_suppresses_stderr_warning()
    {
        Environment.SetEnvironmentVariable("AZUREOPENAIENDPOINT", "https://test.openai.azure.com/");
        Environment.SetEnvironmentVariable("AZUREOPENAIAPI", "test-key");

        var origOut = Console.Out;
        var origErr = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        Console.SetOut(stdout);
        Console.SetError(stderr);
        try
        {
            int rc = Program.HandleExportEnv(Opts(confirm: true, raw: true), new UserConfig());
            Assert.Equal(0, rc);
            Assert.DoesNotContain("[WARNING]", stderr.ToString());
            Assert.Contains("AZUREOPENAIAPI=test-key", stdout.ToString());
        }
        finally
        {
            Console.SetOut(origOut);
            Console.SetError(origErr);
        }
    }
}
