# Review Ruggedness Record -- T7

date: 2026-05-17
phase: Review (T7)
scope: Outbound MCP Stdio Client (FR-013 Phase 1 --mcp-server, az-ai mcp tools, az-ai mcp call) & Dual Framing (Content-Length + NDJSON)
produced_by: inline rubric (companion /rugged protocol applied inline)

1. Actual operator & environment:
   Developers and agent workflows connecting `az-ai` to external MCP servers (`--mcp-server`, `az-ai mcp tools`, `az-ai mcp call`) or connecting strict `Content-Length`-framed MCP clients to `az-ai mcp serve`.
2. Bottleneck decomposition:
   - Intrinsic design constraint: Native AOT (`TrimMode=full`) requires all client/server JSON-RPC 2.0 request and response records to be source-generated in `McpJsonContext`.
   - User capacity: Users want a single `--mcp-server "fs=npx ..."` flag that automatically bridges remote MCP tools into `--agent` / `--ralph`.
   - Operational stress (dominant): Child MCP server processes hanging on startup, leaking `AZUREOPENAIAPI`, or sending `Content-Length` headers instead of newline-delimited JSON.
3. Simplify:
   Unified `McpStdioServer.ReadNextFrameAsync` (auto-detecting `Content-Length: N` vs NDJSON lines with a 4 MB frame cap), credential scrubbing on child `ProcessStartInfo.Environment`, bounded startup timeout with `Kill(entireProcessTree: true)` on `Dispose()`, and thread-safe `SemaphoreSlim` I/O serialization in `McpStdioClient`.
4. Exact check:
   `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj -c Release --filter "FullyQualifiedName~McpStdioClientTests" --nologo` (4/4 passed, including bidirectional pipe dogfooding of `McpStdioClient` <-> `McpStdioServer`).
5. Verdict:
   RUGGED
