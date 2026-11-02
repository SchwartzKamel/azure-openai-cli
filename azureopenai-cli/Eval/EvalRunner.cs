using System.Text;
using System.Text.Json.Serialization;
using AzureOpenAI_CLI.Cli;

namespace AzureOpenAI_CLI.Eval;

internal sealed record EvalAssertionFailure(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("message")] string Message);

internal sealed record EvalFixtureResult(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("passed")] int Passed,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("safety")] bool Safety,
    [property: JsonPropertyName("ungraded_traits")] int UngradedTraits,
    [property: JsonPropertyName("failures")] List<EvalAssertionFailure> Failures,
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("runs")] int Runs = 1,
    [property: JsonPropertyName("passed_runs")] int PassedRuns = 0)
{
    /// <summary>
    /// True when a fixture passed some runs but not all: the signal that a result is not stable.
    /// Only meaningful with <c>--repeat</c>; a single run cannot be flaky.
    /// </summary>
    [JsonPropertyName("flaky")]
    public bool Flaky => PassedRuns > 0 && PassedRuns < Runs;
}

internal sealed record EvalReport(
    [property: JsonPropertyName("suite")] string Suite,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("result")] string Result,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("score_percent")] double ScorePercent,
    [property: JsonPropertyName("fail_under")] double FailUnder,
    [property: JsonPropertyName("graded")] int Graded,
    [property: JsonPropertyName("passed")] int Passed,
    [property: JsonPropertyName("ungraded")] int Ungraded,
    [property: JsonPropertyName("safety_failed")] bool SafetyFailed,
    [property: JsonPropertyName("fixtures")] List<EvalFixtureResult> Fixtures,
    [property: JsonPropertyName("persona_source")] string? PersonaSource = null,
    [property: JsonPropertyName("repeat")] int Repeat = 1,
    [property: JsonPropertyName("flaky")] int FlakyCount = 0,
    [property: JsonPropertyName("baseline")] EvalBaselineComparison? Baseline = null)
{
    internal bool Passing => Result == EvalStatus.Pass;
}

/// <summary>String constants (no enum: it costs AOT JSON converters for no gain).</summary>
internal static class EvalStatus
{
    internal const string Pass = "pass";
    internal const string Fail = "fail";
    internal const string Error = "error";
    internal const string Ungraded = "ungraded";
}

/// <summary>Calls the model for one fixture. The seam that keeps the runner testable offline.</summary>
internal delegate Task<string> EvalModelCall(
    string systemPrompt, string userPrompt, float temperature, int? maxTokens, CancellationToken ct);

/// <summary>Resolves a persona's system prompt, or reports why it cannot.</summary>
internal delegate bool PersonaResolver(string name, out string? systemPrompt);

/// <summary>
/// Runs a suite and scores it.
///
/// Scoring policy, each line earned by a failure mode:
/// <list type="bullet">
///   <item><b>No vacuous pass.</b> A suite that grades nothing FAILS. "0 of 0 passed" must never
///     look like green; freeform traits are not gradable here, so a suite of only traits grades
///     nothing.</item>
///   <item><b>Errors count against the score.</b> A fixture that could not run (missing persona,
///     model error, timeout) is graded and failed, not skipped. Skipping errors would let an
///     outage score 100%.</item>
///   <item><b>Safety fails closed and ignores --fail-under.</b> A safety fixture that fails, errors,
///     or has no deterministic assertion to verify it blocks outright (the design doc: "no
///     override, no waiver").</item>
///   <item><b>Deterministic by default.</b> A fixture with no temperature runs at 0.0.</item>
/// </list>
/// </summary>
internal static class EvalRunner
{
    internal static async Task<EvalReport> RunAsync(
        EvalSuite suite,
        string modelLabel,
        EvalModelCall call,
        PersonaResolver? personas,
        double failUnder,
        string? filter,
        int timeoutSeconds,
        CancellationToken ct,
        Func<string?>? personaSource = null,
        int repeat = 1,
        EvalBaselineFile? baseline = null)
    {
        ArgumentNullException.ThrowIfNull(suite);
        ArgumentNullException.ThrowIfNull(call);

        var selected = suite.Fixtures
            .Where(f => string.IsNullOrWhiteSpace(filter)
                || f.Id.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var results = new List<EvalFixtureResult>();
        foreach (var fixture in selected)
        {
            ct.ThrowIfCancellationRequested();
            results.Add(await RunFixtureAsync(fixture, call, personas, timeoutSeconds, Math.Max(1, repeat), ct));
        }

        return Score(suite.Name, modelLabel, results, failUnder,
            noMatchReason: selected.Count == 0 ? $"no fixture id contains \"{Clean(filter)}\"" : null,
            personaSource: personaSource?.Invoke(),
            repeat: Math.Max(1, repeat),
            baseline: baseline,
            baselineIgnoresMissing: !string.IsNullOrWhiteSpace(filter));
    }

    private static async Task<EvalFixtureResult> RunFixtureAsync(
        EvalFixture f, EvalModelCall call, PersonaResolver? personas, int timeoutSeconds, int repeat, CancellationToken ct)
    {
        var traits = f.ExpectedTraits.Count + f.ForbiddenTraits.Count + (f.IsSafety ? 1 : 0);

        EvalFixtureResult Result(string status, int passed, int total, List<EvalAssertionFailure>? failures = null,
            string? error = null, int runs = 0, int passedRuns = 0)
            => new(f.Id, status, passed, total, f.IsSafety, traits, failures ?? new(), error, runs, passedRuns);

        // A safety fixture with nothing deterministic to check cannot be verified. Passing it
        // silently is the worst failure mode this harness exists to prevent.
        if (f.Assertions.Count == 0)
        {
            return f.IsSafety
                ? Result(EvalStatus.Error, 0, 0, error: "safety fixture has no deterministic assertion, so it cannot be verified")
                : Result(EvalStatus.Ungraded, 0, 0);
        }

        // Resolve the system prompt exactly as the CLI builds it for a real call.
        string system;
        if (string.IsNullOrWhiteSpace(f.Persona))
        {
            system = Program.DEFAULT_SYSTEM_PROMPT + "\n\n" + Program.SAFETY_CLAUSE;
        }
        else
        {
            if (personas is null || !personas(f.Persona!, out var personaPrompt) || personaPrompt is null)
            {
                return Result(EvalStatus.Error, 0, f.Assertions.Count,
                    error: $"persona '{Clean(f.Persona)}' not found (run --squad-init, or fix the fixture)");
            }
            system = personaPrompt + "\n\n" + Program.SAFETY_CLAUSE;
        }

        // Run the fixture `repeat` times. A fixture passes only if EVERY run passes: a model that
        // passes two runs in three is not a model that passes. The pass count is kept so flakiness
        // is visible rather than averaged away.
        var runs = new List<EvalFixtureResult>();
        for (var i = 0; i < repeat; i++)
        {
            ct.ThrowIfCancellationRequested();
            runs.Add(await RunOnceAsync(f, system, call, timeoutSeconds, Result, ct));
        }

        var passedRuns = runs.Count(r => r.Status == EvalStatus.Pass);
        if (passedRuns == runs.Count)
        {
            return Result(EvalStatus.Pass, f.Assertions.Count, f.Assertions.Count, null, null, runs.Count, passedRuns);
        }

        // Report the first run that did not pass, so the failure text is real, not a blend.
        var firstBad = runs.First(r => r.Status != EvalStatus.Pass);
        var allErrors = runs.All(r => r.Status == EvalStatus.Error);
        return Result(allErrors ? EvalStatus.Error : EvalStatus.Fail, firstBad.Passed, firstBad.Total,
            firstBad.Failures, firstBad.Error, runs.Count, passedRuns);
    }

    private static async Task<EvalFixtureResult> RunOnceAsync(
        EvalFixture f, string system, EvalModelCall call, int timeoutSeconds,
        Func<string, int, int, List<EvalAssertionFailure>?, string?, int, int, EvalFixtureResult> result,
        CancellationToken ct)
    {
        string output;
        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            if (timeoutSeconds > 0) cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            try
            {
                output = await call(system, f.UserPrompt, f.Temperature ?? 0f, f.MaxTokens, cts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return result(EvalStatus.Error, 0, f.Assertions.Count, null, $"timed out after {timeoutSeconds}s", 1, 0);
            }
            catch (OperationCanceledException)
            {
                throw; // the user cancelled the whole run
            }
            catch (Exception ex)
            {
                return result(EvalStatus.Error, 0, f.Assertions.Count, null,
                    "model call failed: " + Trunc(Clean(SecretRedactor.Redact(ex.Message)), 160), 1, 0);
            }
        }

        var failures = new List<EvalAssertionFailure>();
        foreach (var assertion in f.Assertions)
        {
            var message = EvalAssertions.Check(assertion, output);
            if (message is not null) failures.Add(new EvalAssertionFailure(assertion.Type, message));
        }

        var passed = f.Assertions.Count - failures.Count;
        return result(failures.Count == 0 ? EvalStatus.Pass : EvalStatus.Fail, passed, f.Assertions.Count, failures, null, 1,
            failures.Count == 0 ? 1 : 0);
    }

    /// <summary>Pure scoring over finished fixture results. Exposed for direct testing.</summary>
    internal static EvalReport Score(
        string suite, string model, List<EvalFixtureResult> results, double failUnder, string? noMatchReason = null,
        string? personaSource = null, int repeat = 1, EvalBaselineFile? baseline = null,
        bool baselineIgnoresMissing = false)
    {
        var graded = results.Count(r => r.Status != EvalStatus.Ungraded);
        var passed = results.Count(r => r.Status == EvalStatus.Pass);
        var ungraded = results.Count - graded;
        var score = graded == 0 ? 0.0 : Math.Round(passed * 100.0 / graded, 1);
        var safetyFailed = results.Any(r => r.Safety && r.Status is EvalStatus.Fail or EvalStatus.Error);
        var flaky = results.Count(r => r.Flaky);
        var comparison = baseline is null ? null : EvalBaselines.Compare(results, baseline, model, baselineIgnoresMissing);

        string result;
        string? reason;
        if (noMatchReason is not null)
        {
            (result, reason) = (EvalStatus.Fail, noMatchReason);
        }
        else if (safetyFailed)
        {
            (result, reason) = (EvalStatus.Fail, "a safety fixture failed or could not be verified (no waiver, --fail-under does not apply)");
        }
        else if (comparison is { Regressions.Count: > 0 })
        {
            // A regression against a recorded run is its own gate: a score can stay above
            // --fail-under while a fixture that used to pass now fails.
            (result, reason) = (EvalStatus.Fail,
                $"regression against baseline: {string.Join(", ", comparison.Regressions.Take(5))}"
                + (comparison.Regressions.Count > 5 ? $" and {comparison.Regressions.Count - 5} more" : ""));
        }
        else if (graded == 0)
        {
            (result, reason) = (EvalStatus.Fail,
                "nothing was graded: no fixture has assertions (freeform traits need a judge, which is not implemented)");
        }
        else if (score < failUnder)
        {
            (result, reason) = (EvalStatus.Fail, $"score {score:0.#}% is below --fail-under {failUnder:0.#}%");
        }
        else
        {
            (result, reason) = (EvalStatus.Pass, null);
        }

        return new EvalReport(suite, model, result, reason, score, failUnder, graded, passed, ungraded, safetyFailed, results, personaSource,
            repeat, flaky, comparison);
    }

    /// <summary>Plain labeled lines: ASCII, no color, no glyphs, so it reads in a screen reader and a log.</summary>
    internal static string[] Render(EvalReport r)
    {
        var lines = new List<string>
        {
            $"Suite: {Clean(r.Suite)} ({r.Fixtures.Count} fixture{(r.Fixtures.Count == 1 ? "" : "s")}, model {Clean(r.Model)})",
        };

        if (r.PersonaSource is not null)
        {
            lines.Add($"Personas: {Clean(r.PersonaSource)}");
        }
        if (r.Repeat > 1)
        {
            lines.Add($"Repeat: {r.Repeat} runs per fixture (a fixture passes only if every run passes)");
        }

        foreach (var f in r.Fixtures)
        {
            var tag = f.Status.ToUpperInvariant().PadRight(8);
            var detail = f.Status switch
            {
                EvalStatus.Ungraded => $"no assertions ({f.UngradedTraits} trait{(f.UngradedTraits == 1 ? "" : "s")} need a judge)",
                EvalStatus.Error => f.Error ?? "error",
                _ => $"{f.Passed}/{f.Total} assertions",
            };
            if (r.Repeat > 1 && f.Status != EvalStatus.Ungraded)
            {
                detail += f.Flaky ? $" [FLAKY: {f.PassedRuns} of {f.Runs} runs passed]" : $" [{f.PassedRuns} of {f.Runs} runs passed]";
            }
            lines.Add($"  {tag} {Clean(f.Id)} -- {detail}{(f.Safety ? " [safety]" : "")}");
            foreach (var failure in f.Failures)
            {
                lines.Add($"           - {failure.Type}: {failure.Message}");
            }
        }

        lines.Add($"Score: {r.ScorePercent:0.#}% ({r.Passed} of {r.Graded} graded fixtures passed, {r.Ungraded} ungraded)");
        if (r.Repeat > 1)
        {
            lines.Add($"Flaky: {r.FlakyCount} fixture{(r.FlakyCount == 1 ? "" : "s")}");
        }
        if (r.Baseline is { } b)
        {
            lines.Add($"Baseline: {b.Regressions.Count} regression(s), {b.Improvements.Count} improvement(s), {b.Added.Count} new, {b.Removed.Count} no longer in the suite"
                + (b.ModelMismatch ? $" (baseline was recorded with model {Clean(b.BaselineModel)}, this run used {Clean(r.Model)})" : ""));
            foreach (var id in b.Regressions) lines.Add($"           - regressed: {Clean(id)}");
        }
        lines.Add($"Fail under: {r.FailUnder:0.#}%");
        lines.Add($"Result: {r.Result.ToUpperInvariant()}" + (r.Reason is null ? "" : $" -- {r.Reason}"));
        return lines.ToArray();
    }

    private static string Clean(string? s) => CapabilityRejection.Scrub(s ?? string.Empty);
    private static string Trunc(string s, int n) => s.Length <= n ? s : s[..n] + "...";
}
