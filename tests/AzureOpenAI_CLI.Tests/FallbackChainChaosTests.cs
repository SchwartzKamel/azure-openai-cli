using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AzureOpenAI_CLI.Resilience;
using AzureOpenAI_CLI.V2.Tests.Fixtures;
using Microsoft.Extensions.AI;
using Xunit;

namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// S04E07 Wave 2 -- The Fallback. Puddy's canonical hermetic suite for the
/// retry envelope wired up in Wave 1.
///
/// File-naming note: the brief lists this slot as <c>FallbackChainTests.cs</c>
/// but that filename was already claimed by the S03E22 cross-provider
/// switcher corpus. This file lands as <c>FallbackChainChaosTests.cs</c>
/// to avoid the collision and to surface "ChaosChatClient-driven" in the
/// filename. The surface under test is <see cref="RetryEnvelope"/> --
/// same-model retry + backoff + wall-clock budget. The S03E22
/// <c>FallbackChain</c> cross-provider switcher is a separate layer and
/// has its own corpus in <c>FallbackChainTests.cs</c>.
///
/// W2 corpus invariants (documented in-line on each fact where they
/// affect assertions):
///
///   * <see cref="RetryEnvelope"/> throws <see cref="FallbackBudgetExhaustedException"/>
///     ONLY when the wall-clock budget expires. Attempt-count exhaustion
///     surfaces the LAST underlying transient error verbatim. The chain-
///     level rc=3 mapping (brief AC#11) lives in the S03E22 outer chain,
///     not in this inner envelope.
///   * One WARN line per call site (brief AC#7 -- "One stderr WARN
///     summary on multi-hop success"), not one per attempt. Two retries
///     followed by success still emit a single WARN line.
///   * <c>AZ_AI_FALLBACK_RETRIES=0</c> and <c>AZ_AI_FALLBACK_BUDGET_MS=0</c>
///     both cause <see cref="RetryEnvelope.Wrap"/> to return the inner
///     client UNCHANGED (zero-allocation opt-out for the headless caller
///     that explicitly disables fallback).
/// </summary>
public class FallbackChainChaosTests
{
    // ── Helpers ─────────────────────────────────────────────────────────

    private static ChatOptions? Opts => null;

    private static IEnumerable<ChatMessage> Msgs() =>
        new[] { new ChatMessage(ChatRole.User, "ping") };

    private static ChainPolicy TestPolicy(int maxAttempts = 3, int budgetMs = 5000)
        => new(
            new RetryPolicy(maxAttempts),
            new BackoffPolicy(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(2)),
            TimeSpan.FromMilliseconds(budgetMs));

    private static readonly Func<TimeSpan, CancellationToken, Task> NoSleep =
        (_, ct) => { ct.ThrowIfCancellationRequested(); return Task.CompletedTask; };

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 6, 4, 2, 47, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now += delta;
    }

    private static IChatClient WrapDefault(
        IChatClient inner,
        ChainPolicy policy,
        List<string>? warns = null,
        TimeProvider? clock = null,
        Func<TimeSpan, CancellationToken, Task>? sleep = null,
        Random? rng = null)
        => RetryEnvelope.Wrap(
            inner, policy, "azure", "gpt-test",
            warnSink: warns is null ? null : warns.Add,
            timeProvider: clock ?? new FakeTimeProvider(),
            sleep: sleep ?? NoSleep,
            jitterRng: rng ?? new Random(0));

    // ── Happy path ──────────────────────────────────────────────────────

    [Fact]
    public async Task HappyPath_SingleCall_NoWarn()
    {
        var inner = new ChaosChatClient(new FailureMode.Success("hello"));
        var warns = new List<string>();
        var wrapped = WrapDefault(inner, TestPolicy(), warns);

        var resp = await wrapped.GetResponseAsync(Msgs());

        Assert.Single(inner.Calls);
        Assert.Empty(warns);
        Assert.Contains("hello", resp.Text, StringComparison.Ordinal);
    }

    // ── Same-model retries (transient set) ──────────────────────────────

    [Fact]
    public async Task OneTransient503_ThenSuccess_OneWarn_Rc0()
    {
        var inner = new ChaosChatClient(
            new FailureMode.Status(503),
            new FailureMode.Success("recovered"));
        var warns = new List<string>();
        var wrapped = WrapDefault(inner, TestPolicy(), warns);

        var resp = await wrapped.GetResponseAsync(Msgs());

        Assert.Equal(2, inner.Calls.Count);
        // W2 corpus: one summary WARN at multi-hop success, not one per attempt (AC#7).
        Assert.Single(warns);
        Assert.Contains("attempts=2", warns[0], StringComparison.Ordinal);
        Assert.Contains("outcome=ok", warns[0], StringComparison.Ordinal);
        Assert.Contains("recovered", resp.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TwoTransients503_ThenSuccess_OneSummaryWarn_Rc0()
    {
        var inner = new ChaosChatClient(
            new FailureMode.Status(503),
            new FailureMode.Status(503),
            new FailureMode.Success("third-time-lucky"));
        var warns = new List<string>();
        var wrapped = WrapDefault(inner, TestPolicy(maxAttempts: 3), warns);

        var resp = await wrapped.GetResponseAsync(Msgs());

        Assert.Equal(3, inner.Calls.Count);
        // W2 corpus: ONE summary WARN, not two. AC#7 is unambiguous --
        // operators get a single summary line per chain invocation.
        Assert.Single(warns);
        Assert.Contains("attempts=3", warns[0], StringComparison.Ordinal);
        Assert.Contains("outcome=ok", warns[0], StringComparison.Ordinal);
        Assert.Contains("third-time-lucky", resp.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AllAttemptsTransient_SurfacesUnderlyingError()
    {
        // W2 corpus: RetryEnvelope throws FallbackBudgetExhaustedException
        // ONLY on wall-clock budget expiry. Attempt-count exhaustion
        // surfaces the last underlying transient verbatim so the
        // surrounding cross-provider chain (S03E22) can re-classify.
        // The rc=3 mapping (brief AC#11) is the outer chain's job.
        var inner = new ChaosChatClient(
            new FailureMode.Status(503),
            new FailureMode.Status(503),
            new FailureMode.Status(503));
        var warns = new List<string>();
        var wrapped = WrapDefault(inner, TestPolicy(maxAttempts: 3), warns);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => wrapped.GetResponseAsync(Msgs()));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
        Assert.Equal(3, inner.Calls.Count);
        Assert.Single(warns);
        Assert.Contains("outcome=exhausted", warns[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task HardError400_NoRetry_SurfacesImmediately()
    {
        var inner = new ChaosChatClient(
            new FailureMode.Status(400),
            new FailureMode.Success("would-be-recovery"));
        var warns = new List<string>();
        var wrapped = WrapDefault(inner, TestPolicy(), warns);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => wrapped.GetResponseAsync(Msgs()));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        // Exactly one upstream call: hard 4xx must NOT trigger retry.
        Assert.Single(inner.Calls);
        Assert.Empty(warns);
    }

    [Theory]
    [InlineData(408)]
    [InlineData(425)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public async Task TransientStatusCode_RetriesAndRecovers(int code)
    {
        var inner = new ChaosChatClient(
            new FailureMode.Status(code),
            new FailureMode.Success("ok"));
        var wrapped = WrapDefault(inner, TestPolicy());

        var resp = await wrapped.GetResponseAsync(Msgs());

        Assert.Equal(2, inner.Calls.Count);
        Assert.Contains("ok", resp.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SocketConnectionReset_ClassifiedAsTransient_Retries()
    {
        var inner = new ChaosChatClient(
            new FailureMode.Throw(new SocketException((int)SocketError.ConnectionReset)),
            new FailureMode.Success("rebound"));
        var warns = new List<string>();
        var wrapped = WrapDefault(inner, TestPolicy(), warns);

        var resp = await wrapped.GetResponseAsync(Msgs());

        Assert.Equal(2, inner.Calls.Count);
        Assert.Single(warns);
        Assert.Contains("rebound", resp.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TaskCanceledException_HttpTimeout_ClassifiedAsTransient()
    {
        // No external CT cancelled -- this is the HTTP-timeout flavour
        // of the closed transient set (brief table row 6).
        var inner = new ChaosChatClient(
            new FailureMode.Hang(50),
            new FailureMode.Success("after-timeout"));
        var wrapped = WrapDefault(inner, TestPolicy());

        var resp = await wrapped.GetResponseAsync(Msgs());

        Assert.Equal(2, inner.Calls.Count);
        Assert.Contains("after-timeout", resp.Text, StringComparison.Ordinal);
    }

    // ── Streaming pre/post first-token invariant ────────────────────────

    [Fact]
    public async Task Streaming_PreFirstTokenFailure_Retries()
    {
        // afterChars=0 means stream throws before yielding any update.
        var inner = new ChaosChatClient(
            new FailureMode.StreamTruncate(AfterChars: 0),
            new FailureMode.Success("recovered-stream"));
        var wrapped = WrapDefault(inner, TestPolicy());

        var collected = new List<string>();
        await foreach (var u in wrapped.GetStreamingResponseAsync(Msgs()))
        {
            if (u.Text is not null) collected.Add(u.Text);
        }

        Assert.Equal(2, inner.Calls.Count);
        Assert.Contains("recovered-stream", collected);
    }

    [Fact]
    public async Task Streaming_PostFirstTokenFailure_DoesNotRetry_PartialSurfaced()
    {
        // First call yields one token then throws -- the invariant says
        // we MUST NOT retry; the partial transcript must surface.
        var inner = new ChaosChatClient(
            new FailureMode.StreamTruncate(AfterChars: 5),
            new FailureMode.Success("would-be-recovery"));
        var wrapped = WrapDefault(inner, TestPolicy());

        var collected = new List<string>();
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await foreach (var u in wrapped.GetStreamingResponseAsync(Msgs()))
            {
                if (u.Text is not null) collected.Add(u.Text);
            }
        });

        // Exactly one upstream attempt: no retry post-first-token.
        Assert.Single(inner.Calls);
        Assert.Single(collected);
        Assert.Equal("xxxxx", collected[0]);
    }

    // ── Budget exhaustion ───────────────────────────────────────────────

    [Fact]
    public async Task BudgetExhausted_ThrowsFallbackBudgetExhaustedException()
    {
        var inner = new ChaosChatClient(new FailureMode.Status(503));
        var warns = new List<string>();
        var clock = new FakeTimeProvider();
        // Each "sleep" burns 60 ms of clock; budget is only 50 ms.
        Func<TimeSpan, CancellationToken, Task> burnsBudget = (_, _) =>
        {
            clock.Advance(TimeSpan.FromMilliseconds(60));
            return Task.CompletedTask;
        };
        var wrapped = WrapDefault(
            inner,
            TestPolicy(maxAttempts: 5, budgetMs: 50),
            warns,
            clock: clock,
            sleep: burnsBudget);

        var ex = await Assert.ThrowsAsync<FallbackBudgetExhaustedException>(
            () => wrapped.GetResponseAsync(Msgs()));

        Assert.IsType<HttpRequestException>(ex.InnerException);
        Assert.Single(warns);
        // W2 corpus: exhaustion reason includes the literal token "budget".
        Assert.Contains("budget", warns[0], StringComparison.Ordinal);
    }

    // ── Backoff jitter range ────────────────────────────────────────────

    [Fact]
    public void BackoffJitter_StaysWithinExpectedRange()
    {
        var policy = new BackoffPolicy(
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(2000));
        var rng = new Random(42);

        for (int attempt = 0; attempt <= 5; attempt++)
        {
            // Cap = min(MaxDelay, BaseDelay * 2^attempt). For attempt=0..3
            // the exponential cap dominates; for attempt>=5 the MaxDelay
            // cap dominates.
            double expCapMs = 100.0 * Math.Pow(2, attempt);
            double expectedCapMs = Math.Min(2000.0, expCapMs);

            for (int trial = 0; trial < 50; trial++)
            {
                var delay = policy.ComputeDelay(attempt, rng);
                Assert.True(delay >= TimeSpan.Zero,
                    "delay must be non-negative; attempt=" + attempt);
                Assert.True(delay.TotalMilliseconds <= expectedCapMs + 0.001,
                    "delay must be <= cap (" + expectedCapMs + " ms); attempt=" + attempt
                    + " actual=" + delay.TotalMilliseconds);
            }
        }
    }

    // ── Env-var clamps and disables ─────────────────────────────────────

    [Fact]
    public void EnvRetries0_DisablesEnvelope_WrapReturnsInnerUnchanged()
    {
        var env = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["AZ_AI_FALLBACK_RETRIES"] = "0",
        };
        var policy = ChainPolicy.FromEnvironment(k => env.TryGetValue(k, out var v) ? v : null);

        Assert.False(policy.IsActive);
        Assert.Equal(1, policy.Retry.MaxAttempts);

        var inner = new ChaosChatClient(new FailureMode.Success("solo"));
        var wrapped = RetryEnvelope.Wrap(
            inner, policy, "azure", "gpt-test",
            warnSink: null, timeProvider: new FakeTimeProvider(),
            sleep: NoSleep, jitterRng: new Random(0));
        Assert.Same(inner, wrapped);
    }

    [Fact]
    public void EnvBudgetMs0_DisablesEnvelope_WrapReturnsInnerUnchanged()
    {
        var env = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["AZ_AI_FALLBACK_BUDGET_MS"] = "0",
        };
        var policy = ChainPolicy.FromEnvironment(k => env.TryGetValue(k, out var v) ? v : null);

        Assert.False(policy.IsActive);
        Assert.Equal(1, policy.Retry.MaxAttempts);
        Assert.Equal(TimeSpan.Zero, policy.WallClockBudget);

        var inner = new ChaosChatClient(new FailureMode.Success("solo"));
        var wrapped = RetryEnvelope.Wrap(
            inner, policy, "azure", "gpt-test",
            warnSink: null, timeProvider: new FakeTimeProvider(),
            sleep: NoSleep, jitterRng: new Random(0));
        Assert.Same(inner, wrapped);
    }

    [Fact]
    public void EnvRetries999_ClampedToMaxRetriesAllowed()
    {
        var env = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["AZ_AI_FALLBACK_RETRIES"] = "999",
        };
        var policy = ChainPolicy.FromEnvironment(k => env.TryGetValue(k, out var v) ? v : null);

        // RETRIES is the count BEYOND the first attempt; clamp ceiling is
        // RetryPolicy.MaxRetriesAllowed (= 10). MaxAttempts = retries + 1.
        Assert.Equal(RetryPolicy.MaxRetriesAllowed + 1, policy.Retry.MaxAttempts);
    }

    [Fact]
    public void EnvBudgetMs999999_ClampedToMaxBudgetMs()
    {
        var env = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["AZ_AI_FALLBACK_BUDGET_MS"] = "999999",
        };
        var policy = ChainPolicy.FromEnvironment(k => env.TryGetValue(k, out var v) ? v : null);

        Assert.Equal(ChainPolicy.MaxBudgetMs, (int)policy.WallClockBudget.TotalMilliseconds);
    }

    // ── External cancellation ───────────────────────────────────────────

    [Fact]
    public async Task ExternalCancellation_MidBackoff_ThrowsOperationCanceled_NotBudgetExhausted()
    {
        var inner = new ChaosChatClient(new FailureMode.Status(503));
        var warns = new List<string>();
        using var cts = new CancellationTokenSource();
        // Sleep delegate: trip the cancellation token, then honour it.
        Func<TimeSpan, CancellationToken, Task> cancellingSleep = (_, ct) =>
        {
            cts.Cancel();
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        };
        var wrapped = WrapDefault(
            inner,
            TestPolicy(maxAttempts: 5, budgetMs: 5000),
            warns,
            sleep: cancellingSleep);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => wrapped.GetResponseAsync(Msgs(), Opts, cts.Token));

        // W2 corpus: caller-triggered cancellation propagates as OCE; it
        // is NOT re-wrapped as FallbackBudgetExhaustedException even
        // though it fires inside the budget window.
    }

    // ── WARN content invariants ─────────────────────────────────────────

    [Fact]
    public async Task WarnLine_AsciiOnly_NoAnsi_ContainsModelAttemptsOutcome()
    {
        var inner = new ChaosChatClient(
            new FailureMode.Status(503),
            new FailureMode.Success("ok"));
        var warns = new List<string>();
        var wrapped = WrapDefault(inner, TestPolicy(), warns);

        await wrapped.GetResponseAsync(Msgs());

        Assert.Single(warns);
        var line = warns[0];

        // ASCII-only: every char must be 0x20-0x7E (printable) or tab.
        foreach (var ch in line)
        {
            Assert.True(ch == '\t' || (ch >= 0x20 && ch <= 0x7E),
                "non-ASCII char in WARN line: 0x" + ((int)ch).ToString("X4"));
        }

        // No ANSI escape sequences (CSI / OSC).
        Assert.DoesNotContain('\x1B', line);

        // Must contain model name, attempt count, and outcome label.
        Assert.Contains("model=gpt-test", line, StringComparison.Ordinal);
        Assert.Contains("attempts=2", line, StringComparison.Ordinal);
        Assert.Contains("outcome=ok", line, StringComparison.Ordinal);
        Assert.StartsWith("[WARN] fallback:", line, StringComparison.Ordinal);
    }
}
