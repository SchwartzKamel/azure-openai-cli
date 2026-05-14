using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace AzureOpenAI_CLI.V2.Tests.Fixtures;

// S04E07 Wave 2 -- Puddy's hermetic chaos mock for the resilience corpus.
//
// Deterministic IChatClient with a per-attempt schedule. NO wall-clock
// reliance: failure timing is driven entirely by the caller-injected
// schedule. NO shared mutable state across tests: every test constructs
// its own ChaosChatClient with its own Calls list.
//
// Each FailureMode in the schedule is consumed for one upstream call --
// unary (GetResponseAsync) or streaming (GetStreamingResponseAsync) --
// in order. If the consumer makes more calls than the schedule has
// entries, the last entry is replayed (so a single-entry schedule
// represents "do this forever").
//
// The Calls list records 0-based attempt indices in dispatch order so
// tests can assert "exactly N upstream calls happened" without
// reaching into the mock's internals.

/// <summary>
/// Deterministic failure schedule entry for <see cref="ChaosChatClient"/>.
/// One entry is consumed per upstream call (unary or streaming).
/// </summary>
public abstract record FailureMode
{
    private FailureMode() { }

    /// <summary>Successful response carrying <paramref name="Text"/>.</summary>
    public sealed record Success(string Text) : FailureMode;

    /// <summary>Throw <see cref="HttpRequestException"/> with the given status code.</summary>
    public sealed record Status(int Code) : FailureMode;

    /// <summary>Throw the supplied exception verbatim.</summary>
    public sealed record Throw(Exception Error) : FailureMode;

    /// <summary>
    /// Streaming-only: yield <paramref name="AfterChars"/> 'x' characters as
    /// a single update, then throw. AfterChars == 0 simulates a pre-first-
    /// token failure (no update yielded). The exception defaults to a
    /// synthetic HTTP 503 if not supplied. On the unary path this entry
    /// behaves as a Status(503) failure.
    /// </summary>
    public sealed record StreamTruncate(int AfterChars, Exception? Error = null) : FailureMode;

    /// <summary>
    /// Synthetic timeout. Throws <see cref="TaskCanceledException"/>
    /// without any wall-clock delay -- the brief's transient classifier
    /// treats this as the closed-set HTTP-timeout signal.
    /// </summary>
    public sealed record Hang(int Ms) : FailureMode;
}

/// <summary>
/// Hermetic <see cref="IChatClient"/> driven by a deterministic schedule of
/// <see cref="FailureMode"/> entries. One entry per upstream call. The
/// <see cref="Calls"/> list records the 0-based index of each call so tests
/// can assert exact retry counts without inspecting mock internals.
/// </summary>
public sealed class ChaosChatClient : IChatClient
{
    private readonly IReadOnlyList<FailureMode> _schedule;
    private int _idx;

    /// <summary>0-based indices of each upstream call, in dispatch order.</summary>
    public List<int> Calls { get; } = new();

    public ChaosChatClient(params FailureMode[] schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        if (schedule.Length == 0) throw new ArgumentException("schedule must not be empty", nameof(schedule));
        _schedule = schedule;
    }

    private FailureMode Next()
    {
        int idx = _idx;
        Calls.Add(idx);
        var entry = idx < _schedule.Count ? _schedule[idx] : _schedule[^1];
        _idx++;
        return entry;
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var entry = Next();
        return entry switch
        {
            FailureMode.Success s
                => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, s.Text))),
            FailureMode.Status st
                => Task.FromException<ChatResponse>(MakeHttp(st.Code)),
            FailureMode.Throw t
                => Task.FromException<ChatResponse>(t.Error),
            FailureMode.StreamTruncate trunc
                // W2 corpus: on the unary path, StreamTruncate degenerates to
                // its supplied error (or synthetic 503). The afterChars
                // payload only matters on the streaming path.
                => Task.FromException<ChatResponse>(trunc.Error ?? MakeHttp(503)),
            FailureMode.Hang
                => Task.FromException<ChatResponse>(new TaskCanceledException("synthetic timeout")),
            _ => Task.FromException<ChatResponse>(new InvalidOperationException("unknown FailureMode")),
        };
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var entry = Next();
        switch (entry)
        {
            case FailureMode.Success s:
                yield return new ChatResponseUpdate(ChatRole.Assistant, s.Text);
                yield break;
            case FailureMode.Status st:
                await Task.Yield();
                throw MakeHttp(st.Code);
            case FailureMode.Throw t:
                await Task.Yield();
                throw t.Error;
            case FailureMode.StreamTruncate trunc:
                if (trunc.AfterChars > 0)
                {
                    yield return new ChatResponseUpdate(
                        ChatRole.Assistant, new string('x', trunc.AfterChars));
                }
                await Task.Yield();
                throw trunc.Error ?? MakeHttp(503);
            case FailureMode.Hang:
                await Task.Yield();
                throw new TaskCanceledException("synthetic timeout");
            default:
                throw new InvalidOperationException("unknown FailureMode");
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }

    private static HttpRequestException MakeHttp(int code)
        => new(message: "synthetic " + code, inner: null, statusCode: (HttpStatusCode)code);
}
