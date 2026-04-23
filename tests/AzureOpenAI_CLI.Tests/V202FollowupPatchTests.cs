using System.Text;
using AzureOpenAI_CLI.V2.Tests;
using AzureOpenAI_CLI;
using AzureOpenAI_CLI.Squad;
using AzureOpenAI_CLI.Tools;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// 2.0.2 hardening follow-ups flagged during 2.0.1 review.
///
/// Each task asserts both the positive AND the negative path
/// (pass-the-pass, fail-the-fail):
///
///   • F-5 sibling — AZURE_TIMEOUT env-var bounds (1..3600)
///   • K-5 sibling — PersonaMemory.LogDecision 32 KB cap + rotation
///   • K-1 sibling — ShellExec firstToken split tolerates leading tab/newline
///                   on the fast path (defense-in-depth over the segment rescan)
/// </summary>
[Collection(SafetyPatchCollection.Name)]
public class V202FollowupPatchTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _squadDir;
    private readonly string _historyDir;

    public V202FollowupPatchTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "v202-" + Guid.NewGuid());
        _squadDir = Path.Combine(_tempDir, ".squad");
        _historyDir = Path.Combine(_squadDir, "history");
        Directory.CreateDirectory(_historyDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }

    // ══════════════════════════════════════════════════════════════════
    // F-5 sibling — AZURE_TIMEOUT env-var validation
    // ══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("1")]
    [InlineData("60")]
    [InlineData("3600")]
    public void F5Sibling_EnvVar_AzureTimeoutInRange_Accepted(string value)
    {
        var prev = Environment.GetEnvironmentVariable("AZURE_TIMEOUT");
        Environment.SetEnvironmentVariable("AZURE_TIMEOUT", value);
        try
        {
            var opts = Program.ParseArgs(["prompt"]);
            Assert.False(opts.ParseError,
                $"AZURE_TIMEOUT={value} must be accepted but parser flagged: parseError=true");
            Assert.Equal(int.Parse(value), opts.TimeoutSeconds);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZURE_TIMEOUT", prev);
        }
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("3601")]
    [InlineData("99999")]
    [InlineData("1.5")]
    public void F5Sibling_EnvVar_AzureTimeoutInvalid_Rejected(string value)
    {
        var prev = Environment.GetEnvironmentVariable("AZURE_TIMEOUT");
        Environment.SetEnvironmentVariable("AZURE_TIMEOUT", value);
        var oldErr = Console.Error;
        using var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            var opts = Program.ParseArgs(["prompt"]);
            Assert.True(opts.ParseError,
                $"AZURE_TIMEOUT={value} must be rejected but parser accepted it");
            Assert.Equal(1, opts.ParseErrorExitCode);
            var err = sw.ToString();
            Assert.Contains("AZURE_TIMEOUT", err);
            Assert.Contains("1-3600", err);
        }
        finally
        {
            Console.SetError(oldErr);
            Environment.SetEnvironmentVariable("AZURE_TIMEOUT", prev);
        }
    }

    [Fact]
    public void F5Sibling_EnvVar_AzureTimeoutEmpty_UsesDefault()
    {
        // Empty/whitespace env var must fall through to the default timeout,
        // not trigger validation — matches the existing AZURE_MAX_TOKENS /
        // AZURE_TEMPERATURE empty-string behaviour.
        var prev = Environment.GetEnvironmentVariable("AZURE_TIMEOUT");
        Environment.SetEnvironmentVariable("AZURE_TIMEOUT", "");
        try
        {
            var opts = Program.ParseArgs(["prompt"]);
            Assert.False(opts.ParseError);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZURE_TIMEOUT", prev);
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // F-5 flag parity — --timeout flag bounds match AZURE_TIMEOUT env
    // ══════════════════════════════════════════════════════════════════
    //
    // Prior to 2.0.2 the flag only checked `int.TryParse` and would accept
    // 0, -5, 3601, 99999 — bypassing the bounds AZURE_TIMEOUT just learned.
    // Closing that gap so CLI flag and env-var validation stay in lockstep.

    [Theory]
    [InlineData("1")]
    [InlineData("60")]
    [InlineData("3600")]
    public void F5Flag_TimeoutInRange_Accepted(string value)
    {
        var prev = Environment.GetEnvironmentVariable("AZURE_TIMEOUT");
        Environment.SetEnvironmentVariable("AZURE_TIMEOUT", null);
        try
        {
            var opts = Program.ParseArgs(["--timeout", value, "prompt"]);
            Assert.False(opts.ParseError,
                $"--timeout {value} must be accepted but parser flagged: parseError=true");
            Assert.Equal(int.Parse(value), opts.TimeoutSeconds);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZURE_TIMEOUT", prev);
        }
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("3601")]
    [InlineData("99999")]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("")]
    public void F5Flag_TimeoutInvalid_Rejected(string value)
    {
        var prev = Environment.GetEnvironmentVariable("AZURE_TIMEOUT");
        Environment.SetEnvironmentVariable("AZURE_TIMEOUT", null);
        var oldErr = Console.Error;
        using var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            var opts = Program.ParseArgs(["--timeout", value, "prompt"]);
            Assert.True(opts.ParseError,
                $"--timeout {value} must be rejected but parser accepted it");
            Assert.Equal(1, opts.ParseErrorExitCode);
            var err = sw.ToString();
            Assert.Contains("--timeout must be a positive integer seconds value (1-3600)", err);
        }
        finally
        {
            Console.SetError(oldErr);
            Environment.SetEnvironmentVariable("AZURE_TIMEOUT", prev);
        }
    }



    // ══════════════════════════════════════════════════════════════════
    // K-5 sibling — PersonaMemory.LogDecision 32 KB cap + rotation
    // ══════════════════════════════════════════════════════════════════

    private const int MaxHistoryBytes = 32 * 1024;
    private const string DecisionsTruncationMarker = "...(earlier decisions truncated)...\n";

    [Fact]
    public void K5Sibling_LogDecision_BelowCap_UnchangedBehaviour()
    {
        var memory = new PersonaMemory(_squadDir);
        memory.LogDecision("coder", "picked Option A for parser strategy");

        var path = Path.Combine(_squadDir, "decisions.md");
        Assert.True(File.Exists(path));
        Assert.False(File.Exists(path + ".old"),
            "below-cap LogDecision must not create a rotation file");
        var contents = File.ReadAllText(path);
        Assert.Contains("Option A", contents);
        Assert.Contains("coder", contents);
    }

    [Fact]
    public void K5Sibling_LogDecision_CrossesCap_RotatesToOldAndStartsFresh()
    {
        var path = Path.Combine(_squadDir, "decisions.md");
        Directory.CreateDirectory(_squadDir);
        // Seed close to cap so the next LogDecision pushes us over 32 KB.
        File.WriteAllText(path, new string('S', 33_000));

        var memory = new PersonaMemory(_squadDir);
        memory.LogDecision("coder", "TRIGGER-ROTATION-DECISION");

        var expectedOld = path + ".old";
        Assert.True(File.Exists(expectedOld),
            "decisions.md.old must exist after rotation");
        var oldBody = File.ReadAllText(expectedOld);
        Assert.Contains("SSSS", oldBody);
        Assert.DoesNotContain("TRIGGER-ROTATION-DECISION", oldBody);

        // Fresh file contains only the new entry.
        var freshBody = File.ReadAllText(path);
        Assert.Contains("TRIGGER-ROTATION-DECISION", freshBody);
        Assert.DoesNotContain("SSSS", freshBody);
        Assert.True(Encoding.UTF8.GetByteCount(freshBody) < MaxHistoryBytes,
            "fresh file must be well under cap after rotation");
    }

    [Fact]
    public void K5Sibling_LogDecision_TwoRotations_SecondOverwritesFirst()
    {
        var path = Path.Combine(_squadDir, "decisions.md");
        Directory.CreateDirectory(_squadDir);
        var memory = new PersonaMemory(_squadDir);

        // Rotation 1 — seed near cap of 'A', rotate with marker MARK-D1.
        File.WriteAllText(path, new string('A', 33_000));
        memory.LogDecision("coder", "MARK-D1-DECISION");
        var r1OldBody = File.ReadAllText(path + ".old");
        Assert.Contains("AAAA", r1OldBody);

        // Rotation 2 — seed near cap of 'B' over the fresh file, rotate again.
        File.WriteAllText(path, new string('B', 33_000));
        memory.LogDecision("coder", "MARK-D2-DECISION");

        var r2OldBody = File.ReadAllText(path + ".old");
        Assert.Contains("BBBB", r2OldBody);
        Assert.DoesNotContain("AAAA", r2OldBody);
        Assert.DoesNotContain("MARK-D1-DECISION", r2OldBody);
    }

    [Fact]
    public void K5Sibling_ReadDecisions_WithOldSibling_PrependsTruncationMarker()
    {
        // Fresh file under the cap + a .old sibling exists → ReadDecisions
        // must prepend the truncation marker so readers see the signal.
        var path = Path.Combine(_squadDir, "decisions.md");
        Directory.CreateDirectory(_squadDir);
        File.WriteAllText(path, "### recent decision entry\nbody body\n");
        File.WriteAllText(path + ".old", "### older rotated decisions\n");

        var memory = new PersonaMemory(_squadDir);
        var body = memory.ReadDecisions();

        Assert.Contains(DecisionsTruncationMarker.TrimEnd('\n'), body);
        Assert.Contains("recent decision entry", body);
    }

    [Fact]
    public void K5Sibling_ReadDecisions_NoOldSibling_NoTruncationMarker()
    {
        // Baseline: no .old, under cap → no truncation marker added.
        var path = Path.Combine(_squadDir, "decisions.md");
        Directory.CreateDirectory(_squadDir);
        File.WriteAllText(path, "### fresh only\n");

        var memory = new PersonaMemory(_squadDir);
        var body = memory.ReadDecisions();

        Assert.DoesNotContain("earlier decisions truncated", body);
        Assert.Contains("fresh only", body);
    }

    // ══════════════════════════════════════════════════════════════════
    // K-1 sibling — ShellExec firstToken leading whitespace (tab/newline)
    // ══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("\trm -rf /", "rm")]
    [InlineData("\n sudo whoami", "sudo")]
    [InlineData(" \trm test", "rm")]
    [InlineData("\t\nrm -rf /", "rm")]
    [InlineData("\n\tsudo ls", "sudo")]
    public async Task K1Sibling_ShellExec_LeadingWhitespace_FirstTokenBlocks(
        string command, string expectedBlockedToken)
    {
        var result = await ShellExecTool.ExecuteAsync(command, CancellationToken.None);
        Assert.StartsWith("Error:", result);
        Assert.Contains($"'{expectedBlockedToken}'", result);
    }

    [Fact]
    public async Task K1Sibling_ShellExec_BenignCommand_StillAllowed()
    {
        // Negative path: a safe command with leading whitespace must not
        // be spuriously blocked by the tightened first-token split.
        var result = await ShellExecTool.ExecuteAsync("\techo hello", CancellationToken.None);
        Assert.DoesNotContain("is blocked for safety", result);
    }

    [Fact]
    public async Task K1Sibling_ShellExec_TabSeparatorBaseline_StillBlocks()
    {
        // Confirm the K-1 (2.0.1) tab-rescan path still fires — belt and
        // suspenders with the first-token tightening above.
        var result = await ShellExecTool.ExecuteAsync("ls\trm -rf /", CancellationToken.None);
        Assert.StartsWith("Error:", result);
        Assert.Contains("'rm'", result);
    }
}
