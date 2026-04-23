using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// Kramer audit M1/M2/L5/M7 regression tests for RalphWorkflow.
///
/// M1: agent is built once (outside the iteration loop).
/// M2: a single MAF AgentSession carries context across retries — the retry
///     prompt no longer re-sends the original task text on every iteration.
/// L5: validation commands pass through ArgumentList — no quote-escaping
///     mangling even when the command contains quotes, spaces, or $.
/// M7: checkpoint I/O failures surface on the Trace channel (was silent).
/// </summary>
[Collection("ConsoleCapture")]
public class RalphWorkflowTests
{
    // ── FakeChatClient: captures per-call message lists and returns canned text ──

    /// <summary>
    /// Test double for <see cref="IChatClient"/>. Records every
    /// <c>GetStreamingResponseAsync</c> invocation's <see cref="ChatMessage"/> list
    /// so the test can inspect what the MAF agent actually sent over the wire.
    /// </summary>
    private sealed class FakeChatClient : IChatClient
    {
        public List<List<ChatMessage>> Calls { get; } = new();
        private readonly Func<int, string> _replyFor;

        public FakeChatClient(Func<int, string> replyFor) => _replyFor = replyFor;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var snapshot = messages.Select(Clone).ToList();
            Calls.Add(snapshot);
            var reply = _replyFor(Calls.Count);
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply)));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var snapshot = messages.Select(Clone).ToList();
            Calls.Add(snapshot);
            var reply = _replyFor(Calls.Count);
            yield return new ChatResponseUpdate(ChatRole.Assistant, reply);
            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }

        // Cheap copy — enough for content inspection.
        private static ChatMessage Clone(ChatMessage m) => new(m.Role, m.Text ?? string.Empty);
    }

    /// <summary>Invokes the internal <c>RalphWorkflow.RunAsync</c> with minimal plumbing.</summary>
    private static Task<int> Run(
        IChatClient chatClient,
        string task,
        string? validate,
        int maxIterations,
        CancellationToken ct = default)
    {
        return AzureOpenAI_CLI.Ralph.RalphWorkflow.RunAsync(
            chatClient,
            taskPrompt: task,
            systemPrompt: "You are a test agent.",
            validateCommand: validate,
            maxIterations: maxIterations,
            temperature: 0.0f,
            maxTokens: 256,
            timeoutSeconds: 30,
            tools: "",                  // no tools — keeps MAF from probing anything
            ct: ct);
    }

    private static IDisposable PushCwd(string dir)
    {
        var orig = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(dir);
        return new CwdRestore(orig);
    }

    private sealed class CwdRestore : IDisposable
    {
        private readonly string _orig;
        public CwdRestore(string orig) => _orig = orig;
        public void Dispose() { try { Directory.SetCurrentDirectory(_orig); } catch { } }
    }

    private static string MakeTempDir([CallerMemberName] string name = "")
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ralph-tests-{name}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    // ── M2: retry prompt does not duplicate the original task text ───────

    [Fact]
    public async Task RetryPrompt_DoesNotDuplicateOriginalTaskText()
    {
        // Arrange — unique marker we can grep for in the second user turn.
        const string marker = "MARKER-DO-NOT-REPEAT-42";
        var client = new FakeChatClient(_ => "understood");
        var workdir = MakeTempDir();
        using var _ = PushCwd(workdir);

        // Act — two iterations; validation always fails so the loop retries.
        var exit = await Run(client, task: $"Do the thing ({marker})", validate: "exit 1", maxIterations: 2);

        // Assert — exit 1 (exhausted), two streaming calls captured.
        Assert.Equal(1, exit);
        Assert.True(client.Calls.Count >= 2, $"expected ≥2 calls, got {client.Calls.Count}");

        // The *new* user message on call 2 is the last user-role message, and
        // the first call already contained the task text (with marker). If M2
        // is fixed, the iteration-2 user delta must NOT repeat the marker.
        var call2 = client.Calls[1];
        var lastUser = call2.LastOrDefault(m => m.Role == ChatRole.User);
        Assert.NotNull(lastUser);
        Assert.DoesNotContain(marker, lastUser!.Text ?? "");
        // And it should look like a compact validation-failure delta.
        Assert.Contains("validation failed", lastUser.Text ?? "", StringComparison.OrdinalIgnoreCase);
    }

    // ── M2: single session preserves context across iterations ──────────

    [Fact]
    public async Task SingleSession_PreservesContextAcrossIterations()
    {
        // Arrange
        var client = new FakeChatClient(i => $"reply-{i}");
        var workdir = MakeTempDir();
        using var _ = PushCwd(workdir);

        // Act — force two iterations via a failing validator.
        await Run(client, task: "please do X", validate: "exit 1", maxIterations: 2);

        // Assert — iteration 2's message list is strictly longer than iteration 1's.
        // That growth is how we know the MAF session is carrying prior turns forward
        // (rather than every call being a fresh context reset).
        Assert.True(client.Calls.Count >= 2);
        var c1 = client.Calls[0];
        var c2 = client.Calls[1];
        Assert.True(c2.Count > c1.Count,
            $"expected call2 message count > call1 ({c1.Count}); got {c2.Count}");

        // The original task text appears exactly once across the cumulative
        // history — proving we didn't re-inject it as a second user turn.
        int taskOccurrences = c2.Count(m => (m.Text ?? "").Contains("please do X"));
        Assert.Equal(1, taskOccurrences);

        // And the prior assistant reply is present in call 2 (session replay).
        Assert.Contains(c2, m => m.Role == ChatRole.Assistant && (m.Text ?? "").Contains("reply-1"));
    }

    // ── L5: validation command with quotes + shell-special chars runs correctly ──

    [Fact]
    public async Task ValidationCommand_WithQuotesAndSpecialChars_RunsCorrectly()
    {
        // Arrange — this command would mangle under the old
        // `Arguments = $"-c \"{command.Replace("\"", "\\\"")}\""` form because
        // it contains both double quotes and a backtick-like construct.
        // ArgumentList passes it through /bin/sh -c intact.
        // Keep command trivially passing (exit 0) to short-circuit the loop on iteration 1.
        const string cmd = "test \"a b\" = \"a b\" && echo 'ok: $HOME'";

        var client = new FakeChatClient(_ => "done");
        var workdir = MakeTempDir();
        using var _ = PushCwd(workdir);

        // Act
        var exit = await Run(client, task: "noop", validate: cmd, maxIterations: 1);

        // Assert — validation passed (exit 0), confirming the shell received
        // the command as a single coherent argv entry.
        Assert.Equal(0, exit);
    }

    [Fact]
    public async Task ValidationCommand_ExitCodeOne_FailsAndRetries()
    {
        // Negative: `exit 1` must be surfaced as a non-zero exit code from
        // the loop when max iterations are exhausted (guards against L5's
        // fix accidentally swallowing the exit code).
        var client = new FakeChatClient(_ => "done");
        var workdir = MakeTempDir();
        using var _ = PushCwd(workdir);

        var exit = await Run(client, task: "noop", validate: "exit 1", maxIterations: 1);

        Assert.Equal(1, exit);
    }

    // ── M7: checkpoint write failures surface on the Trace channel ──────

    [Fact]
    public void Checkpoint_WriteFailure_EmitsTraceMessage()
    {
        // Arrange — point .ralph-log at a directory path (File.AppendAllText will
        // throw because the path is a directory, not a regular file). Register a
        // capturing Trace listener so we can prove the failure wasn't silent.
        var workdir = MakeTempDir();
        var logPath = Path.Combine(workdir, ".ralph-log");
        Directory.CreateDirectory(logPath); // <-- this is what breaks File.AppendAllText

        using var _ = PushCwd(workdir);

        var captured = new List<string>();
        var listener = new ActionTraceListener(captured.Add);
        Trace.Listeners.Add(listener);
        try
        {
            // Act — both write paths should fail silently re: exit code but
            // emit a Trace message so operators can diagnose.
            AzureOpenAI_CLI.Ralph.CheckpointManager.WriteCheckpoint(
                iteration: 1, prompt: "p", agentExitCode: 0, agentResponse: "r",
                validationCommand: null, validationExitCode: null, validationOutput: null);
            AzureOpenAI_CLI.Ralph.CheckpointManager.WriteFinalEntry("final");

            // Assert — both failures captured, each with the [ralph] tag.
            Assert.Contains(captured, s => s.Contains("[ralph]") && s.Contains("checkpoint write failed"));
            Assert.Contains(captured, s => s.Contains("[ralph]") && s.Contains("final-entry write failed"));
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }
    }

    [Fact]
    public void Checkpoint_WriteSuccess_DoesNotEmitTraceMessage()
    {
        // Negative path: under happy-path conditions, the Trace channel must
        // stay quiet — the [ralph] message is reserved for failure signalling.
        var workdir = MakeTempDir();
        using var _ = PushCwd(workdir);

        var captured = new List<string>();
        var listener = new ActionTraceListener(captured.Add);
        Trace.Listeners.Add(listener);
        try
        {
            AzureOpenAI_CLI.Ralph.CheckpointManager.InitializeLog();
            AzureOpenAI_CLI.Ralph.CheckpointManager.WriteCheckpoint(
                iteration: 1, prompt: "p", agentExitCode: 0, agentResponse: "r",
                validationCommand: null, validationExitCode: null, validationOutput: null);

            Assert.DoesNotContain(captured, s => s.Contains("[ralph]"));
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }
    }

    // ── M1: loop-invariants proven by virtue of the refactor compiling ───
    //
    // The agent is now declared above the `for` loop in source; there is no
    // direct runtime knob to count "how many times AsAIAgent was called"
    // without instrumenting MAF internals. The structural guarantee is
    // enforced by the file-shape test below.

    [Fact]
    public void Workflow_BuildsAgentOutsideIterationLoop()
    {
        // Arrange — source inspection is a legitimate structural guard here:
        // if someone reintroduces `var agent = chatClient.AsAIAgent(` inside
        // the `for` loop, this catches the regression at test time.
        var asm = typeof(AzureOpenAI_CLI.Ralph.RalphWorkflow).Assembly;
        _ = asm; // keep reference so the type resolution happens at test time.

        // Fallback: read from the repo (tests run with the source tree available).
        var sourcePath = FindSourceFile("RalphWorkflow.cs");
        Assert.True(File.Exists(sourcePath), $"source file missing: {sourcePath}");
        var src = File.ReadAllText(sourcePath);

        // Assert — exactly one AsAIAgent call, and it appears before the for-loop header.
        int asAgentIdx = src.IndexOf("AsAIAgent(", StringComparison.Ordinal);
        int forIdx = src.IndexOf("for (int iteration", StringComparison.Ordinal);
        Assert.True(asAgentIdx > 0, "no AsAIAgent call found");
        Assert.True(forIdx > 0, "no iteration loop found");
        Assert.True(asAgentIdx < forIdx,
            "AsAIAgent call must be outside (before) the iteration loop; M1 regression.");

        // And only one call total — no rebuilds anywhere else.
        int count = System.Text.RegularExpressions.Regex.Matches(src, @"\.AsAIAgent\(").Count;
        Assert.Equal(1, count);
    }

    private static string FindSourceFile(string leaf)
    {
        // Walk up from the test assembly location until we find the repo's
        // azureopenai-cli/Ralph directory.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "azureopenai-cli", "Ralph", leaf);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return string.Empty;
    }

    // ── Minimal capturing TraceListener ──────────────────────────────────

    private sealed class ActionTraceListener : TraceListener
    {
        private readonly Action<string> _sink;
        public ActionTraceListener(Action<string> sink) => _sink = sink;
        public override void Write(string? message) { if (message is not null) _sink(message); }
        public override void WriteLine(string? message) { if (message is not null) _sink(message); }
    }
}
