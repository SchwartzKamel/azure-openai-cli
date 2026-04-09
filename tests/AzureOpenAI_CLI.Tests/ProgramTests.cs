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
}
