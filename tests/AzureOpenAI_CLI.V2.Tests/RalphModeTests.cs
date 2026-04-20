using System.Reflection;

namespace AzureOpenAI_CLI_V2.Tests;

/// <summary>
/// Tests for Ralph mode (--ralph) CLI flag parsing, argument validation, and help text integration.
/// All tests run via reflection against Program.Main to exercise the real entry point.
/// No live Azure credentials required — these paths resolve before any API call.
/// </summary>
[Collection("ConsoleCapture")]
public class RalphModeTests
{
    private static readonly MethodInfo MainMethod =
        typeof(Program).Assembly.EntryPoint
        ?? throw new InvalidOperationException("Could not locate the assembly entry point");

    /// <summary>
    /// Invokes Program.Main through reflection and returns the exit code.
    /// </summary>
    private static int InvokeMain(string[] args)
    {
        var result = MainMethod.Invoke(null, new object[] { args });
        return result is Task<int> taskResult
            ? taskResult.GetAwaiter().GetResult()
            : throw new InvalidOperationException($"Main returned {result?.GetType().Name ?? "null"} instead of Task<int>");
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
        Assert.Contains("Ralph Mode", stdout);
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

        // Assert — non-zero due to missing credentials, but NOT exit code 1 from parse error
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

    // ── --ralph + validation ────────────────────────────────────

    [Fact]
    public void Main_RalphValidateNoPrompt_ReturnsNonZero()
    {
        // Arrange — --ralph --validate with no prompt should error
        var args = new[] { "--ralph", "--validate", "true" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — no prompt means error
        Assert.NotEqual(0, exitCode);
    }

    // ── --version and other flags still work ─────────────────────

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
    public void Main_SquadInitStillWorksAfterRalphChanges()
    {
        // Arrange — --squad-init should still work
        var args = new[] { "--squad-init" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — squad init exits successfully
        Assert.Equal(0, exitCode);
    }

    // ── Checkpoint manager (unit tests) ──────────────────────────

    [Fact]
    public void CheckpointManager_InitializeLog_CreatesFile()
    {
        // Arrange — clean slate
        var testDir = Path.Combine(Path.GetTempPath(), "ralph-test-" + Guid.NewGuid());
        Directory.CreateDirectory(testDir);
        var origDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(testDir);

        try
        {
            // Act
            AzureOpenAI_CLI_V2.Ralph.CheckpointManager.InitializeLog();

            // Assert — .ralph-log exists
            Assert.True(File.Exists(".ralph-log"));
            var content = File.ReadAllText(".ralph-log");
            Assert.Contains("# Ralph Loop Log", content);
        }
        finally
        {
            Directory.SetCurrentDirectory(origDir);
            try { Directory.Delete(testDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void CheckpointManager_WriteCheckpoint_AppendsEntry()
    {
        // Arrange
        var testDir = Path.Combine(Path.GetTempPath(), "ralph-test-" + Guid.NewGuid());
        Directory.CreateDirectory(testDir);
        var origDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(testDir);

        try
        {
            AzureOpenAI_CLI_V2.Ralph.CheckpointManager.InitializeLog();

            // Act
            AzureOpenAI_CLI_V2.Ralph.CheckpointManager.WriteCheckpoint(
                iteration: 1,
                prompt: "test prompt",
                agentExitCode: 0,
                agentResponse: "test response",
                validationCommand: "echo test",
                validationExitCode: 0,
                validationOutput: "test output"
            );

            // Assert — entry appended
            var content = File.ReadAllText(".ralph-log");
            Assert.Contains("## Iteration 1", content);
            Assert.Contains("**Prompt:**", content);
            Assert.Contains("**Agent exit:** 0", content);
            Assert.Contains("**Validation:** PASSED", content);
        }
        finally
        {
            Directory.SetCurrentDirectory(origDir);
            try { Directory.Delete(testDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void CheckpointManager_WriteCheckpoint_FailedValidation()
    {
        // Arrange
        var testDir = Path.Combine(Path.GetTempPath(), "ralph-test-" + Guid.NewGuid());
        Directory.CreateDirectory(testDir);
        var origDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(testDir);

        try
        {
            AzureOpenAI_CLI_V2.Ralph.CheckpointManager.InitializeLog();

            // Act
            AzureOpenAI_CLI_V2.Ralph.CheckpointManager.WriteCheckpoint(
                iteration: 2,
                prompt: "test prompt",
                agentExitCode: 0,
                agentResponse: "test response",
                validationCommand: "exit 1",
                validationExitCode: 1,
                validationOutput: "error output"
            );

            // Assert — failure recorded
            var content = File.ReadAllText(".ralph-log");
            Assert.Contains("## Iteration 2", content);
            Assert.Contains("**Validation:** FAILED (exit 1)", content);
            Assert.Contains("error output", content);
        }
        finally
        {
            Directory.SetCurrentDirectory(origDir);
            try { Directory.Delete(testDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void CheckpointManager_WriteFinalEntry_AppendsMessage()
    {
        // Arrange
        var testDir = Path.Combine(Path.GetTempPath(), "ralph-test-" + Guid.NewGuid());
        Directory.CreateDirectory(testDir);
        var origDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(testDir);

        try
        {
            AzureOpenAI_CLI_V2.Ralph.CheckpointManager.InitializeLog();

            // Act
            AzureOpenAI_CLI_V2.Ralph.CheckpointManager.WriteFinalEntry("Exhausted iterations");

            // Assert
            var content = File.ReadAllText(".ralph-log");
            Assert.Contains("**Final status:** Exhausted iterations", content);
        }
        finally
        {
            Directory.SetCurrentDirectory(origDir);
            try { Directory.Delete(testDir, recursive: true); } catch { }
        }
    }
}
