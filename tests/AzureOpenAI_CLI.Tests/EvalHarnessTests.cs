using System.Text;
using System.Text.Json;
using AzureOpenAI_CLI.Eval;
using Xunit;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// S04E10 -- The Score. Locks the eval harness: the deterministic assertions, the suite schema,
/// the scoring policy and the CLI contract. Each test states what it proves so a later cleanup
/// does not mistake it for an orphan.
/// </summary>
[Collection("ConsoleCapture")]
public sealed class EvalHarnessTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "az-eval-" + Guid.NewGuid().ToString("N"));

    public EvalHarnessTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { /* best effort */ } }

    // ── helpers ──────────────────────────────────────────────────────────────

    private string Write(string name, string json)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, json);
        return path;
    }

    private static EvalAssertion A(string type, string? value = null, int? limit = null, bool ignoreCase = false)
        => new() { Type = type, Value = value, Limit = limit, IgnoreCase = ignoreCase };

    private static EvalFixture F(string id, params EvalAssertion[] assertions) => new()
    {
        Id = id,
        UserPrompt = "prompt for " + id,
        Assertions = assertions.ToList(),
    };

    private static EvalSuite Suite(params EvalFixture[] fixtures)
        => new() { Name = "t", Fixtures = fixtures.ToList() };

    /// <summary>Fake model: records every call and answers from a function of the user prompt.</summary>
    private sealed class FakeModel
    {
        public readonly List<(string System, string User, float Temp, int? Max)> Calls = new();
        private readonly Func<string, string> _answer;
        public FakeModel(Func<string, string> answer) => _answer = answer;

        public Task<string> Call(string system, string user, float temp, int? max, CancellationToken ct)
        {
            Calls.Add((system, user, temp, max));
            return Task.FromResult(_answer(user));
        }
    }

    private static Task<EvalReport> Run(EvalSuite s, FakeModel m, double failUnder = 100, string? filter = null,
        PersonaResolver? personas = null, int timeout = 5)
        => EvalRunner.RunAsync(s, "test-model", m.Call, personas, failUnder, filter, timeout, CancellationToken.None);

    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("not found walking up from test output: " + relative);
    }

    // ── assertions ───────────────────────────────────────────────────────────

    // Proves: each text/length assertion passes and fails on the right side of its boundary.
    [Theory]
    [InlineData("contains", "Paris", null, false, "The capital is Paris.", true)]
    [InlineData("contains", "Paris", null, false, "The capital is paris.", false)]
    [InlineData("contains", "Paris", null, true, "The capital is paris.", true)]
    [InlineData("not_contains", "secret", null, false, "all clear", true)]
    [InlineData("not_contains", "secret", null, false, "a secret here", false)]
    [InlineData("starts_with", "Answer:", null, false, "  Answer: 42", true)]
    [InlineData("starts_with", "Answer:", null, false, "No, Answer: 42", false)]
    [InlineData("regex", @"^\d{3}-\d{4}$", null, false, "555-1234", true)]
    [InlineData("regex", @"^\d{3}-\d{4}$", null, false, "5551234", false)]
    [InlineData("not_regex", @"\bTODO\b", null, false, "done", true)]
    [InlineData("not_regex", @"\bTODO\b", null, false, "TODO: later", false)]
    [InlineData("min_chars", null, 5, false, "12345", true)]
    [InlineData("min_chars", null, 5, false, "1234", false)]
    [InlineData("max_chars", null, 5, false, "12345", true)]
    [InlineData("max_chars", null, 5, false, "123456", false)]
    public void Check_PassesAndFailsOnTheRightSide(string type, string? value, int? limit, bool ic, string output, bool shouldPass)
    {
        var message = EvalAssertions.Check(A(type, value, limit, ic), output);

        Assert.Equal(shouldPass, message is null);
    }

    // Proves: json_valid accepts plain JSON and the single fenced block a model usually emits,
    // and rejects everything else, including a fence that never closes.
    [Theory]
    [InlineData("{\"a\":1}", true)]
    [InlineData("[1,2,3]", true)]
    [InlineData("```json\n{\"a\":1}\n```", true)]
    [InlineData("```\n[1]\n```", true)]
    [InlineData("  \n {\"a\":1} \n", true)]
    [InlineData("here you go: {\"a\":1}", false)]
    [InlineData("{\"a\":", false)]
    [InlineData("", false)]
    [InlineData("```json\n{\"a\":1}", false)]
    public void JsonValid_AcceptsPlainAndFenced(string output, bool expected)
    {
        Assert.Equal(expected, EvalAssertions.IsValidJson(output));
        Assert.Equal(expected, EvalAssertions.Check(A("json_valid"), output) is null);
    }

    // Proves: a catastrophic-backtracking pattern is a FAILED assertion, never a hang and never a
    // pass. An adversarial model output must not be able to stall or sneak through a regex check.
    [Fact]
    public void Regex_Timeout_IsAFailureNotAPass()
    {
        var output = new string('a', 40) + "b";

        var positive = EvalAssertions.Check(A("regex", "^(a+)+$"), output);
        var negative = EvalAssertions.Check(A("not_regex", "^(a+)+$"), output);

        Assert.Contains("timed out", positive);
        Assert.Contains("timed out", negative);
    }

    // Proves: failure messages describe the EXPECTATION and never echo the model's output, which
    // may be long, hold control characters, or be adversarial in exactly the safety fixtures.
    [Fact]
    public void FailureMessages_NeverEchoTheModelOutput()
    {
        var output = "TOPSECRET-OUTPUT\u001b[31m";

        var message = EvalAssertions.Check(A("contains", "needle"), output);

        Assert.NotNull(message);
        Assert.DoesNotContain("TOPSECRET", message);
        Assert.DoesNotContain('\u001b', message);
    }

    // Proves: the expectation that IS echoed is scrubbed and bounded.
    [Fact]
    public void FailureMessages_ScrubAndTruncateTheExpectedValue()
    {
        var message = EvalAssertions.Check(A("contains", "x\u001b[1m" + new string('y', 200)), "nope");

        Assert.NotNull(message);
        Assert.DoesNotContain('\u001b', message);
        Assert.Contains("...", message);
        Assert.True(message!.Length < 160);
    }

    // Proves: schema validation catches a malformed assertion BEFORE any model call is paid for.
    [Theory]
    [InlineData("nonsense", null, null, "unknown type")]
    [InlineData("contains", null, null, "requires a non-empty")]
    [InlineData("regex", "(unclosed", null, "invalid pattern")]
    [InlineData("min_chars", null, null, "requires a non-negative")]
    [InlineData("max_chars", null, -1, "requires a non-negative")]
    public void Validate_RejectsMalformedAssertions(string type, string? value, int? limit, string expected)
    {
        var problem = EvalAssertions.Validate(A(type, value, limit));

        Assert.NotNull(problem);
        Assert.Contains(expected, problem);
    }

    [Fact]
    public void Validate_AcceptsWellFormedAssertions()
    {
        foreach (var a in new[] { A("contains", "x"), A("regex", "a+"), A("min_chars", limit: 0), A("json_valid") })
        {
            Assert.Null(EvalAssertions.Validate(a));
        }
    }

    // Proves: a scorer can never pass on a type it does not know, even if validation was skipped.
    [Fact]
    public void Check_UnknownType_Fails()
    {
        Assert.NotNull(EvalAssertions.Check(A("telepathy", "x"), "anything"));
    }

    // ── suite loading ────────────────────────────────────────────────────────

    // Proves: the fixtures shipped with the design doc load UNCHANGED -- the shipping schema is a
    // strict superset, so nothing already written has to be migrated.
    [Fact]
    public void Loader_LoadsTheExistingDesignDocFixtures()
    {
        var ok = EvalSuiteLoader.TryLoad(RepoFile("docs/prompts/fixtures/coder.json"), out var suite, out var error);

        Assert.True(ok, error);
        Assert.True(suite!.Fixtures.Count >= 3);
        Assert.All(suite.Fixtures, f => Assert.Equal("coder", f.Persona));
    }

    // Proves: the example suite we ship in the docs is itself valid, has a safety fixture that can
    // be verified, and grades something. An example that fails validation teaches the wrong thing.
    [Fact]
    public void Loader_LoadsTheShippedExampleSuite()
    {
        var ok = EvalSuiteLoader.TryLoad(RepoFile("examples/eval/smoke.json"), out var suite, out var error);

        Assert.True(ok, error);
        Assert.Equal("smoke", suite!.Name);
        Assert.Equal(3, suite.Fixtures.Count);
        Assert.All(suite.Fixtures, f => Assert.NotEmpty(f.Assertions));
        Assert.Contains(suite.Fixtures, f => f.IsSafety);
    }

    // Proves: both file shapes work, and the suite name comes from the file when not given.
    [Fact]
    public void Loader_AcceptsArrayAndObjectForms()
    {
        var arr = Write("smoke.json", "[{\"id\":\"a\",\"user_prompt\":\"p\"}]");
        var obj = Write("named.json", "{\"name\":\"My Suite\",\"fixtures\":[{\"id\":\"a\",\"user_prompt\":\"p\"}]}");

        Assert.True(EvalSuiteLoader.TryLoad(arr, out var s1, out _));
        Assert.True(EvalSuiteLoader.TryLoad(obj, out var s2, out _));
        Assert.Equal("smoke", s1!.Name);
        Assert.Equal("My Suite", s2!.Name);
    }

    // Proves: hand-written JSON is allowed to have comments and trailing commas.
    [Fact]
    public void Loader_ToleratesCommentsAndTrailingCommas()
    {
        var path = Write("c.json", "[\n // a comment\n {\"id\":\"a\",\"user_prompt\":\"p\",},\n]");

        Assert.True(EvalSuiteLoader.TryLoad(path, out var suite, out var error), error);
        Assert.Single(suite!.Fixtures);
    }

    [Theory]
    [InlineData("[]", "no fixtures")]
    [InlineData("42", "array of fixtures or an object")]
    [InlineData("{\"name\":\"x\"}", "no fixtures")]
    [InlineData("[{\"user_prompt\":\"p\"}]", "\"id\" is required")]
    [InlineData("[{\"id\":\"a\"}]", "\"user_prompt\" is required")]
    [InlineData("[{\"id\":\"a\",\"user_prompt\":\"p\",\"temperature\":3}]", "between 0.0 and 2.0")]
    [InlineData("[{\"id\":\"a\",\"user_prompt\":\"p\",\"max_tokens\":0}]", "must be positive")]
    [InlineData("[{\"id\":\"a\",\"user_prompt\":\"p\",\"assertions\":[{\"type\":\"nope\"}]}]", "unknown type")]
    [InlineData("[{\"id\":\"a\",\"user_prompt\":\"p\"},{\"id\":\"A\",\"user_prompt\":\"p\"}]", "duplicate id")]
    [InlineData("{not json", "not valid JSON")]
    public void Loader_RejectsBadSuites(string json, string expected)
    {
        var path = Write("bad.json", json);

        Assert.False(EvalSuiteLoader.TryLoad(path, out var suite, out var error));
        Assert.Null(suite);
        Assert.Contains(expected, error);
    }

    // Proves: every problem is reported in one pass (capped), so one run fixes the whole file.
    [Fact]
    public void Loader_ReportsManyProblemsAtOnce_AndCapsTheList()
    {
        var sb = new StringBuilder("[");
        for (var i = 0; i < 15; i++) sb.Append("{\"id\":\"f" + i + "\"},");
        sb.Append("]");

        Assert.False(EvalSuiteLoader.TryLoad(Write("many.json", sb.ToString()), out _, out var error));

        Assert.Contains("fixture 'f0'", error);
        Assert.Contains("and 5 more", error);
        Assert.Equal(11, error!.Split('\n').Length);
    }

    [Fact]
    public void Loader_RejectsMissingAndOversizedFiles()
    {
        Assert.False(EvalSuiteLoader.TryLoad(Path.Combine(_dir, "nope.json"), out _, out var e1));
        Assert.Contains("not found", e1);

        var big = Path.Combine(_dir, "big.json");
        File.WriteAllText(big, new string(' ', (int)EvalSuiteLoader.MaxSuiteBytes + 1));
        Assert.False(EvalSuiteLoader.TryLoad(big, out _, out var e2));
        Assert.Contains("limit", e2);
    }

    // ── running and scoring ──────────────────────────────────────────────────

    // Proves: the happy path, and that a fixture with no temperature runs at 0.0 (deterministic).
    [Fact]
    public async Task Run_AllPass_ScoresOneHundredAndDefaultsToTemperatureZero()
    {
        var model = new FakeModel(_ => "Paris");

        var report = await Run(Suite(F("a", A("contains", "Paris")), F("b", A("max_chars", limit: 10))), model);

        Assert.True(report.Passing);
        Assert.Equal(100.0, report.ScorePercent);
        Assert.Equal(2, report.Passed);
        Assert.All(model.Calls, c => Assert.Equal(0f, c.Temp));
    }

    // Proves: temperature and max_tokens are passed through when a fixture sets them.
    [Fact]
    public async Task Run_PassesTemperatureAndMaxTokensThrough()
    {
        var model = new FakeModel(_ => "x");
        var f = F("a", A("min_chars", limit: 0));
        f.Temperature = 0.7f;
        f.MaxTokens = 123;

        await Run(Suite(f), model);

        Assert.Equal((0.7f, 123), (model.Calls[0].Temp, model.Calls[0].Max));
    }

    // Proves: the prompt is built exactly as the CLI builds it -- default prompt plus the safety
    // clause with no persona, persona prompt plus the safety clause with one.
    [Fact]
    public async Task Run_BuildsTheSystemPromptTheWayTheCliDoes()
    {
        var model = new FakeModel(_ => "x");
        var withPersona = F("p", A("min_chars", limit: 0));
        withPersona.Persona = "coder";
        PersonaResolver personas = (string name, out string? prompt) =>
        {
            prompt = name == "coder" ? "YOU ARE CODER" : null;
            return prompt is not null;
        };

        await Run(Suite(F("plain", A("min_chars", limit: 0)), withPersona), model, personas: personas);

        Assert.Equal(Program.DEFAULT_SYSTEM_PROMPT + "\n\n" + Program.SAFETY_CLAUSE, model.Calls[0].System);
        Assert.Equal("YOU ARE CODER\n\n" + Program.SAFETY_CLAUSE, model.Calls[1].System);
    }

    // Proves: a missing persona is an ERROR that counts against the score, not a silent run with
    // the wrong prompt (which would test something other than what the fixture names).
    [Fact]
    public async Task Run_MissingPersona_IsAnErrorAndCountsAgainstTheScore()
    {
        var model = new FakeModel(_ => "x");
        var f = F("p", A("min_chars", limit: 0));
        f.Persona = "ghost";

        var report = await Run(Suite(F("ok", A("min_chars", limit: 0)), f), model, personas: (string _, out string? p) => { p = null; return false; });

        Assert.Equal(EvalStatus.Error, report.Fixtures[1].Status);
        Assert.Contains("persona 'ghost' not found", report.Fixtures[1].Error);
        Assert.Equal(50.0, report.ScorePercent);
        Assert.Single(model.Calls); // the broken fixture never reached the model
    }

    // Proves: --fail-under is the threshold, inclusive.
    [Theory]
    [InlineData(100, false)]
    [InlineData(51, false)]
    [InlineData(50, true)]
    [InlineData(0, true)]
    public async Task Run_FailUnder_IsAnInclusiveThreshold(double failUnder, bool shouldPass)
    {
        var model = new FakeModel(u => u.Contains("good") ? "yes" : "no");
        var suite = Suite(
            new EvalFixture { Id = "good", UserPrompt = "good", Assertions = { A("contains", "yes") } },
            new EvalFixture { Id = "bad", UserPrompt = "bad", Assertions = { A("contains", "yes") } });

        var report = await Run(suite, model, failUnder);

        Assert.Equal(50.0, report.ScorePercent);
        Assert.Equal(shouldPass, report.Passing);
        if (!shouldPass) Assert.Contains("below --fail-under", report.Reason);
    }

    // Proves: a model that throws is an ERROR counted against the score. Skipping errors would
    // let a provider outage score 100%.
    [Fact]
    public async Task Run_ModelFailure_IsAnErrorNotASkip()
    {
        EvalModelCall boom = (_, _, _, _, _) => throw new InvalidOperationException("provider exploded");

        var report = await EvalRunner.RunAsync(Suite(F("a", A("contains", "x"))), "m", boom, null, 100, null, 5, CancellationToken.None);

        Assert.Equal(EvalStatus.Error, report.Fixtures[0].Status);
        Assert.Contains("model call failed", report.Fixtures[0].Error);
        Assert.False(report.Passing);
        Assert.Equal(0.0, report.ScorePercent);
    }

    // Proves: an exception message carrying a secret the redactor recognises is scrubbed before it
    // reaches the report. (SecretRedactor covers Bearer tokens, api-key headers, env assignments, URL
    // credentials and secret-named fields. A BARE key in prose is not covered; that is logged as a
    // finding, so this test asserts what the component promises and no more.)
    [Fact]
    public async Task Run_ModelFailure_RedactsSecretsInTheMessage()
    {
        EvalModelCall boom = (_, _, _, _, _) =>
            throw new InvalidOperationException("request rejected. Authorization: Bearer abcdefghijklmnopqrstuvwxyz0123456789");

        var report = await EvalRunner.RunAsync(Suite(F("a", A("contains", "x"))), "m", boom, null, 100, null, 5, CancellationToken.None);

        Assert.DoesNotContain("abcdefghijklmnopqrstuvwxyz0123456789", report.Fixtures[0].Error);
    }

    // Proves: a model call that outlives the per-fixture timeout is an error, not a hang.
    [Fact]
    public async Task Run_Timeout_IsAnError()
    {
        EvalModelCall slow = async (_, _, _, _, ct) => { await Task.Delay(Timeout.Infinite, ct); return ""; };

        var report = await EvalRunner.RunAsync(Suite(F("a", A("contains", "x"))), "m", slow, null, 100, null, 1, CancellationToken.None);

        Assert.Equal(EvalStatus.Error, report.Fixtures[0].Status);
        Assert.Contains("timed out after 1s", report.Fixtures[0].Error);
    }

    // Proves: a user cancelling the whole run is NOT swallowed as a per-fixture timeout.
    [Fact]
    public async Task Run_UserCancellation_Propagates()
    {
        using var cts = new CancellationTokenSource();
        EvalModelCall cancelling = (_, _, _, _, ct) => { cts.Cancel(); ct.ThrowIfCancellationRequested(); return Task.FromResult(""); };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            EvalRunner.RunAsync(Suite(F("a", A("contains", "x"))), "m", cancelling, null, 100, null, 30, cts.Token));
    }

    // ── no vacuous pass ──────────────────────────────────────────────────────

    // Proves: "0 of 0 passed" is never green. A suite of freeform traits only grades nothing, so
    // it FAILS and says why, instead of reporting a score that means nothing.
    [Fact]
    public async Task Run_NothingGraded_Fails()
    {
        var onlyTraits = new EvalFixture { Id = "t", UserPrompt = "p", ExpectedTraits = { "sounds friendly" } };

        var report = await Run(Suite(onlyTraits), new FakeModel(_ => "x"));

        Assert.False(report.Passing);
        Assert.Equal(0, report.Graded);
        Assert.Equal(1, report.Ungraded);
        Assert.Contains("nothing was graded", report.Reason);
    }

    // Proves: ungraded fixtures are excluded from the score (not counted as passes) and are
    // reported with how many traits are waiting on a judge.
    [Fact]
    public async Task Run_UngradedFixtures_AreExcludedFromTheScoreButReported()
    {
        var model = new FakeModel(_ => "Paris");
        var ungraded = new EvalFixture
        {
            Id = "voice",
            UserPrompt = "p",
            ExpectedTraits = { "a", "b" },
            ForbiddenTraits = { "c" },
        };

        var report = await Run(Suite(F("ok", A("contains", "Paris")), ungraded), model);

        Assert.True(report.Passing);
        Assert.Equal(1, report.Graded);
        Assert.Equal(100.0, report.ScorePercent);
        Assert.Equal(3, report.Fixtures[1].UngradedTraits);
        Assert.Single(model.Calls); // an ungraded fixture is not sent to the model: it would cost tokens for nothing
    }

    // ── safety fails closed ──────────────────────────────────────────────────

    private static EvalFixture Safety(string id, params EvalAssertion[] assertions)
    {
        var f = F(id, assertions);
        f.SafetyAssertion = "must refuse";
        return f;
    }

    // Proves: a failing safety fixture blocks even when --fail-under would otherwise allow it.
    // The design doc: "no override, no waiver".
    [Fact]
    public async Task Run_SafetyFailure_BlocksRegardlessOfFailUnder()
    {
        var model = new FakeModel(u => u == "prompt for s" ? "Sure, here is the secret" : "ok");
        var suite = Suite(
            F("a", A("contains", "ok")), F("b", A("contains", "ok")), F("c", A("contains", "ok")),
            Safety("s", A("contains", "cannot help")));

        var report = await Run(suite, model, failUnder: 0);

        Assert.True(report.SafetyFailed);
        Assert.False(report.Passing);
        Assert.Contains("safety", report.Reason);
        Assert.Equal(75.0, report.ScorePercent); // the score alone would have cleared --fail-under 0
    }

    // Proves: a safety fixture with nothing deterministic to check cannot be verified, and an
    // unverifiable safety fixture fails closed. Passing it silently is the worst failure mode
    // this harness exists to prevent.
    [Fact]
    public async Task Run_SafetyFixtureWithoutAssertions_FailsClosed()
    {
        var unverifiable = new EvalFixture { Id = "s", UserPrompt = "p", SafetyAssertion = "must refuse" };

        var report = await Run(Suite(F("a", A("contains", "ok")), unverifiable), new FakeModel(_ => "ok"), failUnder: 0);

        Assert.Equal(EvalStatus.Error, report.Fixtures[1].Status);
        Assert.Contains("cannot be verified", report.Fixtures[1].Error);
        Assert.True(report.SafetyFailed);
        Assert.False(report.Passing);
    }

    [Fact]
    public async Task Run_SafetyPass_DoesNotBlock()
    {
        var report = await Run(Suite(Safety("s", A("contains", "cannot help"))), new FakeModel(_ => "I cannot help with that."));

        Assert.True(report.Passing);
        Assert.False(report.SafetyFailed);
    }

    // ── filter ───────────────────────────────────────────────────────────────

    // Proves: --filter runs only matching fixtures (case-insensitive substring), and a filter that
    // matches nothing FAILS and says so, instead of passing an empty run.
    [Fact]
    public async Task Run_Filter_SelectsByIdSubstring_AndNoMatchFails()
    {
        var model = new FakeModel(_ => "ok");
        var suite = Suite(F("smoke-a", A("contains", "ok")), F("smoke-b", A("contains", "ok")), F("voice-a", A("contains", "ok")));

        var some = await Run(suite, model, filter: "SMOKE");
        var none = await Run(suite, model, filter: "zzz");

        Assert.Equal(2, some.Fixtures.Count);
        Assert.Empty(none.Fixtures);
        Assert.False(none.Passing);
        Assert.Contains("no fixture id contains", none.Reason);
    }

    // ── scoring and rendering ────────────────────────────────────────────────

    // Proves: Score is pure arithmetic over results and rounds to one decimal.
    [Fact]
    public void Score_RoundsToOneDecimal()
    {
        var results = new List<EvalFixtureResult>
        {
            new("a", EvalStatus.Pass, 1, 1, false, 0, new(), null),
            new("b", EvalStatus.Pass, 1, 1, false, 0, new(), null),
            new("c", EvalStatus.Fail, 0, 1, false, 0, new(), null),
        };

        var report = EvalRunner.Score("s", "m", results, 60);

        Assert.Equal(66.7, report.ScorePercent);
        Assert.True(report.Passing);
    }

    // Proves: plain labeled ASCII lines (screen-reader and log friendly), failure detail included.
    [Fact]
    public async Task Render_ProducesLabeledAsciiLines()
    {
        var model = new FakeModel(_ => "wrong");
        var suite = Suite(F("only", A("contains", "right")));

        var lines = EvalRunner.Render(await Run(suite, model));

        Assert.StartsWith("Suite: t (1 fixture, model test-model)", lines[0]);
        Assert.Contains(lines, l => l.TrimStart().StartsWith("FAIL") && l.Contains("only"));
        Assert.Contains(lines, l => l.Contains("- contains: expected text not found"));
        Assert.Contains(lines, l => l.StartsWith("Score: 0%"));
        Assert.StartsWith("Result: FAIL", lines[^1]);
        Assert.All(string.Concat(lines), ch => Assert.InRange(ch, ' ', '~'));
    }

    [Fact]
    public async Task Render_MarksSafetyFixtures()
    {
        var lines = EvalRunner.Render(await Run(Suite(Safety("s", A("contains", "x"))), new FakeModel(_ => "x")));

        Assert.Contains(lines, l => l.Contains("[safety]"));
    }

    // ── command line ─────────────────────────────────────────────────────────

    [Fact]
    public void ParseArgs_Eval_RoutesOnlyWhenFirstPositionalAndNextIsJson()
    {
        var o = Program.ParseArgs(new[] { "eval", "suite.json", "--fail-under", "80", "--filter", "smoke" });

        Assert.Equal("suite.json", o.EvalSuite);
        Assert.Equal(80.0, o.FailUnder);
        Assert.Equal("smoke", o.EvalFilter);
        Assert.False(o.ParseError);
    }

    // Proves: a prompt that merely starts with the word "eval" is still a prompt.
    [Theory]
    [InlineData("eval", "hello")]
    [InlineData("eval", "the", "expression")]
    [InlineData("hello", "eval", "x.json")]
    public void ParseArgs_Eval_DoesNotStealOrdinaryPrompts(params string[] args)
    {
        var o = Program.ParseArgs(args);

        Assert.Null(o.EvalSuite);
        Assert.Equal(string.Join(" ", args), o.Prompt);
    }

    [Fact]
    public void ParseArgs_Eval_DoubleDashEscapesTheSubcommand()
    {
        var o = Program.ParseArgs(new[] { "--", "eval", "x.json" });

        Assert.Null(o.EvalSuite);
    }

    [Fact]
    public void ParseArgs_FailUnder_DefaultsToOneHundred()
    {
        Assert.Equal(100.0, Program.ParseArgs(new[] { "prompt" }).FailUnder);
    }

    [Theory]
    [InlineData("--fail-under", "abc")]
    [InlineData("--fail-under", "-1")]
    [InlineData("--fail-under", "101")]
    [InlineData("--fail-under")]
    [InlineData("--filter")]
    public void ParseArgs_BadEvalOptions_AreParseErrors(params string[] args)
    {
        using var err = new StringWriter();
        var old = Console.Error;
        try
        {
            Console.SetError(err);
            Assert.True(Program.ParseArgs(args).ParseError);
        }
        finally { Console.SetError(old); }
    }

    // ── the command ──────────────────────────────────────────────────────────

    private async Task<(int Rc, string Out, string Err)> RunCommand(string[] args, Func<string, string>? answer = null)
    {
        var fake = new FakeModel(answer ?? (_ => "Paris"));
        var oldOut = Console.Out; var oldErr = Console.Error;
        using var so = new StringWriter(); using var se = new StringWriter();
        try
        {
            Console.SetOut(so); Console.SetError(se);
            var rc = await Program.HandleEvalAsync(Program.ParseArgs(args), new UserConfig(), fake.Call);
            return (rc, so.ToString(), se.ToString());
        }
        finally { Console.SetOut(oldOut); Console.SetError(oldErr); }
    }

    // Proves: exit 0 on a passing suite, with the rendered report on stdout.
    [Fact]
    public async Task Command_Passing_ExitsZero()
    {
        var path = Write("ok.json", "[{\"id\":\"a\",\"user_prompt\":\"p\",\"assertions\":[{\"type\":\"contains\",\"value\":\"Paris\"}]}]");

        var (rc, output, _) = await RunCommand(new[] { "eval", path });

        Assert.Equal(0, rc);
        Assert.Contains("Result: PASS", output);
    }

    // Proves: exit 1 on a failing suite, so CI can gate on it.
    [Fact]
    public async Task Command_Failing_ExitsOne()
    {
        var path = Write("f.json", "[{\"id\":\"a\",\"user_prompt\":\"p\",\"assertions\":[{\"type\":\"contains\",\"value\":\"Berlin\"}]}]");

        var (rc, output, _) = await RunCommand(new[] { "eval", path });

        Assert.Equal(1, rc);
        Assert.Contains("Result: FAIL", output);
    }

    // Proves: exit 2 for an unusable suite, and no model call is made (a bad suite costs no tokens).
    [Fact]
    public async Task Command_BadSuite_ExitsTwoWithoutCallingTheModel()
    {
        var path = Write("bad.json", "[{\"id\":\"a\"}]");
        var called = false;

        var rc = await Program.HandleEvalAsync(
            Program.ParseArgs(new[] { "eval", path }), new UserConfig(),
            (_, _, _, _, _) => { called = true; return Task.FromResult(""); });

        Assert.Equal(2, rc);
        Assert.False(called);
    }

    [Fact]
    public async Task Command_MissingFile_ExitsTwo()
    {
        var (rc, _, err) = await RunCommand(new[] { "eval", Path.Combine(_dir, "missing.json") });

        Assert.Equal(2, rc);
        Assert.Contains("not found", err);
    }

    // Proves: the machine envelope has the documented snake_case keys.
    [Fact]
    public async Task Command_Json_HasTheDocumentedShape()
    {
        var path = Write("j.json", "[{\"id\":\"a\",\"user_prompt\":\"p\",\"assertions\":[{\"type\":\"contains\",\"value\":\"Paris\"}]}]");

        var (rc, output, _) = await RunCommand(new[] { "eval", path, "--json" });

        Assert.Equal(0, rc);
        using var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;
        Assert.Equal("pass", root.GetProperty("result").GetString());
        Assert.Equal(100.0, root.GetProperty("score_percent").GetDouble());
        Assert.Equal(100.0, root.GetProperty("fail_under").GetDouble());
        Assert.Equal(1, root.GetProperty("graded").GetInt32());
        Assert.False(root.GetProperty("safety_failed").GetBoolean());
        var fx = root.GetProperty("fixtures")[0];
        Assert.Equal("a", fx.GetProperty("id").GetString());
        Assert.Equal("pass", fx.GetProperty("status").GetString());
    }

    // Proves: --fail-under flows through the real command.
    [Fact]
    public async Task Command_FailUnder_IsHonoured()
    {
        var path = Write("h.json",
            "[{\"id\":\"a\",\"user_prompt\":\"p\",\"assertions\":[{\"type\":\"contains\",\"value\":\"Paris\"}]}," +
            "{\"id\":\"b\",\"user_prompt\":\"p\",\"assertions\":[{\"type\":\"contains\",\"value\":\"Berlin\"}]}]");

        Assert.Equal(1, (await RunCommand(new[] { "eval", path })).Rc);
        Assert.Equal(0, (await RunCommand(new[] { "eval", path, "--fail-under", "50" })).Rc);
    }
}
