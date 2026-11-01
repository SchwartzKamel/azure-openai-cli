using System.Diagnostics;
using System.Text;
using AzureOpenAI_CLI.Patterns;
using AzureOpenAI_CLI.Providers;
using AzureOpenAI_CLI.Squad;
using AzureOpenAI_CLI.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AzureOpenAI_CLI.Commander;

/// <summary>
/// Executes tasks in a <see cref="WorkQueueFile"/> in topological dependency and
/// priority order, routing each task to its configured <c>provider</c>, <c>model</c>,
/// <c>persona</c>, <c>pattern</c>, and optional <c>validate_command</c>.
/// </summary>
internal static class CommanderRunner
{
    /// <summary>
    /// Runs all ready tasks in <paramref name="queue"/> until convergence or
    /// unrecoverable failure. Persists state atomically after every task state transition.
    /// </summary>
    public static async Task<(int ExitCode, int ExecutedCount, string? ErrorMessage)> RunQueueAsync(
        WorkQueueFile queue,
        string? queueFilePath,
        UserConfig userConfig,
        string? defaultProviderFlag = null,
        string? defaultModelFlag = null,
        int concurrency = 1,
        bool dryRun = false,
        bool recoverStale = false,
        bool quiet = false,
        Func<QueueTaskItem, ResolvedProviderConfig, IChatClient>? chatClientFactory = null,
        PluginLoadOptions? pluginOptions = null,
        CancellationToken ct = default)
    {
        var dagError = WorkQueue.ValidateDag(queue);
        if (dagError is not null)
        {
            return (1, 0, dagError);
        }

        var saveLock = new SemaphoreSlim(1, 1);
        async Task PersistAsync()
        {
            if (dryRun) return;
            await saveLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                WorkQueue.SaveAtomic(queue, queueFilePath);
            }
            finally
            {
                saveLock.Release();
            }
        }

        if (recoverStale)
        {
            bool recoveredAny = false;
            foreach (var t in queue.Tasks)
            {
                if (string.Equals(t.Status, "running", StringComparison.OrdinalIgnoreCase))
                {
                    t.Status = "pending";
                    t.UpdatedAt = DateTime.UtcNow.ToString("O");
                    recoveredAny = true;
                }
            }
            if (recoveredAny)
            {
                await PersistAsync().ConfigureAwait(false);
            }
        }

        int executedCount = 0;
        int effectiveConcurrency = Math.Clamp(concurrency, 1, 8);
        var byId = queue.Tasks.ToDictionary(t => t.Id, StringComparer.OrdinalIgnoreCase);

        while (!ct.IsCancellationRequested)
        {
            // Mark tasks whose upstream dependency failed/blocked as blocked
            bool stateChanged = false;
            foreach (var t in queue.Tasks)
            {
                if (!string.Equals(t.Status, "pending", StringComparison.OrdinalIgnoreCase))
                    continue;

                var failedDep = t.DependsOn.FirstOrDefault(d =>
                    byId.TryGetValue(d, out var depTask)
                    && (string.Equals(depTask.Status, "failed", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(depTask.Status, "blocked", StringComparison.OrdinalIgnoreCase)));

                if (failedDep is not null)
                {
                    t.Status = "blocked";
                    t.Error = $"Blocked by failed dependency '{failedDep}'.";
                    t.UpdatedAt = DateTime.UtcNow.ToString("O");
                    stateChanged = true;
                }
            }

            if (stateChanged)
            {
                await PersistAsync().ConfigureAwait(false);
            }

            // Find all pending tasks whose dependencies are all 'done'
            var readyBatch = queue.Tasks
                .Where(t => string.Equals(t.Status, "pending", StringComparison.OrdinalIgnoreCase)
                    && t.DependsOn.All(d => byId.TryGetValue(d, out var dep)
                        && string.Equals(dep.Status, "done", StringComparison.OrdinalIgnoreCase)))
                .OrderBy(t => t.Priority)
                .ThenBy(t => t.Id, StringComparer.OrdinalIgnoreCase)
                .Take(effectiveConcurrency)
                .ToList();

            if (readyBatch.Count == 0)
            {
                break;
            }

            if (dryRun)
            {
                foreach (var item in readyBatch)
                {
                    item.Status = "done";
                    item.Result = $"[dry-run] Would execute task '{item.Id}' ({item.Title})";
                    executedCount++;
                }
                continue;
            }

            foreach (var item in readyBatch)
            {
                item.Status = "running";
                item.UpdatedAt = DateTime.UtcNow.ToString("O");
            }
            await PersistAsync().ConfigureAwait(false);

            var batchTasks = readyBatch.Select(async item =>
            {
                await ExecuteSingleTaskAsync(
                    item,
                    byId,
                    userConfig,
                    defaultProviderFlag,
                    defaultModelFlag,
                    quiet,
                    chatClientFactory,
                    pluginOptions,
                    ct).ConfigureAwait(false);

                Interlocked.Increment(ref executedCount);
                await PersistAsync().ConfigureAwait(false);
            });

            await Task.WhenAll(batchTasks).ConfigureAwait(false);
        }

        bool anyFailedOrBlocked = queue.Tasks.Any(t =>
            string.Equals(t.Status, "failed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(t.Status, "blocked", StringComparison.OrdinalIgnoreCase));

        return (anyFailedOrBlocked ? 1 : 0, executedCount, null);
    }

    private static async Task ExecuteSingleTaskAsync(
        QueueTaskItem item,
        IReadOnlyDictionary<string, QueueTaskItem> byId,
        UserConfig userConfig,
        string? defaultProviderFlag,
        string? defaultModelFlag,
        bool quiet,
        Func<QueueTaskItem, ResolvedProviderConfig, IChatClient>? chatClientFactory,
        PluginLoadOptions? pluginOptions,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        int maxAttempts = Math.Clamp(item.MaxAttempts > 0 ? item.MaxAttempts : 1, 1, 5);

        var (resolvedProvider, providerErr) = ProviderSelector.TryResolve(
            providerFlag: item.Provider ?? defaultProviderFlag,
            endpointOverride: null,
            apiKeyOverride: null,
            modelFlag: item.Model ?? defaultModelFlag,
            userConfig: userConfig);

        if (resolvedProvider is null)
        {
            item.Status = "failed";
            item.Error = providerErr ?? "Provider resolution failed.";
            item.DurationMs = sw.ElapsedMilliseconds;
            item.UpdatedAt = DateTime.UtcNow.ToString("O");
            return;
        }

        // Resolve system prompt: pattern / persona / default
        string systemPrompt = "You are a secure, concise CLI assistant. Keep answers factual, no fluff.";
        string? effectiveTools = item.Tools;

        if (!string.IsNullOrWhiteSpace(item.Pattern))
        {
            var pat = PatternRegistry.Resolve(item.Pattern);
            if (pat is not null)
            {
                systemPrompt = pat.SystemPrompt;
            }
        }

        if (!string.IsNullOrWhiteSpace(item.Persona))
        {
            var squad = SquadConfig.Load();
            var persona = squad?.GetPersona(item.Persona);
            if (persona is not null)
            {
                systemPrompt = persona.SystemPrompt;
                if (string.IsNullOrWhiteSpace(effectiveTools) && persona.Tools.Count > 0)
                {
                    effectiveTools = string.Join(",", persona.Tools);
                }
            }
        }

        // Inject completed upstream dependency outputs as context
        var promptBuilder = new StringBuilder();
        if (item.DependsOn.Count > 0)
        {
            var depOutputs = new List<string>();
            foreach (var depId in item.DependsOn)
            {
                if (byId.TryGetValue(depId, out var depItem) && !string.IsNullOrWhiteSpace(depItem.Result))
                {
                    depOutputs.Add($"### Output from prerequisite task {depId} ({depItem.Title}):\n{depItem.Result}");
                }
            }
            if (depOutputs.Count > 0)
            {
                promptBuilder.AppendLine("<upstream_context>");
                promptBuilder.AppendLine(string.Join("\n\n", depOutputs));
                promptBuilder.AppendLine("</upstream_context>");
                promptBuilder.AppendLine();
            }
        }
        promptBuilder.Append(item.Prompt);
        var fullPrompt = promptBuilder.ToString();

        var chatClient = chatClientFactory is not null
            ? chatClientFactory(item, resolvedProvider)
            : ProviderSelector.CreateChatClient(resolvedProvider);

        while (item.Attempts < maxAttempts && !ct.IsCancellationRequested)
        {
            item.Attempts++;
            if (!quiet && !Console.IsErrorRedirected)
            {
                Console.Error.WriteLine($"▶ [Commander] Running {item.Id}: {item.Title} (provider={resolvedProvider.Name}, model={resolvedProvider.Model}, attempt={item.Attempts}/{maxAttempts})");
            }

            try
            {
                bool useTools = !string.IsNullOrWhiteSpace(effectiveTools) || !string.IsNullOrWhiteSpace(item.ValidateCommand);
                var instructions = useTools ? systemPrompt + "\n\n" + Program.SAFETY_CLAUSE : systemPrompt;
                var agent = useTools
                    ? chatClient.AsAIAgent(
                        instructions: instructions,
                        tools: ToolRegistry.CreateMafTools(
                            effectiveTools?.Split(',', StringSplitOptions.RemoveEmptyEntries),
                            maxRounds: Program.DEFAULT_MAX_AGENT_ROUNDS,
                            pluginOptions: pluginOptions))
                    : chatClient.AsAIAgent(instructions: instructions);

                var runOpts = new ChatClientAgentRunOptions
                {
                    ChatOptions = Program.BuildModernChatOptions(
                        temperature: !string.IsNullOrWhiteSpace(item.ValidateCommand) ? Program.RALPH_VALIDATE_TEMPERATURE : 0.3f,
                        maxTokens: 4096),
                };

                var sb = new StringBuilder();
                await foreach (var update in agent.RunStreamingAsync(fullPrompt, options: runOpts, cancellationToken: ct).ConfigureAwait(false))
                {
                    if (!string.IsNullOrEmpty(update.Text))
                    {
                        sb.Append(update.Text);
                    }
                }

                var outputText = sb.ToString().Trim();

                if (!string.IsNullOrWhiteSpace(item.ValidateCommand))
                {
                    var (valCode, valOut) = await RunValidateShellAsync(item.ValidateCommand, timeoutSeconds: 60, ct).ConfigureAwait(false);
                    if (valCode != 0)
                    {
                        item.Error = $"Validation failed (exit {valCode}): {valOut}";
                        if (item.Attempts < maxAttempts)
                        {
                            fullPrompt = $"{fullPrompt}\n\n[Previous attempt failed validation (exit {valCode}): {valOut}. Fix and retry.]";
                            continue;
                        }
                        item.Status = "failed";
                        item.DurationMs = sw.ElapsedMilliseconds;
                        item.UpdatedAt = DateTime.UtcNow.ToString("O");
                        return;
                    }
                }

                item.Result = outputText;
                item.Error = null;
                item.Status = "done";
                item.DurationMs = sw.ElapsedMilliseconds;
                item.UpdatedAt = DateTime.UtcNow.ToString("O");
                return;
            }
            catch (Exception ex)
            {
                item.Error = ex.Message;
                if (item.Attempts >= maxAttempts)
                {
                    item.Status = "failed";
                    item.DurationMs = sw.ElapsedMilliseconds;
                    item.UpdatedAt = DateTime.UtcNow.ToString("O");
                    return;
                }
            }
        }
    }

    private static async Task<(int ExitCode, string Output)> RunValidateShellAsync(
        string command,
        int timeoutSeconds,
        CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        var psi = new ProcessStartInfo
        {
            FileName = "/bin/sh",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add(command);

        try
        {
            using var proc = Process.Start(psi);
            if (proc is null)
                return (1, "Failed to start validation process");

            proc.StandardInput.Close();
            var stdout = await proc.StandardOutput.ReadToEndAsync(cts.Token).ConfigureAwait(false);
            var stderr = await proc.StandardError.ReadToEndAsync(cts.Token).ConfigureAwait(false);
            await proc.WaitForExitAsync(cts.Token).ConfigureAwait(false);

            var combined = string.IsNullOrEmpty(stderr) ? stdout : $"{stdout}\n{stderr}";
            return (proc.ExitCode, combined.Trim());
        }
        catch (OperationCanceledException)
        {
            return (1, "Validation command timed out");
        }
    }
}
