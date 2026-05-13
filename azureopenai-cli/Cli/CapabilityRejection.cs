using System;
using System.Collections.Generic;
using System.Text;

namespace AzureOpenAI_CLI.Cli;

// S04E03 -- The Capabilities (Bookman, Wave 1).
//
// CapabilityRejection.Build produces the single-line rejection string that
// CapabilityGate hands to ErrorAndExit(..., rc=2, ...) when a flag requires
// a capability the resolved model does not advertise.
//
// Wording contract (acceptance criteria #2, #6, #7):
//   "model '{model}' does not support {capability} (required by --{flag}). "
//   "Try: {comma-joined suggestions}"
// or, when no configured model carries the capability:
//   "model '{model}' does not support {capability} (required by --{flag}). "
//   "no configured model supports this; see --doctor"
//
// Constraints:
//   - The literal "[ERROR]" prefix is added by Program.ErrorAndExit; this
//     builder does NOT prepend it.
//   - Single line. No '\n'. No markdown. No ANSI.
//   - ASCII only. C0 (0x00..0x1F) and C1 (0x7F..0x9F) bytes in any
//     interpolated string are scrubbed to '?'. Non-ASCII (>= 0xA0) is
//     scrubbed too; the registry vocabulary is ASCII by policy.
//   - Total length BEFORE the suggestion list is <= 240 chars.
//
// Sanitization: this file duplicates the C0/C1 stripper from
// Program.SanitizeForTerminal rather than calling across namespaces.
// Program.SanitizeForTerminal is `private static` and lives in the
// top-level program namespace; exposing it would either widen its
// visibility (every caller in Program.cs now competes with this builder
// for that surface) or force a circular using-graph between Cli/ and the
// root namespace. The duplication is six lines, zero allocations on the
// happy path, and keeps the rejection wording self-contained -- Bookman
// can re-tier this string without reopening Program.cs.
//
// Bookman, 2026-05-21. Tier: S (Snap). One line, no preamble.
public static class CapabilityRejection
{
    // Hard ceiling on the "prefix" portion of the message: everything up to
    // and including the period that ends "(required by --{flag})." plus
    // the single space before the suggestion tail. Acceptance #7.
    public const int PrefixBudgetChars = 240;

    /// <summary>
    /// Builds the single-line capability-rejection message.
    /// </summary>
    /// <param name="flag">Long flag name without leading dashes (e.g. "tools"). The "--" is added.</param>
    /// <param name="capability">Capability tag (e.g. "tool_calls").</param>
    /// <param name="model">Resolved model name (e.g. "gpt-5.4-nano").</param>
    /// <param name="suggestions">
    /// Models that carry the capability AND appear in AZUREOPENAIMODEL. Order
    /// preserved. Duplicates are not deduplicated; the caller owns the list.
    /// </param>
    /// <returns>The rejection string. ASCII only. Single line. No trailing newline.</returns>
    public static string Build(
        string flag,
        string capability,
        string model,
        IReadOnlyList<string> suggestions)
    {
        ArgumentNullException.ThrowIfNull(flag);
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(suggestions);

        var sFlag = Scrub(flag);
        var sCapability = Scrub(capability);
        var sModel = Scrub(model);

        // Prefix: everything before the suggestion tail. Built once, then
        // length-checked against PrefixBudgetChars. The format is fixed.
        var prefix =
            "model '" + sModel + "' does not support " + sCapability
            + " (required by --" + sFlag + ").";

        // Acceptance #7: prefix (sans suggestion tail) must fit 240 chars.
        // We treat this as an internal invariant; truncation would corrupt
        // the diagnostic, so we throw instead. Caller-supplied strings are
        // bounded by the registry vocabulary plus flag/capability constants
        // -- a violation here means the inputs themselves are out of contract.
        if (prefix.Length > PrefixBudgetChars)
        {
            throw new ArgumentException(
                "CapabilityRejection prefix exceeded "
                + PrefixBudgetChars
                + "-char budget (was "
                + prefix.Length
                + "). Inputs out of contract.",
                nameof(model));
        }

        var sb = new StringBuilder(prefix.Length + 64);
        sb.Append(prefix);
        sb.Append(' ');

        if (suggestions.Count == 0)
        {
            sb.Append("no configured model supports this; see --doctor");
        }
        else
        {
            sb.Append("Try: ");
            for (var i = 0; i < suggestions.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(Scrub(suggestions[i] ?? string.Empty));
            }
        }

        return sb.ToString();
    }

    // C0 (0x00..0x1F) and C1 (0x7F..0x9F) bytes scrubbed to '?'. Anything
    // outside printable ASCII (0x20..0x7E) is also scrubbed -- the
    // registry capability vocabulary, model names, and flag names are
    // ASCII by policy (ModelCapability.AllowedTags + AZUREOPENAIMODEL
    // allowlist), so a non-ASCII byte here is either a registry-override
    // attack or a typo. Either way: '?'.
    private static string Scrub(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (ch >= 0x20 && ch <= 0x7E)
            {
                sb.Append(ch);
            }
            else
            {
                sb.Append('?');
            }
        }
        return sb.ToString();
    }
}
