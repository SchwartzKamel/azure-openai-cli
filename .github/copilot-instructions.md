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
- JSON source generators: `azureopenai-cli/JsonGenerationContext.cs` (`AppJsonContext` — AOT-compatible serialization)
- Squad persona system: `azureopenai-cli/Squad/` (SquadConfig, SquadCoordinator, SquadInitializer, PersonaMemory)
- Four modes: standard (single response), agent (tool-calling loop), ralph (Wiggum autonomous loop), persona (squad member with persistent memory)

## Persona System (Squad)
- Inspired by bradygaster/squad — AI team members with persistent memory
- Config: `.squad.json` in project root (team, personas, routing rules)
- Memory: `.squad/history/<name>.md` — per-persona, auto-managed, 32 KB cap
- Decisions: `.squad/decisions.md` — shared log across all personas
- 5 default personas: coder, reviewer, architect, writer, security
- Routing: keyword-based scoring via SquadCoordinator (deterministic, zero-latency)
- Flags: `--squad-init`, `--persona <name|auto>`, `--personas`
- Zero new dependencies — built entirely with System.Text.Json

## Key Conventions
- All tool classes are `internal sealed class` implementing `IBuiltInTool`
- Tools define their own JSON schema via `BinaryData ParametersSchema`
- Tools use `TryGetProperty()` (not `GetProperty()`) for parameter access — graceful handling of missing fields
- Security-first: all tools validate inputs, block dangerous operations
- Use `AppJsonContext` (in `JsonGenerationContext.cs`) for all new JSON serialization — required for AOT compatibility
- Use `Azure.AI.OpenAI 2.1.0` (stable GA) — tool calling works correctly on this release; pre-release packages have been removed from the dependency set
- Streaming via `CompleteChatStreamingAsync` — tool calls arrive as indexed fragments
- `ChatToolCall.CreateFunctionToolCall(id, name, BinaryData.FromString(args))` for tool call construction
- Records for immutable data (`CliOptions`, `CliParseError`) defined in Program.cs
- Use `ErrorAndExit()` helper for all fatal exit paths — consistent `[ERROR]` prefix on stderr + JSON-aware output. Do NOT duplicate inline error/exit patterns
- `--raw` flag suppresses all formatting (spinner, newline, stderr). Use `isRaw` guard in any new output path
- Shell substitution blocking: `ShellExecTool` rejects `$()`, backticks, `<()`, `>()`, `eval`, `exec`. Use `ArgumentList` (not `Arguments`) for OS-level escaping. New blocked patterns must have corresponding tests in `ToolHardeningTests`

## Build & Test
- Build: `dotnet build azureopenai-cli/AzureOpenAI_CLI.csproj`
- Test: `dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj --verbosity minimal`
- Integration: `bash tests/integration_tests.sh`
- Format: `dotnet format azure-openai-cli.sln --verify-no-changes`
- Docker: `docker build -t azure-openai-cli:test .`

## Environment Variables
- `AZUREOPENAIENDPOINT` — Azure OpenAI endpoint URL (required)
- `AZUREOPENAIAPI` — API key (required, note: not "KEY")
- `AZUREOPENAIMODEL` — Default model deployment name (comma-separated for multi-model)
- `RALPH_DEPTH` — Subagent recursion depth (0-3, set automatically by delegate_task)

## File Structure Rules
- New tools go in `azureopenai-cli/Tools/` and must be registered in `ToolRegistry.Create()`
- Squad system files go in `azureopenai-cli/Squad/` — persona config, routing, memory, initialization
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
