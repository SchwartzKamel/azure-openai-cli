using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzureOpenAI_CLI;

// FR-014 / S03E06 -- The Schema.
//
// `preferences.json` v1 -- the unified provider + profile registry that
// downstream FR-018/019/020 + S03E08+ episodes will populate. v1 carries
// only the fields needed to render `az-ai --config show` and to seed the
// profile-resolution layer. No API keys live here -- credentials stay in
// the OS credential store / env (per ADR-007 and Newman's S03E04 audit).
//
// Resolution order is documented in ADR-009 (default-model-resolution);
// this file extends that chain to provider/profile/endpoint per the
// generalised order in the same ADR's "Compliance" section:
//
//   1. CLI flag         (e.g. --provider, --model)
//   2. Environment      (AZUREOPENAIENDPOINT, AZUREOPENAIMODEL, AZ_PROFILE)
//   3. Active profile   (preferences.profiles[<active>])
//   4. Provider default (preferences.providers[<provider>])
//
// AOT: serialised through AppJsonContext (JsonGenerationContext.cs).

/// <summary>
/// Root preferences document persisted at <see cref="DefaultPath"/>.
/// File is OPTIONAL -- a missing file deserialises to a default-constructed
/// instance. No secret material is ever stored here.
/// </summary>
internal sealed class Preferences
{
    /// <summary>Schema version pin. v1 == "1". Loader does not yet upgrade.</summary>
    [JsonPropertyName("schema")]
    public string Schema { get; set; } = "1";

    /// <summary>Provider registry, keyed by provider name (azure, openai, ...).</summary>
    [JsonPropertyName("providers")]
    public Dictionary<string, ProviderEntry> Providers { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Profile registry, keyed by profile name (default, work, ...).</summary>
    [JsonPropertyName("profiles")]
    public Dictionary<string, ProfileEntry> Profiles { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Path this instance was loaded from. Not serialised.</summary>
    [JsonIgnore]
    public string? LoadedFrom { get; set; }

    /// <summary>
    /// Canonical preferences path. XDG on Linux/macOS, %APPDATA% on Windows.
    /// </summary>
    public static string DefaultPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "az-ai", "preferences.json");
        }

        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var configHome = !string.IsNullOrWhiteSpace(xdg)
            ? xdg
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config");
        return Path.Combine(configHome, "az-ai", "preferences.json");
    }

    /// <summary>
    /// Load preferences from <paramref name="path"/>. If the file is missing,
    /// returns a default-constructed instance (never throws on missing).
    /// Throws <see cref="InvalidPreferencesException"/> on malformed JSON.
    /// </summary>
    public static Preferences Load(string path)
    {
        if (!File.Exists(path))
        {
            return new Preferences { LoadedFrom = null };
        }

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (IOException ex)
        {
            throw new InvalidPreferencesException(path, "Could not read preferences file: " + ex.Message, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidPreferencesException(path, "Permission denied reading preferences file: " + ex.Message, ex);
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return new Preferences { LoadedFrom = path };
        }

        try
        {
            var prefs = JsonSerializer.Deserialize(json, AppJsonContext.Default.Preferences);
            if (prefs == null)
            {
                return new Preferences { LoadedFrom = path };
            }
            // Defensive: deserialiser leaves dictionaries null when absent.
            prefs.Providers ??= new Dictionary<string, ProviderEntry>(StringComparer.Ordinal);
            prefs.Profiles ??= new Dictionary<string, ProfileEntry>(StringComparer.Ordinal);
            prefs.LoadedFrom = path;
            return prefs;
        }
        catch (JsonException ex)
        {
            throw new InvalidPreferencesException(path, "Malformed JSON in preferences file: " + ex.Message, ex);
        }
    }

    /// <summary>
    /// Persist preferences to <paramref name="path"/>. Creates parent dirs.
    /// On Unix, sets mode 0600 (best-effort). On Windows, leaves default ACL.
    /// </summary>
    public static void Save(string path, Preferences prefs)
    {
        ArgumentNullException.ThrowIfNull(prefs);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(prefs, AppJsonContext.Default.Preferences);
        File.WriteAllText(path, json);
        SetRestrictivePermissions(path);
        prefs.LoadedFrom = path;
    }

    private static void SetRestrictivePermissions(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch
        {
            // Best-effort -- matches UserConfig.SetRestrictivePermissions.
        }
    }
}

/// <summary>
/// Provider entry. v1 carries only what `--config show` needs; richer fields
/// (apiKeyEnv, apiVersion, deployments[], capabilities{}) land in later
/// episodes per FR-014 §4.
/// </summary>
internal sealed class ProviderEntry
{
    /// <summary>Endpoint base URL (e.g. https://x.openai.azure.com/).</summary>
    [JsonPropertyName("endpoint")]
    public string? Endpoint { get; set; }

    /// <summary>Default model alias for this provider (e.g. "gpt-4o-mini").</summary>
    [JsonPropertyName("modelAlias")]
    public string? ModelAlias { get; set; }

    /// <summary>Free-form note (operator memo). Never displayed in --raw mode.</summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

/// <summary>
/// Profile entry. Pins a provider + optional model override. Profiles do not
/// carry credentials; the provider entry does that.
/// </summary>
internal sealed class ProfileEntry
{
    /// <summary>Provider name (must match a key in Preferences.Providers).</summary>
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "azure";

    /// <summary>Optional model override; null falls back to ProviderEntry.ModelAlias.</summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>Free-form note.</summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

/// <summary>
/// Thrown when preferences.json is present but unreadable or malformed.
/// Carries the offending path so callers can surface a useful error.
/// </summary>
internal sealed class InvalidPreferencesException : Exception
{
    public string Path { get; }

    public InvalidPreferencesException(string path, string message)
        : base(message)
    {
        Path = path;
    }

    public InvalidPreferencesException(string path, string message, Exception inner)
        : base(message, inner)
    {
        Path = path;
    }
}
