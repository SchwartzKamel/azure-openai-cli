# Copilot Instructions for azure-openai-cli

## Project Overview

Azure OpenAI CLI (`az-ai`) -- a C# .NET 10 Native AOT single-file binary.
Primary use case: headless text generation for Espanso/AHK text expansion workflows.
Version source of truth: `<Version>` in `azureopenai-cli/AzureOpenAI_CLI.csproj`.

## Build, Test, Lint

```bash
# Build
dotnet build azureopenai-cli/AzureOpenAI_CLI.csproj

# Full test suite (~7 min, 600+ xUnit tests)
dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj --verbosity minimal

# Single test (use --filter with test name or partial match)
dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj --filter "LoadConfigEnvFrom_CjkValues"

# Format check (what CI runs)
dotnet format azure-openai-cli.sln --verify-no-changes

# Auto-fix formatting
dotnet format azure-openai-cli.sln

# Integration tests (bash assertions against the binary)
bash tests/integration_tests.sh

# Preflight -- run before EVERY code commit (non-negotiable)
make preflight
```

`make preflight` runs: format-check, build, unit tests, integration tests, **exec-report-check**. Skipping it is how commit `180d64f` turned `main` red for five runs.

## Exec-Report Protocol (mandatory before push)

**Every push that touches code, examples, configs, or docs outside `docs/exec-reports/` MUST add a new exec-report file** at `docs/exec-reports/sNNeMM-kebab-title.md` per the [`exec-report-format` skill](../.github/skills/exec-report-format.md). Mechanically enforced by `make exec-report-check` (wired into `make preflight`) and the `pre-push` git hook (install via `make install-hooks`).

If you push without one, the gate fails with the next sNNeMM number you should use and a list of the commits in the range.

**Opt out** (use sparingly) by adding a `Skip-Exec-Report: <reason>` trailer (start-of-line, colon-separated, like `Co-authored-by:`) to any commit body in the push range. Legitimate cases: docs typo fixes, dependency bumps, hotfix rollbacks. If in doubt, write the report -- they are short and the season finale retrospective relies on them.

The episode is one report per push, not one per commit. A two-commit field-debug session is one episode. Update CHANGELOG `[Unreleased]` and write the exec-report **before** the final `git push`, not after a reviewer notices.

**Native AOT binary:** `make publish-aot` builds to `dist/aot/`, `make install` copies to `~/.local/bin/az-ai`.

## Architecture

**Single-file entry point:** `azureopenai-cli/Program.cs` (~2200 lines). Contains CLI parsing, all execution modes, model resolution, provider dispatch, and client construction. Large but deliberate -- keeps the hot path in one file for AOT friendliness.

**Four execution modes:**

- Standard (single LLM response)
- Agent (tool-calling loop with built-in tools)
- Ralph (autonomous self-correcting loop with `--validate`)
- Persona (squad member with persistent memory)

**Tool system:** 6 built-in tools in `azureopenai-cli/Tools/`, registered in `ToolRegistry.cs`. Uses `Microsoft.Extensions.AI` (`AIFunctionFactory.Create`) -- no custom interface, tools are static async methods with `[Description]` attributes. Tool aliases (e.g., `shell` -> `shell_exec`) resolved in `ToolRegistry`.

**Multi-provider dispatch:** `BuildChatClient()` in Program.cs routes models to Azure OpenAI or Azure AI Foundry based on `AZURE_FOUNDRY_MODELS` allowlist. `FoundryAuthPolicy` (inner class) handles Foundry's auth header format.

**Auto-config loading:** `LoadConfigEnvFrom()` parses `~/.config/az-ai/env` at startup (shell `export KEY="value"` format). Critical for non-shell contexts (Espanso, AHK, cron) where no login profile runs.

**Model allowlist:** `AZUREOPENAIMODEL` is comma-separated -- first = default, full list = allowed set. `ParseModelEnv()` splits and validates. Requests for unlisted models are rejected.

**Squad persona system:** `azureopenai-cli/Squad/` -- config in `.squad.json`, per-persona memory in `.squad/history/<name>.md` (32 KB cap), keyword-based routing via `SquadCoordinator`.

**Image generation mode:** `--image` switches from chat to image generation. `BuildImageClient()` constructs the image client (same dual-provider dispatch as chat). `RunImageGeneration()` handles the generation flow. `ClipboardImageWriter` copies PNG to the system clipboard (xclip/wl-copy/osascript/powershell). `--image` cannot be combined with `--agent`, `--ralph`, `--persona`, or `--schema`.

## Key Conventions

**Error handling:** Use `ErrorAndExit()` for all fatal exits -- consistent `[ERROR]` prefix on stderr + JSON-aware output. Never duplicate inline error/exit patterns.

**Raw mode:** `--raw` suppresses all formatting (spinner, newline, stderr). Guard any new output path with `isRaw`.

**JSON serialization:** Use `AppJsonContext` (in `JsonGenerationContext.cs`) for all new types -- required for Native AOT. The csproj also sets `JsonSerializerIsReflectionEnabledByDefault=true` for MAF compatibility.

**Security patterns in tools:**

- `ShellExecTool`: rejects `$()`, backticks, `<()`, `>()`, `eval`, `exec`. Uses `ArgumentList` (not `Arguments`). New blocked patterns need tests in `ToolHardeningTests`.
- `ReadFileTool`: NFKC-normalizes paths (Unicode homoglyph defense), blocks sensitive paths.
- `WebFetchTool`: HTTPS-only, SSRF filtering against private/link-local ranges, validates post-redirect URL.
- `ShellExecTool` scrubs sensitive env vars (including `AZURE_IMAGE_MODEL`) from child processes.

**Encoding:** UTF-8 is enforced end-to-end. `Console.OutputEncoding`/`InputEncoding` set at startup. `ProcessStartInfo` in tools uses `StandardOutputEncoding = Encoding.UTF8`. Espanso configs require `$OutputEncoding` for PowerShell 5.1.

**String comparisons:** Always `StringComparison.Ordinal` or `OrdinalIgnoreCase`. The csproj sets `InvariantGlobalization=true`.

## Commit Protocol

Conventional Commits with Copilot co-author trailer:

```text
<type>(<scope>): <imperative subject>

<body>

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```

Types: `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, `bench`, `security`.

Docs-only changes (`*.md`) can skip preflight per the `docs-only-commit` skill.

## Environment Variables

| Variable | Required | Notes |
|----------|----------|-------|
| `AZUREOPENAIENDPOINT` | Yes | Base URL only (e.g., `https://foo.cognitiveservices.azure.com/`) -- SDK adds the path |
| `AZUREOPENAIAPI` | Yes | API key (note: not "KEY") |
| `AZUREOPENAIMODEL` | Yes | Comma-separated deployment names (first = default, all = allowed) |
| `AZURE_FOUNDRY_ENDPOINT` | No | Enables multi-provider routing to Azure AI Foundry |
| `AZURE_FOUNDRY_KEY` | No | API key for Foundry endpoint |
| `AZURE_FOUNDRY_MODELS` | No | Comma-separated model names routed to Foundry |
| `AZURE_IMAGE_MODEL` | No | Image model deployment name for `--image` mode |

Credentials live in `~/.config/az-ai/env` (chmod 600), auto-loaded by `LoadConfigEnvFrom()`.

## File Structure

- `azureopenai-cli/Program.cs` -- CLI entry point, all modes, provider dispatch
- `azureopenai-cli/Tools/*.cs` -- Built-in tools (register new ones in `ToolRegistry.CreateMafTools()`); includes `ClipboardImageWriter.cs` for `--image` clipboard support
- `azureopenai-cli/Squad/` -- Persona config, routing, memory, initialization
- `azureopenai-cli/UserConfig.cs` -- Persists to `~/.azureopenai-cli.json` (`public class` for test access)
- `azureopenai-cli/JsonGenerationContext.cs` -- `AppJsonContext` AOT-compatible serialization
- `tests/AzureOpenAI_CLI.Tests/` -- xUnit tests (naming: `MethodName_Scenario_ExpectedResult`)
- `tests/integration_tests.sh` -- Bash integration assertions
- `examples/espanso-ahk-wsl/` -- Espanso configs (Windows, Linux, macOS) + WSL wrapper
- `docs/proposals/FR-NNN-*.md` -- Feature proposals
- `docs/adr/ADR-NNN-*.md` -- Architecture Decision Records
- `.github/agents/` -- 27 Copilot custom agent personas (see `AGENTS.md`)
- `.github/skills/` -- Procedural skills (preflight, commit, ci-triage, etc.)

## Security Constraints

- Never expose API keys in output or logs
- Shell command blocklist (rm -rf, sudo, etc.) with test coverage in `ToolHardeningTests`
- File reads blocked from sensitive paths (/etc/shadow, ~/.ssh, etc.)
- SSRF protection on web fetches (private/link-local IP ranges)
- Subagent delegation depth capped at 3 (`MaxDepth` in `DelegateTaskTool`)
- `RALPH_DEPTH` env var tracks recursion (0-3, set automatically)

## CI Pipeline (.github/workflows/ci.yml)

3 jobs: `build-and-test` (format + build + unit tests + vuln scan), `integration-test`, `docker` (build + Trivy scan). Mirrors `make preflight` locally.

## Agent Fleet

27 Copilot custom agents in `.github/agents/` organized as a Seinfeld-themed fleet. Showrunner (Larry David) orchestrates; main cast of 5 (Costanza/product, Kramer/engineering, Elaine/docs, Jerry/DevOps, Newman/security) drives the build-ship loop; 21 supporting players cover PM, release, QA, legal, perf, a11y, i18n, etc. See `AGENTS.md` for the full roster and pipeline diagram.

## Skills (procedural how-tos in `.github/skills/`)

| Skill | When to use |
|-------|-------------|
| `preflight` | Before every code commit |
| `commit` | Conventional Commits format + trailer |
| `ci-triage` | Diagnosing a red CI run |
| `ascii-validation` | Check for smart quotes / em-dashes |
| `docs-only-commit` | Decision tree for markdown-only diffs |
| `changelog-append` | Adding to `[Unreleased]` in CHANGELOG.md |

Additional orchestration skills: `episode-brief`, `fleet-dispatch`, `shared-file-protocol`, `exec-report-format`, `writers-room-cast-balance`, `findings-backlog`.
