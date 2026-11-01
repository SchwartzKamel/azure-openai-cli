# Review Ruggedness Record -- T6
date: 2026-05-17
phase: Review (T6)
scope: Curated Pattern Library (FR-015 --pattern/-p, --patterns [--json]) & MCP Prompts/Resources (prompts/list, prompts/get, resources/list, resources/read)
produced_by: inline rubric (companion /rugged protocol applied inline)

1. Actual operator & environment:
   Developers and shell scripts running `git diff | az-ai -p commit-message` or external MCP agent hosts querying `prompts/list`, `prompts/get`, `resources/list`, and `resources/read` over `az-ai mcp serve`.
2. Bottleneck decomposition:
   - Intrinsic design constraint: Native AOT requires `PatternTemplate`, `McpPromptsListResult`, `McpGetPromptResult`, `McpResourcesListResult`, and `McpReadResourceResult` to be source-generated in `AppJsonContext` / `McpJsonContext`.
   - User capacity: Users want built-in patterns ready out of the box plus easy JSON customization in `~/.config/azureopenai-cli/patterns/*.json`.
   - Operational stress (dominant): Path traversal via `resources/read` (`squad://memory/../../etc/passwd`) or malformed user pattern JSON files.
3. Simplify:
   Delegate `squad://memory/<name>` reads through `PersonaMemory.ReadHistory` (which enforces strict `[a-z0-9_-]{1,64}` regex and path-containment verification) and return JSON-RPC `-32602` on any invalid URI or traversal attempt.
4. Exact check:
   `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj -c Release --filter "FullyQualifiedName~PatternLibraryAndMcpPromptsTests" --nologo` (4/4 passed, including path traversal rejection).
5. Verdict:
   RUGGED
