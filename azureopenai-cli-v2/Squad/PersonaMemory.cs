using System.Text;

namespace AzureOpenAI_CLI_V2.Squad;

/// <summary>
/// Manages persistent memory for each persona.
/// History is stored in .squad/history/{name}.md — one file per persona.
/// Each session appends learnings. Next session reads the full history.
///
/// NOTE: MAF v1.1.0 provides ChatHistoryMemoryProvider which uses vector stores.
/// This implementation preserves the v1 file-based contract for backward compatibility
/// with existing .squad/ directories. Users' existing persona histories must work unchanged.
///
/// Hardening (FDR chaos drill F1/F2/F3 — closed pre-v2.0.0 cutover):
///   F1 — ReadHistory streams the tail via FileStream.Seek instead of loading the whole
///        file into memory. A 100 MB history file now allocates ≤ 32 KB on the read path.
///   F2 — ReadHistory refuses non-regular files (character/block devices, pipes,
///        non-seekable streams) up front, and belt-and-suspenders: wraps the tail read
///        in a 5-second cancellation token. /dev/urandom is no longer a hang vector.
///   F3 — All entry points taking a persona name route through SanitizePersonaName,
///        which rejects anything outside [a-z0-9_-]{1,64}. Path traversal
///        (../../canary, /etc/passwd, nul bytes, over-length) throws ArgumentException.
/// </summary>
internal sealed class PersonaMemory
{
    private const string SquadDir = ".squad";
    private const string HistoryDir = "history";
    private const string DecisionsFile = "decisions.md";
    private const int MaxHistoryBytes = 32_768;          // 32 KB tail window per persona
    private const int MaxPersonaNameLength = 64;
    private const int ReadTimeoutSeconds = 5;

    // Pre-truncation header — kept verbatim for backward compat with existing tests
    // that assert on this exact prefix.
    private const string TruncationMarker = "...(earlier history truncated)...\n";
    private const string DecisionsTruncationMarker = "...(earlier decisions truncated)...\n";

    private readonly string _baseDir;

    public PersonaMemory(string? baseDir = null)
    {
        _baseDir = baseDir ?? Path.Combine(Directory.GetCurrentDirectory(), SquadDir);
    }

    /// <summary>
    /// Validate + normalise a persona name.
    /// Allows ONLY [a-z0-9_-] after ToLowerInvariant(), max 64 chars, non-empty after trim.
    /// Rejects path separators, traversal sequences, control/null bytes, non-ASCII.
    /// Throws ArgumentException with a user-facing message on invalid input.
    /// </summary>
    public static string SanitizePersonaName(string? name)
    {
        // K-4 (2.0.1): error message format is "invalid persona name: '<value>'".
        // Existing callers still assert `Assert.Contains("invalid persona name", ex.Message)`
        // so the substring contract is preserved.
        static string Msg(string? raw) => $"invalid persona name: '{raw ?? "<null>"}'";

        if (name is null)
            throw new ArgumentException(Msg(name), nameof(name));

        var trimmed = name.Trim();
        if (trimmed.Length == 0 || trimmed.Length > MaxPersonaNameLength)
            throw new ArgumentException(Msg(name), nameof(name));

        // Regex contract: ^[A-Za-z0-9_-]{1,64}$ (spec). We accept mixed-case on
        // input and normalise via ToLowerInvariant so the on-disk path is
        // deterministic and case-insensitive across platforms.
        var lowered = trimmed.ToLowerInvariant();
        foreach (var c in lowered)
        {
            bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-';
            if (!ok)
                throw new ArgumentException(Msg(name), nameof(name));
        }
        return lowered;
    }

    /// <summary>
    /// Read the accumulated history for a persona.
    /// Returns empty string if no history exists, the path points to a non-regular
    /// file, the stream is non-seekable, or the read times out.
    /// </summary>
    public string ReadHistory(string personaName)
    {
        var safeName = SanitizePersonaName(personaName);
        var path = GetHistoryPathForSafeName(safeName);
        if (!File.Exists(path))
            return "";

        // F2: refuse anything that isn't a regular file under _historyDir.
        // Two complementary checks:
        //   (a) FileAttributes.Device — catches character/block devices on
        //       platforms where .NET reports it (reliable on Windows;
        //       sometimes reliable on Unix).
        //   (b) Symlink-target canonicalization — catches the common attack
        //       vector: .squad/history/rogue.md symlinked to /dev/urandom,
        //       /etc/passwd, or anywhere else. If the resolved target isn't
        //       under our history directory, refuse. This is the fast-path
        //       that keeps /dev/urandom from ever being opened.
        FileAttributes attrs;
        try
        {
            attrs = File.GetAttributes(path);
        }
        catch (Exception ex)
        {
            LogNonFatal($"[persona] history file {safeName} is not readable ({ex.GetType().Name}) — skipping");
            return "";
        }

        if ((attrs & FileAttributes.Device) != 0 || (attrs & FileAttributes.Directory) != 0)
        {
            LogNonFatal($"[persona] history file {safeName} is not a regular file — skipping");
            return "";
        }

        if (IsSymlinkEscape(path, out var resolvedTarget))
        {
            LogNonFatal($"[persona] history file {safeName} resolves outside history dir ({resolvedTarget}) — skipping");
            return "";
        }

        try
        {
            using var fs = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read);

            if (!fs.CanSeek)
            {
                LogNonFatal($"[persona] history file {safeName} is not a regular file — skipping");
                return "";
            }

            long len;
            try { len = fs.Length; }
            catch (NotSupportedException)
            {
                LogNonFatal($"[persona] history file {safeName} is not a regular file — skipping");
                return "";
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(ReadTimeoutSeconds));
            try
            {
                var body = ReadSeekableTail(fs, len, MaxHistoryBytes, cts.Token);
                // K-5 (2.0.1): surface the truncation marker when a rotated
                // <name>.md.old sibling exists, so readers still see the
                // "there was earlier history" signal the pre-rotation,
                // read-side truncation path provided.
                if (!body.StartsWith(TruncationMarker) && File.Exists(path + ".old"))
                    body = TruncationMarker + body;
                return body;
            }
            catch (OperationCanceledException)
            {
                LogNonFatal($"[persona] history file {safeName} read timed out after {ReadTimeoutSeconds}s — skipping");
                return "";
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            LogNonFatal($"[persona] history file {safeName} is not readable ({ex.GetType().Name}) — skipping");
            return "";
        }
        catch (IOException ex)
        {
            LogNonFatal($"[persona] history file {safeName} is not readable ({ex.GetType().Name}) — skipping");
            return "";
        }
    }

    /// <summary>
    /// Stream the last <paramref name="maxBytes"/> bytes out of a seekable stream
    /// of known <paramref name="totalLength"/>, honoring a cancellation token.
    /// Extracted into a testable helper so the 5 s cancellation path can be
    /// exercised without relying on platform-specific blocking device files.
    /// </summary>
    internal static string ReadSeekableTail(Stream stream, long totalLength, int maxBytes, CancellationToken ct)
    {
        bool truncated = totalLength > maxBytes;
        long seekOffset = truncated ? totalLength - maxBytes : 0;
        if (seekOffset > 0 && stream.CanSeek)
            stream.Seek(seekOffset, SeekOrigin.Begin);

        if (truncated)
            SkipUtf8ContinuationBytes(stream);

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
        string body = reader.ReadToEndAsync(ct).GetAwaiter().GetResult();

        if (body.Length > maxBytes)
            body = body[^maxBytes..];

        return truncated ? TruncationMarker + body : body;
    }

    /// <summary>
    /// Returns true if <paramref name="path"/> is a symlink whose final target
    /// resolves outside our history directory. Out-parameter carries the
    /// resolved target for diagnostic logging.
    /// </summary>
    private bool IsSymlinkEscape(string path, out string resolvedTarget)
    {
        resolvedTarget = path;
        try
        {
            var fi = new FileInfo(path);
            var target = fi.ResolveLinkTarget(returnFinalTarget: true);
            if (target == null) return false;   // not a symlink

            resolvedTarget = target.FullName;
            var expectedRoot = Path.GetFullPath(Path.Combine(_baseDir, HistoryDir));
            var sep = Path.DirectorySeparatorChar;
            var rootWithSep = expectedRoot.EndsWith(sep) ? expectedRoot : expectedRoot + sep;
            return !resolvedTarget.StartsWith(rootWithSep, StringComparison.Ordinal);
        }
        catch
        {
            // Couldn't resolve — err on the side of refusing.
            return true;
        }
    }

    /// <summary>
    /// Append a session entry to persona history.
    ///
    /// K-5 (2.0.1): enforces the 32 KB per-persona cap atomically.
    /// If current-size + new-entry-size would exceed <see cref="MaxHistoryBytes"/>,
    /// the existing file is rotated to <c>&lt;name&gt;.md.old</c> (overwriting
    /// any previous rotation) and a fresh file is started with only the new
    /// entry. All I/O uses <see cref="FileShare.Read"/> so concurrent readers
    /// (e.g. espanso, another squad process) don't corrupt the rotation.
    /// </summary>
    public void AppendHistory(string personaName, string task, string summary)
    {
        var safeName = SanitizePersonaName(personaName);
        var path = GetHistoryPathForSafeName(safeName);
        var dir = Path.GetDirectoryName(path);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var entry = $"\n## Session — {DateTime.UtcNow:yyyy-MM-dd HH:mm UTC}\n" +
                    $"**Task:** {Truncate(task, 200)}\n" +
                    $"**Result:** {Truncate(summary, 500)}\n";
        var entryBytes = Encoding.UTF8.GetBytes(entry);

        long currentSize = 0;
        if (File.Exists(path))
        {
            try { currentSize = new FileInfo(path).Length; }
            catch { currentSize = 0; }
        }

        bool rotate = currentSize + entryBytes.LongLength > MaxHistoryBytes;
        if (rotate)
        {
            var oldPath = path + ".old";
            try
            {
                // File.Move(overwrite: true) is atomic on the same filesystem
                // and overwrites any pre-existing .md.old from a prior rotation.
                File.Move(path, oldPath, overwrite: true);
            }
            catch (FileNotFoundException) { /* racy delete — nothing to move */ }

            // Fresh file starts with only the new entry.
            using var fresh = new FileStream(
                path, FileMode.Create, FileAccess.Write, FileShare.Read);
            fresh.Write(entryBytes, 0, entryBytes.Length);
        }
        else
        {
            using var fs = new FileStream(
                path, FileMode.Append, FileAccess.Write, FileShare.Read);
            fs.Write(entryBytes, 0, entryBytes.Length);
        }
    }

    /// <summary>
    /// Log a decision to the shared decisions file.
    /// </summary>
    public void LogDecision(string personaName, string decision)
    {
        // Sanitize for the log line even though this file is shared (not per-persona
        // path) — keeps attacker-controlled strings out of the on-disk log.
        var safeName = SanitizePersonaName(personaName);
        var path = Path.Combine(_baseDir, DecisionsFile);
        if (!Directory.Exists(_baseDir))
            Directory.CreateDirectory(_baseDir);

        var entry = $"\n### {DateTime.UtcNow:yyyy-MM-dd HH:mm UTC} — {safeName}\n{decision}\n";
        File.AppendAllText(path, entry);
    }

    /// <summary>
    /// Read shared decisions log. Same hardening as ReadHistory (F1/F2) since the
    /// decisions file is on-disk user-writable too.
    /// </summary>
    public string ReadDecisions()
    {
        var path = Path.Combine(_baseDir, DecisionsFile);
        if (!File.Exists(path))
            return "";

        FileAttributes attrs;
        try { attrs = File.GetAttributes(path); }
        catch { return ""; }

        if ((attrs & FileAttributes.Device) != 0 || (attrs & FileAttributes.Directory) != 0)
        {
            LogNonFatal("[persona] decisions file is not a regular file — skipping");
            return "";
        }

        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (!fs.CanSeek) return "";

            long len = fs.Length;
            bool truncated = len > MaxHistoryBytes;
            long seekOffset = truncated ? len - MaxHistoryBytes : 0;
            if (seekOffset > 0)
                fs.Seek(seekOffset, SeekOrigin.Begin);
            if (truncated)
                SkipUtf8ContinuationBytes(fs);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(ReadTimeoutSeconds));
            using var reader = new StreamReader(fs, Encoding.UTF8, false, 4096, leaveOpen: true);
            string body;
            try { body = reader.ReadToEndAsync(cts.Token).GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { return ""; }

            if (body.Length > MaxHistoryBytes)
                body = body[^MaxHistoryBytes..];

            return truncated ? DecisionsTruncationMarker + body : body;
        }
        catch (IOException) { return ""; }
    }

    /// <summary>
    /// Check if the .squad directory exists (has been initialized).
    /// </summary>
    public bool IsInitialized() => Directory.Exists(_baseDir);

    /// <summary>
    /// Initialize the .squad directory structure.
    /// </summary>
    public void Initialize()
    {
        Directory.CreateDirectory(_baseDir);
        Directory.CreateDirectory(Path.Combine(_baseDir, HistoryDir));

        var decisionsPath = Path.Combine(_baseDir, DecisionsFile);
        if (!File.Exists(decisionsPath))
            File.WriteAllText(decisionsPath, "# Squad Decisions\n\nShared decision log across all personas.\n");
    }

    /// <summary>
    /// Public so SquadCoordinator (and callers in Program.cs) can compose the
    /// same sanitized path the read/append methods use, without duplicating
    /// validation logic.
    /// </summary>
    internal string GetHistoryPath(string personaName)
    {
        var safe = SanitizePersonaName(personaName);
        return GetHistoryPathForSafeName(safe);
    }

    private string GetHistoryPathForSafeName(string safeName)
    {
        // K-4 belt-and-suspenders: SanitizePersonaName has already rejected any
        // path-separator/traversal input, but run the name through
        // Path.GetFileName one more time so even if a future refactor loosens
        // the regex, we can never compose a path that escapes the history dir.
        var fileOnly = Path.GetFileName(safeName);
        if (fileOnly != safeName)
            throw new ArgumentException($"invalid persona name: '{safeName}'", nameof(safeName));
        return Path.Combine(_baseDir, HistoryDir, $"{fileOnly}.md");
    }

    /// <summary>
    /// Advance the stream past any UTF-8 continuation bytes (0x80–0xBF) so the
    /// StreamReader starts on a valid UTF-8 lead byte. Bounded to 3 skips —
    /// UTF-8 sequences are at most 4 bytes so 3 continuations is the max we'd
    /// ever have to discard.
    /// </summary>
    private static void SkipUtf8ContinuationBytes(Stream fs)
    {
        for (int i = 0; i < 3; i++)
        {
            long pos = fs.Position;
            int b = fs.ReadByte();
            if (b < 0) return;                  // EOF
            if ((b & 0xC0) != 0x80)             // not a continuation byte → we're aligned
            {
                fs.Seek(pos, SeekOrigin.Begin); // unread it
                return;
            }
            // else: loop — skip this continuation byte
        }
    }

    /// <summary>
    /// Emit a non-fatal advisory to stderr. Honors raw-mode by checking the
    /// AZUREOPENAI_CLI_RAW env var (Program.cs sets this when --raw is passed
    /// — see FR-014 plumbing). Non-raw is the default, so unit tests see the
    /// message unless they explicitly set the env var.
    /// </summary>
    private static void LogNonFatal(string message)
    {
        var raw = Environment.GetEnvironmentVariable("AZUREOPENAI_CLI_RAW");
        if (!string.IsNullOrEmpty(raw) && raw != "0") return;
        try { Console.Error.WriteLine(message); }
        catch { /* best-effort */ }
    }

    private static string Truncate(string text, int maxLen) =>
        text.Length <= maxLen ? text : text[..maxLen] + "...";
}
