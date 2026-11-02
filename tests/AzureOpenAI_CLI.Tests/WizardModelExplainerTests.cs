using AzureOpenAI_CLI.Registry;
using AzureOpenAI_CLI.Resolution;
using Xunit;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// S04E08 -- The Onboarding. Locks the "why this model" panel the setup wizard
/// prints. Each test states the behavior it proves, so a later cleanup does not
/// mistake it for an orphan.
/// </summary>
public class WizardModelExplainerTests
{
    // Names the registry "knows". Anything else is an unregistered (custom) deployment.
    private static IModelRegistry Registry(params string[] known)
        => new ArrayModelRegistry(known.Select(n => new ModelRegistryEntry(n, "azure", Array.Empty<string>(), 128000, "standard", null)).ToArray());

    private static string[] Explain(string csv, params string[] known)
        => WizardModelExplainer.Explain(Registry(known), csv);

    // Prose checks must not depend on where the wrapper breaks a line.
    private static string Reflow(string[] lines)
        => string.Join(" ", lines.Select(l => l.Trim()));

    // Proves: the panel can never disagree with the runtime. The default it names is
    // exactly what ResolveSmartDefault.Pick returns for the same list -- there is no
    // second decision-maker. If someone changes the picker, this keeps the panel honest.
    [Theory]
    [InlineData("gpt-4o-mini")]
    [InlineData("gpt-4o-mini,gpt-4o")]
    [InlineData("my-custom-deploy,gpt-4o")]
    [InlineData("  gpt-4o ,  o3  ")]
    public void Explain_DefaultAlwaysMatchesThePicker(string csv)
    {
        var registry = Registry("gpt-4o-mini", "gpt-4o", "o3");
        var allowlist = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var expected = ResolveSmartDefault.Pick(registry, new ResolverInputs(null, null, allowlist)).Model;

        var lines = WizardModelExplainer.Explain(registry, csv);

        Assert.Equal("Azure default model: " + expected, lines[0]);
    }

    // Proves: a known model gets the plain rule and no scary note.
    [Fact]
    public void Explain_KnownHead_StatesTheRuleAndHasNoNote()
    {
        var lines = Explain("gpt-4o-mini", "gpt-4o-mini");

        Assert.Contains("first in your list", lines[1]);
        Assert.DoesNotContain(lines, l => l.Contains("Note:"));
    }

    // Proves: an unregistered deployment name is explained as NORMAL, not as an error.
    // The picker returns FALLBACK here and its raw text says "fix AZUREOPENAIMODEL",
    // which would alarm a first-time user whose deployment is simply named "my-gpt4".
    [Fact]
    public void Explain_UnregisteredHead_IsPresentedAsNormalNotAnError()
    {
        var lines = Explain("my-gpt4", "gpt-4o-mini");
        var text = Reflow(lines);

        Assert.Equal("Azure default model: my-gpt4", lines[0]);
        Assert.Contains("not in hopper's built-in model list", text);
        Assert.Contains("normal for a custom deployment name", text);
        Assert.Contains("skips capability checks", text);
        foreach (var alarming in new[] { "error", "invalid", "failed", "fix AZUREOPENAIMODEL" })
        {
            Assert.DoesNotContain(alarming, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    // Proves: one model means no "also in your list" clutter.
    [Fact]
    public void Explain_SingleModel_OmitsAlsoLine()
    {
        var lines = Explain("gpt-4o-mini", "gpt-4o-mini");

        Assert.DoesNotContain(lines, l => l.Contains("Also in your list"));
        Assert.DoesNotContain(lines, l => l.Contains("--model"));
    }

    // Proves: with several models the user is told the others exist and how to use one.
    [Fact]
    public void Explain_MultipleModels_ListsTheOthersAndTheFlag()
    {
        var lines = Explain("gpt-4o-mini,gpt-4o,o3", "gpt-4o-mini", "gpt-4o", "o3");
        var text = Reflow(lines);

        Assert.Contains("Also in your list: gpt-4o, o3.", text);
        Assert.Contains("--model <name>", text);
        Assert.DoesNotContain("Also in your list: gpt-4o-mini", text); // the default is not repeated
    }

    // Proves: the panel always ends by telling the user how to change the default.
    [Fact]
    public void Explain_AlwaysEndsWithHowToChangeTheDefault()
    {
        var lines = Explain("gpt-4o-mini", "gpt-4o-mini");

        Assert.Contains("re-run 'hopper --setup'", lines[^1]);
    }

    // Proves: nothing to explain yields nothing, so the wizard prints no empty panel.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(",,,")]
    [InlineData(" , ,")]
    [InlineData(null)]
    public void Explain_NoModels_ReturnsEmpty(string? csv)
    {
        Assert.Empty(WizardModelExplainer.Explain(Registry("gpt-4o"), csv));
    }

    // Proves: screen-reader and narrow-terminal safety (Mickey). Every line fits in 78
    // columns and is plain printable ASCII -- no color, no box drawing -- even with
    // many models, which forces the wrap path.
    [Theory]
    [InlineData("gpt-4o-mini")]
    [InlineData("unknown-deployment-name")]
    [InlineData("a1,b2,c3,d4,e5,f6,g7,h8,i9,j10,k11,l12,m13,n14,o15,p16,q17,r18,s19,t20,u21,v22")]
    [InlineData("gpt-4o-mini,my-extremely-long-custom-deployment-name-one,my-extremely-long-custom-deployment-name-two,third")]
    public void Explain_EveryLineFitsAndIsPlainAscii(string csv)
    {
        foreach (var line in Explain(csv, "gpt-4o-mini"))
        {
            Assert.True(line.Length <= WizardModelExplainer.MaxLineWidth,
                $"line is {line.Length} cols: '{line}'");
            Assert.All(line, ch => Assert.InRange(ch, ' ', '~'));
        }
    }

    // Proves: wrapping never splits a model name. A split name would be copied
    // wrongly into --model. A single over-long token stays whole on its own line.
    [Fact]
    public void Explain_WrapNeverSplitsAModelName()
    {
        var longName = new string('x', 90);
        var lines = Explain("gpt-4o-mini," + longName, "gpt-4o-mini");

        Assert.Contains(lines, l => l.Contains(longName));
    }

    // Proves: terminal-injection guard (Newman, same threat as S04E01 F-02). Model names
    // are echoed from user input; an ESC/CSI sequence must not reach the terminal.
    [Fact]
    public void Explain_ControlCharactersInNames_AreScrubbed()
    {
        var lines = Explain("gpt\u001b[31mred,ok\u0007bell", "gpt-4o-mini");
        var text = string.Join("\n", lines);

        Assert.DoesNotContain('\u001b', text);
        Assert.DoesNotContain('\u0007', text);
        Assert.Contains("gpt?[31mred", text);
    }

    // Proves: a null registry is a programming error, surfaced loudly and not as a
    // silent empty panel.
    [Fact]
    public void Explain_NullRegistry_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => WizardModelExplainer.Explain(null!, "gpt-4o"));
    }
}
