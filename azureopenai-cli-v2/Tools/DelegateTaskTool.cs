using System.ComponentModel;
using System.Text;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AzureOpenAI_CLI_V2.Tools;

/// <summary>
/// Delegate a subtask to an in-process child MAF agent built from the parent's
/// <see cref="IChatClient"/>. Replaces the v1-style <c>Process.Start</c> re-launch
/// (Kramer audit H2): no exe-locate dance, no shell-argument quoting, no
/// <c>RALPH_DEPTH</c> env-var marshalling, no credential re-plumbing.
///
/// Registration: <see cref="DelegateAsync"/> is a static function wired into
/// <c>ToolRegistry.CreateMafTools</c> via <c>AIFunctionFactory.Create</c>.
/// Program.cs calls <see cref="Configure"/> exactly once after building the
/// parent <c>IChatClient</c> so the tool can spawn child agents on demand.
///
/// Depth guard: tracked via <see cref="AsyncLocal{T}"/> so nested delegations
/// stay isolated per logical flow; cap is <see cref="MaxDepth"/> (3).
/// </summary>
internal static class DelegateTaskTool
{
    internal const int MaxDepth = 3;

    // Parent-supplied chat client + baseline instructions. Set by Program.Configure
    // at startup so DelegateAsync (a static tool function) can spawn children
    // without the registry needing an instance dependency.
    private static IChatClient? s_chatClient;
    private static string s_baseInstructions = string.Empty;
    private static string? s_model;

    // AsyncLocal so nested delegations in the same logical flow see monotonic
    // depth, while parallel roots stay isolated. Replaces RALPH_DEPTH env var.
    private static readonly AsyncLocal<int> s_depth = new();

    /// <summary>
    /// Wire the parent agent's chat client and system instructions into the tool.
    /// Call once from Program.cs after <c>chatClient</c> is built. Safe to call
    /// multiple times (last write wins; used by tests).
    /// </summary>
    public static void Configure(IChatClient chatClient, string baseInstructions, string? model = null)
    {
        s_chatClient = chatClient;
        s_baseInstructions = baseInstructions ?? string.Empty;
        s_model = model;
    }

    /// <summary>Test hook: reset static config + depth counter.</summary>
    internal static void ResetForTests()
    {
        s_chatClient = null;
        s_baseInstructions = string.Empty;
        s_model = null;
        s_depth.Value = 0;
    }

    /// <summary>Test hook: read current depth (AsyncLocal).</summary>
    internal static int CurrentDepth => s_depth.Value;

    [Description("Delegate a subtask to a child agent. Use this to break complex tasks into smaller, focused sub-tasks. The child agent runs in-process and shares the parent's chat client; it receives only the tools you list.")]
    public static async Task<string> DelegateAsync(
        [Description("A clear, specific description of the subtask to delegate")] string task,
        [Description("Comma-separated list of tools to enable for the child agent (default: shell_exec,read_file,web_fetch,get_datetime). Options: shell_exec, read_file, web_fetch, get_datetime, get_clipboard")] string? tools = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(task))
            return "Error: parameter 'task' must not be empty.";

        if (s_chatClient is null)
            return "Error: delegate_task is not configured (no parent IChatClient available).";

        // Depth guard via AsyncLocal — replaces env-var marshalling.
        var currentDepth = s_depth.Value;
        if (currentDepth >= MaxDepth)
            return $"Error: maximum delegation depth ({MaxDepth}) reached. Complete this task directly instead of delegating.";

        // Allowlist contract: child gets *only* the tools named here, never the
        // parent's full registry. M5: default mirrors ToolRegistry.DefaultChildAgentTools
        // (excludes clipboard + nested delegate).
        IEnumerable<string> filtered = string.IsNullOrWhiteSpace(tools)
            ? ToolRegistry.DefaultChildAgentTools
            : tools.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var childTools = ToolRegistry.CreateMafTools(filtered);
        var childAgent = s_chatClient.AsAIAgent(
            instructions: s_baseInstructions,
            tools: childTools);

        s_depth.Value = currentDepth + 1;
        var output = new StringBuilder();
        int? inTok = null;
        int? outTok = null;
        try
        {
            await foreach (var update in childAgent.RunStreamingAsync(task, cancellationToken: ct))
            {
                if (!string.IsNullOrEmpty(update.Text))
                    output.Append(update.Text);

                if (update.Contents is not null)
                {
                    foreach (var c in update.Contents)
                    {
                        if (c is UsageContent uc && uc.Details is not null)
                        {
                            if (uc.Details.InputTokenCount is long i) inTok = (inTok ?? 0) + (int)i;
                            if (uc.Details.OutputTokenCount is long o) outTok = (outTok ?? 0) + (int)o;
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return output.Length == 0
                ? "Error: child agent cancelled."
                : output.ToString() + "\n[child agent cancelled]";
        }
        catch (Exception ex)
        {
            return output.Length == 0
                ? $"Error: child agent failed: {ex.Message}"
                : output.ToString() + $"\n[child agent error: {ex.Message}]";
        }
        finally
        {
            s_depth.Value = currentDepth;

            // Frank's telemetry surface: log the delegation. RecordRequest is a
            // no-op when telemetry is disabled, so zero overhead in the common path.
            if (Observability.Telemetry.IsEnabled)
            {
                Observability.Telemetry.RecordRequest(
                    s_model ?? "unknown",
                    inTok.GetValueOrDefault(),
                    outTok.GetValueOrDefault(),
                    "delegate");
            }
        }

        return output.Length == 0 ? "(child agent produced no output)" : output.ToString();
    }
}
