using System.Linq;
using AzureOpenAI_CLI.Registry;
using AzureOpenAI_CLI.Resolution;
using AzureOpenAI_CLI.Tests.Resolution;
using Xunit;

namespace AzureOpenAI_CLI.Tests;

// S04E05 Wave 1 facts -- Costanza/Maestro seeds covering the baseline
// resolver contract. ResolverTestCorpus was extracted in Wave 2 to its
// own file (tests/AzureOpenAI_CLI.Tests/Resolution/ResolverTestCorpus.cs)
// so E11 *The Corpus* can reuse the seed builders without depending on
// this specific test class.
//
// Wave 2 (Puddy) appends adversarial + corner-case facts below the
// original 18, plus an FDR-derived adversarial mini-corpus folded in.

public class ResolveSmartDefaultTests
{
    [Fact]
    public void Pick_Explicit_ShortCircuitsAxisAndAllowlist()
    {
        var reg = ResolverTestCorpus.MixedRegistry();
        var inputs = ResolverTestCorpus.Inputs(
            explicitModel: "anything-goes",
            preferAxis: "quality",
            allowlist: new[] { "cheap-fast", "balanced" });

        var r = ResolveSmartDefault.Pick(reg, inputs);

        Assert.Equal("anything-goes", r.Model);
        Assert.Equal(ResolutionReason.EXPLICIT, r.ReasonCode);
        Assert.Contains("--model", r.HumanReason);
    }

    [Fact]
    public void Pick_PreferCost_PicksCheapestFromAllowlist()
    {
        var reg = ResolverTestCorpus.MixedRegistry();
        var inputs = ResolverTestCorpus.Inputs(
            preferAxis: "cost",
            allowlist: ResolverTestCorpus.MixedAllowlist);

        var r = ResolveSmartDefault.Pick(reg, inputs);

        Assert.Equal("cheap-fast", r.Model);
        Assert.Equal(ResolutionReason.PREFER_AXIS, r.ReasonCode);
        Assert.Contains("cost", r.HumanReason);
        Assert.Contains("runner-up", r.HumanReason);
    }

    [Fact]
    public void Pick_PreferLatency_PicksFastest()
    {
        var reg = ResolverTestCorpus.MixedRegistry();
        var inputs = ResolverTestCorpus.Inputs(
            preferAxis: "latency",
            allowlist: ResolverTestCorpus.MixedAllowlist);

        var r = ResolveSmartDefault.Pick(reg, inputs);

        Assert.Equal("cheap-fast", r.Model);
        Assert.Equal(ResolutionReason.PREFER_AXIS, r.ReasonCode);
        Assert.Contains("latency", r.HumanReason);
    }

    [Fact]
    public void Pick_PreferQuality_PicksHighest()
    {
        var reg = ResolverTestCorpus.MixedRegistry();
        var inputs = ResolverTestCorpus.Inputs(
            preferAxis: "quality",
            allowlist: ResolverTestCorpus.MixedAllowlist);

        var r = ResolveSmartDefault.Pick(reg, inputs);

        Assert.Equal("premium-slow", r.Model);
        Assert.Equal(ResolutionReason.PREFER_AXIS, r.ReasonCode);
        Assert.Contains("quality", r.HumanReason);
    }

    [Fact]
    public void Pick_StableSort_TieBreakerIsAllowlistOrder()
    {
        // Both have CostTier=low; allowlist order picks "first-cheap".
        var reg = ResolverTestCorpus.Registry(
            ResolverTestCorpus.Entry("first-cheap", costTier: "low"),
            ResolverTestCorpus.Entry("second-cheap", costTier: "low"));
        var inputs = ResolverTestCorpus.Inputs(
            preferAxis: "cost",
            allowlist: new[] { "first-cheap", "second-cheap" });

        var r = ResolveSmartDefault.Pick(reg, inputs);

        Assert.Equal("first-cheap", r.Model);

        // Now reverse the allowlist; the other should win.
        var rev = ResolverTestCorpus.Inputs(
            preferAxis: "cost",
            allowlist: new[] { "second-cheap", "first-cheap" });
        Assert.Equal("second-cheap", ResolveSmartDefault.Pick(reg, rev).Model);
    }

    [Fact]
    public void Pick_MissingTier_SortsLast()
    {
        // "missing-tiers" has no latency tier; it must NOT win on latency
        // axis when any tiered entry is present.
        var reg = ResolverTestCorpus.MixedRegistry();
        var inputs = ResolverTestCorpus.Inputs(
            preferAxis: "latency",
            // Put missing-tiers FIRST in allowlist -- if missing were sorted
            // first, the picker would return it. Allowlist-index tie-break
            // only kicks in at equal rank; rank int.MaxValue sorts last.
            allowlist: new[] { "missing-tiers", "balanced", "cheap-fast" });

        var r = ResolveSmartDefault.Pick(reg, inputs);

        Assert.Equal("cheap-fast", r.Model);
    }

    [Fact]
    public void Pick_NoExplicitNoAxis_ReturnsAllowlistHead()
    {
        var reg = ResolverTestCorpus.MixedRegistry();
        var inputs = ResolverTestCorpus.Inputs(
            allowlist: new[] { "balanced", "cheap-fast" });

        var r = ResolveSmartDefault.Pick(reg, inputs);

        Assert.Equal("balanced", r.Model);
        Assert.Equal(ResolutionReason.ALLOWLIST_HEAD, r.ReasonCode);
        Assert.Contains("AZUREOPENAIMODEL", r.HumanReason);
    }

    [Fact]
    public void Pick_EmptyAllowlist_ReturnsFallback()
    {
        var reg = ResolverTestCorpus.MixedRegistry();
        var inputs = ResolverTestCorpus.Inputs(allowlist: System.Array.Empty<string>());

        var r = ResolveSmartDefault.Pick(reg, inputs);

        Assert.Equal("", r.Model);
        Assert.Equal(ResolutionReason.FALLBACK, r.ReasonCode);
        Assert.Contains("AZUREOPENAIMODEL", r.HumanReason);
        Assert.Contains("empty", r.HumanReason);
    }

    [Fact]
    public void Pick_AllowlistHeadNotInRegistry_NoAxis_ReturnsFallback()
    {
        var reg = ResolverTestCorpus.Registry(
            ResolverTestCorpus.Entry("known-model"));
        var inputs = ResolverTestCorpus.Inputs(
            allowlist: new[] { "ghost-model", "known-model" });

        var r = ResolveSmartDefault.Pick(reg, inputs);

        Assert.Equal("ghost-model", r.Model);
        Assert.Equal(ResolutionReason.FALLBACK, r.ReasonCode);
        Assert.Contains("ghost-model", r.HumanReason);
        Assert.Contains("--doctor", r.HumanReason);
    }

    [Fact]
    public void Pick_UnknownPreferAxis_FallsThroughToAllowlistHead()
    {
        var reg = ResolverTestCorpus.MixedRegistry();
        var inputs = ResolverTestCorpus.Inputs(
            preferAxis: "vibes",
            allowlist: new[] { "balanced", "cheap-fast" });

        var r = ResolveSmartDefault.Pick(reg, inputs);

        Assert.Equal("balanced", r.Model);
        Assert.Equal(ResolutionReason.ALLOWLIST_HEAD, r.ReasonCode);
        // The decision to ignore unknown axes is documented in HumanReason
        // so users (and future debuggers) see why their --prefer flag had
        // no effect.
        Assert.Contains("unknown axis", r.HumanReason);
        Assert.Contains("vibes", r.HumanReason);
    }

    [Fact]
    public void Pick_Determinism_100Iterations_IdenticalResult()
    {
        var reg = ResolverTestCorpus.MixedRegistry();
        var inputs = ResolverTestCorpus.Inputs(
            preferAxis: "cost",
            allowlist: ResolverTestCorpus.MixedAllowlist);

        var first = ResolveSmartDefault.Pick(reg, inputs);
        for (int i = 0; i < 100; i++)
        {
            var r = ResolveSmartDefault.Pick(reg, inputs);
            Assert.Equal(first.Model, r.Model);
            Assert.Equal(first.ReasonCode, r.ReasonCode);
            Assert.Equal(first.HumanReason, r.HumanReason);
        }
    }

    [Fact]
    public void Pick_HumanReason_AllPaths_WithinLengthCap()
    {
        var reg = ResolverTestCorpus.MixedRegistry();
        var samples = new[]
        {
            ResolveSmartDefault.Pick(reg, ResolverTestCorpus.Inputs(explicitModel: "x", allowlist: new[] { "a" })),
            ResolveSmartDefault.Pick(reg, ResolverTestCorpus.Inputs(preferAxis: "cost", allowlist: ResolverTestCorpus.MixedAllowlist)),
            ResolveSmartDefault.Pick(reg, ResolverTestCorpus.Inputs(preferAxis: "latency", allowlist: ResolverTestCorpus.MixedAllowlist)),
            ResolveSmartDefault.Pick(reg, ResolverTestCorpus.Inputs(preferAxis: "quality", allowlist: ResolverTestCorpus.MixedAllowlist)),
            ResolveSmartDefault.Pick(reg, ResolverTestCorpus.Inputs(allowlist: new[] { "cheap-fast" })),
            ResolveSmartDefault.Pick(reg, ResolverTestCorpus.Inputs(allowlist: System.Array.Empty<string>())),
            ResolveSmartDefault.Pick(reg, ResolverTestCorpus.Inputs(allowlist: new[] { "ghost" })),
            ResolveSmartDefault.Pick(reg, ResolverTestCorpus.Inputs(preferAxis: "vibes", allowlist: new[] { "cheap-fast" })),
        };
        foreach (var s in samples)
        {
            Assert.True(s.HumanReason.Length <= 120,
                $"HumanReason exceeded 120 chars ({s.HumanReason.Length}): {s.HumanReason}");
        }
    }

    [Fact]
    public void Pick_HumanReason_AllPaths_AsciiOnly()
    {
        var reg = ResolverTestCorpus.MixedRegistry();
        var samples = new[]
        {
            ResolveSmartDefault.Pick(reg, ResolverTestCorpus.Inputs(explicitModel: "x", allowlist: new[] { "a" })),
            ResolveSmartDefault.Pick(reg, ResolverTestCorpus.Inputs(preferAxis: "cost", allowlist: ResolverTestCorpus.MixedAllowlist)),
            ResolveSmartDefault.Pick(reg, ResolverTestCorpus.Inputs(preferAxis: "latency", allowlist: ResolverTestCorpus.MixedAllowlist)),
            ResolveSmartDefault.Pick(reg, ResolverTestCorpus.Inputs(preferAxis: "quality", allowlist: ResolverTestCorpus.MixedAllowlist)),
            ResolveSmartDefault.Pick(reg, ResolverTestCorpus.Inputs(allowlist: new[] { "cheap-fast" })),
            ResolveSmartDefault.Pick(reg, ResolverTestCorpus.Inputs(allowlist: System.Array.Empty<string>())),
            ResolveSmartDefault.Pick(reg, ResolverTestCorpus.Inputs(allowlist: new[] { "ghost" })),
            ResolveSmartDefault.Pick(reg, ResolverTestCorpus.Inputs(preferAxis: "vibes", allowlist: new[] { "cheap-fast" })),
        };
        foreach (var s in samples)
        {
            foreach (var c in s.HumanReason)
            {
                Assert.True(c >= 0x20 && c <= 0x7E,
                    $"Non-ASCII char U+{((int)c):X4} in HumanReason: {s.HumanReason}");
            }
        }
    }

    [Fact]
    public void Pick_IsCapabilityGateBlind_ReturnsAxisWinnerLackingCapability()
    {
        // The picker MUST NOT consult capability tags. The downstream
        // CapabilityGate (E03) names the resolved model in its rejection;
        // if the picker silently skipped axis winners that lacked a
        // capability the user would never see the correct signal.
        var reg = ResolverTestCorpus.Registry(
            ResolverTestCorpus.Entry("no-tools-but-cheap",
                costTier: "low",
                capabilities: new[] { "streaming" }),
            ResolverTestCorpus.Entry("has-tools-expensive",
                costTier: "high",
                capabilities: new[] { "streaming", "tool_calls" }));
        var inputs = ResolverTestCorpus.Inputs(
            preferAxis: "cost",
            allowlist: new[] { "no-tools-but-cheap", "has-tools-expensive" });

        var r = ResolveSmartDefault.Pick(reg, inputs);

        Assert.Equal("no-tools-but-cheap", r.Model);
        Assert.Equal(ResolutionReason.PREFER_AXIS, r.ReasonCode);
    }

    [Fact]
    public void Pick_ExplicitWhitespaceOnly_TreatedAsNull()
    {
        var reg = ResolverTestCorpus.MixedRegistry();
        var inputs = new ResolverInputs(
            ExplicitModel: "   ",
            PreferAxis: null,
            Allowlist: new[] { "balanced" });

        var r = ResolveSmartDefault.Pick(reg, inputs);

        Assert.Equal("balanced", r.Model);
        Assert.Equal(ResolutionReason.ALLOWLIST_HEAD, r.ReasonCode);
    }

    [Fact]
    public void Pick_PreferAxisWhitespaceOnly_TreatedAsNull_HeadWithoutAxisNote()
    {
        var reg = ResolverTestCorpus.MixedRegistry();
        var inputs = new ResolverInputs(
            ExplicitModel: null,
            PreferAxis: "   ",
            Allowlist: new[] { "balanced" });

        var r = ResolveSmartDefault.Pick(reg, inputs);

        Assert.Equal("balanced", r.Model);
        Assert.Equal(ResolutionReason.ALLOWLIST_HEAD, r.ReasonCode);
        Assert.DoesNotContain("unknown axis", r.HumanReason);
    }

    [Fact]
    public void Pick_AllAxesMissingFromRegistry_ReturnsAllowlistOrderHead()
    {
        // Axis set, but no entry in the allowlist has the tier field
        // populated. Stable sort by allowlist index returns the head.
        var reg = ResolverTestCorpus.Registry(
            ResolverTestCorpus.Entry("a"),
            ResolverTestCorpus.Entry("b"),
            ResolverTestCorpus.Entry("c"));
        var inputs = ResolverTestCorpus.Inputs(
            preferAxis: "quality",
            allowlist: new[] { "a", "b", "c" });

        var r = ResolveSmartDefault.Pick(reg, inputs);

        Assert.Equal("a", r.Model);
        Assert.Equal(ResolutionReason.PREFER_AXIS, r.ReasonCode);
    }

    [Fact]
    public void ResolutionReason_ConstantsAreLockedFour()
    {
        // Lock the four reason codes. If anyone adds or renames a code
        // they will break this test and trigger a brief revision.
        Assert.Equal("EXPLICIT", ResolutionReason.EXPLICIT);
        Assert.Equal("PREFER_AXIS", ResolutionReason.PREFER_AXIS);
        Assert.Equal("ALLOWLIST_HEAD", ResolutionReason.ALLOWLIST_HEAD);
        Assert.Equal("FALLBACK", ResolutionReason.FALLBACK);
    }

    // ====================================================================
    // S04E05 Wave 2 (Puddy) -- adversarial + corner cases.
    // Adds 18 facts to the original 18 (target: 33+ total).
    // FDR's adversarial mini-corpus is folded in (whitespace allowlist,
    // all-null tiers + axis request, unknown axis string) since FDR is
    // not dispatched as a separate Wave 3 this episode.
    // ====================================================================

    [Fact]
    public void Pick_Determinism_1000Iterations_IdenticalResult()
    {
        // W2: stress the 100-iteration bound from Wave 1 to 1000 over a
        // 7-entry allowlist with mixed tier coverage. If hidden state
        // existed (clock, dictionary order, statics) 1000 trials raises
        // the detection probability without making CI flaky.
        var reg = ResolverTestCorpus.Registry(
            ResolverTestCorpus.Entry("a", costTier: "low", latencyTier: "fast", qualityTier: "basic"),
            ResolverTestCorpus.Entry("b", costTier: "medium", latencyTier: "medium", qualityTier: "standard"),
            ResolverTestCorpus.Entry("c", costTier: "high", latencyTier: "slow", qualityTier: "premium"),
            ResolverTestCorpus.Entry("d", costTier: "low", latencyTier: "slow", qualityTier: "standard"),
            ResolverTestCorpus.Entry("e", costTier: "high", latencyTier: "fast", qualityTier: "basic"),
            ResolverTestCorpus.Entry("f", costTier: "medium", latencyTier: "fast", qualityTier: "premium"),
            ResolverTestCorpus.Entry("g"));
        var allowlist = new[] { "a", "b", "c", "d", "e", "f", "g" };

        foreach (var axis in new[] { "cost", "latency", "quality" })
        {
            var inputs = ResolverTestCorpus.Inputs(preferAxis: axis, allowlist: allowlist);
            var first = ResolveSmartDefault.Pick(reg, inputs);
            for (int i = 0; i < 1000; i++)
            {
                var r = ResolveSmartDefault.Pick(reg, inputs);
                Assert.Equal(first.Model, r.Model);
                Assert.Equal(first.ReasonCode, r.ReasonCode);
                Assert.Equal(first.HumanReason, r.HumanReason);
            }
        }
    }

    [Fact]
    public void Pick_AdversarialAllowlistEntryWithEscByte_NoCrash_FallbackOrEcho()
    {
        // W2 corpus: an entry containing a literal ESC byte (U+001B).
        // Current behaviour: the picker is byte-blind. The registry will
        // not find a match, so the ESC entry routes via ALLOWLIST_HEAD's
        // miss branch to FALLBACK with the entry echoed into the model
        // and the HumanReason. Sanitisation, if any, is the gate's job.
        const string esc = "\u001Bfoo";
        var reg = ResolverTestCorpus.Registry(ResolverTestCorpus.Entry("ok"));
        var inputs = ResolverTestCorpus.Inputs(allowlist: new[] { esc });

        var r = ResolveSmartDefault.Pick(reg, inputs);

        Assert.Equal(esc, r.Model);
        Assert.Equal(ResolutionReason.FALLBACK, r.ReasonCode);
        Assert.Contains(esc, r.HumanReason);
    }

    [Fact]
    public void Pick_AdversarialAllowlistEntryWithNulByte_NoCrash_FallbackOrEcho()
    {
        // W2 corpus: an entry containing a literal NUL byte. Same contract
        // as the ESC case: the picker echoes; the gate decides. Documents
        // that the ASCII guarantee on HumanReason applies only when the
        // inputs themselves are ASCII.
        const string nul = "foo\0bar";
        var reg = ResolverTestCorpus.Registry(ResolverTestCorpus.Entry("ok"));
        var inputs = ResolverTestCorpus.Inputs(allowlist: new[] { nul });

        var r = ResolveSmartDefault.Pick(reg, inputs);

        Assert.Equal(nul, r.Model);
        Assert.Equal(ResolutionReason.FALLBACK, r.ReasonCode);
        Assert.Contains(nul, r.HumanReason);
    }

    [Theory]
    [InlineData("Cost")]
    [InlineData("COST")]
    [InlineData("Cost ")]
    public void Pick_PreferAxis_IsCaseSensitive_NonLowercaseTreatedAsUnknown(string axis)
    {
        // W2 corpus: IsKnownAxis uses StringComparison.Ordinal against
        // lowercase literals. Mixed-case is "unknown" by design; the trim
        // step does not lower-case. Lock the contract so a well-meaning
        // refactor to OrdinalIgnoreCase trips this fact.
        var reg = ResolverTestCorpus.MixedRegistry();
        var inputs = ResolverTestCorpus.Inputs(
            preferAxis: axis,
            allowlist: ResolverTestCorpus.MixedAllowlist);

        var r = ResolveSmartDefault.Pick(reg, inputs);

        Assert.Equal("cheap-fast", r.Model);
        Assert.Equal(ResolutionReason.ALLOWLIST_HEAD, r.ReasonCode);
        Assert.Contains("unknown axis", r.HumanReason);
        Assert.Contains(axis.Trim(), r.HumanReason);
    }

    [Fact]
    public void Pick_PreferAxis_LeadingTrailingWhitespace_TrimmedAndAccepted()
    {
        // W2 corpus: NormalizeOrNull trims, so "  cost  " becomes "cost"
        // and IsKnownAxis accepts it. Documents the trim contract.
        var reg = ResolverTestCorpus.MixedRegistry();
        var inputs = ResolverTestCorpus.Inputs(
            preferAxis: "  cost  ",
            allowlist: ResolverTestCorpus.MixedAllowlist);

        var r = ResolveSmartDefault.Pick(reg, inputs);

        Assert.Equal("cheap-fast", r.Model);
        Assert.Equal(ResolutionReason.PREFER_AXIS, r.ReasonCode);
        Assert.DoesNotContain("unknown axis", r.HumanReason);
    }

    [Fact]
    public void Pick_DuplicateAllowlistEntries_PassThroughStable()
    {
        // W2 corpus: the picker does not dedupe. Duplicates are kept in
        // place and the first occurrence wins on allowlist-index tie
        // break. Dedup is the caller's responsibility (Program.cs splits
        // AZUREOPENAIMODEL verbatim today; if dedup becomes desired, it
        // is a separate concern with its own contract).
        var reg = ResolverTestCorpus.Registry(
            ResolverTestCorpus.Entry("a", costTier: "low"),
            ResolverTestCorpus.Entry("b", costTier: "low"));
        var inputs = ResolverTestCorpus.Inputs(
            preferAxis: "cost",
            allowlist: new[] { "a", "b", "a", "b" });

        var r = ResolveSmartDefault.Pick(reg, inputs);

        Assert.Equal("a", r.Model);
        Assert.Equal(ResolutionReason.PREFER_AXIS, r.ReasonCode);
    }

    [Fact]
    public void Pick_AllSameTier_TieBrokenByAllowlistOrder_ThreeEntries()
    {
        // W2 corpus: extend Wave 1's two-entry tie-break to three entries
        // with identical CostTier=low; assert the head wins for any
        // permutation.
        var reg = ResolverTestCorpus.Registry(
            ResolverTestCorpus.Entry("alpha", costTier: "low"),
            ResolverTestCorpus.Entry("bravo", costTier: "low"),
            ResolverTestCorpus.Entry("charlie", costTier: "low"));

        foreach (var head in new[] { "alpha", "bravo", "charlie" })
        {
            var rest = new[] { "alpha", "bravo", "charlie" }
                .Where(n => !string.Equals(n, head, System.StringComparison.Ordinal))
                .ToArray();
            var allowlist = new[] { head, rest[0], rest[1] };
            var inputs = ResolverTestCorpus.Inputs(
                preferAxis: "cost",
                allowlist: allowlist);

            var r = ResolveSmartDefault.Pick(reg, inputs);

            Assert.Equal(head, r.Model);
            Assert.Equal(ResolutionReason.PREFER_AXIS, r.ReasonCode);
        }
    }

    [Fact]
    public void Pick_AllowlistSizeOne_NoAxis_ReturnsAllowlistHead()
    {
        // W2 corpus: degenerate allowlist of size 1 with no axis. Step 3
        // (ALLOWLIST_HEAD) hits with the lone entry.
        var reg = ResolverTestCorpus.Registry(
            ResolverTestCorpus.Entry("solo", costTier: "medium"));
        var inputs = ResolverTestCorpus.Inputs(allowlist: new[] { "solo" });

        var r = ResolveSmartDefault.Pick(reg, inputs);

        Assert.Equal("solo", r.Model);
        Assert.Equal(ResolutionReason.ALLOWLIST_HEAD, r.ReasonCode);
        Assert.Contains("solo", r.HumanReason);
    }

    [Fact]
    public void Pick_AllowlistSizeOne_WithMatchingAxis_ReturnsPreferAxis()
    {
        // W2 corpus: degenerate allowlist of size 1 with a known axis.
        // RankByAxis returns the lone entry; PREFER_AXIS wins (no
        // runner-up segment in HumanReason).
        var reg = ResolverTestCorpus.Registry(
            ResolverTestCorpus.Entry("solo", costTier: "low"));
        var inputs = ResolverTestCorpus.Inputs(
            preferAxis: "cost",
            allowlist: new[] { "solo" });

        var r = ResolveSmartDefault.Pick(reg, inputs);

        Assert.Equal("solo", r.Model);
        Assert.Equal(ResolutionReason.PREFER_AXIS, r.ReasonCode);
        Assert.DoesNotContain("runner-up", r.HumanReason);
        Assert.Contains("tier low", r.HumanReason);
    }

    [Fact]
    public void Pick_AllowlistSizeOne_HeadMissingFromRegistry_NoAxis_Fallback()
    {
        // W2 corpus: registry does not know the lone allowlist entry.
        // Step 3's TryGet miss falls through to FALLBACK.
        var reg = ResolverTestCorpus.Registry(
            ResolverTestCorpus.Entry("other"));
        var inputs = ResolverTestCorpus.Inputs(allowlist: new[] { "ghost-only" });

        var r = ResolveSmartDefault.Pick(reg, inputs);

        Assert.Equal("ghost-only", r.Model);
        Assert.Equal(ResolutionReason.FALLBACK, r.ReasonCode);
        Assert.Contains("ghost-only", r.HumanReason);
    }

    [Fact]
    public void Pick_ExplicitEmptyString_TreatedAsNull()
    {
        // W2 corpus: ExplicitModel="" should not trigger the EXPLICIT
        // branch (whitespace-only was Wave 1; this locks the bare empty).
        var reg = ResolverTestCorpus.MixedRegistry();
        var inputs = new ResolverInputs(
            ExplicitModel: "",
            PreferAxis: null,
            Allowlist: new[] { "balanced" });

        var r = ResolveSmartDefault.Pick(reg, inputs);

        Assert.Equal("balanced", r.Model);
        Assert.Equal(ResolutionReason.ALLOWLIST_HEAD, r.ReasonCode);
    }

    [Fact]
    public void Pick_PreferCost_NoEntryHasCostTier_StableAllowlistOrderHead()
    {
        // W2 corpus: cost axis requested but every allowlist entry has
        // CostTier="unknown" (the registry's documented missing-marker).
        // Every entry ranks int.MaxValue; allowlist-index tie-break gives
        // the head. Reason stays PREFER_AXIS because the axis was honored
        // even though no signal differentiated the candidates.
        var reg = ResolverTestCorpus.Registry(
            ResolverTestCorpus.Entry("x"),
            ResolverTestCorpus.Entry("y"),
            ResolverTestCorpus.Entry("z"));
        var inputs = ResolverTestCorpus.Inputs(
            preferAxis: "cost",
            allowlist: new[] { "x", "y", "z" });

        var r = ResolveSmartDefault.Pick(reg, inputs);

        Assert.Equal("x", r.Model);
        Assert.Equal(ResolutionReason.PREFER_AXIS, r.ReasonCode);
        Assert.Contains("tier unknown", r.HumanReason);
    }

    [Fact]
    public void Pick_MixedTierCorpus_AxisSwitchesHead()
    {
        // W2 corpus: 3 entries with cost=low + latency=slow vs 2 entries
        // with cost=high + latency=fast. Cost axis picks a "slow but
        // cheap" entry; latency axis picks a "fast but expensive" entry.
        // Demonstrates the picker is axis-faithful and order-independent.
        var reg = ResolverTestCorpus.Registry(
            ResolverTestCorpus.Entry("slow-1", costTier: "low", latencyTier: "slow"),
            ResolverTestCorpus.Entry("slow-2", costTier: "low", latencyTier: "slow"),
            ResolverTestCorpus.Entry("slow-3", costTier: "low", latencyTier: "slow"),
            ResolverTestCorpus.Entry("fast-1", costTier: "high", latencyTier: "fast"),
            ResolverTestCorpus.Entry("fast-2", costTier: "high", latencyTier: "fast"));
        var allowlist = new[] { "fast-1", "slow-1", "fast-2", "slow-2", "slow-3" };

        var costR = ResolveSmartDefault.Pick(reg,
            ResolverTestCorpus.Inputs(preferAxis: "cost", allowlist: allowlist));
        var latR = ResolveSmartDefault.Pick(reg,
            ResolverTestCorpus.Inputs(preferAxis: "latency", allowlist: allowlist));

        Assert.Equal("slow-1", costR.Model);
        Assert.Equal("fast-1", latR.Model);
        Assert.Equal(ResolutionReason.PREFER_AXIS, costR.ReasonCode);
        Assert.Equal(ResolutionReason.PREFER_AXIS, latR.ReasonCode);
    }

    [Theory]
    [InlineData("explicit")]
    [InlineData("prefer-cost")]
    [InlineData("prefer-latency")]
    [InlineData("prefer-quality")]
    [InlineData("allowlist-head")]
    [InlineData("allowlist-head-unknown-axis")]
    public void Pick_HumanReason_NonFallbackReasons_ContainResolvedModelName(string scenario)
    {
        // W2 corpus: every non-FALLBACK reason names the resolved model
        // in HumanReason so a user grepping logs can match deployment
        // names. FALLBACK is excluded because the "empty allowlist"
        // branch has no model to name.
        var reg = ResolverTestCorpus.MixedRegistry();
        var r = scenario switch
        {
            "explicit" => ResolveSmartDefault.Pick(reg,
                ResolverTestCorpus.Inputs(explicitModel: "my-deploy", allowlist: new[] { "cheap-fast" })),
            "prefer-cost" => ResolveSmartDefault.Pick(reg,
                ResolverTestCorpus.Inputs(preferAxis: "cost", allowlist: ResolverTestCorpus.MixedAllowlist)),
            "prefer-latency" => ResolveSmartDefault.Pick(reg,
                ResolverTestCorpus.Inputs(preferAxis: "latency", allowlist: ResolverTestCorpus.MixedAllowlist)),
            "prefer-quality" => ResolveSmartDefault.Pick(reg,
                ResolverTestCorpus.Inputs(preferAxis: "quality", allowlist: ResolverTestCorpus.MixedAllowlist)),
            "allowlist-head" => ResolveSmartDefault.Pick(reg,
                ResolverTestCorpus.Inputs(allowlist: new[] { "balanced", "cheap-fast" })),
            "allowlist-head-unknown-axis" => ResolveSmartDefault.Pick(reg,
                ResolverTestCorpus.Inputs(preferAxis: "vibes", allowlist: new[] { "balanced" })),
            _ => throw new System.ArgumentOutOfRangeException(nameof(scenario)),
        };

        Assert.NotEqual(ResolutionReason.FALLBACK, r.ReasonCode);
        Assert.False(string.IsNullOrEmpty(r.Model), "Resolved model must not be empty for non-FALLBACK paths.");
        Assert.Contains(r.Model, r.HumanReason);
    }

    public static System.Collections.Generic.IEnumerable<object[]> AsciiReasonScenarios()
    {
        yield return new object[] { "explicit" };
        yield return new object[] { "prefer-cost" };
        yield return new object[] { "prefer-latency" };
        yield return new object[] { "prefer-quality" };
        yield return new object[] { "allowlist-head" };
        yield return new object[] { "allowlist-head-unknown-axis" };
        yield return new object[] { "fallback-empty" };
        yield return new object[] { "fallback-head-missing" };
    }

    [Theory]
    [MemberData(nameof(AsciiReasonScenarios))]
    public void Pick_HumanReason_EveryReasonResultCombination_AsciiOnly(string scenario)
    {
        // W2 corpus: ASCII guarantee over every (reason code x result
        // shape) combination the picker can emit when given ASCII inputs.
        // Adversarial non-ASCII inputs (ESC/NUL) are covered separately
        // and explicitly NOT part of this sweep -- the picker echoes,
        // and ASCII applies only to the picker's own additions.
        var reg = ResolverTestCorpus.Registry(
            ResolverTestCorpus.Entry("cheap-fast", costTier: "low", latencyTier: "fast", qualityTier: "basic"),
            ResolverTestCorpus.Entry("balanced", costTier: "medium", latencyTier: "medium", qualityTier: "standard"),
            ResolverTestCorpus.Entry("premium-slow", costTier: "high", latencyTier: "slow", qualityTier: "premium"));
        var full = new[] { "cheap-fast", "balanced", "premium-slow" };

        var r = scenario switch
        {
            "explicit" => ResolveSmartDefault.Pick(reg,
                ResolverTestCorpus.Inputs(explicitModel: "my-deploy", allowlist: new[] { "cheap-fast" })),
            "prefer-cost" => ResolveSmartDefault.Pick(reg,
                ResolverTestCorpus.Inputs(preferAxis: "cost", allowlist: full)),
            "prefer-latency" => ResolveSmartDefault.Pick(reg,
                ResolverTestCorpus.Inputs(preferAxis: "latency", allowlist: full)),
            "prefer-quality" => ResolveSmartDefault.Pick(reg,
                ResolverTestCorpus.Inputs(preferAxis: "quality", allowlist: full)),
            "allowlist-head" => ResolveSmartDefault.Pick(reg,
                ResolverTestCorpus.Inputs(allowlist: new[] { "balanced", "cheap-fast" })),
            "allowlist-head-unknown-axis" => ResolveSmartDefault.Pick(reg,
                ResolverTestCorpus.Inputs(preferAxis: "vibes", allowlist: new[] { "balanced" })),
            "fallback-empty" => ResolveSmartDefault.Pick(reg,
                ResolverTestCorpus.Inputs(allowlist: System.Array.Empty<string>())),
            "fallback-head-missing" => ResolveSmartDefault.Pick(reg,
                ResolverTestCorpus.Inputs(allowlist: new[] { "ghost" })),
            _ => throw new System.ArgumentOutOfRangeException(nameof(scenario)),
        };

        Assert.True(r.HumanReason.Length <= 120,
            $"HumanReason exceeded 120 chars ({r.HumanReason.Length}): {r.HumanReason}");
        foreach (var c in r.HumanReason)
        {
            Assert.True(c >= 0x20 && c <= 0x7E,
                $"Non-ASCII char U+{((int)c):X4} in HumanReason for scenario '{scenario}': {r.HumanReason}");
        }
    }

    // ---- FDR-derived adversarial mini-corpus (Wave 2 fold-in) ----

    [Fact]
    public void Pick_FDR_AllowlistWithSingleWhitespaceEntry_FallbackWithEchoedSpace()
    {
        // FDR seed: an allowlist of size 1 whose single entry is " ". The
        // allowlist is NOT empty (length == 1), so the empty-allowlist
        // FALLBACK branch is bypassed. The registry rejects the lookup
        // and the late FALLBACK branch echoes the whitespace entry. This
        // is the picker's responsibility, not the gate's -- documenting
        // current behaviour so any future scrubbing is intentional.
        var reg = ResolverTestCorpus.Registry(ResolverTestCorpus.Entry("real"));
        var inputs = ResolverTestCorpus.Inputs(allowlist: new[] { " " });

        var r = ResolveSmartDefault.Pick(reg, inputs);

        Assert.Equal(" ", r.Model);
        Assert.Equal(ResolutionReason.FALLBACK, r.ReasonCode);
    }

    [Fact]
    public void Pick_FDR_HeadMatchesButAllTiersNull_AxisCost_PreferAxisWithUnknownTier()
    {
        // FDR seed: registry knows the head, but its card has CostTier=
        // "unknown" and null latency/quality tiers. With axis=cost the
        // ranker produces rank int.MaxValue across the board and the
        // allowlist head wins on index tie-break. Reason stays
        // PREFER_AXIS (the axis was applied even if it produced no
        // signal), HumanReason notes "tier unknown".
        var reg = ResolverTestCorpus.Registry(
            ResolverTestCorpus.Entry("blank-card"));
        var inputs = ResolverTestCorpus.Inputs(
            preferAxis: "cost",
            allowlist: new[] { "blank-card" });

        var r = ResolveSmartDefault.Pick(reg, inputs);

        Assert.Equal("blank-card", r.Model);
        Assert.Equal(ResolutionReason.PREFER_AXIS, r.ReasonCode);
        Assert.Contains("tier unknown", r.HumanReason);
    }

    [Fact]
    public void Pick_FDR_UnknownAxisString_FallsThroughWithEchoedAxis()
    {
        // FDR seed: PreferAxis="foobar". IsKnownAxis is false; step 3
        // takes over and the ALLOWLIST_HEAD branch surfaces the ignored
        // axis name verbatim. Locks the contract that no exception is
        // thrown for unknown axes.
        var reg = ResolverTestCorpus.MixedRegistry();
        var inputs = ResolverTestCorpus.Inputs(
            preferAxis: "foobar",
            allowlist: new[] { "balanced", "cheap-fast" });

        var r = ResolveSmartDefault.Pick(reg, inputs);

        Assert.Equal("balanced", r.Model);
        Assert.Equal(ResolutionReason.ALLOWLIST_HEAD, r.ReasonCode);
        Assert.Contains("unknown axis", r.HumanReason);
        Assert.Contains("foobar", r.HumanReason);
    }

    // S04E05 W3 -- F-PICKER-TRACE-01 close-out. Pick is documented as pure
    // (no I/O, no env reads, no statics). Re-call N times across all four
    // reason codes and assert byte-equal results -- if Pick had any hidden
    // side channel (cached state, clock skew leaking into HumanReason),
    // determinism would break. Stderr emission moved to the Program.cs call
    // site; this fact pins the Pick contract.
    [Fact]
    public void Pick_IsPure_RepeatedCallsYieldIdenticalResultsAcrossAllReasonCodes()
    {
        var reg = ResolverTestCorpus.MixedRegistry();
        var inputSets = new[]
        {
            ResolverTestCorpus.Inputs(explicitModel: "user-pick"),
            ResolverTestCorpus.Inputs(preferAxis: "cost", allowlist: ResolverTestCorpus.MixedAllowlist),
            ResolverTestCorpus.Inputs(allowlist: new[] { "balanced", "cheap-fast" }),
            ResolverTestCorpus.Inputs(allowlist: System.Array.Empty<string>()),
        };
        foreach (var inputs in inputSets)
        {
            var first = ResolveSmartDefault.Pick(reg, inputs);
            for (int i = 0; i < 25; i++)
            {
                var r = ResolveSmartDefault.Pick(reg, inputs);
                Assert.Equal(first.Model, r.Model);
                Assert.Equal(first.ReasonCode, r.ReasonCode);
                Assert.Equal(first.HumanReason, r.HumanReason);
            }
        }
    }
}
