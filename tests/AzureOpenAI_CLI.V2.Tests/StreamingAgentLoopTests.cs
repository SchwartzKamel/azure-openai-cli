using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// FR-011 parity: streaming text from the MAF agent must reach stdout in
/// <em>every</em> round — including pre-tool-call preambles — not only on the
/// final round. v1 shipped the fix in commit ea7ce8c; v2's agent loop writes
/// <c>update.Text</c> unconditionally in <c>Program.RunAsync</c>, so this
/// regression test pins that behavior with a <see cref="FakeChatClient"/> that
/// emits multiple text chunks across multiple "rounds" (simulated by staggered
/// yields) and asserts every chunk was written. Also guards the streaming
/// property: chunks must arrive progressively, not only at end-of-stream.
/// </summary>
[Collection("ConsoleCapture")]
public class StreamingAgentLoopTests
{
    // Staggered fake that yields text updates with an artificial delay between
    // them so we can observe the progressive-write property via a custom
    // TextWriter that snapshots its buffer at each yield point.
    private sealed class StaggeredFakeClient : IChatClient
    {
        private readonly string[] _chunks;
        private readonly TimeSpan _delay;
        public StaggeredFakeClient(string[] chunks, TimeSpan delay)
        {
            _chunks = chunks;
            _delay = delay;
        }

        public void Dispose() { }
        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, string.Concat(_chunks))));

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var c in _chunks)
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, c);
                if (_delay > TimeSpan.Zero)
                    await Task.Delay(_delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>TextWriter that records a snapshot after each Write call — used
    /// to prove text arrived progressively, not only at stream end.</summary>
    private sealed class SnapshotWriter : StringWriter
    {
        public List<string> Snapshots { get; } = new();
        public override void Write(string? value)
        {
            base.Write(value);
            Snapshots.Add(ToString());
        }
        public override void Write(char value)
        {
            base.Write(value);
            Snapshots.Add(ToString());
        }
    }

    [Fact]
    public async Task AgentStreaming_EmitsEveryChunk_InOrder()
    {
        // Three separate text chunks — simulates preamble + tool-roundtext + final.
        var chunks = new[] { "pre-", "mid-", "final" };
        var fake = new StaggeredFakeClient(chunks, TimeSpan.Zero);
        var agent = fake.AsAIAgent(instructions: "sys");

        var sb = new System.Text.StringBuilder();
        await foreach (var update in agent.RunStreamingAsync("go"))
        {
            if (!string.IsNullOrEmpty(update.Text))
                sb.Append(update.Text);
        }

        Assert.Equal("pre-mid-final", sb.ToString());
    }

    [Fact]
    public async Task AgentStreaming_ChunksArriveProgressively_NotBuffered()
    {
        // With a 50ms delay between chunks, a progressive consumer must see
        // intermediate snapshots where only the first chunk is present.
        var chunks = new[] { "AAA", "BBB", "CCC" };
        var fake = new StaggeredFakeClient(chunks, TimeSpan.FromMilliseconds(50));
        var agent = fake.AsAIAgent(instructions: "sys");

        var snapshots = new List<string>();
        await foreach (var update in agent.RunStreamingAsync("go"))
        {
            if (!string.IsNullOrEmpty(update.Text))
                snapshots.Add(update.Text);
        }

        // Three distinct updates observed — not one concatenated buffer.
        Assert.Equal(3, snapshots.Count);
        Assert.Equal("AAA", snapshots[0]);
        Assert.Equal("BBB", snapshots[1]);
        Assert.Equal("CCC", snapshots[2]);
    }

    [Fact]
    public async Task AgentStreaming_Stdout_ReceivesAllChunksAcrossRounds()
    {
        // Exercises the exact loop shape used by Program.RunAsync: for each
        // update with non-empty Text, write to stdout. Mirrors the FR-011 v1
        // regression test — pins that text from every round reaches stdout,
        // not only the final round.
        var chunks = new[] { "[round1-preamble]", "[round2-postamble]", "[round3-final]" };
        var fake = new StaggeredFakeClient(chunks, TimeSpan.Zero);
        var agent = fake.AsAIAgent(instructions: "sys");

        var snap = new SnapshotWriter();
        var oldOut = Console.Out;
        Console.SetOut(snap);
        try
        {
            await foreach (var update in agent.RunStreamingAsync("go"))
            {
                if (!string.IsNullOrEmpty(update.Text))
                    Console.Out.Write(update.Text);
            }
        }
        finally
        {
            Console.SetOut(oldOut);
        }

        var final = snap.ToString();
        Assert.Contains("[round1-preamble]", final);
        Assert.Contains("[round2-postamble]", final);
        Assert.Contains("[round3-final]", final);

        // Snapshots confirm progressive writes: at least one intermediate
        // state contains the first chunk but not the last.
        Assert.Contains(snap.Snapshots, s => s.Contains("[round1-preamble]") && !s.Contains("[round3-final]"));
    }

    [Fact]
    public async Task AgentStreaming_EmptyTextUpdates_NotWritten()
    {
        // Puddy: prove the negative path too — empty Text must never reach stdout.
        var fake = new StaggeredFakeClient(new[] { "", "real", "" }, TimeSpan.Zero);
        var agent = fake.AsAIAgent(instructions: "sys");

        var sw = new StringWriter();
        var oldOut = Console.Out;
        Console.SetOut(sw);
        try
        {
            await foreach (var update in agent.RunStreamingAsync("go"))
            {
                if (!string.IsNullOrEmpty(update.Text))
                    Console.Out.Write(update.Text);
            }
        }
        finally { Console.SetOut(oldOut); }

        Assert.Equal("real", sw.ToString());
    }
}
