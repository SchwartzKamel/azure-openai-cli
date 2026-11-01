using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AzureOpenAI_CLI.Commander;

/// <summary>
/// A single task item in the Commander Work Queue (<c>.az-ai/queue.json</c>).
/// Serialized via <see cref="AppJsonContext"/> (100% Native AOT compatible).
/// </summary>
internal sealed class QueueTaskItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending"; // pending | running | done | failed | blocked

    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 100;

    [JsonPropertyName("depends_on")]
    public List<string> DependsOn { get; set; } = new();

    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("persona")]
    public string? Persona { get; set; }

    [JsonPropertyName("pattern")]
    public string? Pattern { get; set; }

    [JsonPropertyName("tools")]
    public string? Tools { get; set; }

    [JsonPropertyName("validate_command")]
    public string? ValidateCommand { get; set; }

    [JsonPropertyName("max_iterations")]
    public int MaxIterations { get; set; } = 3;

    [JsonPropertyName("attempts")]
    public int Attempts { get; set; } = 0;

    [JsonPropertyName("max_attempts")]
    public int MaxAttempts { get; set; } = 2;

    [JsonPropertyName("result")]
    public string? Result { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("duration_ms")]
    public long? DurationMs { get; set; }

    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; set; }
}

/// <summary>
/// Persistent Commander Work Queue document (<c>.az-ai/queue.json</c>).
/// </summary>
internal sealed class WorkQueueFile
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("name")]
    public string Name { get; set; } = "default-queue";

    [JsonPropertyName("tasks")]
    public List<QueueTaskItem> Tasks { get; set; } = new();
}

/// <summary>
/// Summary report emitted by <c>az-ai queue status [--json]</c> and <c>az-ai queue run [--json]</c>.
/// </summary>
internal sealed record QueueStatusSummary(
    [property: JsonPropertyName("queue_file")] string QueueFile,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("pending")] int Pending,
    [property: JsonPropertyName("running")] int Running,
    [property: JsonPropertyName("done")] int Done,
    [property: JsonPropertyName("failed")] int Failed,
    [property: JsonPropertyName("blocked")] int Blocked,
    [property: JsonPropertyName("executed_in_run")] int ExecutedInRun = 0,
    [property: JsonPropertyName("tasks")] List<QueueTaskItem>? Tasks = null
);

/// <summary>
/// Crash-safe, DAG-validated manager for <c>.az-ai/queue.json</c> (or custom <c>--queue-file</c>).
/// </summary>
internal static class WorkQueue
{
    private const long MaxQueueFileBytes = 2L * 1024 * 1024; // 2 MB cap
    private const int MaxJsonDepth = 32;

    private static readonly Regex ValidTaskIdRegex =
        new(@"^[a-zA-Z0-9][a-zA-Z0-9_.-]{0,63}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string DefaultQueuePath =>
        Path.Combine(Directory.GetCurrentDirectory(), ".az-ai", "queue.json");

    public static string ResolvePath(string? customQueueFile = null) =>
        !string.IsNullOrWhiteSpace(customQueueFile) ? customQueueFile : DefaultQueuePath;

    public static WorkQueueFile LoadOrCreate(string? customQueueFile = null)
    {
        var path = ResolvePath(customQueueFile);
        if (!File.Exists(path))
        {
            return new WorkQueueFile();
        }

        var len = new FileInfo(path).Length;
        if (len > MaxQueueFileBytes)
        {
            throw new InvalidOperationException($"Queue file '{path}' exceeds 2 MB limit.");
        }

        var json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new WorkQueueFile();
        }

        var opts = new JsonSerializerOptions(AppJsonContext.Default.Options)
        {
            MaxDepth = MaxJsonDepth,
        };
        var ctx = new AppJsonContext(opts);
        return JsonSerializer.Deserialize(json, ctx.WorkQueueFile) ?? new WorkQueueFile();
    }

    /// <summary>
    /// Saves the queue atomically via write-to-temp + rename so an interrupted
    /// process or SIGINT never leaves a truncated <c>queue.json</c>.
    /// </summary>
    public static void SaveAtomic(WorkQueueFile queue, string? customQueueFile = null)
    {
        var path = ResolvePath(customQueueFile);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var tmpPath = $"{path}.{Guid.NewGuid():N}.tmp";
        var json = JsonSerializer.Serialize(queue, AppJsonContext.Default.WorkQueueFile);
        File.WriteAllText(tmpPath, json);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                File.SetUnixFileMode(tmpPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch { }
        }

        File.Move(tmpPath, path, overwrite: true);
    }

    public static bool IsValidTaskId(string? id) =>
        !string.IsNullOrWhiteSpace(id) && ValidTaskIdRegex.IsMatch(id);

    /// <summary>
    /// Adds or updates a task in <paramref name="queue"/>. Auto-assigns <c>T1</c>, <c>T2</c>, ...
    /// when <paramref name="item"/> has no <c>Id</c>.
    /// </summary>
    public static QueueTaskItem AddTask(WorkQueueFile queue, QueueTaskItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Id))
        {
            int nextNum = queue.Tasks.Count + 1;
            while (queue.Tasks.Any(t => string.Equals(t.Id, $"T{nextNum}", StringComparison.OrdinalIgnoreCase)))
            {
                nextNum++;
            }
            item.Id = $"T{nextNum}";
        }

        if (!IsValidTaskId(item.Id))
        {
            throw new ArgumentException($"Invalid task ID '{item.Id}'. Must match [a-zA-Z0-9][a-zA-Z0-9_.-]{{0,63}}.");
        }

        if (string.IsNullOrWhiteSpace(item.Prompt))
        {
            throw new ArgumentException($"Task '{item.Id}' requires a non-empty prompt.");
        }

        if (string.IsNullOrWhiteSpace(item.Title))
        {
            item.Title = item.Prompt.Length > 60 ? item.Prompt[..60] + "..." : item.Prompt;
        }

        item.UpdatedAt = DateTime.UtcNow.ToString("O");

        var existingIdx = queue.Tasks.FindIndex(t => string.Equals(t.Id, item.Id, StringComparison.OrdinalIgnoreCase));
        if (existingIdx >= 0)
        {
            queue.Tasks[existingIdx] = item;
        }
        else
        {
            queue.Tasks.Add(item);
        }

        return item;
    }

    /// <summary>
    /// Validates the DAG for missing dependencies or cycles (`A -> B -> A`).
    /// Returns <c>null</c> if valid, or a descriptive error string if invalid.
    /// </summary>
    public static string? ValidateDag(WorkQueueFile queue)
    {
        var byId = new Dictionary<string, QueueTaskItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in queue.Tasks)
        {
            if (!IsValidTaskId(t.Id))
                return $"Invalid task ID '{t.Id}'.";
            if (!byId.TryAdd(t.Id, t))
                return $"Duplicate task ID '{t.Id}' in queue.";
        }

        foreach (var t in queue.Tasks)
        {
            foreach (var dep in t.DependsOn)
            {
                if (!byId.ContainsKey(dep))
                    return $"Task '{t.Id}' depends on unknown task '{dep}'.";
            }
        }

        // 0 = unvisited, 1 = visiting (on current recursion stack), 2 = visited
        var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        bool HasCycle(string currentId, List<string> path)
        {
            state[currentId] = 1;
            path.Add(currentId);

            var item = byId[currentId];
            foreach (var dep in item.DependsOn)
            {
                state.TryGetValue(dep, out int depState);
                if (depState == 1)
                {
                    path.Add(dep);
                    return true;
                }
                if (depState == 0 && HasCycle(dep, path))
                {
                    return true;
                }
            }

            path.RemoveAt(path.Count - 1);
            state[currentId] = 2;
            return false;
        }

        foreach (var t in queue.Tasks)
        {
            state.TryGetValue(t.Id, out int s);
            if (s == 0)
            {
                var cyclePath = new List<string>();
                if (HasCycle(t.Id, cyclePath))
                {
                    return $"Dependency cycle detected in queue: {string.Join(" -> ", cyclePath)}";
                }
            }
        }

        return null;
    }

    public static QueueStatusSummary Summarize(WorkQueueFile queue, string queuePath, int executedInRun = 0)
    {
        int pending = 0, running = 0, done = 0, failed = 0, blocked = 0;
        foreach (var t in queue.Tasks)
        {
            switch ((t.Status ?? "pending").ToLowerInvariant())
            {
                case "done": done++; break;
                case "running": running++; break;
                case "failed": failed++; break;
                case "blocked": blocked++; break;
                default: pending++; break;
            }
        }

        return new QueueStatusSummary(
            QueueFile: queuePath,
            Total: queue.Tasks.Count,
            Pending: pending,
            Running: running,
            Done: done,
            Failed: failed,
            Blocked: blocked,
            ExecutedInRun: executedInRun,
            Tasks: queue.Tasks);
    }
}
