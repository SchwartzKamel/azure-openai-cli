using System.Text.Json;
using OpenAI.Chat;

namespace AzureOpenAI_CLI.Tools;

/// <summary>
/// Registry of built-in tools available for agentic mode.
/// Maps tool names to implementations and generates ChatTool definitions.
/// </summary>
internal sealed class ToolRegistry
{
    private readonly Dictionary<string, IBuiltInTool> _tools = new(StringComparer.OrdinalIgnoreCase);

    public void Register(IBuiltInTool tool) => _tools[tool.Name] = tool;

    public IReadOnlyCollection<IBuiltInTool> All => _tools.Values;

    public IBuiltInTool? Get(string name) =>
        _tools.TryGetValue(name, out var tool) ? tool : null;

    /// <summary>
    /// Generate ChatTool definitions for the Azure OpenAI API.
    /// </summary>
    public List<ChatTool> ToChatTools() =>
        _tools.Values.Select(t => ChatTool.CreateFunctionTool(
            t.Name, t.Description, t.ParametersSchema)).ToList();

    /// <summary>
    /// Execute a tool call from the model response.
    /// </summary>
    public async Task<string> ExecuteAsync(string toolName, string argumentsJson, CancellationToken ct)
    {
        var tool = Get(toolName);
        if (tool is null)
            return $"Error: unknown tool '{toolName}'";

        try
        {
            var args = string.IsNullOrEmpty(argumentsJson)
                ? new JsonElement()
                : JsonDocument.Parse(argumentsJson).RootElement;
            return await tool.ExecuteAsync(args, ct);
        }
        catch (OperationCanceledException)
        {
            return $"Error: tool '{toolName}' timed out";
        }
        catch (Exception ex)
        {
            return $"Error executing '{toolName}': {ex.Message}";
        }
    }

    // Short alias → full tool name mapping for --tools CLI flag
    private static readonly Dictionary<string, string> ShortAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["shell"] = "shell_exec",
        ["file"] = "read_file",
        ["web"] = "web_fetch",
        ["clipboard"] = "get_clipboard",
        ["datetime"] = "get_datetime",
    };

    /// <summary>
    /// Create a registry with the specified tool names enabled.
    /// Pass null to enable all tools. Accepts full tool names or short aliases.
    /// </summary>
    public static ToolRegistry Create(IEnumerable<string>? enabledTools = null)
    {
        var registry = new ToolRegistry();
        var allTools = new IBuiltInTool[]
        {
            new ShellExecTool(),
            new ReadFileTool(),
            new WebFetchTool(),
            new GetClipboardTool(),
            new GetDateTimeTool(),
        };

        var enabled = enabledTools?.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var tool in allTools)
        {
            if (enabled is null
                || enabled.Contains(tool.Name)
                || enabled.Any(e => ShortAliases.TryGetValue(e, out var fullName)
                                    && fullName.Equals(tool.Name, StringComparison.OrdinalIgnoreCase)))
                registry.Register(tool);
        }

        return registry;
    }
}
