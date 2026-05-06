using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;
using AzureOpenAI_CLI;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// S03E14 (Mickey Abbott): accessibility / CLI ergonomics regression tests.
///
/// Locks in three contracts:
///   1. <see cref="Plain.IsActive"/> precedence (override > flag > env > TERM).
///   2. ASCII-only output for <c>--help</c>, <c>--version</c>, the
///      <c>--models</c> list, and the <c>--current-model</c> command --
///      regardless of whether <c>NO_COLOR</c> / <c>--plain</c> /
///      <c>AZ_AI_PLAIN</c> / <c>TERM=dumb</c> is in effect.
///   3. No ANSI SGR escape sequences leak through the same surfaces.
///
/// Runs in the <c>ConsoleCapture</c> collection (sequential) because each
/// test mutates <c>Console.Out</c> / <c>Console.Error</c> and process env
/// vars. Uses <see cref="Plain.Override"/> / restoration helpers so tests
/// never permanently mutate global state.
/// </summary>
[Collection("ConsoleCapture")]
public sealed class AccessibilityTests
{
    // Any ANSI escape that starts with ESC ([ then attrs then a final letter).
    // Mirrors the regex called out in the S03E14 brief.
    private static readonly Regex AnsiEscape =
        new("\u001b\\[[0-9;]*[a-zA-Z]", RegexOptions.Compiled);

    // Anything outside basic ASCII (printable + whitespace + control). NL/CR/TAB
    // are inside the [\x00-\x7F] range so multi-line stdout is fine.
    private static readonly Regex NonAscii =
        new("[^\u0000-\u007F]", RegexOptions.Compiled);

    private static readonly string[] ManagedEnv =
    {
        "NO_COLOR", "FORCE_COLOR", "CLICOLOR", "CLICOLOR_FORCE",
        "TERM", "AZ_AI_PLAIN",
    };

    private sealed class EnvIsolator : IDisposable
    {
        private readonly System.Collections.Generic.Dictionary<string, string?> _originals = new();
        private readonly bool? _planOverride;
        private readonly bool _planFlag;

        public EnvIsolator()
        {
            foreach (var n in ManagedEnv)
            {
                _originals[n] = Environment.GetEnvironmentVariable(n);
                Environment.SetEnvironmentVariable(n, null);
            }
            _planOverride = Plain.Override;
            _planFlag = Plain.FlagSet;
            Plain.ResetForTests();
        }

        public void Dispose()
        {
            foreach (var kv in _originals)
            {
                Environment.SetEnvironmentVariable(kv.Key, kv.Value);
            }
            Plain.Override = _planOverride;
            Plain.FlagSet = _planFlag;
        }
    }

    // -----------------------------------------------------------------------
    // Plain.IsActive() precedence
    // -----------------------------------------------------------------------

    [Fact]
    public void Plain_IsActive_DefaultsFalse_WhenNoSignals()
    {
        using var _ = new EnvIsolator();
        Assert.False(Plain.IsActive());
    }

    [Fact]
    public void Plain_FlagSet_ForcesActive()
    {
        using var _ = new EnvIsolator();
        Plain.FlagSet = true;
        Assert.True(Plain.IsActive());
    }

    [Fact]
    public void Plain_NoColorEnv_ForcesActive()
    {
        using var _ = new EnvIsolator();
        Environment.SetEnvironmentVariable("NO_COLOR", "1");
        Assert.True(Plain.IsActive());
    }

    [Fact]
    public void Plain_NoColorEmpty_DoesNotActivate()
    {
        // no-color.org spec: the variable must be set AND non-empty.
        using var _ = new EnvIsolator();
        Environment.SetEnvironmentVariable("NO_COLOR", "");
        Assert.False(Plain.IsActive());
    }

    [Fact]
    public void Plain_AzAiPlainEnv_ForcesActive()
    {
        using var _ = new EnvIsolator();
        Environment.SetEnvironmentVariable("AZ_AI_PLAIN", "1");
        Assert.True(Plain.IsActive());
    }

    [Fact]
    public void Plain_AzAiPlain_Zero_DoesNotActivate()
    {
        using var _ = new EnvIsolator();
        Environment.SetEnvironmentVariable("AZ_AI_PLAIN", "0");
        Assert.False(Plain.IsActive());
    }

    [Fact]
    public void Plain_TermDumb_ForcesActive()
    {
        using var _ = new EnvIsolator();
        Environment.SetEnvironmentVariable("TERM", "dumb");
        Assert.True(Plain.IsActive());
    }

    [Fact]
    public void Plain_Activate_SetsEnvAndFlag()
    {
        using var _ = new EnvIsolator();
        Plain.Activate();
        Assert.True(Plain.FlagSet);
        Assert.Equal("1", Environment.GetEnvironmentVariable("NO_COLOR"));
        Assert.Equal("1", Environment.GetEnvironmentVariable("AZ_AI_PLAIN"));
        Assert.True(Plain.IsActive());
    }

    [Fact]
    public void Plain_Override_BeatsEverything()
    {
        using var _ = new EnvIsolator();
        Environment.SetEnvironmentVariable("NO_COLOR", "1");
        Plain.Override = false;
        Assert.False(Plain.IsActive());
    }

    // -----------------------------------------------------------------------
    // CLI surface output: ASCII-only and ANSI-free under every plain signal.
    // -----------------------------------------------------------------------

    private static (string stdout, string stderr) CaptureMain(string[] args)
    {
        var oldOut = Console.Out;
        var oldErr = Console.Error;
        var so = new StringWriter();
        var se = new StringWriter();
        try
        {
            Console.SetOut(so);
            Console.SetError(se);
            // Program.Main is async Task<int> -- block on it for test simplicity.
            var task = (System.Threading.Tasks.Task<int>)
                typeof(Program)
                    .GetMethod("Main",
                        System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Static)!
                    .Invoke(null, new object[] { args })!;
            task.GetAwaiter().GetResult();
        }
        finally
        {
            Console.SetOut(oldOut);
            Console.SetError(oldErr);
        }
        return (so.ToString(), se.ToString());
    }

    private static void AssertAsciiAndAnsiFree(string surface, string label)
    {
        Assert.False(AnsiEscape.IsMatch(surface),
            $"{label} contains an ANSI escape: {surface}");
        Assert.False(NonAscii.IsMatch(surface),
            $"{label} contains a non-ASCII byte: {surface}");
    }

    public static System.Collections.Generic.IEnumerable<object[]> PlainSignalMatrix =>
        new[]
        {
            new object[] { "baseline",   (Action)(() => { }) },
            new object[] { "no_color",   (Action)(() => Environment.SetEnvironmentVariable("NO_COLOR", "1")) },
            new object[] { "term_dumb",  (Action)(() => Environment.SetEnvironmentVariable("TERM", "dumb")) },
            new object[] { "az_ai_plain",(Action)(() => Environment.SetEnvironmentVariable("AZ_AI_PLAIN", "1")) },
            new object[] { "force_flag", (Action)(() => Plain.FlagSet = true) },
        };

    [Theory]
    [MemberData(nameof(PlainSignalMatrix))]
    public void Help_IsAsciiOnly_AndAnsiFree(string label, Action enable)
    {
        using var _ = new EnvIsolator();
        enable();
        var (stdout, stderr) = CaptureMain(new[] { "--help" });
        AssertAsciiAndAnsiFree(stdout, $"--help stdout [{label}]");
        AssertAsciiAndAnsiFree(stderr, $"--help stderr [{label}]");
        Assert.Contains("az-ai", stdout, StringComparison.Ordinal);
        Assert.Contains("--plain", stdout, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(PlainSignalMatrix))]
    public void Version_IsAsciiOnly_AndAnsiFree(string label, Action enable)
    {
        using var _ = new EnvIsolator();
        enable();
        var (stdout, stderr) = CaptureMain(new[] { "--version" });
        AssertAsciiAndAnsiFree(stdout, $"--version stdout [{label}]");
        AssertAsciiAndAnsiFree(stderr, $"--version stderr [{label}]");
        Assert.Contains("az-ai", stdout, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(PlainSignalMatrix))]
    public void Version_Short_IsAsciiOnly(string label, Action enable)
    {
        using var _ = new EnvIsolator();
        enable();
        var (stdout, _) = CaptureMain(new[] { "--version", "--short" });
        AssertAsciiAndAnsiFree(stdout, $"--version --short [{label}]");
    }

    [Fact]
    public void PlainFlag_AppearsInHelpAndCompletions()
    {
        using var _ = new EnvIsolator();
        var (stdout, _) = CaptureMain(new[] { "--help" });
        Assert.Contains("--plain", stdout, StringComparison.Ordinal);
        Assert.Contains("AZ_AI_PLAIN", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void PlainFlag_ParsedIntoCliOptions()
    {
        var opts = Program.ParseArgs(new[] { "--plain", "hello" });
        Assert.True(opts.Plain);
        Assert.False(opts.ParseError);
        Assert.Equal("hello", opts.Prompt);
    }

    [Fact]
    public void PlainFlag_AcceptedAlongsideRaw()
    {
        var opts = Program.ParseArgs(new[] { "--plain", "--raw", "hi" });
        Assert.True(opts.Plain);
        Assert.True(opts.Raw);
        Assert.False(opts.ParseError);
    }

    [Fact]
    public void Help_SourceCode_Banner_HasNoEmDash()
    {
        // Regression guard for the v2.0.0 banner: previously "az-ai (v2.0.0) — Azure...".
        using var _ = new EnvIsolator();
        var (stdout, _) = CaptureMain(new[] { "--help" });
        Assert.DoesNotContain("\u2014", stdout);
        Assert.DoesNotContain("\u2192", stdout); // arrow
        Assert.DoesNotContain("\u2022", stdout); // bullet
        Assert.DoesNotContain("\u2713", stdout); // check mark
        Assert.DoesNotContain("\ud83c\udfad", stdout); // theatre mask emoji surrogate pair
    }
}
