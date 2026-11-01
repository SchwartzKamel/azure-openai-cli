# Review Ruggedness Record -- T4

date: 2026-05-17
phase: Review (T4)
scope: MCP Stdio Server (az-ai mcp serve / --mcp-serve) & Documentation Update
produced_by: inline rubric (companion /rugged protocol applied inline)

1. Actual operator & environment:
   External AI agents (Claude Code, GitHub Copilot CLI, Cursor, Codex) spawning `az-ai mcp serve` over `stdio` JSON-RPC 2.0, potentially without Azure credentials when invoking local tools/plugins, and potentially sending malformed JSON frames or attempting to call `shell_exec`.
2. Bottleneck decomposition:
   - Intrinsic design constraint: MCP stdio requires compact single-line NDJSON (`WriteIndented = false` in `McpJsonContext`) and zero reflection for Native AOT.
   - User capacity: Operators configuring `claude_mcp_config.json` want safe defaults (`read_file`, `web_fetch`, `get_datetime`, `get_clipboard`, `delegate_task`, safe plugins) without accidental RCE exposure.
   - Operational stress (dominant): Malformed JSON-RPC lines or unauthorized `shell_exec` calls terminating the stdio server or executing arbitrary shell commands.
3. Simplify:
   Dedicated AOT-safe `McpJsonContext` (`WriteIndented = false`) + line-by-line JSON-RPC 2.0 loop in `McpStdioServer.cs` that isolates per-frame parse/method errors (`-32700`, `-32601`, `-32602`) and requires `--allow-shell` (or `--tools shell`) before exposing `shell_exec` or `dangerous: true` plugins.
4. Exact check:
   `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj -c Release --filter "FullyQualifiedName~McpStdioServerTests" --nologo` (5/5 passed).
5. Verdict:
   RUGGED
