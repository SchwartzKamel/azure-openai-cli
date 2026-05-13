using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace AzureOpenAI_CLI.Registry;

/// <summary>
/// Typed parse / validation failure surface for <see cref="ModelRegistry.ReadCard"/>.
/// Wrapped to rc=99 + stderr by the outer caller; thrown directly by the
/// test-only <c>ReadCardOrThrow</c> helper so we don't need process isolation
/// to exercise the F-01 / F-03 / F-04 guards.
/// </summary>
internal sealed class ModelCardException : Exception
{
    public ModelCardException(string message) : base(message) { }
}

// S04E01 -- The Registry (Kramer). Static loader for ModelRegistryEntry[].
//
// Load order:
//   1. Embedded registry.json (seed -- always present).
//   2. ~/.config/az-ai/registry.json -- if present, REPLACES the seed list
//      (override semantics; no merge). Documented in ADR-012.
//
// Validation: every entry's capabilities must be a subset of
// ModelCapability.AllowedTags. An unknown tag causes rc=99 and an [ERROR]
// message naming both the tag and the entry. Missing cardPath is a WARN
// (to stderr unless isRaw) but never fatal.
//
// cardPath resolution: paths are relative to AppContext.BaseDirectory so
// the binary can run from any CWD and still resolve cards correctly
// (see brief Risk row 3 / ADR-012).
//
// AOT safety: DynamicDependency on Load() pins ModelRegistryEntry so the
// ILC linker cannot trim it even though the only reach is via JSON
// deserialization (brief Risk row 6).

/// <summary>
/// Static loader for the model registry. Call <see cref="Load"/> once at
/// startup; the result is stored in <c>Program.RegistryEntries</c>.
/// </summary>
internal static class ModelRegistry
{
    private const string EmbeddedResourceName =
        "AzureOpenAI_CLI.Registry.registry.json";

    /// <summary>
    /// Load and validate the registry. Reads the embedded seed first; if
    /// <c>~/.config/az-ai/registry.json</c> exists it replaces the seed
    /// list entirely (no merge). Unknown capability tags cause rc=99.
    /// </summary>
    // DynamicDependency: pins ModelRegistryEntry against ILC trimming.
    // Only constructors + properties are needed for JSON source-gen deserialization.
    // Using All here adds ~89 KB to the AOT binary; scoped access keeps the delta small.
    [DynamicDependency(
        DynamicallyAccessedMemberTypes.PublicConstructors
        | DynamicallyAccessedMemberTypes.PublicProperties,
        typeof(ModelRegistryEntry))]
    public static ModelRegistryEntry[] Load(bool isRaw = false)
    {
        var entries = LoadEmbedded();
        entries = ApplyUserOverride(entries, isRaw);
        ValidateEntries(entries, isRaw);
        return entries;
    }

    /// <summary>
    /// Returns the names of all registered model entries whose Capabilities
    /// array contains <paramref name="tag"/>. Returns empty array if no
    /// registry has been loaded or no entries match. Lookup is case-sensitive
    /// (capability tags are intentionally lowercase canonical, per ADR-012).
    /// </summary>
    /// <remarks>
    /// S04E03 -- consumed by CapabilityGate.Check to build rejection-message
    /// suggestion lists.
    /// </remarks>
    public static string[] ModelsWithCapability(string tag)
    {
        var entries = Program.RegistryEntries;
        if (entries is null || entries.Length == 0) return Array.Empty<string>();
        return entries
            .Where(e => e.Capabilities is not null && e.Capabilities.Contains(tag, StringComparer.Ordinal))
            .Select(e => e.Name)
            .ToArray();
    }

    /// <summary>
    /// Enumerates all loaded registry entries in their original registry-file
    /// order. Returns an empty list if no registry has been loaded. Stable
    /// across invocations -- the order matches the JSON source (embedded
    /// seed, or the user override that replaced it).
    /// </summary>
    /// <remarks>
    /// S04E04 -- consumed by <c>ModelsCommand</c> for <c>az-ai models list</c>
    /// and as the tie-break source for the alphabetical sort (registration
    /// order breaks ties; see brief acceptance criterion 9).
    /// </remarks>
    public static IReadOnlyList<ModelRegistryEntry> EnumerateInOrder()
    {
        return Program.RegistryEntries ?? Array.Empty<ModelRegistryEntry>();
    }

    /// <summary>
    /// Case-sensitive name lookup against the loaded registry. Returns
    /// <c>true</c> and the matching entry, or <c>false</c> and <c>null</c>
    /// when no registry is loaded, the name is null/empty, or no entry
    /// matches. Use this to back <c>az-ai models show &lt;name&gt;</c>.
    /// </summary>
    /// <remarks>
    /// S04E04 -- ordinal comparison only, consistent with
    /// <see cref="ModelsWithCapability"/> and ADR-012's case-sensitive
    /// capability-tag stance.
    /// </remarks>
    public static bool TryFind(string name, out ModelRegistryEntry? entry)
    {
        if (string.IsNullOrEmpty(name)) { entry = null; return false; }
        var entries = Program.RegistryEntries;
        if (entries is null) { entry = null; return false; }
        foreach (var e in entries)
        {
            if (string.Equals(e.Name, name, StringComparison.Ordinal))
            {
                entry = e;
                return true;
            }
        }
        entry = null;
        return false;
    }

    // -- private helpers -------------------------------------------------

    private static ModelRegistryEntry[] LoadEmbedded()
    {
        var assembly = typeof(ModelRegistry).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName);
        if (stream is null)
        {
            Console.Error.WriteLine(
                $"[ERROR] Embedded resource '{EmbeddedResourceName}' not found in assembly.");
            Environment.Exit(99);
            return []; // unreachable; satisfies compiler
        }

        using var reader = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8);
        var json = reader.ReadToEnd();
        var result = JsonSerializer.Deserialize(json, AppJsonContext.Default.ModelRegistryEntryArray);
        return result ?? [];
    }

    private static ModelRegistryEntry[] ApplyUserOverride(
        ModelRegistryEntry[] seed, bool isRaw)
    {
        var userPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "az-ai", "registry.json");

        if (!File.Exists(userPath))
            return seed;

        try
        {
            var json = File.ReadAllText(userPath);
            var overrideEntries = JsonSerializer.Deserialize(
                json, AppJsonContext.Default.ModelRegistryEntryArray);
            return overrideEntries ?? seed;
        }
        catch (Exception ex)
        {
            if (!isRaw)
                Console.Error.WriteLine(
                    $"[WARN] Failed to load user registry override '{userPath}': {ex.Message}");
            return seed;
        }
    }

    /// <summary>
    /// Validates every entry returned by <see cref="LoadEmbedded"/> /
    /// <see cref="ApplyUserOverride"/>. Two fatal classes (both rc=99):
    /// (1) any capability tag outside <see cref="ModelCapability.AllowedTags"/>;
    /// (2) any <c>Name</c> containing a shell-hostile character -- single
    /// quote, double quote, backslash, or a C0/C1 control codepoint
    /// (0x00-0x1F or 0x7F-0x9F). The shell-hostile-name reject closes
    /// Mickey's A11Y-CG-01 from S04E03 (terminal-injection / screen-reader
    /// noise in suggestion lists). Missing <c>CardPath</c> remains a WARN.
    /// </summary>
    private static void ValidateEntries(ModelRegistryEntry[] entries, bool isRaw)
    {
        foreach (var entry in entries)
        {
            // A11Y-CG-01 -- reject shell-hostile chars in model names at load
            // time so they never reach the renderer, suggestion lists, or
            // shell-quoted command examples. Fatal (rc=99) -- consistent with
            // unknown-capability-tag handling above.
            if (!string.IsNullOrEmpty(entry.Name))
            {
                for (int i = 0; i < entry.Name.Length; i++)
                {
                    var c = entry.Name[i];
                    if (c == '\'' || c == '"' || c == '\\'
                        || c <= '\u001F'
                        || (c >= '\u007F' && c <= '\u009F'))
                    {
                        Console.Error.WriteLine(
                            $"[ERROR] registry rejected: model name '{entry.Name}' contains shell-hostile character at offset {i}");
                        Environment.Exit(99);
                        return; // unreachable; satisfies compiler
                    }
                }
            }

            // Validate capability tags -- unknown tag is fatal (rc=99).
            foreach (var tag in entry.Capabilities ?? [])
            {
                if (!ModelCapability.IsValid(tag))
                {
                    Console.Error.WriteLine(
                        $"[ERROR] Unknown capability tag '{tag}' in registry entry '{entry.Name}'. "
                        + "Allowed tags: "
                        + string.Join(", ", ModelCapability.AllowedTags)
                        + ". rc=99.");
                    Environment.Exit(99);
                    return; // unreachable; satisfies compiler
                }
            }

            // Missing cardPath is a warning, not fatal.
            if (string.IsNullOrEmpty(entry.CardPath) && !isRaw)
            {
                Console.Error.WriteLine(
                    $"[WARN] Registry entry '{entry.Name}' has no cardPath.");
            }
        }
    }

    // ── S04E02 Wave 1 -- Embedded Cards ─────────────────────────────────
    //
    // ReadCard resolves a registry entry's cardPath against the directory
    // that contains registry.json and parses the YAML-ish front matter into
    // a ModelCard record. Russell wires this into --doctor formatting in
    // Wave 2; do not surface it through Program.cs here.
    //
    // Hardening (FDR S04E01 Wave 2 findings):
    //   F-01  ../traversal escape  -> rc=99
    //   F-03  >256 KB file         -> rc=99
    //   F-04  FIFO / device / sock -> rc=99 (only regular files allowed)
    //   bonus symlink reject       -> rc=99 (ReparsePoint attribute)
    //
    // Missing-file behaviour: returns null (NOT fatal). Aligns with the
    // existing "missing cardPath is a WARN" stance from S04E01 -- absence
    // of a card never blocks a working registry.
    //
    // Parse failure behaviour: returns null + a [WARN] to stderr (unless
    // isRaw). The CLI keeps running with whatever cards parsed cleanly.

    private const int MaxCardBytes = 256 * 1024;

    /// <summary>
    /// Resolve <paramref name="cardPath"/> against <paramref name="registryDir"/>
    /// and parse its YAML-ish front matter into a <see cref="ModelCard"/>.
    /// Returns <c>null</c> when the file does not exist or its front matter
    /// is missing/unparseable. Hardening failures (path traversal, oversize,
    /// non-regular files) exit the process with rc=99.
    /// </summary>
    public static ModelCard? ReadCard(string cardPath, string registryDir, bool isRaw = false)
    {
        try
        {
            return ReadCardOrThrow(cardPath, registryDir, isRaw);
        }
        catch (ModelCardException ex)
        {
            Console.Error.WriteLine($"[ERROR] {ex.Message} rc=99.");
            Environment.Exit(99);
            return null; // unreachable
        }
    }

    /// <summary>
    /// Test-visible variant of <see cref="ReadCard"/> that surfaces hardening
    /// violations as <see cref="ModelCardException"/> instead of calling
    /// <see cref="Environment.Exit"/>. The public wrapper translates those
    /// exceptions to rc=99.
    /// </summary>
    internal static ModelCard? ReadCardOrThrow(string cardPath, string registryDir, bool isRaw = false)
    {
        if (string.IsNullOrEmpty(cardPath))
            return null;

        // ── F-01: path-traversal guard ────────────────────────────────
        // Cheap pre-check on the literal cardPath so an obvious "../"
        // prefix is rejected with a message that names what the registry
        // actually wrote, not the post-resolve canonical form.
        var registryFull = Path.GetFullPath(registryDir);
        var resolved = Path.GetFullPath(Path.Combine(registryFull, cardPath));
        var registryWithSep = registryFull.EndsWith(Path.DirectorySeparatorChar)
            ? registryFull
            : registryFull + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(registryWithSep, StringComparison.Ordinal)
            && !string.Equals(resolved, registryFull, StringComparison.Ordinal))
        {
            throw new ModelCardException(
                $"Card path '{cardPath}' escapes registry directory '{registryFull}'.");
        }

        var info = new FileInfo(resolved);
        if (!info.Exists)
            return null;

        // ── F-EE-01: symlink-aware prefix re-check ────────────────────
        // Path.GetFullPath collapses ".." LEXICALLY only -- it does not
        // call realpath(3) and does not resolve symlinks anywhere along
        // the path. An attacker who can drop a symlink at any *parent*
        // directory of the resolved path (e.g. <registryDir>/sub -> /etc)
        // defeats the lexical prefix check above and File.ResolveLinkTarget
        // (which only inspects the leaf) misses the bypass entirely.
        // Mitigation: canonicalise BOTH registryFull and resolved through
        // realpath(3) on Linux (preferred) or a per-ancestor
        // Directory.ResolveLinkTarget walk elsewhere, then re-run the
        // StartsWith check. macOS via the ancestor walk is best-effort
        // (tracked as F-EE-05 in ADR-012). FDR S04E02 Wave 2 / F-EE-01.
        var canonicalRegistry = Canonicalize(registryFull);
        var canonicalResolved = Canonicalize(resolved);
        var canonicalRegistryWithSep = canonicalRegistry.EndsWith(Path.DirectorySeparatorChar)
            ? canonicalRegistry
            : canonicalRegistry + Path.DirectorySeparatorChar;
        if (!canonicalResolved.StartsWith(canonicalRegistryWithSep, StringComparison.Ordinal)
            && !string.Equals(canonicalResolved, canonicalRegistry, StringComparison.Ordinal))
        {
            throw new ModelCardException(
                $"Card path '{cardPath}' escapes registry directory after symlink resolution.");
        }

        // ── F-04 + symlink: regular files only ────────────────────────
        // FileAttributes.Device is unreliable on Linux (.NET 10 reports
        // 'Normal' for FIFOs), so we cannot use the brief's literal
        // attribute check on Unix -- File.ReadAllText() on an unfiltered
        // FIFO blocks the process indefinitely (verified in S04E02 Wave 1
        // probe). Strategy:
        //   - Symlinks: File.ResolveLinkTarget() works cross-platform.
        //   - File type (FIFO/device/socket): stat() via libc on Unix;
        //     FileAttributes on Windows (FIFOs don't exist there).
        if (File.ResolveLinkTarget(resolved, returnFinalTarget: false) is not null)
        {
            throw new ModelCardException(
                $"Card file '{cardPath}' is a symlink (rejected; only regular files allowed).");
        }
        if (!IsRegularFile(resolved))
        {
            throw new ModelCardException(
                $"Card file '{cardPath}' is not a regular file (FIFO, device, or socket rejected).");
        }
        // Belt-and-braces: keep the Windows-side attribute check the brief
        // calls for so .NET's own ReparsePoint detection still gates there.
        if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new ModelCardException(
                $"Card file '{cardPath}' has reparse-point attribute (rejected).");
        }

        // ── F-03: 256 KB cap ──────────────────────────────────────────
        if (info.Length > MaxCardBytes)
        {
            throw new ModelCardException(
                $"Card file '{cardPath}' exceeds 256 KB cap (actual: {info.Length} bytes).");
        }

        string text;
        try
        {
            text = File.ReadAllText(resolved, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            if (!isRaw)
                Console.Error.WriteLine($"[WARN] Failed to read card '{cardPath}': {ex.Message}");
            return null;
        }

        return ParseFrontMatter(text, cardPath, isRaw);
    }

    /// <summary>
    /// Parse the YAML-ish front matter between two <c>---</c> fences.
    /// Returns <c>null</c> (with a [WARN] unless <paramref name="isRaw"/>)
    /// when the fences are absent or required keys are missing. We do NOT
    /// crash on unparseable cards -- a malformed doc is a doc problem, not
    /// a registry problem.
    /// </summary>
    private static ModelCard? ParseFrontMatter(string text, string cardPath, bool isRaw)
    {
        // Normalise line endings so the fence regex below stays trivial.
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        int i = 0;
        // Skip leading blank lines.
        while (i < lines.Length && lines[i].Length == 0)
            i++;

        if (i >= lines.Length || !string.Equals(lines[i].Trim(), "---", StringComparison.Ordinal))
        {
            if (!isRaw)
                Console.Error.WriteLine($"[WARN] Card '{cardPath}' missing opening '---' front-matter fence.");
            return null;
        }
        i++; // past opening fence

        var keys = new Dictionary<string, string>(StringComparer.Ordinal);
        bool closed = false;
        for (; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.Equals(line.Trim(), "---", StringComparison.Ordinal))
            {
                closed = true;
                break;
            }
            if (line.Length == 0 || line.TrimStart().StartsWith('#'))
                continue;

            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0)
                continue; // not a key: value line; ignore quietly

            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            keys[key] = value;
        }

        if (!closed)
        {
            if (!isRaw)
                Console.Error.WriteLine($"[WARN] Card '{cardPath}' missing closing '---' front-matter fence.");
            return null;
        }

        // Accept "model:" as an alias for "name:" -- the seed cards in
        // docs/model-cards/ pre-date this typed surface and use 'model'.
        // Documenting the alias inline so reviewers don't have to chase ADR.
        string? name = Get(keys, "name") ?? Get(keys, "model");
        string? provider = Get(keys, "provider");

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(provider))
        {
            if (!isRaw)
                Console.Error.WriteLine(
                    $"[WARN] Card '{cardPath}' missing required front-matter keys (name/model + provider).");
            return null;
        }

        var description = Get(keys, "description") ?? string.Empty;
        var status = Get(keys, "status") ?? "active";
        var notes = ParseBracketedList(Get(keys, "notes"));

        return new ModelCard(name, provider, description, status, notes);
    }

    private static string? Get(Dictionary<string, string> keys, string key)
    {
        if (!keys.TryGetValue(key, out var raw))
            return null;
        return StripQuotes(raw);
    }

    private static string StripQuotes(string s)
    {
        if (s.Length >= 2)
        {
            if ((s[0] == '"' && s[^1] == '"') || (s[0] == '\'' && s[^1] == '\''))
                return s[1..^1];
        }
        return s;
    }

    private static string[] ParseBracketedList(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
            return [];
        var t = raw.Trim();
        if (t.Length < 2 || t[0] != '[' || t[^1] != ']')
            return [];
        var inner = t[1..^1].Trim();
        if (inner.Length == 0)
            return [];
        var parts = inner.Split(',');
        var result = new List<string>(parts.Length);
        foreach (var p in parts)
        {
            var v = StripQuotes(p.Trim());
            if (v.Length > 0)
                result.Add(v);
        }
        return [.. result];
    }

    /// <summary>
    /// Bulk variant: read every entry's card (when present) into a map
    /// keyed by entry name. Russell consumes this for <c>--doctor</c>
    /// formatting in Wave 2. Entries whose cardPath is empty or whose card
    /// file is absent / unparseable map to <c>null</c>.
    /// </summary>
    public static Dictionary<string, ModelCard?> LoadCards(
        ModelRegistryEntry[] entries, string registryDir, bool isRaw = false)
    {
        var result = new Dictionary<string, ModelCard?>(entries.Length, StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.CardPath))
            {
                result[entry.Name] = null;
                continue;
            }
            result[entry.Name] = ReadCard(entry.CardPath, registryDir, isRaw);
        }
        return result;
    }

    // ── stat() shim for Unix file-type detection ─────────────────────────
    //
    // On Linux + macOS we must reject FIFOs/devices/sockets BEFORE any
    // open() call -- File.ReadAllText() on a FIFO with no writer hangs
    // indefinitely. .NET's FileAttributes does not report Device on Linux
    // FIFOs (verified .NET 10), so we go straight to libc stat(). On
    // Windows there are no FIFOs and FileAttributes is sufficient, so we
    // short-circuit to "true".
    //
    // st_mode layout: Linux glibc/musl x64 + arm64 place st_mode at byte
    // offset 24 of struct stat as a uint32 little-endian. Same offset
    // applies on macOS (struct stat layout matches via st_dev=u32 at 0,
    // st_mode=u16 at 4 -- different! macOS uses 'struct stat' from
    // sys/stat.h with mode at offset 4). Cross-platform offset detection
    // adds complexity; for now we Linux-only the syscall path and trust
    // FileAttributes elsewhere.

    private const int LinuxStatModeOffset = 24;
    private const uint S_IFMT = 0xF000;
    private const uint S_IFREG = 0x8000;

    [DllImport("libc", EntryPoint = "stat", CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern int LibcStat([MarshalAs(UnmanagedType.LPUTF8Str)] string path, byte[] statbuf);

    // realpath(3) -- canonicalises a path by resolving every symlink along
    // its length (NOT what Path.GetFullPath does; see F-EE-01). POSIX
    // signature: char *realpath(const char *path, char *resolved_path).
    // We pass a 4096-byte buffer (Linux PATH_MAX) so glibc/musl never
    // malloc -- no free needed, no IntPtr ownership games. Returns the
    // resolved_path pointer on success, NULL on failure (errno set).
    private const int LinuxPathMax = 4096;

    [DllImport("libc", EntryPoint = "realpath", CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern IntPtr LibcRealpath([MarshalAs(UnmanagedType.LPUTF8Str)] string path, byte[] resolved);

    // Canonicalise an absolute path so that EVERY symlink along its
    // length is resolved. Used by the F-EE-01 mitigation: a parent
    // directory symlink (e.g. <registryDir>/sub -> /etc) defeats the
    // lexical Path.GetFullPath prefix check, so we have to walk the
    // path through realpath(3) (or a per-ancestor ResolveLinkTarget
    // fallback) before re-running StartsWith.
    private static string Canonicalize(string fullPath)
    {
        if (OperatingSystem.IsLinux())
        {
            try
            {
                var buf = new byte[LinuxPathMax];
                if (LibcRealpath(fullPath, buf) != IntPtr.Zero)
                {
                    int len = Array.IndexOf(buf, (byte)0);
                    if (len < 0) len = buf.Length;
                    return Encoding.UTF8.GetString(buf, 0, len);
                }
                // realpath failed (target missing, EACCES, ELOOP). Fall
                // through to the .NET ancestor walk -- it can still resolve
                // most cases without the syscall.
            }
            catch (DllNotFoundException) { /* fall through */ }
            catch (EntryPointNotFoundException) { /* fall through */ }
        }
        return CanonicalizeViaAncestors(fullPath);
    }

    // Cross-platform fallback: walk the path component by component and
    // call Directory/File.ResolveLinkTarget(returnFinalTarget: true) on
    // each ancestor so intermediate-directory symlinks ARE resolved
    // (Path.GetFullPath alone does not). Best-effort on macOS where
    // /tmp itself is a symlink to /private/tmp -- documented as F-EE-05.
    private static string CanonicalizeViaAncestors(string fullPath)
    {
        var components = new List<string>();
        string? cursor = fullPath;
        while (true)
        {
            var parent = Path.GetDirectoryName(cursor);
            if (string.IsNullOrEmpty(parent) || string.Equals(parent, cursor, StringComparison.Ordinal))
            {
                components.Add(cursor!); // root, e.g. "/" or "C:\"
                break;
            }
            components.Add(Path.GetFileName(cursor!));
            cursor = parent;
        }
        components.Reverse();

        string accum = components[0];
        for (int i = 1; i < components.Count; i++)
        {
            accum = Path.Combine(accum, components[i]);
            try
            {
                FileSystemInfo? target = i < components.Count - 1
                    ? Directory.ResolveLinkTarget(accum, returnFinalTarget: true)
                    : File.ResolveLinkTarget(accum, returnFinalTarget: true);
                if (target is not null)
                    accum = target.FullName;
            }
            catch (IOException) { /* component missing or not a link -- leave lexical */ }
            catch (UnauthorizedAccessException) { /* not readable -- leave lexical */ }
        }
        return accum;
    }

    private static bool IsRegularFile(string path)
    {
        if (!OperatingSystem.IsLinux())
        {
            // Windows: FileAttributes works; macOS: best-effort fall-through.
            // The caller still checks ReparsePoint independently.
            return true;
        }

        try
        {
            // 256 bytes >> sizeof(struct stat) on every glibc/musl variant.
            var buf = new byte[256];
            if (LibcStat(path, buf) != 0)
                return false; // can't stat -> conservative reject

            uint mode = (uint)(buf[LinuxStatModeOffset]
                | (buf[LinuxStatModeOffset + 1] << 8)
                | (buf[LinuxStatModeOffset + 2] << 16)
                | (buf[LinuxStatModeOffset + 3] << 24));
            return (mode & S_IFMT) == S_IFREG;
        }
        catch (DllNotFoundException)
        {
            // No libc reachable (unlikely on Linux). Conservative reject is
            // wrong here -- it would block every card on a misconfigured
            // host -- so trust .NET's FileAttributes path instead.
            return true;
        }
        catch (EntryPointNotFoundException)
        {
            // Older glibc (<2.33) only exports __xstat. Same fallback.
            return true;
        }
    }
}
