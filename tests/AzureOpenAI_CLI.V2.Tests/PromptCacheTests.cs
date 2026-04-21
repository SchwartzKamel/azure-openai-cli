using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AzureOpenAI_CLI_V2.Cache;

namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// FR-008 PromptCache tests. Covers key derivation, TTL, storage path,
/// eviction, and the security-hardening contract that no credentials ever
/// land on disk. Sequential ([Collection]) so AZ_CACHE_DIR mutation doesn't
/// race other tests.
/// </summary>
[Collection("ConsoleCapture")]
public class PromptCacheTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _originalEnv;

    public PromptCacheTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "az-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _originalEnv = Environment.GetEnvironmentVariable("AZ_CACHE_DIR");
        Environment.SetEnvironmentVariable("AZ_CACHE_DIR", _tempDir);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("AZ_CACHE_DIR", _originalEnv);
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort */ }
    }

    // ── Key derivation ─────────────────────────────────────────────────

    [Fact]
    public void ComputeKey_DeterministicAcrossCalls()
    {
        var a = PromptCache.ComputeKey("gpt-4o", 0.5f, 1000, "sys", "user");
        var b = PromptCache.ComputeKey("gpt-4o", 0.5f, 1000, "sys", "user");
        Assert.Equal(a, b);
        Assert.Equal(64, a.Length); // SHA-256 hex
        Assert.Matches("^[0-9a-f]+$", a);
    }

    [Theory]
    [InlineData("gpt-4o", "gpt-4o-mini")]
    public void ComputeKey_ModelDifferentiates(string m1, string m2)
    {
        var a = PromptCache.ComputeKey(m1, 0.5f, 1000, "sys", "user");
        var b = PromptCache.ComputeKey(m2, 0.5f, 1000, "sys", "user");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ComputeKey_TemperatureDifferentiates()
    {
        var a = PromptCache.ComputeKey("m", 0.5f, 1000, "sys", "user");
        var b = PromptCache.ComputeKey("m", 0.7f, 1000, "sys", "user");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ComputeKey_MaxTokensDifferentiates()
    {
        var a = PromptCache.ComputeKey("m", 0.5f, 1000, "sys", "user");
        var b = PromptCache.ComputeKey("m", 0.5f, 2000, "sys", "user");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ComputeKey_SystemPromptDifferentiates()
    {
        var a = PromptCache.ComputeKey("m", 0.5f, 1000, "sysA", "user");
        var b = PromptCache.ComputeKey("m", 0.5f, 1000, "sysB", "user");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ComputeKey_UserPromptDifferentiates()
    {
        var a = PromptCache.ComputeKey("m", 0.5f, 1000, "sys", "userA");
        var b = PromptCache.ComputeKey("m", 0.5f, 1000, "sys", "userB");
        Assert.NotEqual(a, b);
    }

    // ── Put / TryGet round-trip ────────────────────────────────────────

    [Fact]
    public void PutThenTryGet_ReturnsSameResponse()
    {
        var key = PromptCache.ComputeKey("m", 0.5f, 1000, "sys", "user");
        var entry = new CachedResponse("hello world", DateTime.UtcNow, 24, "m", 10, 20);
        PromptCache.Put(key, entry);

        var hit = PromptCache.TryGet(key);
        Assert.NotNull(hit);
        Assert.Equal("hello world", hit!.Response);
        Assert.Equal(10, hit.InputTokens);
        Assert.Equal(20, hit.OutputTokens);
    }

    [Fact]
    public void TryGet_MissingKey_ReturnsNull()
    {
        var hit = PromptCache.TryGet("deadbeef"); // never written
        Assert.Null(hit);
    }

    [Fact]
    public void TryGet_ExpiredEntry_ReturnsNullAndDeletes()
    {
        var key = PromptCache.ComputeKey("m", 0.5f, 1000, "sys", "exp-user");
        var entry = new CachedResponse("stale", DateTime.UtcNow.AddHours(-10), 1, "m", null, null);
        PromptCache.Put(key, entry);

        var hit = PromptCache.TryGet(key);
        Assert.Null(hit);

        // Expired entry is best-effort deleted
        Assert.False(File.Exists(PromptCache.PathForKey(key)));
    }

    [Fact]
    public void TryGet_CustomMaxAge_Overrides_EntryTtl()
    {
        // Entry claims TTL=100h but custom maxAge=1s should still expire it.
        var key = PromptCache.ComputeKey("m", 0.5f, 1000, "sys", "override-ttl");
        var entry = new CachedResponse("data", DateTime.UtcNow.AddMinutes(-5), 100, "m", null, null);
        PromptCache.Put(key, entry);

        var hit = PromptCache.TryGet(key, maxAge: TimeSpan.FromSeconds(1));
        Assert.Null(hit);
    }

    [Fact]
    public void TryGet_CorruptFile_ReturnsNullNeverThrows()
    {
        var key = "badkey";
        Directory.CreateDirectory(Path.GetDirectoryName(PromptCache.PathForKey(key))!);
        File.WriteAllText(PromptCache.PathForKey(key), "not valid json {{{");

        var hit = PromptCache.TryGet(key);
        Assert.Null(hit);
    }

    // ── Storage path ───────────────────────────────────────────────────

    [Fact]
    public void ResolveCacheDir_UsesV1Schema()
    {
        var dir = PromptCache.ResolveCacheDir();
        Assert.EndsWith("v1", dir, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveCacheDir_RespectsEnvOverride()
    {
        var dir = PromptCache.ResolveCacheDir();
        Assert.StartsWith(_tempDir, dir, StringComparison.Ordinal);
    }

    [Fact]
    public void Put_CreatesDirectory_IfMissing()
    {
        var nested = Path.Combine(_tempDir, "doesnotexist");
        Environment.SetEnvironmentVariable("AZ_CACHE_DIR", nested);
        try
        {
            var key = PromptCache.ComputeKey("m", 0.5f, 10, "s", "u");
            PromptCache.Put(key, new CachedResponse("x", DateTime.UtcNow, 1));
            Assert.True(File.Exists(PromptCache.PathForKey(key)));
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZ_CACHE_DIR", _tempDir);
        }
    }

    [Fact]
    public void Put_Unix_AppliesRestrictiveFilePerms()
    {
        if (OperatingSystem.IsWindows()) return;
        var key = PromptCache.ComputeKey("m", 0.5f, 10, "s", "perm-check");
        PromptCache.Put(key, new CachedResponse("secret-ish", DateTime.UtcNow, 1));

        var mode = File.GetUnixFileMode(PromptCache.PathForKey(key));
        // Owner rw only, no group/other bits.
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
    }

    // ── Eviction ────────────────────────────────────────────────────────

    [Fact]
    public void Evict_NoOp_BelowCap()
    {
        for (int i = 0; i < 5; i++)
        {
            PromptCache.Put(
                PromptCache.ComputeKey("m", 0.5f, 10, "s", $"u{i}"),
                new CachedResponse("x", DateTime.UtcNow, 1));
        }
        var before = Directory.GetFiles(PromptCache.ResolveCacheDir()).Length;
        PromptCache.Evict();
        var after = Directory.GetFiles(PromptCache.ResolveCacheDir()).Length;
        Assert.Equal(before, after);
    }

    [Fact]
    public void Evict_AboveCap_RemovesOldestByMtime()
    {
        // Stuff enough bytes to exceed 50 MB: write 6 files of 10 MB each.
        var dir = PromptCache.ResolveCacheDir();
        Directory.CreateDirectory(dir);
        var baseTime = DateTime.UtcNow.AddDays(-7);
        var paths = new List<string>();
        for (int i = 0; i < 6; i++)
        {
            var p = Path.Combine(dir, $"{i:D2}" + new string('a', 62) + ".json");
            File.WriteAllBytes(p, new byte[10 * 1024 * 1024]);
            File.SetLastWriteTimeUtc(p, baseTime.AddMinutes(i)); // ascending
            paths.Add(p);
        }

        PromptCache.Evict();

        // 20% of 6 = ceil(1.2) = 2 entries evicted (oldest two).
        Assert.False(File.Exists(paths[0]));
        Assert.False(File.Exists(paths[1]));
        Assert.True(File.Exists(paths[5]));
    }

    // ── Security: no api-key material ever written ─────────────────────

    [Fact]
    public void PromptCache_NeverWritesApiKeyMaterial()
    {
        // Hardening contract: even if a user prompt includes the string of an
        // api key, the cache KEY is a hash — credentials never appear in any
        // filename. And values only contain the response, never the key.
        Environment.SetEnvironmentVariable("AZUREOPENAIAPI", "sk-this-must-not-leak-abc123");
        var fakeEndpoint = "https://fake.openai.azure.com/";

        var key = PromptCache.ComputeKey("gpt-4o", 0.5f, 1000, "system", "user prompt");

        // Hash must not be the api key, not contain the api key substring.
        Assert.DoesNotContain("sk-this-must-not-leak", key);
        Assert.DoesNotContain("openai.azure.com", key);

        PromptCache.Put(key, new CachedResponse("response body", DateTime.UtcNow, 1, "gpt-4o"));

        // Scan all files in cache dir for the api key / endpoint — must not appear.
        foreach (var path in Directory.GetFiles(PromptCache.ResolveCacheDir(), "*.json", SearchOption.AllDirectories))
        {
            Assert.DoesNotContain("sk-this-must-not-leak", Path.GetFileName(path));
            var content = File.ReadAllText(path);
            Assert.DoesNotContain("sk-this-must-not-leak", content);
            Assert.DoesNotContain(fakeEndpoint, content);
        }
    }

    [Fact]
    public void ComputeKey_Ignores_Endpoint_And_ApiKey_By_Design()
    {
        // Rotating credentials must not invalidate cache (proving the key
        // derivation doesn't touch them). Two "sessions" with different api
        // keys / endpoints produce the same key for the same prompt.
        Environment.SetEnvironmentVariable("AZUREOPENAIAPI", "key-A");
        Environment.SetEnvironmentVariable("AZUREOPENAIENDPOINT", "https://a.example.com/");
        var keyA = PromptCache.ComputeKey("m", 0.5f, 10, "s", "u");

        Environment.SetEnvironmentVariable("AZUREOPENAIAPI", "key-B");
        Environment.SetEnvironmentVariable("AZUREOPENAIENDPOINT", "https://b.example.com/");
        var keyB = PromptCache.ComputeKey("m", 0.5f, 10, "s", "u");

        Assert.Equal(keyA, keyB);
    }
}
