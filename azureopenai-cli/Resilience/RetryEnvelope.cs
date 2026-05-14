using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AzureOpenAI_CLI.Observability;
using Microsoft.Extensions.AI;

namespace AzureOpenAI_CLI.Resilience;

// S04E07 -- The Fallback. Frank Costanza, on-call edition, the retry-and-
// budget envelope. Wraps an IChatClient with a same-model retry loop that
// applies a transient-error classifier (408, 425, 429, 500/502/503/504,
// and socket-level connect resets / timeouts), full-jitter exponential
// backoff between attempts, and a hard wall-clock budget across the chain.
//
// File-disjoint from FallbackChain.cs (S03E22, provider-preset switcher) on
// purpose: that file owns cross-provider alternate selection; this file
// owns retry-and-backoff within a single (provider, model) tuple. Both
// surfaces live in the Resilience namespace and compose -- E07 wraps the
// chat client first, S03E22's chain wraps the wrapped client afterward,
// so each alternate gets its own retry budget on its own clock.
//
// Doctrine (mirrored from docs/episode-briefs/s04e07-the-fallback.md):
//
//   * Opt-in by default. When the policy has MaxAttempts <= 1 or a zero
//     budget, Wrap returns the inner client unchanged -- zero allocation,
//     zero behaviour change for users who do not set the env vars.
//   * Transient set is closed and lives in code (see IsTransient). Adding
//     a row requires an ADR-015 amendment per the brief.
//   * Streaming pre-first-token invariant: once a token has flowed to the
//     caller, mid-stream failures are NEVER retried -- the user has
//     already seen output and a retry would produce an incoherent
//     transcript. The partial response is surfaced; the error propagates.
//   * Wall-clock budget is the hard ceiling. Backoff sleeps never overrun
//     the deadline; on expiry FallbackBudgetExhaustedException carries the
//     last underlying error.
//   * TimeProvider + sleep delegate are injectable so tests do not race
//     the wall clock. Production callers pass nothing and get the system
//     defaults.
//   * Telemetry is gated by AZ_AI_TELEMETRY=1 (strict equality). One
//     fallback_hop row per attempt via TelemetryEmitter.EmitFallbackHop.
//   * WARN summary on stderr fires only when more than one attempt was
//     needed (success path) or on budget exhaustion. ASCII only,
//     NO_COLOR-clean, one line, screen-reader friendly. Suppressed when
//     warnSink is null (the caller's --raw / --json gate).

/// <summary>
/// Number of attempts the retry envelope will make against a single model
/// before surfacing the underlying transient error.
/// <see cref="MaxAttempts"/> is the TOTAL attempt count including the
/// first call (so MaxAttempts=1 means "no retry, single attempt";
/// MaxAttempts=3 means "first call plus up to two retries"). The env-var
/// surface uses the more natural "retries beyond first" reading -- see
/// <see cref="FromEnvironment"/>.
/// </summary>
public sealed record RetryPolicy(int MaxAttempts)
{
    /// <summary>Default total attempts: 3 (first call + up to 2 retries).</summary>
    public const int DefaultMaxAttempts = 3;

    /// <summary>Hard ceiling on env-var-configured retries beyond the first call.</summary>
    public const int MaxRetriesAllowed = 10;

    /// <summary>Env-var name: AZ_AI_FALLBACK_RETRIES (retries beyond the first call).</summary>
    public const string EnvVarName = "AZ_AI_FALLBACK_RETRIES";

    /// <summary>Default policy: MaxAttempts = <see cref="DefaultMaxAttempts"/>.</summary>
    public static RetryPolicy Default { get; } = new(DefaultMaxAttempts);

    /// <summary>
    /// Resolve from the environment. AZ_AI_FALLBACK_RETRIES is the count of
    /// retries BEYOND the first attempt; clamped to [0, 10]. So
    /// RETRIES=0 -> MaxAttempts=1 (single attempt, no retry);
    /// RETRIES=2 -> MaxAttempts=3 (the default).
    /// Unset / malformed values fall back to <see cref="Default"/>.
    /// </summary>
    public static RetryPolicy FromEnvironment(Func<string, string?> getEnv)
    {
        ArgumentNullException.ThrowIfNull(getEnv);
        var raw = getEnv(EnvVarName);
        if (string.IsNullOrWhiteSpace(raw)) return Default;
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var retries))
        {
            return Default;
        }
        if (retries < 0) retries = 0;
        if (retries > MaxRetriesAllowed) retries = MaxRetriesAllowed;
        return new RetryPolicy(retries + 1);
    }
}

/// <summary>
/// Full-jitter exponential backoff parameters. Inter-attempt delay is
/// drawn uniformly at random from [0, min(MaxDelay, BaseDelay * 2^N)]
/// where N is the zero-based prior-attempt index.
/// </summary>
public sealed record BackoffPolicy(TimeSpan BaseDelay, TimeSpan MaxDelay)
{
    /// <summary>Default: 100ms base, 2000ms cap. Conservative for headless callers.</summary>
    public static BackoffPolicy Default { get; } =
        new(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(2000));

    /// <summary>
    /// Compute the next inter-attempt sleep duration. <paramref name="priorAttemptIndex"/>
    /// is zero-based: 0 means "after attempt #1 (first call)". RNG is
    /// supplied by the caller for determinism in tests.
    /// </summary>
    public TimeSpan ComputeDelay(int priorAttemptIndex, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        if (priorAttemptIndex < 0) priorAttemptIndex = 0;
        // Cap the exponent to avoid Math.Pow overflow on pathological values.
        int n = priorAttemptIndex > 30 ? 30 : priorAttemptIndex;
        double expMs = BaseDelay.TotalMilliseconds * Math.Pow(2, n);
        double cappedMs = Math.Min(MaxDelay.TotalMilliseconds, expMs);
        if (cappedMs <= 0) return TimeSpan.Zero;
        double jitteredMs = rng.NextDouble() * cappedMs;
        return TimeSpan.FromMilliseconds(jitteredMs);
    }
}

/// <summary>
/// Composite policy for the retry envelope. Resolved from the environment
/// (see <see cref="FromEnvironment"/>) with documented clamps so a malformed
/// env-var value never blows the wall-clock budget or fans out N billable
/// calls per keystroke.
/// </summary>
public sealed record ChainPolicy(
    RetryPolicy Retry,
    BackoffPolicy Backoff,
    TimeSpan WallClockBudget)
{
    /// <summary>Default budget: 5000 ms total across all attempts.</summary>
    public const int DefaultBudgetMs = 5000;

    /// <summary>Hard ceiling: 60000 ms. Operator foot-gun protection.</summary>
    public const int MaxBudgetMs = 60000;

    /// <summary>Env-var name: AZ_AI_FALLBACK_BUDGET_MS.</summary>
    public const string BudgetEnvVarName = "AZ_AI_FALLBACK_BUDGET_MS";

    /// <summary>Default policy: 3 attempts, default backoff, 5000ms budget.</summary>
    public static ChainPolicy Default { get; } =
        new(RetryPolicy.Default, BackoffPolicy.Default, TimeSpan.FromMilliseconds(DefaultBudgetMs));

    /// <summary>
    /// Resolve the composite policy from the environment. BUDGET_MS clamped
    /// to [0, 60000]; 0 means "no fallback, single attempt regardless of
    /// RETRIES". RETRIES clamped to [0, 10] (see <see cref="RetryPolicy.FromEnvironment"/>).
    /// </summary>
    public static ChainPolicy FromEnvironment(Func<string, string?> getEnv)
    {
        ArgumentNullException.ThrowIfNull(getEnv);
        var retry = RetryPolicy.FromEnvironment(getEnv);
        int budgetMs = DefaultBudgetMs;
        var rawBudget = getEnv(BudgetEnvVarName);
        if (!string.IsNullOrWhiteSpace(rawBudget)
            && int.TryParse(rawBudget, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            if (parsed < 0) parsed = 0;
            if (parsed > MaxBudgetMs) parsed = MaxBudgetMs;
            budgetMs = parsed;
        }
        // Brief: BUDGET_MS=0 means "no fallback, single attempt" -- force
        // a single-attempt retry policy on top so the operator's foot-gun
        // protection is total, not partial.
        if (budgetMs == 0)
        {
            retry = new RetryPolicy(1);
        }
        return new ChainPolicy(retry, BackoffPolicy.Default, TimeSpan.FromMilliseconds(budgetMs));
    }

    /// <summary>True when the envelope should wrap (retry > single, budget > 0).</summary>
    public bool IsActive =>
        Retry.MaxAttempts > 1 && WallClockBudget > TimeSpan.Zero;
}

/// <summary>
/// Thrown when the wall-clock budget expires before the retry envelope
/// reaches a successful response. The triggering transient error is
/// carried in <see cref="Exception.InnerException"/> so the caller's
/// existing error-classification path sees the actionable signal.
/// </summary>
public sealed class FallbackBudgetExhaustedException : Exception
{
    /// <summary>Stable telemetry error_class identifier.</summary>
    public const string ErrorClass = "FallbackBudgetExhausted";

    /// <summary>Configured budget at the time of expiry.</summary>
    public TimeSpan Budget { get; }

    /// <summary>Number of attempts made before the budget ran out.</summary>
    public int Attempts { get; }

    public FallbackBudgetExhaustedException(TimeSpan budget, int attempts, Exception lastError)
        : base($"Fallback retry budget of {(int)budget.TotalMilliseconds} ms exhausted "
              + $"after {attempts} attempt(s); surfacing last transient error.", lastError)
    {
        Budget = budget;
        Attempts = attempts;
    }
}

/// <summary>
/// Static entry point for wrapping an <see cref="IChatClient"/> with the
/// S04E07 retry envelope. See file-top commentary for the policy doctrine.
/// </summary>
public static class RetryEnvelope
{
    /// <summary>
    /// Wrap <paramref name="inner"/> with the retry-and-budget envelope
    /// described by <paramref name="policy"/>. Returns <paramref name="inner"/>
    /// unchanged when the policy is inactive.
    /// </summary>
    /// <param name="inner">The constructed chat client (post-resolver, post-capability-gate).</param>
    /// <param name="policy">Composite retry / backoff / budget policy.</param>
    /// <param name="provider">Provider name for telemetry (e.g. "azure", "foundry").</param>
    /// <param name="model">Model deployment name for telemetry / WARN summary.</param>
    /// <param name="warnSink">Stderr writer for the post-multi-hop summary. Null suppresses.</param>
    /// <param name="timeProvider">Clock source (defaults to <see cref="TimeProvider.System"/>).</param>
    /// <param name="sleep">Sleep delegate (defaults to <see cref="Task.Delay(TimeSpan, CancellationToken)"/>).</param>
    /// <param name="jitterRng">Jitter RNG (defaults to <see cref="Random.Shared"/>).</param>
    public static IChatClient Wrap(
        IChatClient inner,
        ChainPolicy policy,
        string provider,
        string model,
        Action<string>? warnSink,
        TimeProvider? timeProvider = null,
        Func<TimeSpan, CancellationToken, Task>? sleep = null,
        Random? jitterRng = null)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(policy);
        if (!policy.IsActive) return inner;
        return new RetryChatClient(
            inner, policy, provider, model, warnSink,
            timeProvider ?? TimeProvider.System,
            sleep ?? ((d, ct) => Task.Delay(d, ct)),
            jitterRng ?? Random.Shared);
    }

    /// <summary>
    /// True iff <paramref name="ex"/> belongs to the E07 closed transient
    /// set: HTTP 408 / 425 / 429 / 500 / 502 / 503 / 504, HTTP-timeout
    /// TaskCanceledException, or a socket-level connect reset / refused /
    /// aborted / unreachable / timeout. Caller-triggered cancellation is
    /// NOT transient -- it propagates.
    /// </summary>
    public static bool IsTransient(Exception ex, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ex);
        if (ex is OperationCanceledException && ct.IsCancellationRequested)
        {
            return false;
        }
        if (ex is TaskCanceledException)
        {
            return true;
        }
        if (ex is HttpRequestException http && http.StatusCode is HttpStatusCode code)
        {
            int n = (int)code;
            return n == 408 || n == 425 || n == 429 || (n >= 500 && n <= 504);
        }
        if (ex is SocketException se)
        {
            return se.SocketErrorCode is SocketError.ConnectionReset
                or SocketError.ConnectionRefused
                or SocketError.ConnectionAborted
                or SocketError.HostUnreachable
                or SocketError.NetworkUnreachable
                or SocketError.TimedOut;
        }
        return false;
    }
}

internal sealed class RetryChatClient : IChatClient
{
    private readonly IChatClient _inner;
    private readonly ChainPolicy _policy;
    private readonly string _provider;
    private readonly string _model;
    private readonly Action<string>? _warn;
    private readonly TimeProvider _time;
    private readonly Func<TimeSpan, CancellationToken, Task> _sleep;
    private readonly Random _jitter;

    public RetryChatClient(
        IChatClient inner,
        ChainPolicy policy,
        string provider,
        string model,
        Action<string>? warnSink,
        TimeProvider timeProvider,
        Func<TimeSpan, CancellationToken, Task> sleep,
        Random jitterRng)
    {
        _inner = inner;
        _policy = policy;
        _provider = provider;
        _model = model;
        _warn = warnSink;
        _time = timeProvider;
        _sleep = sleep;
        _jitter = jitterRng;
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var msgList = messages as IReadOnlyList<ChatMessage> ?? new List<ChatMessage>(messages);
        var startUtc = _time.GetUtcNow();
        var deadline = startUtc + _policy.WallClockBudget;
        Exception? lastError = null;

        for (int attempt = 1; attempt <= _policy.Retry.MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var resp = await _inner.GetResponseAsync(msgList, options, cancellationToken)
                    .ConfigureAwait(false);
                TelemetryEmitter.EmitFallbackHop(_provider, _model, attempt, "success", null);
                if (attempt > 1)
                {
                    EmitWarn(attempt, "ok", elapsedMs: (int)(_time.GetUtcNow() - startUtc).TotalMilliseconds, lastError);
                }
                return resp;
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                bool transient = RetryEnvelope.IsTransient(ex, cancellationToken);
                string errClass = FallbackChain.ErrorClassLabel(ex);
                TelemetryEmitter.EmitFallbackHop(
                    _provider, _model, attempt,
                    transient ? "transient_error" : "hard_error",
                    errClass);
                lastError = ex;
                if (!transient) throw;
                if (attempt >= _policy.Retry.MaxAttempts) break;

                var remaining = deadline - _time.GetUtcNow();
                if (remaining <= TimeSpan.Zero)
                {
                    EmitWarn(attempt, "budget-exhausted",
                        elapsedMs: (int)(_time.GetUtcNow() - startUtc).TotalMilliseconds, ex);
                    throw new FallbackBudgetExhaustedException(_policy.WallClockBudget, attempt, ex);
                }
                var delay = _policy.Backoff.ComputeDelay(attempt - 1, _jitter);
                if (delay > remaining) delay = remaining;
                if (delay > TimeSpan.Zero)
                {
                    await _sleep(delay, cancellationToken).ConfigureAwait(false);
                }
                if (_time.GetUtcNow() >= deadline)
                {
                    EmitWarn(attempt, "budget-exhausted",
                        elapsedMs: (int)(_time.GetUtcNow() - startUtc).TotalMilliseconds, ex);
                    throw new FallbackBudgetExhaustedException(_policy.WallClockBudget, attempt, ex);
                }
            }
        }

        EmitWarn(_policy.Retry.MaxAttempts, "exhausted",
            elapsedMs: (int)(_time.GetUtcNow() - startUtc).TotalMilliseconds, lastError);
        throw lastError!;
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var msgList = messages as IReadOnlyList<ChatMessage> ?? new List<ChatMessage>(messages);
        var startUtc = _time.GetUtcNow();
        var deadline = startUtc + _policy.WallClockBudget;
        Exception? lastError = null;

        for (int attempt = 1; attempt <= _policy.Retry.MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool sawFirstToken = false;
            Exception? attemptError = null;

            await foreach (var update in EnumerateSafely(
                _inner, msgList, options, cancellationToken,
                ex => attemptError = ex).ConfigureAwait(false))
            {
                sawFirstToken = true;
                yield return update;
            }

            if (attemptError is null)
            {
                TelemetryEmitter.EmitFallbackHop(_provider, _model, attempt, "success", null);
                if (attempt > 1)
                {
                    EmitWarn(attempt, "ok",
                        elapsedMs: (int)(_time.GetUtcNow() - startUtc).TotalMilliseconds, lastError);
                }
                yield break;
            }

            if (attemptError is OperationCanceledException && cancellationToken.IsCancellationRequested)
            {
                throw attemptError;
            }

            string errClass = FallbackChain.ErrorClassLabel(attemptError);

            // Pre-first-token invariant: once a token has flowed, mid-stream
            // failures are never retried -- the user already saw output and
            // a retry would produce an incoherent transcript.
            if (sawFirstToken)
            {
                TelemetryEmitter.EmitFallbackHop(
                    _provider, _model, attempt, "stream_truncated", errClass);
                throw attemptError;
            }

            bool transient = RetryEnvelope.IsTransient(attemptError, cancellationToken);
            TelemetryEmitter.EmitFallbackHop(
                _provider, _model, attempt,
                transient ? "transient_error" : "hard_error",
                errClass);
            lastError = attemptError;
            if (!transient) throw attemptError;
            if (attempt >= _policy.Retry.MaxAttempts) break;

            var remaining = deadline - _time.GetUtcNow();
            if (remaining <= TimeSpan.Zero)
            {
                EmitWarn(attempt, "budget-exhausted",
                    elapsedMs: (int)(_time.GetUtcNow() - startUtc).TotalMilliseconds, attemptError);
                throw new FallbackBudgetExhaustedException(
                    _policy.WallClockBudget, attempt, attemptError);
            }
            var delay = _policy.Backoff.ComputeDelay(attempt - 1, _jitter);
            if (delay > remaining) delay = remaining;
            if (delay > TimeSpan.Zero)
            {
                await _sleep(delay, cancellationToken).ConfigureAwait(false);
            }
            if (_time.GetUtcNow() >= deadline)
            {
                EmitWarn(attempt, "budget-exhausted",
                    elapsedMs: (int)(_time.GetUtcNow() - startUtc).TotalMilliseconds, attemptError);
                throw new FallbackBudgetExhaustedException(
                    _policy.WallClockBudget, attempt, attemptError);
            }
        }

        EmitWarn(_policy.Retry.MaxAttempts, "exhausted",
            elapsedMs: (int)(_time.GetUtcNow() - startUtc).TotalMilliseconds, lastError);
        throw lastError!;
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
        => _inner.GetService(serviceType, serviceKey);

    public void Dispose() => _inner.Dispose();

    private void EmitWarn(int attempts, string outcome, int elapsedMs, Exception? lastError)
    {
        if (_warn is null) return;
        var errLabel = lastError is null ? "n/a" : FallbackChain.ErrorClassLabel(lastError);
        // ASCII only, NO_COLOR-clean, one line, screen-reader friendly.
        _warn(
            "[WARN] fallback: model=" + _model
            + " attempts=" + attempts.ToString(CultureInfo.InvariantCulture)
            + " outcome=" + outcome
            + " last_error=" + errLabel
            + " elapsed_ms=" + elapsedMs.ToString(CultureInfo.InvariantCulture));
    }

    // Same shape as FallbackChain.EnumerateSafely (S03E22): isolates the
    // try/catch from the yielding side so the outer iterator can yield
    // updates one-by-one AND capture an exception at iterator boundaries.
    private static async IAsyncEnumerable<ChatResponseUpdate> EnumerateSafely(
        IChatClient client,
        IReadOnlyList<ChatMessage> messages,
        ChatOptions? options,
        [EnumeratorCancellation] CancellationToken ct,
        Action<Exception> onError)
    {
        IAsyncEnumerator<ChatResponseUpdate>? enumerator = null;
        try
        {
            try
            {
                enumerator = client.GetStreamingResponseAsync(messages, options, ct).GetAsyncEnumerator(ct);
            }
            catch (Exception ex)
            {
                onError(ex);
                yield break;
            }

            while (true)
            {
                bool moved;
                ChatResponseUpdate current;
                try
                {
                    moved = await enumerator.MoveNextAsync().ConfigureAwait(false);
                    current = moved ? enumerator.Current : default!;
                }
                catch (Exception ex)
                {
                    onError(ex);
                    yield break;
                }
                if (!moved) yield break;
                yield return current;
            }
        }
        finally
        {
            if (enumerator is not null)
            {
                await enumerator.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
