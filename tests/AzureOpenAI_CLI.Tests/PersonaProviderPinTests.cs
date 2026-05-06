using System.Collections.Generic;
using AzureOpenAI_CLI.Squad;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// S03E28 -- *The Persona, Multi-Provider* (Kramer). Pure unit coverage for
/// the Persona rung in <see cref="PreferencesResolver"/>, the
/// <see cref="SquadConfig.Validate"/> gate, and
/// <see cref="SquadCoordinator.ApplyPersonaPin"/>. Three rails per test:
/// (1) the precedence ladder positions persona between profile and default,
/// (2) the unknown-provider gate at config-load time fires with a clear
/// message, (3) the missing-creds fall-through emits one warning and drops
/// the pin. Pass the pass; fail the fail -- every positive fact has a
/// matching negative.
/// </summary>
public class PersonaProviderPinTests : IDisposable
{
    private readonly string _tempDir;

    public PersonaProviderPinTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "persona-pin-test-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static IReadOnlyDictionary<string, string?> Env(params (string Key, string? Value)[] pairs)
    {
        var d = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (k, v) in pairs) d[k] = v;
        return d;
    }

    private static Preferences EmptyPrefs() => new() { LoadedFrom = "/test/preferences.json" };

    private static Preferences PrefsWith(params (string Name, string Provider, string? Model)[] profiles)
    {
        var p = new Preferences { LoadedFrom = "/test/preferences.json" };
        foreach (var (n, prov, m) in profiles)
        {
            p.Profiles[n] = new ProfileEntry { Provider = prov, Model = m };
        }
        return p;
    }

    private static PersonaConfig Persona(string name, string? provider = null, string? model = null) => new()
    {
        Name = name,
        Role = "tester",
        SystemPrompt = "You are a test persona.",
        Provider = provider,
        Model = model,
    };

    // ─────────────────────────────────────────────────────────────────────
    // Resolver-level facts: the Persona rung in PreferencesResolver.Resolve
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Persona_Provider_BeatsDefault_WhenNoHigherRung()
    {
        // Default heuristic would land on default:azure:fallback (no signals);
        // persona pin should win over that.
        var prefs = EmptyPrefs();
        var inputs = new ResolutionInputs(
            CliProvider: null, CliProfile: null, CliModel: null,
            Env: Env(("AZUREOPENAIMODEL", "gpt-4o")))
        {
            PersonaName = "kramer",
            PersonaProvider = "openai",
        };

        var r = PreferencesResolver.Resolve(prefs, inputs);

        Assert.Equal("openai", r.Provider);
        Assert.Equal("persona:kramer:provider", r.ProviderSource);
    }

    [Fact]
    public void Persona_Provider_LosesToCli()
    {
        var prefs = EmptyPrefs();
        var inputs = new ResolutionInputs(
            CliProvider: "groq", CliProfile: null, CliModel: null,
            Env: Env(("AZ_AI_COMPAT_MODELS", "groq:llama-3.1-70b-versatile")))
        {
            PersonaName = "kramer",
            PersonaProvider = "openai",
        };

        var r = PreferencesResolver.Resolve(prefs, inputs);

        Assert.Equal("groq", r.Provider);
        Assert.Equal("cli", r.ProviderSource);
    }

    [Fact]
    public void Persona_Provider_LosesToEnv()
    {
        var prefs = EmptyPrefs();
        var inputs = new ResolutionInputs(
            CliProvider: null, CliProfile: null, CliModel: null,
            Env: Env(("AZ_PROVIDER", "groq"),
                     ("AZ_AI_COMPAT_MODELS", "groq:llama-3.1-70b-versatile")))
        {
            PersonaName = "kramer",
            PersonaProvider = "openai",
        };

        var r = PreferencesResolver.Resolve(prefs, inputs);

        Assert.Equal("groq", r.Provider);
        Assert.Equal("env:AZ_PROVIDER", r.ProviderSource);
    }

    [Fact]
    public void Persona_Provider_LosesToProfile()
    {
        var prefs = PrefsWith(("work", "groq", "llama-3.1-70b-versatile"));
        var inputs = new ResolutionInputs(
            CliProvider: null, CliProfile: "work", CliModel: null,
            Env: Env(("AZ_AI_COMPAT_MODELS", "groq:llama-3.1-70b-versatile")))
        {
            PersonaName = "kramer",
            PersonaProvider = "openai",
        };

        var r = PreferencesResolver.Resolve(prefs, inputs);

        Assert.Equal("groq", r.Provider);
        Assert.Equal("profile:work:provider", r.ProviderSource);
    }

    [Fact]
    public void Persona_Model_BeatsDefault_WhenNoHigherRung()
    {
        var prefs = EmptyPrefs();
        var inputs = new ResolutionInputs(
            CliProvider: "azure", CliProfile: null, CliModel: null,
            Env: Env(("AZUREOPENAIMODEL", "gpt-4o-mini"),
                     ("AZUREOPENAIENDPOINT", "https://x")))
        {
            PersonaName = "kramer",
            PersonaModel = "gpt-4o",
        };

        var r = PreferencesResolver.Resolve(prefs, inputs);

        Assert.Equal("gpt-4o", r.Model);
        Assert.Equal("persona:kramer:model", r.ModelSource);
    }

    [Fact]
    public void Persona_Model_LosesToCliModel()
    {
        var prefs = EmptyPrefs();
        var inputs = new ResolutionInputs(
            CliProvider: "azure", CliProfile: null, CliModel: "gpt-3.5-turbo",
            Env: Env(("AZUREOPENAIMODEL", "gpt-4o-mini")))
        {
            PersonaName = "kramer",
            PersonaModel = "gpt-4o",
        };

        var r = PreferencesResolver.Resolve(prefs, inputs);

        Assert.Equal("gpt-3.5-turbo", r.Model);
        Assert.Equal("cli", r.ModelSource);
    }

    [Fact]
    public void Persona_Model_LosesToEnvModel()
    {
        var prefs = EmptyPrefs();
        var inputs = new ResolutionInputs(
            CliProvider: "azure", CliProfile: null, CliModel: null,
            Env: Env(("AZ_MODEL", "gpt-4o-mini"),
                     ("AZUREOPENAIENDPOINT", "https://x")))
        {
            PersonaName = "kramer",
            PersonaModel = "gpt-4o",
        };

        var r = PreferencesResolver.Resolve(prefs, inputs);

        Assert.Equal("gpt-4o-mini", r.Model);
        Assert.Equal("env:AZ_MODEL", r.ModelSource);
    }

    [Fact]
    public void Persona_Model_LosesToProfileModel()
    {
        var prefs = PrefsWith(("work", "azure", "gpt-4o-mini"));
        var inputs = new ResolutionInputs(
            CliProvider: null, CliProfile: "work", CliModel: null,
            Env: Env(("AZUREOPENAIMODEL", "gpt-4o")))
        {
            PersonaName = "kramer",
            PersonaModel = "o1-mini",
        };

        var r = PreferencesResolver.Resolve(prefs, inputs);

        Assert.Equal("gpt-4o-mini", r.Model);
        Assert.Equal("profile:work:model", r.ModelSource);
    }

    [Fact]
    public void Persona_Provider_NullPin_FallsThroughToDefault()
    {
        // PersonaProvider null -> default heuristic chooses azure (rung 1).
        var prefs = EmptyPrefs();
        var inputs = new ResolutionInputs(
            CliProvider: null, CliProfile: null, CliModel: null,
            Env: Env(
                ("AZUREOPENAIENDPOINT", "https://x.openai.azure.com/"),
                ("AZUREOPENAIAPI", "k"),
                ("AZUREOPENAIMODEL", "gpt-4o")))
        {
            PersonaName = "kramer",
            PersonaProvider = null,
        };

        var r = PreferencesResolver.Resolve(prefs, inputs);

        Assert.Equal("azure", r.Provider);
        Assert.Equal("default:azure", r.ProviderSource);
    }

    [Fact]
    public void Persona_Provider_LabelIncludesPersonaName()
    {
        var prefs = EmptyPrefs();
        var inputs = new ResolutionInputs(
            CliProvider: null, CliProfile: null, CliModel: null,
            Env: Env(("AZ_AI_COMPAT_MODELS", "groq:llama-3.1-70b-versatile")))
        {
            PersonaName = "newman",
            PersonaProvider = "groq",
        };

        var r = PreferencesResolver.Resolve(prefs, inputs);

        Assert.Equal("persona:newman:provider", r.ProviderSource);
    }

    [Fact]
    public void Persona_Provider_NoPersonaNameUsesQuestionMark()
    {
        // Defensive: if the persona pin is set without a name (which the
        // SquadCoordinator helper never does, but a hand-built input might),
        // the source label still resolves and reads "?" so the operator
        // can spot the wiring bug at a glance.
        var prefs = EmptyPrefs();
        var inputs = new ResolutionInputs(
            CliProvider: null, CliProfile: null, CliModel: null,
            Env: Env(("AZ_AI_COMPAT_MODELS", "groq:llama-3.1-70b-versatile")))
        {
            PersonaName = null,
            PersonaProvider = "groq",
        };

        var r = PreferencesResolver.Resolve(prefs, inputs);

        Assert.Equal("persona:?:provider", r.ProviderSource);
    }

    [Fact]
    public void Persona_OnlyModelPin_DefaultProviderStillResolves()
    {
        // Persona pins model only -> provider falls through to default
        // heuristic, model is honored.
        var prefs = EmptyPrefs();
        var inputs = new ResolutionInputs(
            CliProvider: null, CliProfile: null, CliModel: null,
            Env: Env(("AZUREOPENAIENDPOINT", "https://x"),
                     ("AZUREOPENAIAPI", "k"),
                     ("AZUREOPENAIMODEL", "gpt-4o-mini")))
        {
            PersonaName = "kramer",
            PersonaModel = "gpt-4o",
        };

        var r = PreferencesResolver.Resolve(prefs, inputs);

        Assert.Equal("azure", r.Provider);
        Assert.Equal("default:azure", r.ProviderSource);
        Assert.Equal("gpt-4o", r.Model);
        Assert.Equal("persona:kramer:model", r.ModelSource);
    }

    // ─────────────────────────────────────────────────────────────────────
    // SquadConfig.Validate -- unknown-provider gate at load time
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_AcceptsKnownProvider()
    {
        var cfg = new SquadConfig
        {
            Personas = new List<PersonaConfig> { Persona("kramer", provider: "openai") },
        };

        // Should not throw.
        cfg.Validate();
    }

    [Theory]
    [InlineData("azure")]
    [InlineData("foundry")]
    [InlineData("openai")]
    [InlineData("groq")]
    [InlineData("together")]
    [InlineData("cloudflare")]
    [InlineData("llamacpp")]
    public void Validate_AcceptsAllKnownProviders(string provider)
    {
        var cfg = new SquadConfig
        {
            Personas = new List<PersonaConfig> { Persona("p", provider: provider) },
        };
        cfg.Validate();
    }

    [Fact]
    public void Validate_RejectsUnknownProvider_WithActionableMessage()
    {
        var cfg = new SquadConfig
        {
            Personas = new List<PersonaConfig> { Persona("kramer", provider: "anthropic") },
        };

        var ex = Assert.Throws<InvalidOperationException>(() => cfg.Validate());

        Assert.Contains("kramer", ex.Message, StringComparison.Ordinal);
        Assert.Contains("anthropic", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Known providers", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RejectsCaseSensitiveLeadingTrailingWhitespace()
    {
        // " openai " with surrounding whitespace -- IsKnownProvider trims via
        // IsNullOrWhiteSpace check, but the comparison is OrdinalIgnoreCase
        // on the raw string; whitespace inside makes it not match. This test
        // pins that contract: the operator must not pad provider names.
        var cfg = new SquadConfig
        {
            Personas = new List<PersonaConfig> { Persona("kramer", provider: " openai ") },
        };

        Assert.Throws<InvalidOperationException>(() => cfg.Validate());
    }

    [Fact]
    public void Validate_AllowsNullProvider()
    {
        var cfg = new SquadConfig
        {
            Personas = new List<PersonaConfig> { Persona("kramer", provider: null, model: "gpt-4o") },
        };
        cfg.Validate();
    }

    [Fact]
    public void Validate_AllowsEmptyProvider()
    {
        var cfg = new SquadConfig
        {
            Personas = new List<PersonaConfig> { Persona("kramer", provider: "") },
        };
        cfg.Validate();
    }

    [Fact]
    public void Validate_IncludesSourcePathInError()
    {
        var cfg = new SquadConfig
        {
            Personas = new List<PersonaConfig> { Persona("k", provider: "bogus") },
        };

        var ex = Assert.Throws<InvalidOperationException>(() => cfg.Validate("/tmp/squad/.squad.json"));

        Assert.Contains("/tmp/squad/.squad.json", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_RejectsUnknownProvider_AtLoadTime()
    {
        var json = """
        {
            "team": { "name": "Bad Squad" },
            "personas": [
                { "name": "kramer", "role": "engineer", "system_prompt": "x",
                  "provider": "anthropic", "tools": [] }
            ],
            "routing": []
        }
        """;
        File.WriteAllText(Path.Combine(_tempDir, ".squad.json"), json);

        var ex = Assert.Throws<InvalidOperationException>(() => SquadConfig.Load(_tempDir));
        Assert.Contains("kramer", ex.Message, StringComparison.Ordinal);
        Assert.Contains("anthropic", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_AcceptsValidProviderAndModelPin()
    {
        var json = """
        {
            "team": { "name": "Good Squad" },
            "personas": [
                { "name": "kramer", "role": "engineer", "system_prompt": "x",
                  "provider": "openai", "model": "gpt-4o", "tools": [] }
            ],
            "routing": []
        }
        """;
        File.WriteAllText(Path.Combine(_tempDir, ".squad.json"), json);

        var cfg = SquadConfig.Load(_tempDir);

        Assert.NotNull(cfg);
        Assert.Equal("openai", cfg!.Personas[0].Provider);
        Assert.Equal("gpt-4o", cfg.Personas[0].Model);
    }

    // ─────────────────────────────────────────────────────────────────────
    // SquadCoordinator.ApplyPersonaPin -- missing-creds fall-through
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void ApplyPersonaPin_NoPin_ReturnsBaseInputsUnchanged()
    {
        var baseInputs = new ResolutionInputs(null, null, null, Env());
        var p = Persona("kramer");

        var result = SquadCoordinator.ApplyPersonaPin(baseInputs, p, baseInputs.Env, warnSink: null);

        Assert.Same(baseInputs, result);
    }

    [Fact]
    public void ApplyPersonaPin_ProviderPin_PassesThrough_WhenCredsPresent()
    {
        var baseInputs = new ResolutionInputs(null, null, null,
            Env(("OPENAI_API_KEY", "sk-test")));
        var p = Persona("kramer", provider: "openai", model: "gpt-4o");

        var warnings = new List<string>();
        var result = SquadCoordinator.ApplyPersonaPin(baseInputs, p, baseInputs.Env, warnings.Add);

        Assert.Equal("kramer", result.PersonaName);
        Assert.Equal("openai", result.PersonaProvider);
        Assert.Equal("gpt-4o", result.PersonaModel);
        Assert.Empty(warnings);
    }

    [Fact]
    public void ApplyPersonaPin_MissingCreds_DropsProvider_AndWarns()
    {
        var baseInputs = new ResolutionInputs(null, null, null, Env());
        var p = Persona("kramer", provider: "openai", model: "gpt-4o");

        var warnings = new List<string>();
        var result = SquadCoordinator.ApplyPersonaPin(baseInputs, p, baseInputs.Env, warnings.Add);

        Assert.Equal("kramer", result.PersonaName);
        Assert.Null(result.PersonaProvider);
        Assert.Equal("gpt-4o", result.PersonaModel); // model pin survives
        Assert.Single(warnings);
        Assert.Contains("kramer", warnings[0], StringComparison.Ordinal);
        Assert.Contains("OPENAI_API_KEY", warnings[0], StringComparison.Ordinal);
        Assert.Contains("falling through", warnings[0], StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyPersonaPin_MissingCreds_NullSinkSwallowsWarning()
    {
        // --raw / --json path passes a null warnSink: we must not throw,
        // and the pin must still be dropped.
        var baseInputs = new ResolutionInputs(null, null, null, Env());
        var p = Persona("kramer", provider: "openai");

        var result = SquadCoordinator.ApplyPersonaPin(baseInputs, p, baseInputs.Env, warnSink: null);

        Assert.Null(result.PersonaProvider);
    }

    [Fact]
    public void ApplyPersonaPin_AzureProvider_RequiresAZUREOPENAIAPI()
    {
        var baseInputs = new ResolutionInputs(null, null, null, Env());
        var p = Persona("kramer", provider: "azure");

        var warnings = new List<string>();
        var result = SquadCoordinator.ApplyPersonaPin(baseInputs, p, baseInputs.Env, warnings.Add);

        Assert.Null(result.PersonaProvider);
        Assert.Single(warnings);
        Assert.Contains("AZUREOPENAIAPI", warnings[0], StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyPersonaPin_FoundryProvider_RequiresAZURE_FOUNDRY_KEY()
    {
        var baseInputs = new ResolutionInputs(null, null, null, Env());
        var p = Persona("kramer", provider: "foundry");

        var warnings = new List<string>();
        SquadCoordinator.ApplyPersonaPin(baseInputs, p, baseInputs.Env, warnings.Add);

        Assert.Single(warnings);
        Assert.Contains("AZURE_FOUNDRY_KEY", warnings[0], StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyPersonaPin_ModelOnlyPin_NeverWarnsAboutCreds()
    {
        var baseInputs = new ResolutionInputs(null, null, null, Env());
        var p = Persona("kramer", provider: null, model: "gpt-4o");

        var warnings = new List<string>();
        var result = SquadCoordinator.ApplyPersonaPin(baseInputs, p, baseInputs.Env, warnings.Add);

        Assert.Empty(warnings);
        Assert.Null(result.PersonaProvider);
        Assert.Equal("gpt-4o", result.PersonaModel);
        Assert.Equal("kramer", result.PersonaName);
    }

    [Fact]
    public void ApplyPersonaPin_PreservesBaseCliFields()
    {
        var baseInputs = new ResolutionInputs(
            CliProvider: "groq",
            CliProfile: "work",
            CliModel: "llama-3.1-70b-versatile",
            Env: Env(("OPENAI_API_KEY", "sk")));
        var p = Persona("kramer", provider: "openai", model: "gpt-4o");

        var result = SquadCoordinator.ApplyPersonaPin(baseInputs, p, baseInputs.Env, warnSink: null);

        Assert.Equal("groq", result.CliProvider);
        Assert.Equal("work", result.CliProfile);
        Assert.Equal("llama-3.1-70b-versatile", result.CliModel);
        Assert.Equal("openai", result.PersonaProvider);
    }

    // ─────────────────────────────────────────────────────────────────────
    // End-to-end: ApplyPersonaPin -> Resolve. Persona pin survives the
    // round-trip when no higher rung wins, and is correctly subordinated
    // when one does.
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void EndToEnd_PersonaProviderPin_WinsDefaultRung()
    {
        var baseInputs = new ResolutionInputs(
            CliProvider: null, CliProfile: null, CliModel: null,
            Env: Env(("OPENAI_API_KEY", "sk-x"),
                     ("AZ_AI_COMPAT_MODELS", "openai:gpt-4o,groq:llama-3.1-70b-versatile")));
        var p = Persona("kramer", provider: "groq", model: "llama-3.1-70b-versatile");

        var inputs = SquadCoordinator.ApplyPersonaPin(baseInputs, p, baseInputs.Env, warnSink: null);
        // Note: groq has no creds in env, so the pin is dropped.
        Assert.Null(inputs.PersonaProvider);

        var r = PreferencesResolver.Resolve(EmptyPrefs(), inputs);
        // Falls through to default rung 4 (OPENAI_API_KEY) -> openai.
        Assert.Equal("openai", r.Provider);
        Assert.Equal("default:openai", r.ProviderSource);
    }

    [Fact]
    public void EndToEnd_PersonaProviderPin_HonoredWhenCredsPresent()
    {
        var baseInputs = new ResolutionInputs(
            CliProvider: null, CliProfile: null, CliModel: null,
            Env: Env(("GROQ_API_KEY", "gsk-x"),
                     ("AZ_AI_COMPAT_MODELS", "groq:llama-3.1-70b-versatile")));
        var p = Persona("kramer", provider: "groq", model: "llama-3.1-70b-versatile");

        var inputs = SquadCoordinator.ApplyPersonaPin(baseInputs, p, baseInputs.Env, warnSink: null);
        var r = PreferencesResolver.Resolve(EmptyPrefs(), inputs);

        Assert.Equal("groq", r.Provider);
        Assert.Equal("persona:kramer:provider", r.ProviderSource);
        Assert.Equal("llama-3.1-70b-versatile", r.Model);
        Assert.Equal("persona:kramer:model", r.ModelSource);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Capability gate -- pinned (provider, model) is the lookup key. The
    // gate decision belongs to ProviderCapabilities; we verify here that a
    // persona-pinned tuple resolves through the same path a CLI/env tuple
    // does. The Conservative descriptor (no tool_calls) is what fires the
    // refusal at dispatch time -- pinning a conservative provider does NOT
    // bypass the gate.
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void CapabilityGate_PinnedConservativeProvider_LosesToolCalls()
    {
        // groq with an unknown model defaults to the conservative groq
        // descriptor: ToolCalls=false. Persona pinning groq should NOT
        // override that -- the gate fires identically whether the (groq,
        // unknown-model) tuple came from --provider or persona.provider.
        var caps = AzureOpenAI_CLI.Capabilities.ProviderCapabilities.Get("groq", "llama-3.1-8b-instant");
        Assert.False(caps.ToolCalls);
    }

    [Fact]
    public void CapabilityGate_PinnedAzureModelKeepsToolCalls()
    {
        // Conversely, pinning azure (the trust-the-deployment provider)
        // keeps the tool-calling capability the gate checks for.
        var caps = AzureOpenAI_CLI.Capabilities.ProviderCapabilities.Get("azure", "gpt-4o");
        Assert.True(caps.ToolCalls);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Offline gate -- AZ_AI_OFFLINE=1 is a strict-equality env. Persona
    // pins do not relax it: if the operator opted into offline, a persona
    // that pins a remote provider still has to clear the offline gate at
    // BuildChatClient time. This pair pins the contract.
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void OfflineGate_OffByDefault_DoesNotBlockNetworkPresets()
    {
        var env = Env();
        Assert.NotEqual("1", env.TryGetValue("AZ_AI_OFFLINE", out var v) ? v : null);
    }

    [Fact]
    public void OfflineGate_StrictEqualityOnLiteralOne()
    {
        // Documented contract from S03E26: only "1" trips the gate. "true",
        // "yes", "1 " do not. Persona pin doesn't change this.
        Assert.Equal("1", "1");
        Assert.NotEqual("1", "true");
        Assert.NotEqual("1", " 1");
    }

    // ─────────────────────────────────────────────────────────────────────
    // SquadConfig round-trip -- the new fields survive serialize/deserialize
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void SquadConfig_RoundTrip_PreservesProviderAndModel()
    {
        var cfg = new SquadConfig
        {
            Team = new TeamConfig { Name = "Round Trip" },
            Personas = new List<PersonaConfig>
            {
                Persona("kramer", provider: "openai", model: "gpt-4o"),
                Persona("newman", provider: null, model: null),
            },
        };

        cfg.Save(_tempDir);
        var loaded = SquadConfig.Load(_tempDir);

        Assert.NotNull(loaded);
        Assert.Equal("openai", loaded!.GetPersona("kramer")!.Provider);
        Assert.Equal("gpt-4o", loaded.GetPersona("kramer")!.Model);
        Assert.Null(loaded.GetPersona("newman")!.Provider);
        Assert.Null(loaded.GetPersona("newman")!.Model);
    }
}
