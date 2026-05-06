using System.Collections.Generic;

namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// S03E22 -- The Default (Costanza). Pure unit tests for the ADR-011
/// default-provider heuristic implemented in
/// <see cref="PreferencesResolver.Resolve"/>'s default rung. Every rung,
/// the tie-break, the multi-preset warning, the loopback-port matching,
/// and the determinism contract get exercised here.
///
/// The resolver is a pure function of (Preferences, ResolutionInputs);
/// these tests construct the env snapshot directly -- no Environment
/// mutation, no Console capture needed. The Collection attribute is a
/// discipline marker to serialize with sister files that DO mutate env.
/// </summary>
[Collection("ConsoleCapture")]
public class DefaultProviderHeuristicTests
{
    private static IReadOnlyDictionary<string, string?> Env(params (string Key, string? Value)[] pairs)
    {
        var d = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (k, v) in pairs) d[k] = v;
        return d;
    }

    private static ResolutionInputs DefaultInputs(IReadOnlyDictionary<string, string?> env, string? cliModel = null)
        => new ResolutionInputs(null, null, cliModel, env);

    // ── Rung 1 -- AZUREOPENAIENDPOINT + AZUREOPENAIAPI ──────────────────

    [Fact]
    public void Rung1_BothAzureSignals_ProducesDefaultAzure()
    {
        var r = PreferencesResolver.Resolve(new Preferences(),
            DefaultInputs(Env(("AZUREOPENAIENDPOINT", "https://x"), ("AZUREOPENAIAPI", "k")), "gpt-4o"));
        Assert.Equal("azure", r.Provider);
        Assert.Equal("default:azure", r.ProviderSource);
        Assert.Empty(r.Warnings);
    }

    [Fact]
    public void Rung1_EndpointWithoutKey_DoesNotFire()
    {
        // Endpoint alone is not enough: rung 1 demands BOTH signals.
        var r = PreferencesResolver.Resolve(new Preferences(),
            DefaultInputs(Env(("AZUREOPENAIENDPOINT", "https://x")), "gpt-4o"));
        Assert.Equal("azure", r.Provider);
        Assert.Equal("default:azure:fallback", r.ProviderSource);
    }

    [Fact]
    public void Rung1_KeyWithoutEndpoint_DoesNotFire()
    {
        var r = PreferencesResolver.Resolve(new Preferences(),
            DefaultInputs(Env(("AZUREOPENAIAPI", "k")), "gpt-4o"));
        Assert.Equal("default:azure:fallback", r.ProviderSource);
    }

    [Fact]
    public void Rung1_BeatsRung4WhenBothAzureAndOpenAiKeySet()
    {
        var r = PreferencesResolver.Resolve(new Preferences(),
            DefaultInputs(Env(
                ("AZUREOPENAIENDPOINT", "https://x"),
                ("AZUREOPENAIAPI", "k"),
                ("OPENAI_API_KEY", "sk-x")), "gpt-4o"));
        Assert.Equal("azure", r.Provider);
        Assert.Equal("default:azure", r.ProviderSource);
    }

    [Fact]
    public void Rung1_WhitespaceValues_TreatedAsUnset()
    {
        var r = PreferencesResolver.Resolve(new Preferences(),
            DefaultInputs(Env(("AZUREOPENAIENDPOINT", "https://x"), ("AZUREOPENAIAPI", "  ")), "gpt-4o"));
        Assert.Equal("default:azure:fallback", r.ProviderSource);
    }

    // ── Rung 2 -- exactly one AZ_AI_<PRESET>_ENDPOINT ───────────────────

    [Fact]
    public void Rung2_SingleOllamaEndpoint_ProducesDefaultOllama()
    {
        var r = PreferencesResolver.Resolve(new Preferences(),
            DefaultInputs(Env(("AZ_AI_OLLAMA_ENDPOINT", "http://localhost:11434/v1")), "llama3"));
        Assert.Equal("ollama", r.Provider);
        Assert.Equal("default:ollama", r.ProviderSource);
        Assert.Empty(r.Warnings);
    }

    [Fact]
    public void Rung2_SingleGroqEndpoint_ProducesDefaultGroq()
    {
        var r = PreferencesResolver.Resolve(new Preferences(),
            DefaultInputs(Env(("AZ_AI_GROQ_ENDPOINT", "https://api.groq.com/openai/v1")), "llama-3.1"));
        Assert.Equal("groq", r.Provider);
        Assert.Equal("default:groq", r.ProviderSource);
    }

    [Fact]
    public void Rung2_SinglePresetEndpoint_LowercasedInLabel()
    {
        // Env-var convention is upper; source label is lower for stability.
        var r = PreferencesResolver.Resolve(new Preferences(),
            DefaultInputs(Env(("AZ_AI_LMSTUDIO_ENDPOINT", "http://localhost:1234/v1")), "phi"));
        Assert.Equal("lmstudio", r.Provider);
        Assert.Equal("default:lmstudio", r.ProviderSource);
    }

    [Fact]
    public void Rung2_OutranksRung4_OpenAiKeyIgnored()
    {
        // One preset endpoint set + OPENAI_API_KEY: rung 2 fires (single
        // endpoint is unambiguous); rung 4 never gets a turn.
        var r = PreferencesResolver.Resolve(new Preferences(),
            DefaultInputs(Env(
                ("AZ_AI_LLAMACPP_ENDPOINT", "http://localhost:8080/v1"),
                ("OPENAI_API_KEY", "sk-x")), "phi"));
        Assert.Equal("llamacpp", r.Provider);
        Assert.Equal("default:llamacpp", r.ProviderSource);
    }

    [Fact]
    public void Rung2_EmptyEndpointValue_DoesNotCount()
    {
        // Empty / whitespace AZ_AI_<PRESET>_ENDPOINT is treated as unset
        // (matches AZUREOPENAIENDPOINT semantics).
        var r = PreferencesResolver.Resolve(new Preferences(),
            DefaultInputs(Env(
                ("AZ_AI_OLLAMA_ENDPOINT", "  "),
                ("AZ_AI_GROQ_ENDPOINT", "https://api.groq.com")), "x"));
        Assert.Equal("groq", r.Provider);
        Assert.Equal("default:groq", r.ProviderSource);
    }

    // ── Rung 3 -- local-detected ────────────────────────────────────────

    [Fact]
    public void Rung3_TwoLocalEndpoints_LocalProvidersOptIn_ProducesLocalDetected()
    {
        var r = PreferencesResolver.Resolve(new Preferences(),
            DefaultInputs(Env(
                ("AZ_AI_OLLAMA_ENDPOINT", "http://localhost:11434/v1"),
                ("AZ_AI_LLAMACPP_ENDPOINT", "http://localhost:8080/v1"),
                ("AZ_AI_LOCAL_PROVIDERS", "1")), "llama3"));
        // Alphabetical first match -- llamacpp < ollama lexically.
        Assert.Equal("llamacpp", r.Provider);
        Assert.Equal("default:llamacpp:local-detected", r.ProviderSource);
    }

    [Fact]
    public void Rung3_LocalProvidersStrictEqualsOne()
    {
        // AZ_AI_LOCAL_PROVIDERS != "1" disables rung 3 -- falls through
        // to tie-break.
        var r = PreferencesResolver.Resolve(new Preferences(),
            DefaultInputs(Env(
                ("AZ_AI_OLLAMA_ENDPOINT", "http://localhost:11434/v1"),
                ("AZ_AI_LMSTUDIO_ENDPOINT", "http://localhost:1234/v1"),
                ("AZ_AI_LOCAL_PROVIDERS", "true")), "x"));
        Assert.DoesNotContain(":local-detected", r.ProviderSource);
        Assert.NotEmpty(r.Warnings);
    }

    [Fact]
    public void Rung3_LoopbackHost127_0_0_1_Matches()
    {
        var r = PreferencesResolver.Resolve(new Preferences(),
            DefaultInputs(Env(
                ("AZ_AI_OLLAMA_ENDPOINT", "http://127.0.0.1:11434/v1"),
                ("AZ_AI_LLAMACPP_ENDPOINT", "http://localhost:8080/v1"),
                ("AZ_AI_LOCAL_PROVIDERS", "1")), "x"));
        Assert.EndsWith(":local-detected", r.ProviderSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Rung3_NonLoopbackHost_DoesNotMatch()
    {
        // Cloud URLs do not satisfy the loopback rule even with opt-in.
        var r = PreferencesResolver.Resolve(new Preferences(),
            DefaultInputs(Env(
                ("AZ_AI_GROQ_ENDPOINT", "https://api.groq.com/openai/v1"),
                ("AZ_AI_TOGETHER_ENDPOINT", "https://api.together.xyz/v1"),
                ("AZ_AI_LOCAL_PROVIDERS", "1")), "x"));
        Assert.DoesNotContain(":local-detected", r.ProviderSource);
    }

    [Fact]
    public void Rung3_WrongPortDoesNotMatch()
    {
        // localhost on a non-canonical port falls through.
        var r = PreferencesResolver.Resolve(new Preferences(),
            DefaultInputs(Env(
                ("AZ_AI_OLLAMA_ENDPOINT", "http://localhost:9999/v1"),
                ("AZ_AI_LLAMACPP_ENDPOINT", "http://localhost:7777/v1"),
                ("AZ_AI_LOCAL_PROVIDERS", "1")), "x"));
        Assert.DoesNotContain(":local-detected", r.ProviderSource);
    }

    [Fact]
    public void Rung3_FirstAlphabeticalLocalMatchWins()
    {
        // Three local endpoints, all canonical -- llamacpp wins (alpha).
        var r = PreferencesResolver.Resolve(new Preferences(),
            DefaultInputs(Env(
                ("AZ_AI_OLLAMA_ENDPOINT", "http://localhost:11434/v1"),
                ("AZ_AI_LLAMACPP_ENDPOINT", "http://localhost:8080/v1"),
                ("AZ_AI_LMSTUDIO_ENDPOINT", "http://localhost:1234/v1"),
                ("AZ_AI_LOCAL_PROVIDERS", "1")), "x"));
        Assert.Equal("llamacpp", r.Provider);
        Assert.Equal("default:llamacpp:local-detected", r.ProviderSource);
    }

    // ── Rung 4 -- OPENAI_API_KEY ────────────────────────────────────────

    [Fact]
    public void Rung4_OpenAiKey_ProducesDefaultOpenAi()
    {
        var r = PreferencesResolver.Resolve(new Preferences(),
            DefaultInputs(Env(("OPENAI_API_KEY", "sk-x")), "gpt-4o-mini"));
        Assert.Equal("openai", r.Provider);
        Assert.Equal("default:openai", r.ProviderSource);
    }

    [Fact]
    public void Rung4_DoesNotFireWhenPresetEndpointPresent()
    {
        // One preset endpoint preempts rung 4.
        var r = PreferencesResolver.Resolve(new Preferences(),
            DefaultInputs(Env(
                ("AZ_AI_GROQ_ENDPOINT", "https://api.groq.com/openai/v1"),
                ("OPENAI_API_KEY", "sk-x")), "x"));
        Assert.Equal("groq", r.Provider);
    }

    // ── Rung 5 -- tie-break ─────────────────────────────────────────────

    [Fact]
    public void Rung5_TieBreak_AlphabeticallyFirstPresetWins()
    {
        var r = PreferencesResolver.Resolve(new Preferences(),
            DefaultInputs(Env(
                ("AZ_AI_GROQ_ENDPOINT", "https://api.groq.com/openai/v1"),
                ("AZ_AI_TOGETHER_ENDPOINT", "https://api.together.xyz/v1")), "x"));
        Assert.Equal("groq", r.Provider);
        Assert.Equal("default:groq", r.ProviderSource);
    }

    [Fact]
    public void Rung5_TieBreak_EmitsDocumentedWarning()
    {
        var r = PreferencesResolver.Resolve(new Preferences(),
            DefaultInputs(Env(
                ("AZ_AI_OLLAMA_ENDPOINT", "https://example.com/o"),
                ("AZ_AI_GROQ_ENDPOINT", "https://api.groq.com")), "x"));
        Assert.NotEmpty(r.Warnings);
        Assert.Contains("multiple-presets-no-cli-no-profile-no-env-pin", r.Warnings[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Rung5_TieBreak_AlphaPickAcrossManyPresets()
    {
        // cloudflare < groq < ollama < together; cloudflare wins.
        var r = PreferencesResolver.Resolve(new Preferences(),
            DefaultInputs(Env(
                ("AZ_AI_OLLAMA_ENDPOINT", "https://example.com/o"),
                ("AZ_AI_GROQ_ENDPOINT", "https://example.com/g"),
                ("AZ_AI_CLOUDFLARE_ENDPOINT", "https://example.com/c"),
                ("AZ_AI_TOGETHER_ENDPOINT", "https://example.com/t")), "x"));
        Assert.Equal("cloudflare", r.Provider);
    }

    [Fact]
    public void Rung5_NotTriggered_WhenAzProviderEnvPinned()
    {
        // AZ_PROVIDER pre-empts the entire default ladder.
        var r = PreferencesResolver.Resolve(new Preferences(),
            new ResolutionInputs(null, null, "x", Env(
                ("AZ_PROVIDER", "groq"),
                ("AZ_AI_OLLAMA_ENDPOINT", "https://x"),
                ("AZ_AI_GROQ_ENDPOINT", "https://y"))));
        Assert.Equal("groq", r.Provider);
        Assert.Equal("env:AZ_PROVIDER", r.ProviderSource);
        Assert.Empty(r.Warnings);
    }

    // ── Rung 6 -- fallback ──────────────────────────────────────────────

    [Fact]
    public void Rung6_NoSignals_ProducesAzureFallback()
    {
        var r = PreferencesResolver.Resolve(new Preferences(),
            DefaultInputs(Env(), "gpt-4o-mini"));
        Assert.Equal("azure", r.Provider);
        Assert.Equal("default:azure:fallback", r.ProviderSource);
    }

    [Fact]
    public void Rung6_BackwardCompat_ProviderIsAzureRegardlessOfLabel()
    {
        // The ADR-011 promise: with no env signals the provider is still
        // "azure" -- only the source label changed (added :fallback).
        var r = PreferencesResolver.Resolve(new Preferences(),
            DefaultInputs(Env(), null));
        Assert.Equal("azure", r.Provider);
        Assert.StartsWith("default:azure", r.ProviderSource, StringComparison.Ordinal);
    }

    // ── Determinism + label contract ────────────────────────────────────

    [Fact]
    public void Determinism_IdenticalInputs_ProduceIdenticalOutput()
    {
        var env = Env(
            ("AZ_AI_OLLAMA_ENDPOINT", "http://localhost:11434/v1"),
            ("AZ_AI_LMSTUDIO_ENDPOINT", "http://localhost:1234/v1"),
            ("AZ_AI_LOCAL_PROVIDERS", "1"));
        var r1 = PreferencesResolver.Resolve(new Preferences(), DefaultInputs(env, "x"));
        var r2 = PreferencesResolver.Resolve(new Preferences(), DefaultInputs(env, "x"));
        Assert.Equal(r1.Provider, r2.Provider);
        Assert.Equal(r1.ProviderSource, r2.ProviderSource);
        Assert.Equal(r1.Warnings.Count, r2.Warnings.Count);
    }

    [Theory]
    [InlineData("default:azure")]
    [InlineData("default:azure:fallback")]
    [InlineData("default:openai")]
    public void LabelContract_KnownDefaultLabelsAreLowercaseAndColonSeparated(string label)
    {
        Assert.Equal(label, label.ToLowerInvariant());
        Assert.Contains(":", label, StringComparison.Ordinal);
        Assert.DoesNotContain(" ", label, StringComparison.Ordinal);
    }

    [Fact]
    public void LabelContract_LocalDetectedIncludesPresetAndSuffix()
    {
        var r = PreferencesResolver.Resolve(new Preferences(),
            DefaultInputs(Env(
                ("AZ_AI_OLLAMA_ENDPOINT", "http://localhost:11434"),
                ("AZ_AI_LLAMACPP_ENDPOINT", "http://localhost:8080"),
                ("AZ_AI_LOCAL_PROVIDERS", "1")), "x"));
        Assert.Matches("^default:[a-z]+:local-detected$", r.ProviderSource);
    }

    [Fact]
    public void Purity_NoEnvironmentMutation()
    {
        // The resolver must not write back to Environment. Any write would
        // be visible to a subsequent process-wide read.
        var key = "AZ_AI_NEVER_SET_KEY_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(key, null);
        var before = Environment.GetEnvironmentVariable(key);
        _ = PreferencesResolver.Resolve(new Preferences(),
            DefaultInputs(Env(("OPENAI_API_KEY", "sk-x")), "x"));
        var after = Environment.GetEnvironmentVariable(key);
        Assert.Equal(before, after);
    }

    [Fact]
    public void Purity_NoSocketProbeForLocalDetected()
    {
        // A genuinely-closed loopback port still satisfies rung 3 because
        // the rule is URL-string-shaped, not socket-probing. ProviderDoctor
        // owns the live check.
        var r = PreferencesResolver.Resolve(new Preferences(),
            DefaultInputs(Env(
                ("AZ_AI_OLLAMA_ENDPOINT", "http://localhost:11434/v1"),
                ("AZ_AI_LMSTUDIO_ENDPOINT", "http://localhost:1234/v1"),
                ("AZ_AI_LOCAL_PROVIDERS", "1")), "x"));
        Assert.EndsWith(":local-detected", r.ProviderSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Heuristic_PresetEndpointDiscoveryIgnoresUnrelatedKeys()
    {
        // AZ_AI_COMPAT_MODELS / AZ_AI_LOCAL_PROVIDERS look like they could
        // match the AZ_AI_*_ENDPOINT pattern but do not (no _ENDPOINT
        // suffix). Confirm they do not pollute the preset count.
        var r = PreferencesResolver.Resolve(new Preferences(),
            DefaultInputs(Env(
                ("AZ_AI_COMPAT_MODELS", "groq:llama-3.1"),
                ("AZ_AI_LOCAL_PROVIDERS", "1"),
                ("OPENAI_API_KEY", "sk-x")), "x"));
        Assert.Equal("openai", r.Provider);
        Assert.Equal("default:openai", r.ProviderSource);
    }

    [Fact]
    public void Heuristic_FallbackDoesNotEmitWarning()
    {
        // The :fallback rung is silent -- the audit trail is the label
        // itself. No spurious stderr noise on a clean Espanso invocation.
        var r = PreferencesResolver.Resolve(new Preferences(),
            DefaultInputs(Env(), "gpt-4o-mini"));
        Assert.Empty(r.Warnings);
    }

    [Fact]
    public void Heuristic_TieBreakWarningIsSelfContained()
    {
        // The warning string names the action: pin one of the rails.
        var r = PreferencesResolver.Resolve(new Preferences(),
            DefaultInputs(Env(
                ("AZ_AI_GROQ_ENDPOINT", "https://x"),
                ("AZ_AI_TOGETHER_ENDPOINT", "https://y")), "x"));
        Assert.Single(r.Warnings);
        Assert.Contains("AZ_PROVIDER", r.Warnings[0], StringComparison.Ordinal);
        Assert.Contains("--provider", r.Warnings[0], StringComparison.Ordinal);
        Assert.Contains("profile", r.Warnings[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Heuristic_ExactlyOneEndpointDoesNotEmitTieBreakWarning()
    {
        var r = PreferencesResolver.Resolve(new Preferences(),
            DefaultInputs(Env(("AZ_AI_LMSTUDIO_ENDPOINT", "http://localhost:1234/v1")), "x"));
        Assert.Empty(r.Warnings);
    }

    [Fact]
    public void Heuristic_LocalDetectedDoesNotEmitTieBreakWarning()
    {
        // Rung 3 wins cleanly over rung 5; no warning when local detection
        // succeeds.
        var r = PreferencesResolver.Resolve(new Preferences(),
            DefaultInputs(Env(
                ("AZ_AI_OLLAMA_ENDPOINT", "http://localhost:11434/v1"),
                ("AZ_AI_LLAMACPP_ENDPOINT", "http://localhost:8080/v1"),
                ("AZ_AI_LOCAL_PROVIDERS", "1")), "x"));
        Assert.Empty(r.Warnings);
    }
}
