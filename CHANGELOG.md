# Changelog

All notable changes to Azure OpenAI CLI will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed
- **Native AOT promoted from experimental to recommended** — `make publish-aot`
  produces a ~9 MB single-file binary with **~11 ms cold start** (vs ~100 ms for
  the JIT/ReadyToRun build) on Linux x64. That 9× improvement is significant
  for Espanso/AutoHotKey text-injection workflows, where every key sequence
  spawns a fresh process. `make publish` is now an alias for `publish-aot`;
  `publish-fast` is retained for compatibility.
- **Remaining AOT warnings fixed** — Migrated `SquadConfig.Load` / `Save` and
  `SquadInitializer.Initialize` off reflection-based `JsonSerializer` overloads
  onto source-generated `AppJsonContext.Default.SquadConfig`. Added
  `ReadCommentHandling`, `AllowTrailingCommas`, and `PropertyNameCaseInsensitive`
  to `AppJsonContext`'s shared options so Squad config parsing stays forgiving.
- **Anonymous type eliminated in `OutputJsonError`** — Replaced with the new
  `ErrorJsonResponse` record registered in `AppJsonContext` (Program.cs:1172).
- **`DelegateTaskTool` single-file safety** — Replaced
  `Assembly.GetExecutingAssembly().Location` (empty in single-file/AOT builds)
  with `Environment.ProcessPath` + `AppContext.BaseDirectory`. Child agents can
  now be spawned correctly from the AOT-published binary.

The only remaining AOT publish warnings come from third-party assemblies
(`Azure.AI.OpenAI`, `OpenAI`) and do not affect runtime behavior.

## [1.7.0] — 2025-07-21

### Added
- **Token usage tracking** — Displays `[tokens: X→Y, Z total]` on stderr after every API call. Included in `--json` output as `input_tokens` and `output_tokens` fields
- **`--raw` flag** — Suppresses all formatting (no spinner, no newline, no stderr output). Designed for Espanso/AHK text expansion integration
- **TTY-aware output** — Spinners auto-suppress when stdout is piped (`Console.IsOutputRedirected`). Works even without `--raw`
- **Espanso/AHK integration guide** — New `docs/espanso-ahk-integration.md` with working configs for Espanso (Linux/macOS/Windows) and AutoHotKey v2
- **24 new tests** (454 total unit tests)

### Changed
- **AOT anonymous type elimination** — Replaced 2 anonymous types with source-generated `ChatJsonResponse` and `AgentJsonResponse` records in `AppJsonContext`. Removes last Native AOT blocker
- **ErrorAndExit DRY helper** — Extracted shared error handler replacing 8 duplicated error patterns (~40 lines saved). Consistent `[ERROR]` prefix across all error paths
- **DotEnv resilience** — `.env` file loading now wrapped in try-catch (missing/malformed `.env` no longer crashes)

### Security
- **Shell injection hardening** — `ShellExecTool` now blocks `$()`, backticks, process substitution (`<()`, `>()`), `eval`, and `exec`. Switched from string-interpolated `Arguments` to `ArgumentList` for proper OS-level escaping

## [1.6.0] — 2025-07-20

### Added
- **JSON Source Generators (AOT)** — `JsonGenerationContext.cs` with `AppJsonContext` providing source-generated serialization for `UserConfig`, `SquadConfig`, `PersonaConfig`, and all Squad types. Unblocks Native AOT compilation
- **CLI Validation** — Temperature validated to 0.0–2.0 range, max-tokens validated to 1–128000 range in `ParseCliFlags`
- **Rate-limit aware backoff** — Streaming retry now respects `Retry-After` header from Azure API (capped at 60s)
- **60+ new tests** — `JsonSourceGeneratorTests` (16), `ToolHardeningTests` (33), `ProgramTests` validation (11), integration tests (8 new)

### Changed
- **Dockerfile Optimization** — Added `PublishReadyToRun=true` for ~50% startup improvement; improved layer caching by copying `.csproj` first, restoring, then copying source
- **Makefile** — Fixed stale .NET 9.0 references → .NET 10.0

### Security
- **WebFetchTool SSRF redirect protection** — Validates final URL after HTTP redirects (HTTPS-only, no private IPs)
- **Tool parameter hardening** — All tools replaced `GetProperty()` with `TryGetProperty()` for graceful error handling on missing parameters

### Fixed
- **Console.Out race condition** — `RunRalphLoop` now guarantees `Console.Out` restoration via try-finally

## [1.5.0] — 2026-04-09

### Added
- **Persona System** inspired by [bradygaster/squad](https://github.com/bradygaster/squad) — AI team members with persistent memory
- `--persona <name>` flag: select named persona (coder, reviewer, architect, writer, security)
- `--persona auto`: auto-route to best persona via keyword-based routing
- `--personas`: list available personas from `.squad.json`
- `--squad-init`: scaffold `.squad.json` and `.squad/` directory with default team
- Persistent persona memory in `.squad/history/` — knowledge compounds across sessions
- Shared decision log in `.squad/decisions.md`
- `SquadCoordinator` for intelligent task routing with keyword scoring
- `SquadConfig` for JSON-based team configuration (`.squad.json`)
- `PersonaMemory` for per-persona history management with 32 KB cap and tail truncation
- `SquadInitializer` for scaffolding default squad with 5 personas and routing rules
- 5 default personas with specialized system prompts and tool selections
- ~46 new unit tests for Squad system
- Zero new dependencies — built entirely with `System.Text.Json`

## [1.4.0] — 2025-07-13

### Added
- **Ralph Mode** (`--ralph`): Autonomous Wiggum loop for self-correcting agent workflows
- `--validate <cmd>`: External validation command for Ralph loop iterations
- `--task-file <path>`: Read task prompt from file
- `--max-iterations <n>`: Control Ralph loop iteration limit (default: 10, max: 50)
- **DelegateTaskTool**: New built-in tool for subagent calling (`delegate_task`)
- Subagent recursion depth control via `RALPH_DEPTH` env var
- `.ralph-log` iteration history file
- 44 new tests (28 Ralph mode unit tests + 16 delegate tool tests)
- 20 new integration tests for Ralph mode flags

## [1.3.0] — 2025-04-09

### Security
- **ReadFileTool**: Fixed symlink traversal vulnerability; added prefix-based path blocking
- **ShellExecTool**: Expanded blocked commands (sudo, su, crontab, vi, vim, nano, nc, ncat, netcat, wget); close stdin on child process; HasExited guard before Kill
- **WebFetchTool**: Added DNS rebinding protection (private IP blocklist); limited redirects to 3; dynamic User-Agent from assembly version
- **GetClipboardTool**: Enforced clipboard size cap with truncation warning; PATH-based command detection
- **ToolRegistry**: Replaced substring matching with exact alias dictionary

### Added
- Parallel tool call execution via `Task.WhenAll` for concurrent agent tool rounds
- Accurate tool call counting in JSON output (`tools_called` field)
- CI: code formatting check (`dotnet format --verify-no-changes`)
- CI: NuGet vulnerability audit (`dotnet list package --vulnerable`)
- CI: Trivy container image scanning (CRITICAL/HIGH severity)
- CI: integration test job
- Makefile: `format`, `format-check`, `audit`, `all-tests` targets
- 104 new security unit tests (138 total)
- 11 parallel execution unit tests

### Changed
- Agent loop now executes multiple tool calls concurrently instead of sequentially
- Tool name matching uses explicit alias dictionary instead of broad substring search

## [1.2.0] — 2026-04-08

### Added
- **Agentic mode** (`--agent`): model can call built-in tools before responding
- 5 built-in tools: `shell_exec`, `read_file`, `web_fetch`, `get_clipboard`, `get_datetime`
- `--tools <list>` flag to restrict which tools are available (comma-separated)
- `--max-rounds N` flag to limit tool-calling iterations (default: 5)
- Agent-aware system prompt injection with available tool names
- JSON output includes agent metadata (rounds, tools_called) when `--agent --json` combined
- Tool safety: shell command blocklist, HTTPS-only web fetch, file size caps, path blocking
- Unit tests for tool registry and built-in tools
- Integration tests for agent mode CLI flags

### Changed
- Upgraded Azure.AI.OpenAI from 2.1.0 to 2.9.0-beta.1 (required for tool calling)
- Upgraded Azure.Core from 1.47.2 to 1.51.1
- `--json` flag now detected anywhere in args (previously required first position)
- Removed experimental `SetNewMaxCompletionTokensPropertyEnabled` call (incompatible across SDK versions)

### Security
- Shell tool blocks dangerous commands (rm, kill, mkfs, dd, etc.) and pipe chains containing them
- Shell command timeout (10s) and output size cap (64KB)
- File read tool blocks sensitive paths (/etc/shadow, /etc/passwd, etc.)
- Web fetch enforces HTTPS-only with timeout and response size cap

## [1.1.0] — 2026-04-08

### Added
- Stdin pipe support: `echo "question" | az-ai`, `cat file | az-ai "summarize"` — combines piped content with prompt arguments
- `--json` output mode for scripting and automation
- `--version` / `-v` flag to display current version
- Progress spinner (braille animation) on stderr while waiting for first token
- Input validation with 32K character prompt limit
- Azure-specific exception handling for HTTP 401, 403, 404, and 429 responses
- Configurable streaming timeout via `AZURE_TIMEOUT` environment variable
- Configurable `AZURE_MAX_TOKENS` and `AZURE_TEMPERATURE` environment variables
- Restrictive file permissions (chmod 600) on config file at creation
- API key validation before client creation
- HTTPS endpoint validation
- xUnit test project with 16 unit tests
- GitHub Actions CI/CD pipeline
- SECURITY.md — comprehensive security documentation
- ARCHITECTURE.md — system design and component documentation
- CONTRIBUTING.md — developer onboarding guide
- CODE_OF_CONDUCT.md — Contributor Covenant v2.1
- GitHub issue templates (bug report, feature request)
- Pull request template
- Copilot agent archetypes: Costanza (PM), Kramer (engineer), Newman, Elaine, Jerry
- 5 feature proposals (FR-001 through FR-005) with priority matrix and shipping timeline

### Changed
- Upgraded from .NET 9.0-preview to .NET 10.0 stable
- Upgraded Dockerfile base images from preview tags to stable
- Optimized Dockerfile layer ordering for faster rebuilds
- Removed redundant COPY instruction in Dockerfile
- Credentials now injected via `--env-file` at runtime instead of baked into image
- Makefile: added `help`, `test`, and `smoke-test` targets
- README: added badges, configuration reference table, and exit code documentation

### Fixed
- Removed `.env` credential bundling from Docker image (security vulnerability)
- Generic exception handling replaced with Azure-specific error handlers
- Stream null check for content delta updates

### Security
- Container credentials are no longer baked into Docker images
- API key is validated before Azure OpenAI client creation
- HTTPS endpoint validation prevents insecure connections
- Config file restricted to owner-only access (chmod 600 on Unix)

## [1.0.1] — 2025-12-04

### Added
- Multi-model selection support: `--models`, `--set-model`, `--current-model` flags
- Feature proposals README with priority matrix
- Copilot agent definitions (Costanza, Kramer)

### Fixed
- Improved exception handling based on code review feedback (modern C# range syntax, named constants)

## [1.0.0] — 2025-08-17

### Added
- Initial release
- Azure OpenAI chat completion via Docker container
- Streaming responses (token-by-token output)
- System prompt configuration via `SYSTEM_PROMPT` environment variable
- Docker-first architecture with Alpine Linux base image
- Non-root container execution (`appuser`)
- `.dockerignore` for minimal build context

### Security
- Switched to Alpine Linux for reduced attack surface (OWASP/Snyk compliance)
- Fixed 2 critical and several high-severity container vulnerabilities
- Vulnerability scanning integrated into workflow
