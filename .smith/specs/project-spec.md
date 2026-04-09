# Project Specification: azure-openai-cli

## Identity
- **Name:** Azure OpenAI CLI
- **Version:** 1.4.0
- **Language:** C# / .NET 10
- **License:** MIT
- **Repository:** https://github.com/SchwartzKamel/azure-openai-cli

## Purpose
Command-line tool for Azure OpenAI API interaction. Designed for text injection via AHK/Espanso hotkey workflows. Supports standard chat, agentic tool-calling, and autonomous Ralph (Wiggum) loop modes.

## Architecture
- Entry: azureopenai-cli/Program.cs (~1300 lines)
- Tools: azureopenai-cli/Tools/ (6 tools implementing IBuiltInTool)
- Config: azureopenai-cli/UserConfig.cs
- Tests: tests/AzureOpenAI_CLI.Tests/ (301 unit tests, xUnit)
- Integration: tests/integration_tests.sh (78 tests)
- CI: .github/workflows/ci.yml (3 jobs: build-and-test, integration-test, docker)

## Modes
1. **Standard** — Single prompt → single response
2. **Agent** (`--agent`) — Multi-round tool-calling loop with streaming
3. **Ralph** (`--ralph`) — Autonomous Wiggum loop with external validation

## Dependencies
- Azure.AI.OpenAI 2.9.0-beta.1 (tool calling requires beta)
- Azure.Core 1.51.1
- dotenv.net 3.1.2

## Quality Gates
- `dotnet format azure-openai-cli.sln --verify-no-changes`
- `dotnet test` (301 unit tests)
- `bash tests/integration_tests.sh` (78 integration tests)
- `dotnet list package --vulnerable` (0 vulnerabilities)
- Trivy container scan
