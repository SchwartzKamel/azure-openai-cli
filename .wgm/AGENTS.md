# AGENTS.md (.wgm operational guide)

> Operational guide only -- how to build, run, and validate `azure-openai-cli` (`az-ai`).

## Build & run

```bash
dotnet build azureopenai-cli/AzureOpenAI_CLI.csproj -c Release
dotnet run --project azureopenai-cli/AzureOpenAI_CLI.csproj -- --help
```

## Validate (backpressure)

```bash
dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj -c Release --nologo
```

- The loop is not done on a task until `dotnet test` exits `0` and all new + existing tests pass.

## Operational notes

- Source code lives in `azureopenai-cli/`; tests live in `tests/AzureOpenAI_CLI.Tests/`.
- All JSON types must be registered on `AppJsonContext` in `azureopenai-cli/JsonGenerationContext.cs` for Native AOT compatibility.
- Tests that capture `Console.Out` or `Console.Error` must use `[Collection("ConsoleCapture")]` to prevent parallel test pollution.

## Codebase patterns

- `Program.ParseArgs(string[] args)` parses flags into `Program.CliOptions` (seeded from `Program.DefaultOptions()`).
- `Program.BuildModernChatOptions(...)` constructs `Microsoft.Extensions.AI.ChatOptions` for MAF (`AsAIAgent`) calls.
- `ToolRegistry.CreateMafTools(...)` builds `IList<AITool>` for agent mode and MCP server exposure.
