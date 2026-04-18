using System.Reflection;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// Tests for graceful CTRL+C cancellation plumbing: the Console.CancelKeyPress
/// hook, the 130 exit-code mapping (SIGINT = 128+2), and the Ralph log flush
/// on cancel.
///
/// We cannot send a real SIGINT inside xUnit, so we exercise the *internal*
/// surface that Main relies on: the registration method, the exit-code mapper,
/// and the log-writer. Each test pairs a positive assertion ("pass the pass")
/// with a negative assertion ("fail the fail").
/// </summary>
[Collection("ConsoleCapture")]
public class CancellationTests
{
    private static readonly Type ProgramType =
        typeof(UserConfig).Assembly.GetType("Program")
        ?? throw new InvalidOperationException("Program type not found");

    private static T GetStatic<T>(string name)
    {
        var prop = ProgramType.GetProperty(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (prop != null) return (T)prop.GetValue(null)!;
        var field = ProgramType.GetField(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (field != null) return (T)field.GetValue(null)!;
        throw new InvalidOperationException($"No static member '{name}' on Program");
    }

    private static object? InvokeStatic(string method, params object?[] args)
    {
        var mi = ProgramType.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                 ?? throw new InvalidOperationException($"No method '{method}'");
        return mi.Invoke(null, args);
    }

    // ── CancelKeyPress handler registration ────────────────────────

    [Fact]
    public void RegisterCancelKeyPress_SetsRegisteredFlag()
    {
        // Arrange — force registration (idempotent; safe to call many times).
        using var cts = new CancellationTokenSource();
        InvokeStatic("RegisterCancelKeyPress", cts);

        // Assert positive: flag flips to true
        Assert.True(GetStatic<bool>("CancelHandlerRegistered"),
            "CancelHandlerRegistered must be true after RegisterCancelKeyPress");

        // Assert negative: ShutdownCts must NOT be null once registered
        Assert.NotNull(GetStatic<CancellationTokenSource?>("ShutdownCts"));
    }

    [Fact]
    public void RegisterCancelKeyPress_IsIdempotent()
    {
        // Arrange — register twice with different CTS instances
        using var first = new CancellationTokenSource();
        InvokeStatic("RegisterCancelKeyPress", first);
        var after1 = GetStatic<CancellationTokenSource?>("ShutdownCts");

        using var second = new CancellationTokenSource();
        InvokeStatic("RegisterCancelKeyPress", second);
        var after2 = GetStatic<CancellationTokenSource?>("ShutdownCts");

        // Assert positive: first registration wins; second is ignored
        Assert.Same(after1, after2);

        // Assert negative: the second CTS must NOT have replaced the first
        Assert.NotSame(second, after2);
    }

    // ── Exit code mapping (130 on SIGINT, 3 on timeout) ────────────

    [Fact]
    public void MapCancellationExitCode_ReturnsExpectedCodes()
    {
        // Positive: external cancel → 130
        int cancelled = (int)InvokeStatic("MapCancellationExitCode", true)!;
        Assert.Equal(130, cancelled);

        // Negative: not externally cancelled (timeout) → 3, NOT 130
        int timedOut = (int)InvokeStatic("MapCancellationExitCode", false)!;
        Assert.Equal(3, timedOut);
        Assert.NotEqual(130, timedOut);
    }

    [Fact]
    public void ExitCodeCancelledConstant_FollowsSigintConvention()
    {
        // SIGINT by POSIX convention: 128 + 2 = 130
        var fld = ProgramType.GetField("EXIT_CODE_CANCELLED",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(fld);
        int value = (int)fld!.GetRawConstantValue()!;

        // Positive: matches SIGINT convention
        Assert.Equal(130, value);
        // Negative: NOT zero (would indicate success) and NOT legacy timeout code 3
        Assert.NotEqual(0, value);
        Assert.NotEqual(3, value);
    }

    // ── Ralph log flush ─────────────────────────────────────────────

    [Fact]
    public void WriteRalphLog_FlushesContentToCurrentDirectory()
    {
        // Arrange — work in a scratch cwd so we don't stomp the repo's .ralph-log
        var origCwd = Directory.GetCurrentDirectory();
        var tmp = Directory.CreateTempSubdirectory("ralph-cancel-test-");
        Directory.SetCurrentDirectory(tmp.FullName);
        try
        {
            // Act
            InvokeStatic("WriteRalphLog", "## Iteration 1\n**Status:** [cancelled]\n");

            // Assert positive: file written with the marker
            var path = Path.Combine(tmp.FullName, ".ralph-log");
            Assert.True(File.Exists(path), ".ralph-log must be written on flush");
            var content = File.ReadAllText(path);
            Assert.Contains("[cancelled]", content);
            Assert.Contains("# Ralph Loop Log", content);

            // Assert negative: no stale content from an earlier iteration leaked in
            Assert.DoesNotContain("Iteration 99", content);
        }
        finally
        {
            Directory.SetCurrentDirectory(origCwd);
            try { Directory.Delete(tmp.FullName, recursive: true); } catch { }
        }
    }

    [Fact]
    public void WriteRalphLog_IsBestEffort_WhenPathUnwritable()
    {
        // Arrange — cwd where we cannot write (use a non-existent read-only dir path).
        // If WriteAllText throws, the method must swallow it (best-effort).
        var origCwd = Directory.GetCurrentDirectory();
        var tmp = Directory.CreateTempSubdirectory("ralph-cancel-ro-");
        Directory.SetCurrentDirectory(tmp.FullName);
        try
        {
            // Pre-create .ralph-log as a directory so WriteAllText on the path fails.
            Directory.CreateDirectory(Path.Combine(tmp.FullName, ".ralph-log"));

            // Positive: invocation must NOT throw — exception is swallowed.
            var ex = Record.Exception(() => InvokeStatic("WriteRalphLog", "content"));
            Assert.Null(ex);

            // Negative: ensure the path is still a directory (nothing was corrupted).
            Assert.True(Directory.Exists(Path.Combine(tmp.FullName, ".ralph-log")));
            Assert.False(File.Exists(Path.Combine(tmp.FullName, ".ralph-log")));
        }
        finally
        {
            Directory.SetCurrentDirectory(origCwd);
            try { Directory.Delete(tmp.FullName, recursive: true); } catch { }
        }
    }

    // ── Help text mentions CTRL+C ──────────────────────────────────

    [Fact]
    public void Help_Text_Mentions_CtrlC_Cancellation()
    {
        var mainMethod = typeof(UserConfig).Assembly.EntryPoint!;
        var origOut = Console.Out;
        var sw = new StringWriter();
        try
        {
            Console.SetOut(sw);
            mainMethod.Invoke(null, new object[] { new[] { "--help" } });
        }
        finally
        {
            Console.SetOut(origOut);
        }
        var help = sw.ToString();

        // Positive: help surfaces the CTRL+C behaviour + 130 exit code
        Assert.Contains("CTRL+C", help);
        Assert.Contains("130", help);

        // Negative: it does NOT claim we exit with 0 on cancel (that would be misleading)
        Assert.DoesNotContain("CTRL+C exits with 0", help);
    }
}
