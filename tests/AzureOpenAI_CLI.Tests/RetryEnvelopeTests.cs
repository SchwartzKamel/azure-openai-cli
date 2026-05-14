using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AzureOpenAI_CLI.Resilience;
using Microsoft.Extensions.AI;
using Xunit;

namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// S04E07 Wave 1 -- The Fallback (retry envelope). Hermetic smoke for
/// Frank Costanza's RetryEnvelope.Wrap surface. Puddy lands the full
/// chaos corpus in Wave 2; this is the wake-the-suite-up minimum.
///
/// Each fact pins a single invariant from the brief AC and runs against
/// an inline ScheduledChatClient stub. No wall-clock dependence: the
/// envelope's TimeProvider and sleep delegate are injected.
/// </summary>
public class RetryEnvelopeTests
{
    private static ChainPolicy TestPolicy(int maxAttempts = 3, int budgetMs = 5000)
        => new(
            new RetryPolicy(maxAttempts),
            new BackoffPolicy(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(2)),
            TimeSpan.FromMilliseconds(budgetMs));

    /// <summary>No-op sleep for tests -- the envelope's deadline math is
    /// driven by the injected TimeProvider, not by actual elapsed time.</summary>
    private static readonly Func<TimeSpan, CancellationToken, Task> NoSleep =
        (_, _) => Task.CompletedTask;

    [Fact]
    public async Task GetResponseAsync_HappyPath_SingleCall_NoWarn()
    {
        var inner = new ScheduledChatClient(new[] { Outcome.Ok("hello") });
        var warns = new List<string>();
        var policy = TestPolicy();

        var wrapped = RetryEnvelope.Wrap(
            inner, policy, "azure", "gpt-test",
            warnSink: warns.Add,
            timeProvider: new FakeTimeProvider(),
            sleep: NoSleep,
            jitterRng: new Random(0));

        var resp = await wrapped.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "ping") });

        Assert.Single(inner.Calls);
        Assert.Empty(warns);
        Assert.Contains("hello", resp.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetResponseAsync_503ThenSuccess_OneHop_WarnEmitted()
    {
        var inner = new ScheduledChatClient(new[]
        {
            Outcome.Throw(MakeHttp(HttpStatusCode.ServiceUnavailable)),
            Outcome.Ok("recovered"),
        });
        var warns = new List<string>();
        var policy = TestPolicy();

        var wrapped = RetryEnvelope.Wrap(
            inner, policy, "azure", "gpt-test",
            warnSink: warns.Add,
            timeProvider: new FakeTimeProvider(),
            sleep: NoSleep,
            jitterRng: new Random(0));

        var resp = await wrapped.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "ping") });

        Assert.Equal(2, inner.Calls.Count);
        Assert.Single(warns);
        Assert.Contains("[WARN] fallback:", warns[0], StringComparison.Ordinal);
        Assert.Contains("attempts=2", warns[0], StringComparison.Ordinal);
        Assert.Contains("outcome=ok", warns[0], StringComparison.Ordinal);
        Assert.Contains("recovered", resp.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetResponseAsync_BudgetExhausted_SurfacesLastError()
    {
        // Persistent 503; clock advances past budget during sleep so the
        // envelope never reaches max attempts.
        var inner = new ScheduledChatClient(new[]
        {
            Outcome.Throw(MakeHttp(HttpStatusCode.ServiceUnavailable)),
            Outcome.Throw(MakeHttp(HttpStatusCode.ServiceUnavailable)),
            Outcome.Throw(MakeHttp(HttpStatusCode.ServiceUnavailable)),
        });
        var warns = new List<string>();
        var policy = TestPolicy(maxAttempts: 5, budgetMs: 10);
        var clock = new FakeTimeProvider();
        Func<TimeSpan, CancellationToken, Task> burnsBudget = (d, _) =>
        {
            clock.Advance(TimeSpan.FromMilliseconds(50));
            return Task.CompletedTask;
        };

        var wrapped = RetryEnvelope.Wrap(
            inner, policy, "azure", "gpt-test",
            warnSink: warns.Add,
            timeProvider: clock,
            sleep: burnsBudget,
            jitterRng: new Random(0));

        var ex = await Assert.ThrowsAsync<FallbackBudgetExhaustedException>(
            () => wrapped.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "ping") }));

        Assert.IsType<HttpRequestException>(ex.InnerException);
        Assert.Single(warns);
        Assert.Contains("budget-exhausted", warns[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_FailureAfterFirstToken_NoRetry()
    {
        // First call streams one chunk then fails -- pre-first-token
        // invariant must abort the chain, NOT trigger a retry.
        var inner = new ScheduledChatClient(new[]
        {
            Outcome.StreamThenThrow("partial", MakeHttp(HttpStatusCode.ServiceUnavailable)),
            Outcome.Ok("would-be-recovery"),
        });
        var warns = new List<string>();
        var policy = TestPolicy();

        var wrapped = RetryEnvelope.Wrap(
            inner, policy, "azure", "gpt-test",
            warnSink: warns.Add,
            timeProvider: new FakeTimeProvider(),
            sleep: NoSleep,
            jitterRng: new Random(0));

        var collected = new List<string>();
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await foreach (var u in wrapped.GetStreamingResponseAsync(
                new[] { new ChatMessage(ChatRole.User, "ping") }))
            {
                collected.Add(u.Text ?? string.Empty);
            }
        });

        // Exactly one upstream attempt: the post-first-token failure must
        // NOT have triggered a retry of the recovery outcome.
        Assert.Single(inner.Calls);
        Assert.Contains("partial", collected);
    }

    [Fact]
    public void ChainPolicy_FromEnvironment_ClampsBudgetAndRetries()
    {
        var env = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["AZ_AI_FALLBACK_BUDGET_MS"] = "999999",
            ["AZ_AI_FALLBACK_RETRIES"] = "999",
        };
        var policy = ChainPolicy.FromEnvironment(k => env.TryGetValue(k, out var v) ? v : null);

        Assert.Equal(ChainPolicy.MaxBudgetMs, (int)policy.WallClockBudget.TotalMilliseconds);
        // RETRIES is clamped to MaxRetriesAllowed; MaxAttempts = retries + 1.
        Assert.Equal(RetryPolicy.MaxRetriesAllowed + 1, policy.Retry.MaxAttempts);

        // BUDGET_MS=0 forces single-attempt regardless of RETRIES.
        env["AZ_AI_FALLBACK_BUDGET_MS"] = "0";
        var disabled = ChainPolicy.FromEnvironment(k => env.TryGetValue(k, out var v) ? v : null);
        Assert.False(disabled.IsActive);
        Assert.Equal(1, disabled.Retry.MaxAttempts);
    }

    // ── Test infrastructure ─────────────────────────────────────────────

    private static HttpRequestException MakeHttp(HttpStatusCode code)
        => new(message: "synthetic " + (int)code, inner: null, statusCode: code);

    /// <summary>Deterministic monotonic clock for budget assertions.</summary>
    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 6, 4, 2, 47, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now += delta;
    }

    private enum OutcomeKind { Ok, Throw, StreamThenThrow }

    private readonly struct Outcome
    {
        public OutcomeKind Kind { get; }
        public string? Text { get; }
        public Exception? Error { get; }
        private Outcome(OutcomeKind k, string? text, Exception? err)
        {
            Kind = k;
            Text = text;
            Error = err;
        }
        public static Outcome Ok(string text) => new(OutcomeKind.Ok, text, null);
        public static Outcome Throw(Exception ex) => new(OutcomeKind.Throw, null, ex);
        public static Outcome StreamThenThrow(string partial, Exception ex)
            => new(OutcomeKind.StreamThenThrow, partial, ex);
    }

    /// <summary>Returns a pre-programmed schedule of outcomes, one per call.</summary>
    private sealed class ScheduledChatClient : IChatClient
    {
        private readonly Outcome[] _schedule;
        private int _idx;
        public List<int> Calls { get; } = new();

        public ScheduledChatClient(Outcome[] schedule) => _schedule = schedule;

        private Outcome NextOutcome()
        {
            Calls.Add(_idx);
            var o = _idx < _schedule.Length ? _schedule[_idx] : _schedule[^1];
            _idx++;
            return o;
        }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var o = NextOutcome();
            return o.Kind switch
            {
                OutcomeKind.Ok => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, o.Text!))),
                OutcomeKind.Throw => Task.FromException<ChatResponse>(o.Error!),
                OutcomeKind.StreamThenThrow => Task.FromException<ChatResponse>(o.Error!),
                _ => Task.FromException<ChatResponse>(new InvalidOperationException("unknown outcome")),
            };
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var o = NextOutcome();
            switch (o.Kind)
            {
                case OutcomeKind.Ok:
                    yield return new ChatResponseUpdate(ChatRole.Assistant, o.Text);
                    yield break;
                case OutcomeKind.Throw:
                    await Task.Yield();
                    throw o.Error!;
                case OutcomeKind.StreamThenThrow:
                    yield return new ChatResponseUpdate(ChatRole.Assistant, o.Text);
                    await Task.Yield();
                    throw o.Error!;
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
