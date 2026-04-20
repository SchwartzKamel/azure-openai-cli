using Microsoft.Extensions.AI;

namespace AzureOpenAI_CLI_V2.Tools;

/// <summary>
/// Registry of built-in tools available for agent mode in v2.
/// Uses Microsoft.Extensions.AI's AIFunctionFactory to generate AITool instances.
/// </summary>
internal static class ToolRegistry
{
    // Short alias → full tool name mapping for --tools CLI flag
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
    /// Create a list of AITool instances for MAF consumption.
    /// Pass null to enable all tools. Accepts full tool names or short aliases.
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

        var enabled = enabledTools?.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tools = new List<AITool>();

        foreach (var (name, factory) in allToolFactories)
        {
            if (enabled is null
                || enabled.Contains(name)
                || enabled.Any(e => ShortAliases.TryGetValue(e, out var fullName)
                                    && fullName.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                tools.Add(factory());
            }
        }

        return tools;
    }
}
