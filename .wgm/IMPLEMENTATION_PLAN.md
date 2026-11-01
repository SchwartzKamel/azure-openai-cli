# Implementation plan

> The shared state of the loop. Prioritized; the agent always takes the most important `pending`
> task. Update every iteration so a fresh agent could resume from this file alone.
> Status values: `pending | in_progress | done | blocked`.

## Convergence

- **Satisfaction threshold:** 95 -- overall holdout-scenario score to reach before Ship.
- **Stratified order:** converge tier 1 → 2 → 3 (see `references/scoring.md`).
- **Scenarios:** `.wgm/scenarios/` (judged in Validate/Review; never read in Implement).

## Ruggedness gate (required -- every track)

Exactly one verdict per run; record it here, not just in a transcript.

| When | Scope | Verdict | Date | Produced by | Follow-up task |
|---|---|---|---|---|---|
| Plan-exit | `.wgm/specs/agent-automation-interop.md` + `.wgm/IMPLEMENTATION_PLAN.md` | RUGGED | 2026-05-17 | inline rubric (`/rugged` protocol inline) | none |
| Review (T1) | T1 diff (`Program.cs`, `JsonGenerationContext.cs`, `RalphWorkflow.cs`, `UserConfig.cs`, `MachineIoAndCombinedStdinTests.cs`) | RUGGED | 2026-05-17 | inline rubric (`/rugged` protocol inline) | none |
| Review (T2) | T2 diff (`Program.cs`, `ToolRegistry.cs`, `StartupAndMaxRoundsTests.cs`) | RUGGED | 2026-05-17 | inline rubric (`/rugged` protocol inline) | none |
| Review (T3) | T3 diff (`ManifestTool.cs`, `ToolRegistry.cs`, `JsonGenerationContext.cs`, `Program.cs`, `ManifestPluginAndIntrospectionTests.cs`) | RUGGED | 2026-05-17 | inline rubric (`/rugged` protocol inline) | none |
| Review (T4) | T4 diff (`McpStdioServer.cs`, `JsonGenerationContext.cs`, `Program.cs`, `README.md`, `McpStdioServerTests.cs`) | RUGGED | 2026-05-17 | inline rubric (`/rugged` protocol inline) | none |
| Review (TZ) | End-to-end demo validation (`677/677` xUnit tests + live `--list-tools --json` & `az-ai mcp serve`) | RUGGED | 2026-05-17 | inline rubric (`/rugged` protocol inline) | none |
| Plan-exit (Wave 2) | T5 (`--jsonl` + `--personas --json`), T6 (`PatternRegistry` + MCP Prompts/Resources), T7 (`McpStdioClient` + Content-Length framing) | RUGGED | 2026-05-17 | inline rubric (`/rugged` protocol inline) | none |
| Review (T5) | T5 diff (`Program.cs`, `JsonGenerationContext.cs`, `JsonlAndMachineDiscoveryTests.cs`) | RUGGED | 2026-05-17 | inline rubric (`/rugged` protocol inline) | none |
| Review (T6) | T6 diff (`PatternRegistry.cs`, `McpStdioServer.cs`, `JsonGenerationContext.cs`, `Program.cs`, `PatternLibraryAndMcpPromptsTests.cs`) | RUGGED | 2026-05-17 | inline rubric (`/rugged` protocol inline) | none |
| Review (T7) | T7 diff (`McpStdioClient.cs`, `McpStdioServer.cs`, `JsonGenerationContext.cs`, `Program.cs`, `RalphWorkflow.cs`, `McpStdioClientTests.cs`) | RUGGED | 2026-05-17 | inline rubric (`/rugged` protocol inline) | none |
| Plan-exit (Wave 3) | T8 (Multi-Provider Router) + T9 (Commander Work Queue Mode & MCP Queue Tools) | RUGGED | 2026-05-17 | inline rubric (`/rugged` protocol inline) | none |
| Review (T8) | T8 diff (`ProviderSelector.cs`, `UserConfig.cs`, `CostHook.cs`, `Program.cs`, `MultiProviderTests.cs`) | RUGGED | 2026-05-17 | inline rubric (`/rugged` protocol inline) | none |
| Review (T9) | T9 diff (`WorkQueue.cs`, `CommanderRunner.cs`, `QueueTools.cs`, `ToolRegistry.cs`, `Program.cs`, `CommanderQueueTests.cs`) | RUGGED | 2026-05-17 | inline rubric (`/rugged` protocol inline) | none |

## Now (next up)

### T1 -- Complete Machine I/O Contract (`--json` happy-path envelope, `--schema` wire enforcement, `--code` extraction) & Combined Stdin+Args Piping

- **files/areas:** `azureopenai-cli/Program.cs`, `azureopenai-cli/JsonGenerationContext.cs`, `azureopenai-cli/Ralph/RalphWorkflow.cs`, `tests/AzureOpenAI_CLI.Tests/MachineIoAndCombinedStdinTests.cs`
- **validation:** `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj -c Release --filter "FullyQualifiedName~MachineIoAndCombinedStdinTests"`
- **acceptance:**
  1. When stdin is redirected and non-empty AND a positional prompt is supplied, `MergeStdinAndPrompt(stdinContent, prompt)` combines them (`<context>\n{stdin}\n</context>\n\n{prompt}`) while honoring the 1 MB `MAX_STDIN_BYTES` cap.
  2. `ChatJsonResponse` (`response`, `model`, `mode`, `input_tokens`, `output_tokens`, `total_tokens`, `duration_ms`, `cached`) is registered in `AppJsonContext` and emitted on stdout when `--json` is passed in standard, cache-hit, agent, or Ralph mode, with human stderr status lines suppressed.
  3. `--schema <json>` wires `ChatResponseFormat.ForJsonSchema` into `BuildModernChatOptions` along with `Temperature` and `MaxOutputTokenCount`.
  4. `--code` flag (`opts.Code`) extracts fenced markdown code blocks (or emits trimmed text when unfenced) via `ExtractCodeBlocks(string text)` on stdout and is included in shell completions and `--help`.
- **scenarios/tier:** `tier1-machine-io-and-combined-stdin.yaml` (Tier 1)
- **rugged:** RUGGED (2026-05-17, `.wgm/rugged/review-t1.md`)
- **status:** done
- **notes:** Implemented `ChatJsonResponse` in `AppJsonContext`, `MergeStdinAndPrompt` with `HasRedirectedStdinDataAvailable`, `ExtractCodeBlocks` (`--code`), and wired `--schema` (`ChatResponseFormat.ForJsonSchema`) + `Temperature` + `MaxOutputTokens` in `BuildModernChatOptions`. Fixed `UserConfig.DefaultPath` to honor `HOME` override in tests. 11/11 new tests + 664/664 total tests green.

### T2 -- Startup & Runtime Optimization: Shared `SocketsHttpHandler` Prewarm Pool & Agent `--max-rounds` Cap Enforcement

- **files/areas:** `azureopenai-cli/Program.cs`, `azureopenai-cli/Tools/ToolRegistry.cs`, `tests/AzureOpenAI_CLI.Tests/StartupAndMaxRoundsTests.cs`, `tests/AzureOpenAI_CLI.Tests/PrewarmTests.cs`
- **validation:** `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj -c Release --filter "FullyQualifiedName~StartupAndMaxRoundsTests|FullyQualifiedName~PrewarmTests"`
- **acceptance:**
  1. `Program.SharedSocketsHandler` / `Program.SharedHttpClient` is used by both `PrewarmAsync` and `CreateAzureOpenAIClient` (via `HttpClientPipelineTransport`) so TLS prewarming warms the exact connection pool used by chat completions.
  2. `--max-rounds` (`opts.MaxRounds`) is enforced during agent tool execution so a runaway tool-calling model cannot exceed `opts.MaxRounds` tool rounds, returning a clear boundary message to the model when the cap is reached.
  3. Existing `PrewarmTests` and `Fr017RegressionTests` continue to pass.
- **scenarios/tier:** `tier1-machine-io-and-combined-stdin.yaml` (Tier 1)
- **rugged:** RUGGED (2026-05-17, `.wgm/rugged/review-t2.md`)
- **status:** done
- **notes:** Added `SharedSocketsHandler`, `SharedHttpClient`, and `CreateAzureOpenAIClient` using `HttpClientPipelineTransport`, plus `ToolRoundCounter` and `MaxRoundsAIFunction` in `ToolRegistry.cs`. 13/13 tests in `StartupAndMaxRoundsTests` + `PrewarmTests` green.

### T3 -- Custom Manifest Tool Plugins (FR-012) & Tool Introspection (`--list-tools [--json]`)

- **files/areas:** `azureopenai-cli/Tools/ManifestTool.cs`, `azureopenai-cli/Tools/ToolRegistry.cs`, `azureopenai-cli/Program.cs`, `azureopenai-cli/JsonGenerationContext.cs`, `tests/AzureOpenAI_CLI.Tests/ManifestPluginAndIntrospectionTests.cs`
- **validation:** `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj -c Release --filter "FullyQualifiedName~ManifestPluginAndIntrospectionTests"`
- **acceptance:**
  1. `ToolManifest` and `ToolDescriptor` records are registered in `AppJsonContext` (100% Native AOT compatible).
  2. `ManifestLoader` discovers `*.json` manifests from `$XDG_CONFIG_HOME/azureopenai-cli/tools/` (or `~/.config/azureopenai-cli/tools/`), optional `--plugin-dir <path>`, and repo-local `./.azureopenai-cli/tools/` ONLY when `--allow-local-plugins` or `AZ_ALLOW_LOCAL_PLUGINS=1` is set; `--no-plugins` disables all manifest loading.
  3. Built-in tool names (`shell_exec`, `read_file`, `web_fetch`, `get_clipboard`, `get_datetime`, `delegate_task` and short aliases) always win on name collision and cannot be shadowed by a manifest.
  4. `ManifestTool` executes commands via `ProcessStartInfo.ArgumentList` with `{placeholder}` argv substitution (never `/bin/sh -c`), enforces `timeout_seconds` (killing process tree on timeout) and `stdout_limit_bytes` (default 64 KB), and scrubs `AZUREOPENAIAPI` / `AZUREOPENAIENDPOINT` from the child environment unless explicitly listed in `env_passthrough`.
  5. `--list-tools` (and `--list-tools --json`) lists all built-in and active manifest tools with `name`, `alias`, `source`, `description`, and `dangerous` metadata, short-circuiting before credential checks.
- **scenarios/tier:** `tier2-manifest-plugins-and-introspection.yaml` (Tier 2)
- **rugged:** RUGGED (2026-05-17, `.wgm/rugged/review-t3.md`)
- **status:** done
- **notes:** Implemented `ToolManifest`, `ToolDescriptor`, `ManifestLoader`, and `ManifestAIFunction` in `azureopenai-cli/Tools/ManifestTool.cs`, wired into `ToolRegistry`, `AppJsonContext`, and `Program.ListToolsCommand`. 4/4 tests in `ManifestPluginAndIntrospectionTests` green.

### T4 -- MCP Stdio Server (`az-ai mcp serve` / `--mcp-serve`) & Documentation Update

- **files/areas:** `azureopenai-cli/Mcp/McpStdioServer.cs`, `azureopenai-cli/Program.cs`, `azureopenai-cli/JsonGenerationContext.cs`, `README.md`, `tests/AzureOpenAI_CLI.Tests/McpStdioServerTests.cs`
- **validation:** `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj -c Release --filter "FullyQualifiedName~McpStdioServerTests"`
- **acceptance:**
  1. `az-ai mcp serve` (and `--mcp-serve`) starts an AOT-safe JSON-RPC 2.0 server over `stdin`/`stdout` supporting `initialize`, `notifications/initialized`, `ping`, `tools/list`, and `tools/call`.
  2. By default, `mcp serve` exposes safe built-in tools (`read_file`, `web_fetch`, `get_datetime`, `get_clipboard`, `delegate_task`) plus loaded manifest tools; `shell_exec` (and any manifest tool with `"dangerous": true`) is excluded unless `--allow-shell` (or explicit `--tools shell`) is passed; `--read-only` restricts to `read_file`, `web_fetch`, `get_datetime`, `get_clipboard`.
  3. `tools/call` invokes the target tool and returns MCP content items (`[{ "type": "text", "text": "..." }]`) with `isError: false` on success or `isError: true` on tool error/unauthorized tool.
  4. Malformed JSON or unknown JSON-RPC methods return standard JSON-RPC 2.0 error responses (`-32700`, `-32601`, `-32602`) without terminating the server loop.
  5. `README.md` and `--help` / completions document `--json`, `--code`, `--list-tools`, `--plugin-dir`, `--allow-local-plugins`, `--no-plugins`, and `az-ai mcp serve`.
- **scenarios/tier:** `tier3-mcp-stdio-server-and-security.yaml` (Tier 3)
- **rugged:** RUGGED (2026-05-17, `.wgm/rugged/review-t4.md`)
- **status:** done
- **notes:** Implemented `McpStdioServer` and `McpJsonContext` (`WriteIndented = false` for NDJSON stdio framing), wired `az-ai mcp serve` / `--mcp-serve` / `--allow-shell` / `--read-only` in `Program.cs`, updated `README.md`, and verified 5/5 tests in `McpStdioServerTests`.

### T5 -- NDJSON Event Streaming (`--jsonl`) & Machine-Readable Personas (`--personas --json`, `--models --json`)

- **files/areas:** `azureopenai-cli/Program.cs`, `azureopenai-cli/JsonGenerationContext.cs`, `azureopenai-cli/Ralph/RalphWorkflow.cs`, `tests/AzureOpenAI_CLI.Tests/JsonlAndMachineDiscoveryTests.cs`
- **validation:** `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj -c Release --filter "FullyQualifiedName~JsonlAndMachineDiscoveryTests"`
- **acceptance:**
  1. `--jsonl` flag (`opts.Jsonl`) emits single-line NDJSON events (`{"type":"delta","text":"..."}` followed by `{"type":"done","response":"...","model":"...","mode":"...","input_tokens":N,"output_tokens":N,"duration_ms":N}`) on stdout via `McpJsonContext` (compact single-line, zero reflection).
  2. `--personas --json` emits a valid JSON array of `PersonaConfig` objects on stdout, and `--models --json` emits the configured alias map as JSON on stdout.
- **scenarios/tier:** `tier3-wave2-mcp-client-patterns-jsonl.yaml` (Tier 3)
- **rugged:** RUGGED (2026-05-17, `.wgm/rugged/review-t5.md`)
- **status:** done
- **notes:** Added `JsonlStreamEvent` in `McpJsonContext`, `--jsonl` delta/done streaming in `Program.cs`, and `--personas --json` / `--models --json`. 4/4 tests in `JsonlAndMachineDiscoveryTests` green.

### T6 -- Curated Pattern Library (FR-015 `--pattern` / `-p`, `--patterns [--json]`) & MCP Prompts/Resources (`prompts/list`, `prompts/get`, `resources/list`, `resources/read`)

- **files/areas:** `azureopenai-cli/Patterns/PatternRegistry.cs`, `azureopenai-cli/Mcp/McpStdioServer.cs`, `azureopenai-cli/JsonGenerationContext.cs`, `azureopenai-cli/Program.cs`, `tests/AzureOpenAI_CLI.Tests/PatternLibraryAndMcpPromptsTests.cs`
- **validation:** `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj -c Release --filter "FullyQualifiedName~PatternLibraryAndMcpPromptsTests"`
- **acceptance:**
  1. `PatternRegistry` ships 8 built-in automation/developer patterns (`summarize`, `fix-grammar`, `commit-message`, `explain-code`, `shell-oneliner`, `json-extract`, `code-review`, `security-audit`) and loads user-defined `*.json` patterns from `$XDG_CONFIG_HOME/azureopenai-cli/patterns/` or `~/.config/azureopenai-cli/patterns/`.
  2. `--patterns` (and `--patterns --json`) lists all built-in + custom patterns short-circuiting before credentials; `--pattern <name>` / `-p <name>` applies the pattern's system prompt (and optional default temperature).
  3. `McpStdioServer` advertises `prompts` and `resources` capabilities in `initialize`, serves all patterns + squad personas via `prompts/list` and `prompts/get`, and serves `.squad.json` + `.squad/history/*.md` via `resources/list` and `resources/read` (read-only, path-sanitized).
- **scenarios/tier:** `tier3-wave2-mcp-client-patterns-jsonl.yaml` (Tier 3)
- **rugged:** RUGGED (2026-05-17, `.wgm/rugged/review-t6.md`)
- **status:** done
- **notes:** Implemented `PatternRegistry` (8 built-in patterns + user `~/.config/azureopenai-cli/patterns/*.json` loader), `--patterns [--json]`, `--pattern` / `-p`, and MCP `prompts/list`, `prompts/get`, `resources/list`, `resources/read`. 4/4 tests in `PatternLibraryAndMcpPromptsTests` green.

### T7 -- Outbound MCP Stdio Client (FR-013 Phase 1 `--mcp-server`, `az-ai mcp tools`, `az-ai mcp call`) & Dual Framing (`Content-Length` + NDJSON)

- **files/areas:** `azureopenai-cli/Mcp/McpStdioClient.cs`, `azureopenai-cli/Mcp/McpStdioServer.cs`, `azureopenai-cli/Tools/ToolRegistry.cs`, `azureopenai-cli/Program.cs`, `README.md`, `tests/AzureOpenAI_CLI.Tests/McpStdioClientTests.cs`
- **validation:** `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj -c Release --filter "FullyQualifiedName~McpStdioClientTests"`
- **acceptance:**
  1. `McpStdioServer` auto-detects and supports both newline-delimited JSON-RPC and `Content-Length: N\r\n\r\n{...}` header-framed JSON-RPC so every MCP host and SDK works seamlessly.
  2. `McpStdioClient` spawns an external MCP stdio server command, scrubs sensitive parent env vars by default, runs `initialize` + `notifications/initialized`, enumerates `tools/list`, invokes `tools/call` with bounded timeout, and bridges remote tools as `AITool` (`McpClientAIFunction`) into `az-ai --agent` and `--ralph` when `--mcp-server <command>` is passed.
  3. `az-ai mcp tools "<command>"` (supports `--json`) and `az-ai mcp call "<command>" <tool> [<json-args>]` allow scripts and operators to inspect and call any MCP server directly from the CLI.
  4. Dogfood test connects `McpStdioClient` directly to `az-ai mcp serve` over pipes and executes a tool end-to-end.
- **scenarios/tier:** `tier3-wave2-mcp-client-patterns-jsonl.yaml` (Tier 3)
- **rugged:** RUGGED (2026-05-17, `.wgm/rugged/review-t7.md`)
- **status:** done
- **notes:** Implemented `McpStdioClient` + `McpClientAIFunction` in `azureopenai-cli/Mcp/McpStdioClient.cs`, added `Content-Length` + NDJSON dual framing in `McpStdioServer.ReadNextFrameAsync`, wired `--mcp-server`, `az-ai mcp tools`, and `az-ai mcp call` in `Program.cs` and `RalphWorkflow.cs`. 4/4 tests in `McpStdioClientTests` green.

### T8 [SUPERSEDED] -- Multi-Provider Router (`--provider`, `AZ_PROVIDER`, `--providers [--json]`, `--endpoint`, `--api-key`) with Loopback Security Guard

- **superseded (2026-05-17, reconcile onto origin/main):** `origin/main` gained its own provider stack (`PreferencesResolver`, `OpenAiCompatAdapter`, `EndpointAllowlist`, `CapabilityGate`, `--provider`/`--profile`, ADR-010/011) after this task was built. `ProviderSelector`, `MultiProviderTests`, `--providers`, `--endpoint`, `--api-key` and `default_provider` were dropped rather than maintain two provider stacks. T9's queue now routes per task through `Program.BuildChatClient`, so offline mode, the endpoint allowlist and the capability gate apply to queued work. The RUGGED verdicts below describe the pre-reconcile code and are history, not current claims.
- **files/areas:** `azureopenai-cli/Providers/ProviderSelector.cs`, `azureopenai-cli/Observability/CostHook.cs`, `azureopenai-cli/JsonGenerationContext.cs`, `azureopenai-cli/Program.cs`, `tests/AzureOpenAI_CLI.Tests/MultiProviderTests.cs`
- **validation:** `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj -c Release --filter "FullyQualifiedName~MultiProviderTests"`
- **acceptance:**
  1. `ProviderSelector` resolves `azure` (default), `openai` (`https://api.openai.com/v1`, `OPENAI_API_KEY`), `openrouter` (`https://openrouter.ai/api/v1`, `OPENROUTER_API_KEY`), `ollama` (`http://localhost:11434/v1`, no key required, $0 rate card), `llamacpp` / `local` (`http://localhost:8080/v1`, no key required, $0 rate card), and `custom` (`AZ_CUSTOM_ENDPOINT`, `AZ_CUSTOM_API_KEY`) using `OpenAIClient` + `HttpClientPipelineTransport(Program.SharedHttpClient)` with zero new NuGet packages.
  2. Newman security invariant: non-HTTPS (`http://`) endpoints are rejected everywhere EXCEPT loopback (`localhost`, `127.0.0.1`, `::1`) when the provider is `ollama`, `llamacpp`, `local`, or `custom` without an API key; passing a real API key over `http://` to a non-loopback host is strictly rejected.
  3. `--providers` (and `--providers --json`) lists all supported providers, default endpoints, env var names, and configured status without making a network call.
- **scenarios/tier:** `tier3-wave3-providers-and-commander-queue.yaml` (Tier 3)
- **rugged:** RUGGED (2026-05-17, `.wgm/rugged/review-t8.md`)
- **status:** done
- **notes:** Implemented `ProviderSelector` supporting `azure`, `openai`, `openrouter`, `ollama`, `llamacpp` (`local`), and `custom` with `IsLoopbackHost` security gating, `default_provider` in `UserConfig`, and $0 local model entries in `CostHook`. 10/10 tests in `MultiProviderTests` green.

### T9 -- Built-in Commander / Work Queue Mode (`az-ai queue` / `az-ai commander`: `init`, `add`, `list`, `status`, `plan`, `run`, `clear`) & MCP Queue Tools

- **files/areas:** `azureopenai-cli/Commander/WorkQueue.cs`, `azureopenai-cli/Commander/CommanderRunner.cs`, `azureopenai-cli/Tools/QueueTools.cs`, `azureopenai-cli/Tools/ToolRegistry.cs`, `azureopenai-cli/JsonGenerationContext.cs`, `azureopenai-cli/Program.cs`, `tests/AzureOpenAI_CLI.Tests/CommanderQueueTests.cs`
- **validation:** `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj -c Release --filter "FullyQualifiedName~CommanderQueueTests"`
- **acceptance:**
  1. `WorkQueue` manages `.az-ai/queue.json` (or `--queue-file <path>`) with atomic temp-file-and-rename persistence and file size/depth caps.
  2. Each `QueueTaskItem` supports `id`, `title`, `prompt`, `status` (`pending|running|done|failed|blocked`), `priority`, `depends_on` (DAG edges), `provider`, `model`, `persona`, `pattern`, `validate_command`, `max_iterations`, `attempts`, `max_attempts`, `result`, and `error`.
  3. `CommanderRunner` detects dependency cycles deterministically, recovers stale `running` tasks when `--recover-stale` is set, supports `--dry-run`, executes ready tasks in topological/priority order (with `--concurrency <1..8>`), routes each task to its specified `provider`/`model`/`persona`/`pattern`/`validate_command`, and emits human or `--json` summary reports.
  4. `queue_add` and `queue_status` tools are registered in `ToolRegistry` (and exposed via `az-ai mcp serve`) so external agents or in-process personas can enqueue and inspect work items.
- **scenarios/tier:** `tier3-wave3-providers-and-commander-queue.yaml` (Tier 3)
- **rugged:** RUGGED (2026-05-17, `.wgm/rugged/review-t9.md`)
- **status:** done
- **notes:** Implemented `WorkQueue`, `CommanderRunner`, and `QueueTools` (`queue_add`, `queue_status`), wired `az-ai queue` / `az-ai commander` (`init`, `add`, `list`, `status`, `plan`, `run`, `clear`) in `Program.cs`. 6/6 tests in `CommanderQueueTests` green.

### TZ -- Demo validation (required before Ship/Handoff)

- **files/areas:** `azureopenai-cli/`, `tests/AzureOpenAI_CLI.Tests/`
- **validation:** `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj -c Release --nologo && dotnet run --project azureopenai-cli/AzureOpenAI_CLI.csproj -c Release -- --list-tools --json`
- **acceptance:** Full test suite passes (`0` failures, `0` warnings), CLI `--list-tools --json` and `az-ai mcp serve` end-to-end demo path runs green.
- **rugged:** RUGGED (2026-05-17, `.wgm/rugged/review-tz.md`)
- **status:** done
- **notes:** Full suite passed 677/677 tests in 10 s; live `--list-tools --json` and `az-ai mcp serve` (`initialize`, `tools/list`, `tools/call`) verified end-to-end.

## Done

- **T1** -- Complete Machine I/O Contract (`--json` happy-path envelope, `--schema` wire enforcement, `--code` extraction) & Combined Stdin+Args Piping (`MachineIoAndCombinedStdinTests` 11/11 green, full suite 664/664 green).
- **T2** -- Startup & Runtime Optimization: Shared `SocketsHttpHandler` Prewarm Pool & Agent `--max-rounds` Cap Enforcement (`StartupAndMaxRoundsTests` + `PrewarmTests` 13/13 green).
- **T3** -- Custom Manifest Tool Plugins (FR-012) & Tool Introspection (`--list-tools [--json]`) (`ManifestPluginAndIntrospectionTests` 4/4 green).
- **T4** -- MCP Stdio Server (`az-ai mcp serve` / `--mcp-serve`) & Documentation Update (`McpStdioServerTests` 5/5 green).
- **TZ** -- Demo validation (`677/677` tests green + live `--list-tools --json` and `az-ai mcp serve` JSON-RPC 2.0 pipeline verified).
- **T5** -- NDJSON Event Streaming (`--jsonl`) & Machine-Readable Personas (`--personas --json`, `--models --json`) (`JsonlAndMachineDiscoveryTests` 4/4 green).
- **T6** -- Curated Pattern Library (FR-015 `--pattern` / `-p`, `--patterns [--json]`) & MCP Prompts/Resources (`PatternLibraryAndMcpPromptsTests` 4/4 green).
- **T7** -- Outbound MCP Stdio Client (FR-013 Phase 1 `--mcp-server`, `az-ai mcp tools`, `az-ai mcp call`) & Dual Framing (`McpStdioClientTests` 4/4 green).
- **T8** -- Multi-Provider Router (`--provider`, `AZ_PROVIDER`, `--providers [--json]`, `--endpoint`, `--api-key`) (`MultiProviderTests` 10/10 green).
- **T9** -- Built-in Commander / Work Queue Mode (`az-ai queue` / `az-ai commander`: `init`, `add`, `list`, `status`, `plan`, `run`, `clear`) & MCP Queue Tools (`CommanderQueueTests` 6/6 green).
