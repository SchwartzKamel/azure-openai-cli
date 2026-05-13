using System;
using System.Collections.Generic;
using System.Linq;
using AzureOpenAI_CLI.Cli;
using Xunit;

namespace AzureOpenAI_CLI.Tests;

// S04E03 -- The Capabilities (Bookman, Wave 1).
//
// Unit suite for CapabilityRejection.Build. Tight, single-purpose: the
// gate's own behavioural tests live in Puddy's CapabilityGateTests file
// (different wave, different scope). Co-located here so re-tiering the
// rejection wording (a Snap-tier responsibility) does not require
// reopening the gate suite.
//
// Test names match the parent-suite filter `~Capability` so
// `dotnet test --filter "FullyQualifiedName~CapabilityRejection"`
// reaches all of these and nothing else.
public class CapabilityRejectionTests
{
    private static readonly IReadOnlyList<string> NoSuggestions = Array.Empty<string>();

    [Fact]
    public void Build_PrefixBeforeSuggestionTail_IsAtMost240Chars()
    {
        // Acceptance #7: prefix portion <= 240 chars regardless of suggestions.
        var msg = CapabilityRejection.Build(
            "system-prompt",
            "system_prompt",
            "gpt-5.4-nano",
            new[] { "gpt-4o-mini", "llama-local" });

        // The "prefix" is everything up to and including the period that
        // ends "(required by --{flag}).". Find that boundary.
        var period = msg.IndexOf(").", StringComparison.Ordinal);
        Assert.True(period > 0, "expected '(required by --flag).' marker");
        var prefixLen = period + 2; // include the ')' and '.'

        Assert.True(
            prefixLen <= CapabilityRejection.PrefixBudgetChars,
            "prefix was " + prefixLen + " chars, budget is "
                + CapabilityRejection.PrefixBudgetChars);
    }

    [Fact]
    public void Build_Output_IsAsciiOnly()
    {
        // Acceptance #6: ASCII only. Char-by-char check against the printable
        // ASCII range [0x20, 0x7E]. Regex would also work but is heavier and
        // the loop is cheaper and more obviously correct.
        var msg = CapabilityRejection.Build(
            "tools",
            "tool_calls",
            "gpt-5.4-nano",
            new[] { "gpt-4o-mini" });

        foreach (var ch in msg)
        {
            Assert.InRange(ch, (char)0x20, (char)0x7E);
        }
    }

    [Fact]
    public void Build_Contains_Model_Capability_And_DashDashFlag()
    {
        // Acceptance #2: the message must name the model, the capability,
        // and the --flag that triggered the rejection.
        var msg = CapabilityRejection.Build(
            "tools",
            "tool_calls",
            "gpt-5.4-nano",
            new[] { "gpt-4o-mini" });

        Assert.Contains("gpt-5.4-nano", msg, StringComparison.Ordinal);
        Assert.Contains("tool_calls", msg, StringComparison.Ordinal);
        Assert.Contains("--tools", msg, StringComparison.Ordinal);
        // Single line.
        Assert.DoesNotContain("\n", msg, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", msg, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WithEmptySuggestions_EndsWithDoctorTail()
    {
        // Acceptance #5: empty intersection -> see-doctor tail, not "Try: ".
        var msg = CapabilityRejection.Build(
            "schema",
            "json_mode",
            "llama-local",
            NoSuggestions);

        Assert.EndsWith(
            "no configured model supports this; see --doctor",
            msg,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Try:", msg, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WithSuggestions_EmitsCommaJoinedTryTail()
    {
        var msg = CapabilityRejection.Build(
            "stream",
            "streaming",
            "gpt-5.4-nano",
            new[] { "gpt-4o-mini", "gpt-4o", "llama-local" });

        Assert.EndsWith(
            "Try: gpt-4o-mini, gpt-4o, llama-local",
            msg,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Build_ScrubsC0AndC1AndNonAsciiInModelName()
    {
        // A hostile registry-override card could embed a CSI escape in a
        // model name. The builder must replace every non-printable-ASCII
        // byte with '?' so the rejection cannot smuggle an escape into
        // stderr.
        var hostile = "gpt\u001b[31m-evil\u00a0nbsp\u0007bell";
        var msg = CapabilityRejection.Build(
            "tools",
            "tool_calls",
            hostile,
            new[] { "gpt-4o-mini" });

        // The original control bytes are gone.
        Assert.DoesNotContain("\u001b", msg, StringComparison.Ordinal);
        Assert.DoesNotContain("\u0007", msg, StringComparison.Ordinal);
        Assert.DoesNotContain("\u00a0", msg, StringComparison.Ordinal);
        // Each scrubbed byte became '?'. The hostile string had 3 such
        // bytes (ESC, NBSP, BEL); expect at least 3 '?' chars in output.
        var qCount = msg.Count(c => c == '?');
        Assert.True(qCount >= 3, "expected >=3 '?' from scrubbing, saw " + qCount);
        // Output remains ASCII.
        foreach (var ch in msg)
        {
            Assert.InRange(ch, (char)0x20, (char)0x7E);
        }
    }
}
