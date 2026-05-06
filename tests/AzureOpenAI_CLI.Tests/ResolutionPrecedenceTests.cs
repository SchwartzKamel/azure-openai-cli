using System.Collections.Generic;

namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// S03E20 -- The Switch (Costanza). Pure unit tests for
/// <see cref="PreferencesResolver.Resolve"/>. The resolver is a pure function
/// of (Preferences, ResolutionInputs); these tests exercise every rung of
/// the precedence ladder (CLI > env > profile > default) for both the
/// provider and the model rails, plus the optional profile rail and the
/// edge cases the brief calls out (mismatch warning, missing profile,
/// empty profile).
///
/// Joins ConsoleCapture purely as a discipline marker -- no test in this
/// file actually writes to Console -- but Preferences.Load() in the
/// MismatchEnv tests reads XDG_CONFIG_HOME, and serializing keeps that
/// race-free with the rest of the suite.
/// </summary>
[Collection("ConsoleCapture")]
public class ResolutionPrecedenceTests
{
    private static IReadOnlyDictionary<string, string?> Env(params (string Key, string? Value)[] pairs)
    {
        var d = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (k, v) in pairs) d[k] = v;
        return d;
    }

    private static Preferences PrefsWith(params (string Name, string Provider, string? Model)[] profiles)
    {
        var p = new Preferences { LoadedFrom = "/test/preferences.json" };
        foreach (var (n, prov, m) in profiles)
        {
            p.Profiles[n] = new ProfileEntry { Provider = prov, Model = m };
        }
        return p;
    }

    // ── Provider precedence ladder ────────────────────────────────────────

    [Fact]
    public void Provider_CliBeatsEverything()
    {
        var prefs = PrefsWith(("work", "azure", "gpt-4o"));
        var inputs = new ResolutionInputs(
            CliProvider: "groq",
            CliProfile: "work",
            CliModel: null,
            Env: Env(("AZ_PROVIDER", "openai"), ("AZUREOPENAIENDPOINT", "https://x")));

        var r = PreferencesResolver.Resolve(prefs, inputs);

        Assert.Equal("groq", r.Provider);
        Assert.Equal("cli", r.ProviderSource);
        Assert.Equal("cli", r.Source);
    }

    [Fact]
    public void Provider_EnvBeatsProfile()
    {
        var prefs = PrefsWith(("work", "azure", null));
        var inputs = new ResolutionInputs(
            CliProvider: null,
            CliProfile: "work",
            CliModel: "gpt-4o",
            Env: Env(("AZ_PROVIDER", "groq")));

        var r = PreferencesResolver.Resolve(prefs, inputs);

        Assert.Equal("groq", r.Provider);
        Assert.Equal("env:AZ_PROVIDER", r.ProviderSource);
    }

    [Fact]
    public void Provider_EnvBeatsDefault()
    {
        var inputs = new ResolutionInputs(
            CliProvider: null, CliProfile: null, CliModel: "gpt-4o",
            Env: Env(("AZ_PROVIDER", "together"), ("AZUREOPENAIENDPOINT", "https://x")));

        var r = PreferencesResolver.Resolve(new Preferences(), inputs);

        Assert.Equal("together", r.Provider);
        Assert.Equal("env:AZ_PROVIDER", r.ProviderSource);
    }

    [Fact]
    public void Provider_ProfileBeatsDefault()
    {
        var prefs = PrefsWith(("work", "groq", "llama-3.1"));
        var inputs = new ResolutionInputs(
            CliProvider: null, CliProfile: "work", CliModel: null,
            Env: Env(("AZUREOPENAIENDPOINT", "https://x"), ("GROQ_API_KEY", "gsk_x")));

        var r = PreferencesResolver.Resolve(prefs, inputs);

        Assert.Equal("groq", r.Provider);
        Assert.Equal("profile:work:provider", r.ProviderSource);
    }

    [Fact]
    public void Provider_AzProfileEnvResolvesProfileProvider()
    {
        var prefs = PrefsWith(("ci", "together", "mixtral"));
        var inputs = new ResolutionInputs(
            CliProvider: null, CliProfile: null, CliModel: null,
            Env: Env(("AZ_PROFILE", "ci"), ("TOGETHER_API_KEY", "k")));

        var r = PreferencesResolver.Resolve(prefs, inputs);

        Assert.Equal("together", r.Provider);
        Assert.Equal("profile:ci:provider", r.ProviderSource);
        Assert.Equal("ci", r.ProfileName);
        Assert.Equal("env:AZ_PROFILE", r.ProfileSource);
    }

    [Fact]
    public void Provider_DefaultIsAzureWhenEndpointSet()
    {
        // ADR-011 rung 1 requires both AZUREOPENAIENDPOINT and AZUREOPENAIAPI.
        var inputs = new ResolutionInputs(null, null, "gpt-4o",
            Env(("AZUREOPENAIENDPOINT", "https://x.cognitiveservices.azure.com/"),
                ("AZUREOPENAIAPI", "k")));

        var r = PreferencesResolver.Resolve(new Preferences(), inputs);

        Assert.Equal("azure", r.Provider);
        Assert.Equal("default:azure", r.ProviderSource);
    }

    [Fact]
    public void Provider_DefaultIsOpenAiWhenOnlyOpenAiKeySet()
    {
        var inputs = new ResolutionInputs(null, null, "gpt-4o-mini",
            Env(("OPENAI_API_KEY", "sk-x")));

        var r = PreferencesResolver.Resolve(new Preferences(), inputs);

        Assert.Equal("openai", r.Provider);
        Assert.Equal("default:openai", r.ProviderSource);
    }

    [Fact]
    public void Provider_DefaultFallsBackToFirstCompatPresetWithKey()
    {
        // ADR-011 rung 2: exactly one AZ_AI_<PRESET>_ENDPOINT set -> default:<preset>.
        // (Pre-ADR-011 this rung keyed off API-key envs; semantics now match
        // the documented heuristic.)
        var inputs = new ResolutionInputs(null, null, "llama",
            Env(("AZ_AI_GROQ_ENDPOINT", "https://api.groq.com/openai/v1"),
                ("GROQ_API_KEY", "gsk_x")));

        var r = PreferencesResolver.Resolve(new Preferences(), inputs);

        Assert.Equal("groq", r.Provider);
        Assert.Equal("default:groq", r.ProviderSource);
    }

    [Fact]
    public void Provider_NoSignalsReturnsAzureFallback()
    {
        // ADR-011 rung 6: no signals at all -> default:azure:fallback.
        // BuildChatClient surfaces the actionable missing-creds error
        // downstream; the resolver no longer throws.
        var inputs = new ResolutionInputs(null, null, null, Env());

        var r = PreferencesResolver.Resolve(new Preferences(), inputs);

        Assert.Equal("azure", r.Provider);
        Assert.Equal("default:azure:fallback", r.ProviderSource);
    }

    // ── Model precedence ladder ───────────────────────────────────────────

    [Fact]
    public void Model_CliBeatsEverything()
    {
        var prefs = PrefsWith(("work", "azure", "gpt-4o"));
        var inputs = new ResolutionInputs(
            CliProvider: null, CliProfile: "work", CliModel: "o1-mini",
            Env: Env(("AZ_MODEL", "from-env"), ("AZUREOPENAIMODEL", "from-azure")));

        var r = PreferencesResolver.Resolve(prefs, inputs);

        Assert.Equal("o1-mini", r.Model);
        Assert.Equal("cli", r.ModelSource);
    }

    [Fact]
    public void Model_AzModelEnvBeatsProfile()
    {
        var prefs = PrefsWith(("work", "azure", "gpt-4o"));
        var inputs = new ResolutionInputs(
            null, "work", null,
            Env(("AZ_MODEL", "gpt-5.4")));

        var r = PreferencesResolver.Resolve(prefs, inputs);

        Assert.Equal("gpt-5.4", r.Model);
        Assert.Equal("env:AZ_MODEL", r.ModelSource);
    }

    [Fact]
    public void Model_FromProfileWhenNoCliOrEnv()
    {
        var prefs = PrefsWith(("work", "azure", "gpt-4o-pinned"));
        var inputs = new ResolutionInputs(
            null, "work", null,
            Env(("AZUREOPENAIENDPOINT", "https://x"), ("AZUREOPENAIMODEL", "gpt-4o-mini")));

        var r = PreferencesResolver.Resolve(prefs, inputs);

        Assert.Equal("gpt-4o-pinned", r.Model);
        Assert.Equal("profile:work:model", r.ModelSource);
    }

    [Fact]
    public void Model_AzureProviderDefaultsToAzureopenaiModelFirstEntry()
    {
        var inputs = new ResolutionInputs(
            null, null, null,
            Env(("AZUREOPENAIENDPOINT", "https://x"),
                ("AZUREOPENAIMODEL", "gpt-5.4-nano,gpt-4o,gpt-4o-mini")));

        var r = PreferencesResolver.Resolve(new Preferences(), inputs);

        Assert.Equal("gpt-5.4-nano", r.Model);
        Assert.Equal("env:AZUREOPENAIMODEL[0]", r.ModelSource);
    }

    [Fact]
    public void Model_CompatProviderDefaultsToCompatModelsEntry()
    {
        var inputs = new ResolutionInputs(
            CliProvider: "groq", CliProfile: null, CliModel: null,
            Env: Env(("GROQ_API_KEY", "k"),
                     ("AZ_AI_COMPAT_MODELS", "groq:llama-3.1-70b,openai:gpt-4o")));

        var r = PreferencesResolver.Resolve(new Preferences(), inputs);

        Assert.Equal("llama-3.1-70b", r.Model);
        Assert.Equal("env:AZ_AI_COMPAT_MODELS[groq]", r.ModelSource);
    }

    [Fact]
    public void Model_HardcodedFallbackWhenNothingApplies()
    {
        var inputs = new ResolutionInputs(
            null, null, null,
            Env(("AZUREOPENAIENDPOINT", "https://x")));

        var r = PreferencesResolver.Resolve(new Preferences(), inputs);

        Assert.Equal("gpt-4o-mini", r.Model);
        Assert.Equal("default:azure", r.ModelSource);
    }

    [Fact]
    public void Model_ProfileWithoutModelFallsThroughToProviderDefault()
    {
        var prefs = PrefsWith(("work", "azure", null));
        var inputs = new ResolutionInputs(
            null, "work", null,
            Env(("AZUREOPENAIMODEL", "gpt-4o,gpt-4o-mini")));

        var r = PreferencesResolver.Resolve(prefs, inputs);

        Assert.Equal("gpt-4o", r.Model);
        Assert.Equal("env:AZUREOPENAIMODEL[0]", r.ModelSource);
    }

    [Fact]
    public void Model_CompatNoMatchingPresetUsesFallback()
    {
        var inputs = new ResolutionInputs(
            CliProvider: "groq", CliProfile: null, CliModel: null,
            Env: Env(("GROQ_API_KEY", "k"),
                     ("AZ_AI_COMPAT_MODELS", "openai:gpt-4o")));

        var r = PreferencesResolver.Resolve(new Preferences(), inputs);

        Assert.Equal("gpt-4o-mini", r.Model);
        Assert.Equal("default:groq", r.ModelSource);
    }

    // ── Profile rail ──────────────────────────────────────────────────────

    [Fact]
    public void Profile_CliProfileSourceLabel()
    {
        var prefs = PrefsWith(("work", "azure", "gpt-4o"));
        var inputs = new ResolutionInputs(null, "work", null, Env());

        var r = PreferencesResolver.Resolve(prefs, inputs);

        Assert.Equal("work", r.ProfileName);
        Assert.Equal("cli", r.ProfileSource);
    }

    [Fact]
    public void Profile_NoProfileNoEnvLeavesProfileNull()
    {
        var prefs = PrefsWith(("work", "azure", "gpt-4o"));
        var inputs = new ResolutionInputs(
            null, null, null,
            Env(("AZUREOPENAIENDPOINT", "https://x")));

        var r = PreferencesResolver.Resolve(prefs, inputs);

        Assert.Null(r.ProfileName);
        Assert.Null(r.ProfileSource);
    }

    [Fact]
    public void Profile_CliProfileMissingThrowsWithAvailableList()
    {
        var prefs = PrefsWith(("work", "azure", "gpt-4o"), ("ci", "groq", "llama"));
        var inputs = new ResolutionInputs(null, "production", null, Env());

        var ex = Assert.Throws<InvalidOperationException>(
            () => PreferencesResolver.Resolve(prefs, inputs));

        Assert.Contains("'production'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Available profiles:", ex.Message, StringComparison.Ordinal);
        Assert.Contains("ci", ex.Message, StringComparison.Ordinal);
        Assert.Contains("work", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Profile_CliProfileMissingWithEmptyPreferencesGivesHint()
    {
        var prefs = new Preferences();
        var inputs = new ResolutionInputs(null, "work", null, Env());

        var ex = Assert.Throws<InvalidOperationException>(
            () => PreferencesResolver.Resolve(prefs, inputs));
        Assert.Contains("contains no profiles", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Profile_AzProfileEnvMissingFallsThroughWithWarning()
    {
        var prefs = PrefsWith(("work", "azure", "gpt-4o"));
        var inputs = new ResolutionInputs(
            null, null, "gpt-4o-mini",
            Env(("AZ_PROFILE", "production"), ("AZUREOPENAIENDPOINT", "https://x")));

        var r = PreferencesResolver.Resolve(prefs, inputs);

        Assert.Null(r.ProfileName);
        Assert.Equal("azure", r.Provider);
        Assert.Single(r.Warnings);
        Assert.Contains("AZ_PROFILE", r.Warnings[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Profile_EmptyProviderInProfileFallsThroughToDefault()
    {
        var prefs = new Preferences { LoadedFrom = "/test/p.json" };
        prefs.Profiles["work"] = new ProfileEntry { Provider = "", Model = "gpt-4o" };
        var inputs = new ResolutionInputs(
            null, "work", null,
            Env(("AZUREOPENAIENDPOINT", "https://x"), ("AZUREOPENAIAPI", "k")));

        var r = PreferencesResolver.Resolve(prefs, inputs);

        Assert.Equal("azure", r.Provider);
        Assert.Equal("default:azure", r.ProviderSource);
        Assert.Equal("gpt-4o", r.Model);
    }

    // ── Mismatch warning ──────────────────────────────────────────────────

    [Fact]
    public void Mismatch_ProfileProviderVsCompatModelsEmitsWarning()
    {
        // Profile pins azure but AZ_AI_COMPAT_MODELS routes the model to openai.
        var prefs = PrefsWith(("work", "azure", "gpt-4o"));
        var inputs = new ResolutionInputs(
            null, "work", null,
            Env(("AZUREOPENAIENDPOINT", "https://x"),
                ("AZ_AI_COMPAT_MODELS", "openai:gpt-4o")));

        var r = PreferencesResolver.Resolve(prefs, inputs);

        Assert.Equal("azure", r.Provider);
        Assert.Equal("gpt-4o", r.Model);
        Assert.Single(r.Warnings);
        Assert.Contains("Profile wins", r.Warnings[0], StringComparison.Ordinal);
        Assert.Contains("openai", r.Warnings[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Mismatch_ProfileMatchesCompatModelsNoWarning()
    {
        var prefs = PrefsWith(("work", "openai", "gpt-4o"));
        var inputs = new ResolutionInputs(
            null, "work", null,
            Env(("OPENAI_API_KEY", "sk-x"),
                ("AZ_AI_COMPAT_MODELS", "openai:gpt-4o")));

        var r = PreferencesResolver.Resolve(prefs, inputs);

        Assert.Empty(r.Warnings);
    }

    [Fact]
    public void Mismatch_CliProviderOverrideDoesNotEmitWarning()
    {
        // Mismatch detection only fires when the provider was sourced from a
        // profile -- a CLI-driven override is the operator's explicit choice.
        var prefs = PrefsWith(("work", "azure", "gpt-4o"));
        var inputs = new ResolutionInputs(
            CliProvider: "azure", CliProfile: "work", CliModel: null,
            Env: Env(("AZUREOPENAIENDPOINT", "https://x"),
                     ("AZ_AI_COMPAT_MODELS", "openai:gpt-4o")));

        var r = PreferencesResolver.Resolve(prefs, inputs);

        Assert.Empty(r.Warnings);
    }

    // ── Combined precedence sanity ────────────────────────────────────────

    [Fact]
    public void Combined_CliWinsOverEverythingAcrossAllRails()
    {
        var prefs = PrefsWith(("work", "azure", "gpt-4o"));
        var inputs = new ResolutionInputs(
            CliProvider: "groq", CliProfile: "work", CliModel: "llama-3.1-70b",
            Env: Env(("AZ_PROVIDER", "openai"),
                     ("AZ_MODEL", "gpt-4o-mini"),
                     ("AZUREOPENAIENDPOINT", "https://x")));

        var r = PreferencesResolver.Resolve(prefs, inputs);

        Assert.Equal("groq", r.Provider);
        Assert.Equal("llama-3.1-70b", r.Model);
        Assert.Equal("work", r.ProfileName);
        Assert.Equal("cli", r.ProviderSource);
        Assert.Equal("cli", r.ModelSource);
        Assert.Equal("cli", r.ProfileSource);
    }

    [Fact]
    public void Combined_EnvCascadeNoCliNoProfile()
    {
        var inputs = new ResolutionInputs(
            null, null, null,
            Env(("AZ_PROVIDER", "together"),
                ("AZ_MODEL", "mixtral-8x7b"),
                ("TOGETHER_API_KEY", "k")));

        var r = PreferencesResolver.Resolve(new Preferences(), inputs);

        Assert.Equal("together", r.Provider);
        Assert.Equal("mixtral-8x7b", r.Model);
        Assert.Equal("env:AZ_PROVIDER", r.ProviderSource);
        Assert.Equal("env:AZ_MODEL", r.ModelSource);
        Assert.Null(r.ProfileName);
    }

    [Fact]
    public void Combined_ProfileCascadeNoCliNoEnv()
    {
        var prefs = PrefsWith(("ci", "groq", "llama-3.1"));
        var inputs = new ResolutionInputs(
            null, "ci", null,
            Env(("GROQ_API_KEY", "k")));

        var r = PreferencesResolver.Resolve(prefs, inputs);

        Assert.Equal("groq", r.Provider);
        Assert.Equal("llama-3.1", r.Model);
        Assert.Equal("ci", r.ProfileName);
        Assert.Equal("profile:ci:provider", r.ProviderSource);
        Assert.Equal("profile:ci:model", r.ModelSource);
    }

    [Fact]
    public void Combined_DefaultCascadeNoUserSignals()
    {
        var inputs = new ResolutionInputs(
            null, null, null,
            Env(("AZUREOPENAIENDPOINT", "https://x"),
                ("AZUREOPENAIAPI", "k"),
                ("AZUREOPENAIMODEL", "gpt-4o")));

        var r = PreferencesResolver.Resolve(new Preferences(), inputs);

        Assert.Equal("azure", r.Provider);
        Assert.Equal("gpt-4o", r.Model);
        Assert.Equal("default:azure", r.ProviderSource);
        Assert.Equal("env:AZUREOPENAIMODEL[0]", r.ModelSource);
    }

    // ── Source string contract ────────────────────────────────────────────

    [Fact]
    public void Source_FieldEqualsProviderSource()
    {
        var inputs = new ResolutionInputs(
            "azure", null, "gpt-4o",
            Env(("AZUREOPENAIENDPOINT", "https://x")));

        var r = PreferencesResolver.Resolve(new Preferences(), inputs);

        Assert.Equal(r.ProviderSource, r.Source);
    }

    [Fact]
    public void Source_LabelsAreOrdinalLowercase()
    {
        // Source labels are stable, machine-grep-able strings: no whitespace,
        // colon-separated, ordinal-lowercase.
        var prefs = PrefsWith(("Work", "Azure", "gpt-4o"));
        var inputs = new ResolutionInputs(null, "Work", null,
            Env(("AZUREOPENAIENDPOINT", "https://x")));

        var r = PreferencesResolver.Resolve(prefs, inputs);

        // Profile name preserved verbatim from the input; source-label syntax
        // matches the contract.
        Assert.Equal("profile:Work:provider", r.ProviderSource);
    }

    [Fact]
    public void Source_DefaultLabelIncludesProvider()
    {
        var inputs = new ResolutionInputs(null, null, null,
            Env(("OPENAI_API_KEY", "sk-x")));
        var r = PreferencesResolver.Resolve(new Preferences(), inputs);
        Assert.StartsWith("default:", r.ProviderSource, StringComparison.Ordinal);
        Assert.EndsWith(":openai", ":" + r.ProviderSource.Split(':')[1], StringComparison.Ordinal);
    }

    // ── Regression / shape guards ─────────────────────────────────────────

    [Fact]
    public void NullPrefs_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => PreferencesResolver.Resolve(null!, new ResolutionInputs(null, null, null, Env())));
    }

    [Fact]
    public void NullInputs_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => PreferencesResolver.Resolve(new Preferences(), null!));
    }

    [Fact]
    public void Whitespace_CliFlagsAreTrimmed()
    {
        var prefs = PrefsWith(("work", "azure", "gpt-4o"));
        var inputs = new ResolutionInputs(
            CliProvider: "  groq  ", CliProfile: "  work  ", CliModel: "  llama  ",
            Env: Env());
        var r = PreferencesResolver.Resolve(prefs, inputs);
        Assert.Equal("groq", r.Provider);
        Assert.Equal("llama", r.Model);
        Assert.Equal("work", r.ProfileName);
    }

    [Fact]
    public void Whitespace_EmptyCliFlagsAreIgnored()
    {
        // An explicit empty string from the CLI parser must NOT be treated as
        // a deliberate override; fall through to the next rung.
        var inputs = new ResolutionInputs(
            CliProvider: "   ", CliProfile: "   ", CliModel: "   ",
            Env: Env(("AZUREOPENAIENDPOINT", "https://x"),
                     ("AZUREOPENAIMODEL", "gpt-4o")));

        var r = PreferencesResolver.Resolve(new Preferences(), inputs);

        Assert.Equal("azure", r.Provider);
        Assert.Equal("gpt-4o", r.Model);
        Assert.Null(r.ProfileName);
    }

    [Fact]
    public void Outcome_WarningsListAlwaysNonNull()
    {
        var inputs = new ResolutionInputs(
            "azure", null, "gpt-4o",
            Env(("AZUREOPENAIENDPOINT", "https://x")));
        var r = PreferencesResolver.Resolve(new Preferences(), inputs);
        Assert.NotNull(r.Warnings);
        Assert.Empty(r.Warnings);
    }

    [Fact]
    public void Outcome_AllSourceFieldsPopulatedOnSuccess()
    {
        var prefs = PrefsWith(("work", "azure", "gpt-4o"));
        var inputs = new ResolutionInputs(
            null, "work", null, Env(("AZUREOPENAIENDPOINT", "https://x")));
        var r = PreferencesResolver.Resolve(prefs, inputs);
        Assert.False(string.IsNullOrEmpty(r.ProviderSource));
        Assert.False(string.IsNullOrEmpty(r.ModelSource));
        Assert.False(string.IsNullOrEmpty(r.ProfileSource));
        Assert.False(string.IsNullOrEmpty(r.Source));
    }

    [Fact]
    public void Defaults_AzureFirstWhenBothEndpointAndOpenAiKeySet()
    {
        // ADR-011: Azure (endpoint+key, rung 1) beats OpenAI (rung 4) when
        // both are present. Without AZUREOPENAIAPI, OpenAI would win.
        var inputs = new ResolutionInputs(null, null, "gpt-4o",
            Env(("AZUREOPENAIENDPOINT", "https://x"),
                ("AZUREOPENAIAPI", "k"),
                ("OPENAI_API_KEY", "sk-x")));
        var r = PreferencesResolver.Resolve(new Preferences(), inputs);
        Assert.Equal("azure", r.Provider);
    }

    [Fact]
    public void Defaults_CompatPresetOrderIsStable()
    {
        // ADR-011 rung 5 (tie-break): two preset endpoints, no other signal,
        // alphabetically first preset wins. cloudflare < groq lexically.
        var inputs = new ResolutionInputs(null, null, "anything",
            Env(("AZ_AI_GROQ_ENDPOINT", "https://api.groq.com/openai/v1"),
                ("AZ_AI_CLOUDFLARE_ENDPOINT", "https://api.cloudflare.com/v1"),
                ("GROQ_API_KEY", "k1"),
                ("CLOUDFLARE_API_TOKEN", "k2")));
        var r = PreferencesResolver.Resolve(new Preferences(), inputs);
        Assert.Equal("cloudflare", r.Provider);
        Assert.NotEmpty(r.Warnings);
        Assert.Contains("multiple-presets-no-cli-no-profile-no-env-pin", r.Warnings[0], StringComparison.Ordinal);
    }

    [Fact]
    public void AzModel_OverridesProfileModelButNotCliModel()
    {
        var prefs = PrefsWith(("work", "azure", "from-profile"));
        var inputs = new ResolutionInputs(
            null, "work", null,
            Env(("AZ_MODEL", "from-env"), ("AZUREOPENAIENDPOINT", "https://x")));

        var r = PreferencesResolver.Resolve(prefs, inputs);
        Assert.Equal("from-env", r.Model);
        Assert.Equal("env:AZ_MODEL", r.ModelSource);
    }

    [Fact]
    public void CompatModels_MalformedEntriesAreSkippedNotThrown()
    {
        // ResolveDefaultModel tolerates malformed entries (no colon, empty
        // halves) -- the strict parser lives in OpenAiCompatAdapter, used by
        // the dispatch path. The resolver just needs the first matching
        // preset; bad entries fall through.
        var inputs = new ResolutionInputs(
            CliProvider: "groq", CliProfile: null, CliModel: null,
            Env: Env(("GROQ_API_KEY", "k"),
                     ("AZ_AI_COMPAT_MODELS", ":bad,no-colon,groq:llama-3.1")));

        var r = PreferencesResolver.Resolve(new Preferences(), inputs);
        Assert.Equal("llama-3.1", r.Model);
    }

    [Fact]
    public void Mismatch_CompatModelsUnsetMeansNoMismatchCheck()
    {
        var prefs = PrefsWith(("work", "azure", "gpt-4o"));
        var inputs = new ResolutionInputs(null, "work", null,
            Env(("AZUREOPENAIENDPOINT", "https://x")));
        var r = PreferencesResolver.Resolve(prefs, inputs);
        Assert.Empty(r.Warnings);
    }
}
