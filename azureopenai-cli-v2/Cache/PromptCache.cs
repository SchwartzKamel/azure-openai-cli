using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzureOpenAI_CLI_V2.Cache;

/// <summary>
/// Serialized cache entry on disk. Contains only the response text + metadata —
/// NEVER the endpoint, api key, or any credential material. The cache key hash
/// is derived only from request shape (model + temperature + max_tokens +
/// system prompt + user prompt) so two users on different endpoints computing
/// the same key would never cross-contaminate credentials.
/// </summary>
internal sealed record CachedResponse(
    [property: JsonPropertyName("response")] string Response,
    [property: JsonPropertyName("cached_at")] DateTime CachedAt,
    [property: JsonPropertyName("ttl_hours")] int TtlHours,
    [property: JsonPropertyName("model")] string? Model = null,
    [property: JsonPropertyName("input_tokens")] int? InputTokens = null,
    [property: JsonPropertyName("output_tokens")] int? OutputTokens = null
);

/// <summary>
/// FR-008 opt-in prompt/response cache. Keyed by SHA-256 of a canonical
/// sorted-JSON payload of {model, temperature, max_tokens, system_prompt,
/// user_prompt}. Storage is per-user:
/// <list type="bullet">
///   <item><c>~/.cache/azureopenai-cli/v1/&lt;hash&gt;.json</c> on Linux/macOS (0600 files, 0700 dir)</item>
///   <item><c>%LOCALAPPDATA%\azureopenai-cli\v1\&lt;hash&gt;.json</c> on Windows</item>
/// </list>
/// Cache is opt-in — enabled only when <c>--cache</c> or <c>AZ_CACHE=1</c>.
/// Size cap: if the cache directory exceeds 50 MB, the oldest 20% of entries
/// (by mtime) are evicted on the next <see cref="Put"/>.
///
/// <para>
/// Security contract: the key derivation does NOT include endpoint URL or api
/// key material — those never touch disk. Cache values are response text only.
/// See <c>ToolHardeningTests.PromptCache_NeverWritesApiKeyMaterial</c>.
/// </para>
///
/// <para>
/// Tests can override the cache directory via the <c>AZ_CACHE_DIR</c>
/// environment variable (undocumented internal hook). Production callers
/// should always use the default path.
/// </para>
/// </summary>
internal static class PromptCache
{
    /// <summary>Default TTL in hours (7 days).</summary>
    internal const int DefaultTtlHours = 24 * 7;

    /// <summary>Soft cap on cache directory size, in bytes. Above this, eviction triggers.</summary>
    internal const long MaxCacheBytes = 50L * 1024 * 1024;

    /// <summary>Fraction of entries to evict (by age) when cap exceeded.</summary>
    internal const double EvictionFraction = 0.20;

    /// <summary>Schema version for on-disk layout. Bump to invalidate older caches.</summary>
    internal const string SchemaVersion = "v1";

    /// <summary>
    /// Resolves the cache directory. Order: <c>AZ_CACHE_DIR</c> env (test hook)
    /// &gt; platform default. On Windows, uses <c>%LOCALAPPDATA%</c>; elsewhere
    /// uses <c>$XDG_CACHE_HOME</c> or <c>~/.cache</c>.
    /// </summary>
    internal static string ResolveCacheDir()
    {
        var overrideDir = Environment.GetEnvironmentVariable("AZ_CACHE_DIR");
        if (!string.IsNullOrWhiteSpace(overrideDir))
        {
            return Path.Combine(overrideDir, SchemaVersion);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "azureopenai-cli", SchemaVersion);
        }

        var xdg = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        var baseDir = !string.IsNullOrWhiteSpace(xdg)
            ? xdg
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".cache");
        return Path.Combine(baseDir, "azureopenai-cli", SchemaVersion);
    }

    /// <summary>
    /// Computes the cache key as a hex-lowercase SHA-256 over a canonical,
    /// sorted-property JSON payload. The payload intentionally omits endpoint
    /// and api key so rotating credentials cannot poison or expose the cache.
    /// </summary>
    internal static string ComputeKey(
        string model,
        float temperature,
        int maxTokens,
        string systemPrompt,
        string userPrompt)
    {
        // Canonical payload: fixed property order, culture-invariant floats.
        // Not using AppJsonContext here to keep the hash input layout explicit
        // and unaffected by future context changes.
        var sb = new StringBuilder(256);
        sb.Append('{');
        sb.Append("\"max_tokens\":").Append(maxTokens.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',');
        sb.Append("\"model\":").Append(JsonEncodedString(model)).Append(',');
        sb.Append("\"system_prompt\":").Append(JsonEncodedString(systemPrompt)).Append(',');
        sb.Append("\"temperature\":").Append(temperature.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)).Append(',');
        sb.Append("\"user_prompt\":").Append(JsonEncodedString(userPrompt));
        sb.Append('}');

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexStringLower(bytes);
    }

    private static string JsonEncodedString(string? s) =>
        JsonSerializer.Serialize(s ?? string.Empty, AppJsonContext.Default.String);

    /// <summary>
    /// Attempts to load a cached response for <paramref name="key"/>. Returns
    /// <c>null</c> on miss, corrupt entry, or TTL expiry (expired entries are
    /// best-effort deleted). Never throws.
    /// </summary>
    /// <param name="key">Cache key from <see cref="ComputeKey"/>.</param>
    /// <param name="maxAge">Optional override for TTL; if supplied, the
    /// entry's own <c>ttl_hours</c> is ignored and <paramref name="maxAge"/>
    /// is applied to its <c>cached_at</c> timestamp.</param>
    internal static CachedResponse? TryGet(string key, TimeSpan? maxAge = null)
    {
        try
        {
            var path = PathForKey(key);
            if (!File.Exists(path)) return null;

            var json = File.ReadAllText(path);
            var entry = JsonSerializer.Deserialize(json, AppJsonContext.Default.CachedResponse);
            if (entry == null) return null;

            var age = DateTime.UtcNow - entry.CachedAt;
            var limit = maxAge ?? TimeSpan.FromHours(entry.TtlHours);
            if (age > limit)
            {
                TryDelete(path);
                return null;
            }
            return entry;
        }
        catch
        {
            // Corrupt / unreadable entry — treat as miss.
            return null;
        }
    }

    /// <summary>
    /// Writes <paramref name="value"/> to the cache under <paramref name="key"/>.
    /// Best-effort: IO errors are swallowed (never fail a successful completion).
    /// Triggers <see cref="Evict"/> when directory size exceeds the cap.
    /// </summary>
    internal static void Put(string key, CachedResponse value)
    {
        try
        {
            var dir = ResolveCacheDir();
            Directory.CreateDirectory(dir);
            TrySetDirPerms(dir);

            var path = PathForKey(key);
            var json = JsonSerializer.Serialize(value, AppJsonContext.Default.CachedResponse);
            File.WriteAllText(path, json);
            TrySetFilePerms(path);

            // Eviction runs AFTER write so the just-written entry can be victim
            // only if it's among the oldest 20% — which it won't be.
            Evict();
        }
        catch
        {
            // Best-effort caching — never fail the call for a cache write.
        }
    }

    /// <summary>
    /// Evicts the oldest 20% of entries (by mtime) when total on-disk size
    /// exceeds <see cref="MaxCacheBytes"/>. No-op below the cap.
    /// </summary>
    internal static void Evict()
    {
        try
        {
            var dir = ResolveCacheDir();
            if (!Directory.Exists(dir)) return;

            var files = new DirectoryInfo(dir).GetFiles("*.json");
            long total = 0;
            foreach (var f in files) total += f.Length;
            if (total <= MaxCacheBytes) return;

            Array.Sort(files, (a, b) => a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc));
            int toEvict = Math.Max(1, (int)Math.Ceiling(files.Length * EvictionFraction));
            for (int i = 0; i < toEvict && i < files.Length; i++)
            {
                TryDelete(files[i].FullName);
            }
        }
        catch
        {
            // Best-effort eviction.
        }
    }

    /// <summary>Returns the absolute file path a given key maps to.</summary>
    internal static string PathForKey(string key) =>
        Path.Combine(ResolveCacheDir(), key + ".json");

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* best-effort */ }
    }

    private static void TrySetDirPerms(string dir)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        try { File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
        catch { /* best-effort perms */ }
    }

    private static void TrySetFilePerms(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        try { File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
        catch { /* best-effort perms */ }
    }
}
