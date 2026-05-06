using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AzureOpenAI_CLI.Capabilities;
using AzureOpenAI_CLI.Observability;
using Microsoft.Extensions.AI;

namespace AzureOpenAI_CLI.Resilience;

// S03E22 -- The Fallback chain executor. Wraps an IChatClient with a
// best-effort retry-against-alternates policy. Frank Costanza's rules:
//
//   * Default off. The wrap is only invoked when FallbackPolicy.IsActive.
//   * Transient = retry the chain. Hard = fail with the original error.
//     A 401 is not transient. A 4xx other than 429 is not transient. A
//     CapabilityMismatchException on the primary is not transient.
//   * Stream-mode invariant: if the primary fails BEFORE the first chunk
//     is yielded, fallback. If it fails AFTER, the user already saw output;
//     emit "stream-truncated" on stderr and propagate. Re-rolling a stream
//     in-flight is a worse outcome than a clean truncation.
//   * Each alternate independently passes through (a) the capability gate
//     (e18) and (b) the endpoint allowlist via the factory contract.
//     A capability-gated or offline-blocked alternate is SKIPPED, not an
//     error -- the chain continues down the list.
//   * One stderr WARN line per fallback hop (silent under --raw); one
//     telemetry attempt event per attempt + one outcome event at the end.

/// <summary>
/// Per-alternate construction outcome from an <see cref="AlternateChatClientFactory"/>.
/// <see cref="Client"/> non-null means the alternate is usable;
/// <see cref="SkipReason"/> non-null means the alternate was deliberately
/// skipped (no creds, blocked offline, capability mismatch). Both null is
/// invalid -- treat it as <see cref="SkipReason"/> = "unavailable".
/// </summary>
public readonly record struct AlternateBuildResult(IChatClient? Client, string? SkipReason)
{
    /// <summary>Convenience: alternate is ready to dispatch.</summary>
    public static AlternateBuildResult Ready(IChatClient c) => new(c, null);

    /// <summary>Convenience: alternate skipped with the given short reason.</summary>
    public static AlternateBuildResult Skipped(string reason) => new(null, reason);
}

/// <summary>
/// Factory delegate that materializes an alternate chat client for a given
/// preset + model. MUST return <see cref="AlternateBuildResult.Skipped"/>
/// (never throw) when the alternate cannot be built (missing creds, offline
/// gate, capability mismatch). Throwing is reserved for genuinely unexpected
/// errors and will short-circuit the chain.
/// </summary>
public delegate AlternateBuildResult AlternateChatClientFactory(string preset, string model);

/// <summary>
/// Coarse classification of an exception thrown by an inner chat client.
/// Drives the fallback decision: <see cref="Transient"/> retries the chain;
/// every other class fails fast with the original error.
/// </summary>
internal enum FallbackClass
{
    /// <summary>5xx, network, timeout, 429. Eligible for fallback.</summary>
    Transient,
    /// <summary>401/403. Hard failure -- no fallback.</summary>
    Auth,
    /// <summary>4xx other than 429. Hard failure -- no fallback.</summary>
    ClientError,
    /// <summary>CapabilityMismatchException on the primary. Hard failure -- no fallback.</summary>
    Capability,
    /// <summary>OperationCanceledException with a triggered token. Propagate.</summary>
    UserCancelled,
}

/// <summary>
/// Static entry point for wrapping an <see cref="IChatClient"/> with a
/// fallback chain. See top-of-file commentary for the policy this enforces.
/// </summary>
public static class FallbackChain
{
    /// <summary>
    /// Wrap <paramref name="primary"/> with the fallback chain described by
    /// <paramref name="policy"/>. Returns <paramref name="primary"/>
    /// unchanged when the policy is empty -- zero allocation, zero overhead
    /// for users who never opted in.
    /// </summary>
    /// <param name="primary">The primary chat client (already constructed).</param>
    /// <param name="primaryProvider">Provider name for telemetry / warn lines (e.g. "azure").</param>
    /// <param name="model">Model alias / deployment name -- threaded through to alternates.</param>
    /// <param name="policy">Resolved fallback policy. <see cref="FallbackPolicy.None"/> is a no-op.</param>
    /// <param name="factory">Builder for alternate clients. See <see cref="AlternateChatClientFactory"/>.</param>
    /// <param name="warnSink">Stderr writer for hop warnings. Null suppresses warns (e.g. under --raw).</param>
    /// <param name="isRaw">True when the caller is in --raw mode; suppresses the post-truncation hint.</param>
    public static IChatClient Wrap(
        IChatClient primary,
        string primaryProvider,
        string model,
        FallbackPolicy policy,
        AlternateChatClientFactory factory,
        Action<string>? warnSink,
        bool isRaw)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(factory);
        if (!policy.IsActive) return primary;
        return new FallbackChatClient(primary, primaryProvider, model, policy, factory, warnSink, isRaw);
    }

    /// <summary>
    /// Classify an exception as <see cref="FallbackClass.Transient"/> (chain
    /// continues), <see cref="FallbackClass.Auth"/> /
    /// <see cref="FallbackClass.ClientError"/> /
    /// <see cref="FallbackClass.Capability"/> (chain stops, original error
    /// surfaces), or <see cref="FallbackClass.UserCancelled"/> (propagate).
    /// </summary>
    internal static FallbackClass Classify(Exception ex, CancellationToken ct)
    {
        if (ex is OperationCanceledException && ct.IsCancellationRequested)
        {
            return FallbackClass.UserCancelled;
        }
        if (ex is CapabilityMismatchException)
        {
            return FallbackClass.Capability;
        }
        if (ex is TaskCanceledException)
        {
            // Timeout in the inner client -- treat as transient.
            return FallbackClass.Transient;
        }
        if (ex is HttpRequestException http && http.StatusCode is HttpStatusCode code)
        {
            int n = (int)code;
            if (n == 401 || n == 403) return FallbackClass.Auth;
            if (n == 429) return FallbackClass.Transient;
            if (n >= 400 && n < 500) return FallbackClass.ClientError;
            if (n >= 500) return FallbackClass.Transient;
            return FallbackClass.Transient;
        }
        // Network-class faults -- DNS, RST, refused -- are transient.
        if (ex is HttpRequestException || ex is SocketException || ex is IOException)
        {
            return FallbackClass.Transient;
        }
        // Default: be conservative and treat as transient. Better an extra
        // hop than a missed retry on something the user explicitly opted in
        // to retry on. The hard-failure cases above are the named exceptions.
        return FallbackClass.Transient;
    }

    /// <summary>
    /// Stable, low-cardinality error_class label for telemetry. Maps the
    /// concrete exception type to a short ASCII token; SecretRedactor on the
    /// emit path scrubs the message body separately. No message tail.
    /// </summary>
    internal static string ErrorClassLabel(Exception ex)
    {
        if (ex is CapabilityMismatchException) return CapabilityMismatchException.ErrorClass;
        if (ex is HttpRequestException http && http.StatusCode is HttpStatusCode code)
        {
            int n = (int)code;
            if (n == 401 || n == 403) return "AuthFailure";
            if (n == 429) return "RateLimited";
            if (n >= 400 && n < 500) return "ClientError";
            if (n >= 500) return "ServerError";
        }
        if (ex is TaskCanceledException) return "Timeout";
        if (ex is OperationCanceledException) return "Cancelled";
        if (ex is HttpRequestException) return "NetworkError";
        if (ex is SocketException) return "SocketError";
        if (ex is IOException) return "IOError";
        return ex.GetType().Name;
    }

    // ── Wrapped IChatClient implementation ───────────────────────────────

    private sealed class FallbackChatClient : IChatClient
    {
        private readonly IChatClient _primary;
        private readonly string _primaryProvider;
        private readonly string _model;
        private readonly FallbackPolicy _policy;
        private readonly AlternateChatClientFactory _factory;
        private readonly Action<string>? _warn;
        private readonly bool _isRaw;

        public FallbackChatClient(
            IChatClient primary,
            string primaryProvider,
            string model,
            FallbackPolicy policy,
            AlternateChatClientFactory factory,
            Action<string>? warnSink,
            bool isRaw)
        {
            _primary = primary;
            _primaryProvider = primaryProvider;
            _model = model;
            _policy = policy;
            _factory = factory;
            _warn = warnSink;
            _isRaw = isRaw;
        }

        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            // Materialize once -- alternates will need the same prompt.
            var msgList = MaterializeMessages(messages);

            Exception? originalError = null;
            int hop = 0;
            try
            {
                EmitAttempt(hop, _primaryProvider, "begin", null);
                var resp = await _primary.GetResponseAsync(msgList, options, cancellationToken)
                    .ConfigureAwait(false);
                EmitOutcome(hop, _primaryProvider, "success", null);
                return resp;
            }
            catch (Exception ex)
            {
                var cls = Classify(ex, cancellationToken);
                EmitAttempt(hop, _primaryProvider, OutcomeForClass(cls), ErrorClassLabel(ex));
                if (cls == FallbackClass.UserCancelled) throw;
                if (cls != FallbackClass.Transient)
                {
                    EmitOutcome(hop, _primaryProvider, OutcomeForClass(cls), ErrorClassLabel(ex));
                    throw;
                }
                originalError = ex;
            }

            // Walk the chain.
            for (int i = 0; i < _policy.Providers.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                hop = i + 1;
                var altPreset = _policy.Providers[i];

                if (!TryBuildAlternate(altPreset, hop, out var alt, out var skipReason))
                {
                    // Skipped -- continue without counting as an attempt.
                    continue;
                }

                using (alt)
                {
                    Warn($"[fallback] hop {hop}/{_policy.Providers.Count}: trying {altPreset} ({_model})");
                    try
                    {
                        EmitAttempt(hop, altPreset, "begin", null);
                        var resp = await alt.GetResponseAsync(msgList, options, cancellationToken)
                            .ConfigureAwait(false);
                        EmitOutcome(hop, altPreset, "success", null);
                        return resp;
                    }
                    catch (Exception ex)
                    {
                        var cls = Classify(ex, cancellationToken);
                        EmitAttempt(hop, altPreset, OutcomeForClass(cls), ErrorClassLabel(ex));
                        if (cls == FallbackClass.UserCancelled) throw;
                        // Per brief: a hard failure on an *alternate* still
                        // surfaces the ORIGINAL primary error if every option
                        // is exhausted. We continue down the chain regardless
                        // of class -- the chain is best-effort.
                        if (i == _policy.Providers.Count - 1)
                        {
                            EmitOutcome(hop, altPreset, OutcomeForClass(cls), ErrorClassLabel(ex));
                        }
                    }
                }
            }

            // All hops exhausted -- surface the ORIGINAL primary error per
            // brief. The user opted into a chain, not a swap; the original
            // signal is what actionable diagnosis hangs off of.
            if (originalError is not null)
            {
                throw new FallbackChainExhaustedException(_policy.Providers.Count, originalError);
            }
            // Should be unreachable -- primary either succeeded (returned) or
            // threw (originalError set).
            throw new InvalidOperationException("FallbackChain reached an impossible terminal state.");
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var msgList = MaterializeMessages(messages);

            // ── Primary attempt ───────────────────────────────────────────
            // We must distinguish "failed before first chunk" from "failed
            // after first chunk". The IAsyncEnumerable contract gives us the
            // hook only at the iterator level; we pump it manually.
            int hop = 0;
            EmitAttempt(hop, _primaryProvider, "begin", null);

            bool sawFirstChunk = false;
            Exception? primaryError = null;

            await foreach (var update in EnumerateSafely(
                _primary, msgList, options, cancellationToken,
                ex => primaryError = ex).ConfigureAwait(false))
            {
                sawFirstChunk = true;
                yield return update;
            }

            if (primaryError is null)
            {
                EmitOutcome(hop, _primaryProvider, "success", null);
                yield break;
            }

            var primaryClass = Classify(primaryError, cancellationToken);
            EmitAttempt(hop, _primaryProvider, OutcomeForClass(primaryClass), ErrorClassLabel(primaryError));

            if (primaryClass == FallbackClass.UserCancelled)
            {
                throw primaryError;
            }

            // Critical correctness invariant from the brief:
            //   * Pre-first-chunk failure -> fallback.
            //   * Post-first-chunk failure -> NO fallback. The user already
            //     saw output; restarting from a different provider would
            //     produce an incoherent transcript.
            if (sawFirstChunk)
            {
                Warn("[fallback] stream-truncated: primary failed after first chunk; "
                    + "no fallback (user already saw output). Original error: "
                    + ErrorClassLabel(primaryError));
                EmitOutcome(hop, _primaryProvider, "stream_truncated", ErrorClassLabel(primaryError));
                throw primaryError;
            }

            if (primaryClass != FallbackClass.Transient)
            {
                EmitOutcome(hop, _primaryProvider, OutcomeForClass(primaryClass), ErrorClassLabel(primaryError));
                throw primaryError;
            }

            // ── Walk the chain ────────────────────────────────────────────
            for (int i = 0; i < _policy.Providers.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                hop = i + 1;
                var altPreset = _policy.Providers[i];

                if (!TryBuildAlternate(altPreset, hop, out var alt, out _))
                {
                    continue;
                }

                Warn($"[fallback] hop {hop}/{_policy.Providers.Count}: streaming from {altPreset} ({_model})");
                EmitAttempt(hop, altPreset, "begin", null);

                bool altSawChunk = false;
                Exception? altError = null;
                using (alt)
                {
                    await foreach (var update in EnumerateSafely(
                        alt, msgList, options, cancellationToken,
                        ex => altError = ex).ConfigureAwait(false))
                    {
                        altSawChunk = true;
                        yield return update;
                    }
                }

                if (altError is null)
                {
                    EmitOutcome(hop, altPreset, "success", null);
                    yield break;
                }

                var altClass = Classify(altError, cancellationToken);
                EmitAttempt(hop, altPreset, OutcomeForClass(altClass), ErrorClassLabel(altError));

                if (altClass == FallbackClass.UserCancelled)
                {
                    throw altError;
                }

                if (altSawChunk)
                {
                    Warn("[fallback] stream-truncated on " + altPreset
                        + "; cannot retry mid-stream. Original error: " + ErrorClassLabel(primaryError));
                    EmitOutcome(hop, altPreset, "stream_truncated", ErrorClassLabel(altError));
                    throw altError;
                }

                if (i == _policy.Providers.Count - 1)
                {
                    EmitOutcome(hop, altPreset, OutcomeForClass(altClass), ErrorClassLabel(altError));
                }
                // Otherwise continue to next alternate.
            }

            // All alternates failed pre-stream or were skipped. Surface the
            // ORIGINAL primary error.
            throw new FallbackChainExhaustedException(_policy.Providers.Count, primaryError);
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
            => _primary.GetService(serviceType, serviceKey);

        public void Dispose() => _primary.Dispose();

        // Internal helpers ──────────────────────────────────────────────────

        private bool TryBuildAlternate(
            string preset, int hop,
            out IChatClient client, out string? skipReason)
        {
            AlternateBuildResult result;
            try
            {
                result = _factory(preset, _model);
            }
            catch (Exception ex)
            {
                // Factory threw something unexpected -- treat as a skipped
                // alternate, do not crash the chain.
                Warn($"[fallback] hop {hop}: factory error for {preset}: {ErrorClassLabel(ex)}; skipping");
                EmitAttempt(hop, preset, "skipped", "FactoryError");
                client = null!;
                skipReason = "FactoryError";
                return false;
            }

            if (result.Client is null)
            {
                var reason = result.SkipReason ?? "unavailable";
                Warn($"[fallback] hop {hop}: skipping {preset} -- {reason}");
                EmitAttempt(hop, preset, "skipped", reason);
                client = null!;
                skipReason = reason;
                return false;
            }

            client = result.Client;
            skipReason = null;
            return true;
        }

        private void Warn(string line)
        {
            if (_warn is null) return;
            _warn(line);
        }

        private void EmitAttempt(int hop, string provider, string outcome, string? errorClass)
            => TelemetryEmitter.EmitFallbackAttempt(
                _primaryProvider, hop, provider, outcome, errorClass, message: null);

        private void EmitOutcome(int hop, string provider, string finalOutcome, string? errorClass)
            => TelemetryEmitter.EmitFallbackOutcome(
                _primaryProvider, _policy.Source, _policy.Providers,
                finalOutcome, winner: provider, attempts: hop, errorClass);

        private static IReadOnlyList<ChatMessage> MaterializeMessages(IEnumerable<ChatMessage> src)
            => src as IReadOnlyList<ChatMessage> ?? new List<ChatMessage>(src);

        private static string OutcomeForClass(FallbackClass cls) => cls switch
        {
            FallbackClass.Transient => "transient_error",
            FallbackClass.Auth => "auth_error",
            FallbackClass.ClientError => "client_error",
            FallbackClass.Capability => "capability_error",
            FallbackClass.UserCancelled => "cancelled",
            _ => "unknown_error",
        };

        // Wraps an IChatClient streaming call so that the outer iterator can
        // yield updates one-by-one AND capture an exception at iterator
        // boundaries. C# does not allow `yield return` inside try/catch with
        // a catch-all that wraps the consumer; this helper isolates the
        // try/catch from the yielding side.
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
}

/// <summary>
/// Thrown when every provider in the fallback chain has been tried (or
/// skipped) and none produced a response. Carries the ORIGINAL primary
/// exception in <see cref="Exception.InnerException"/> so the caller's
/// existing error-classification path (telemetry, --json envelope) sees the
/// signal that mattered, not the last alternate's noise.
/// </summary>
public sealed class FallbackChainExhaustedException : Exception
{
    /// <summary>Stable telemetry error_class identifier.</summary>
    public const string ErrorClass = "FallbackChainExhausted";

    /// <summary>Number of provider entries the chain was configured with.</summary>
    public int ChainDepth { get; }

    public FallbackChainExhaustedException(int chainDepth, Exception primaryError)
        : base($"Fallback chain exhausted after {chainDepth} alternate(s); "
              + "surfacing the original primary error.", primaryError)
    {
        ChainDepth = chainDepth;
    }
}
