using Microsoft.Extensions.AI;

namespace AzureOpenAI_CLI.Tests.Benchmarks;

/// <summary>
/// S03E12 -- self-consistency for the bench harness. If the harness is going
/// into the preflight loop, the harness itself has to be deterministic
/// within tolerance against a deterministic-latency fake. Bania's first
/// rule: noise is the enemy of signal -- prove the noise floor before you
/// go measuring anything.
///
/// All tests here drive <see cref="FakeChatClient"/> through
/// <see cref="BenchmarkHarness.RunAsync"/>. No network. No SDK. Each test
/// caps wall-clock under ~2s so the suite stays well under the 5s preflight
/// budget for the bench class.
/// </summary>
public class BenchmarkHarnessTests
{
    [Fact]
    public async Task RunAsync_ZeroLatencyFake_StatisticsAreNonNegativeAndOrdered()
    {
        var fake = new FakeChatClient();
        var result = await BenchmarkHarness.RunAsync(
            async () => { await fake.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "ping") }); },
            warmup: 2,
            measured: 30);

        Assert.Equal(30, result.SampleCount);
        Assert.Equal(2, result.WarmupCount);
        Assert.True(result.Min >= 0);
        Assert.True(result.Mean >= 0);
        Assert.True(result.Max >= result.Min);
        Assert.True(result.P50 >= result.Min);
        Assert.True(result.P95 >= result.P50);
        Assert.True(result.P99 >= result.P95);
        Assert.True(result.P99 <= result.Max + 1e-9);
        Assert.Equal(2 + 30, fake.CallCount);
    }

    [Fact]
    public async Task RunAsync_DeterministicDelay_MeanWithinTolerance()
    {
        // 5ms artificial delay -- big enough to dominate noise on CI,
        // small enough that 30 samples finishes in ~150ms.
        var delay = TimeSpan.FromMilliseconds(5);
        var fake = new FakeChatClient(firstTokenLatency: delay);
        var result = await BenchmarkHarness.RunAsync(
            async () => { await fake.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "ping") }); },
            warmup: 3,
            measured: 30);

        // Mean must be at least the floor (Task.Delay can run a touch over
        // but never under). Tolerance on the upper bound is wide -- CI
        // schedulers can stretch a 5ms sleep to 25ms easily; we are
        // checking the harness math, not the kernel scheduler.
        Assert.True(result.Mean >= 4.0, $"Mean {result.Mean:F3}ms below floor (expected >= 4ms for 5ms Task.Delay).");
        Assert.True(result.P50 >= 4.0, $"P50 {result.P50:F3}ms below floor.");
        Assert.True(result.P50 <= 50.0, $"P50 {result.P50:F3}ms wildly above expected 5ms (scheduler thrash?).");
    }

    [Fact]
    public async Task RunAsync_StreamingFake_EmitsRequestedTokenCount()
    {
        var fake = new FakeChatClient(tokenCount: 7);
        int totalUpdates = 0;
        await foreach (var _ in fake.GetStreamingResponseAsync(new[] { new ChatMessage(ChatRole.User, "stream") }))
        {
            totalUpdates++;
        }
        Assert.Equal(7, totalUpdates);
        Assert.Equal(1, fake.CallCount);
    }

    [Fact]
    public void Percentile_KnownDistribution_MatchesExpected()
    {
        // 1..10 sorted -- p50 = 5.5, p95 = 9.55, p99 = 9.91 under R-7.
        double[] sorted = Enumerable.Range(1, 10).Select(i => (double)i).ToArray();
        Assert.Equal(5.5, BenchmarkResult.Percentile(sorted, 0.50), 9);
        Assert.Equal(9.55, BenchmarkResult.Percentile(sorted, 0.95), 9);
        Assert.Equal(9.91, BenchmarkResult.Percentile(sorted, 0.99), 9);
        Assert.Equal(1.0, BenchmarkResult.Percentile(sorted, 0.0));
        Assert.Equal(10.0, BenchmarkResult.Percentile(sorted, 1.0));
    }

    [Fact]
    public void Percentile_SingleSample_ReturnsThatSample()
    {
        double[] sorted = { 42.0 };
        Assert.Equal(42.0, BenchmarkResult.Percentile(sorted, 0.0));
        Assert.Equal(42.0, BenchmarkResult.Percentile(sorted, 0.5));
        Assert.Equal(42.0, BenchmarkResult.Percentile(sorted, 1.0));
    }

    [Fact]
    public void From_ZeroSamples_Throws()
    {
        Assert.Throws<ArgumentException>(() => BenchmarkResult.From(Array.Empty<double>(), 0, 0));
    }

    [Fact]
    public async Task RunAsync_InvalidSampleCount_Throws()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await BenchmarkHarness.RunAsync(() => Task.CompletedTask, warmup: 1, measured: 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await BenchmarkHarness.RunAsync(() => Task.CompletedTask, warmup: -1, measured: 1));
    }

    [Fact]
    public void MarkdownRow_RendersFixedPoint()
    {
        var result = new BenchmarkResult(
            WarmupCount: 3, SampleCount: 30,
            Mean: 5.123, Stdev: 0.456,
            Min: 4.0, Max: 7.0,
            P50: 5.0, P95: 6.5, P99: 6.9);
        var row = result.ToMarkdownRow("zero-latency");
        Assert.Contains("| zero-latency | 30 |", row, StringComparison.Ordinal);
        Assert.Contains("5.123", row, StringComparison.Ordinal);
        Assert.Contains("0.456", row, StringComparison.Ordinal);
        Assert.StartsWith("| Scenario", BenchmarkResult.MarkdownHeader, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_DeterministicDelay_StdevSmallRelativeToMean()
    {
        // Same as the mean-tolerance test, but asserts the variance is a
        // sane fraction of mean. Catches a harness bug where Stopwatch is
        // reset wrong and samples explode. Tolerance is generous (CI is
        // a noisy box) -- stdev <= mean is the floor.
        var fake = new FakeChatClient(firstTokenLatency: TimeSpan.FromMilliseconds(3));
        var result = await BenchmarkHarness.RunAsync(
            async () => { await fake.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "x") }); },
            warmup: 3,
            measured: 30);

        Assert.True(result.Mean > 0);
        // Loose: stdev shouldn't exceed 5x mean even on a thrashing CI.
        Assert.True(result.Stdev <= 5.0 * result.Mean,
            $"Stdev {result.Stdev:F3}ms grossly exceeds 5x mean {result.Mean:F3}ms.");
    }

    /// <summary>
    /// Gated snapshot run: emits the markdown table the S03E12 exec report
    /// quotes from. Only runs when <c>AZ_AI_BENCH_FULL=1</c>; the default
    /// preflight loop skips it so wall time stays under budget. To regenerate
    /// the table for the report, run:
    /// <code>AZ_AI_BENCH_FULL=1 dotnet test --filter Snapshot_EmitMarkdownTable</code>.
    /// </summary>
    [Fact]
    public async Task Snapshot_EmitMarkdownTable()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("AZ_AI_BENCH_FULL"), "1", StringComparison.Ordinal))
            return; // Skipped silently outside the full bench gate.

        Console.WriteLine();
        Console.WriteLine("=== S03E12 bench-harness self-consistency snapshot ===");
        Console.WriteLine(BenchmarkResult.MarkdownHeader);

        async Task Emit(string label, FakeChatClient fake)
        {
            var r = await BenchmarkHarness.RunAsync(
                async () => { await fake.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "ping") }); },
                warmup: 5, measured: 50);
            Console.WriteLine(r.ToMarkdownRow(label));
        }

        await Emit("zero-latency-fake", new FakeChatClient());
        await Emit("1ms-delay-fake", new FakeChatClient(firstTokenLatency: TimeSpan.FromMilliseconds(1)));
        await Emit("5ms-delay-fake", new FakeChatClient(firstTokenLatency: TimeSpan.FromMilliseconds(5)));
        await Emit("10ms-delay-fake", new FakeChatClient(firstTokenLatency: TimeSpan.FromMilliseconds(10)));
    }

    [Fact]
    public void FakeChatClient_InvalidTokenCount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FakeChatClient(tokenCount: -1));
        Assert.Throws<ArgumentException>(() => new FakeChatClient(tokenWord: ""));
    }
}
