# Spec: Agent, Tool & Automation Interoperability + Startup Optimization

> Conforms to `.wgm/specs/CONSTITUTION.md`.

## JTBD (job to be done)

When developers, shell scripts, CI pipelines, or parent AI coding agents (Claude Code, GitHub Copilot CLI, Cursor, Codex, custom orchestrators) invoke `az-ai`, they need:

1. **Deterministic machine I/O** (`--json` happy-path envelope, `--schema <json>` wire enforcement, `--code` block extraction, and combined `stdin` + argument piping),
2. **Plug-and-play agent & tool extensibility** (`az-ai mcp serve` over stdio JSON-RPC 2.0, `--list-tools [--json]` introspection, and AOT-safe JSON manifest plugins from `~/.config/azureopenai-cli/tools/*.json` or opt-in `./.azureopenai-cli/tools/*.json`), and
3. **Lower latency & bounded agent execution** (shared `SocketsHttpHandler` connection pool between `PrewarmAsync` and `AzureOpenAIClient`, plus enforcing `--max-rounds` in agent mode).

## User-visible success criteria

- Running `echo "context" | az-ai "summarize"` merges bounded piped `stdin` and the positional argument into a single combined prompt instead of discarding `stdin`.
- Running `az-ai --json <prompt>` emits a single valid `ChatJsonResponse` JSON document on stdout (`response`, `model`, `mode`, `input_tokens`, `output_tokens`, `total_tokens`, `duration_ms`, `cached`) with zero human stderr noise.
- Running `az-ai --schema '<json-schema>' <prompt>` wires the parsed schema into `ChatOptions.ResponseFormat` (`ChatResponseFormat.ForJsonSchema`) so the model output conforms to the schema.
- Running `az-ai --code <prompt>` extracts fenced code blocks (or emits trimmed raw code when no fences are present) cleanly to stdout.
- Running `az-ai --list-tools` (and `az-ai --list-tools --json`) lists all built-in tools plus discovered custom manifest tools with their source (`builtin`, `user-plugin`, `local-plugin`, `custom-plugin`) and descriptions.
- Defining a JSON tool manifest in `~/.config/azureopenai-cli/tools/<name>.json` (or `--plugin-dir <dir>`, or `./.azureopenai-cli/tools/` with `--allow-local-plugins` / `AZ_ALLOW_LOCAL_PLUGINS=1`) registers the tool in `ToolRegistry` and executes it safely via argv (`ProcessStartInfo.ArgumentList`) with timeout, stdout cap, and credential-scrubbed environment.
- Running `az-ai mcp serve` starts a spec-compliant MCP stdio server (`JSON-RPC 2.0`) supporting `initialize`, `notifications/initialized`, `ping`, `tools/list`, and `tools/call` for safe default tools (`read_file`, `web_fetch`, `get_datetime`, `get_clipboard`, `delegate_task`, plus safe manifest tools), requiring `--allow-shell` (or explicit `--tools shell`) before exposing `shell_exec`.
- `PrewarmAsync` and `AzureOpenAIClient` share a `SocketsHttpHandler` connection pool, and `--max-rounds` is enforced on agent tool-call rounds.

## Magic moment

- **The whoa:** Any external agent (or shell one-liner) can pipe JSON-RPC into `az-ai mcp serve` or run `az-ai --list-tools --json` and immediately discover and execute both built-in tools and custom manifest plugins in ~10 ms cold start -- zero reflection, single binary.
- **Demo path:**
  1. Create a temporary plugin manifest `git_head.json` in a temp plugin dir.
  2. Run `az-ai --plugin-dir <dir> --list-tools --json` and verify JSON output includes built-in tools + `git_head` (`source: "custom-plugin"`).
  3. Pipe `initialize`, `tools/list`, and `tools/call` (calling `get_datetime` and `git_head`) into `az-ai mcp serve --plugin-dir <dir>` and verify valid JSON-RPC 2.0 responses on stdout and security refusal of `shell_exec` when `--allow-shell` is omitted.
- **Smallest end-to-end slice:** CLI flag parsing → manifest tool discovery → `--list-tools --json` → `az-ai mcp serve` stdio loop executing both built-in and manifest tools.
- **Merely functional vs magical:** Merely functional prints text that downstream scripts have to regex-parse; magical emits strict, AOT source-generated JSON and speaks standard MCP over stdio out of the box.

## Acceptance criteria → backpressure

| Tag | Criterion (EARS) | How it's verified (command/check) |
|---|---|---|
| `MODIFIED` | When both `stdin` is redirected with non-empty content and a positional prompt argument is provided, `az-ai` shall combine the bounded `stdin` content and positional prompt via `MergeStdinAndPrompt` rather than ignoring `stdin`. | `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj --filter "FullyQualifiedName~CombinedStdin"` |
| `MODIFIED` | When `--json` is passed on a chat or agent invocation, `az-ai` shall emit a single `ChatJsonResponse` JSON object on stdout and suppress human stderr status lines. | `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj --filter "FullyQualifiedName~JsonOutput"` |
| `MODIFIED` | When `--schema <json>` is passed, `BuildModernChatOptions` shall configure `ChatOptions.ResponseFormat` with `ChatResponseFormat.ForJsonSchema` using the validated JSON schema. | `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj --filter "FullyQualifiedName~SchemaEnforcement"` |
| `ADDED` | When `--code` is passed, `az-ai` shall extract code from markdown fenced blocks (or return the trimmed text if unfenced) and write only the code to stdout. | `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj --filter "FullyQualifiedName~CodeExtraction"` |
| `MODIFIED` | When `PrewarmAsync` and `CreateAzureOpenAIClient` run, they shall share a `SocketsHttpHandler` instance and `BuildModernChatOptions` shall configure `Temperature` and `MaxOutputTokenCount`, while agent tool calls enforce `MaxRounds`. | `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj --filter "FullyQualifiedName~Prewarm|FullyQualifiedName~MaxRounds"` |
| `ADDED` | Where custom JSON tool manifests exist in the user config dir, `--plugin-dir <path>`, or `./.azureopenai-cli/tools/` (with `--allow-local-plugins` or `AZ_ALLOW_LOCAL_PLUGINS=1`), `ManifestLoader` and `ToolRegistry` shall load them as `AITool` instances that execute via `ArgumentList` without shadowing built-in tools, and `--list-tools [--json]` shall list all tools and sources. | `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj --filter "FullyQualifiedName~ManifestPlugin"` |
| `ADDED` | When `az-ai mcp serve` (or `--mcp-serve`) is invoked, `McpStdioServer` shall process JSON-RPC 2.0 requests (`initialize`, `ping`, `tools/list`, `tools/call`) over stdin/stdout, expose safe tools + loaded manifest tools by default, and block `shell_exec` unless `--allow-shell` or `--tools shell` is specified. | `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj --filter "FullyQualifiedName~McpStdioServer"` |

| `ADDED` | When `--jsonl` is passed, `az-ai` shall emit compact single-line NDJSON streaming events (`delta`, `done`, `error`) on stdout/stderr via `McpJsonContext` / `AppJsonContext` and `--personas --json` shall emit a valid JSON array of configured personas. | `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj --filter "FullyQualifiedName~JsonlAndPersonasJsonTests"` |
| `ADDED` | When `--patterns [--json]` or `--pattern <name>` (`-p <name>`) is passed, `PatternRegistry` shall list or apply curated built-in and custom user patterns (`~/.config/azureopenai-cli/patterns/*.json`), and `az-ai mcp serve` shall expose patterns and personas via MCP `prompts/list` and `prompts/get`, plus squad state via `resources/list` and `resources/read`. | `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj --filter "FullyQualifiedName~PatternLibraryAndMcpPromptsTests"` |
| `ADDED` | When `--mcp-server <spec>` or `az-ai mcp tools <cmd>` / `az-ai mcp call <cmd> <tool> [json-args]` is invoked, `McpStdioClient` shall spawn the external MCP stdio server, run the `initialize` + `tools/list` handshake, and bridge remote MCP tools into `AITool` instances callable by `az-ai --agent` and `--ralph` (with `Content-Length` and NDJSON framing support in both client and server). | `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj --filter "FullyQualifiedName~McpStdioClientTests"` |
| `ADDED` | When `--provider <name>` (`AZ_PROVIDER`) or `--providers [--json]` is passed, `ProviderSelector` shall route requests to `azure` (default), `openai`, `openrouter`, `ollama`, `llamacpp`/`local`, or `custom`, enforcing `https://` for all remote hosts while permitting `http://` strictly on loopback (`localhost`, `127.0.0.1`, `::1`) for local providers. | `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj --filter "FullyQualifiedName~MultiProviderTests"` |
| `ADDED` | When `az-ai queue <init|add|list|status|plan|run|clear>` (or `az-ai commander ...`) is invoked, `WorkQueue` and `CommanderRunner` shall manage a crash-safe, atomic-write DAG task queue (`.az-ai/queue.json` or `--queue-file`), detect dependency cycles, support per-task `provider`/`model`/`persona`/`pattern`/`validate_command` routing, and expose `queue_add` and `queue_status` tools to agents and MCP. | `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj --filter "FullyQualifiedName~CommanderQueueTests"` |

- **Files:**
  - `.wgm/scenarios/tier1-machine-io-and-combined-stdin.yaml` (Tier 1)
  - `.wgm/scenarios/tier2-manifest-plugins-and-introspection.yaml` (Tier 2)
  - `.wgm/scenarios/tier3-mcp-stdio-server-and-security.yaml` (Tier 3)
- **Holdout rule:** Authored in Plan; never read during Implement; judged only in Validate/Review.

## Ruggedness (rugged gate)

- **Actual operators:** Individual developers, shell/CI automation authors, and parent AI agents (Claude Code, Copilot CLI, Cursor) invoking `az-ai` unattended over pipes or `stdio` MCP.
- **Actual environment:** Non-interactive pipes, redirected stdin/stdout, untrusted git repositories cloned locally, malformed JSON-RPC lines, hung or noisy subprocess plugins, and missing Azure credentials during offline tool introspection or local MCP tool calls.
- **Dominant risk bucket:** **Operational stress & boundary security** -- child processes hanging or leaking secrets (`AZUREOPENAIAPI`), repo-local plugins executing without opt-in, `shell_exec` exposed over MCP without opt-in, or non-JSON text polluting stdout during `--json` / `mcp serve`.
- **Simplification accepted:**
  - Implement MCP stdio server directly in-tree (~250 LOC using `AppJsonContext` source-generated JSON-RPC 2.0 records) rather(than) pulling a preview reflection-heavy NuGet SDK that risks Native AOT trim failures.
  - Execute manifest tools strictly via `ProcessStartInfo.ArgumentList` with `{placeholder}` argv substitution (never `/bin/sh -c`), scrubbing `AZUREOPENAIAPI`/`AZUREOPENAIENDPOINT` from the child process environment unless explicitly allowlisted.
- **Exact field/stress/recovery check:**
  - Command: `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj -c Release --nologo && dotnet publish azureopenai-cli/AzureOpenAI_CLI.csproj -c Release -r linux-x64 --no-restore -p:PublishAot=false`
  - Origin/Environment: Local Linux x64 `.NET 10` test harness including in-memory stdio JSON-RPC fuzzing (malformed JSON, unknown method, unauthorized `shell_exec`, plugin timeout/kill, local-plugin trust gate).
  - Expected observation: All unit + adversarial tests exit `0`, zero compiler/trimmer warnings (`0 Warning(s)`), and MCP server recovers cleanly from invalid JSON-RPC frames with standard JSON-RPC error codes (`-32700`, `-32601`, `-32602`) without crashing the stdio loop.
- **Plan-exit verdict:** **RUGGED** -- 2026-05-17 · produced by inline rubric (companion skill read-only protocol applied inline).

## Assumptions

- `--json` on chat/agent/ralph completions buffers the text and emits one `ChatJsonResponse` object on stdout so `jq` and callers can parse it deterministically.
- `az-ai mcp serve` works even when Azure OpenAI credentials are not set for local tools (`read_file`, `web_fetch`, `get_datetime`, `get_clipboard`, manifest plugins); tools that require Azure OpenAI (`delegate_task`) return a structured MCP tool error (`isError: true`) if credentials are absent.

## Out of scope (this pass)

- HTTP/SSE MCP transport (stdio only per FR-013 v1).
- WASI/Wasmtime `.wasm` plugin runtime (FR-012 Option C future work).
