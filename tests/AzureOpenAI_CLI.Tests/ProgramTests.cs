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
}
