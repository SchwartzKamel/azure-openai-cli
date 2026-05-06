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
    IReadOnlyDictionary<string, string?> Env)
{
    // ── S03E28 -- The Persona, Multi-Provider (Kramer) ────────────────────
    // Per-persona provider/model pin. When a persona is invoked AND the
    // persona declares a `provider` and/or `model` field in .squad.json,
    // those values flow in through these init-only properties. The Persona
    // rung sits between profile and default in the precedence ladder:
    //
    //   cli > env > profile > persona > default
    //
    // Rationale: invoking `--persona kramer` is intent for THIS persona's
    // setup, so it should beat the binary's heuristic default; but profile,
    // env, and CLI flags are explicit user intent for THIS invocation and
    // continue to win. Setting any of these to null is a no-op (the rung
    // is skipped — preserves bit-exact behaviour for non-persona callers).
    //
    // PersonaName is the source-label component (e.g. "kramer") so the
    // resolved Source field reads "persona:kramer:provider" /
    // "persona:kramer:model" — every other rung labels its origin, this
    // one too.

    /// <summary>Active persona name (used for source labels). Null when no persona is invoked.</summary>
    public string? PersonaName { get; init; }

    /// <summary>Persona-pinned provider name (must be a known provider). Null skips the persona-provider rung.</summary>
    public string? PersonaProvider { get; init; }

    /// <summary>Persona-pinned model. Null skips the persona-model rung.</summary>
    public string? PersonaModel { get; init; }
}

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
        "azure", "foundry", "openai", "groq", "together", "cloudflare", "llamacpp",
    };

    // Compat preset name -> ApiKeyEnvVar. Mirrors OpenAiCompatAdapter.BuiltIn
    // without taking a reference (keeps Preferences.cs free of a dependency
    // on the adapter file, which e18 may be reshaping concurrently).
    //
    // S03E22 (ADR-011) note: this table is no longer the source of truth for
    // the *default* provider rung. Default selection now keys off
    // AZ_AI_<PRESET>_ENDPOINT envs (rungs 2-5) and AZUREOPENAIAPI presence
    // (rung 1), not API-key env presence. The table stays for KnownProviders
    // bookkeeping and for the S03E28 squad-validation accessors below.
    private static readonly (string Preset, string ApiKeyEnv)[] CompatPresets =
    {
        ("openai",     "OPENAI_API_KEY"),
        ("groq",       "GROQ_API_KEY"),
        ("together",   "TOGETHER_API_KEY"),
        ("cloudflare", "CLOUDFLARE_API_TOKEN"),
        // S03E17 *The Server* (file slot 21): llamacpp listed for parity.
        // Note (kramer-2026-05-S-2 ack): default-provider auto-detect via
        // this row only fires when AZ_AI_LLAMACPP_API_KEY is set, which is
        // unusual for llama-server (auth is opt-in on the server side). In
        // practice users opt in to llamacpp via --provider llamacpp or
        // AZ_AI_COMPAT_MODELS=llamacpp:<model>; this row is here so the
        // mirror with OpenAiCompatAdapter.BuiltIn stays one-to-one and a
        // future preferences.json default_provider knob can rely on the
        // full set without a special-case.
        ("llamacpp",   "AZ_AI_LLAMACPP_API_KEY"),
    };

    // S03E22 -- The Default (ADR-011). Local-runtime preset -> default
    // loopback port. URL-string match only; no socket is opened. Live
    // probing stays in ProviderDoctor (S03E15). 2026-05 baseline; expand
    // through ADR-011 amendment, not silent edits.
    private static readonly (string Preset, int Port)[] LocalLoopbackPorts =
    {
        ("ollama",   11434),
        ("llamacpp", 8080),
        ("lmstudio", 1234),
    };

    // Loopback host strings recognised by the local-detected rung. Match is
    // OrdinalIgnoreCase against Uri.Host.
    private static readonly string[] LoopbackHosts = { "localhost", "127.0.0.1", "::1" };

    // S03E28 -- The Persona, Multi-Provider (Kramer). Public accessors so
    // SquadConfig.Validate() and the Squad invocation site can reuse the
    // same source-of-truth set this resolver maintains. No duplicated
    // provider lists -- one canonical home.

    /// <summary>
    /// True if <paramref name="name"/> is one of the providers this binary
    /// can dispatch to natively (Azure / Foundry / one of the OpenAI-compat
    /// presets). Used for .squad.json validation (S03E28) so a persona
    /// pinning an unknown provider is rejected at config-load time.
    /// </summary>
    public static bool IsKnownProvider(string? name) =>
        !string.IsNullOrWhiteSpace(name)
        && KnownProviders.Any(p => string.Equals(p, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Comma-separated list of known providers, lowercased, for error
    /// messages. Stable order (matches <see cref="KnownProviders"/>).
    /// </summary>
    public static string KnownProvidersList() => string.Join(", ", KnownProviders);

    /// <summary>
    /// Returns the environment variable that holds credentials for the named
    /// provider, or null for unknown providers. Used by the Squad invocation
    /// site (S03E28) to detect "pinned provider but creds missing" and warn
    /// + fall through to the global default chain.
    /// </summary>
    public static string? GetCredEnvVarName(string? provider)
    {
        if (string.IsNullOrWhiteSpace(provider)) return null;
        if (string.Equals(provider, "azure", StringComparison.OrdinalIgnoreCase))
            return "AZUREOPENAIAPI";
        if (string.Equals(provider, "foundry", StringComparison.OrdinalIgnoreCase))
            return "AZURE_FOUNDRY_KEY";
        foreach (var (preset, keyEnv) in CompatPresets)
        {
            if (string.Equals(preset, provider, StringComparison.OrdinalIgnoreCase))
                return keyEnv;
        }
        return null;
    }

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
        else if (!string.IsNullOrWhiteSpace(inputs.PersonaProvider))
        {
            // ── S03E28 -- The Persona, Multi-Provider (Kramer) ──────────
            // Persona rung: above default, below profile. If the operator
            // pinned a provider on a persona AND no higher rung resolved,
            // honour the pin. The PersonaName is required for a stable
            // source label (Costanza/Kramer/Newman -- every persona invocation
            // is observable via --config show + telemetry).
            provider = inputs.PersonaProvider!.Trim();
            providerSource = "persona:" + (inputs.PersonaName ?? "?") + ":provider";
        }
        else
        {
            // S03E22 -- The Default (ADR-011). The default heuristic returns
            // an optional warning (tie-break case) which we fold into the
            // outcome's Warnings list so --config show / chat-loop stderr
            // can surface it without duplicating the algorithm.
            string? defaultWarning;
            (provider, providerSource, defaultWarning) = ResolveDefaultProvider(inputs.Env);
            if (defaultWarning != null) warnings.Add(defaultWarning);
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
        else if (!string.IsNullOrWhiteSpace(inputs.PersonaModel))
        {
            // S03E28: persona-rung model pin -- above default, below profile.
            model = inputs.PersonaModel!.Trim();
            modelSource = "persona:" + (inputs.PersonaName ?? "?") + ":model";
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

    /// <summary>
    /// S03E22 -- The Default (ADR-011). Six-rung deterministic heuristic
    /// for the default provider when CLI / env / profile rails all miss.
    /// Pure: only consults the env snapshot. No socket probes, no DNS --
    /// the local-detected rung reads URL strings only (ProviderDoctor owns
    /// the live probe). Returns an optional Warning string for the tie-break
    /// case; null otherwise. Never throws -- the worst case is the
    /// fallback rung, which fails closed at BuildChatClient.
    /// </summary>
    private static (string Provider, string Source, string? Warning) ResolveDefaultProvider(IReadOnlyDictionary<string, string?> env)
    {
        // Rung 1: Azure endpoint + API key both present -> default:azure.
        var hasAzureEndpoint = !string.IsNullOrWhiteSpace(GetEnv(env, "AZUREOPENAIENDPOINT"));
        var hasAzureKey = !string.IsNullOrWhiteSpace(GetEnv(env, "AZUREOPENAIAPI"));
        if (hasAzureEndpoint && hasAzureKey)
        {
            return ("azure", "default:azure", null);
        }

        // Discover all AZ_AI_<PRESET>_ENDPOINT envs in the snapshot. Sorted
        // alphabetically by preset (lowercased) so rung 3 + tie-break are
        // deterministic regardless of dictionary iteration order.
        var presetEndpoints = DiscoverPresetEndpoints(env);

        // Rung 2: exactly one preset endpoint set -> default:<preset>.
        if (presetEndpoints.Count == 1)
        {
            var only = presetEndpoints[0];
            return (only.Preset, "default:" + only.Preset, null);
        }

        // Rung 3: multi-preset + AZ_AI_LOCAL_PROVIDERS=1 (strict) + URL
        // matches a known loopback host:port -> default:<preset>:local-detected.
        var localOptIn = string.Equals(GetEnv(env, "AZ_AI_LOCAL_PROVIDERS"), "1", StringComparison.Ordinal);
        if (presetEndpoints.Count >= 2 && localOptIn)
        {
            foreach (var (preset, endpoint) in presetEndpoints)
            {
                if (MatchesLocalLoopback(preset, endpoint))
                {
                    return (preset, "default:" + preset + ":local-detected", null);
                }
            }
        }

        // Rung 4: OPENAI_API_KEY present -> default:openai.
        if (!string.IsNullOrWhiteSpace(GetEnv(env, "OPENAI_API_KEY")))
        {
            return ("openai", "default:openai", null);
        }

        // Rung 5 (tie-break): >= 2 preset endpoints with no other signal.
        // Pick alphabetically first preset and emit a warning so the
        // operator knows the default was ambiguous.
        if (presetEndpoints.Count >= 2)
        {
            var first = presetEndpoints[0].Preset;
            return (first, "default:" + first,
                "multiple-presets-no-cli-no-profile-no-env-pin: "
                + "two or more AZ_AI_<PRESET>_ENDPOINT envs are set with no AZ_PROVIDER, "
                + "--provider, or profile pin. Picked alphabetically first preset '" + first
                + "'. Pin one to silence this warning.");
        }

        // Rung 6: nothing matched. Azure-as-fallback. BuildChatClient will
        // surface the missing-credentials error with its existing message.
        return ("azure", "default:azure:fallback", null);
    }

    /// <summary>
    /// Walk the env snapshot for keys matching <c>AZ_AI_*_ENDPOINT</c> with
    /// non-empty values. Returns (preset, endpoint) pairs sorted alphabetically
    /// by lowercased preset name -- the deterministic order used by the
    /// local-detected rung and the tie-break.
    /// </summary>
    private static List<(string Preset, string Endpoint)> DiscoverPresetEndpoints(IReadOnlyDictionary<string, string?> env)
    {
        const string prefix = "AZ_AI_";
        const string suffix = "_ENDPOINT";
        var found = new List<(string Preset, string Endpoint)>();
        foreach (var kvp in env)
        {
            var key = kvp.Key;
            if (string.IsNullOrWhiteSpace(kvp.Value)) continue;
            if (key.Length <= prefix.Length + suffix.Length) continue;
            if (!key.StartsWith(prefix, StringComparison.Ordinal)) continue;
            if (!key.EndsWith(suffix, StringComparison.Ordinal)) continue;
            // Filter out AZ_AI_COMPAT_MODELS, AZ_AI_LOCAL_PROVIDERS, etc. by
            // shape: must be exactly AZ_AI_<NAME>_ENDPOINT with NAME non-empty.
            var presetRaw = key.Substring(prefix.Length, key.Length - prefix.Length - suffix.Length);
            if (presetRaw.Length == 0) continue;
            // Lowercase for stability (env-var convention is upper, label is lower).
            var preset = presetRaw.ToLowerInvariant();
            found.Add((preset, kvp.Value!));
        }
        // Stable alphabetical order by preset name -- ties broken by ordinal.
        found.Sort((a, b) => string.CompareOrdinal(a.Preset, b.Preset));
        return found;
    }

    /// <summary>
    /// String-only check: does <paramref name="endpoint"/> parse to a known
    /// loopback host on the canonical port for <paramref name="preset"/>?
    /// No socket is opened. ProviderDoctor (S03E15) keeps the live probe.
    /// </summary>
    private static bool MatchesLocalLoopback(string preset, string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)) return false;
        var host = uri.Host;
        var isLoopback = false;
        foreach (var lh in LoopbackHosts)
        {
            if (string.Equals(host, lh, StringComparison.OrdinalIgnoreCase))
            {
                isLoopback = true;
                break;
            }
        }
        if (!isLoopback) return false;
        foreach (var (p, port) in LocalLoopbackPorts)
        {
            if (string.Equals(preset, p, StringComparison.OrdinalIgnoreCase) && uri.Port == port)
            {
                return true;
            }
        }
        return false;
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
