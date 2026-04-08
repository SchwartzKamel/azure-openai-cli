using System.Text.Json;

namespace AzureOpenAI_CLI.Tools;

/// <summary>
/// Interface for built-in tools that the model can invoke during agentic mode.
/// </summary>
internal interface IBuiltInTool
{
    string Name { get; }
    string Description { get; }
    BinaryData ParametersSchema { get; }
    Task<string> ExecuteAsync(JsonElement arguments, CancellationToken ct);
}
