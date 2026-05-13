using System.Linq;
using AzureOpenAI_CLI.Registry;
using AzureOpenAI_CLI.Resolution;
using Xunit;

namespace AzureOpenAI_CLI.Tests;

// S04E05 Wave 2 -- Puddy seeds; Costanza/Maestro Wave 1 wrote these as
// part of the shared-file pass to keep the surface aligned with the
// resolver. Test corpus is factored into ResolverTestCorpus for E11
// reuse (the corpus episode).

internal static class ResolverTestCorpus
{
    public static ModelRegistryEntry Entry(
        string name,
        string costTier = "unknown",
        string? latencyTier = null,
        string? qualityTier = null,
        string[]? capabilities = null)
    {
        return new ModelRegistryEntry(
            Name: name,
            Provider: "azure",
            Capabilities: capabilities ?? new[] { "streaming" },
            ContextWindow: 128000,
            CostTier: costTier,
            CardPath: null,
            LatencyTier: latencyTier,
            QualityTier: qualityTier);
    }

    public static IModelRegistry Registry(params ModelRegistryEntry[] entries)
        => new ArrayModelRegistry(entries);

    public static ResolverInputs Inputs(
        string? explicitModel = null,
        string? preferAxis = null,
        params string[] allowlist)
        => new ResolverInputs(explicitModel, preferAxis, allowlist);

    // Canonical mixed-tier registry shared by multiple facts.
    public static IModelRegistry MixedRegistry() => Registry(
        Entry("cheap-fast", costTier: "low", latencyTier: "fast", qualityTier: "basic"),
        Entry("balanced", costTier: "medium", latencyTier: "medium", qualityTier: "standard"),
        Entry("premium-slow", costTier: "high", latencyTier: "slow", qualityTier: "premium"),
        Entry("missing-tiers", costTier: "unknown"));

    public static readonly string[] MixedAllowlist =
        new[] { "cheap-fast", "balanced", "premium-slow", "missing-tiers" };
}

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
}
