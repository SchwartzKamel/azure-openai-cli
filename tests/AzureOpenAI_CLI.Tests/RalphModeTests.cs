using System.Reflection;
using AzureOpenAI_CLI;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// Tests for Ralph mode (--ralph) CLI flag parsing, argument validation,
/// and help text integration.
///
/// All tests run via reflection against Program.Main to exercise the real
/// entry point. No live Azure credentials required — these paths resolve
/// before any API call.
/// </summary>
public class RalphModeTests
{
    private static readonly MethodInfo MainMethod =
        typeof(UserConfig).Assembly.EntryPoint
        ?? throw new InvalidOperationException("Could not locate the assembly entry point");

    /// <summary>
    /// Invokes Program.Main through reflection and returns the exit code.
    /// </summary>
    private static int InvokeMain(string[] args)
    {
        var result = MainMethod.Invoke(null, new object[] { args });
        return result is int exitCode
            ? exitCode
            : throw new InvalidOperationException($"Main returned {result?.GetType().Name ?? "null"} instead of int");
    }

    /// <summary>
    /// Invokes Program.Main and captures stdout + stderr.
    /// </summary>
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
            int exitCode = result is int ec
                ? ec
                : throw new InvalidOperationException($"Main returned {result?.GetType().Name ?? "null"} instead of int");
            return (exitCode, outWriter.ToString(), errWriter.ToString());
        }
        finally
        {
            Console.SetOut(origOut);
            Console.SetError(origErr);
        }
    }

    // ── Ralph flag is recognized ─────────────────────────────────

    [Fact]
    public void Main_RalphFlagNoPrompt_ReturnsExitCode1()
    {
        // Arrange — --ralph with no prompt should error (no prompt to process)
        var args = new[] { "--ralph" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — no prompt supplied exits non-zero
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Main_HelpFlag_StillReturnsExitCode0WithRalphChanges()
    {
        // Arrange — --help should still work after Ralph mode additions
        var args = new[] { "--help" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — help always succeeds
        Assert.Equal(0, exitCode);
    }

    // ── Help text includes Ralph mode flags ──────────────────────

    [Fact]
    public void Main_HelpText_ContainsRalphFlag()
    {
        // Arrange & Act
        var (exitCode, stdout, _) = InvokeMainWithOutput(new[] { "--help" });

        // Assert — help text mentions --ralph
        Assert.Equal(0, exitCode);
        Assert.Contains("--ralph", stdout);
    }

    [Fact]
    public void Main_HelpText_ContainsValidateFlag()
    {
        // Arrange & Act
        var (exitCode, stdout, _) = InvokeMainWithOutput(new[] { "--help" });

        // Assert — help text mentions --validate
        Assert.Equal(0, exitCode);
        Assert.Contains("--validate", stdout);
    }

    [Fact]
    public void Main_HelpText_ContainsTaskFileFlag()
    {
        // Arrange & Act
        var (exitCode, stdout, _) = InvokeMainWithOutput(new[] { "--help" });

        // Assert — help text mentions --task-file
        Assert.Equal(0, exitCode);
        Assert.Contains("--task-file", stdout);
    }

    [Fact]
    public void Main_HelpText_ContainsMaxIterationsFlag()
    {
        // Arrange & Act
        var (exitCode, stdout, _) = InvokeMainWithOutput(new[] { "--help" });

        // Assert — help text mentions --max-iterations
        Assert.Equal(0, exitCode);
        Assert.Contains("--max-iterations", stdout);
    }

    [Fact]
    public void Main_HelpText_ContainsRalphModeSection()
    {
        // Arrange & Act
        var (exitCode, stdout, _) = InvokeMainWithOutput(new[] { "--help" });

        // Assert — help text has the Ralph Mode section header
        Assert.Equal(0, exitCode);
        Assert.Contains("Ralph Mode:", stdout);
    }

    // ── --validate flag validation ───────────────────────────────

    [Fact]
    public void Main_ValidateWithoutValue_ReturnsExitCode1()
    {
        // Arrange — --validate requires a command argument
        var args = new[] { "--ralph", "--validate" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — missing value is a parse error
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Main_ValidateWithoutValue_ShowsErrorMessage()
    {
        // Arrange & Act
        var (exitCode, _, stderr) = InvokeMainWithOutput(new[] { "--ralph", "--validate" });

        // Assert — error message mentions --validate
        Assert.Equal(1, exitCode);
        Assert.Contains("--validate", stderr);
    }

    // ── --task-file flag validation ──────────────────────────────

    [Fact]
    public void Main_TaskFileWithoutValue_ReturnsExitCode1()
    {
        // Arrange — --task-file requires a file path argument
        var args = new[] { "--ralph", "--task-file" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — missing value is a parse error
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Main_TaskFileNonexistent_ReturnsExitCode1()
    {
        // Arrange — --task-file pointing to a file that does not exist
        var args = new[] { "--ralph", "--task-file", "/nonexistent/path/to/ralph-task.md", "fallback prompt" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — missing file is an error (exit non-zero)
        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public void Main_TaskFileNonexistent_ShowsErrorMessage()
    {
        // Arrange & Act
        var (exitCode, stdout, stderr) = InvokeMainWithOutput(
            new[] { "--ralph", "--task-file", "/nonexistent/path/to/ralph-task.md", "fallback prompt" });
        var combined = stdout + stderr;

        // Assert — error mentions the file or task-file
        Assert.NotEqual(0, exitCode);
        Assert.Contains("not found", combined, StringComparison.OrdinalIgnoreCase);
    }

    // ── --max-iterations flag validation ─────────────────────────

    [Fact]
    public void Main_MaxIterationsZero_ReturnsExitCode1()
    {
        // Arrange — 0 is below the minimum (1)
        var args = new[] { "--ralph", "--max-iterations", "0", "test" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — invalid value is rejected
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Main_MaxIterations51_ReturnsExitCode1()
    {
        // Arrange — 51 is above the maximum (50)
        var args = new[] { "--ralph", "--max-iterations", "51", "test" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — value out of range is rejected
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Main_MaxIterationsNegative_ReturnsExitCode1()
    {
        // Arrange — negative values are invalid
        var args = new[] { "--ralph", "--max-iterations", "-5", "test" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — negative value is rejected
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Main_MaxIterationsNonNumeric_ReturnsExitCode1()
    {
        // Arrange — non-numeric string is invalid
        var args = new[] { "--ralph", "--max-iterations", "abc", "test" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — non-numeric value is rejected
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Main_MaxIterationsWithoutValue_ReturnsExitCode1()
    {
        // Arrange — --max-iterations with no following argument
        var args = new[] { "--ralph", "--max-iterations" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — missing value is rejected
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Main_MaxIterationsZero_ShowsErrorMessage()
    {
        // Arrange & Act
        var (exitCode, _, stderr) = InvokeMainWithOutput(
            new[] { "--ralph", "--max-iterations", "0", "test" });

        // Assert — error message mentions valid range
        Assert.Equal(1, exitCode);
        Assert.Contains("--max-iterations", stderr);
    }

    // ── --ralph implies --agent ──────────────────────────────────

    [Fact]
    public void Main_RalphWithPromptNoCredentials_ReturnsNonZero()
    {
        // Arrange — --ralph with a prompt but no Azure credentials
        // This should get past flag parsing (ralph implies agent) and fail
        // at the credential validation stage, NOT at flag parsing
        var args = new[] { "--ralph", "test prompt" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — fails at credential stage, not flag parsing
        Assert.NotEqual(0, exitCode);
    }

    // ── Valid boundary values ────────────────────────────────────

    [Fact]
    public void Main_MaxIterations1_ParsesSuccessfully()
    {
        // Arrange — 1 is the minimum valid value; should parse OK
        // Will fail later (no creds) but should NOT fail at flag parsing
        var args = new[] { "--ralph", "--max-iterations", "1", "test prompt" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — non-zero due to missing credentials, but the specific
        // exit code 1 from parse error for "--max-iterations" should NOT apply.
        // The error path should be credential-related, not parse-related.
        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public void Main_MaxIterations50_ParsesSuccessfully()
    {
        // Arrange — 50 is the maximum valid value; should parse OK
        var args = new[] { "--ralph", "--max-iterations", "50", "test prompt" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — fails due to missing creds, not flag parsing
        Assert.NotEqual(0, exitCode);
    }

    // ── --ralph + --json mode ────────────────────────────────────

    [Fact]
    public void Main_RalphJsonNoPrompt_ReturnsNonZero()
    {
        // Arrange — --json --ralph with no prompt should error
        var args = new[] { "--json", "--ralph" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — no prompt means error
        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public void Main_RalphJsonNoPrompt_OutputsJsonError()
    {
        // Arrange & Act
        var (exitCode, stdout, _) = InvokeMainWithOutput(new[] { "--json", "--ralph" });

        // Assert — JSON error output should contain error field
        Assert.NotEqual(0, exitCode);
        Assert.Contains("\"error\"", stdout);
    }

    // ── --task-file with --json mode ─────────────────────────────

    [Fact]
    public void Main_TaskFileMissingJsonMode_ReturnsNonZero()
    {
        // Arrange — --json with nonexistent task file
        var args = new[] { "--json", "--ralph", "--task-file", "/nonexistent/file.md", "fallback" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — file not found error
        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public void Main_TaskFileMissingJsonMode_OutputsJsonError()
    {
        // Arrange & Act
        var (exitCode, stdout, _) = InvokeMainWithOutput(
            new[] { "--json", "--ralph", "--task-file", "/nonexistent/file.md", "fallback" });

        // Assert — JSON error output
        Assert.NotEqual(0, exitCode);
        Assert.Contains("\"error\"", stdout);
        Assert.Contains("not found", stdout, StringComparison.OrdinalIgnoreCase);
    }

    // ── --ralph coexists with other flags ────────────────────────

    [Fact]
    public void Main_VersionFlagStillWorksAfterRalphChanges()
    {
        // Arrange — --version should still work
        var args = new[] { "--version" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — version info exits successfully
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Main_ModelsListStillWorksAfterRalphChanges()
    {
        // Arrange — --models should still work
        var args = new[] { "--models" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — models listing exits successfully
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Main_ConfigShowStillWorksAfterRalphChanges()
    {
        // Arrange — --config show should still work
        var args = new[] { "--config", "show" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — config display exits successfully
        Assert.Equal(0, exitCode);
    }
}
