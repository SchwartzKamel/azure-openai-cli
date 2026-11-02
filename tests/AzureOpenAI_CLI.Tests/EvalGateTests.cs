using System.Text.Json;
using AzureOpenAI_CLI.Eval;
using Xunit;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// S04E12 -- The Gate. Locks --repeat (flakiness), baselines (the "golden"), and the gate policy,
/// script and workflow. Each test states what it proves so a later cleanup does not mistake it for
/// an orphan.
/// </summary>
[Collection("ConsoleCapture")]
public sealed class EvalGateTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "az-gate-" + Guid.NewGuid().ToString("N"));

    public EvalGateTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { /* best effort */ } }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static EvalAssertion Contains(string v) => new() { Type = "contains", Value = v };

    private static EvalFixture F(string id, bool safety = false) => new()
    {
        Id = id,
        UserPrompt = "prompt for " + id,
        SafetyAssertion = safety ? "must refuse" : null,
        Assertions = { Contains("ok") },
    };

    private static EvalSuite Suite(params EvalFixture[] f) => new() { Name = "t", Fixtures = f.ToList() };

    /// <summary>Model that answers from a per-call function of (fixture prompt, call index for that prompt).</summary>
    private sealed class Model
    {
        public readonly List<string> Calls = new();
        private readonly Dictionary<string, int> _n = new();
        private readonly Func<string, int, string> _answer;
        public Model(Func<string, int, string> answer) => _answer = answer;

        public Task<string> Call(string system, string user, float temp, int? max, CancellationToken ct)
        {
            Calls.Add(user);
            _n[user] = _n.GetValueOrDefault(user) + 1;
            return Task.FromResult(_answer(user, _n[user]));
        }
    }

    private static Task<EvalReport> Run(EvalSuite s, Model m, int repeat = 1, EvalBaselineFile? baseline = null,
        double failUnder = 100, string? filter = null)
        => EvalRunner.RunAsync(s, "m", m.Call, null, failUnder, filter, 5, CancellationToken.None, null, repeat, baseline);

    private static EvalFixtureResult R(string id, string status, int runs = 1, int passedRuns = 0, bool safety = false)
        => new(id, status, 0, 1, safety, 0, new(), null, runs, passedRuns);

    private static EvalBaselineFile Baseline(string model, params (string Id, string Status)[] f) => new()
    {
        Suite = "t",
        Model = model,
        Repeat = 1,
        Fixtures = f.ToDictionary(x => x.Id, x => new EvalBaselineEntry { Status = x.Status, Runs = 1, PassedRuns = x.Status == "pass" ? 1 : 0 }),
    };

    private string Write(string name, string text)
    {
        var p = Path.Combine(_dir, name);
        File.WriteAllText(p, text);
        return p;
    }

    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var c = Path.Combine(dir.FullName, relative);
            if (File.Exists(c)) return c;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("not found walking up from test output: " + relative);
    }

    // ── --repeat ─────────────────────────────────────────────────────────────

    // Proves: --repeat n calls the model n times per fixture, and a fixture with no assertions is
    // never called at all (it would cost tokens for nothing).
    [Fact]
    public async Task Repeat_CallsTheModelNTimesPerGradedFixture()
    {
        var model = new Model((_, _) => "ok");
        var ungraded = new EvalFixture { Id = "u", UserPrompt = "p", ExpectedTraits = { "x" } };

        var report = await Run(Suite(F("a"), F("b"), ungraded), model, repeat: 3);

        Assert.Equal(6, model.Calls.Count);
        Assert.Equal(3, report.Repeat);
        Assert.Equal(0, report.Fixtures.Single(f => f.Id == "u").Runs);
    }

    // Proves: a fixture passes only if EVERY run passes. Passing two runs in three is not passing,
    // and the pass count is kept so the flakiness is visible instead of averaged away.
    [Fact]
    public async Task Repeat_FlakyFixture_FailsAndIsReportedAsFlaky()
    {
        var model = new Model((_, n) => n == 2 ? "wrong" : "ok");   // runs 1 and 3 pass, run 2 fails

        var report = await Run(Suite(F("a")), model, repeat: 3);
        var f = report.Fixtures.Single();

        Assert.Equal(EvalStatus.Fail, f.Status);
        Assert.Equal(2, f.PassedRuns);
        Assert.Equal(3, f.Runs);
        Assert.True(f.Flaky);
        Assert.Equal(1, report.FlakyCount);
        Assert.False(report.Passing);
    }

    // Proves: consistently failing is NOT flaky. Flaky means some runs passed, which is the signal
    // that a result is unstable; a stable failure is a plain failure.
    [Fact]
    public async Task Repeat_ConsistentFailure_IsNotFlaky()
    {
        var report = await Run(Suite(F("a")), new Model((_, _) => "wrong"), repeat: 3);

        Assert.Equal(EvalStatus.Fail, report.Fixtures[0].Status);
        Assert.False(report.Fixtures[0].Flaky);
        Assert.Equal(0, report.FlakyCount);
    }

    [Fact]
    public async Task Repeat_AllPass_IsPassAndNotFlaky()
    {
        var report = await Run(Suite(F("a")), new Model((_, _) => "ok"), repeat: 3);

        Assert.Equal(EvalStatus.Pass, report.Fixtures[0].Status);
        Assert.Equal(3, report.Fixtures[0].PassedRuns);
        Assert.False(report.Fixtures[0].Flaky);
    }

    // Proves: an error in one run plus passes in the rest is a failure and flaky, not an "error".
    // Only a fixture whose EVERY run errored is an error.
    [Fact]
    public async Task Repeat_MixedErrorAndPass_IsFailAndFlaky_AllErrors_IsError()
    {
        var calls = 0;
        EvalModelCall errFirst = (_, _, _, _, _) =>
            ++calls == 1 ? throw new InvalidOperationException("boom") : Task.FromResult("ok");
        var mixed = await EvalRunner.RunAsync(Suite(F("a")), "m", errFirst, null, 100, null, 5, CancellationToken.None, null, 2, null);

        EvalModelCall alwaysErr = (_, _, _, _, _) => throw new InvalidOperationException("boom");
        var allErr = await EvalRunner.RunAsync(Suite(F("a")), "m", alwaysErr, null, 100, null, 5, CancellationToken.None, null, 2, null);

        Assert.Equal(EvalStatus.Fail, mixed.Fixtures[0].Status);
        Assert.True(mixed.Fixtures[0].Flaky);
        Assert.Equal(EvalStatus.Error, allErr.Fixtures[0].Status);
    }

    // Proves: a safety fixture that passes only SOME runs blocks as a safety failure. "Refuses most
    // of the time" is not a safety property.
    [Fact]
    public async Task Repeat_SafetyFixtureThatFlakes_BlocksAsSafetyFailure()
    {
        var model = new Model((_, n) => n == 3 ? "wrong" : "ok");

        var report = await Run(Suite(F("s", safety: true)), model, repeat: 3, failUnder: 0);

        Assert.True(report.SafetyFailed);
        Assert.False(report.Passing);
    }

    // Proves: a nonsense repeat count cannot run a fixture zero times and score "nothing" as a pass.
    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Repeat_BelowOne_IsTreatedAsOne(int repeat)
    {
        var model = new Model((_, _) => "ok");

        var report = await Run(Suite(F("a")), model, repeat: repeat);

        Assert.Single(model.Calls);
        Assert.Equal(1, report.Repeat);
    }

    // Proves: the text report says how many runs happened and which fixtures are flaky.
    [Fact]
    public async Task Render_ShowsRepeatAndFlakyMarkers()
    {
        var model = new Model((_, n) => n == 1 ? "ok" : "wrong");
        var report = await Run(Suite(F("a")), model, repeat: 2);

        var lines = EvalRunner.Render(report);

        Assert.Contains(lines, l => l.StartsWith("Repeat: 2 runs per fixture"));
        Assert.Contains(lines, l => l.Contains("[FLAKY: 1 of 2 runs passed]"));
        Assert.Contains(lines, l => l.StartsWith("Flaky: 1 fixture"));
    }

    // ── baselines: create / compare ──────────────────────────────────────────

    // Proves: a baseline records OUTCOMES ONLY. It never contains model text, so it cannot leak output
    // into the repository, and it stays small. Ungraded fixtures are not recorded.
    [Fact]
    public async Task Baseline_Create_RecordsOutcomesOnly()
    {
        var secretOutput = "SUPER-SECRET-MODEL-TEXT ok";
        var ungraded = new EvalFixture { Id = "u", UserPrompt = "p", ExpectedTraits = { "x" } };
        var report = await Run(Suite(F("a"), ungraded), new Model((_, _) => secretOutput), repeat: 2);

        var baseline = EvalBaselines.Create(report);
        var json = JsonSerializer.Serialize(baseline, AppJsonContext.Default.EvalBaselineFile);

        Assert.DoesNotContain("SUPER-SECRET", json);
        Assert.Equal(new[] { "a" }, baseline.Fixtures.Keys.ToArray());
        Assert.Equal(("pass", 2, 2), (baseline.Fixtures["a"].Status, baseline.Fixtures["a"].Runs, baseline.Fixtures["a"].PassedRuns));
    }

    // Proves: pass to fail and pass to error are regressions; fail to pass is an improvement; a fixture
    // that failed before and still fails is NOT a regression (the baseline was already red there).
    [Fact]
    public void Compare_ClassifiesEveryTransition()
    {
        var baseline = Baseline("m", ("a", "pass"), ("b", "pass"), ("c", "fail"), ("d", "fail"));
        var results = new List<EvalFixtureResult>
        {
            R("a", EvalStatus.Fail), R("b", EvalStatus.Error), R("c", EvalStatus.Pass), R("d", EvalStatus.Fail), R("e", EvalStatus.Pass),
        };

        var c = EvalBaselines.Compare(results, baseline, "m", ignoreMissing: false);

        Assert.Equal(new[] { "a", "b" }, c.Regressions);
        Assert.Equal(new[] { "c" }, c.Improvements);
        Assert.Equal(new[] { "e" }, c.Added);
        Assert.Empty(c.Removed);
        Assert.False(c.ModelMismatch);
    }

    // Proves: a fixture that vanished from the suite is listed, so deleting a failing fixture cannot hide
    // a regression; --filter (a deliberately narrowed run) suppresses that list.
    [Fact]
    public void Compare_RemovedFixtures_AreListed_UnlessTheRunWasFiltered()
    {
        var baseline = Baseline("m", ("a", "pass"), ("gone", "pass"));
        var results = new List<EvalFixtureResult> { R("a", EvalStatus.Pass) };

        Assert.Equal(new[] { "gone" }, EvalBaselines.Compare(results, baseline, "m", false).Removed);
        Assert.Empty(EvalBaselines.Compare(results, baseline, "m", true).Removed);
    }

    // Proves: comparing against a baseline from a different model is allowed but visible.
    [Fact]
    public void Compare_DifferentModel_IsFlaggedNotFailed()
    {
        var c = EvalBaselines.Compare(new List<EvalFixtureResult> { R("a", EvalStatus.Pass) },
            Baseline("gpt-4o", ("a", "pass")), "gpt-4o-mini", false);

        Assert.True(c.ModelMismatch);
        Assert.Equal("gpt-4o", c.BaselineModel);
        Assert.Empty(c.Regressions);
    }

    // Proves: a baseline fixture that passed every run and now passes only some is a REGRESSION: the
    // stability it had is gone.
    [Fact]
    public async Task Compare_PassThenFlaky_IsARegression()
    {
        var baseline = Baseline("m", ("a", "pass"));
        var model = new Model((_, n) => n == 2 ? "wrong" : "ok");

        var report = await Run(Suite(F("a")), model, repeat: 3, baseline: baseline);

        Assert.Contains("a", report.Baseline!.Regressions);
        Assert.False(report.Passing);
    }

    // ── baselines: the gate ──────────────────────────────────────────────────

    // Proves: a regression fails the run even when the score is above --fail-under. A threshold alone lets
    // one fixture fail quietly while others improve.
    [Fact]
    public async Task Gate_Regression_FailsEvenWhenTheScoreClearsFailUnder()
    {
        var baseline = Baseline("m", ("a", "pass"), ("b", "pass"), ("c", "pass"), ("d", "fail"));
        var model = new Model((p, _) => p.Contains("prompt for a") ? "wrong" : "ok");   // a regresses; d improves

        var report = await Run(Suite(F("a"), F("b"), F("c"), F("d")), model, baseline: baseline, failUnder: 50);

        Assert.True(report.ScorePercent >= 50);
        Assert.False(report.Passing);
        Assert.Contains("regression against baseline: a", report.Reason);
        Assert.Contains("d", report.Baseline!.Improvements);
    }

    // Proves: a clean comparison does not fail, and improvements are fine.
    [Fact]
    public async Task Gate_NoRegression_Passes()
    {
        var baseline = Baseline("m", ("a", "pass"), ("b", "fail"));

        var report = await Run(Suite(F("a"), F("b")), new Model((_, _) => "ok"), baseline: baseline);

        Assert.True(report.Passing);
        Assert.Empty(report.Baseline!.Regressions);
        Assert.Contains("b", report.Baseline.Improvements);
    }

    // Proves: a safety failure is reported as such even when there is also a regression, because the
    // safety reason is the one that cannot be waived.
    [Fact]
    public async Task Gate_SafetyFailure_TakesPrecedenceOverRegression()
    {
        var baseline = Baseline("m", ("s", "pass"));

        var report = await Run(Suite(F("s", safety: true)), new Model((_, _) => "wrong"), baseline: baseline, failUnder: 0);

        Assert.True(report.SafetyFailed);
        Assert.Contains("safety", report.Reason);
    }

    // Proves: a long regression list is summarised so the reason stays one readable line.
    [Fact]
    public void Gate_ManyRegressions_AreSummarised()
    {
        var ids = Enumerable.Range(0, 8).Select(i => "f" + i).ToArray();
        var baseline = new EvalBaselineFile { Model = "m", Fixtures = ids.ToDictionary(i => i, _ => new EvalBaselineEntry { Status = "pass", Runs = 1, PassedRuns = 1 }) };

        var report = EvalRunner.Score("t", "m", ids.Select(i => R(i, EvalStatus.Fail)).ToList(), 0, baseline: baseline);

        Assert.Contains("and 3 more", report.Reason);
    }

    // ── baselines: files ─────────────────────────────────────────────────────

    // Proves: a baseline survives a write and a load unchanged, and comparing a run to its own baseline
    // finds nothing.
    [Fact]
    public async Task Baseline_RoundTrip_ComparesCleanAgainstItself()
    {
        var report = await Run(Suite(F("a"), F("b")), new Model((_, _) => "ok"), repeat: 2);
        var path = Path.Combine(_dir, "nested", "dir", "b.json");

        EvalBaselines.Write(path, EvalBaselines.Create(report));
        Assert.True(EvalBaselines.TryLoad(path, out var loaded, out var error), error);
        var again = await Run(Suite(F("a"), F("b")), new Model((_, _) => "ok"), repeat: 2, baseline: loaded);

        Assert.Equal("m", loaded!.Model);
        Assert.Equal(2, loaded.Repeat);
        Assert.Empty(again.Baseline!.Regressions);
        Assert.True(again.Passing);
    }

    // Proves: writes are atomic. No temp file is left behind, and overwriting works.
    [Fact]
    public async Task Baseline_Write_LeavesNoTempFilesAndOverwrites()
    {
        var report = await Run(Suite(F("a")), new Model((_, _) => "ok"));
        var path = Path.Combine(_dir, "b.json");

        EvalBaselines.Write(path, EvalBaselines.Create(report));
        EvalBaselines.Write(path, EvalBaselines.Create(report));

        Assert.Equal(new[] { "b.json" }, Directory.GetFiles(_dir).Select(Path.GetFileName).ToArray());
    }

    // Proves: a missing, broken, empty, oversized or nonsensical baseline is REFUSED with a reason. A bad
    // baseline must never degrade into "no baseline, so nothing can regress".
    [Theory]
    [InlineData("missing", null, "not found")]
    [InlineData("bad.json", "{not json", "not valid JSON")]
    [InlineData("empty.json", "{\"model\":\"m\",\"fixtures\":{}}", "no fixtures")]
    [InlineData("status.json", "{\"model\":\"m\",\"fixtures\":{\"a\":{\"status\":\"maybe\"}}}", "unknown status")]
    public void Baseline_TryLoad_RefusesBadFiles(string name, string? body, string expected)
    {
        var path = body is null ? Path.Combine(_dir, name) : Write(name, body);

        Assert.False(EvalBaselines.TryLoad(path, out var b, out var error));
        Assert.Null(b);
        Assert.Contains(expected, error);
    }

    [Fact]
    public void Baseline_TryLoad_RefusesOversizedFiles()
    {
        var path = Write("big.json", new string(' ', (int)EvalBaselines.MaxBaselineBytes + 1));

        Assert.False(EvalBaselines.TryLoad(path, out _, out var error));
        Assert.Contains("limit", error);
    }

    // ── command line ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("1", 1)]
    [InlineData("3", 3)]
    [InlineData("10", 10)]
    public void ParseArgs_Repeat_AcceptsOneToTen(string raw, int expected)
    {
        var o = Program.ParseArgs(new[] { "eval", "--corpus", "smoke", "--repeat", raw });

        Assert.Equal(expected, o.Repeat);
        Assert.False(o.ParseError);
    }

    [Theory]
    [InlineData("--repeat", "0")]
    [InlineData("--repeat", "11")]
    [InlineData("--repeat", "abc")]
    [InlineData("--repeat")]
    [InlineData("--baseline")]
    [InlineData("--write-baseline")]
    public void ParseArgs_BadGateOptions_AreParseErrors(params string[] args)
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

    [Fact]
    public void ParseArgs_Defaults_AreOneRunAndNoBaseline()
    {
        var o = Program.ParseArgs(new[] { "prompt" });

        Assert.Equal(1, o.Repeat);
        Assert.Null(o.Baseline);
        Assert.Null(o.WriteBaseline);
    }

    private const string SuiteJson =
        "[{\"id\":\"a\",\"user_prompt\":\"pa\",\"assertions\":[{\"type\":\"contains\",\"value\":\"ok\"}]}," +
        "{\"id\":\"b\",\"user_prompt\":\"pb\",\"assertions\":[{\"type\":\"contains\",\"value\":\"ok\"}]}]";

    private async Task<(int Rc, string Out)> Command(string[] args, EvalModelCall? model = null)
    {
        var oldOut = Console.Out; var oldErr = Console.Error;
        using var so = new StringWriter(); using var se = new StringWriter();
        try
        {
            Console.SetOut(so); Console.SetError(se);
            var rc = await Program.HandleEvalAsync(Program.ParseArgs(args), new UserConfig(),
                model ?? ((_, _, _, _, _) => Task.FromResult("ok")));
            return (rc, so.ToString() + se.ToString());
        }
        finally { Console.SetOut(oldOut); Console.SetError(oldErr); }
    }

    // Proves: --write-baseline records the run, and the file then gates the next one.
    [Fact]
    public async Task Command_WriteBaseline_ThenBaseline_GatesTheNextRun()
    {
        var suite = Write("s.json", SuiteJson);
        var bl = Path.Combine(_dir, "out", "bl.json");

        var first = await Command(new[] { "eval", suite, "--write-baseline", bl });
        Assert.Equal(0, first.Rc);
        Assert.True(File.Exists(bl));

        var good = await Command(new[] { "eval", suite, "--baseline", bl });
        Assert.Equal(0, good.Rc);
        Assert.Contains("Baseline: 0 regression(s)", good.Out);

        var bad = await Command(new[] { "eval", suite, "--baseline", bl }, (_, user, _, _, _) => Task.FromResult(user == "pa" ? "wrong" : "ok"));
        Assert.Equal(1, bad.Rc);
        Assert.Contains("regressed: a", bad.Out);
        Assert.Contains("regression against baseline: a", bad.Out);
    }

    // Proves: an unusable baseline exits 2 BEFORE any model call, so a bad path costs no tokens and
    // cannot masquerade as "no baseline".
    [Fact]
    public async Task Command_BadBaseline_ExitsTwoWithoutCallingTheModel()
    {
        var suite = Write("s.json", SuiteJson);
        var called = false;

        var (rc, output) = await Command(new[] { "eval", suite, "--baseline", Path.Combine(_dir, "nope.json") },
            (_, _, _, _, _) => { called = true; return Task.FromResult("ok"); });

        Assert.Equal(2, rc);
        Assert.False(called);
        Assert.Contains("baseline file not found", output);
    }

    // Proves: a baseline written from a failing run says so, because it records the failures.
    [Fact]
    public async Task Command_WriteBaseline_FromAFailingRun_Warns()
    {
        var suite = Write("s.json", SuiteJson);
        var bl = Path.Combine(_dir, "bl.json");

        var (rc, output) = await Command(new[] { "eval", suite, "--write-baseline", bl }, (_, _, _, _, _) => Task.FromResult("wrong"));

        Assert.Equal(1, rc);
        Assert.Contains("baseline written from a run that did not pass", output);
        Assert.True(File.Exists(bl));
    }

    [Fact]
    public async Task Command_WriteBaseline_ToAnUnwritablePath_ExitsTwo()
    {
        var suite = Write("s.json", SuiteJson);

        // A directory in the way of the file: cannot be replaced by a file.
        var blocked = Path.Combine(_dir, "blocked.json");
        Directory.CreateDirectory(blocked);

        var (rc, output) = await Command(new[] { "eval", suite, "--write-baseline", blocked });

        Assert.Equal(2, rc);
        Assert.Contains("cannot write baseline", output);
    }

    // Proves: --repeat and the baseline appear in the JSON envelope with the documented keys.
    [Fact]
    public async Task Command_Json_IncludesRepeatFlakyAndBaseline()
    {
        var suite = Write("s.json", SuiteJson);
        var bl = Path.Combine(_dir, "bl.json");
        await Command(new[] { "eval", suite, "--write-baseline", bl });

        var seen = new Dictionary<string, int>();
        var (_, output) = await Command(new[] { "eval", suite, "--repeat", "2", "--baseline", bl, "--json" },
            (_, user, _, _, _) => { seen[user] = seen.GetValueOrDefault(user) + 1; return Task.FromResult(user == "pa" && seen[user] == 2 ? "wrong" : "ok"); });

        using var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;
        Assert.Equal(2, root.GetProperty("repeat").GetInt32());
        Assert.Equal(1, root.GetProperty("flaky").GetInt32());
        Assert.Equal(1, root.GetProperty("baseline").GetProperty("regressions").GetArrayLength());
        var a = root.GetProperty("fixtures").EnumerateArray().Single(f => f.GetProperty("id").GetString() == "a");
        Assert.True(a.GetProperty("flaky").GetBoolean());
        Assert.Equal(1, a.GetProperty("passed_runs").GetInt32());
        Assert.Equal(2, a.GetProperty("runs").GetInt32());
    }
}

/// <summary>
/// The gate's policy, script and workflow are files that decide whether a regression can merge, so
/// they are pinned by tests like code. Weakening the safety floor, or handing secrets to fork code,
/// must fail the build.
/// </summary>
public sealed class EvalGatePolicyTests
{
    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var c = Path.Combine(dir.FullName, relative);
            if (File.Exists(c)) return c;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("not found walking up from test output: " + relative);
    }

    private static JsonElement Policy()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(RepoFile(".github/eval-gate.json")));
        return doc.RootElement.Clone();
    }

    // Proves: the policy lists every built-in corpus, with real thresholds and a sane repeat count. A corpus
    // left out of the policy would ship without a gate.
    [Fact]
    public void Policy_CoversEveryBuiltInCorpusWithValidThresholds()
    {
        var p = Policy();
        var listed = p.GetProperty("corpora").EnumerateArray().Select(c => c.GetProperty("name").GetString()!).ToArray();

        Assert.Equal(Corpora.Names.OrderBy(n => n), listed.OrderBy(n => n));
        Assert.InRange(p.GetProperty("repeat").GetInt32(), 1, 10);
        Assert.All(p.GetProperty("corpora").EnumerateArray(),
            c => Assert.InRange(c.GetProperty("fail_under").GetDouble(), 0, 100));
    }

    // Proves: the SAFETY FLOOR. The refusal corpus must stay at 100 and smoke must stay high. Lowering these
    // is a deliberate, reviewed change to this test, never a quiet edit to a JSON file.
    [Fact]
    public void Policy_PinsTheSafetyFloor()
    {
        double Threshold(string name) => Policy().GetProperty("corpora").EnumerateArray()
            .Single(c => c.GetProperty("name").GetString() == name).GetProperty("fail_under").GetDouble();

        Assert.Equal(100, Threshold("refusal"));
        Assert.True(Threshold("smoke") >= 90);
        Assert.True(Threshold("persona-voice") >= 50);
    }

    // Proves: the workflow never uses pull_request_target. That trigger runs with secrets against code from
    // a fork, which would let a pull request exfiltrate the model credentials.
    [Fact]
    public void Workflow_NeverUsesPullRequestTarget_AndIsReadOnly()
    {
        var yml = File.ReadAllText(RepoFile(".github/workflows/eval-gate.yml"));

        Assert.DoesNotContain("pull_request_target", yml);
        Assert.Contains("permissions:\n  contents: read", yml);
        Assert.DoesNotContain("write-all", yml);
        Assert.DoesNotContain("contents: write", yml);
    }

    // Proves: the workflow runs the gate script and passes the secrets through env, not by echoing them into
    // a command line, and uploads reports even on failure.
    [Fact]
    public void Workflow_RunsTheScript_WithSecretsInEnv()
    {
        var yml = File.ReadAllText(RepoFile(".github/workflows/eval-gate.yml"));

        Assert.Contains("bash scripts/eval-gate.sh run", yml);
        Assert.Contains("AZUREOPENAIAPI: ${{ secrets.AZUREOPENAIAPI }}", yml);
        Assert.Contains("EVAL_GATE_REQUIRED: ${{ vars.EVAL_GATE_REQUIRED }}", yml);
        Assert.Contains("if: always()", yml);
        Assert.DoesNotContain("echo ${{ secrets", yml);
    }

    // Proves: every action is pinned to a commit SHA, as the rest of the repo's workflows are.
    [Fact]
    public void Workflow_PinsEveryActionToACommitSha()
    {
        var yml = File.ReadAllText(RepoFile(".github/workflows/eval-gate.yml"));

        foreach (var line in yml.Split('\n').Where(l => l.TrimStart().StartsWith("uses:") || l.TrimStart().StartsWith("- uses:")))
        {
            Assert.Matches(@"@[0-9a-f]{40}\b", line);
        }
    }

    // Proves: the script exists, fails closed when required, and is wired to the same policy path.
    [Fact]
    public void Script_FailsClosedAndReadsThePolicy()
    {
        var sh = File.ReadAllText(RepoFile("scripts/eval-gate.sh"));

        Assert.Contains(".github/eval-gate.json", sh);
        Assert.Contains("EVAL_GATE_REQUIRED", sh);
        Assert.Contains("Refusing to report green without running", sh);
        Assert.Contains("This is not a pass", sh);
    }
}
