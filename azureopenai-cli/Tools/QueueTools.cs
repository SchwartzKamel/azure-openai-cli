using System.ComponentModel;
using System.Text.Json;
using AzureOpenAI_CLI.Commander;

namespace AzureOpenAI_CLI.Tools;

/// <summary>
/// Built-in tools (<c>queue_add</c>, <c>queue_status</c>) that allow an in-process
/// agent or an external MCP host (<c>hopper mcp serve</c>) to enqueue tasks into
/// the Commander Work Queue (<c>.az-ai/queue.json</c>) and inspect queue status.
/// </summary>
internal static class QueueTools
{
    [Description("Add a task to the Commander Work Queue (.az-ai/queue.json) with optional DAG dependencies, priority, provider, model, persona, pattern, and validation command.")]
    public static Task<string> AddAsync(
        [Description("The task prompt / instructions to execute")] string prompt,
        [Description("Optional task ID (e.g. 'T1'). Auto-assigned if omitted.")] string? id = null,
        [Description("Optional short title for the task")] string? title = null,
        [Description("Optional comma-separated task IDs this task depends on (e.g. 'T1,T2')")] string? dependsOn = null,
        [Description("Optional priority (lower runs first, default 100)")] int priority = 100,
        [Description("Optional provider (azure, openai, openrouter, ollama, llamacpp, custom)")] string? provider = null,
        [Description("Optional model deployment or alias")] string? model = null,
        [Description("Optional squad persona name")] string? persona = null,
        [Description("Optional prompt pattern name (e.g. commit-message, code-review)")] string? pattern = null,
        [Description("Optional shell command to validate completion (exit 0 = pass)")] string? validateCommand = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return Task.FromResult("Error: parameter 'prompt' must not be empty.");

        try
        {
            var queue = WorkQueue.LoadOrCreate();
            var deps = string.IsNullOrWhiteSpace(dependsOn)
                ? new List<string>()
                : dependsOn.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

            var item = new QueueTaskItem
            {
                Id = id ?? string.Empty,
                Title = title ?? string.Empty,
                Prompt = prompt,
                Priority = priority,
                DependsOn = deps,
                Provider = provider,
                Model = model,
                Persona = persona,
                Pattern = pattern,
                ValidateCommand = validateCommand,
            };

            var added = WorkQueue.AddTask(queue, item);
            var dagError = WorkQueue.ValidateDag(queue);
            if (dagError is not null)
            {
                return Task.FromResult($"Error: {dagError}");
            }

            WorkQueue.SaveAtomic(queue);
            return Task.FromResult(JsonSerializer.Serialize(added, AppJsonContext.Default.QueueTaskItem));
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error adding task to queue: {ex.Message}");
        }
    }

    [Description("Inspect the current status and tasks of the Commander Work Queue (.az-ai/queue.json) as JSON.")]
    public static Task<string> StatusAsync(
        [Description("Optional path to an alternate queue.json file")] string? queueFile = null,
        CancellationToken ct = default)
    {
        try
        {
            var path = WorkQueue.ResolvePath(queueFile);
            var queue = WorkQueue.LoadOrCreate(path);
            var summary = WorkQueue.Summarize(queue, path);
            return Task.FromResult(JsonSerializer.Serialize(summary, AppJsonContext.Default.QueueStatusSummary));
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error reading queue status: {ex.Message}");
        }
    }
}
