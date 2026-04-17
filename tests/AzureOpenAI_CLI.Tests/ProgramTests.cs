using System.Reflection;
using AzureOpenAI_CLI;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// Tests for the CLI entry point (Program.Main) covering argument handling
/// and exit codes.
///
/// Because Program and Main are non-public, we invoke Main via Assembly.EntryPoint
/// reflection.  The tests deliberately avoid scenarios that require live Azure
/// credentials — we only exercise paths that resolve before the API call.
/// </summary>
[Collection("ConsoleCapture")]
public class ProgramTests
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

    // ── No-args / help ─────────────────────────────────────────────

    [Fact]
    public void Main_NoArgs_ReturnsExitCode1()
    {
        // Arrange
        var args = Array.Empty<string>();

        // Act
        int exitCode = InvokeMain(args);

        // Assert — no arguments shows usage and exits with 1
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Main_HelpFlag_ReturnsExitCode0()
    {
        // Arrange
        var args = new[] { "--help" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — help succeeds
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Main_ShortHelpFlag_ReturnsExitCode0()
    {
        // Arrange
        var args = new[] { "-h" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — short-form help also succeeds
        Assert.Equal(0, exitCode);
    }

    // ── Model management via CLI flags ─────────────────────────────

    [Fact]
    public void Main_ModelsFlag_ReturnsExitCode0()
    {
        // Arrange — --models lists available models (may be empty without env)
        var args = new[] { "--models" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — listing models always succeeds even if the list is empty
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Main_ListModelsFlag_ReturnsExitCode0()
    {
        // Arrange — alternate spelling
        var args = new[] { "--list-models" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Main_SetModelWithoutName_ReturnsExitCode1()
    {
        // Arrange — --set-model requires a second argument
        var args = new[] { "--set-model" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — missing model name is an error
        Assert.Equal(1, exitCode);
    }

    // ── Prompt validation ──────────────────────────────────────────

    [Fact]
    public void Main_OversizedPrompt_ReturnsExitCode1()
    {
        // Arrange — a prompt exceeding the 32 000-char limit
        // The code validates prompt length before attempting any API call,
        // but only after checking env vars.  Without AZUREOPENAIAPI set the
        // code throws ArgumentNullException which is caught and returns 99.
        // If AZUREOPENAIAPI *is* set but no valid endpoint exists we'd get
        // a different error.  To isolate the prompt-length check we'd need
        // to set env vars, so we test the *negative* exit (not 0) instead.
        var args = new[] { new string('x', 33_000) };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — should not succeed (exact code depends on env state)
        Assert.NotEqual(0, exitCode);
    }

    // ── Version flags ──────────────────────────────────────────────

    [Fact]
    public void Main_VersionFlag_ReturnsExitCode0()
    {
        // Arrange
        var args = new[] { "--version" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — version info printed, exits successfully
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Main_ShortVersionFlag_ReturnsExitCode0()
    {
        // Arrange
        var args = new[] { "-v" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — short-form version also succeeds
        Assert.Equal(0, exitCode);
    }

    // ── JSON mode ──────────────────────────────────────────────────

    [Fact]
    public void Main_JsonFlagNoPrompt_ReturnsNonZero()
    {
        // Arrange — --json with no prompt should error
        var args = new[] { "--json" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — no prompt provided is an error even in JSON mode
        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public void Main_JsonFlagOversizedPrompt_ReturnsNonZero()
    {
        // Arrange — --json with a prompt exceeding 32 000 chars
        var args = new[] { "--json", new string('x', 33_000) };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — oversized prompt is rejected (exact code depends on env state)
        Assert.NotEqual(0, exitCode);
    }

    // ── Schema flag ────────────────────────────────────────────────

    [Fact]
    public void Main_SchemaFlagWithoutValue_ReturnsExitCode1()
    {
        // Arrange — --schema with no value should fail
        var args = new[] { "--schema" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — missing schema value is a parse error
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Main_SchemaFlagWithInvalidJson_ReturnsNonZero()
    {
        // Arrange — --schema with invalid JSON should fail
        var args = new[] { "--schema", "not valid json", "test prompt" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — invalid JSON is rejected (exact code depends on env: parse error or API key missing)
        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public void Main_SchemaFlagWithValidJsonNoCredentials_ReturnsNonZero()
    {
        // Arrange — --schema with valid JSON but no Azure credentials
        var args = new[] { "--schema", "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\"}}}", "test prompt" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — will fail at API call or credential validation, not at schema parsing
        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public void Main_SchemaFlagWithJsonModeInvalidJson_ReturnsNonZero()
    {
        // Arrange — --json --schema with invalid JSON
        var args = new[] { "--json", "--schema", "{invalid", "test prompt" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — invalid JSON schema is rejected
        Assert.NotEqual(0, exitCode);
    }

    // ── Persona / Squad flags ──────────────────────────────────────

    [Fact]
    public void Main_SquadInit_ReturnsExitCode0()
    {
        // Arrange — run --squad-init in an isolated temp directory
        var tempDir = Path.Combine(Path.GetTempPath(), "squad-prog-test-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(tempDir);
            var args = new[] { "--squad-init" };

            // Act
            int exitCode = InvokeMain(args);

            // Assert — init succeeds and creates .squad.json
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(Path.Combine(tempDir, ".squad.json")));
            Assert.True(Directory.Exists(Path.Combine(tempDir, ".squad")));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Main_SquadInit_SecondCall_ReturnsExitCode0()
    {
        // Arrange — run --squad-init twice: second call should also exit 0 (already exists)
        var tempDir = Path.Combine(Path.GetTempPath(), "squad-prog-test2-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(tempDir);

            // Act — first init
            int exitCode1 = InvokeMain(new[] { "--squad-init" });
            Assert.Equal(0, exitCode1);

            // Act — second init (already exists)
            int exitCode2 = InvokeMain(new[] { "--squad-init" });

            // Assert — still exits 0, not an error
            Assert.Equal(0, exitCode2);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Main_PersonasFlag_NoSquadJson_ReturnsExitCode1()
    {
        // Arrange — --personas with no .squad.json should fail
        var tempDir = Path.Combine(Path.GetTempPath(), "squad-prog-test3-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(tempDir);
            var args = new[] { "--personas" };

            // Act
            int exitCode = InvokeMain(args);

            // Assert — no .squad.json means error
            Assert.Equal(1, exitCode);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Main_PersonasFlag_AfterInit_ReturnsExitCode0()
    {
        // Arrange — init, then list personas
        var tempDir = Path.Combine(Path.GetTempPath(), "squad-prog-test4-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(tempDir);
            InvokeMain(new[] { "--squad-init" });

            // Act
            int exitCode = InvokeMain(new[] { "--personas" });

            // Assert — listing personas succeeds after init
            Assert.Equal(0, exitCode);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Main_PersonaFlagWithoutValue_ReturnsExitCode1()
    {
        // Arrange — --persona with no name is a parse error
        var args = new[] { "--persona" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — missing persona name is an error
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Main_PersonaUnknownName_NoSquadJson_ReturnsNonZero()
    {
        // Arrange — --persona with unknown name and no .squad.json
        var tempDir = Path.Combine(Path.GetTempPath(), "squad-prog-test5-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(tempDir);
            var args = new[] { "--persona", "nonexistent", "test prompt" };

            // Act
            int exitCode = InvokeMain(args);

            // Assert — should fail (no .squad.json or unknown persona)
            Assert.NotEqual(0, exitCode);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Main_PersonaUnknownName_WithSquadJson_ReturnsNonZero()
    {
        // Arrange — --persona nonexistent after init should fail
        // Note: exits with non-zero (may be 99 if creds missing, or 1 if persona check runs first)
        var tempDir = Path.Combine(Path.GetTempPath(), "squad-prog-test6-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(tempDir);
            InvokeMain(new[] { "--squad-init" });
            var args = new[] { "--persona", "nonexistent", "test prompt" };

            // Act
            int exitCode = InvokeMain(args);

            // Assert — unknown persona name is an error (exact code depends on env state)
            Assert.NotEqual(0, exitCode);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    // ── Raw flag ─────────────────────────────────────────────────

    [Fact]
    public void Main_RawFlagNoPrompt_ReturnsNonZero()
    {
        // Arrange — --raw with no prompt should error (no prompt provided)
        var args = new[] { "--raw" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — no prompt provided is an error even in raw mode
        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public void Main_RawFlagWithPrompt_DoesNotReturnExitCode1ForParse()
    {
        // Arrange — --raw with a prompt should pass flag parsing
        // Without Azure creds, it will fail later (exit 99), but NOT exit 1 for parse
        var args = new[] { "--raw", "test prompt" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — should pass flag parsing (exit code is NOT 1 from parse error;
        // it may be 99 due to missing credentials, which proves parse check passed)
        Assert.NotEqual(1, exitCode);
    }

    [Fact]
    public void Main_RawFlagCombinedWithOtherFlags_DoesNotReturnExitCode1()
    {
        // Arrange — --raw combined with --temperature should both parse correctly
        var args = new[] { "--raw", "--temperature", "0.5", "test prompt" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — both flags parsed; failure is from missing credentials, not parsing
        Assert.NotEqual(1, exitCode);
    }

    [Fact]
    public void Main_HelpFlag_OutputContainsRaw()
    {
        // Arrange — capture stdout to verify --raw appears in usage output
        var originalOut = Console.Out;
        var writer = new System.IO.StringWriter();
        Console.SetOut(writer);
        try
        {
            // Act
            int exitCode = InvokeMain(new[] { "--help" });

            // Assert — usage text includes --raw documentation
            Assert.Equal(0, exitCode);
            var output = writer.ToString();
            Assert.Contains("--raw", output);
            Assert.Contains("Espanso", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void Main_RawFlagNotRecognizedAsPrompt()
    {
        // Arrange — --raw alone should not be treated as a prompt text
        // It should be consumed as a flag, leaving no prompt
        var args = new[] { "--raw" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — exits non-zero (shows usage since no prompt remaining)
        // The key thing: it should NOT try to use "--raw" as a prompt
        Assert.NotEqual(0, exitCode);
    }

    // ── Temperature range validation ───────────────────────────────

    [Fact]
    public void Main_TemperatureBelowRange_ReturnsExitCode1()
    {
        // Arrange — temperature -0.1 is below the valid range 0.0–2.0
        var args = new[] { "--temperature", "-0.1", "test prompt" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — out-of-range temperature is rejected
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Main_TemperatureAboveRange_ReturnsExitCode1()
    {
        // Arrange — temperature 2.1 exceeds the valid range 0.0–2.0
        var args = new[] { "--temperature", "2.1", "test prompt" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — out-of-range temperature is rejected
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Main_TemperatureValidValue_DoesNotReturnExitCode1ForRange()
    {
        // Arrange — temperature 0.5 is within the valid range;
        // Without Azure creds, it will fail later (exit 99), but NOT exit 1 for range
        var args = new[] { "--temperature", "0.5", "test prompt" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — should pass range validation (exit code is NOT 1 from parse error;
        // it may be 99 due to missing credentials, which proves range check passed)
        Assert.NotEqual(1, exitCode);
    }

    [Fact]
    public void Main_TemperatureLowerBound_Accepted()
    {
        // Arrange — temperature 0.0 is the lower bound, should be accepted
        var args = new[] { "--temperature", "0.0", "test prompt" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — boundary value passes range validation
        Assert.NotEqual(1, exitCode);
    }

    [Fact]
    public void Main_TemperatureUpperBound_Accepted()
    {
        // Arrange — temperature 2.0 is the upper bound, should be accepted
        var args = new[] { "--temperature", "2.0", "test prompt" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — boundary value passes range validation
        Assert.NotEqual(1, exitCode);
    }

    // ── Max-tokens range validation ────────────────────────────────

    [Fact]
    public void Main_MaxTokensZero_ReturnsExitCode1()
    {
        // Arrange — max-tokens 0 is below the valid range 1–128000
        var args = new[] { "--max-tokens", "0", "test prompt" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — out-of-range max-tokens is rejected
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Main_MaxTokensNegative_ReturnsExitCode1()
    {
        // Arrange — max-tokens -1 is below the valid range
        var args = new[] { "--max-tokens", "-1", "test prompt" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — negative max-tokens is rejected
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Main_MaxTokensAboveRange_ReturnsExitCode1()
    {
        // Arrange — max-tokens 200000 exceeds the valid range 1–128000
        var args = new[] { "--max-tokens", "200000", "test prompt" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — out-of-range max-tokens is rejected
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Main_MaxTokensValidValue_DoesNotReturnExitCode1ForRange()
    {
        // Arrange — max-tokens 4096 is within the valid range;
        // Without Azure creds, it will fail later (exit 99), but NOT exit 1 for range
        var args = new[] { "--max-tokens", "4096", "test prompt" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — should pass range validation (exit code is NOT 1 from parse error)
        Assert.NotEqual(1, exitCode);
    }

    [Fact]
    public void Main_MaxTokensLowerBound_Accepted()
    {
        // Arrange — max-tokens 1 is the lower bound, should be accepted
        var args = new[] { "--max-tokens", "1", "test prompt" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — boundary value passes range validation
        Assert.NotEqual(1, exitCode);
    }

    [Fact]
    public void Main_MaxTokensUpperBound_Accepted()
    {
        // Arrange — max-tokens 128000 is the upper bound, should be accepted
        var args = new[] { "--max-tokens", "128000", "test prompt" };

        // Act
        int exitCode = InvokeMain(args);

        // Assert — boundary value passes range validation
        Assert.NotEqual(1, exitCode);
    }

    // ── ErrorAndExit helper (DRY error handling) ───────────────────

    [Fact]
    public void ErrorAndExit_NonJsonMode_WritesErrorPrefixToStderr()
    {
        // Arrange — call the internal helper directly in non-JSON mode
        var originalErr = Console.Error;
        var errWriter = new System.IO.StringWriter();
        Console.SetError(errWriter);
        try
        {
            // Act
            int exitCode = Program.ErrorAndExit("something went wrong", 1, jsonMode: false);

            // Assert — produces [ERROR] prefix on stderr and returns correct exit code
            Assert.Equal(1, exitCode);
            var stderrOutput = errWriter.ToString();
            Assert.Contains("[ERROR]", stderrOutput);
            Assert.Contains("something went wrong", stderrOutput);
            // Negative: must NOT contain JSON tokens
            Assert.DoesNotContain("\"error\"", stderrOutput);
            Assert.DoesNotContain("\"exit_code\"", stderrOutput);
        }
        finally
        {
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void ErrorAndExit_JsonMode_WritesJsonToStdout()
    {
        // Arrange — call the internal helper in JSON mode
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        var outWriter = new System.IO.StringWriter();
        var errWriter = new System.IO.StringWriter();
        Console.SetOut(outWriter);
        Console.SetError(errWriter);
        try
        {
            // Act
            int exitCode = Program.ErrorAndExit("bad request", 2, jsonMode: true);

            // Assert — returns correct exit code, outputs JSON to stdout
            Assert.Equal(2, exitCode);
            var stdoutOutput = outWriter.ToString();
            Assert.Contains("\"error\"", stdoutOutput);
            Assert.Contains("bad request", stdoutOutput);
            Assert.Contains("\"exit_code\": 2", stdoutOutput);
            // Negative: stderr should NOT get the [ERROR] prefix in JSON mode
            var stderrOutput = errWriter.ToString();
            Assert.DoesNotContain("[ERROR]", stderrOutput);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void ErrorAndExit_PreservesExitCode99()
    {
        // Arrange — verify non-standard exit codes are preserved
        var originalErr = Console.Error;
        var errWriter = new System.IO.StringWriter();
        Console.SetError(errWriter);
        try
        {
            // Act
            int exitCode = Program.ErrorAndExit("unhandled crash", 99, jsonMode: false);

            // Assert — exit code 99 is returned faithfully
            Assert.Equal(99, exitCode);
            var stderrOutput = errWriter.ToString();
            Assert.Contains("[ERROR] unhandled crash", stderrOutput);
        }
        finally
        {
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void ErrorAndExit_PreservesExitCode3()
    {
        // Arrange — verify timeout exit code is preserved
        var originalErr = Console.Error;
        var errWriter = new System.IO.StringWriter();
        Console.SetError(errWriter);
        try
        {
            // Act
            int exitCode = Program.ErrorAndExit("timed out", 3, jsonMode: false);

            // Assert — exit code 3 is returned faithfully
            Assert.Equal(3, exitCode);
        }
        finally
        {
            Console.SetError(originalErr);
        }
    }

    // ── Integration: ErrorAndExit through Main paths ───────────────

    [Fact]
    public void Main_TaskFileNotFound_StderrContainsErrorPrefix()
    {
        // Arrange — --task-file with a nonexistent path triggers ErrorAndExit
        var originalErr = Console.Error;
        var errWriter = new System.IO.StringWriter();
        Console.SetError(errWriter);
        try
        {
            var args = new[] { "--task-file", "/nonexistent/path/task.md", "dummy" };

            // Act
            int exitCode = InvokeMain(args);

            // Assert — exit code 1, stderr contains [ERROR] and the file path
            Assert.Equal(1, exitCode);
            var stderrOutput = errWriter.ToString();
            Assert.Contains("[ERROR]", stderrOutput);
            Assert.Contains("Task file not found", stderrOutput);
            Assert.Contains("/nonexistent/path/task.md", stderrOutput);
            // Negative: should NOT contain JSON structure in stderr
            Assert.DoesNotContain("\"error\"", stderrOutput);
        }
        finally
        {
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void Main_TaskFileNotFound_JsonMode_StdoutContainsJson()
    {
        // Arrange — --json --task-file with nonexistent path triggers JSON error output
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        var outWriter = new System.IO.StringWriter();
        var errWriter = new System.IO.StringWriter();
        Console.SetOut(outWriter);
        Console.SetError(errWriter);
        try
        {
            var args = new[] { "--json", "--task-file", "/nonexistent/path/task.md", "dummy" };

            // Act
            int exitCode = InvokeMain(args);

            // Assert — exit code 1, JSON error on stdout
            Assert.Equal(1, exitCode);
            var stdoutOutput = outWriter.ToString();
            Assert.Contains("\"error\": true", stdoutOutput);
            Assert.Contains("Task file not found", stdoutOutput);
            // Negative: stderr should NOT get [ERROR] prefix in JSON mode
            var stderrOutput = errWriter.ToString();
            Assert.DoesNotContain("[ERROR]", stderrOutput);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void Main_OversizedPrompt_StderrContainsErrorPrefix()
    {
        // Arrange — prompt exceeding 32K chars routed through ErrorAndExit
        // Need valid env vars so we get past the creds check to the prompt length check
        var prevKey = Environment.GetEnvironmentVariable("AZUREOPENAIAPI");
        var prevEndpoint = Environment.GetEnvironmentVariable("AZUREOPENAIENDPOINT");
        var prevModel = Environment.GetEnvironmentVariable("AZUREOPENAIMODEL");
        Environment.SetEnvironmentVariable("AZUREOPENAIAPI", "test-key-for-unit-test");
        Environment.SetEnvironmentVariable("AZUREOPENAIENDPOINT", "https://fake.openai.azure.com");
        Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", "gpt-4");
        var originalErr = Console.Error;
        var errWriter = new System.IO.StringWriter();
        Console.SetError(errWriter);
        try
        {
            var args = new[] { new string('x', 33_000) };

            // Act
            int exitCode = InvokeMain(args);

            // Assert — oversized prompt caught, [ERROR] on stderr with prompt length detail
            Assert.Equal(1, exitCode);
            var stderrOutput = errWriter.ToString();
            Assert.Contains("[ERROR]", stderrOutput);
            Assert.Contains("Prompt too long", stderrOutput);
        }
        finally
        {
            Console.SetError(originalErr);
            Environment.SetEnvironmentVariable("AZUREOPENAIAPI", prevKey);
            Environment.SetEnvironmentVariable("AZUREOPENAIENDPOINT", prevEndpoint);
            Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", prevModel);
        }
    }
}
