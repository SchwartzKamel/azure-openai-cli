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

// ─────────────────────────────────────────────────────────────────────────────
// S03E20 -- The Switch (Costanza). The precedence chain finally has a name
// AND an algorithm. Pure function. No I/O, no Console writes, no Environment
// reads -- everything funnels through ResolutionInputs.Env so tests can stamp
// a deterministic environment without touching process state.
//
// Precedence (cli > env > preferences.json > built-in default):
//
//   PROVIDER            PROFILE              MODEL
//   --------            -------              ---------
//   --provider          --profile            --model
//   AZ_PROVIDER         AZ_PROFILE           AZ_MODEL
//   profile.provider    (no further step)    profile.model
//   default heuristic   --                   AZUREOPENAIMODEL[0] / compat[0]
//                                            default fallback
//
// Profile is the only optional rail: if no --profile and no AZ_PROFILE, the
// profile lookup is skipped entirely (steps 3 + 4 of provider / step 3 of
// model become no-ops). The default heuristic for provider:
//
//   1. AZUREOPENAIENDPOINT set -> azure
//   2. OPENAI_API_KEY set      -> openai
//   3. first compat preset whose ApiKeyEnvVar is set
//   4. else -> InvalidOperationException with the actionable list
//
// ADR-009 documents the original generalised order; this is its codification.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Inputs to <see cref="Preferences.Resolve"/>. Every signal that influences
/// the resolution lives here; callers must not pull from Environment behind
/// the back of the resolver. <c>Env</c> is the snapshot of environment
/// variables -- values may be null for "not set".
/// </summary>
internal sealed record ResolutionInputs(
    string? CliProvider,
    string? CliProfile,
    string? CliModel,
    IReadOnlyDictionary<string, string?> Env);

/// <summary>
/// Outcome of <see cref="Preferences.Resolve"/>. <c>Source</c> is the
/// provider source label ("cli" / "env:AZ_PROVIDER" / "profile:&lt;name&gt;:provider"
/// / "default:azure" / "default:openai" / "default:&lt;preset&gt;") for backwards
/// compatibility with the brief's documented contract; <c>ProviderSource</c>,
/// <c>ModelSource</c>, and <c>ProfileSource</c> are the per-field labels
/// surfaced by <c>--config show</c>. <c>Warnings</c> carries non-fatal
/// advisories (e.g. profile.provider mismatch with AZ_AI_COMPAT_MODELS); the
/// caller is expected to forward them to stderr unless <c>--raw</c> /
/// <c>--json</c> is active.
/// </summary>
internal sealed record ResolutionOutcome(
    string Provider,
    string Model,
    string? ProfileName,
    string Source,
    string ProviderSource,
    string ModelSource,
    string? ProfileSource,
    IReadOnlyList<string> Warnings);

internal static class PreferencesResolver
{
    // The set of provider names this binary natively understands. Kept in
    // step with OpenAiCompatAdapter.BuiltIn + the two first-class providers
    // (azure, foundry). Used for the mismatch-warning heuristic only -- the
    // actual dispatch path lives in BuildChatClient (e18 territory) and is
    // NOT touched here.
    private static readonly string[] KnownProviders =
    {
        "azure", "foundry", "openai", "groq", "together", "cloudflare",
    };

    // Compat preset name -> ApiKeyEnvVar. Mirrors OpenAiCompatAdapter.BuiltIn
    // without taking a reference (keeps Preferences.cs free of a dependency
    // on the adapter file, which e18 may be reshaping concurrently).
    private static readonly (string Preset, string ApiKeyEnv)[] CompatPresets =
    {
        ("openai",     "OPENAI_API_KEY"),
        ("groq",       "GROQ_API_KEY"),
        ("together",   "TOGETHER_API_KEY"),
        ("cloudflare", "CLOUDFLARE_API_TOKEN"),
    };

    /// <summary>
    /// Resolve the (provider, model, profile) triple per the documented
    /// precedence chain. Pure: no I/O, no environment reads, no Console
    /// writes. Throws <see cref="InvalidOperationException"/> if every path
    /// fails (e.g. no CLI flag, no env, no profile, no default heuristic
    /// match). Throws the same exception type with a friendly listing if
    /// <c>inputs.CliProfile</c> names a profile that does not exist in
    /// <paramref name="prefs"/>.
    /// </summary>
    public static ResolutionOutcome Resolve(Preferences prefs, ResolutionInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(prefs);
        ArgumentNullException.ThrowIfNull(inputs);

        var warnings = new List<string>();

        // ── Profile (optional rail) ──────────────────────────────────────
        ProfileEntry? profile = null;
        string? profileName = null;
        string? profileSource = null;

        if (!string.IsNullOrWhiteSpace(inputs.CliProfile))
        {
            profileName = inputs.CliProfile.Trim();
            if (!prefs.Profiles.TryGetValue(profileName, out profile))
            {
                throw new InvalidOperationException(BuildMissingProfileMessage(profileName, prefs));
            }
            profileSource = "cli";
        }
        else
        {
            var envProfile = GetEnv(inputs.Env, "AZ_PROFILE");
            if (!string.IsNullOrWhiteSpace(envProfile))
            {
                profileName = envProfile.Trim();
                if (!prefs.Profiles.TryGetValue(profileName, out profile))
                {
                    // Env-named profile that doesn't exist: warn and fall
                    // through (no fatal error -- the env may legitimately
                    // outlive a profile rename).
                    warnings.Add(
                        "AZ_PROFILE='" + profileName + "' does not match any profile in preferences.json; ignoring.");
                    profileName = null;
                }
                else
                {
                    profileSource = "env:AZ_PROFILE";
                }
            }
        }

        // ── Provider ─────────────────────────────────────────────────────
        string provider;
        string providerSource;

        if (!string.IsNullOrWhiteSpace(inputs.CliProvider))
        {
            provider = inputs.CliProvider.Trim();
            providerSource = "cli";
        }
        else if (GetEnv(inputs.Env, "AZ_PROVIDER") is { } envProv && !string.IsNullOrWhiteSpace(envProv))
        {
            provider = envProv.Trim();
            providerSource = "env:AZ_PROVIDER";
        }
        else if (profile != null && !string.IsNullOrWhiteSpace(profile.Provider))
        {
            provider = profile.Provider.Trim();
            providerSource = "profile:" + profileName + ":provider";
        }
        else
        {
            (provider, providerSource) = ResolveDefaultProvider(inputs.Env);
        }

        // ── Model ────────────────────────────────────────────────────────
        string? model = null;
        string modelSource = "";

        if (!string.IsNullOrWhiteSpace(inputs.CliModel))
        {
            model = inputs.CliModel.Trim();
            modelSource = "cli";
        }
        else if (GetEnv(inputs.Env, "AZ_MODEL") is { } envModel && !string.IsNullOrWhiteSpace(envModel))
        {
            model = envModel.Trim();
            modelSource = "env:AZ_MODEL";
        }
        else if (profile != null && !string.IsNullOrWhiteSpace(profile.Model))
        {
            model = profile.Model!.Trim();
            modelSource = "profile:" + profileName + ":model";
        }
        else
        {
            (model, modelSource) = ResolveDefaultModel(provider, inputs.Env);
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException(
                "Could not determine a model: no --model flag, no AZ_MODEL env, no profile.model, "
                + "and no provider-default model for provider '" + provider + "'. "
                + "Set --model, AZ_MODEL, or AZUREOPENAIMODEL (azure) / AZ_AI_COMPAT_MODELS (compat).");
        }

        // ── Mismatch warning (profile.provider vs AZ_AI_COMPAT_MODELS) ──
        // If a profile pinned the provider but AZ_AI_COMPAT_MODELS routes the
        // resolved model to a different preset, emit a non-fatal warning.
        // Profile wins per precedence (the brief is explicit on this).
        if (profile != null && providerSource.StartsWith("profile:", StringComparison.Ordinal))
        {
            var compatRaw = GetEnv(inputs.Env, "AZ_AI_COMPAT_MODELS");
            if (!string.IsNullOrWhiteSpace(compatRaw))
            {
                var compatPreset = TryLookupCompatPreset(compatRaw, model!);
                if (compatPreset != null
                    && !string.Equals(compatPreset, provider, StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add(
                        "profile '" + profileName + "' selects provider '" + provider
                        + "' but AZ_AI_COMPAT_MODELS routes model '" + model
                        + "' to preset '" + compatPreset + "'. Profile wins; check the profile if this is wrong.");
                }
            }
        }

        return new ResolutionOutcome(
            Provider: provider,
            Model: model!,
            ProfileName: profileName,
            Source: providerSource,
            ProviderSource: providerSource,
            ModelSource: modelSource,
            ProfileSource: profileSource,
            Warnings: warnings);
    }

    private static string? GetEnv(IReadOnlyDictionary<string, string?> env, string key)
    {
        return env.TryGetValue(key, out var v) ? v : null;
    }

    private static (string Provider, string Source) ResolveDefaultProvider(IReadOnlyDictionary<string, string?> env)
    {
        if (!string.IsNullOrWhiteSpace(GetEnv(env, "AZUREOPENAIENDPOINT")))
        {
            return ("azure", "default:azure");
        }
        if (!string.IsNullOrWhiteSpace(GetEnv(env, "OPENAI_API_KEY")))
        {
            return ("openai", "default:openai");
        }
        foreach (var (preset, keyEnv) in CompatPresets)
        {
            if (!string.IsNullOrWhiteSpace(GetEnv(env, keyEnv)))
            {
                return (preset, "default:" + preset);
            }
        }
        var hint = string.Join(", ", KnownProviders);
        throw new InvalidOperationException(
            "No provider could be resolved: no --provider flag, no AZ_PROVIDER env, no profile pin, "
            + "and no credentials present for any built-in provider. "
            + "Set AZUREOPENAIENDPOINT for Azure, OPENAI_API_KEY for OpenAI, "
            + "or one of GROQ_API_KEY / TOGETHER_API_KEY / CLOUDFLARE_API_TOKEN. "
            + "Known providers: [" + hint + "].");
    }

    private static (string? Model, string Source) ResolveDefaultModel(string provider, IReadOnlyDictionary<string, string?> env)
    {
        if (string.Equals(provider, "azure", StringComparison.OrdinalIgnoreCase)
         || string.Equals(provider, "foundry", StringComparison.OrdinalIgnoreCase))
        {
            var raw = GetEnv(env, "AZUREOPENAIMODEL");
            if (!string.IsNullOrWhiteSpace(raw))
            {
                var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length > 0)
                {
                    return (parts[0], "env:AZUREOPENAIMODEL[0]");
                }
            }
        }
        else
        {
            var raw = GetEnv(env, "AZ_AI_COMPAT_MODELS");
            if (!string.IsNullOrWhiteSpace(raw))
            {
                // First entry whose preset matches the resolved provider.
                var entries = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var entry in entries)
                {
                    var colon = entry.IndexOf(':');
                    if (colon <= 0 || colon == entry.Length - 1) continue;
                    var preset = entry.Substring(0, colon).Trim();
                    var modelPart = entry.Substring(colon + 1).Trim();
                    if (preset.Length == 0 || modelPart.Length == 0) continue;
                    if (string.Equals(preset, provider, StringComparison.OrdinalIgnoreCase))
                    {
                        return (modelPart, "env:AZ_AI_COMPAT_MODELS[" + preset + "]");
                    }
                }
            }
        }

        // Hardcoded fallback (matches Program.DefaultModelFallback per ADR-009).
        return ("gpt-4o-mini", "default:" + provider);
    }

    private static string? TryLookupCompatPreset(string compatRaw, string model)
    {
        var entries = compatRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var entry in entries)
        {
            var colon = entry.IndexOf(':');
            if (colon <= 0 || colon == entry.Length - 1) continue;
            var preset = entry.Substring(0, colon).Trim();
            var modelPart = entry.Substring(colon + 1).Trim();
            if (string.Equals(modelPart, model, StringComparison.OrdinalIgnoreCase))
            {
                return preset;
            }
        }
        return null;
    }

    private static string BuildMissingProfileMessage(string requested, Preferences prefs)
    {
        var available = prefs.Profiles.Keys
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();
        if (available.Count == 0)
        {
            return "Profile '" + requested + "' was requested via --profile but preferences.json "
                + "contains no profiles. Add a profile under \"profiles\" in "
                + (prefs.LoadedFrom ?? "preferences.json")
                + " or remove the --profile flag.";
        }
        return "Profile '" + requested + "' not found in preferences.json. "
            + "Available profiles: [" + string.Join(", ", available) + "]. "
            + "Check spelling or add the profile to "
            + (prefs.LoadedFrom ?? "preferences.json") + ".";
    }
}
