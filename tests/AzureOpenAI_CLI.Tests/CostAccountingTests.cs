// CostAccountingTests.cs -- S02E09 The Receipt.
//
// Behaviour tests for CostAccounting (price table, accumulator, formatter).
// One behaviour per test, Given/When/Then naming (ADR-003).
//
// "Tokens always. Dollars when known. NEVER guess." -- Morty

using AzureOpenAI_CLI;

namespace AzureOpenAI_CLI.Tests;

[Trait("type", "behavior")]
public class CostAccountingTests
{
    // ── price-table lookup ────────────────────────────────────────────

    [Fact]
    public void Given_KnownModel_When_LookupPrice_Then_ReturnsRow() =>
        Assert.NotNull(CostAccounting.LookupPrice("gpt-4o-mini"));

    [Fact]
    public void Given_KnownModel_When_HasPrice_Then_True() =>
        Assert.True(CostAccounting.HasPrice("gpt-4o"));

    [Fact]
    public void Given_UnknownModel_When_HasPrice_Then_False() =>
        Assert.False(CostAccounting.HasPrice("imaginary-model-9000"));

    [Fact]
    public void Given_NullDeployment_When_HasPrice_Then_False() =>
        Assert.False(CostAccounting.HasPrice(null));

    [Fact]
    public void Given_EmptyDeployment_When_HasPrice_Then_False() =>
        Assert.False(CostAccounting.HasPrice(""));

    [Fact]
    public void Given_DeploymentWithSuffix_When_LookupPrice_Then_PrefixMatches()
    {
        // Real Azure deployments often include date suffixes; the longest-
        // prefix fallback should resolve them.
        var row = CostAccounting.LookupPrice("gpt-4o-mini-2024-07-18");
        Assert.NotNull(row);
        Assert.Equal(0.00015m, row!.Value.InputPer1K);
    }

    [Fact]
    public void Given_GptFourOMiniSuffix_When_LookupPrice_Then_BeatsGptFourO()
    {
        // Longest-prefix matters: "gpt-4o-mini-foo" must NOT collapse to "gpt-4o".
        var row = CostAccounting.LookupPrice("gpt-4o-mini-foo");
        Assert.Equal(0.00015m, row!.Value.InputPer1K); // mini, not 4o.
    }

    [Fact]
    public void Given_CaseDifference_When_LookupPrice_Then_StillResolves() =>
        Assert.NotNull(CostAccounting.LookupPrice("GPT-4O-MINI"));

    // ── ComputeUsd ────────────────────────────────────────────────────

    [Fact]
    public void Given_KnownModel_When_ComputeUsd_Then_ReturnsValue()
    {
        // gpt-4o-mini: $0.00015 in / $0.00060 out per 1K.
        // 1000 in + 1000 out = 0.00015 + 0.00060 = 0.00075
        var usd = CostAccounting.ComputeUsd("gpt-4o-mini", 1000, 1000);
        Assert.Equal(0.00075m, usd);
    }

    [Fact]
    public void Given_UnknownModel_When_ComputeUsd_Then_Null() =>
        Assert.Null(CostAccounting.ComputeUsd("nonexistent", 100, 100));

    [Fact]
    public void Given_ZeroTokens_When_ComputeUsd_Then_Zero() =>
        Assert.Equal(0m, CostAccounting.ComputeUsd("gpt-4o-mini", 0, 0));

    // ── Entry (defensive normalisation) ──────────────────────────────

    [Fact]
    public void Given_NullTokenCounts_When_Entry_Then_ClampsToZero()
    {
        var e = CostAccounting.Entry("gpt-4o-mini", null, null);
        Assert.Equal(0, e.InputTokens);
        Assert.Equal(0, e.OutputTokens);
        Assert.Equal(0, e.TotalTokens);
    }

    [Fact]
    public void Given_NegativeTokenCounts_When_Entry_Then_ClampsToZero()
    {
        // Defensive: SDK has been observed to return odd values on stream errors.
        var e = CostAccounting.Entry("gpt-4o-mini", -5, -7);
        Assert.Equal(0, e.InputTokens);
        Assert.Equal(0, e.OutputTokens);
    }

    [Fact]
    public void Given_VeryLargeTokenCounts_When_Entry_Then_NoOverflow()
    {
        // 2 billion tokens -- outside any realistic bill but verify decimal
        // math doesn't blow up.
        var e = CostAccounting.Entry("gpt-4o", 2_000_000_000, 2_000_000_000);
        Assert.NotNull(e.UsdCost);
        Assert.True(e.UsdCost > 0m);
    }

    [Fact]
    public void Given_UnknownModel_When_Entry_Then_TokensCountedDollarsNull()
    {
        var e = CostAccounting.Entry("phantom-model", 100, 200);
        Assert.Equal(100, e.InputTokens);
        Assert.Equal(200, e.OutputTokens);
        Assert.Equal(300, e.TotalTokens);
        Assert.Null(e.UsdCost);
    }

    // ── FormatReceipt ────────────────────────────────────────────────

    [Fact]
    public void Given_KnownModel_When_FormatReceipt_Then_IncludesDollarLine()
    {
        var e = CostAccounting.Entry("gpt-4o", 1000, 500);
        var line = CostAccounting.FormatReceipt(e, "gpt-4o");
        Assert.Contains("[cost] in=1000 out=500 total=1500 tokens", line);
        Assert.Contains("@ gpt-4o", line);
        Assert.Contains("~$", line);
    }

    [Fact]
    public void Given_UnknownModel_When_FormatReceipt_Then_TokensOnlyNoFakeDollar()
    {
        var e = CostAccounting.Entry("imaginary", 100, 50);
        var line = CostAccounting.FormatReceipt(e, "imaginary");
        Assert.Contains("[cost] in=100 out=50 total=150 tokens", line);
        Assert.DoesNotContain("$", line);                       // no fake dollar
        Assert.Contains("not in price table", line);            // honest fallback
    }

    [Fact]
    public void Given_NullDeployment_When_FormatReceipt_Then_TokensOnly()
    {
        var e = CostAccounting.Entry(null, 10, 20);
        var line = CostAccounting.FormatReceipt(e, null);
        Assert.Equal("[cost] in=10 out=20 total=30 tokens", line);
    }

    [Fact]
    public void Given_DollarLine_When_FormatReceipt_Then_UsesInvariantCulture()
    {
        // Guard against locales that use ',' as decimal separator.
        var prev = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture =
                new System.Globalization.CultureInfo("de-DE");
            var e = CostAccounting.Entry("gpt-4o", 1000, 1000);
            var line = CostAccounting.FormatReceipt(e, "gpt-4o");
            Assert.Contains("$0.0125", line); // dot, not comma
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = prev;
        }
    }

    // ── CostAccumulator ──────────────────────────────────────────────

    [Fact]
    public void Given_NewAccumulator_When_Read_Then_AllZero()
    {
        var acc = new CostAccumulator();
        Assert.Equal(0, acc.Calls);
        Assert.Equal(0, acc.InputTokens);
        Assert.Equal(0, acc.OutputTokens);
        Assert.Equal(0, acc.TotalTokens);
        Assert.Equal(0m, acc.UsdCost);
        Assert.False(acc.HasAnyKnownCost);
    }

    [Fact]
    public void Given_TwoKnownEntries_When_Add_Then_DollarsSum()
    {
        var acc = new CostAccumulator();
        acc.Add(CostAccounting.Entry("gpt-4o-mini", 1000, 1000));
        acc.Add(CostAccounting.Entry("gpt-4o-mini", 1000, 1000));
        Assert.Equal(2, acc.Calls);
        Assert.Equal(2000, acc.InputTokens);
        Assert.Equal(2000, acc.OutputTokens);
        Assert.Equal(0.00150m, acc.UsdCost);
        Assert.True(acc.HasAnyKnownCost);
    }

    [Fact]
    public void Given_OnlyUnknownModel_When_Add_Then_TokensSumDollarsZero()
    {
        var acc = new CostAccumulator();
        acc.Add(CostAccounting.Entry("phantom", 100, 200));
        acc.Add(CostAccounting.Entry("phantom", 50, 50));
        Assert.Equal(2, acc.Calls);
        Assert.Equal(150, acc.InputTokens);
        Assert.Equal(250, acc.OutputTokens);
        Assert.Equal(0m, acc.UsdCost);
        Assert.False(acc.HasAnyKnownCost);
    }

    [Fact]
    public void Given_MixedKnownAndUnknown_When_FormatTotal_Then_DollarsShown()
    {
        var acc = new CostAccumulator();
        acc.Add(CostAccounting.Entry("gpt-4o", 100, 100));
        acc.Add(CostAccounting.Entry("phantom", 100, 100));
        var line = CostAccounting.FormatTotalReceipt(acc, "gpt-4o", "agent");
        Assert.Contains("agent: calls=2", line);
        Assert.Contains("in=200 out=200 total=400 tokens", line);
        Assert.Contains("~$", line); // at least one entry contributed
    }

    [Fact]
    public void Given_AllUnknownModel_When_FormatTotal_Then_NoDollar()
    {
        var acc = new CostAccumulator();
        acc.Add(CostAccounting.Entry("phantom", 100, 100));
        var line = CostAccounting.FormatTotalReceipt(acc, "phantom", "ralph");
        Assert.Contains("ralph: calls=1", line);
        Assert.Contains("not in price table", line);
        Assert.DoesNotContain("$", line);
    }
}
