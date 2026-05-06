using System;
using System.Collections.Generic;
using System.Linq;

namespace AzureOpenAI_CLI.Resilience;

// S03E22 -- The Fallback. Frank Costanza, on-call edition: a primary
// provider with no plan B is a 3am incident waiting to happen. The policy
// is opt-in (zero default behavior change), bounded (max chain depth 3),
// and validated up front against the ProviderCapabilities preset registry
// so a typo at 4:59 PM Friday does not turn into a confused 4xx at 5:00.
//
// Inputs (CLI > env, mirroring the rest of the CLI's precedence chain):
//
//     --fallback ollama,openai             (CLI flag, comma-separated)
//     AZ_AI_FALLBACK=ollama,openai         (env var, comma-separated)
//
// Strict-equality enable -- whitespace-only or empty values are treated as
// "none". Unknown presets are refused with a friendly error that names the
// known set; this is the same "fail fast at the gate" doctrine the
// capability gate (e18) uses.

/// <summary>
/// Parsed, validated fallback chain. <see cref="Providers"/> is an ordered
/// list of provider preset names (lower-cased Ordinal). When
/// <see cref="HasError"/> is true the call site MUST refuse to dispatch and
/// surface <see cref="ErrorMessage"/>; otherwise <see cref="IsActive"/>
/// indicates whether <see cref="FallbackChain.Wrap"/> should be invoked.
/// </summary>
public sealed record FallbackPolicy(
    IReadOnlyList<string> Providers,
    string Source,
    string? ErrorMessage)
{
    /// <summary>True when the policy parsed cleanly and has at least one provider.</summary>
    public bool IsActive => ErrorMessage is null && Providers.Count > 0;

    /// <summary>True when the policy failed to parse (caller must surface the error and exit).</summary>
    public bool HasError => ErrorMessage is not null;

    /// <summary>Hard cap: chains longer than this are refused with a clear error.</summary>
    public const int MaxDepth = 3;

    /// <summary>Empty no-op policy. Returned when neither CLI nor env supplies a value.</summary>
    public static FallbackPolicy None { get; } =
        new(Array.Empty<string>(), "none", null);

    /// <summary>Env-var name. Strict-equality semantics mirror AZ_AI_TELEMETRY (e13) -- whitespace is not a value.</summary>
    public const string EnvVarName = "AZ_AI_FALLBACK";

    /// <summary>CLI flag name. Value follows as the next argv entry (no `=` syntax).</summary>
    public const string CliFlagName = "--fallback";

    // Preset allowlist. Mirrors ProviderCapabilities.PresetDefaults snapshot
    // (e18, 2026-05). Kept here as a static set so the parser does not take
    // a hard runtime dependency on the capability registry's reflection
    // surface -- AOT-clean and cheap. If a preset is added there, add it
    // here too; the symmetry test (FallbackChainTests.PolicyParse_*) will
    // catch the drift.
    private static readonly HashSet<string> KnownPresets =
        new(StringComparer.Ordinal)
        {
            "azure",
            "foundry",
            "openai",
            "groq",
            "together",
            "cloudflare",
            "ollama",
        };

    /// <summary>
    /// Resolve the fallback policy from <paramref name="argv"/> and the
    /// process environment. CLI flag wins over env var. Whitespace-only
    /// values yield <see cref="None"/>. Unknown presets, duplicates, empty
    /// tokens, or chains longer than <see cref="MaxDepth"/> yield a policy
    /// with <see cref="HasError"/> true and a friendly
    /// <see cref="ErrorMessage"/>.
    /// </summary>
    public static FallbackPolicy Resolve(string[] argv, Func<string, string?> getEnv)
    {
        ArgumentNullException.ThrowIfNull(argv);
        ArgumentNullException.ThrowIfNull(getEnv);

        // CLI flag pre-scan. We do not modify the main ParseArgs surface
        // (e20/e25 territory) -- the chain reads argv directly so the
        // BuildChatClient wrap site stays self-contained.
        for (int i = 0; i < argv.Length; i++)
        {
            if (string.Equals(argv[i], CliFlagName, StringComparison.Ordinal))
            {
                if (i + 1 >= argv.Length)
                {
                    return new FallbackPolicy(
                        Array.Empty<string>(),
                        "cli-error",
                        $"{CliFlagName} requires a comma-separated list of provider presets "
                        + $"(e.g. {CliFlagName} ollama,openai). Known presets: {KnownList()}.");
                }
                return Parse(argv[i + 1], $"cli:{CliFlagName}");
            }
        }

        var fromEnv = getEnv(EnvVarName);
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return Parse(fromEnv!, $"env:{EnvVarName}");
        }

        return None;
    }

    /// <summary>
    /// Internal parse + validate. Visible for tests so the table of
    /// invalid inputs can be exercised without round-tripping argv.
    /// </summary>
    internal static FallbackPolicy Parse(string raw, string source)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return None with { Source = source };
        }

        var parts = raw.Split(',');
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<string>(parts.Length);
        foreach (var rawPart in parts)
        {
            var trimmed = rawPart.Trim();
            if (trimmed.Length == 0)
            {
                return new FallbackPolicy(
                    Array.Empty<string>(),
                    source,
                    $"Fallback chain contains an empty entry. Source: {source}. "
                    + "Use a comma-separated list of provider presets, e.g. ollama,openai.");
            }
            // Lowercase Ordinal -- preset names are ASCII tokens; we want a
            // stable canonical form for the rest of the pipeline (telemetry
            // dedupe, stderr warn lines, capability lookups).
            var canonical = trimmed.ToLowerInvariant();
            if (!KnownPresets.Contains(canonical))
            {
                return new FallbackPolicy(
                    Array.Empty<string>(),
                    source,
                    $"Unknown fallback provider preset '{trimmed}'. Known presets: {KnownList()}. "
                    + $"Source: {source}.");
            }
            if (!seen.Add(canonical))
            {
                return new FallbackPolicy(
                    Array.Empty<string>(),
                    source,
                    $"Fallback chain lists '{canonical}' more than once. Each preset may "
                    + $"appear at most once. Source: {source}.");
            }
            ordered.Add(canonical);
        }

        if (ordered.Count > MaxDepth)
        {
            return new FallbackPolicy(
                Array.Empty<string>(),
                source,
                $"Fallback chain depth {ordered.Count} exceeds the maximum of {MaxDepth}. "
                + $"Source: {source}. Trim the chain -- a fourth alternate is a sign the "
                + "primary is the wrong choice, not that the backup needs a backup.");
        }

        return new FallbackPolicy(ordered, source, null);
    }

    /// <summary>True if <paramref name="preset"/> is a known fallback preset (case-insensitive).</summary>
    public static bool IsKnownPreset(string preset)
        => preset is not null && KnownPresets.Contains(preset.ToLowerInvariant());

    private static string KnownList()
        => string.Join(", ", KnownPresets.OrderBy(s => s, StringComparer.Ordinal));
}
