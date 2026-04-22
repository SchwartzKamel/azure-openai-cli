using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// FDR v2 dogfood High-severity (fdr-v2-ralph-exit-code) tests:
/// Ralph must return exit 1 when max-iterations are exhausted without
/// validation passing. Exit 0 only when validation actually passes.
/// SIGINT-130 preserved (covered by existing RalphWorkflowTests).
/// </summary>
[Collection("ConsoleCapture")]
public class RalphExitCodeTests
{
    /// <summary>Minimal IChatClient that yields a fixed canned reply.</summary>
    private sealed class CannedChatClient : IChatClient
    {
        private readonly string _reply;
        public CannedChatClient(string reply) => _reply = reply;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, _reply)));

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, _reply);
            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private static string MakeTempDir([CallerMemberName] string name = "")
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ralph-exit-{name}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private sealed class CwdGuard : IDisposable
    {
        private readonly string _orig;
        public CwdGuard(string newDir)
        {
            _orig = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(newDir);
        }
        public void Dispose() { try { Directory.SetCurrentDirectory(_orig); } catch { } }
    }

    private static Task<int> Run(
        IChatClient client, string task, string? validate, int maxIterations)
        => AzureOpenAI_CLI_V2.Ralph.RalphWorkflow.RunAsync(
            client,
            taskPrompt: task,
            systemPrompt: "test",
            validateCommand: validate,
            maxIterations: maxIterations,
            temperature: 0.0f,
            maxTokens: 128,
            timeoutSeconds: 30,
            tools: "",
            ct: CancellationToken.None);

    [Fact]
    public async Task MaxIterationsExhaustedWithoutPass_ReturnsExitCode1()
    {
        // Arrange — validation always fails; agent always "succeeds" with a canned reply.
        var client = new CannedChatClient("working on it");
        using var _ = new CwdGuard(MakeTempDir());

        // Act
        int exit = await Run(client, "do a thing", validate: "exit 1", maxIterations: 3);

        // Assert — exit 1, not 0.
        Assert.Equal(1, exit);
    }

    [Fact]
    public async Task ValidationPassesOnLaterIteration_ReturnsExitCode0()
    {
        // Arrange — validator that fails twice, then passes on the 3rd run.
        // Uses a counter file in the temp cwd so each invocation is sequential.
        var dir = MakeTempDir();
        using var _ = new CwdGuard(dir);
        var counter = "counter.txt";
        var validateCmd =
            $"n=$(cat {counter} 2>/dev/null || echo 0); n=$((n+1)); echo $n > {counter}; [ $n -ge 3 ]";
        var client = new CannedChatClient("attempting");

        // Act — max 5 iterations, should succeed on iteration 3.
        int exit = await Run(client, "do a thing", validate: validateCmd, maxIterations: 5);

        // Assert — exit 0.
        Assert.Equal(0, exit);

        // .ralph-log should record the passing iteration.
        var log = await File.ReadAllTextAsync(Path.Combine(dir, ".ralph-log"));
        Assert.Contains("Validation passed", log);
        Assert.Contains("iteration 3", log);
    }

    [Fact]
    public async Task ValidationPassesFirstIteration_ReturnsExitCode0AndSingleLogEntry()
    {
        // Arrange — validation succeeds immediately.
        var dir = MakeTempDir();
        using var _ = new CwdGuard(dir);
        var client = new CannedChatClient("done");

        // Act
        int exit = await Run(client, "do a thing", validate: "true", maxIterations: 5);

        // Assert — exit 0 and only one iteration recorded in .ralph-log.
        Assert.Equal(0, exit);
        var log = await File.ReadAllTextAsync(Path.Combine(dir, ".ralph-log"));
        Assert.Contains("## Iteration 1", log);
        Assert.DoesNotContain("## Iteration 2", log);
        Assert.Contains("Validation passed", log);
    }

    [Fact]
    public async Task MaxIterationsExhausted_RalphLogRecordsFinalVerdict()
    {
        // Arrange — validation always fails; confirm .ralph-log captures the verdict.
        var dir = MakeTempDir();
        using var _ = new CwdGuard(dir);
        var client = new CannedChatClient("trying");

        // Act
        int exit = await Run(client, "do a thing", validate: "exit 1", maxIterations: 2);

        // Assert — exit 1 AND final verdict captured.
        Assert.Equal(1, exit);
        var log = await File.ReadAllTextAsync(Path.Combine(dir, ".ralph-log"));
        Assert.Contains("**Final status:**", log);
        Assert.Contains("exhausted", log, StringComparison.OrdinalIgnoreCase);
    }
}
