using System.Text.Json;
using AzureOpenAI_CLI.Registry;
using AzureOpenAI_CLI.Resolution;
using Xunit;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// S04E09 -- The Override. Locks <c>--prefer</c> and <c>--why-this-model</c>. Each test states
/// what it proves, so a later cleanup does not mistake it for an orphan.
/// </summary>
[Collection("ConsoleCapture")]
public class PreferAndWhyThisModelTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static ModelRegistryEntry Entry(string name, string cost = "unknown",
        string? latency = null, string? quality = null)
        => new(name, "azure", Array.Empty<string>(), 128000, cost, null, latency, quality);

    private static IModelRegistry Reg(params ModelRegistryEntry[] entries) => new ArrayModelRegistry(entries);

    // Two models whose rankings disagree on every axis, so an axis visibly changes the pick.
    private static IModelRegistry Tiered() => Reg(
        Entry("slow-big", cost: "high", latency: "slow", quality: "premium"),
        Entry("fast-small", cost: "low", latency: "fast", quality: "basic"));

    private static ModelDecision Decide(IModelRegistry r, string[] allowlist, string? prefer = null,
        string? explicitModel = null, string? configDefault = null)
        => ModelDecisionExplainer.Decide(r, explicitModel, prefer, allowlist, configDefault, "hard-fallback");

    // ── the override actually overrides ──────────────────────────────────────

    // Proves: with no preference the list order wins, the behavior every existing user has.
    [Fact]
    public void NoPrefer_UsesTheHeadOfTheList()
    {
        var d = Decide(Tiered(), new[] { "slow-big", "fast-small" });

        Assert.Equal("slow-big", d.Model);
        Assert.Equal(ResolutionReason.ALLOWLIST_HEAD, d.ReasonCode);
        Assert.Null(d.Note);
    }

    // Proves: each axis really re-orders the list. The two models are built so no single
    // model wins every axis; if --prefer were a no-op this would fail.
    [Theory]
    [InlineData("latency", "fast-small")]
    [InlineData("cost", "fast-small")]
    [InlineData("quality", "slow-big")]
    public void Prefer_ReordersByTheChosenAxis(string axis, string expected)
    {
        // Head of the list is slow-big, so "fast-small" wins only if the axis truly overrode it.
        var d = Decide(Tiered(), new[] { "slow-big", "fast-small" }, prefer: axis);

        Assert.Equal(expected, d.Model);
        Assert.Equal(ResolutionReason.PREFER_AXIS, d.ReasonCode);
        Assert.Null(d.Note);
    }

    // Proves: order in the list does not matter once an axis is chosen.
    [Fact]
    public void Prefer_IsIndependentOfListOrder()
    {
        var a = Decide(Tiered(), new[] { "slow-big", "fast-small" }, prefer: "latency");
        var b = Decide(Tiered(), new[] { "fast-small", "slow-big" }, prefer: "latency");

        Assert.Equal("fast-small", a.Model);
        Assert.Equal("fast-small", b.Model);
    }

    // Proves: precedence. --model wins over --prefer, and the user is told --prefer was ignored
    // instead of being left to wonder why their flag had no effect.
    [Fact]
    public void Explicit_BeatsPrefer_AndSaysSo()
    {
        var d = Decide(Tiered(), new[] { "slow-big", "fast-small" }, prefer: "latency", explicitModel: "slow-big");

        Assert.Equal("slow-big", d.Model);
        Assert.Equal(ResolutionReason.EXPLICIT, d.ReasonCode);
        Assert.Contains("--prefer was ignored", d.Note);
    }

    // ── honesty when there is nothing to rank ────────────────────────────────

    // Proves: a stock install ships cost tiers only, so --prefer latency has no data. List order
    // is kept AND the note says so. Presenting this as a real decision would be a lie.
    [Fact]
    public void Prefer_WithNoTierData_KeepsListOrderAndSaysSo()
    {
        var noTiers = Reg(Entry("a"), Entry("b"));

        var d = Decide(noTiers, new[] { "a", "b" }, prefer: "latency");

        Assert.Equal("a", d.Model);
        Assert.Contains("No 'latency' tier data", d.Note);
        Assert.Contains("registry.json", d.Note);
        Assert.Contains("latencyTier", d.Note);
        Assert.Contains("replaces the built-in list", d.Note);
    }

    // Proves: a tier outside the pinned vocabulary counts as "no data" -- the same rule the
    // ranker uses -- so the note cannot claim data that the ranker ignored.
    [Fact]
    public void Prefer_WithUnrecognisedTierValue_CountsAsNoData()
    {
        var d = Decide(Reg(Entry("a", latency: "turbo"), Entry("b", latency: "warp")),
            new[] { "a", "b" }, prefer: "latency");

        Assert.Contains("No 'latency' tier data", d.Note);
    }

    // Proves: one model with data is enough -- no false "no data" note.
    [Fact]
    public void Prefer_WithSomeTierData_HasNoNote()
    {
        var d = Decide(Reg(Entry("a"), Entry("b", latency: "fast")), new[] { "a", "b" }, prefer: "latency");

        Assert.Equal("b", d.Model);
        Assert.Null(d.Note);
    }

    // Proves: a one-entry list gives --prefer nothing to choose between, and it says that,
    // with the right singular/plural.
    [Theory]
    [InlineData(1, "lists 1 model.")]
    [InlineData(0, "lists 0 models.")]
    public void Prefer_WithTooFewModels_ExplainsWhy(int count, string expectedFragment)
    {
        var list = Enumerable.Range(0, count).Select(i => "m" + i).ToArray();

        var d = Decide(Tiered(), list, prefer: "cost");

        Assert.Contains("nothing to choose between", d.Note);
        Assert.Contains(expectedFragment, d.Note);
    }

    // Proves: the last-resort chain matches Program's. Empty list falls to the config default,
    // then to the hard-coded fallback, and the reason stays FALLBACK.
    [Fact]
    public void EmptyAllowlist_FallsBackToConfigThenHardDefault()
    {
        var withConfig = Decide(Tiered(), Array.Empty<string>(), configDefault: "from-config");
        var without = Decide(Tiered(), Array.Empty<string>());

        Assert.Equal("from-config", withConfig.Model);
        Assert.Equal("hard-fallback", without.Model);
        Assert.Equal(ResolutionReason.FALLBACK, withConfig.ReasonCode);
        Assert.Equal(ResolutionReason.FALLBACK, without.ReasonCode);
    }

    // ── no second decision-maker ─────────────────────────────────────────────

    // Proves: --why-this-model can never disagree with the runtime. The model and reason code
    // are exactly the picker's, for every combination of list, axis and explicit model.
    [Theory]
    [InlineData("slow-big,fast-small", null, null)]
    [InlineData("slow-big,fast-small", "latency", null)]
    [InlineData("fast-small,slow-big", "quality", null)]
    [InlineData("unknown-a,unknown-b", "cost", null)]
    [InlineData("slow-big,fast-small", "cost", "fast-small")]
    public void Decide_AlwaysMatchesThePicker(string csv, string? axis, string? explicitModel)
    {
        var reg = Tiered();
        var list = csv.Split(',');

        var pick = ResolveSmartDefault.Pick(reg, new ResolverInputs(explicitModel, axis, list));
        var d = ModelDecisionExplainer.Decide(reg, explicitModel, axis, list, null, "hard-fallback");

        Assert.Equal(pick.Model, d.Model);
        Assert.Equal(pick.ReasonCode, d.ReasonCode);
        Assert.Equal(pick.HumanReason, d.Reason);
    }

    // Proves: HasUsableTier follows the ranker's own rule, so "had data" and "was ranked"
    // cannot drift apart.
    [Theory]
    [InlineData("fast-small", "latency", true)]
    [InlineData("fast-small", "cost", true)]
    [InlineData("slow-big", "quality", true)]
    [InlineData("not-in-registry", "latency", false)]
    public void HasUsableTier_MatchesRanking(string name, string axis, bool expected)
    {
        Assert.Equal(expected, ResolveSmartDefault.HasUsableTier(Tiered(), name, axis));
    }

    [Fact]
    public void HasUsableTier_CostUnknown_IsNotUsable()
    {
        Assert.False(ResolveSmartDefault.HasUsableTier(Reg(Entry("llama", cost: "unknown")), "llama", "cost"));
    }

    // ── axis parsing ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("cost", "cost")]
    [InlineData("  Latency ", "latency")]
    [InlineData("QUALITY", "quality")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void NormalizeAxis_TrimsAndLowercases(string? raw, string? expected)
    {
        Assert.Equal(expected, ModelDecisionExplainer.NormalizeAxis(raw));
    }

    [Theory]
    [InlineData("cost", true)]
    [InlineData("latency", true)]
    [InlineData("quality", true)]
    [InlineData("speed", false)]
    [InlineData("Cost", false)]   // validation is on the normalized value
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidAxis_AcceptsOnlyTheThreeAxes(string? axis, bool expected)
    {
        Assert.Equal(expected, ModelDecisionExplainer.IsValidAxis(axis));
    }

    // ── rendering ────────────────────────────────────────────────────────────

    // Proves: plain labeled lines (screen-reader and log friendly), with the note when present.
    [Fact]
    public void Render_ProducesLabeledAsciiLines()
    {
        var d = Decide(Reg(Entry("a"), Entry("b")), new[] { "a", "b" }, prefer: "latency");

        var lines = ModelDecisionExplainer.Render(d);

        Assert.StartsWith("Model: a", lines[0]);
        Assert.StartsWith("Reason code: PREFER_AXIS", lines[1]);
        Assert.StartsWith("Why: ", lines[2]);
        Assert.Equal("Prefer: latency", lines[3]);
        Assert.Equal("Candidates: a, b", lines[4]);
        Assert.StartsWith("Note: ", lines[5]);
        Assert.All(string.Concat(lines), ch => Assert.InRange(ch, ' ', '~'));
    }

    // Proves: with no preference and no candidates the labels still read sensibly, and no
    // empty "Note:" line is printed.
    [Fact]
    public void Render_NoPreferNoCandidates_UsesPlaceholders()
    {
        var lines = ModelDecisionExplainer.Render(Decide(Tiered(), Array.Empty<string>()));

        Assert.Contains("Prefer: (none)", lines);
        Assert.Contains(lines, l => l.StartsWith("Candidates: (none"));
        Assert.DoesNotContain(lines, l => l.StartsWith("Note:"));
    }

    // Proves: terminal-injection guard (same threat as S04E01 F-02). Names come from the
    // environment, so an ESC sequence in AZUREOPENAIMODEL must not reach the terminal.
    [Fact]
    public void Render_ScrubsControlCharacters()
    {
        var d = Decide(Reg(), new[] { "evil\u001b[31mred" });

        var text = string.Join("\n", ModelDecisionExplainer.Render(d));

        Assert.DoesNotContain('\u001b', text);
    }

    // ── command-line parsing ─────────────────────────────────────────────────

    [Theory]
    [InlineData("cost", "cost")]
    [InlineData("LATENCY", "latency")]
    [InlineData("  quality ", "quality")]
    public void ParseArgs_Prefer_IsNormalised(string raw, string expected)
    {
        var opts = Program.ParseArgs(new[] { "--prefer", raw, "prompt" });

        Assert.Equal(expected, opts.Prefer);
        Assert.False(opts.ParseError);
    }

    // Proves: a typo is rejected loudly (not silently ignored by the picker, which would run
    // the wrong model without a word), and the message lists the valid choices.
    [Theory]
    [InlineData("speed")]
    [InlineData("cheap")]
    [InlineData("")]
    public void ParseArgs_Prefer_RejectsUnknownAxis(string raw)
    {
        using var err = new StringWriter();
        var old = Console.Error;
        try
        {
            Console.SetError(err);
            var opts = Program.ParseArgs(new[] { "--prefer", raw });

            Assert.True(opts.ParseError);
            Assert.Contains("--prefer must be one of: cost, latency, quality", err.ToString());
        }
        finally { Console.SetError(old); }
    }

    [Fact]
    public void ParseArgs_Prefer_RequiresAValue()
    {
        using var err = new StringWriter();
        var old = Console.Error;
        try
        {
            Console.SetError(err);
            var opts = Program.ParseArgs(new[] { "--prefer" });

            Assert.True(opts.ParseError);
            Assert.Contains("--prefer requires an axis", err.ToString());
        }
        finally { Console.SetError(old); }
    }

    [Fact]
    public void ParseArgs_WhyThisModel_DefaultsOffAndParsesOn()
    {
        Assert.False(Program.ParseArgs(new[] { "prompt" }).WhyThisModel);
        Assert.True(Program.ParseArgs(new[] { "--why-this-model" }).WhyThisModel);
    }

    // ── the command ──────────────────────────────────────────────────────────

    private static string RunCommand(string[] args, string? allowlist, out int rc)
    {
        var oldEnv = Environment.GetEnvironmentVariable("AZUREOPENAIMODEL");
        var oldOut = Console.Out;
        using var sw = new StringWriter();
        try
        {
            Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", allowlist);
            Console.SetOut(sw);
            rc = Program.WhyThisModelCommand(Program.ParseArgs(args), new UserConfig());
        }
        finally
        {
            Console.SetOut(oldOut);
            Environment.SetEnvironmentVariable("AZUREOPENAIMODEL", oldEnv);
        }
        return sw.ToString();
    }

    // Proves: the command prints the labeled decision and exits 0 without credentials.
    [Fact]
    public void Command_Text_PrintsTheDecisionAndExitsZero()
    {
        var output = RunCommand(new[] { "--why-this-model" }, "alpha,beta", out var rc);

        Assert.Equal(0, rc);
        Assert.Contains("Model: alpha", output);
        Assert.Contains("Reason code:", output);
        Assert.Contains("Candidates: alpha, beta", output);
    }

    // Proves: the machine envelope has the documented snake_case keys, and null fields are
    // omitted (the repo-wide JSON convention).
    [Fact]
    public void Command_Json_HasTheDocumentedShape()
    {
        var output = RunCommand(new[] { "--why-this-model", "--json" }, "alpha,beta", out var rc);

        Assert.Equal(0, rc);
        using var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;
        Assert.Equal("alpha", root.GetProperty("model").GetString());
        Assert.False(string.IsNullOrEmpty(root.GetProperty("reason_code").GetString()));
        Assert.False(string.IsNullOrEmpty(root.GetProperty("reason").GetString()));
        Assert.Equal(2, root.GetProperty("candidates").GetArrayLength());
        Assert.False(root.TryGetProperty("prefer", out _));
        Assert.False(root.TryGetProperty("note", out _));
    }

    // Proves: through the real command, a preference with no registry data reports itself
    // (the registry is empty in unit tests, so no model has tiers).
    [Fact]
    public void Command_PreferWithoutTierData_ReportsTheNote()
    {
        var output = RunCommand(new[] { "--why-this-model", "--prefer", "latency", "--json" }, "alpha,beta", out _);

        using var doc = JsonDocument.Parse(output);
        Assert.Equal("latency", doc.RootElement.GetProperty("prefer").GetString());
        Assert.Contains("No 'latency' tier data", doc.RootElement.GetProperty("note").GetString());
    }

    // Proves: no allowlist at all still answers (hard fallback), exit 0.
    [Fact]
    public void Command_WithNoAllowlist_AnswersWithTheFallback()
    {
        var output = RunCommand(new[] { "--why-this-model" }, null, out var rc);

        Assert.Equal(0, rc);
        Assert.Contains("Reason code: FALLBACK", output);
        Assert.Contains("Candidates: (none", output);
    }
}
