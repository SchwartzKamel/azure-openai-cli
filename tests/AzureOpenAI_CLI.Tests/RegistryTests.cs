// S04E01 -- The Registry (Puddy, Wave 2).
//
// 7 required tests from the episode brief acceptance criteria.
// Single new file only -- no production code changes.
//
// Collection("ConsoleCapture"): test 3 redirects Console.Error; serialized
// against all other stderr/env mutators in the suite.
//
// Override-path note: ModelRegistry.Load() hard-codes
// ~/.config/az-ai/registry.json as the user override path with no
// injectable seam. Tests that require a specific registry state use
// RegistryOverrideScope (backup/restore) and SuppressOverrideScope
// (temporarily hides any existing file). Finding: see return message.

using System.Text.Json;
using AzureOpenAI_CLI;
using AzureOpenAI_CLI.Registry;

namespace AzureOpenAI_CLI.Tests.Registry;

[Collection("ConsoleCapture")]
public class RegistryTests
{
    // ── Test infrastructure ────────────────────────────────────────────────

    private static string OverridePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config", "az-ai", "registry.json");

    /// <summary>
    /// Writes <paramref name="json"/> to the user override path, backing up
    /// any existing file. Restores on dispose.
    /// </summary>
    private sealed class RegistryOverrideScope : IDisposable
    {
        private readonly string _path = OverridePath;
        private readonly string? _backup;
        private readonly bool _existed;

        public RegistryOverrideScope(string json)
        {
            _existed = File.Exists(_path);
            if (_existed)
            {
                _backup = _path + ".puddy-test-bak";
                File.Copy(_path, _backup, overwrite: true);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, json, System.Text.Encoding.UTF8);
        }

        public void Dispose()
        {
            if (_existed && _backup is not null)
            {
                File.Copy(_backup, _path, overwrite: true);
                File.Delete(_backup);
            }
            else if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
    }

    /// <summary>
    /// Temporarily removes any existing user override file so that Load()
    /// reads the embedded seed. Restores on dispose.
    /// </summary>
    private sealed class SuppressOverrideScope : IDisposable
    {
        private readonly string _path = OverridePath;
        private readonly string? _backup;

        public SuppressOverrideScope()
        {
            if (!File.Exists(_path))
                return;

            _backup = _path + ".puddy-suppress-bak";
            File.Move(_path, _backup);
        }

        public void Dispose()
        {
            if (_backup is not null && File.Exists(_backup))
                File.Move(_backup, _path);
        }
    }

    /// <summary>Redirects Console.Error to a StringWriter for the scope.</summary>
    private sealed class StderrScope : IDisposable
    {
        private readonly TextWriter _prev;
        public StringWriter Writer { get; } = new StringWriter();

        public StderrScope()
        {
            _prev = Console.Error;
            Console.SetError(Writer);
        }

        public void Dispose()
        {
            Console.SetError(_prev);
            Writer.Dispose();
        }
    }

    // ── Test 1: HappyPath ─────────────────────────────────────────────────

    [Fact]
    public void LoadRegistry_HappyPath_ReturnsThreeEntries()
    {
        // Suppress any existing user override so the embedded seed is used.
        using var suppress = new SuppressOverrideScope();

        var entries = ModelRegistry.Load(isRaw: true);

        Assert.Equal(3, entries.Length);
        Assert.Contains(entries, e => string.Equals(e.Name, "gpt-4o-mini", StringComparison.Ordinal));
        Assert.Contains(entries, e => string.Equals(e.Name, "gpt-5.4-nano", StringComparison.Ordinal));
        Assert.Contains(entries, e => string.Equals(e.Name, "llama-local", StringComparison.Ordinal));
    }

    // ── Test 2: UnknownCapabilityTag ──────────────────────────────────────

    // ModelRegistry.ValidateEntries calls Environment.Exit(99) -- not
    // in-process testable without process isolation. The unit-level surface
    // is ModelCapability.ValidateOrThrow, which throws ArgumentException on
    // the first unknown tag and is exactly the predicate that drives rc=99.
    // Finding: no test-isolable validator surface in ModelRegistry itself;
    // a throwing internal overload would close the gap without process spawn.
    [Fact]
    public void LoadRegistry_UnknownCapabilityTag_ExitsRc99()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ModelCapability.ValidateOrThrow(
                ["tool_calls", "BOGUS_TAG_XYZ"],
                "test-entry-for-puddy"));

        Assert.Contains("BOGUS_TAG_XYZ", ex.Message, StringComparison.Ordinal);
        Assert.Contains("test-entry-for-puddy", ex.Message, StringComparison.Ordinal);
    }

    // ── Test 3: MissingCardPath ────────────────────────────────────────────

    [Fact]
    public void LoadRegistry_MissingCardPath_WarnsNotFatal()
    {
        const string json = """
            [
              {
                "name": "no-card-model",
                "provider": "azure",
                "capabilities": ["streaming"],
                "contextWindow": 4096,
                "costTier": "low"
              }
            ]
            """;

        using var stderr = new StderrScope();
        using var scope = new RegistryOverrideScope(json);

        var entries = ModelRegistry.Load(isRaw: false);

        Assert.Single(entries);
        Assert.Equal("no-card-model", entries[0].Name, StringComparer.Ordinal);
        Assert.Null(entries[0].CardPath);

        var captured = stderr.Writer.ToString();
        Assert.Contains("[WARN]", captured, StringComparison.Ordinal);
        Assert.Contains("no-card-model", captured, StringComparison.Ordinal);
    }

    // ── Test 4: EmptyFile ─────────────────────────────────────────────────

    [Fact]
    public void LoadRegistry_EmptyFile_ReturnsEmptyList()
    {
        using var scope = new RegistryOverrideScope("[]");

        var entries = ModelRegistry.Load(isRaw: true);

        Assert.Empty(entries);
    }

    // ── Test 5: UserOverrideFile replaces seed (no merge) ─────────────────

    [Fact]
    public void LoadRegistry_UserOverrideFile_ReplacesSeedEntries()
    {
        const string json = """
            [
              {
                "name": "custom-only-model",
                "provider": "custom",
                "capabilities": ["streaming"],
                "contextWindow": 32000,
                "costTier": "low",
                "cardPath": "docs/model-cards/custom.md"
              }
            ]
            """;

        using var scope = new RegistryOverrideScope(json);

        var entries = ModelRegistry.Load(isRaw: true);

        Assert.Single(entries);
        Assert.Equal("custom-only-model", entries[0].Name, StringComparer.Ordinal);
        Assert.DoesNotContain(entries, e =>
            string.Equals(e.Name, "gpt-4o-mini", StringComparison.Ordinal));
        Assert.DoesNotContain(entries, e =>
            string.Equals(e.Name, "gpt-5.4-nano", StringComparison.Ordinal));
        Assert.DoesNotContain(entries, e =>
            string.Equals(e.Name, "llama-local", StringComparison.Ordinal));
    }

    // ── Test 6: AOT-safe serialization round-trip ──────────────────────────

    // Uses AppJsonContext source-gen path (AOT-compatible). ModelRegistryEntry
    // and ModelRegistryEntry[] are registered in JsonGenerationContext.cs.
    [Fact]
    public void ModelRegistryEntry_Serialization_RoundTrip()
    {
        var original = new ModelRegistryEntry(
            Name: "round-trip-model",
            Provider: "azure",
            Capabilities: ["tool_calls", "streaming", "json_mode"],
            ContextWindow: 128000,
            CostTier: "low",
            CardPath: "docs/model-cards/round-trip.md");

        var json = JsonSerializer.Serialize(original, AppJsonContext.Default.ModelRegistryEntry);
        var deserialized = JsonSerializer.Deserialize(json, AppJsonContext.Default.ModelRegistryEntry);

        Assert.NotNull(deserialized);
        Assert.Equal(original.Name, deserialized!.Name, StringComparer.Ordinal);
        Assert.Equal(original.Provider, deserialized.Provider, StringComparer.Ordinal);
        Assert.Equal(original.ContextWindow, deserialized.ContextWindow);
        Assert.Equal(original.CostTier, deserialized.CostTier, StringComparer.Ordinal);
        Assert.Equal(original.CardPath, deserialized.CardPath, StringComparer.Ordinal);
        Assert.Equal(original.Capabilities, deserialized.Capabilities);
    }

    // ── Test 7: OfflineFlag -- registry load is pure local, no HTTP ────────

    // ModelRegistry.Load() reads only the embedded resource and the local
    // filesystem. It contains no HttpClient, WebRequest, or HttpRequestMessage
    // references. Two assertions:
    //   (a) Runtime: Load() succeeds with AZ_AI_OFFLINE=1 set.
    //   (b) Structural: no HttpClient parameter appears on any ModelRegistry method.
    [Fact]
    public void LoadRegistry_OfflineFlag_DoesNotAttemptFetch()
    {
        using var suppress = new SuppressOverrideScope();
        var prev = Environment.GetEnvironmentVariable("AZ_AI_OFFLINE");
        try
        {
            Environment.SetEnvironmentVariable("AZ_AI_OFFLINE", "1");
            var entries = ModelRegistry.Load(isRaw: true);
            Assert.NotNull(entries);
            Assert.True(entries.Length > 0, "Seed entries must load when AZ_AI_OFFLINE=1");
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZ_AI_OFFLINE", prev);
        }

        // Structural: ModelRegistry must not accept or create HttpClient.
        var methods = typeof(ModelRegistry).GetMethods(
            System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Static
            | System.Reflection.BindingFlags.Instance);

        foreach (var method in methods)
        {
            foreach (var param in method.GetParameters())
            {
                Assert.False(
                    typeof(System.Net.Http.HttpClient).IsAssignableFrom(param.ParameterType),
                    $"ModelRegistry.{method.Name} has an HttpClient parameter -- registry must be offline-safe.");
            }
        }
    }

    // ── S04E02 Wave 1 -- ReadCard / Embedded Cards (Kramer) ───────────────
    //
    // Five tests, one per FDR S04E01 Wave 2 finding plus a happy path and a
    // missing-file null contract. The rc=99 surfaces use the internal
    // ReadCardOrThrow seam (typed ModelCardException) rather than the public
    // ReadCard which calls Environment.Exit -- same predicate, no process
    // isolation needed. Documented in ModelRegistry.cs.

    private static string FindRepoRoot()
    {
        // Tests run from bin/.../net10.0; walk up to the repo root by looking
        // for the .sln file. Keeps tests robust against future test-host churn.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "azure-openai-cli.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    [Fact]
    public void ReadCard_HappyPath_ReturnsParsedFields()
    {
        var repoRoot = FindRepoRoot();

        var card = ModelRegistry.ReadCard(
            cardPath: "docs/model-cards/azure-gpt-4o-mini.md",
            registryDir: repoRoot,
            isRaw: true);

        Assert.NotNull(card);
        // Seed card uses 'model:' alias for name; provider is 'azure'.
        Assert.Equal("gpt-4o-mini", card!.Name, StringComparer.Ordinal);
        Assert.Equal("azure", card.Provider, StringComparer.Ordinal);
        // Defaults applied (seed cards predate description/status/notes keys).
        Assert.Equal("active", card.Status, StringComparer.Ordinal);
        Assert.NotNull(card.Notes);
    }

    [Fact]
    public void ReadCard_PathTraversal_ExitsRc99()
    {
        // Use a freshly-minted temp registry dir so the traversal escape is
        // unambiguous: ../../../etc/passwd from /tmp/<rand>/ cannot possibly
        // resolve back under /tmp/<rand>/.
        var tempDir = Path.Combine(Path.GetTempPath(), "az-ai-kramer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var ex = Assert.Throws<ModelCardException>(() =>
                ModelRegistry.ReadCardOrThrow(
                    cardPath: "../../../etc/passwd",
                    registryDir: tempDir,
                    isRaw: true));
            Assert.Contains("../../../etc/passwd", ex.Message, StringComparison.Ordinal);
            Assert.Contains("escapes", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ReadCard_OversizeFile_ExitsRc99()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "az-ai-kramer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            // 300 KB > 256 KB cap.
            var cardFile = Path.Combine(tempDir, "fat-card.md");
            File.WriteAllBytes(cardFile, new byte[300 * 1024]);

            var ex = Assert.Throws<ModelCardException>(() =>
                ModelRegistry.ReadCardOrThrow(
                    cardPath: "fat-card.md",
                    registryDir: tempDir,
                    isRaw: true));
            Assert.Contains("256 KB", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ReadCard_FifoOrDevice_ExitsRc99()
    {
        // Linux-only: needs mkfifo. Early-return on Windows / macOS CI shards
        // that don't ship the binary (no Xunit.SkippableFact in this project).
        if (!OperatingSystem.IsLinux())
            return;
        var mkfifo = new[] { "/usr/bin/mkfifo", "/bin/mkfifo" }.FirstOrDefault(File.Exists);
        if (mkfifo is null)
            return;

        var tempDir = Path.Combine(Path.GetTempPath(), "az-ai-kramer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var fifoPath = Path.Combine(tempDir, "card.fifo");
            using (var p = System.Diagnostics.Process.Start(mkfifo!, fifoPath))
            {
                p.WaitForExit();
                Assert.Equal(0, p.ExitCode);
            }

            var ex = Assert.Throws<ModelCardException>(() =>
                ModelRegistry.ReadCardOrThrow(
                    cardPath: "card.fifo",
                    registryDir: tempDir,
                    isRaw: true));
            Assert.Contains("not a regular file", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ReadCard_MissingFile_ReturnsNull()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "az-ai-kramer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var card = ModelRegistry.ReadCard(
                cardPath: "nope-does-not-exist.md",
                registryDir: tempDir,
                isRaw: true);
            Assert.Null(card);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
