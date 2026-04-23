# Copilot Instructions for azure-openai-cli

## Project Overview

- Azure OpenAI CLI tool written in C# targeting .NET 10
- Docker-first deployment, Alpine-based multi-stage builds
- Primary use case: text injection for AHK/Espanso workflows
- Version: see `azureopenai-cli/AzureOpenAI_CLI.csproj` `<Version>` element (source of truth); latest released version is listed in `CHANGELOG.md`

## Architecture

- Single-file CLI entry point: `azureopenai-cli/Program.cs` (~1300 lines)
- Tool interface: `azureopenai-cli/Tools/IBuiltInTool.cs`
- 6 built-in tools: shell_exec, read_file, web_fetch, get_clipboard, get_datetime, delegate_task
- Tool registry: `azureopenai-cli/Tools/ToolRegistry.cs`
- User config: `azureopenai-cli/UserConfig.cs` (persists to `~/.azureopenai-cli.json`)
- JSON source generators: `azureopenai-cli/JsonGenerationContext.cs` (`AppJsonContext` -- AOT-compatible serialization)
- Squad persona system: `azureopenai-cli/Squad/` (SquadConfig, SquadCoordinator, SquadInitializer, PersonaMemory)
- Four modes: standard (single response), agent (tool-calling loop), Ralph mode (autonomous Wiggum loop), persona (squad member with persistent memory)

## Persona System (Squad)

- Inspired by bradygaster/squad -- AI team members with persistent memory
- Config: `.squad.json` in project root (team, personas, routing rules)
- Memory: `.squad/history/<name>.md` -- per-persona, auto-managed, 32 KB cap
- Decisions: `.squad/decisions.md` -- shared log across all personas
- 5 default personas: coder, reviewer, architect, writer, security
- Routing: keyword-based scoring via SquadCoordinator (deterministic, zero-latency)
- Flags: `--squad-init`, `--persona <name|auto>`, `--personas`
- Zero new dependencies -- built entirely with System.Text.Json

## Key Conventions

- All tool classes are `internal sealed class` implementing `IBuiltInTool`
- Tools define their own JSON schema via `BinaryData ParametersSchema`
- Tools use `TryGetProperty()` (not `GetProperty()`) for parameter access -- graceful handling of missing fields
- Security-first: all tools validate inputs, block dangerous operations
- Use `AppJsonContext` (in `JsonGenerationContext.cs`) for all new JSON serialization -- required for AOT compatibility
- Use `Azure.AI.OpenAI 2.1.0` (stable GA) -- tool calling works correctly on this release; pre-release packages have been removed from the dependency set
- Streaming via `CompleteChatStreamingAsync` -- tool calls arrive as indexed fragments
- `ChatToolCall.CreateFunctionToolCall(id, name, BinaryData.FromString(args))` for tool call construction
- Records for immutable data (`CliOptions`, `CliParseError`) defined in Program.cs
- Use `ErrorAndExit()` helper for all fatal exit paths -- consistent `[ERROR]` prefix on stderr + JSON-aware output. Do NOT duplicate inline error/exit patterns
- `--raw` flag suppresses all formatting (spinner, newline, stderr). Use `isRaw` guard in any new output path
- Shell substitution blocking: `ShellExecTool` rejects `$()`, backticks, `<()`, `>()`, `eval`, `exec`. Use `ArgumentList` (not `Arguments`) for OS-level escaping. New blocked patterns must have corresponding tests in `ToolHardeningTests`

## Build & Test

- Build: `dotnet build azureopenai-cli/AzureOpenAI_CLI.csproj`
- Test: `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj --verbosity minimal`
- Integration: `bash tests/integration_tests.sh`
- Format: `dotnet format azure-openai-cli.sln --verify-no-changes`
- Docker: `docker build -t azure-openai-cli:test .`
- **Preflight (run before every code commit):** `make preflight` -- see [`.github/skills/preflight.md`](skills/preflight.md). Non-negotiable for `.cs`/`.csproj`/`.sln`/workflow changes. Skipping this is how commit `180d64f` turned `main` red for five runs.

## Skills

- [`preflight`](skills/preflight.md) -- format + build + test gates before commit
- [`commit`](skills/commit.md) -- Conventional Commits, Copilot co-author trailer, signing rules
- [`ci-triage`](skills/ci-triage.md) -- diagnosing and fix-forwarding a red CI run
- [`episode-brief`](skills/episode-brief.md) -- canonical structure of every sub-agent dispatch prompt
- [`exec-report-format`](skills/exec-report-format.md) -- spec for `docs/exec-reports/sNNeMM-*.md` (overrides `_template.md`)
- [`fleet-dispatch`](skills/fleet-dispatch.md) -- orchestrator pre-flight + never-solo-background + wave-on-collision
- [`shared-file-protocol`](skills/shared-file-protocol.md) -- orchestrator-owned files + sub-agent staging discipline
- [`ascii-validation`](skills/ascii-validation.md) -- smart-quote / em-dash grep + ASCII enforcement
- [`docs-only-commit`](skills/docs-only-commit.md) -- decision tree for markdown-only diffs (when to skip preflight)
- [`changelog-append`](skills/changelog-append.md) -- `[Unreleased]` placement + push-timing serialization
- [`writers-room-cast-balance`](skills/writers-room-cast-balance.md) -- the 5 casting rules + audit query
- [`findings-backlog`](skills/findings-backlog.md) -- canonical entry format + 5-state lifecycle for every finding

Process governance docs live in [`docs/process/`](../docs/process/) (change management, ADR stewardship, CAB-lite, retrospective cadence). Skills are *verbs*; process docs are the *rules around the verbs*.

## Environment Variables

- `AZUREOPENAIENDPOINT` -- Azure OpenAI endpoint URL (required)
- `AZUREOPENAIAPI` -- API key (required, note: not "KEY")
- `AZUREOPENAIMODEL` -- Default model deployment name (comma-separated for multi-model)
- `RALPH_DEPTH` -- Subagent recursion depth (0-3, set automatically by delegate_task)

## File Structure Rules

- New tools go in `azureopenai-cli/Tools/` and must be registered in `ToolRegistry.Create()`
- Squad system files go in `azureopenai-cli/Squad/` -- persona config, routing, memory, initialization
- Tests go in `tests/AzureOpenAI_CLI.Tests/` using xUnit
- Integration tests go in `tests/integration_tests.sh` using bash assertions
- Feature proposals go in `docs/proposals/FR-NNN-*.md`

## Security Constraints

- Never expose API keys in output or logs
- Tool inputs must be validated before execution
- Shell commands have a blocklist (rm -rf, sudo, etc.)
- File reads are restricted from sensitive paths (/etc/shadow, ~/.ssh, etc.)
- Web fetches block private/internal IP ranges (SSRF protection) and validate final URL after redirects
- Subagent delegation depth is capped at 3 (`MaxDepth` in DelegateTaskTool)

## Code Style

- C# with .NET 10 conventions
- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- Implicit usings enabled (`<ImplicitUsings>enable</ImplicitUsings>`)
- XML doc comments (`///`) on classes and methods
- `internal` access modifier for tool types (not `public`)
- `UserConfig` is the exception: `public class` for test accessibility
- Records for immutable data (CliOptions, CliParseError)

## CI Pipeline (.github/workflows/ci.yml)

- 3 jobs: `build-and-test`, `integration-test`, `docker`
- Checks: format verification, build, unit tests, vulnerability scan
- Docker job builds image and runs Trivy security scan
- Integration tests run after build succeeds

## Agent Archetypes 🤖

This project uses [GitHub Copilot custom agents](https://gh.io/customagents/config) -- specialized AI personas defined in `.github/agents/`. The **showrunner** -- **Larry David** -- sits one tier above the main cast as the predominant orchestration agent: he conceives episodes, casts leads and guests, dispatches fleets of sub-agents, and signs off on every cut. The **main cast** of five drives the core build-ship loop: **Costanza** (product), **Kramer** (engineering), **Elaine** (docs), **Jerry** (DevOps), and **Newman** (security). A bench of **+21 supporting players** covers executive PM, release, marketing, QA, legal, FinOps, integrations, DevRel, SRE, prompt engineering, perf, accessibility, competitive analysis, speaking, AI ethics, i18n, UX, process, style gating, red-team/chaos, and a junior-dev onboarding lens -- **27 agents total** (1 showrunner + 5 main + 21 supporting).

See [`AGENTS.md`](../AGENTS.md) for the full roster, per-agent capsule bios, and the end-to-end workflow pipeline (Planning → Design → Implementation → Testing → Hardening → Merge Gates → Release → Community → Operations). `AGENTS.md` is canonical; this file just points there.
