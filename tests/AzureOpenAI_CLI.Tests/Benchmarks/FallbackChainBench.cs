using AzureOpenAI_CLI.Resilience;
using Microsoft.Extensions.AI;

namespace AzureOpenAI_CLI.Tests.Benchmarks;

/// <summary>
/// S04E07 -- The Fallback. Bania-grade happy-path overhead gate for the
/// RetryEnvelope shipped in W1. Hand-rolled, in-process, NO network, NO
/// SDK. Measures the per-call latency tail of a successful first-attempt
/// chat call with and without the envelope wrapping it.
///
/// Pass criterion: wrapped p99 minus baseline p99 must stay below
/// <see cref="OverheadBudgetMs"/> (0.5 ms). If we breach, Frank rolls
/// back to a lazier policy wrapper (see ADR-015 fallback doctrine).
///
/// Bania's rules honoured here:
///   * Same harness, same fake, same call shape on both legs -- only the
///     wrapping differs.
///   * Warm-up iterations are discarded so JIT/allocator settle before
///     the first measured sample.
///   * Sample size is large enough that p99 has a meaningful rank
///     (10 000 samples puts p99 at rank ~9 900 with 100 samples in the
///     tail). Below ~1 000 samples p99 is just "the worst few," which is
///     not a percentile.
///   * Threshold lives in code so the budget is reviewable in diff. Any
///     change to <see cref="OverheadBudgetMs"/> needs an ADR-015 amendment.
///
/// Heavier hardware-pinned BenchmarkDotNet runs are gated behind
/// AZ_AI_BENCH_FULL=1 elsewhere; this is the preflight-grade gate.
/// </summary>
public class FallbackChainBench
{
    /// <summary>Wrapped-minus-baseline p99 ceiling in milliseconds.</summary>
    private const double OverheadBudgetMs = 0.5;

    /// <summary>Warmup iterations (JIT + allocator settle).</summary>
    private const int Warmup = 1_000;

    /// <summary>Measured iterations -- enough samples for a meaningful p99.</summary>
    private const int Measured = 10_000;

    /// <summary>
    /// In an env-noise sanity test the harness can over-report on a hot
    /// CI runner. We do not want a flaky red, so when AZ_AI_BENCH_FULL is
    /// not set we record the delta and require it to be non-pathological
    /// (under <see cref="LooseOverheadCeilingMs"/>). With FULL set, the
    /// tight 0.5 ms budget is enforced. Toggle at the workflow layer.
    /// </summary>
    private const double LooseOverheadCeilingMs = 2.0;

    [Fact]
    public async Task HappyPath_WrappedOverheadStaysUnderBudget()
    {
        var baselineClient = new FakeChatClient();
        var wrappedClient = RetryEnvelope.Wrap(
            new FakeChatClient(),
            ChainPolicy.Default,
            provider: "bench",
            model: "bench-model",
            warnSink: null);

        // Defensive: if Wrap ever returns the inner unchanged we are not
        // measuring what we think we are measuring. Bania's rule -- the
        // baseline you cannot reproduce is not a baseline.
        Assert.NotSame(baselineClient, wrappedClient);
        Assert.True(ChainPolicy.Default.IsActive,
            "ChainPolicy.Default must be IsActive for the wrapper to engage.");

        var messages = new[] { new ChatMessage(ChatRole.User, "ping") };

        var baseline = await BenchmarkHarness.RunAsync(
            async () => { await baselineClient.GetResponseAsync(messages); },
            warmup: Warmup,
            measured: Measured);

        var wrapped = await BenchmarkHarness.RunAsync(
            async () => { await wrappedClient.GetResponseAsync(messages); },
            warmup: Warmup,
            measured: Measured);

        double deltaP99 = wrapped.P99 - baseline.P99;

        // Surface the numbers via the harness's markdown row format so a
        // failing assertion message is paste-ready for the exec report.
        string report =
            $"\n{BenchmarkResult.MarkdownHeader}\n"
            + baseline.ToMarkdownRow("baseline (no-op IChatClient)") + "\n"
            + wrapped.ToMarkdownRow("wrapped  (RetryEnvelope.Default)") + "\n"
            + $"deltaP99 = {deltaP99:F4} ms  (budget = {OverheadBudgetMs:F3} ms)";

        // Always print to the test output stream so CI captures the row
        // even when the assertion passes.
        Console.WriteLine(report);

        bool full = string.Equals(
            Environment.GetEnvironmentVariable("AZ_AI_BENCH_FULL"),
            "1",
            StringComparison.Ordinal);
        double ceiling = full ? OverheadBudgetMs : LooseOverheadCeilingMs;

        Assert.True(
            deltaP99 < ceiling,
            $"RetryEnvelope happy-path overhead breached budget. {report}");
    }

    [Fact]
    public async Task HappyPath_WrappedCallsInnerExactlyOnce()
    {
        // Companion invariant: a successful first attempt must NOT retry.
        // If this ever fails, the p99 number above is meaningless because
        // the wrapped path is doing extra work the baseline is not.
        var inner = new FakeChatClient();
        var wrapped = RetryEnvelope.Wrap(
            inner,
            ChainPolicy.Default,
            provider: "bench",
            model: "bench-model",
            warnSink: null);

        var messages = new[] { new ChatMessage(ChatRole.User, "ping") };
        for (int i = 0; i < 16; i++)
        {
            await wrapped.GetResponseAsync(messages);
        }

        Assert.Equal(16, inner.CallCount);
    }
}
