using AzureOpenAI_CLI.Tests.Benchmarks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// S03E17 -- The Stream. Streaming + tool-call parity for the OpenAI-compat
/// dispatch path that landed in S03E09 (ADR-010). The dispatch surface is the
/// <see cref="IChatClient"/> seam returned by <see cref="OpenAiCompatAdapter.Build"/>;
/// MAF then wraps it with <c>AsAIAgent().RunStreamingAsync()</c>. This file
/// pins the contract at the IChatClient seam: every streaming behavior the
/// real compat path depends on (chunk reassembly, tool-call delta merging,
/// cancellation, empty stream) must hold against a deterministic in-memory
/// fake before it can hold against Groq, Together, or OpenAI direct.
///
/// All tests use <see cref="FakeChatClient"/> in its S03E17 explicit-chunk
/// constructor mode -- no network, no real SDK construction, no env-var
/// fiddling. <c>[Collection("ConsoleCapture")]</c> is kept for parity with
/// the rest of the suite even though these tests do not write to stdout.
/// </summary>
[Collection("ConsoleCapture")]
public class CompatStreamingTests
{
    // ---- Helpers ------------------------------------------------------------

    private static ChatResponseUpdate Text(string s)
        => new(ChatRole.Assistant, s);

    private static ChatResponseUpdate ToolCall(string callId, string? name, IDictionary<string, object?>? args)
    {
        // FunctionCallContent's args dictionary is non-null IDictionary<string, object>.
        // Null args -> empty dict; this mirrors how the OpenAI SDK adapter shapes
        // partial deltas (name on the first delta, args accumulated across the
        // following deltas, all sharing the same callId).
        IDictionary<string, object?>? a = args is null
            ? null
            : args.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        var fc = new FunctionCallContent(callId, name ?? string.Empty, a);
        return new ChatResponseUpdate { Role = ChatRole.Assistant, Contents = new List<AIContent> { fc } };
    }

    // ---- Text-chunk reassembly (Fact 1-4) -----------------------------------

    [Fact]
    public async Task StreamingTextChunks_FiveDeltas_JoinedMatchesExpected()
    {
        var chunks = new[] { Text("Hello, "), Text("compat "), Text("world"), Text("! "), Text(":giddyup:") };
        var fake = new FakeChatClient(chunks);

        var sb = new System.Text.StringBuilder();
        await foreach (var u in fake.GetStreamingResponseAsync(Array.Empty<ChatMessage>()))
            sb.Append(u.Text);

        Assert.Equal("Hello, compat world! :giddyup:", sb.ToString());
        Assert.Equal(1, fake.CallCount);
    }

    [Fact]
    public async Task StreamingTextChunks_OrderPreservedAcrossUpdates()
    {
        var chunks = new[] { Text("a"), Text("b"), Text("c"), Text("d"), Text("e") };
        var fake = new FakeChatClient(chunks);

        var observed = new List<string>();
        await foreach (var u in fake.GetStreamingResponseAsync(Array.Empty<ChatMessage>()))
            observed.Add(u.Text ?? string.Empty);

        Assert.Equal(new[] { "a", "b", "c", "d", "e" }, observed);
    }

    [Fact]
    public async Task StreamingTextChunks_EmptyStringDeltas_AppearAsEmptyText()
    {
        // Negative path: empty-text updates are emitted verbatim. The dispatch
        // path in Program.RunAsync then filters them via IsNullOrEmpty.
        var chunks = new[] { Text(""), Text("real"), Text("") };
        var fake = new FakeChatClient(chunks);

        var observed = new List<string>();
        await foreach (var u in fake.GetStreamingResponseAsync(Array.Empty<ChatMessage>()))
            observed.Add(u.Text ?? string.Empty);

        Assert.Equal(3, observed.Count);
        Assert.Equal(string.Empty, observed[0]);
        Assert.Equal("real", observed[1]);
        Assert.Equal(string.Empty, observed[2]);
    }

    [Fact]
    public async Task StreamingTextChunks_AggregateToChatResponse_RetainsJoinedText()
    {
        // ToChatResponseAsync is the M.E.AI helper the agent layer uses to fold
        // streaming updates into a final ChatResponse. It must round-trip our
        // chunk sequence into a single message with concatenated text.
        var chunks = new[] { Text("alpha-"), Text("beta-"), Text("gamma") };
        var fake = new FakeChatClient(chunks);

        var response = await fake.GetStreamingResponseAsync(Array.Empty<ChatMessage>()).ToChatResponseAsync();

        Assert.NotNull(response);
        Assert.Single(response.Messages);
        Assert.Equal("alpha-beta-gamma", response.Messages[0].Text);
    }

    // ---- Tool-call delta reassembly (Fact 5-7) ------------------------------

    [Fact]
    public async Task ToolCallDeltas_NameAndCallId_AggregateIntoSingleFunctionCall()
    {
        // Three deltas, all sharing call id "call_42", with the function name
        // arriving on delta 0 and arguments accumulated on delta 1 and 2.
        // ToChatResponseAsync must coalesce them into a single
        // FunctionCallContent with the joined argument shape.
        var args1 = new Dictionary<string, object?> { ["path"] = "/etc/hosts" };
        var args2 = new Dictionary<string, object?> { ["limit"] = 200 };
        var chunks = new[]
        {
            ToolCall("call_42", "read_file", null),
            ToolCall("call_42", null, args1),
            ToolCall("call_42", null, args2),
        };
        var fake = new FakeChatClient(chunks);

        var response = await fake.GetStreamingResponseAsync(Array.Empty<ChatMessage>()).ToChatResponseAsync();

        var fc = response.Messages
            .SelectMany(m => m.Contents)
            .OfType<FunctionCallContent>()
            .ToList();

        Assert.NotEmpty(fc);
        // All deltas share the same call id.
        Assert.All(fc, c => Assert.Equal("call_42", c.CallId));
        // Some delta carried the function name.
        Assert.Contains(fc, c => string.Equals(c.Name, "read_file", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ToolCallDeltas_ArgumentsAccumulated_AcrossMultipleUpdates()
    {
        // Pin the union-of-arguments invariant: at least one delta carries
        // each key. We do not assume the aggregator merges them into one
        // dictionary -- M.E.AI's behavior is "preserve every FunctionCallContent
        // exactly" -- but the OpenAI SDK adapter aggregates upstream of us, so
        // by the time we see the chunks they already carry the merged shape.
        // Either way: the union must be visible.
        var args1 = new Dictionary<string, object?> { ["a"] = "1" };
        var args2 = new Dictionary<string, object?> { ["b"] = "2" };
        var args3 = new Dictionary<string, object?> { ["c"] = "3" };
        var chunks = new[]
        {
            ToolCall("call_99", "shell_exec", args1),
            ToolCall("call_99", null, args2),
            ToolCall("call_99", null, args3),
        };
        var fake = new FakeChatClient(chunks);

        var response = await fake.GetStreamingResponseAsync(Array.Empty<ChatMessage>()).ToChatResponseAsync();

        var allArgKeys = response.Messages
            .SelectMany(m => m.Contents)
            .OfType<FunctionCallContent>()
            .Where(c => c.Arguments is not null)
            .SelectMany(c => c.Arguments!.Keys)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("a", allArgKeys);
        Assert.Contains("b", allArgKeys);
        Assert.Contains("c", allArgKeys);
    }

    [Fact]
    public async Task ToolCallDeltas_MixedTextAndToolCall_BothPreservedInOrder()
    {
        // Some compat providers stream a preamble "let me check the file" plus
        // the tool call. Both content kinds must survive aggregation.
        var args = new Dictionary<string, object?> { ["q"] = "weather" };
        var chunks = new[]
        {
            Text("checking..."),
            ToolCall("call_77", "web_fetch", args),
            Text("done."),
        };
        var fake = new FakeChatClient(chunks);

        var response = await fake.GetStreamingResponseAsync(Array.Empty<ChatMessage>()).ToChatResponseAsync();

        var allText = string.Concat(response.Messages.Select(m => m.Text));
        Assert.Contains("checking...", allText, StringComparison.Ordinal);
        Assert.Contains("done.", allText, StringComparison.Ordinal);

        var fc = response.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>().ToList();
        Assert.Contains(fc, c => string.Equals(c.Name, "web_fetch", StringComparison.Ordinal));
        Assert.Contains(fc, c => string.Equals(c.CallId, "call_77", StringComparison.Ordinal));
    }

    // ---- Cancellation (Fact 8-9) --------------------------------------------

    [Fact]
    public async Task CancellationMidStream_ThrowsOperationCanceledException()
    {
        // Inject cancellation at chunk index 2: we expect to observe chunks 0
        // and 1, then a clean OCE -- not a leaked task or zombie connection.
        var chunks = new[] { Text("first"), Text("second"), Text("third"), Text("fourth") };
        var fake = new FakeChatClient(chunks, throwAfterChunk: 2);

        var observed = new List<string>();
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var u in fake.GetStreamingResponseAsync(Array.Empty<ChatMessage>()))
                observed.Add(u.Text ?? string.Empty);
        });

        Assert.Equal(new[] { "first", "second" }, observed);
        Assert.Equal(1, fake.CallCount);
    }

    [Fact]
    public async Task CancellationViaToken_BeforeFirstChunk_ProducesNoUpdates()
    {
        var chunks = new[] { Text("a"), Text("b") };
        var fake = new FakeChatClient(chunks);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var observed = new List<string>();
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var u in fake.GetStreamingResponseAsync(Array.Empty<ChatMessage>(), cancellationToken: cts.Token))
                observed.Add(u.Text ?? string.Empty);
        });

        Assert.Empty(observed);
    }

    // ---- Empty stream (Fact 10) ---------------------------------------------

    [Fact]
    public async Task EmptyStream_ServerClosedImmediately_HandledGracefully()
    {
        // Zero-chunk stream: the loop completes without throwing, no text is
        // emitted, and the call count still reflects the single dispatch.
        var fake = new FakeChatClient(Array.Empty<ChatResponseUpdate>());

        int count = 0;
        await foreach (var _ in fake.GetStreamingResponseAsync(Array.Empty<ChatMessage>()))
            count++;

        Assert.Equal(0, count);
        Assert.Equal(1, fake.CallCount);
    }

    [Fact]
    public async Task EmptyStream_AggregatedResponse_HasEmptyOrZeroMessages()
    {
        // Aggregating an empty stream must not throw; the resulting response
        // either has no messages or messages with empty text -- both are
        // acceptable graceful shapes per M.E.AI today.
        var fake = new FakeChatClient(Array.Empty<ChatResponseUpdate>());

        var response = await fake.GetStreamingResponseAsync(Array.Empty<ChatMessage>()).ToChatResponseAsync();

        Assert.NotNull(response);
        var allText = string.Concat(response.Messages.Select(m => m.Text ?? string.Empty));
        Assert.Equal(string.Empty, allText);
    }

    // ---- MAF agent surface parity (Fact 11-12) ------------------------------

    [Fact]
    public async Task MafAgent_OverFakeClient_StreamsEveryTextChunk()
    {
        // The actual dispatch surface in Program.RunAsync is
        //   chatClient.AsAIAgent(...).RunStreamingAsync(prompt, ...)
        // Pin that the MAF wrapper does not buffer or drop deltas when
        // streaming over a compat-style IChatClient.
        var chunks = new[] { Text("compat-"), Text("through-"), Text("maf") };
        var fake = new FakeChatClient(chunks);
        var agent = fake.AsAIAgent(instructions: "sys");

        var sb = new System.Text.StringBuilder();
        await foreach (var u in agent.RunStreamingAsync("go"))
        {
            if (!string.IsNullOrEmpty(u.Text))
                sb.Append(u.Text);
        }

        Assert.Equal("compat-through-maf", sb.ToString());
    }

    [Fact]
    public async Task MafAgent_ToolCallDeltas_ReachAgentRunStreamingAsync()
    {
        // Tool-call deltas surface at the MAF agent layer too -- not just the
        // raw IChatClient. We assert at least one update carries a
        // FunctionCallContent with our seeded call id, which is the contract
        // the agent loop relies on to decide whether to dispatch a tool.
        var args = new Dictionary<string, object?> { ["expr"] = "2+2" };
        var chunks = new[]
        {
            ToolCall("call_maf_1", "calc", args),
            Text("ack"),
        };
        var fake = new FakeChatClient(chunks);
        var agent = fake.AsAIAgent(instructions: "sys");

        var sawToolCall = false;
        await foreach (var u in agent.RunStreamingAsync("go"))
        {
            if (u.Contents is null) continue;
            foreach (var c in u.Contents)
            {
                if (c is FunctionCallContent fc && string.Equals(fc.CallId, "call_maf_1", StringComparison.Ordinal))
                    sawToolCall = true;
            }
        }

        Assert.True(sawToolCall, "FunctionCallContent did not surface through the MAF agent stream.");
    }

    // ---- Fact 13: --json mode is a stdout-formatting flag, not a stream
    // shaping flag. Programs.RunAsync does not transform streaming text
    // before writing it to Console.Out -- the only --json effect on the
    // streaming hot path is suppressing the trailing decorative newline and
    // the "[tokens: ...]" stderr line. Pin that the IChatClient seam itself
    // produces no JSON envelope on its own (parity with the Azure path).

    [Fact]
    public async Task JsonModeStreaming_DispatchSeam_EmitsRawTextNotJsonEnvelope()
    {
        // Confirm: the IChatClient seam writes plain text deltas. Any JSON
        // shaping is applied by the caller (Program), never by the dispatch
        // adapter. If a future change wraps deltas in a JSON envelope here,
        // this test fails and the docs/--json contract gets revisited.
        var chunks = new[] { Text("plain text"), Text(" payload") };
        var fake = new FakeChatClient(chunks);

        var sb = new System.Text.StringBuilder();
        await foreach (var u in fake.GetStreamingResponseAsync(Array.Empty<ChatMessage>()))
            sb.Append(u.Text);

        var s = sb.ToString();
        Assert.Equal("plain text payload", s);
        Assert.DoesNotContain("\"text\":", s, StringComparison.Ordinal);
        Assert.DoesNotContain("\"delta\":", s, StringComparison.Ordinal);
    }

    // ---- Fact 14: deterministic latency budget. Streaming tests must stay
    // sub-second per Bania's perf-gate doctrine. With perTokenLatency=Zero
    // (default) the entire 100-chunk stream completes well under 250ms on a
    // CI runner. If this fails, someone added a sleep that did not belong.

    [Fact]
    public async Task StreamingTextChunks_ManyChunks_CompletesSubSecond()
    {
        var chunks = Enumerable.Range(0, 100).Select(i => Text("x")).ToArray();
        var fake = new FakeChatClient(chunks);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        int count = 0;
        await foreach (var _ in fake.GetStreamingResponseAsync(Array.Empty<ChatMessage>()))
            count++;
        sw.Stop();

        Assert.Equal(100, count);
        Assert.True(sw.Elapsed.TotalMilliseconds < 1000, $"100-chunk stream took {sw.Elapsed.TotalMilliseconds}ms (>=1000ms).");
    }
}
