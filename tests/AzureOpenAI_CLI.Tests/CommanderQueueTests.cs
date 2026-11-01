using System.Runtime.CompilerServices;
using System.Text.Json;
using AzureOpenAI_CLI.Commander;
using AzureOpenAI_CLI.Tools;
using Microsoft.Extensions.AI;

namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// T9 tests: proves the built-in Commander Work Queue (<c>az-ai queue</c> /
/// <c>az-ai commander</c>): DAG topological ordering, dependency cycle detection,
/// upstream output propagation, per-task validation &amp; blocking propagation,
/// stale-running recovery, atomic persistence, and <c>queue_add</c> / <c>queue_status</c>
/// tool integration.
/// </summary>
[Collection("ConsoleCapture")]
public class CommanderQueueTests
{
    [Fact]
    public void ParseArgs_QueueAndCommanderSubcommands_ParsedCorrectly()
    {
        var opts1 = Program.ParseArgs([
            "queue", "add",
            "--task-id", "T10",
            "--title", "Audit WebFetch",
            "--depends-on", "T1,T2",
            "--priority", "10",
            "--provider", "ollama",
            "--model", "gemma3",
            "--validate", "dotnet test",
            "Run the security audit"
        ]);

        Assert.False(opts1.ParseError);
        Assert.Equal("add", opts1.QueueSubcommand);
        Assert.Equal("T10", opts1.TaskId);
        Assert.Equal("Audit WebFetch", opts1.Title);
        Assert.Equal("T1,T2", opts1.DependsOn);
        Assert.Equal(10, opts1.Priority);
        Assert.Equal("ollama", opts1.Provider);
        Assert.Equal("gemma3", opts1.Model);
        Assert.Equal("dotnet test", opts1.ValidateCommand);
        Assert.Equal("Run the security audit", opts1.Prompt);

        var opts2 = Program.ParseArgs([
            "commander", "run",
            "--concurrency", "4",
            "--dry-run",
            "--recover-stale"
        ]);
        Assert.False(opts2.ParseError);
        Assert.Equal("run", opts2.QueueSubcommand);
        Assert.Equal(4, opts2.Concurrency);
        Assert.True(opts2.DryRun);
        Assert.True(opts2.RecoverStale);
    }

    [Fact]
    public void ValidateDag_DetectsDirectAndTransitiveDependencyCycles()
    {
        var q = new WorkQueueFile();
        WorkQueue.AddTask(q, new QueueTaskItem { Id = "A", Prompt = "step A", DependsOn = new List<string> { "C" } });
        WorkQueue.AddTask(q, new QueueTaskItem { Id = "B", Prompt = "step B", DependsOn = new List<string> { "A" } });
        WorkQueue.AddTask(q, new QueueTaskItem { Id = "C", Prompt = "step C", DependsOn = new List<string> { "B" } });

        var err = WorkQueue.ValidateDag(q);
        Assert.NotNull(err);
        Assert.Contains("Dependency cycle detected", err);
    }

    [Fact]
    public void ValidateDag_DetectsMissingDependencyTarget()
    {
        var q = new WorkQueueFile();
        WorkQueue.AddTask(q, new QueueTaskItem { Id = "T1", Prompt = "step 1", DependsOn = new List<string> { "NONEXISTENT" } });

        var err = WorkQueue.ValidateDag(q);
        Assert.NotNull(err);
        Assert.Contains("depends on unknown task 'NONEXISTENT'", err);
    }

    [Fact]
    public async Task CommanderRunner_ExecutesDagInTopologicalAndPriorityOrder_AndPropagatesUpstreamContext()
    {
        var qFile = Path.Combine(Path.GetTempPath(), $"az-queue-{Guid.NewGuid():N}.json");
        try
        {
            var q = new WorkQueueFile();
            // T2 depends on T1; T3 has higher priority (10) than T1 (50) and no dependencies,
            // so execution order must be: T3 -> T1 -> T2.
            WorkQueue.AddTask(q, new QueueTaskItem
            {
                Id = "T1",
                Title = "Generate schema",
                Prompt = "Create user schema",
                Priority = 50,
                Provider = "ollama",
            });
            WorkQueue.AddTask(q, new QueueTaskItem
            {
                Id = "T2",
                Title = "Implement endpoint",
                Prompt = "Build endpoint using schema",
                Priority = 10,
                DependsOn = new List<string> { "T1" },
                Provider = "ollama",
                ValidateCommand = "exit 0",
            });
            WorkQueue.AddTask(q, new QueueTaskItem
            {
                Id = "T3",
                Title = "Lint check",
                Prompt = "Run preflight check",
                Priority = 10,
                Provider = "ollama",
            });

            var executionOrder = new List<string>();
            var promptsReceived = new Dictionary<string, string>();

            var (exitCode, executed, err) = await CommanderRunner.RunQueueAsync(
                queue: q,
                queueFilePath: qFile,
                chatClientFactory: taskItem => (
                    new RecordingFakeChatClient(
                        onCall: lastUserMessage =>
                        {
                            lock (executionOrder)
                            {
                                executionOrder.Add(taskItem.Id);
                                promptsReceived[taskItem.Id] = lastUserMessage;
                            }
                            return $"completed:{taskItem.Id}";
                        }),
                    "fake-model",
                    null),
                concurrency: 1,
                quiet: true);

            Assert.Null(err);
            Assert.Equal(0, exitCode);
            Assert.Equal(3, executed);
            Assert.Equal(new[] { "T3", "T1", "T2" }, executionOrder);

            // Verify T2 received T1's output in <upstream_context>
            Assert.Contains("<upstream_context>", promptsReceived["T2"]);
            Assert.Contains("completed:T1", promptsReceived["T2"]);

            // Verify atomic persistence to disk
            var reloaded = WorkQueue.LoadOrCreate(qFile);
            Assert.All(reloaded.Tasks, t => Assert.Equal("done", t.Status));
        }
        finally
        {
            try { File.Delete(qFile); } catch { }
        }
    }

    [Fact]
    public async Task CommanderRunner_WhenUpstreamValidationFails_MarksDownstreamTasksBlocked()
    {
        var qFile = Path.Combine(Path.GetTempPath(), $"az-queue-fail-{Guid.NewGuid():N}.json");
        try
        {
            var q = new WorkQueueFile();
            WorkQueue.AddTask(q, new QueueTaskItem
            {
                Id = "T1",
                Prompt = "Failing task",
                Provider = "ollama",
                ValidateCommand = "exit 1",
                MaxAttempts = 2,
            });
            WorkQueue.AddTask(q, new QueueTaskItem
            {
                Id = "T2",
                Prompt = "Dependent task",
                Provider = "ollama",
                DependsOn = new List<string> { "T1" },
            });

            var (exitCode, executed, err) = await CommanderRunner.RunQueueAsync(
                queue: q,
                queueFilePath: qFile,
                chatClientFactory: _ => (new RecordingFakeChatClient(_ => "attempted fix"), "fake-model", null),
                concurrency: 1,
                quiet: true);

            Assert.Null(err);
            Assert.Equal(1, exitCode);
            Assert.Equal(1, executed);

            var t1 = q.Tasks.Single(t => t.Id == "T1");
            var t2 = q.Tasks.Single(t => t.Id == "T2");
            Assert.Equal("failed", t1.Status);
            Assert.Equal(2, t1.Attempts);
            Assert.Equal("blocked", t2.Status);
            Assert.Contains("Blocked by failed dependency 'T1'", t2.Error);
        }
        finally
        {
            try { File.Delete(qFile); } catch { }
        }
    }

    [Fact]
    public async Task QueueTools_AddAndStatus_WorkThroughToolRegistry()
    {
        using var cwd = new TempCwd();

        var addJson = await QueueTools.AddAsync(
            prompt: "Refactor auth module",
            id: "AUTH-1",
            title: "Refactor auth",
            provider: "ollama",
            model: "gemma3",
            pattern: "code-review");

        using var addDoc = JsonDocument.Parse(addJson);
        Assert.Equal("AUTH-1", addDoc.RootElement.GetProperty("id").GetString());
        Assert.Equal("ollama", addDoc.RootElement.GetProperty("provider").GetString());

        var statusJson = await QueueTools.StatusAsync();
        using var statusDoc = JsonDocument.Parse(statusJson);
        Assert.Equal(1, statusDoc.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(1, statusDoc.RootElement.GetProperty("pending").GetInt32());
    }

    private sealed class RecordingFakeChatClient : IChatClient
    {
        private readonly Func<string, string> _onCall;
        public RecordingFakeChatClient(Func<string, string> onCall) => _onCall = onCall;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var lastText = messages.LastOrDefault()?.Text ?? string.Empty;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, _onCall(lastText))));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var lastText = messages.LastOrDefault()?.Text ?? string.Empty;
            var reply = _onCall(lastText);
            yield return new ChatResponseUpdate(ChatRole.Assistant, reply);
            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
