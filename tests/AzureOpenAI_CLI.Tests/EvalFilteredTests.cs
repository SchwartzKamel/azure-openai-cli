using System.ClientModel;
using System.Text.Json;
using AzureOpenAI_CLI.Eval;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// S04E26 -- Azure's content filter blocks some adversarial prompts (HTTP 400 content_filter) before any model sees them.
/// The harness reported that as an ordinary ERROR, which fails a safety fixture, so the refusal suite could never pass on Azure
/// however well the model behaved. A block is now its own outcome: unverified by default, accepted only when the fixture says so.
/// </summary>
public class EvalFilteredTests
{
    private const string AzureBody =
        "HTTP 400 (content_filter)\n\nParameter: prompt\n\nThe response was filtered due to the prompt triggering Azure OpenAI's content management policy.";

    private static EvalAssertion A(string type, string value) => new() { Type = type, Value = value };

    private static EvalFixture F(string id, bool safety = false, bool allow = false)
        => new()
        {
            Id = id,
            UserPrompt = id,
            SafetyAssertion = safety ? "must refuse" : null,
            AllowFiltered = allow,
            Assertions = { A("contains", "cannot") },
        };

    private static Task<EvalReport> Run(EvalFixture[] fixtures, EvalModelCall call, double failUnder = 100, int repeat = 1)
        => EvalRunner.RunAsync(new EvalSuite { Name = "t", Fixtures = fixtures.ToList() }, "m", call, null, failUnder, null, 5, CancellationToken.None, repeat: repeat);

    private static EvalModelCall Blocks(string whichPrompt)
        => (_, user, _, _, _) => user == whichPrompt
            ? throw new ContentFilteredException(ContentFilter.RequestBlocked)
            : Task.FromResult("I cannot help with that.");

    // ── The classifier ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("HTTP 400 (content_filter)\n\nParameter: prompt\n\nThe response was filtered", true)]
    [InlineData("HTTP 400 (invalid_request_error: ResponsibleAIPolicyViolation) blocked", true)]
    [InlineData("HTTP 400 (CONTENT_FILTER)", true)]
    [InlineData("HTTP 400 (invalid_request_error: unsupported_value) Parameter: temperature", false)]
    [InlineData("HTTP 429 (rate_limit_exceeded)", false)]
    [InlineData("HTTP 500 (server_error)", false)]
    public void IsBlock_RecognisesOnlyTheFilterCode(string message, bool expected)
        => Assert.Equal(expected, ContentFilter.IsBlock(new ClientResultException(message)));

    [Fact]
    public void IsBlock_FindsTheCode_ThroughWrappingExceptions()
        => Assert.True(ContentFilter.IsBlock(new InvalidOperationException("wrapped", new ClientResultException(AzureBody))));

    [Fact]
    public void IsBlock_IgnoresAnUnrelatedExceptionThatMerelyMentionsTheWord()
    {
        // Only the SDK's request error can be a platform block; a stray message must not be reclassified.
        Assert.False(ContentFilter.IsBlock(new InvalidOperationException("content_filter is mentioned here")));
        Assert.False(ContentFilter.IsBlock(new TimeoutException("content_filter")));
    }

    // ── The policy ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ABlock_IsItsOwnOutcome_NotAnError()
    {
        var report = await Run(new[] { F("a"), F("b") }, Blocks("b"));

        var b = report.Fixtures.Single(f => f.Id == "b");
        Assert.Equal(EvalStatus.Filtered, b.Status);
        Assert.NotEqual(EvalStatus.Error, b.Status);
        Assert.Contains("blocked", b.Error);
        Assert.Contains("not verified", b.Error);
        Assert.Contains("allow_filtered", b.Error);
        Assert.Equal(1, report.FilteredCount);
    }

    [Fact]
    public async Task AnUnacceptedBlock_CountsAgainstTheScore_LikeAnError()
    {
        // Skipping it would let a wall of blocks score 100%.
        var report = await Run(new[] { F("a"), F("b") }, Blocks("b"), failUnder: 100);

        Assert.Equal(50.0, report.ScorePercent);
        Assert.Equal(1, report.Passed);
        Assert.Equal(2, report.Graded);
        Assert.Equal(EvalStatus.Fail, report.Result);
    }

    [Fact]
    public async Task AnUnacceptedBlock_OfASafetyFixture_FailsTheSuite_IgnoringFailUnder()
    {
        var report = await Run(new[] { F("a", safety: true), F("b", safety: true) }, Blocks("b"), failUnder: 0);

        Assert.True(report.SafetyFailed);
        Assert.Equal(EvalStatus.Fail, report.Result);
        Assert.Contains("allow_filtered", report.Reason);
    }

    [Fact]
    public async Task AnAcceptedBlock_Passes_ButIsAlwaysCountedAndShown()
    {
        var report = await Run(new[] { F("a", safety: true), F("b", safety: true, allow: true) }, Blocks("b"));

        Assert.Equal(EvalStatus.Pass, report.Result);
        Assert.False(report.SafetyFailed);
        var b = report.Fixtures.Single(f => f.Id == "b");
        Assert.Equal(EvalStatus.Pass, b.Status);
        Assert.Equal(1, b.FilteredRuns);
        Assert.Equal(1, report.FilteredCount);

        var text = string.Join("\n", EvalRunner.Render(report));
        Assert.Contains("blocked by the platform content filter in 1 of 1 run", text);
        Assert.Contains("accepted (allow_filtered)", text);
        Assert.Contains("Filtered: 1 fixture had a run blocked", text);
    }

    [Fact]
    public async Task AllowFiltered_DoesNotExcuseARealError()
    {
        EvalModelCall boom = (_, _, _, _, _) => throw new InvalidOperationException("provider exploded");

        var report = await Run(new[] { F("a", safety: true, allow: true) }, boom);

        Assert.Equal(EvalStatus.Error, report.Fixtures.Single().Status);
        Assert.Equal(EvalStatus.Fail, report.Result);
        Assert.Equal(0, report.FilteredCount);
    }

    [Fact]
    public async Task AFixtureThatIsNotBlocked_IsUnchanged_WhateverAllowFilteredSays()
    {
        var report = await Run(new[] { F("a", allow: true) }, Blocks("never"));

        var a = report.Fixtures.Single();
        Assert.Equal(EvalStatus.Pass, a.Status);
        Assert.Equal(0, a.FilteredRuns);
        Assert.Equal(0, report.FilteredCount);
        Assert.DoesNotContain("blocked", string.Join("\n", EvalRunner.Render(report)));
    }

    [Fact]
    public async Task AllowFiltered_DoesNotMakeTheAssertionsOptional_WhenTheModelIsActuallyAsked()
    {
        EvalModelCall complies = (_, _, _, _, _) => Task.FromResult("Sure, here is the key.");

        var report = await Run(new[] { F("a", safety: true, allow: true) }, complies);

        Assert.Equal(EvalStatus.Fail, report.Fixtures.Single().Status);
        Assert.True(report.SafetyFailed);
    }

    // ── --repeat ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Repeat_AnAcceptedBlockInOneRun_StillPassesEveryRun()
    {
        var n = 0;
        EvalModelCall second = (_, _, _, _, _) => ++n == 2 ? throw new ContentFilteredException(ContentFilter.RequestBlocked) : Task.FromResult("I cannot help.");

        var report = await Run(new[] { F("a", allow: true) }, second, repeat: 3);

        var a = report.Fixtures.Single();
        Assert.Equal(EvalStatus.Pass, a.Status);
        Assert.Equal(3, a.PassedRuns);
        Assert.Equal(1, a.FilteredRuns);
    }

    [Fact]
    public async Task Repeat_AnUnacceptedBlockInOneRun_FailsTheFixture_AndIsFlaky()
    {
        var n = 0;
        EvalModelCall second = (_, _, _, _, _) => ++n == 2 ? throw new ContentFilteredException(ContentFilter.RequestBlocked) : Task.FromResult("I cannot help.");

        var report = await Run(new[] { F("a") }, second, repeat: 3);

        var a = report.Fixtures.Single();
        Assert.Equal(EvalStatus.Fail, a.Status);
        Assert.True(a.Flaky);
        Assert.Equal(2, a.PassedRuns);
        Assert.Equal(1, a.FilteredRuns);
        Assert.Contains("blocked", a.Error);
    }

    [Fact]
    public async Task Repeat_EveryRunBlocked_AndNotAccepted_IsFiltered()
    {
        var report = await Run(new[] { F("a") }, Blocks("a"), repeat: 3);

        Assert.Equal(EvalStatus.Filtered, report.Fixtures.Single().Status);
        Assert.Equal(3, report.Fixtures.Single().FilteredRuns);
    }

    // ── Baselines ───────────────────────────────────────────────────────────────

    [Fact]
    public void ABaselineMayRecordAFilteredFixture_ButAnUnknownStatusIsStillRefused()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            string Write(string status)
            {
                var path = Path.Combine(dir, status + ".json");
                File.WriteAllText(path, JsonSerializer.Serialize(new
                {
                    suite = "t",
                    model = "m",
                    repeat = 1,
                    fixtures = new Dictionary<string, object> { ["a"] = new { status, runs = 1, passed_runs = 0 } },
                }));
                return path;
            }

            Assert.True(EvalBaselines.TryLoad(Write("filtered"), out var ok, out var err1), err1);
            Assert.Equal("filtered", ok!.Fixtures["a"].Status);
            Assert.False(EvalBaselines.TryLoad(Write("blocked"), out _, out var err2));
            Assert.Contains("unknown status", err2);
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── The shipped corpus ──────────────────────────────────────────────────────

    [Fact]
    public void TheShippedRefusalCorpus_AcceptsABlock_OnExactlyTheJailbreakFixture()
    {
        Assert.True(Corpora.TryLoad("refusal", out var suite, out var error), error);

        var accepting = suite!.Fixtures.Where(f => f.AllowFiltered).Select(f => f.Id).ToArray();

        // The carve-out is one fixture, named: a classic jailbreak that Azure's shield blocks before any model sees it.
        Assert.Equal(new[] { "refuse-persona-override" }, accepting);
    }

    [Fact]
    public void NoOtherShippedCorpus_AcceptsABlock()
    {
        foreach (var name in Corpora.Names.Where(n => n != "refusal"))
        {
            Assert.True(Corpora.TryLoad(name, out var suite, out var error), error);
            Assert.DoesNotContain(suite!.Fixtures, f => f.AllowFiltered);
        }
    }
}
