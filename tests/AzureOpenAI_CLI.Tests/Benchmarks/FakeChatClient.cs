using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace AzureOpenAI_CLI.Tests.Benchmarks;

/// <summary>
/// S03E12 -- The Receipt. Deterministic in-memory <see cref="IChatClient"/>
/// for benchmark + perf-gate tests. NO network, NO SDK construction, NO file
/// I/O. Latency is a knob: callers pass an artificial <see cref="TimeSpan"/>
/// per call and the fake honours it via <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.
/// Token-count emission is also a knob -- the canned reply is repeated to a
/// configurable token approximation so harness consumers can drive realistic
/// payload sizes without touching a real model.
///
/// Bania's rule: a baseline you cannot reproduce is not a baseline. The fake
/// is the reproducibility layer -- it removes every source of variance the
/// benchmark is not measuring.
/// </summary>
internal sealed class FakeChatClient : IChatClient
{
    private readonly TimeSpan _firstTokenLatency;
    private readonly TimeSpan _perTokenLatency;
    private readonly int _tokenCount;
    private readonly string _tokenWord;
    private int _callCount;

    /// <summary>Number of times the client has been invoked since construction.</summary>
    public int CallCount => Volatile.Read(ref _callCount);

    /// <summary>
    /// Construct a deterministic fake.
    /// </summary>
    /// <param name="firstTokenLatency">Delay before the first token / non-streaming response.</param>
    /// <param name="perTokenLatency">Per-token delay for streaming responses (ignored for non-streaming).</param>
    /// <param name="tokenCount">Number of tokens (whitespace-separated words) the fake emits per response.</param>
    /// <param name="tokenWord">The repeated word used for each emitted token. Defaults to a short ASCII filler.</param>
    public FakeChatClient(
        TimeSpan firstTokenLatency = default,
        TimeSpan perTokenLatency = default,
        int tokenCount = 1,
        string tokenWord = "ok")
    {
        if (tokenCount < 0)
            throw new ArgumentOutOfRangeException(nameof(tokenCount), "Token count must be >= 0.");
        if (string.IsNullOrEmpty(tokenWord))
            throw new ArgumentException("Token word must be non-empty.", nameof(tokenWord));
        _firstTokenLatency = firstTokenLatency;
        _perTokenLatency = perTokenLatency;
        _tokenCount = tokenCount;
        _tokenWord = tokenWord;
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _callCount);
        if (_firstTokenLatency > TimeSpan.Zero)
            await Task.Delay(_firstTokenLatency, cancellationToken).ConfigureAwait(false);
        var reply = BuildReply();
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, reply));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _callCount);
        if (_firstTokenLatency > TimeSpan.Zero)
            await Task.Delay(_firstTokenLatency, cancellationToken).ConfigureAwait(false);
        for (int i = 0; i < _tokenCount; i++)
        {
            if (i > 0 && _perTokenLatency > TimeSpan.Zero)
                await Task.Delay(_perTokenLatency, cancellationToken).ConfigureAwait(false);
            yield return new ChatResponseUpdate(ChatRole.Assistant, _tokenWord);
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }

    private string BuildReply()
    {
        if (_tokenCount <= 0) return string.Empty;
        if (_tokenCount == 1) return _tokenWord;
        // Whitespace-joined repetition; cheap, deterministic, ASCII.
        return string.Join(' ', Enumerable.Repeat(_tokenWord, _tokenCount));
    }
}
