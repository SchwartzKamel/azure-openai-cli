# FR-013 -- MCP (Model Context Protocol) Client and Server Support

**Status:** Design (Costanza, 2026-04-21) -- supersedes 2026-04-20 stub
**Priority:** P1 -- Table-stakes competitive parity + distribution lever
**Effort:** Large (phased, 5-7 engineer-weeks end-to-end)
**Depends on:** FR-006 (AOT), FR-012 (plugin registry -- shares manifest/allowlist seams), FR-014 (preferences.toml as canonical config surface)
**Related:** `docs/opportunity-analysis.md` row 1, `docs/competitive-analysis.md` §Q7

---

## 1. Problem Statement

By April 2026, **Model Context Protocol (MCP)** is the de facto integration standard for CLI AI tools. Claude Code, OpenAI Codex CLI, GitHub Copilot CLI, Gemini CLI, aichat, `crush`, fabric, and ollama-via-bridge all ship MCP *client* support; most also act as MCP *servers*. az-ai ships neither. Two concrete costs:

1. **Inbound gap (client):** Teams that standardized on MCP tool catalogs (GitHub, Atlassian, Linear, filesystem, Postgres, Playwright, …) cannot plug az-ai into their existing workflow without forking.
2. **Outbound gap (server):** az-ai cannot be surfaced as a sub-tool inside Claude Code or `gh copilot cli`. This is the **single largest distribution vector we are leaving on the table** -- users pull az-ai in because their primary agent can call it, rather than us having to convince them to install a new CLI.

## 2. Goals / Non-Goals

**Goals**
- Ship `--mcp-server <name>` client support so az-ai's agent loop can call tools exposed by any spec-compliant MCP server over **stdio** transport.
- Ship `az-ai mcp serve` so az-ai exposes its built-in tools (shell, read_file, web_fetch, delegate, …) to any MCP-compatible host.
- Maintain **Native AOT** compatibility: zero new reflection-based JSON, all new types registered in `AppJsonContext`.
- Preserve the 5.4 ms cold-start advantage on the *non-MCP* path -- MCP wiring is lazy/pay-for-what-you-use.

**Non-Goals (v1)**
- HTTP/SSE transport (deferred; stdio is what 95% of MCP servers ship today).
- Resources, Prompts, Roots, Sampling -- MCP has multiple feature surfaces; v1 implements **Tools only**. Resources and Prompts land in v2.
- Hot-reload of server config at runtime.
- A plugin marketplace UI (FR-012 scope).

## 3. Scope Split & Phasing

MCP has two halves. They are separable; we ship them in two phases.

**Phase 1 -- MCP Client (ships first, ~3 weeks).** az-ai calls external MCP servers.
Rationale: user value is immediate and compounding (every MCP server in existence becomes available to az-ai), risk is contained to our process, and it gives us a working codec to reuse for Phase 2.

**Phase 2 -- MCP Server (ships second, ~2-3 weeks).** az-ai exposes itself as a server so Claude Code / Codex / gh copilot can call it.
Rationale: reuses the protocol codec from Phase 1, but requires harder decisions about *which* tools to expose and how to auth/sandbox. Also: until Phase 1 lands, we have no way to dogfood our own server (we'd call it from az-ai's own client path).

**Why not both at once?** Two reasons. (1) Phase 1 is pure consumer; Phase 2 requires a security review (Newman) that can run in parallel with Phase 1 implementation. (2) Shipping client-first lets us publish `az-ai mcp add filesystem …` demo videos and capture distribution/marketing wind *before* the server lands -- Sue Ellen wants this sequencing for the 1.10 release note drumbeat.

**Recommendation: build the CLIENT first.**

## 4. Transport Decisions

| Transport | Spec status | Ecosystem prevalence (Apr 2026) | v1? |
|-----------|-------------|----------------------------------|-----|
| **stdio** (framed JSON-RPC over child process stdin/stdout) | Stable | ~95% of published servers | ✅ **yes** |
| HTTP + SSE (streamable HTTP) | Stable | Growing, mostly hosted/enterprise | ⏳ v2 |
| WebSocket | Draft/optional | Rare | ❌ |

**v1 = stdio only.** Every consumer-facing MCP server (filesystem, git, github, postgres, playwright, sqlite, time, fetch, memory) ships stdio. HTTP/SSE becomes interesting only when az-ai is itself deployed server-side -- that's a post-v1 concern. Doing stdio first lets us spawn a child process, write newline-delimited JSON-RPC 2.0 frames, and read responses -- no HTTP stack, no TLS, no keepalives, and no AOT headaches.

## 5. Dependency Audit

### 5.1 Official C# SDK -- `ModelContextProtocol` (NuGet)

As of April 2026 the **`ModelContextProtocol`** NuGet package (`modelcontextprotocol/csharp-sdk`, co-maintained by Microsoft and Anthropic) is the canonical C# MCP implementation. Sub-packages:

- `ModelContextProtocol` -- core client/server types
- `ModelContextProtocol.Extensions.AI` -- bridges MCP tools to `Microsoft.Extensions.AI.AITool` -- **this is exactly our MAF surface**
- `ModelContextProtocol.AspNetCore` -- HTTP/SSE host (not needed for v1)

**AOT verdict:** the core package is `IsTrimmable=true` and ships source-generated JSON contexts; the Extensions.AI bridge uses `AIFunctionFactory` patterns we already use in `ToolRegistry.cs`. **The SDK is AOT-compatible in principle** but must be smoke-tested on `PublishAot=true` in our CI before we commit -- some preview builds have slipped reflection calls past the trimmer. Kramer owns that spike.

**Decision:** **adopt `ModelContextProtocol` + `ModelContextProtocol.Extensions.AI`** for v1. If the AOT smoke test fails, fall back to the minimal implementation in §5.2.

### 5.2 Fallback: Minimal In-Tree Protocol

If the SDK can't be made AOT-clean in time, we implement MCP ourselves. The surface is small enough to justify it (this is what `crush` and `aichat` did originally). Wire references:

- Spec: <https://spec.modelcontextprotocol.io/specification/2025-03-26/>
- Framing: JSON-RPC 2.0 (`Content-Length: N\r\n\r\n{…}` headers **or** newline-delimited -- check current spec; stdio examples in practice use plain NDJSON)
- Required methods for Tools-only v1:
  - Client → server: `initialize`, `notifications/initialized`, `tools/list`, `tools/call`
  - Server → client: responses + `notifications/tools/list_changed` (optional)
- Schemas: JSON-Schema subset for `inputSchema` -- already compatible with `AIFunctionFactory`.

A minimal impl is ~600 LOC across codec, transport, and client. Worst-case fallback only.

## 6. CLI Surface

### 6.1 Client side

**Preferences file** (canonical; FR-014 defines `~/.config/az-ai/preferences.toml`):

```toml
[mcp.servers.filesystem]
command = "npx"
args = ["-y", "@modelcontextprotocol/server-filesystem", "/home/tweber/projects"]
enabled = true
allowed_tools = ["read_file", "list_directory"]   # optional allowlist; omit = all
env = { MY_TOKEN = "${env:MY_TOKEN}" }            # env var interpolation

[mcp.servers.github]
command = "docker"
args = ["run", "-i", "--rm", "ghcr.io/github/github-mcp-server"]
allowed_tools = ["search_repositories", "get_file_contents"]
```

**CLI flags:**

- `--mcp-server <name>[,<name>…]` -- enable these configured servers for this invocation (agent mode).
- `--mcp-server-all` -- enable every server with `enabled = true`.
- No servers are loaded by default; opt-in only. Keeps cold-start clean for users who don't use MCP.

**Subcommands** (mirror `aichat` / Codex conventions -- users moving between tools should find the same verbs):

```
az-ai mcp list                            # list configured servers + reachability
az-ai mcp add <name> -- <command> [args]  # append to preferences.toml
az-ai mcp remove <name>
az-ai mcp tools <name>                    # enumerate tools the server exposes
az-ai mcp call <name> <tool> [--arg k=v]  # direct invocation (debugging / scripting)
az-ai mcp doctor                          # spawn each server, run initialize, report
```

`mcp add` writes into FR-014's TOML with a preserving parser (no clobbering comments).

### 6.2 Server side

```
az-ai mcp serve [--stdio] [--tools shell,file,web] [--allow-shell] [--read-only]
```

- `--stdio` is the default (and in v1, only) transport.
- `--tools` respects the existing `ToolRegistry` short-alias set (`shell`, `file`, `web`, `clipboard`, `datetime`, `delegate`). Uses the same parser as agent-mode `--tools`.
- `--read-only` convenience: equivalent to `--tools file,web,clipboard,datetime` (omits shell + delegate).
- `--allow-shell` must be explicit; without it `shell_exec` is **not exposed** even if listed. Defense in depth -- the host process may be a stranger's agent.

**Launch example (Claude Code `claude_mcp_config.json`):**

```json
{
  "mcpServers": {
    "az-ai": {
      "command": "az-ai",
      "args": ["mcp", "serve", "--tools", "file,web,delegate"]
    }
  }
}
```

One line. Paste. Done. That's the hero demo -- see §10.

## 7. Security Model

MCP servers are **untrusted input sources**. They can return: crafted tool names, malicious schemas, tool results containing prompt-injection payloads, descriptions that attempt to social-engineer the model into running dangerous tools. Mitigations:

1. **Per-server allowlist** -- `allowed_tools` in TOML. If present, tools not in the list are filtered out during `tools/list`. This is the single most important control.
2. **Namespacing** -- every MCP tool surfaces to the model as `mcp__<server>__<tool>` (double-underscore, mirrors Claude Code's convention for grep-ability). Prevents a rogue server from shadowing `shell_exec`.
3. **Description sanitization** -- strip ANSI + control characters from server-provided `description` and `title` fields before they reach the model. Log when stripping occurs.
4. **Tool-result quoting** -- MCP tool results are fenced under a system-visible marker (`<mcp-tool-result server="foo" tool="bar">…</mcp-tool-result>`) so the model treats them as data, not instructions. Aligns with FR-012's existing tool-output convention.
5. **Process isolation** -- each MCP server runs as a child process with inherited-but-scrubbed env (only keys explicitly listed in `env = {…}` are passed through). No shell interpolation of `args`.
6. **Spawn-time budget** -- `mcp.startup_timeout_ms` (default 3000). Slow servers are killed; we don't hang the chat loop waiting for `npm install`.
7. **Per-call timeout** -- `mcp.call_timeout_ms` (default 30000). Prevents runaway servers.
8. **Audit log** -- every MCP call emits an observability event (`mcp.tool.called`, `mcp.tool.result_size`, `mcp.tool.duration`) to whatever sink FR-018-ish observability chose. Frank Costanza's territory.

On the server side, `az-ai mcp serve` reuses the existing tool-hardening path (`ShellExecTool` blocklist, `ReadFileTool` sensitive-path blocks, `WebFetchTool` SSRF guard) -- every inbound MCP `tools/call` flows through the same validators as agent mode. **No new trust boundary, just a new caller.**

Newman signs off before Phase 2 ships.

## 8. Integration with the Existing Tool System

MCP-sourced tools plug into **the same `ToolRegistry` that agent mode already iterates**. Proposed shape (no code, just contract):

- `ToolRegistry.CreateMafTools(enabledTools, mcpClients)` -- add an optional `IEnumerable<McpClientSession>` parameter. Each session contributes its `AITool` instances (via `ModelContextProtocol.Extensions.AI.AIFunctionFactory.FromMcpTool`) with names prefixed `mcp__<server>__`.
- Short-alias resolution is unchanged; MCP tools do not get short aliases (their namespacing *is* their disambiguation).
- Agent loop treats MCP tools identically to built-ins -- same tool-call JSON shape, same `max_tool_iterations`, same observability events.
- When `--mcp-server` is not passed, `mcpClients` is empty, and **zero MCP code executes** on the hot path.

This keeps the change narrowly additive. No refactor of `Program.cs`, no new "tool provider" abstraction layer yet -- if FR-012 lands after this, it can absorb MCP as one provider among many.

## 9. AOT Compatibility

Non-negotiable. Rules the implementation must follow:

1. All JSON types (`InitializeRequest`, `InitializeResponse`, `ToolDescriptor`, `CallToolRequest`, `CallToolResult`, `McpServerConfig`, etc.) are added to `AppJsonContext` in `JsonGenerationContext.cs` with `[JsonSerializable]` attributes.
2. The `ModelContextProtocol` SDK is consumed **only through APIs that accept a `JsonSerializerContext`**. Any API that internally falls back to reflection gets wrapped or replaced.
3. `PublishAot=true` in the v2 csproj must still pass `dotnet publish` after MCP lands. CI gets a new job: `aot-publish-mcp` that compiles with `--mcp-server` wired in.
4. No `System.Text.Json.JsonSerializer.Deserialize<T>(string)` without a context. Lint rule added to `.editorconfig` (Soup Nazi).
5. Child-process spawning via `System.Diagnostics.Process` (trimmer-safe). No `Microsoft.Extensions.Hosting` generic-host machinery in the MCP path -- that drags in too much DI + reflection.

## 10. Distribution Story -- the Hero Use Case

The money quote in the 1.10 release announcement:

> Add az-ai to Claude Code with one line:
> ```json
> { "mcpServers": { "az-ai": { "command": "az-ai", "args": ["mcp", "serve"] } } }
> ```
> Now Claude can delegate Azure-specific prompts, read_file, web_fetch, and your Espanso text-injection pipeline. 9 MB binary, 5.4 ms startup, Azure-OpenAI-native, MCP-compliant.

This is the first time az-ai has a **zero-friction on-ramp from a bigger tool's user base**. Peterman writes the blog post. Keith Hernandez demos it on stream. Bob Sacamano adds the snippet to Homebrew formula description. We do not ship Phase 2 without a coordinated launch.

## 11. Proof-of-Concept Class Layout

Design only -- the implementer lays out actual files. Target layout under `azureopenai-cli-v2/Mcp/`:

```
Mcp/
  McpConfig.cs          # records: McpServerConfig, McpClientOptions, parsed from FR-014 TOML
  McpClientSession.cs   # owns one child process; exposes StartAsync, ListToolsAsync, CallToolAsync, DisposeAsync
  McpClientManager.cs   # owns N sessions keyed by server name; spawn + health-check fan-out
  McpTool.cs            # AITool adapter: wraps a (sessionRef, toolName) into a MAF-callable tool
  McpServer.cs          # hosts the server side (az-ai mcp serve); loops on stdin, dispatches to ToolRegistry
  McpCommands.cs        # argv handler for `mcp add | remove | list | tools | call | doctor | serve`
  Protocol/
    JsonRpc.cs          # framing + envelope types (for the in-tree fallback path; unused if SDK works)
    Messages.cs         # [JsonSerializable] record types for MCP request/response payloads
```

### Pseudocode -- stdio client, tools/list + tools/call

```
session = new McpClientSession(config):
    proc = Process.Start(config.Command, config.Args, env=scrub(config.Env), redirect stdin/stdout/stderr)
    stderr -> observability sink (non-fatal, diagnostic)
    stdin  <- NDJSON writer
    stdout -> NDJSON reader loop -> Channel<JsonRpcResponse>

session.InitializeAsync():
    send { "jsonrpc":"2.0", "id":1, "method":"initialize",
           "params": { "protocolVersion":"2025-03-26",
                       "capabilities": { "tools": {} },
                       "clientInfo": { "name":"az-ai", "version":AppVersion } } }
    await response
    send notification "notifications/initialized"

session.ListToolsAsync():
    send tools/list
    filter by config.AllowedTools if present
    return [ ToolDescriptor { name, description, inputSchema } ]

session.CallToolAsync(name, args):
    send tools/call with { name, arguments: args }
    await response within call_timeout_ms
    return result.content (text | image | resource)
```

### Pseudocode -- stdio server

```
mcp serve main:
    tools = ToolRegistry.CreateMafTools(options.EnabledTools)  // same path as agent mode
    filter out shell_exec unless --allow-shell
    stdin NDJSON reader -> dispatch loop:
        initialize -> reply with capabilities.tools = {}
        tools/list -> reply with [ { name, description, inputSchema } for each tool ]
        tools/call -> locate tool, validate args against schema, invoke, wrap result as
                      { content: [ { type:"text", text: result } ], isError: bool }
        shutdown   -> break loop
```

## 12. Testing Strategy

**Client path** (no real servers needed):
- Unit tests drive the codec against canned JSON-RPC fixtures in `tests/fixtures/mcp/`.
- Integration tests spawn a **tiny scripted stub server** -- a bash script or a `dotnet run` helper in `tests/McpStub/` that reads NDJSON from stdin and replies from a fixture table. Covers initialize / tools/list / tools/call / error / timeout / malformed-frame scenarios.
- One end-to-end test against the reference `@modelcontextprotocol/server-filesystem` npx server, gated behind `MCP_E2E=1` so CI without node still passes.

**Server path** (no real client needed):
- Unit tests invoke `McpServer.HandleMessageAsync(jsonRpc)` directly with fixture messages; assert on reply payloads.
- Integration test: pipe-based -- start `az-ai mcp serve --stdio` as a child, send initialize + tools/list + tools/call via a Python or Bash driver in `tests/integration_tests.sh`, assert on responses. Zero external dependencies.
- Golden test: **dogfood** -- once Phase 1 lands, `az-ai --mcp-server self` against `az-ai mcp serve` must round-trip `read_file` successfully. That's our smoke test forever.

**Security tests** (Puddy + Newman):
- Server rejects `tools/call` with arguments that would trip `ShellExecTool` blocklist.
- Client strips control chars from server-provided `description`.
- Client enforces per-server allowlist.
- Client enforces `call_timeout_ms` (fake a hanging stub server).
- Client scrubs env vars not listed in config.

## 13. Milestones & Estimated Effort

| # | Milestone | Scope | Owner cast | Effort |
|---|-----------|-------|------------|--------|
| **M0** | SDK AOT spike | Publish a hello-world with `ModelContextProtocol` referenced, confirm `PublishAot=true` works; fallback decision | Kramer | 2 days |
| **M1** | Preferences schema | `[mcp.servers.*]` table in FR-014 TOML; `McpConfig` types in `AppJsonContext`; `mcp list/add/remove` subcommands | Kramer + Elaine (docs) | 3 days |
| **M2** | Client codec + session | `McpClientSession`, stdio transport, initialize/tools/list/tools/call, timeouts, env scrub | Kramer | 1 week |
| **M3** | Client ↔ agent integration | MCP tools flow into `ToolRegistry`, namespacing, allowlist, observability events, `--mcp-server` flag | Kramer | 3-4 days |
| **M4** | Client hardening + tests | Security tests, `mcp doctor`, `mcp call` debug command, docs | Kramer + Newman + Puddy | 3-4 days |
| **M5** | **CLIENT SHIP (v1.10)** | Release notes, Peterman blog, Keith demo, Bob Sacamano Homebrew blurb | Lippman + Peterman | 2 days |
| **M6** | Server core | `McpServer`, reuse ToolRegistry, stdio loop, `az-ai mcp serve --stdio` | Kramer | 1 week |
| **M7** | Server hardening | `--allow-shell` gating, read-only mode, security review, Claude Code + Codex round-trip tests | Kramer + Newman | 3-4 days |
| **M8** | **SERVER SHIP (v1.11)** | Hero "one-line Claude Code install" announcement, distribution push | Lippman + Peterman + Sacamano | 2 days |
| M9 (post-v1) | HTTP/SSE transport, Resources, Prompts, `notifications/tools/list_changed` | -- | -- | separate FR |

**Total Phase 1:** ~3 weeks wall clock. **Total Phase 2:** ~2 weeks wall clock. Contingency + docs + coordination: +1 week. **~6 weeks end-to-end.** Matches the "Large" original estimate.

## 14. Success Criteria

- **Client:** `az-ai --mcp-server filesystem --agent "list files in ~/projects"` completes in ≤ 2× the latency of an equivalent built-in `read_file` call (measured p50 over 20 invocations).
- **Server:** Claude Code with az-ai configured can successfully call `read_file` and `web_fetch` tools end-to-end in under 500 ms handshake + call.
- **Cold start:** `az-ai --version` stays ≤ 10 ms (budget: MCP code must not be touched on non-MCP paths).
- **AOT:** `dotnet publish -c Release -p:PublishAot=true` produces a single binary that passes `mcp doctor` against at least two reference servers (filesystem, time).
- **Adoption signal:** 3+ external references to "az-ai as an MCP server in Claude Code/Codex" within 60 days of M8.
- **Binary size:** ≤ +1.5 MB over pre-FR-013 baseline. If we exceed that, Morty escalates.

## 15. Open Questions (resolve before M2)

1. Does the `ModelContextProtocol` SDK's trimmer story actually hold under `PublishAot=true` in Apr 2026? (M0 spike answers this.)
2. Do we expose `delegate_task` via `mcp serve` by default, or hide it behind `--allow-delegate`? Recursion from a remote agent invoking our delegate tool that invokes their model is a cost-blast risk -- Morty's call.
3. How does MCP interact with `.squad/` personas? Do we expose per-persona MCP servers (e.g., the `security` persona gets `github-mcp`, the `writer` persona gets `fetch`)? Capture as FR-021 follow-up, not v1 scope.
4. Config precedence: `--mcp-server` flag > project `.az-ai/preferences.toml` > user `~/.config/az-ai/preferences.toml` -- confirm with FR-014 owner.
