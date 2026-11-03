using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzureOpenAI_CLI.Eval;

/// <summary>One fixture's recorded outcome in a baseline file.</summary>
internal sealed class EvalBaselineEntry
{
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("runs")] public int Runs { get; set; }
    [JsonPropertyName("passed_runs")] public int PassedRuns { get; set; }
}

/// <summary>
/// A recorded run to compare later runs against: the "golden". Per fixture it keeps only the
/// outcome (never the model's text), so a baseline cannot leak output and stays small.
/// </summary>
internal sealed class EvalBaselineFile
{
    [JsonPropertyName("suite")] public string Suite { get; set; } = "";
    [JsonPropertyName("model")] public string Model { get; set; } = "";
    [JsonPropertyName("repeat")] public int Repeat { get; set; } = 1;
    [JsonPropertyName("fixtures")] public Dictionary<string, EvalBaselineEntry> Fixtures { get; set; } = new();
}

/// <summary>How a run compares with its baseline.</summary>
internal sealed record EvalBaselineComparison(
    [property: JsonPropertyName("regressions")] List<string> Regressions,
    [property: JsonPropertyName("improvements")] List<string> Improvements,
    [property: JsonPropertyName("added")] List<string> Added,
    [property: JsonPropertyName("removed")] List<string> Removed,
    [property: JsonPropertyName("model_mismatch")] bool ModelMismatch,
    [property: JsonPropertyName("baseline_model")] string? BaselineModel);

/// <summary>
/// Baselines turn "the score is high enough" into "nothing that used to pass has stopped passing".
/// A score threshold alone lets one fixture fail quietly as long as others improve; a regression
/// gate does not.
///
/// Rules, each earned by a failure mode:
/// <list type="bullet">
///   <item><b>Regression means pass to not-pass.</b> A fixture that failed in the baseline and still
///     fails is not a regression (the baseline was already red there); one that passed and now fails,
///     errors or flakes is.</item>
///   <item><b>A different model is flagged, not failed.</b> Comparing across models is allowed but
///     visible; each model should have its own baseline file.</item>
///   <item><b>Removed fixtures do not hide regressions.</b> They are listed, and ignored only when
///     <c>--filter</c> narrowed the run on purpose.</item>
///   <item><b>Atomic writes.</b> A half-written baseline would make every later run fail or, worse,
///     pass against nothing, so writes go through a temp file and a rename.</item>
/// </list>
/// </summary>
internal static class EvalBaselines
{
    internal const long MaxBaselineBytes = 262_144;

    internal static EvalBaselineFile Create(EvalReport report) => new()
    {
        Suite = report.Suite,
        Model = report.Model,
        Repeat = report.Repeat,
        Fixtures = report.Fixtures
            .Where(f => f.Status != EvalStatus.Ungraded)
            .ToDictionary(f => f.Id, f => new EvalBaselineEntry { Status = f.Status, Runs = f.Runs, PassedRuns = f.PassedRuns }),
    };

    internal static EvalBaselineComparison Compare(
        List<EvalFixtureResult> results, EvalBaselineFile baseline, string model, bool ignoreMissing)
    {
        var regressions = new List<string>();
        var improvements = new List<string>();
        var added = new List<string>();

        foreach (var r in results.Where(r => r.Status != EvalStatus.Ungraded))
        {
            if (!baseline.Fixtures.TryGetValue(r.Id, out var was))
            {
                added.Add(r.Id);
                continue;
            }
            var passedBefore = was.Status == EvalStatus.Pass;
            var passesNow = r.Status == EvalStatus.Pass;
            if (passedBefore && !passesNow) regressions.Add(r.Id);
            else if (!passedBefore && passesNow) improvements.Add(r.Id);
        }

        var present = results.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        var removed = ignoreMissing
            ? new List<string>()
            : baseline.Fixtures.Keys.Where(id => !present.Contains(id)).OrderBy(id => id, StringComparer.Ordinal).ToList();

        var mismatch = !string.Equals(baseline.Model, model, StringComparison.OrdinalIgnoreCase);
        return new EvalBaselineComparison(regressions, improvements, added, removed, mismatch,
            mismatch ? baseline.Model : null);
    }

    internal static bool TryLoad(string path, out EvalBaselineFile? baseline, out string? error)
    {
        baseline = null;
        error = null;
        if (string.IsNullOrWhiteSpace(path)) { error = "no baseline file given"; return false; }
        if (!File.Exists(path)) { error = $"baseline file not found: {path}"; return false; }

        var length = new FileInfo(path).Length;
        if (length > MaxBaselineBytes)
        {
            error = $"baseline file is {length} bytes; the limit is {MaxBaselineBytes}";
            return false;
        }

        try
        {
            baseline = JsonSerializer.Deserialize(File.ReadAllText(path), AppJsonContext.Default.EvalBaselineFile);
        }
        catch (JsonException ex)
        {
            error = $"baseline is not valid JSON: {ex.Message}";
            return false;
        }
        catch (IOException ex)
        {
            error = $"cannot read baseline: {ex.Message}";
            return false;
        }

        if (baseline is null || baseline.Fixtures.Count == 0)
        {
            error = "baseline has no fixtures, so it cannot gate anything";
            baseline = null;
            return false;
        }
        if (baseline.Fixtures.Values.Any(e => e.Status is not (EvalStatus.Pass or EvalStatus.Fail or EvalStatus.Error or EvalStatus.Filtered)))
        {
            error = "baseline has a fixture with an unknown status";
            baseline = null;
            return false;
        }
        return true;
    }

    /// <summary>Write through a temp file and rename so a reader never sees a partial baseline.</summary>
    internal static void Write(string path, EvalBaselineFile baseline)
    {
        var full = Path.GetFullPath(path);
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var tmp = full + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(tmp, JsonSerializer.Serialize(baseline, AppJsonContext.Default.EvalBaselineFile) + "\n");
            File.Move(tmp, full, overwrite: true);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }
}
