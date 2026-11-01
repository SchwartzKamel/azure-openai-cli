# Review Ruggedness Record -- TZ (End-to-End Demo Validation)

date: 2026-05-17
phase: Review (TZ)
scope: Full test suite + live CLI demo path (--list-tools --json and az-ai mcp serve over stdio)
produced_by: inline rubric (companion /rugged protocol applied inline)

1. Actual operator & environment:
   Operators and parent agents running the compiled `az-ai` binary over real OS pipes (`--list-tools --json` and `az-ai mcp serve`).
2. Bottleneck decomposition:
   - Intrinsic design constraint: Native AOT / trimmed single-file execution with zero reflection.
   - User capacity: Single-command JSON output and stdio JSON-RPC 2.0 protocol.
   - Operational stress: Real process stdio pipes, EOF handling, and full 677-test regression suite.
3. Simplify:
   Direct stdio pipe execution without intermediary wrappers.
4. Exact check:
   `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj -c Release --nologo && dotnet run --project azureopenai-cli/AzureOpenAI_CLI.csproj -c Release --no-build -- --list-tools --json && printf '{"jsonrpc":"2.0","id":1,"method":"initialize"}\n{"jsonrpc":"2.0","id":2,"method":"tools/list"}\n{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"get_datetime","arguments":{"timezone":"UTC"}}}\n' | dotnet run --project azureopenai-cli/AzureOpenAI_CLI.csproj -c Release --no-build -- mcp serve`
5. Verdict:
   RUGGED
