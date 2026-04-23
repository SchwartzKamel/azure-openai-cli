using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.AI;
using AzureOpenAI_CLI.Tools;
using AzureOpenAI_CLI.Observability;

namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// Tests for the in-process delegate_task tool (Kramer audit H2 refactor).
/// Verifies the child-agent runs in-process (no Process.Start), respects
/// the AsyncLocal depth cap, honors the tool allowlist, emits telemetry,
/// returns child text, and survives adversarial task strings.
/// </summary>
[Collection(TelemetryGlobalStateCollection.Name)]
public class DelegateTaskToolTests : IDisposable
{
    public DelegateTaskToolTests()
    {
        DelegateTaskTool.ResetForTests();
        Telemetry.Shutdown();
    }

    public void Dispose()
    {
        DelegateTaskTool.ResetForTests();
        Telemetry.Shutdown();
    }

    // ---- Fake IChatClient: streams a fixed text response, optionally captures
    // ChatOptions.Tools count for allowlist assertions, optionally captures the
    // incoming task text for quoting-safety assertions, optionally emits a
    // UsageContent for telemetry, or recurses into DelegateAsync for depth test.
    private sealed class FakeChatClient : IChatClient
    {
        public string ResponseText { get; set; } = "done";
        public int LastToolCount { get; private set; }
        public List<string> LastToolNames { get; } = new();
        public string? LastUserText { get; private set; }
        public bool EmitUsage { get; set; }
        public int UsageIn { get; set; } = 11;
        public int UsageOut { get; set; } = 7;
        public Func<Task<string>>? RecursiveCall { get; set; }

        public void Dispose() { }
        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            CaptureState(messages, options);
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, ResponseText)));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            CaptureState(messages, options);

            // Optionally re-enter DelegateAsync to simulate the model calling
            // the delegate_task tool (drives the depth-cap test).
            if (RecursiveCall is not null)
            {
                var nested = await RecursiveCall();
                yield return new ChatResponseUpdate(ChatRole.Assistant, nested);
                yield break;
            }

            yield return new ChatResponseUpdate(ChatRole.Assistant, ResponseText);

            if (EmitUsage)
            {
                var usage = new UsageContent(new UsageDetails
                {
                    InputTokenCount = UsageIn,
                    OutputTokenCount = UsageOut,
                });
                yield return new ChatResponseUpdate(ChatRole.Assistant, new List<AIContent> { usage });
            }
        }

        private void CaptureState(IEnumerable<ChatMessage> messages, ChatOptions? options)
        {
            LastToolCount = options?.Tools?.Count ?? 0;
            LastToolNames.Clear();
            if (options?.Tools is { } tl)
            {
                foreach (var t in tl)
                {
                    if (t is AIFunction f) LastToolNames.Add(f.Name);
                }
            }
            // Find the non-system user message text.
            foreach (var m in messages)
            {
                if (m.Role == ChatRole.User)
                {
                    LastUserText = m.Text;
                }
            }
        }
    }

    [Fact]
    public async Task DelegateAsync_ReturnsChildText()
    {
        var fake = new FakeChatClient { ResponseText = "child-output-42" };
        DelegateTaskTool.Configure(fake, "sys", "gpt-test");

        var result = await DelegateTaskTool.DelegateAsync("summarize the thing");

        Assert.Equal("child-output-42", result);
    }

    [Fact]
    public async Task DelegateAsync_NotConfigured_ReturnsError()
    {
        // ResetForTests clears the chat client — negative path (Puddy approves).
        var result = await DelegateTaskTool.DelegateAsync("task");
        Assert.StartsWith("Error:", result);
        Assert.Contains("not configured", result);
    }

    [Fact]
    public async Task DelegateAsync_EmptyTask_ReturnsError()
    {
        var fake = new FakeChatClient();
        DelegateTaskTool.Configure(fake, "sys");
        var result = await DelegateTaskTool.DelegateAsync("");
        Assert.StartsWith("Error:", result);
        Assert.Contains("must not be empty", result);
    }

    [Fact]
    public async Task DelegateAsync_RespectsToolAllowlist_DefaultExcludesClipboardAndDelegate()
    {
        var fake = new FakeChatClient();
        DelegateTaskTool.Configure(fake, "sys");

        await DelegateTaskTool.DelegateAsync("do work");

        // Default allowlist: shell,file,web,datetime (no clipboard, no nested delegate).
        Assert.Equal(4, fake.LastToolCount);
        Assert.Contains("shell_exec", fake.LastToolNames);
        Assert.Contains("read_file", fake.LastToolNames);
        Assert.Contains("web_fetch", fake.LastToolNames);
        Assert.Contains("get_datetime", fake.LastToolNames);
        Assert.DoesNotContain("get_clipboard", fake.LastToolNames);
        Assert.DoesNotContain("delegate_task", fake.LastToolNames);
    }

    [Fact]
    public async Task DelegateAsync_RespectsToolAllowlist_ExplicitSubset()
    {
        var fake = new FakeChatClient();
        DelegateTaskTool.Configure(fake, "sys");

        await DelegateTaskTool.DelegateAsync("do work", tools: "file,datetime");

        Assert.Equal(2, fake.LastToolCount);
        Assert.Contains("read_file", fake.LastToolNames);
        Assert.Contains("get_datetime", fake.LastToolNames);
        Assert.DoesNotContain("shell_exec", fake.LastToolNames);
        Assert.DoesNotContain("web_fetch", fake.LastToolNames);
    }

    [Fact]
    public async Task DelegateAsync_DepthCap_BlocksAtMaxDepth()
    {
        // Build a fake whose streaming handler recursively calls DelegateAsync.
        // After 3 nested levels the 4th call must hit the depth guard.
        var fake = new FakeChatClient();
        DelegateTaskTool.Configure(fake, "sys");
        fake.RecursiveCall = () => DelegateTaskTool.DelegateAsync("deeper");

        var result = await DelegateTaskTool.DelegateAsync("root");

        // Innermost (depth=3) attempt returns the depth-cap error; outer calls
        // propagate that error text back up through their streams.
        Assert.Contains($"maximum delegation depth ({DelegateTaskTool.MaxDepth}) reached", result);
    }

    [Fact]
    public async Task DelegateAsync_DepthCounter_ResetsAfterReturn()
    {
        var fake = new FakeChatClient { ResponseText = "ok" };
        DelegateTaskTool.Configure(fake, "sys");

        Assert.Equal(0, DelegateTaskTool.CurrentDepth);
        await DelegateTaskTool.DelegateAsync("a");
        Assert.Equal(0, DelegateTaskTool.CurrentDepth);
        await DelegateTaskTool.DelegateAsync("b");
        Assert.Equal(0, DelegateTaskTool.CurrentDepth);
    }

    [Fact]
    public async Task DelegateAsync_QuotingSafety_AdversarialStringFlowsThroughUnchanged()
    {
        // The v1 shape had to .Replace("\"", "\\\"") the task to survive a
        // shell argv round-trip. In-process: the task string is a plain .NET
        // argument and these characters must pass through verbatim.
        var fake = new FakeChatClient();
        DelegateTaskTool.Configure(fake, "sys");

        var evil = "quote:\" backslash:\\ dollar:$ newline:\nend";
        var result = await DelegateTaskTool.DelegateAsync(evil);

        Assert.Equal("done", result);
        Assert.Equal(evil, fake.LastUserText);
    }

    [Fact]
    public async Task DelegateAsync_EmitsTelemetry_WhenEnabled()
    {
        // Frank's StderrWriter hook: redirect telemetry to an in-memory sink
        // and assert a cost event is emitted with mode=delegate.
        var sink = new StringWriter();
        Telemetry.StderrWriter = sink;
        Telemetry.Initialize(enableOtel: false, enableMetrics: true);

        try
        {
            var fake = new FakeChatClient
            {
                ResponseText = "child said hi",
                EmitUsage = true,
                UsageIn = 17,
                UsageOut = 29,
            };
            DelegateTaskTool.Configure(fake, "sys", "gpt-4o-mini");

            var result = await DelegateTaskTool.DelegateAsync("any task");

            Assert.Equal("child said hi", result);

            var stderr = sink.ToString();
            Assert.False(string.IsNullOrWhiteSpace(stderr), "expected a cost event on stderr");

            // Parse the JSON line and verify mode tag.
            var line = stderr.Trim().Split('\n').Last().Trim();
            using var doc = JsonDocument.Parse(line);
            Assert.Equal("cost", doc.RootElement.GetProperty("kind").GetString());
            Assert.Equal("delegate", doc.RootElement.GetProperty("mode").GetString());
            Assert.Equal("gpt-4o-mini", doc.RootElement.GetProperty("model").GetString());
            Assert.Equal(17, doc.RootElement.GetProperty("input_tokens").GetInt32());
            Assert.Equal(29, doc.RootElement.GetProperty("output_tokens").GetInt32());
        }
        finally
        {
            Telemetry.Shutdown();
            Telemetry.StderrWriter = Console.Error;
        }
    }

    [Fact]
    public async Task DelegateAsync_NoTelemetry_WhenDisabled()
    {
        // Opposite of the previous test: pass the fail. When telemetry is off
        // there must be no stderr noise from the delegate tool.
        var sink = new StringWriter();
        Telemetry.StderrWriter = sink;
        // No Initialize() → Telemetry.IsEnabled == false.

        try
        {
            var fake = new FakeChatClient { ResponseText = "quiet", EmitUsage = true };
            DelegateTaskTool.Configure(fake, "sys", "m");

            await DelegateTaskTool.DelegateAsync("t");

            Assert.Equal(string.Empty, sink.ToString());
        }
        finally
        {
            Telemetry.StderrWriter = Console.Error;
        }
    }
}
