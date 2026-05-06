using System.Diagnostics;

namespace AzureOpenAI_CLI.Tests.Benchmarks;

/// <summary>
/// S03E12 -- The Receipt. Tiny in-process benchmark harness. Runs N warm-up
/// iterations (discarded), M measured iterations, and computes mean / p50 /
/// p95 / p99 / stdev over the measured set. NO network, NO file I/O,
/// NO BenchmarkDotNet -- a real BDN run is too slow for the preflight loop;
/// this is the gate-grade harness that runs on every PR.
///
/// Bania's rule: report sample size, variance, and the box it ran on, or do
/// not report at all. The harness records sample size and variance; the box
/// is the CI runner class (recorded by the workflow, not by this code).
///
/// Hardware-pinning, statistical-significance dispatch, and 30-day rolling
/// baselines live in the CI workflow and the longer-running suite gated
/// behind <c>AZ_AI_BENCH_FULL=1</c>. This file is the in-process kernel.
/// </summary>
internal static class BenchmarkHarness
{
    /// <summary>
    /// Run the supplied async <paramref name="action"/> <paramref name="warmup"/>
    /// times (discarded), then <paramref name="measured"/> times under
    /// stopwatch instrumentation. Returns the latency distribution.
    /// </summary>
    /// <param name="action">The unit of work to time. Should perform the same
    /// nominal work each call so the distribution is meaningful.</param>
    /// <param name="warmup">Number of warm-up iterations. JIT, allocator, DNS
    /// caches, and connection pools all need a chance to settle before we
    /// start recording. Default: 3.</param>
    /// <param name="measured">Number of measured iterations. p99 is unstable
    /// below 30; default 50 strikes a balance for a sub-second harness.</param>
    public static async Task<BenchmarkResult> RunAsync(
        Func<Task> action,
        int warmup = 3,
        int measured = 50,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (warmup < 0) throw new ArgumentOutOfRangeException(nameof(warmup));
        if (measured <= 0) throw new ArgumentOutOfRangeException(nameof(measured));

        for (int i = 0; i < warmup; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await action().ConfigureAwait(false);
        }

        var samples = new double[measured];
        var sw = new Stopwatch();
        for (int i = 0; i < measured; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sw.Restart();
            await action().ConfigureAwait(false);
            sw.Stop();
            samples[i] = sw.Elapsed.TotalMilliseconds;
        }

        return BenchmarkResult.From(samples, warmup, measured);
    }
}

/// <summary>
/// Latency distribution for a benchmark run. All values in milliseconds.
/// p50/p95/p99 use linear interpolation between order statistics
/// (R-7, the Excel / NumPy default) so small sample sizes are well-behaved.
/// </summary>
internal sealed record BenchmarkResult(
    int WarmupCount,
    int SampleCount,
    double Mean,
    double Stdev,
    double Min,
    double Max,
    double P50,
    double P95,
    double P99)
{
    /// <summary>
    /// Compute the distribution from raw samples. Sorts in place internally
    /// (caller-owned array is not mutated).
    /// </summary>
    public static BenchmarkResult From(IReadOnlyList<double> samples, int warmup, int measured)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count == 0)
            throw new ArgumentException("Cannot compute statistics from zero samples.", nameof(samples));

        var sorted = samples.ToArray();
        Array.Sort(sorted);

        double sum = 0;
        for (int i = 0; i < sorted.Length; i++) sum += sorted[i];
        double mean = sum / sorted.Length;

        double sq = 0;
        for (int i = 0; i < sorted.Length; i++)
        {
            double d = sorted[i] - mean;
            sq += d * d;
        }
        double stdev = sorted.Length > 1 ? Math.Sqrt(sq / (sorted.Length - 1)) : 0.0;

        return new BenchmarkResult(
            WarmupCount: warmup,
            SampleCount: measured,
            Mean: mean,
            Stdev: stdev,
            Min: sorted[0],
            Max: sorted[^1],
            P50: Percentile(sorted, 0.50),
            P95: Percentile(sorted, 0.95),
            P99: Percentile(sorted, 0.99));
    }

    /// <summary>
    /// Linear-interpolation percentile (R-7). <paramref name="sorted"/> must
    /// already be ascending. <paramref name="q"/> is in [0, 1].
    /// </summary>
    public static double Percentile(double[] sorted, double q)
    {
        ArgumentNullException.ThrowIfNull(sorted);
        if (sorted.Length == 0)
            throw new ArgumentException("Empty array.", nameof(sorted));
        if (q < 0.0 || q > 1.0)
            throw new ArgumentOutOfRangeException(nameof(q), "Percentile must be in [0, 1].");

        if (sorted.Length == 1) return sorted[0];
        double rank = q * (sorted.Length - 1);
        int lo = (int)Math.Floor(rank);
        int hi = (int)Math.Ceiling(rank);
        if (lo == hi) return sorted[lo];
        double frac = rank - lo;
        return sorted[lo] + (sorted[hi] - sorted[lo]) * frac;
    }

    /// <summary>
    /// Render as a small Markdown table row; useful for exec-report inserts.
    /// All numbers fixed-point with 3 decimals (millisecond grain).
    /// </summary>
    public string ToMarkdownRow(string label) =>
        $"| {label} | {SampleCount} | {Mean:F3} | {P50:F3} | {P95:F3} | {P99:F3} | {Stdev:F3} |";

    /// <summary>Markdown header for the row format above.</summary>
    public static string MarkdownHeader =>
        "| Scenario | n | mean ms | p50 ms | p95 ms | p99 ms | stdev ms |\n"
        + "|---|---:|---:|---:|---:|---:|---:|";
}
