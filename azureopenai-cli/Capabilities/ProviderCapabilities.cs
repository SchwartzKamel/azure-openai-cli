using System.Collections.Generic;

namespace AzureOpenAI_CLI.Capabilities;

// S03E18 -- The Capability Gate.
//
// ProviderCapabilities is the registry + lookup surface for the capability
// gate. Two-key resolution: (preset, model) wins; preset-level default falls
// back next; Conservative() is the floor for unknown presets. Override
// mechanism is a single env var, AZ_AI_CAPABILITY_OVERRIDES, with the shape
//
//     preset:model:capability=bool[, ...]
//
// which lets a user flip a flag for a single deployment without a binary
// rebuild. Snapshot-in-time: the matrix is correct as of 2026-05; see
// findings-backlog `costanza-2026-05-CG-1` for the review-cadence finding.

/// <summary>
/// Thrown at dispatch time when a request asks for a capability the
/// (preset, model) tuple does not advertise. Carries a friendly message that
/// names the override env var; <see cref="ErrorClass"/> is the stable string
/// used by telemetry (E13).
/// </summary>
public sealed class CapabilityMismatchException : Exception
{
    /// <summary>Stable telemetry error_class identifier.</summary>
    public const string ErrorClass = "CapabilityMismatch";

    public string Preset { get; }
    public string Model { get; }
    public string Capability { get; }

    public CapabilityMismatchException(string preset, string model, string capability, string message)
        : base(message)
    {
        Preset = preset;
        Model = model;
        Capability = capability;
    }
}

/// <summary>
/// Static registry of <see cref="CapabilityDescriptor"/> entries keyed by
/// preset name (case-insensitive Ordinal) and optionally by model name. Use
/// <see cref="Get"/> to resolve a (preset, model) pair; the resolver applies
/// env-var overrides and falls back to <see cref="CapabilityDescriptor.Conservative"/>
/// for unknown presets.
/// </summary>
public static class ProviderCapabilities
{
    /// <summary>Env-var name for runtime capability overrides.</summary>
    public const string OverridesEnvVar = "AZ_AI_CAPABILITY_OVERRIDES";

    // Built-in registry. Two layers:
    //   * PresetDefaults[preset]            -> CapabilityDescriptor for any
    //                                          model under that preset.
    //   * ModelOverrides[preset][model]     -> model-specific override that
    //                                          beats the preset default.
    //
    // Snapshot date: 2026-05. Reviewed quarterly per findings backlog
    // `costanza-2026-05-CG-1`.

    private static readonly Dictionary<string, CapabilityDescriptor> PresetDefaults =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Azure / Foundry: caller controls deployment, so we trust it.
            ["azure"] = new(ToolCalls: true, Streaming: true, Vision: true, JsonMode: true, MaxContextTokens: null),
            ["foundry"] = new(ToolCalls: true, Streaming: true, Vision: true, JsonMode: true, MaxContextTokens: null),
            // OpenAI direct (GPT-4o family). Model-specific overrides below
            // narrow gpt-3.5-turbo (no vision) and o1-* (no streaming).
            ["openai"] = new(ToolCalls: true, Streaming: true, Vision: true, JsonMode: true, MaxContextTokens: 128_000),
            // Groq. Llama-on-Groq: streaming yes, json yes; tool-calls only
            // on a small allowlist of 70B+ models (see ModelOverrides).
            ["groq"] = new(ToolCalls: false, Streaming: true, Vision: false, JsonMode: true, MaxContextTokens: 32_768),
            // Together: model-zoo, conservative defaults until per-model entries land.
            ["together"] = new(ToolCalls: false, Streaming: true, Vision: false, JsonMode: true, MaxContextTokens: null),
            // Cloudflare Workers AI: conservative -- only streaming is reliable.
            ["cloudflare"] = new(ToolCalls: false, Streaming: true, Vision: false, JsonMode: false, MaxContextTokens: null),
        };

    private static readonly Dictionary<string, Dictionary<string, CapabilityDescriptor>> ModelOverrides =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["openai"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["gpt-3.5-turbo"] = new(ToolCalls: true, Streaming: true, Vision: false, JsonMode: true, MaxContextTokens: 16_385),
                ["o1-preview"] = new(ToolCalls: true, Streaming: false, Vision: false, JsonMode: false, MaxContextTokens: 128_000),
                ["o1-mini"] = new(ToolCalls: false, Streaming: false, Vision: false, JsonMode: false, MaxContextTokens: 128_000),
            },
            ["groq"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["llama-3.1-70b-versatile"] = new(ToolCalls: true, Streaming: true, Vision: false, JsonMode: true, MaxContextTokens: 131_072),
                ["llama-3.3-70b-versatile"] = new(ToolCalls: true, Streaming: true, Vision: false, JsonMode: true, MaxContextTokens: 131_072),
            },
        };

    /// <summary>
    /// Resolve capabilities for a (preset, model) tuple. Lookup order:
    /// <list type="number">
    ///   <item>Env-var override (<see cref="OverridesEnvVar"/>) -- a flag set
    ///     here mutates the descriptor that would otherwise be returned.</item>
    ///   <item>Model-specific entry under the preset.</item>
    ///   <item>Preset-level default.</item>
    ///   <item><see cref="CapabilityDescriptor.Conservative"/>.</item>
    /// </list>
    /// Case-insensitive Ordinal everywhere. Never throws; malformed override
    /// entries warn (see <see cref="ParseOverrides"/>) and are skipped.
    /// </summary>
    public static CapabilityDescriptor Get(string preset, string model)
        => Get(preset, model, Environment.GetEnvironmentVariable(OverridesEnvVar), warnSink: null);

    /// <summary>
    /// Test seam: explicit override string + warning sink. <paramref name="warnSink"/>
    /// receives one line per malformed override entry (or null to silence).
    /// </summary>
    internal static CapabilityDescriptor Get(string preset, string model, string? rawOverrides, Action<string>? warnSink)
    {
        if (string.IsNullOrWhiteSpace(preset)) preset = string.Empty;
        if (string.IsNullOrWhiteSpace(model)) model = string.Empty;

        // Base layer: model-specific -> preset default -> Conservative.
        CapabilityDescriptor caps = CapabilityDescriptor.Conservative();
        if (ModelOverrides.TryGetValue(preset, out var byModel)
            && byModel.TryGetValue(model, out var modelEntry))
        {
            caps = modelEntry;
        }
        else if (PresetDefaults.TryGetValue(preset, out var presetEntry))
        {
            caps = presetEntry;
        }

        // Env-var override layer.
        var overrides = ParseOverrides(rawOverrides, warnSink);
        if (overrides.Count > 0)
        {
            foreach (var entry in overrides)
            {
                if (!string.Equals(entry.Preset, preset, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.Equals(entry.Model, model, StringComparison.OrdinalIgnoreCase)) continue;
                caps = ApplyFlag(caps, entry.Capability, entry.Value);
            }
        }
        return caps;
    }

    /// <summary>
    /// True if the registry has an entry (preset default or model-specific)
    /// for <paramref name="preset"/>. Used by <c>OpenAiCompatAdapter.Build</c>
    /// to decide whether to warn that a Conservative profile is being applied.
    /// </summary>
    public static bool HasPreset(string preset)
        => !string.IsNullOrWhiteSpace(preset) && PresetDefaults.ContainsKey(preset);

    private static CapabilityDescriptor ApplyFlag(CapabilityDescriptor caps, string capability, bool value)
    {
        // Capability identifiers are case-insensitive. Underscores accepted to
        // match the doc convention (`tool_calls`).
        var c = capability.Trim();
        if (string.Equals(c, "tool_calls", StringComparison.OrdinalIgnoreCase)
            || string.Equals(c, "toolcalls", StringComparison.OrdinalIgnoreCase))
        {
            return caps with { ToolCalls = value };
        }
        if (string.Equals(c, "streaming", StringComparison.OrdinalIgnoreCase))
        {
            return caps with { Streaming = value };
        }
        if (string.Equals(c, "vision", StringComparison.OrdinalIgnoreCase))
        {
            return caps with { Vision = value };
        }
        if (string.Equals(c, "json_mode", StringComparison.OrdinalIgnoreCase)
            || string.Equals(c, "jsonmode", StringComparison.OrdinalIgnoreCase))
        {
            return caps with { JsonMode = value };
        }
        return caps;
    }

    internal readonly record struct OverrideEntry(string Preset, string Model, string Capability, bool Value);

    /// <summary>
    /// Parse the override env var. Format: comma-separated
    /// <c>preset:model:capability=bool</c> items. Whitespace-tolerant.
    /// Malformed items emit one warning line each via <paramref name="warnSink"/>
    /// and are skipped (parser is lenient -- a typo never breaks dispatch).
    /// </summary>
    internal static List<OverrideEntry> ParseOverrides(string? raw, Action<string>? warnSink)
    {
        var result = new List<OverrideEntry>();
        if (string.IsNullOrWhiteSpace(raw)) return result;

        var items = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var item in items)
        {
            var eq = item.IndexOf('=');
            if (eq <= 0 || eq == item.Length - 1)
            {
                warnSink?.Invoke($"[capability] malformed override '{item}' (missing '=' or empty value)");
                continue;
            }
            var lhs = item.Substring(0, eq).Trim();
            var rhs = item.Substring(eq + 1).Trim();

            var parts = lhs.Split(':');
            if (parts.Length != 3
                || string.IsNullOrWhiteSpace(parts[0])
                || string.IsNullOrWhiteSpace(parts[1])
                || string.IsNullOrWhiteSpace(parts[2]))
            {
                warnSink?.Invoke($"[capability] malformed override '{item}' (expected preset:model:capability=bool)");
                continue;
            }

            if (!TryParseBool(rhs, out var value))
            {
                warnSink?.Invoke($"[capability] malformed override '{item}' (value '{rhs}' is not true/false)");
                continue;
            }

            // Capability sanity check: if the name is unrecognised, warn but
            // still record the entry -- ApplyFlag will simply no-op on
            // resolution, leaving the descriptor unchanged.
            var cap = parts[2].Trim();
            if (!IsKnownCapability(cap))
            {
                warnSink?.Invoke($"[capability] override '{item}' names unknown capability '{cap}' (known: tool_calls, streaming, vision, json_mode)");
                continue;
            }

            result.Add(new OverrideEntry(parts[0].Trim(), parts[1].Trim(), cap, value));
        }
        return result;
    }

    private static bool TryParseBool(string s, out bool value)
    {
        if (string.Equals(s, "true", StringComparison.OrdinalIgnoreCase)) { value = true; return true; }
        if (string.Equals(s, "false", StringComparison.OrdinalIgnoreCase)) { value = false; return true; }
        if (string.Equals(s, "1", StringComparison.Ordinal)) { value = true; return true; }
        if (string.Equals(s, "0", StringComparison.Ordinal)) { value = false; return true; }
        value = false;
        return false;
    }

    private static bool IsKnownCapability(string cap)
        => string.Equals(cap, "tool_calls", StringComparison.OrdinalIgnoreCase)
        || string.Equals(cap, "toolcalls", StringComparison.OrdinalIgnoreCase)
        || string.Equals(cap, "streaming", StringComparison.OrdinalIgnoreCase)
        || string.Equals(cap, "vision", StringComparison.OrdinalIgnoreCase)
        || string.Equals(cap, "json_mode", StringComparison.OrdinalIgnoreCase)
        || string.Equals(cap, "jsonmode", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Build a CapabilityMismatch exception with a friendly, override-aware
    /// message. Format:
    ///
    ///     {preset}:{model} does not support {capability}. Pick a model with
    ///     capability or set AZ_AI_CAPABILITY_OVERRIDES={preset}:{model}:{capability}=true
    ///     to override.
    /// </summary>
    public static CapabilityMismatchException Mismatch(string preset, string model, string capability)
    {
        var msg = $"{preset}:{model} does not support {capability}. "
                + $"Pick a model with capability or set "
                + $"{OverridesEnvVar}={preset}:{model}:{capability}=true to override.";
        return new CapabilityMismatchException(preset, model, capability, msg);
    }
}
