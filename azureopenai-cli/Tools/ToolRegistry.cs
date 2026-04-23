using Microsoft.Extensions.AI;

namespace AzureOpenAI_CLI.Tools;

/// <summary>
/// Registry of built-in tools available for agent mode in v2.
/// Uses Microsoft.Extensions.AI's AIFunctionFactory to generate AITool instances.
/// </summary>
internal static class ToolRegistry
{
    /// <summary>
    /// Short alias → canonical full tool name. Kramer audit M4: resolved once at
    /// the <see cref="CreateMafTools"/> entry so downstream code works on
    /// canonical names only.
    /// </summary>
    private static readonly Dictionary<string, string> ShortAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["shell"] = "shell_exec",
        ["file"] = "read_file",
        ["web"] = "web_fetch",
        ["clipboard"] = "get_clipboard",
        ["datetime"] = "get_datetime",
        ["delegate"] = "delegate_task",
    };

    /// <summary>
    /// Kramer audit M5: canonical default tool set used by agent mode when the
    /// user does not pass <c>--tools</c>. Referenced by <c>Program.RunAsync</c>
    /// (Ralph mode default) and <see cref="DelegateTaskTool"/> (child-agent default)
    /// so the list has exactly one source of truth. Order doesn't matter for
    /// behavior; sorted for readability.
    /// </summary>
    internal static readonly IReadOnlyList<string> DefaultAgentTools = new[]
    {
        "shell_exec",
        "read_file",
        "web_fetch",
        "get_datetime",
        "delegate_task",
    };

    /// <summary>
    /// Default tool set for a <em>child</em> agent spawned via <c>delegate_task</c>.
    /// Intentionally omits <c>delegate_task</c> to discourage runaway recursion
    /// even though <see cref="DelegateTaskTool.MaxDepth"/> already caps it.
    /// Clipboard is also excluded — child agents don't get to touch the GUI surface.
    /// </summary>
    internal static readonly IReadOnlyList<string> DefaultChildAgentTools = new[]
    {
        "shell_exec",
        "read_file",
        "web_fetch",
        "get_datetime",
    };

    /// <summary>
    /// Canonicalize a user-supplied tool name (short alias or full name) to its
    /// canonical full name. Returns the input unchanged when it's already a full
    /// name or an unknown token (unknown tokens are silently ignored downstream).
    /// </summary>
    internal static string Canonicalize(string name)
        => ShortAliases.TryGetValue(name, out var full) ? full : name;

    /// <summary>
    /// Create a list of AITool instances for MAF consumption.
    /// Pass null to enable all tools. Accepts full tool names or short aliases —
    /// both are canonicalized once at entry, then matched by full name only.
    /// </summary>
    public static IList<AITool> CreateMafTools(IEnumerable<string>? enabledTools = null)
    {
        var allToolFactories = new Dictionary<string, Func<AITool>>(StringComparer.OrdinalIgnoreCase)
        {
            ["shell_exec"] = () => AIFunctionFactory.Create(ShellExecTool.ExecuteAsync, name: "shell_exec"),
            ["read_file"] = () => AIFunctionFactory.Create(ReadFileTool.ReadAsync, name: "read_file"),
            ["web_fetch"] = () => AIFunctionFactory.Create(WebFetchTool.FetchAsync, name: "web_fetch"),
            ["get_clipboard"] = () => AIFunctionFactory.Create(GetClipboardTool.GetAsync, name: "get_clipboard"),
            ["get_datetime"] = () => AIFunctionFactory.Create(GetDateTimeTool.GetAsync, name: "get_datetime"),
            ["delegate_task"] = () => AIFunctionFactory.Create(DelegateTaskTool.DelegateAsync, name: "delegate_task"),
        };

        // M4: canonicalize once. Downstream match is full-name only.
        HashSet<string>? enabled = null;
        if (enabledTools is not null)
        {
            enabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in enabledTools)
            {
                if (!string.IsNullOrWhiteSpace(t))
                    enabled.Add(Canonicalize(t.Trim()));
            }
        }

        var tools = new List<AITool>();
        foreach (var (name, factory) in allToolFactories)
        {
            if (enabled is null || enabled.Contains(name))
                tools.Add(factory());
        }
        return tools;
    }
}
