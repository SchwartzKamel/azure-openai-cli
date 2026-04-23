using System.Text;
using AzureOpenAI_CLI.Squad;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// Adversarial regression tests for PersonaMemory, closing FDR's chaos-drill
/// findings F1 (RSS amplification), F2 (unbounded/device hangs), and F3
/// (persona-name path traversal). Every reproducer from
/// tests/chaos/11_persona_live.sh has a matching test here.
///
/// These tests run fully offline — no mock server, no real Azure endpoint —
/// and gate the v2.0.0 cutover per docs/chaos-drill-v2.md.
/// </summary>
public class PersonaMemoryHardeningTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _squadDir;
    private readonly string _historyDir;

    public PersonaMemoryHardeningTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "persona-hard-" + Guid.NewGuid());
        _squadDir = Path.Combine(_tempDir, ".squad");
        _historyDir = Path.Combine(_squadDir, "history");
        Directory.CreateDirectory(_historyDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }

    // ══════════════════════════════════════════════════════════════════
    // F1 — ReadHistory must not amplify file size into RSS.
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void F1_ReadHistory_100MbFile_ReturnsAtMost32Kb()
    {
        // Write a file that would have OOM'd the old File.ReadAllText path.
        // We use 8 MB instead of 100 MB here to keep CI fast, but the code
        // path is identical — the old bug scaled linearly with file size.
        // The chaos script 11a covers the 100 MB measurement end-to-end.
        var path = Path.Combine(_historyDir, "big.md");
        var oneKb = new string('X', 1024) + "\n";
        using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (var sw = new StreamWriter(fs, Encoding.UTF8))
        {
            // Write a tail marker last so we can confirm tail-seek behaviour.
            for (int i = 0; i < 8 * 1024; i++) sw.Write(oneKb);
            sw.Write("TAIL-MARKER\n");
        }

        var memory = new PersonaMemory(_squadDir);
        var history = memory.ReadHistory("big");

        // Truncation marker must be present.
        Assert.Contains("...(earlier history truncated)...", history);
        // Tail (most recent) must be preserved.
        Assert.Contains("TAIL-MARKER", history);
        // Returned string must not exceed the 32 KB tail + the truncation prefix.
        Assert.True(
            history.Length <= 32_768 + "...(earlier history truncated)...\n".Length,
            $"ReadHistory returned {history.Length} chars — expected ≤ 32 KB + marker");
    }

    [Fact]
    public void F1_ReadHistory_TailWindow_LandsOnUtf8Boundary()
    {
        // Build a file where the byte at position (len - 32 KB) lands in the
        // MIDDLE of a 3-byte UTF-8 sequence for "€" (0xE2 0x82 0xAC).
        // Before hardening, this would throw or produce a replacement char
        // at the head of the returned string; after, SkipUtf8ContinuationBytes
        // advances past the continuation bytes onto a clean lead byte.
        var path = Path.Combine(_historyDir, "utf8.md");

        // Prefix of N bytes of 'A', then a stream of "€" (3 bytes each),
        // then a clean ASCII tail. Choose N so seekOffset = len - 32768
        // lands on a continuation byte.
        var ascii = Encoding.UTF8.GetBytes(new string('A', 1000));
        var euro = Encoding.UTF8.GetBytes("€"); // {0xE2, 0x82, 0xAC}
        var tail = Encoding.UTF8.GetBytes("\nCLEAN-TAIL-ASCII\n");

        using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
        {
            fs.Write(ascii);
            // Enough euros to push total well past 32 KB — each = 3 bytes.
            for (int i = 0; i < 20_000; i++) fs.Write(euro);
            fs.Write(tail);
        }

        var memory = new PersonaMemory(_squadDir);
        var history = memory.ReadHistory("utf8");

        // Must not contain the Unicode replacement char at the head (that
        // would indicate we started reading mid-sequence).
        // The hardened path skips continuation bytes so the returned string
        // parses as valid UTF-8 from its first char.
        Assert.DoesNotContain("\uFFFD", history);
        Assert.Contains("CLEAN-TAIL-ASCII", history);
        Assert.Contains("...(earlier history truncated)...", history);
    }

    [Fact]
    public void F1_ReadHistory_SmallFile_ReadsEntireFileNoTruncationMarker()
    {
        // Files ≤ 32 KB must behave exactly as before — no truncation marker,
        // full content returned. Proves the hardening is additive, not a
        // behaviour regression for the happy path.
        var path = Path.Combine(_historyDir, "small.md");
        File.WriteAllText(path, "tiny history file\n");

        var memory = new PersonaMemory(_squadDir);
        var history = memory.ReadHistory("small");

        Assert.Equal("tiny history file\n", history);
        Assert.DoesNotContain("...(earlier history truncated)...", history);
    }

    // ══════════════════════════════════════════════════════════════════
    // F2 — ReadHistory must refuse unbounded / device / non-regular files.
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public void F2_ReadHistory_DevUrandomSymlink_RefusesWithinTimeout()
    {
        // Skip on non-Unix — /dev/urandom isn't available.
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            return;
        if (!File.Exists("/dev/urandom"))
            return;

        var link = Path.Combine(_historyDir, "rogue.md");
        File.CreateSymbolicLink(link, "/dev/urandom");

        var memory = new PersonaMemory(_squadDir);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var history = memory.ReadHistory("rogue");
        sw.Stop();

        // The symlink-target check refuses BEFORE we ever open the stream,
        // so this must complete essentially instantly.
        Assert.Equal("", history);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(1),
            $"ReadHistory on /dev/urandom symlink took {sw.Elapsed.TotalSeconds:F2}s — should refuse instantly via symlink-target check");
    }

    [Fact]
    public void F2_ReadSeekableTail_CancellationTokenFires_OnSlowStream()
    {
        // Unit-test the 5 s cancellation path directly on the extracted
        // ReadSeekableTail helper. Stream length < maxBytes so the skip-
        // continuation-bytes sync path doesn't run; we only exercise the
        // async StreamReader path which DOES honor the token.
        var slow = new SlowStream(delayMs: 60_000, length: 1024);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // TaskCanceledException derives from OperationCanceledException.
        Assert.ThrowsAny<OperationCanceledException>(() =>
            PersonaMemory.ReadSeekableTail(slow, slow.Length, 32_768, cts.Token));

        sw.Stop();
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2),
            $"ReadSeekableTail didn't honor cancellation — took {sw.Elapsed.TotalSeconds:F2}s");
    }

    /// <summary>
    /// A seekable Stream that blocks every ReadAsync until the token cancels.
    /// Used to verify cancellation-token plumbing through StreamReader.
    /// </summary>
    private sealed class SlowStream : Stream
    {
        private readonly int _delayMs;
        private long _position;
        public override long Length { get; }
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Position { get => _position; set => _position = value; }
        public SlowStream(int delayMs, long length) { _delayMs = delayMs; Length = length; }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin)
        {
            _position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => Length + offset,
                _ => _position,
            };
            return _position;
        }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) { Thread.Sleep(_delayMs); return 0; }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
            => Task.Delay(_delayMs, ct).ContinueWith(_ => 0, ct);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
            => new ValueTask<int>(Task.Delay(_delayMs, ct).ContinueWith(_ => 0, ct));
    }

    [Fact]
    public void F2_ReadHistory_RegularSmallFile_StillReadsCorrectly()
    {
        // Regression guard: all the F2 hardening must not touch the happy path.
        var path = Path.Combine(_historyDir, "regular.md");
        File.WriteAllText(path, "normal content\n");

        var memory = new PersonaMemory(_squadDir);
        Assert.Equal("normal content\n", memory.ReadHistory("regular"));
    }

    // ══════════════════════════════════════════════════════════════════
    // F3 — Persona-name sanitization.
    // ══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("../../canary")]
    [InlineData("..\\..\\canary")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\System32\\cmd")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("with space")]
    [InlineData("dot.name")]
    [InlineData("persona:admin")]
    [InlineData("persona;rm -rf /")]
    [InlineData("日本語")]
    public void F3_SanitizePersonaName_RejectsTraversalAndSpecialChars(string input)
    {
        var ex = Assert.Throws<ArgumentException>(() => PersonaMemory.SanitizePersonaName(input));
        Assert.Contains("invalid persona name", ex.Message);
    }

    [Fact]
    public void F3_SanitizePersonaName_RejectsNulByte()
    {
        Assert.Throws<ArgumentException>(() => PersonaMemory.SanitizePersonaName("nul\0byte"));
    }

    [Fact]
    public void F3_SanitizePersonaName_Rejects65CharName()
    {
        var tooLong = new string('a', 65);
        Assert.Throws<ArgumentException>(() => PersonaMemory.SanitizePersonaName(tooLong));
    }

    [Fact]
    public void F3_SanitizePersonaName_Accepts64CharName()
    {
        // Exactly at the boundary — must pass.
        var atLimit = new string('a', 64);
        Assert.Equal(atLimit, PersonaMemory.SanitizePersonaName(atLimit));
    }

    [Theory]
    [InlineData("coder")]
    [InlineData("reviewer")]
    [InlineData("architect")]
    [InlineData("writer")]
    [InlineData("security")]
    public void F3_SanitizePersonaName_AcceptsAllDefaultPersonas(string defaultName)
    {
        // This is the "don't break existing users" guard. If this test ever
        // goes red, the 5 scaffolded personas in SquadInitializer are broken.
        Assert.Equal(defaultName, PersonaMemory.SanitizePersonaName(defaultName));
    }

    [Fact]
    public void F3_SanitizePersonaName_UppercaseIsNormalized()
    {
        // Uppercase is accepted because we ToLowerInvariant() before
        // validation — the resulting path must be lowercase.
        Assert.Equal("coder", PersonaMemory.SanitizePersonaName("CODER"));
        Assert.Equal("coder", PersonaMemory.SanitizePersonaName("Coder"));
    }

    [Fact]
    public void F3_SanitizePersonaName_AcceptsHyphenUnderscoreDigits()
    {
        Assert.Equal("coder-2", PersonaMemory.SanitizePersonaName("coder-2"));
        Assert.Equal("code_r", PersonaMemory.SanitizePersonaName("code_r"));
        Assert.Equal("p1", PersonaMemory.SanitizePersonaName("p1"));
    }

    [Fact]
    public void F3_ReadHistory_TraversalNameThrows()
    {
        var memory = new PersonaMemory(_squadDir);
        Assert.Throws<ArgumentException>(() => memory.ReadHistory("../../canary"));
    }

    [Fact]
    public void F3_AppendHistory_TraversalNameThrows()
    {
        // Plant a canary that a successful traversal would clobber — proof
        // that the write side refuses before touching disk.
        var canary = Path.Combine(_tempDir, "canary.md");
        File.WriteAllText(canary, "ORIGINAL");

        var memory = new PersonaMemory(_squadDir);
        Assert.Throws<ArgumentException>(() =>
            memory.AppendHistory("../../canary", "task", "summary"));

        // Canary untouched.
        Assert.Equal("ORIGINAL", File.ReadAllText(canary));
    }

    [Fact]
    public void F3_LogDecision_TraversalNameThrows()
    {
        var memory = new PersonaMemory(_squadDir);
        Assert.Throws<ArgumentException>(() =>
            memory.LogDecision("../../attacker", "decision body"));
    }

    [Fact]
    public void F3_AppendThenRead_RoundTripSurvivesCasing()
    {
        // Sanitization normalises case on BOTH write and read, so a session
        // logged as "Coder" is retrievable as "coder" and vice-versa.
        var memory = new PersonaMemory(_squadDir);
        memory.AppendHistory("Coder", "Task", "wrote parser");
        var history = memory.ReadHistory("coder");
        Assert.Contains("wrote parser", history);
    }
}
