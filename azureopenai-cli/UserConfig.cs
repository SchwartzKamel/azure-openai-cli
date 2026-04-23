using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzureOpenAI_CLI;

/// <summary>
/// Persistent user preferences (FR-003) + model aliases (FR-010) + directory
/// overrides (FR-009). JSON schema:
/// <code>
/// {
///   "models": { "fast": "gpt-4o-mini", "smart": "gpt-4o" },
///   "default_model": "fast",
///   "defaults": {
///     "temperature": 0.7,
///     "max_tokens": 4000,
///     "timeout_seconds": 90,
///     "system_prompt": "…"
///   }
/// }
/// </code>
///
/// Precedence (highest wins): CLI flag &gt; env var &gt; project-local config
/// (<c>./.azureopenai-cli.json</c>) &gt; user config (<c>~/.azureopenai-cli.json</c>)
/// &gt; hardcoded defaults.
///
/// Matches the resolver at <c>Program.cs</c> (model/temperature/system/etc.)
/// and the help text printed under "Configuration" — corrected per Elaine's
/// 2026 audit (prior comment inverted CLI and env). For boolean umbrellas like
/// <c>AZ_TELEMETRY</c> / <c>AZ_PREWARM</c>, env can still enable a flag when
/// the CLI did not explicitly set it. See <c>ParseArgs</c>.
///
/// AOT: serialized via <see cref="AppJsonContext"/> source generator.
/// </summary>
internal sealed class UserConfig
{
    internal const string ConfigFileName = ".azureopenai-cli.json";

    /// <summary>Alias → Azure deployment name (e.g. "fast" → "gpt-4o-mini").</summary>
    [JsonPropertyName("models")]
    public Dictionary<string, string> Models { get; set; } = new();

    /// <summary>Alias of the default model (looked up in <see cref="Models"/>).</summary>
    [JsonPropertyName("default_model")]
    public string? DefaultModel { get; set; }

    /// <summary>User-preference defaults (FR-003).</summary>
    [JsonPropertyName("defaults")]
    public UserDefaults Defaults { get; set; } = new();

    /// <summary>
    /// Path this config was loaded from (null if no file found). Not serialized.
    /// </summary>
    [JsonIgnore]
    public string? LoadedFrom { get; set; }

    /// <summary>Default user config path (<c>~/.azureopenai-cli.json</c>).</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ConfigFileName);

    /// <summary>Project-local config path (<c>./.azureopenai-cli.json</c>).</summary>
    public static string LocalPath => Path.Combine(
        Directory.GetCurrentDirectory(),
        ConfigFileName);

    /// <summary>
    /// Load config from the given path (explicit <c>--config &lt;path&gt;</c>), or
    /// from project-local path, or from user-home path, in that precedence order.
    /// Missing files return an empty default <see cref="UserConfig"/>.
    /// <para>
    /// <paramref name="quiet"/> suppresses the stderr <c>[WARNING]</c> lines for
    /// parse / IO / permission errors. The <c>--raw</c> contract (FDR v2 dogfood
    /// High-severity finding) requires NOTHING on stderr for Espanso / AHK
    /// consumers; a malformed <c>~/.azureopenai-cli.json</c> must degrade
    /// silently to defaults rather than leak diagnostics into their pipe.
    /// </para>
    /// </summary>
    public static UserConfig Load(string? explicitPath = null, bool quiet = false)
    {
        // Precedence: explicit path > ./local > ~/user > empty default
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            candidates.Add(explicitPath);
        }
        else
        {
            if (File.Exists(LocalPath)) candidates.Add(LocalPath);
            if (File.Exists(DefaultPath)) candidates.Add(DefaultPath);
        }

        foreach (var path in candidates)
        {
            try
            {
                if (!File.Exists(path)) continue;
                var json = File.ReadAllText(path);
                var cfg = JsonSerializer.Deserialize(json, AppJsonContext.Default.UserConfig);
                if (cfg != null)
                {
                    cfg.LoadedFrom = path;
                    return cfg;
                }
            }
            catch (JsonException ex)
            {
                if (!quiet)
                    Console.Error.WriteLine($"[WARNING] Config file '{path}' has invalid JSON: {ex.Message}");
            }
            catch (IOException ex)
            {
                if (!quiet)
                    Console.Error.WriteLine($"[WARNING] Could not read config '{path}': {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                if (!quiet)
                    Console.Error.WriteLine($"[WARNING] Permission denied reading config '{path}': {ex.Message}");
            }
        }

        return new UserConfig();
    }

    /// <summary>Persist the config. Writes to <see cref="LoadedFrom"/> if set, else user-home.</summary>
    public void Save(string? explicitPath = null)
    {
        var path = explicitPath ?? LoadedFrom ?? DefaultPath;
        try
        {
            var json = JsonSerializer.Serialize(this, AppJsonContext.Default.UserConfig);
            File.WriteAllText(path, json);
            SetRestrictivePermissions(path);
            LoadedFrom = path;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"[WARNING] Could not save config '{path}': {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.Error.WriteLine($"[WARNING] Permission denied saving config '{path}': {ex.Message}");
        }
    }

    /// <summary>Resolve a model name through the alias map (FR-010). Literal deployments pass through.</summary>
    public string? ResolveModel(string? aliasOrDeployment)
    {
        if (string.IsNullOrWhiteSpace(aliasOrDeployment)) return null;
        return Models.TryGetValue(aliasOrDeployment, out var deployment) ? deployment : aliasOrDeployment;
    }

    /// <summary>Smart default (FR-010): if env unset, use <see cref="DefaultModel"/> alias → deployment.</summary>
    public string? ResolveSmartDefault()
    {
        if (string.IsNullOrWhiteSpace(DefaultModel)) return null;
        return Models.TryGetValue(DefaultModel, out var deployment) ? deployment : DefaultModel;
    }

    /// <summary>FR-009: set a dotted config key (e.g. "defaults.temperature", "default_model", "models.fast").</summary>
    public bool SetKey(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        var parts = key.Split('.', 2);
        switch (parts[0])
        {
            case "default_model":
                DefaultModel = value;
                return true;
            case "models" when parts.Length == 2:
                Models[parts[1]] = value;
                return true;
            case "defaults" when parts.Length == 2:
                return Defaults.SetKey(parts[1], value);
            default:
                return false;
        }
    }

    /// <summary>FR-009: read a dotted config key as a string. Returns null if not set.</summary>
    public string? GetKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var parts = key.Split('.', 2);
        return parts[0] switch
        {
            "default_model" => DefaultModel,
            "models" when parts.Length == 2 => Models.TryGetValue(parts[1], out var v) ? v : null,
            "defaults" when parts.Length == 2 => Defaults.GetKey(parts[1]),
            _ => null,
        };
    }

    /// <summary>FR-009: enumerate all keys as "key=value" lines (stable order).</summary>
    public IEnumerable<string> ListKeys()
    {
        if (!string.IsNullOrEmpty(DefaultModel))
            yield return $"default_model={DefaultModel}";
        foreach (var kv in Models.OrderBy(k => k.Key, StringComparer.Ordinal))
            yield return $"models.{kv.Key}={kv.Value}";
        foreach (var line in Defaults.ListKeys())
            yield return $"defaults.{line}";
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
            // Best-effort.
        }
    }
}

/// <summary>FR-003 user-preference defaults. Nullable = only override when explicitly set.</summary>
internal sealed class UserDefaults
{
    [JsonPropertyName("temperature")]
    public float? Temperature { get; set; }

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("timeout_seconds")]
    public int? TimeoutSeconds { get; set; }

    [JsonPropertyName("system_prompt")]
    public string? SystemPrompt { get; set; }

    public bool SetKey(string key, string value)
    {
        switch (key)
        {
            case "temperature":
                if (float.TryParse(value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var t))
                { Temperature = t; return true; }
                return false;
            case "max_tokens":
                if (int.TryParse(value, out var mt)) { MaxTokens = mt; return true; }
                return false;
            case "timeout_seconds":
                if (int.TryParse(value, out var ts)) { TimeoutSeconds = ts; return true; }
                return false;
            case "system_prompt":
                SystemPrompt = value;
                return true;
            default:
                return false;
        }
    }

    public string? GetKey(string key) => key switch
    {
        "temperature" => Temperature?.ToString(System.Globalization.CultureInfo.InvariantCulture),
        "max_tokens" => MaxTokens?.ToString(System.Globalization.CultureInfo.InvariantCulture),
        "timeout_seconds" => TimeoutSeconds?.ToString(System.Globalization.CultureInfo.InvariantCulture),
        "system_prompt" => SystemPrompt,
        _ => null,
    };

    public IEnumerable<string> ListKeys()
    {
        if (Temperature.HasValue)
            yield return $"temperature={Temperature.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        if (MaxTokens.HasValue) yield return $"max_tokens={MaxTokens.Value}";
        if (TimeoutSeconds.HasValue) yield return $"timeout_seconds={TimeoutSeconds.Value}";
        if (!string.IsNullOrEmpty(SystemPrompt)) yield return $"system_prompt={SystemPrompt}";
    }
}
