using System;
using System.Collections.Generic;
using System.Linq;
using AzureOpenAI_CLI.Cli;
using AzureOpenAI_CLI.Registry;

namespace AzureOpenAI_CLI.Capabilities;

// S04E03 -- The Capabilities (Maestro, Wave 1).
//
// Startup gate. Runs post-model-resolution, pre-client-construction. For
// each of the four flag-vs-capability pairs canonized in the episode brief
// (sect. "Flag-vs-capability mapping"), check whether the resolved model
// advertises the required tag. First miss wins -- no aggregation. On a
// miss the gate hands a Bookman-built rejection string back to Program.cs,
// which routes it through ErrorAndExit(msg, rc=2, opts.Json).
//
// Decisions documented inline (per brief):
//
//   1. UNREGISTERED MODEL PASS-THROUGH. If the resolved model is absent
//      from the registry the gate returns null (no check). Rationale:
//      backward compatibility with users running deployments outside the
//      seed or their personal override. A future episode may tighten this
//      to a strict mode, but E03 ships permissive.
//
//   2. FIRST-MISS WINS. Iteration order matches the brief's mapping table:
//      tool_calls -> json_mode -> streaming -> system_prompt. Users fix
//      one incompatibility and retry; if another remains the next attempt
//      surfaces it. Aggregation would balloon the 240-char prefix budget
//      and confuse the actionable "switch to <model>" handoff.
//
//   3. ALLOWLIST INTERSECTION FOR SUGGESTIONS. Per acceptance #5, the
//      suggestion list is (registered models with the capability) INTERSECT
//      (AZUREOPENAIMODEL allowlist). When the allowlist is null/empty the
//      full registered set is offered. When the intersection is empty,
//      CapabilityRejection.Build emits the "no configured model supports
//      this; see --doctor" tail.
//
//   4. NO ESCAPE HATCH. There is no AZ_AI_DISABLE_CAPABILITY_GATE env-var.
//      Per ADR-013, an env-var bypass is the exact failure mode this
//      episode exists to prevent (it would land in a user's
//      ~/.config/az-ai/env six months from now and silently re-enable the
//      confused-4xx era). If you need to bypass, fix your registry entry.
//
// AOT note: this class adds no records, no JSON, no embedded resources.
// One LINQ chain in SuggestionsFor; that path is already pulled in by the
// rest of Program.cs so it costs no extra binary weight.
internal static class CapabilityGate
{
    // Mirrors Program.DEFAULT_SYSTEM_PROMPT. Kept as a private literal here
    // -- not internal-exposed from Program.cs -- so the gate file stands
    // alone and the single-insertion contract in Program.cs is preserved.
    // If the program-side default ever changes, update this constant and
    // add a regression test that pins the pair (Puddy, Wave 2).
    private const string DefaultSystemPromptSentinel =
        "You are a secure, concise CLI assistant. Keep answers factual, no fluff.";

    /// <summary>
    /// Check the resolved model against the four flag-vs-capability pairs.
    /// Returns null when everything checks out, or when the model is not
    /// registered (pass-through; see decision 1 above). Returns the
    /// rejection string -- already formatted by
    /// <see cref="CapabilityRejection.Build"/> -- on first miss.
    /// </summary>
    /// <param name="resolvedModel">Final resolved model name (after CLI, env, profile, smart-default, fallback).</param>
    /// <param name="opts">Parsed CliOptions; we read Tools, AgentMode, RalphMode, Schema, SystemPrompt.</param>
    /// <param name="allowlist">Parsed AZUREOPENAIMODEL allowed set (null when no allowlist is configured).</param>
    public static string? Check(
        string resolvedModel,
        Program.CliOptions opts,
        ISet<string>? allowlist)
    {
        if (string.IsNullOrWhiteSpace(resolvedModel) || opts is null) return null;

        // Look up the resolved model in the registry. Lookup is
        // case-insensitive to mirror the AZUREOPENAIMODEL allowlist
        // comparison at Program.cs:529 -- model identity is OrdinalIgnoreCase
        // throughout the resolution chain, so the gate stays consistent.
        var entries = Program.RegistryEntries;
        if (entries is null || entries.Length == 0) return null;

        ModelRegistryEntry? entry = null;
        for (var i = 0; i < entries.Length; i++)
        {
            if (string.Equals(entries[i].Name, resolvedModel, StringComparison.OrdinalIgnoreCase))
            {
                entry = entries[i];
                break;
            }
        }
        if (entry is null) return null; // Decision 1: unregistered -> pass-through.

        var caps = entry.Capabilities ?? Array.Empty<string>();

        // Iteration order is the brief's mapping-table order. First miss wins.
        // (flag-name-without-dashes, capability-tag, did-the-user-ask-for-it)
        var checks = new (string Flag, string Capability, bool Requested)[]
        {
            ("tools",         "tool_calls",    !string.IsNullOrWhiteSpace(opts.Tools) || opts.AgentMode || opts.RalphMode),
            ("schema",        "json_mode",     !string.IsNullOrWhiteSpace(opts.Schema)),
            // --stream has no CliOptions field today; the slot is reserved so a
            // future PR that adds the flag only has to flip the third tuple
            // element to its detection expression. Keeping the row here keeps
            // the brief's canonical four-flag table 1:1 with the gate body.
            ("stream",        "streaming",     false),
            ("system-prompt", "system_prompt", !string.IsNullOrEmpty(opts.SystemPrompt)
                                                 && !string.Equals(opts.SystemPrompt, DefaultSystemPromptSentinel, StringComparison.Ordinal)),
        };

        foreach (var (flag, capability, requested) in checks)
        {
            if (!requested) continue;
            if (caps.Contains(capability, StringComparer.Ordinal)) continue;

            // Miss. Build suggestions = (registered with cap) INTERSECT allowlist,
            // hand them to Bookman's builder, return the string immediately.
            var suggestions = SuggestionsFor(capability, allowlist);
            return CapabilityRejection.Build(flag, capability, resolvedModel, suggestions);
        }

        return null;
    }

    // (registered models with capability) INTERSECT (AZUREOPENAIMODEL allowlist).
    // When the allowlist is null, no intersection is applied -- the full
    // registered set is offered. When the intersection is empty,
    // CapabilityRejection.Build emits the "no configured model" tail.
    //
    // Comparison: model identity is OrdinalIgnoreCase (same as the
    // allowlist enforcement at Program.cs:529). The registry helper
    // returns names verbatim from registry.json; we preserve that casing
    // in the output for readability.
    private static IReadOnlyList<string> SuggestionsFor(string capability, ISet<string>? allowlist)
    {
        var registered = ModelRegistry.ModelsWithCapability(capability);
        if (registered.Length == 0) return Array.Empty<string>();

        if (allowlist is null || allowlist.Count == 0)
        {
            return registered;
        }

        // Case-insensitive intersection while preserving registry casing.
        var allowlistCi = new HashSet<string>(allowlist, StringComparer.OrdinalIgnoreCase);
        var result = new List<string>(registered.Length);
        foreach (var name in registered)
        {
            if (allowlistCi.Contains(name))
            {
                result.Add(name);
            }
        }
        return result;
    }
}
