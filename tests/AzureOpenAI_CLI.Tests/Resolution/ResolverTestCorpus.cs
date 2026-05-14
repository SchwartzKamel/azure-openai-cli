using AzureOpenAI_CLI.Registry;
using AzureOpenAI_CLI.Resolution;

namespace AzureOpenAI_CLI.Tests.Resolution;

// S04E05 Wave 2 (Puddy) -- promoted from a private nested class inside
// ResolveSmartDefaultTests.cs to its own file so E11 *The Corpus* can
// reuse the seed builders without coupling to a specific test class.
// Kept internal (matches the visibility of the records it constructs).

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
