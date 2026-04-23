using System.Text;
using System.Text.Json;
using AzureOpenAI_CLI.V2.Tests;
using AzureOpenAI_CLI;
using AzureOpenAI_CLI.Squad;
using AzureOpenAI_CLI.Tools;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// 2.0.1 safety patches — K-1 / K-4 / K-5 / K-6 / F-4 / F-5.
///
/// One file per patch section, keeps the diff reviewable and makes it obvious
/// which task a test belongs to. Every task asserts both the positive path
/// AND the negative path (pass-the-pass, fail-the-fail).
/// </summary>
[Collection(SafetyPatchCollection.Name)]
public class V201SafetyPatchTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _squadDir;
    private readonly string _historyDir;

    public V201SafetyPatchTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "v201-" + Guid.NewGuid());
        _squadDir = Path.Combine(_tempDir, ".squad");
        _historyDir = Path.Combine(_squadDir, "history");
        Directory.CreateDirectory(_historyDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }

    // ══════════════════════════════════════════════════════════════════
    // K-1 — ShellExec tab separator re-scan
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task K1_ShellExec_RejectsTabSeparatedRmRf()
    {
        var result = await ShellExecTool.ExecuteAsync("ls\trm -rf /", CancellationToken.None);
        Assert.StartsWith("Error:", result);
        Assert.Contains("'rm'", result);
    }

    [Fact]
    public async Task K1_ShellExec_RejectsSemicolonSeparatedRmRf_BaselineUnchanged()
    {
        // Baseline — the existing separator (;) must keep working.
        var result = await ShellExecTool.ExecuteAsync("ls ; rm -rf /", CancellationToken.None);
        Assert.StartsWith("Error:", result);
        Assert.Contains("'rm'", result);
    }

    [Fact]
    public async Task K1_ShellExec_RejectsTabSeparatedSudo()
    {
        var result = await ShellExecTool.ExecuteAsync("echo hi\tsudo whoami", CancellationToken.None);
        Assert.StartsWith("Error:", result);
        Assert.Contains("'sudo'", result);
    }

    [Fact]
    public async Task K1_ShellExec_BenignTabs_StillWork()
    {
        // Tabs inside args to a single harmless command must NOT block.
        var result = await ShellExecTool.ExecuteAsync("echo a\tb", CancellationToken.None);
        Assert.DoesNotContain("Error:", result);
    }

    // ══════════════════════════════════════════════════════════════════
    // K-4 — PersonaMemory name regex + traversal
    // ══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("coder")]
    [InlineData("code_reviewer")]
    [InlineData("sec-ops")]
    [InlineData("A1")]
    public void K4_Sanitize_AcceptsValidNames(string name)
    {
        // The function normalises case, but must not throw.
        var result = PersonaMemory.SanitizePersonaName(name);
        Assert.Equal(name.ToLowerInvariant(), result);
    }

    [Fact]
    public void K4_Sanitize_Accepts64CharMax()
    {
        var atLimit = new string('a', 64);
        Assert.Equal(atLimit, PersonaMemory.SanitizePersonaName(atLimit));
    }

    [Theory]
    [InlineData("../etc/passwd")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("foo bar")]
    [InlineData(".hidden")]
    [InlineData("foo/bar")]
    [InlineData("🎉emoji")]
    public void K4_Sanitize_RejectsInvalid(string name)
    {
        var ex = Assert.Throws<ArgumentException>(() => PersonaMemory.SanitizePersonaName(name));
        Assert.Contains("invalid persona name", ex.Message);
        // New format contract: "invalid persona name: '<value>'".
        Assert.Contains(":", ex.Message);
    }

    [Fact]
    public void K4_Sanitize_Rejects65Chars()
    {
        var tooLong = new string('a', 65);
        Assert.Throws<ArgumentException>(() => PersonaMemory.SanitizePersonaName(tooLong));
    }

    [Fact]
    public void K4_ErrorMessage_IncludesOffendingValueInQuotes()
    {
        var ex = Assert.Throws<ArgumentException>(() => PersonaMemory.SanitizePersonaName("foo bar"));
        Assert.Contains("'foo bar'", ex.Message);
    }

    // ══════════════════════════════════════════════════════════════════
    // K-5 — PersonaMemory 32 KB cap with rotation
    // ══════════════════════════════════════════════════════════════════

    private const int MaxHistoryBytes = 32 * 1024;

    [Fact]
    public void K5_Append_BelowCap_UnchangedBehaviour()
    {
        var memory = new PersonaMemory(_squadDir);
        memory.AppendHistory("coder", "small task", "small summary");

        var path = Path.Combine(_historyDir, "coder.md");
        Assert.True(File.Exists(path));
        Assert.False(File.Exists(path + ".old"),
            "below-cap append must not create a rotation file");
        var contents = File.ReadAllText(path);
        Assert.Contains("small task", contents);
    }

    [Fact]
    public void K5_Append_CrossesCap_RotatesToOldAndStartsFresh()
    {
        var path = Path.Combine(_historyDir, "coder.md");
        // Seed close to cap so the next append pushes us over 32 KB.
        File.WriteAllText(path, new string('S', 33_000));

        var memory = new PersonaMemory(_squadDir);
        memory.AppendHistory("coder", "trigger-rotation-task", "trigger-rotation-summary");

        var expectedOld = path + ".old";
        Assert.True(File.Exists(expectedOld), ".md.old must exist after rotation");
        var oldBody = File.ReadAllText(expectedOld);
        Assert.Contains("SSSS", oldBody);
        Assert.DoesNotContain("trigger-rotation-task", oldBody);

        // Fresh file contains only the new entry.
        var freshBody = File.ReadAllText(path);
        Assert.Contains("trigger-rotation-task", freshBody);
        Assert.DoesNotContain("SSSS", freshBody);
        Assert.True(Encoding.UTF8.GetByteCount(freshBody) < MaxHistoryBytes,
            "fresh file must be well under cap after rotation");
    }

    [Fact]
    public void K5_Append_TwoRotations_SecondOverwritesFirst()
    {
        var path = Path.Combine(_historyDir, "coder.md");
        var memory = new PersonaMemory(_squadDir);

        // Rotation 1 — seed near cap of 'A', rotate with marker MARK-R1-TASK.
        File.WriteAllText(path, new string('A', 33_000));
        memory.AppendHistory("coder", "MARK-R1-TASK", "r1");
        var r1OldBody = File.ReadAllText(path + ".old");
        Assert.Contains("AAAA", r1OldBody);

        // Rotation 2 — seed near cap of 'B' over the fresh file, rotate again.
        File.WriteAllText(path, new string('B', 33_000));
        memory.AppendHistory("coder", "MARK-R2-TASK", "r2");

        var r2OldBody = File.ReadAllText(path + ".old");
        Assert.Contains("BBBB", r2OldBody);
        Assert.DoesNotContain("AAAA", r2OldBody);
        Assert.DoesNotContain("MARK-R1-TASK", r2OldBody);
    }

    // ══════════════════════════════════════════════════════════════════
    // K-6 / F-4 — SquadConfig size + depth guards
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void K6_Load_HalfMegabyte_Succeeds()
    {
        // Valid small config padded with a big description string up to ~0.5 MB.
        var bigDesc = new string('x', 500_000);
        var json = "{\"team\":{\"name\":\"t\",\"description\":\"" + bigDesc + "\"}," +
                   "\"personas\":[],\"routing\":[]}";
        var path = Path.Combine(_tempDir, ".squad.json");
        File.WriteAllText(path, json);

        var config = SquadConfig.Load(_tempDir);
        Assert.NotNull(config);
        Assert.Equal("t", config!.Team.Name);
    }

    [Fact]
    public void K6_Load_Over1Mb_Throws()
    {
        var tooBig = new string('x', 1_500_000);
        var json = "{\"team\":{\"name\":\"t\",\"description\":\"" + tooBig + "\"}}";
        var path = Path.Combine(_tempDir, ".squad.json");
        File.WriteAllText(path, json);

        var ex = Assert.Throws<InvalidOperationException>(() => SquadConfig.Load(_tempDir));
        Assert.Contains("1 MB", ex.Message);
    }

    [Fact]
    public void F4_Load_DeeplyNestedJson_ThrowsJsonException()
    {
        // SquadConfig doesn't natively accept nested objects, but JsonExtensionData
        // isn't configured so we'll get caught on the Depth=32 limiter before any
        // binding happens. Build a 40-deep array-of-arrays payload.
        var sb = new StringBuilder();
        sb.Append("{\"team\":{\"name\":\"t\"},\"personas\":");
        for (int i = 0; i < 40; i++) sb.Append('[');
        sb.Append("null");
        for (int i = 0; i < 40; i++) sb.Append(']');
        sb.Append('}');
        var path = Path.Combine(_tempDir, ".squad.json");
        File.WriteAllText(path, sb.ToString());

        Assert.Throws<JsonException>(() => SquadConfig.Load(_tempDir));
    }

    // ══════════════════════════════════════════════════════════════════
    // F-5 — arg-parse bounds for --max-tokens / --temperature
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void F5_MaxTokens_Positive_Accepted()
    {
        var opts = Program.ParseArgs(["--max-tokens", "1000", "prompt"]);
        Assert.False(opts.ParseError);
        Assert.Equal(1000, opts.MaxTokens);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("abc")]
    public void F5_MaxTokens_Invalid_Rejected(string value)
    {
        var oldErr = Console.Error;
        using var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            var opts = Program.ParseArgs(["--max-tokens", value, "prompt"]);
            Assert.True(opts.ParseError);
            Assert.Equal(1, opts.ParseErrorExitCode);
            Assert.Contains("--max-tokens", sw.ToString());
            Assert.Contains("positive integer", sw.ToString());
        }
        finally { Console.SetError(oldErr); }
    }

    [Theory]
    [InlineData("0.0")]
    [InlineData("1.5")]
    [InlineData("2.0")]
    public void F5_Temperature_InRange_Accepted(string value)
    {
        var opts = Program.ParseArgs(["--temperature", value, "prompt"]);
        Assert.False(opts.ParseError);
    }

    [Theory]
    [InlineData("-0.1")]
    [InlineData("2.5")]
    [InlineData("foo")]
    public void F5_Temperature_Invalid_Rejected(string value)
    {
        var oldErr = Console.Error;
        using var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            var opts = Program.ParseArgs(["--temperature", value, "prompt"]);
            Assert.True(opts.ParseError);
            Assert.Equal(1, opts.ParseErrorExitCode);
            Assert.Contains("--temperature", sw.ToString());
            Assert.Contains("0.0 and 2.0", sw.ToString());
        }
        finally { Console.SetError(oldErr); }
    }

    [Fact]
    public void F5_EnvVar_AzureMaxTokensInvalid_Rejected()
    {
        var prev = Environment.GetEnvironmentVariable("AZURE_MAX_TOKENS");
        Environment.SetEnvironmentVariable("AZURE_MAX_TOKENS", "-3");
        var oldErr = Console.Error;
        using var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            var opts = Program.ParseArgs(["prompt"]);
            Assert.True(opts.ParseError);
            Assert.Contains("positive integer", sw.ToString());
        }
        finally
        {
            Console.SetError(oldErr);
            Environment.SetEnvironmentVariable("AZURE_MAX_TOKENS", prev);
        }
    }

    [Fact]
    public void F5_EnvVar_AzureTemperatureOutOfRange_Rejected()
    {
        var prev = Environment.GetEnvironmentVariable("AZURE_TEMPERATURE");
        Environment.SetEnvironmentVariable("AZURE_TEMPERATURE", "3.0");
        var oldErr = Console.Error;
        using var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            var opts = Program.ParseArgs(["prompt"]);
            Assert.True(opts.ParseError);
            Assert.Contains("0.0 and 2.0", sw.ToString());
        }
        finally
        {
            Console.SetError(oldErr);
            Environment.SetEnvironmentVariable("AZURE_TEMPERATURE", prev);
        }
    }
}
