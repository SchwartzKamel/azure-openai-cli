# Constitution: azure-openai-cli (az-ai)

> Project-wide principles every spec, plan, and task must honor. Written once (Triage/Plan), revised
> rarely and deliberately. wgm loads this first and checks every artifact against it at the
> Plan-exit gate. A slice may deviate only by recording the reason in the table below.

## Principles
1. **Code quality & Native AOT cleanliness** -- C# (.NET 10) must compile with `IsTrimmable=true`, `TrimMode=full`, and `PublishAot=true` with zero trim/AOT warnings. All JSON serialization and deserialization must register types in `AppJsonContext` (`System.Text.Json` source generation) -- never reflection-based `JsonSerializer`.
2. **Testing** -- Every behavioral change, CLI flag, tool, and error path must have deterministic xUnit coverage in `tests/AzureOpenAI_CLI.Tests/`. Existing 500+ unit tests must continue to pass without regressions.
3. **Security & privacy** -- Defense-in-depth on every automation/agent boundary:
   - Never log or print raw API keys or secrets (`UnsafeReplaceSecrets` redaction).
   - Bounded inputs (`MAX_STDIN_BYTES` = 1 MB on stdin; capped stdout/stderr on subprocesses).
   - `az-ai mcp serve` defaults to safe tools (`read_file`, `web_fetch`, `get_datetime`, `get_clipboard`, `delegate_task`) and omits `shell_exec` unless `--allow-shell` or explicit `--tools shell` is passed.
   - Custom manifest tools execute via `ProcessStartInfo.ArgumentList` (argv entries, never `/bin/sh -c` string interpolation), scrub parent credentials from child environment by default, cannot shadow built-in tool names, and only load repo-local `./.azureopenai-cli/tools/*.json` when `--allow-local-plugins` or `AZ_ALLOW_LOCAL_PLUGINS=1` is set.
4. **UX & machine-surface consistency** -- `--raw` and `--json` must never emit human banners, spinners, or `[warn]` lines onto stdout. In `--json` mode, happy-path completions emit a single valid JSON document on stdout and errors emit structured JSON on stderr (`ErrorJsonResponse`).
5. **Performance** -- Preserve the sub-15 ms Native AOT cold-start path; MCP, manifest plugin loading, and connection prewarming must be zero-cost when not invoked, and connection prewarming must share `SocketsHttpHandler` with `AzureOpenAIClient` so the warmed TLS socket is actually reused.
6. **Ruggedness** -- Built for real operators running `az-ai` inside shell pipelines, CI workflows, Espanso/AHK text expanders, and parent MCP agent hosts (Claude Code, Copilot CLI, Cursor) where malformed JSON, hung child processes, or leaked stderr noise breaks downstream parsers.

## Ruggedness gate (non-negotiable)
Every plan and every reviewed diff carries **exactly one** ruggedness verdict -- produced by
`/rugged plan` / `/rugged review` when the companion is discoverable, or by wgm's embedded five-step
rubric run inline (recording the missing capability) when it is not. An absent verdict is a gate
FAIL, never a pass, on every track including Quick.

| Verdict | Effect | Required recovery |
|---|---|---|
| **RUGGED** | passes the gate | none -- record the verdict and continue |
| **FRAGILE** | blocks | a remediation task in `IMPLEMENTATION_PLAN.md`, fixed before the gate re-runs |
| **UNKNOWN** | blocks | a validation-signal task that creates the missing deterministic field/stress evidence |

Plan-exit judges plan readiness (an exact runnable field/stress/recovery check *design*); Review
judges real implementation evidence. See `SKILL.md`, "The ruggedness gate".

## Non-negotiables
- Zero reflection-based JSON serialization; every serialized record must live in `AppJsonContext`.
- Never break existing CLI flag contracts or exit code semantics (`0` success, `1` runtime/config error, `2` unknown flag/usage error, `3` timeout/cancellation).
- Never overwrite root `AGENTS.md`.

## Tech constraints
- **Must use:** .NET 10 (`net10.0`), `Azure.AI.OpenAI`, `Microsoft.Agents.AI`, `System.Text.Json` source generator (`AppJsonContext`), xUnit (`tests/AzureOpenAI_CLI.Tests`).
- **Must avoid:** Reflection-based JSON/serialization, unescaped shell command concatenation in plugin execution, unbounded memory reads on stdin/stdout.
- **Deployment target:** Cross-platform Native AOT single-file binary (`az-ai`) and Alpine container.

## Recording deviations
When a slice must break a principle, log it here so the next agent sees the trade-off instead of
"fixing" it back.

| Date | Principle | Why we deviated | Scope |
|---|---|---|---|
| None | -- | -- | -- |

**Version**: 1.0.0 | **Ratified**: 2026-05-17 | **Last Amended**: 2026-05-17
