# Plan-exit Ruggedness Record
date: 2026-05-17
phase: Plan-exit
scope: .wgm/specs/agent-automation-interop.md + .wgm/IMPLEMENTATION_PLAN.md
produced_by: inline rubric (companion /rugged protocol applied inline)

1. Actual operator & environment:
   Developers, shell scripts, CI pipelines, and parent AI agents (Claude Code, GitHub Copilot CLI, Cursor) running `az-ai` unattended over non-interactive pipes and `stdio` MCP in environments that may have untrusted repo-local files, malformed JSON-RPC inputs, or missing Azure credentials during local tool discovery.
2. Bottleneck decomposition:
   - Intrinsic design constraint: Native AOT (`TrimMode=full`) forbids reflection-based JSON serialization or dynamic `.dll` plugin loading.
   - User capacity: Operators piping `--json` or configuring `az-ai mcp serve` expect zero stdout pollution and safe defaults without reading a 20-page security manual.
   - Operational stress (dominant): Subprocess tools hanging/leaking `AZUREOPENAIAPI`, repo-local plugins executing without opt-in, `shell_exec` exposed over MCP without opt-in, or invalid JSON-RPC frames crashing the server loop.
3. Simplify:
   Implement MCP stdio (`initialize`, `ping`, `tools/list`, `tools/call`) directly in-tree (~250 LOC) backed by `AppJsonContext` source-generated records rather than pulling a reflection-heavy external MCP NuGet dependency; execute manifest plugins strictly via `ProcessStartInfo.ArgumentList` with scrubbed env vars and hard timeouts.
4. Exact check:
   `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj -c Release --nologo` testing combined stdin+args, `--json` envelope, `--schema` wire option, `--code` extraction, shared `SocketsHttpHandler`, `--max-rounds` cap, manifest plugin argv/env isolation/trust gates, and `McpStdioServer` JSON-RPC error recovery and `--allow-shell` gating.
5. Verdict:
   RUGGED
