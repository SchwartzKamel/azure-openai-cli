# Azure OpenAI CLI ⚡

> A **sub-15 ms cold-start, ~13 MiB single-binary** Azure OpenAI agent for text injection, shell automation, and scripted AI workflows.

[![CI](https://github.com/SchwartzKamel/azure-openai-cli/actions/workflows/ci.yml/badge.svg)](https://github.com/SchwartzKamel/azure-openai-cli/actions/workflows/ci.yml)
[![Release](https://github.com/SchwartzKamel/azure-openai-cli/actions/workflows/release.yml/badge.svg)](https://github.com/SchwartzKamel/azure-openai-cli/actions/workflows/release.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![Platforms](https://img.shields.io/badge/platforms-linux%20%7C%20macOS%20%7C%20windows-informational)](#install)
[![GHCR](https://img.shields.io/badge/ghcr.io-azure--openai--cli-2496ED?logo=docker)](https://github.com/SchwartzKamel/azure-openai-cli/pkgs/container/azure-openai-cli)

## Why

- 🚀 **10.7 ms p50 cold start** — Native AOT single-file binary (12.97 MiB, linux-x64), fast enough to feel synchronous inside text expanders. Measured on v2.0.6, laptop reference rig — see [docs/perf/v2.0.5-baseline.md](docs/perf/v2.0.5-baseline.md).
- 🧰 **5 execution modes** — one-shot prompts, tool-calling agent, autonomous self-correcting loops, named personas with persistent memory, raw-pipe mode for Espanso/AHK.
- 🔒 **Security hardened** — shell-injection blocklist, SSRF protection on `web_fetch`, file-read denylist, bounded sub-agent recursion. See [SECURITY.md](SECURITY.md).
- 🖥️ **Cross-platform** — Pre-built AOT binaries for Linux (glibc/musl/arm64), macOS (x64/arm64), and Windows (x64/arm64).
- 🧪 **1,510+ passing tests** (1,025 v1 + 485 v2 xUnit, plus ~174 bash integration assertions), .NET 10, `Azure.AI.OpenAI 2.1.0` stable.

## Quickstart

```bash
git clone https://github.com/SchwartzKamel/azure-openai-cli && cd azure-openai-cli
make setup && make install        # installs ~/.local/bin/az-ai
cp azureopenai-cli/.env.example .env && $EDITOR .env   # add Azure creds (template is shared across v1/v2)
az-ai --raw "Summarize this file in 5 words: $(cat README.md)"
```

You need an Azure OpenAI resource — grab the [endpoint](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/create-resource), a [deployed model](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/deploy-models), and the [API key](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/create-resource#retrieve-key-and-endpoint).

## Execution Modes

| Mode | Flag | One-liner |
|------|------|-----------|
| **Standard** | *(default)* | Streaming chat completion with spinner and token stats. |
| **Raw** | `--raw` | Clean stdout only — designed for Espanso, AutoHotkey, and shell pipes. |
| **Agent** | `--agent` | Model can call tools: `shell`, `file`, `web`, `clipboard`, `datetime`, `delegate`. |
| **Ralph** | `--ralph` | Autonomous loop — agent retries against a validator (`--validate "dotnet test"`) until it passes. |
| **Persona / Squad** | `--persona <name>` | Named AI team members with per-persona system prompts, tools, and persistent memory in `.squad/`. |

Full flag reference: `az-ai --help`.

Scripting tip: `az-ai --version --short` emits bare semver (e.g. `2.0.0`) — ideal for packaging scripts, release automation, and shell `$()` substitutions.

### New in v2.0.0

| Flag | What it does |
|------|--------------|
| `--json` | Machine-readable output. Errors go to stdout as structured JSON with `error`, `message`, and `exit_code` fields. |
| `--schema <json>` | Capture a JSON schema for structured output (wire enforcement lands in 2.1.x). |
| `--max-rounds <n>` | Agent tool-call cap. Default `5`, range `1–20`. |
| `--config <path>` | Use an alternate config file instead of `./.azureopenai-cli.json` or `~/.azureopenai-cli.json`. |
| `--config set/get/list/reset/show` | Full CRUD for the persistent user config (e.g. `az-ai --config set defaults.temperature=0.3`). |
| `--completions <bash\|zsh\|fish>` | Emit a shell-completion script to stdout. Source it or drop it into your completions dir. |
| `--models`, `--list-models`, `--current-model`, `--set-model <alias>=<deployment>` | Persist alias → deployment mappings in `~/.azureopenai-cli.json`. First `--set-model` also becomes the default. |
| `--telemetry` (or `AZ_TELEMETRY=1`) | Opt-in OpenTelemetry spans + per-call cost events on stderr. Zero overhead when off. |
| `--estimate` / `--dry-run-cost` / `--estimate-with-output <n>` | Predict USD cost for a prompt **without calling the API**. Short-circuits before credential resolution — safe for CI budget gates. |
| `--persona <name\|auto>` | Named persona routing — now wired end-to-end via `SquadCoordinator` and `PersonaMemory`. See [docs/persona-guide.md](docs/persona-guide.md). |

Upgrading from v1.9.x? See [docs/migration-v1-to-v2.md](docs/migration-v1-to-v2.md). Nothing breaks; a lot adds.

## Performance

Cold-start and binary-size figures for v2.0.5 are being re-measured on the current release matrix. See [docs/perf/v2.0.5-baseline.md](docs/perf/v2.0.5-baseline.md) for current numbers — v1.8 legacy figures are no longer representative and have been removed here pending that refresh.

Relative ordering is unchanged: **Native AOT** (`make install`, no .NET runtime) is the fastest and smallest; **ReadyToRun** (`make publish-r2r`) is JIT-assisted and requires the runtime; **Docker (Alpine)** pays container + runtime cold-start overhead on every invocation. The AOT binary remains the only option fast enough to feel synchronous inside text expanders like Espanso and AutoHotkey.

## Espanso / AutoHotkey

Drop an AI layer into any text field on your OS:

```yaml
# ~/.config/espanso/match/ai.yml
matches:
  - trigger: ":aifix"
    replace: "{{output}}"
    vars:
      - name: output
        type: shell
        params:
          cmd: "xclip -selection clipboard -o | az-ai --raw --system 'Fix grammar. Output ONLY corrected text.'"
```

Type `:aifix` → clipboard text goes in → corrected prose comes out in-place. `--raw` strips spinner/formatting so only clean text is injected.

📖 Full integration guide (Espanso + AHK v2, macOS/Windows variants, perf tuning): [docs/espanso-ahk-integration.md](docs/espanso-ahk-integration.md).

## Configuration

Set via environment, `.env` file, or `~/.azureopenai-cli.json`. Precedence: **CLI flag > environment variable > user config > built-in default** (`gpt-4o-mini` for model). An explicit `--config <path>` takes priority over `./.azureopenai-cli.json`, which takes priority over `~/.azureopenai-cli.json`. Inspect the effective config with `az-ai --config show`.

Full env-var reference (single source of truth): [docs/prerequisites.md](docs/prerequisites.md).

| Variable | Required | Default | Description |
|----------|:--------:|--------:|-------------|
| `AZUREOPENAIENDPOINT` | ✅ | — | Azure OpenAI resource endpoint |
| `AZUREOPENAIAPI` | ✅ | — | Azure OpenAI API key |
| `AZUREOPENAIMODEL` | ✅ | — | Comma-separated deployment names (first = default) |
| `SYSTEMPROMPT` |  | *(built-in)* | Default system prompt |
| `AZURE_MAX_TOKENS` |  | `10000` | Max output tokens (1–128000) |
| `AZURE_TEMPERATURE` |  | `0.55` | Sampling temperature (0.0–2.0) |
| `AZURE_TIMEOUT` |  | `120` | Streaming timeout (seconds) |
| `AZ_TELEMETRY` |  | *unset* | Set to `1` to enable OTel + cost events (equivalent to `--telemetry`) |

Switch models on the fly: `az-ai --models`, `az-ai --set-model gpt-4o` (persisted to `~/.azureopenai-cli.json`).

Keeping token spend sane — model selection, caching, and per-persona budgets: [docs/cost-optimization.md](docs/cost-optimization.md).

## Security

The CLI is meant to be given shell and file access inside agent mode, so defense-in-depth matters. `shell_exec` blocks a denylist of destructive commands and enforces timeouts; `web_fetch` is HTTPS-only with SSRF filtering against private/link-local ranges; `read_file` refuses sensitive paths and caps read size; `delegate_task` recursion is depth-capped. Credentials are never baked into the binary or Docker image — always injected at runtime.

Full threat model and hardening checklist: [SECURITY.md](SECURITY.md). Report vulnerabilities per the policy there. To cryptographically verify a downloaded binary, container, or SBOM against the build attestations, see [docs/verifying-releases.md](docs/verifying-releases.md).

## Install

### Pre-built binaries

Download for your platform from [Releases](https://github.com/SchwartzKamel/azure-openai-cli/releases). Filenames follow the `az-ai-v2-<version>-<rid>` scheme (v2.0.5 shown):

| Platform | Artifact |
|----------|----------|
| Linux x64 (glibc) | `az-ai-v2-2.0.5-linux-x64.tar.gz` |
| Linux x64 (musl / Alpine) | `az-ai-v2-2.0.5-linux-musl-x64.tar.gz` |
| macOS (Apple Silicon) | `az-ai-v2-2.0.5-osx-arm64.tar.gz` |
| Windows x64 | `az-ai-v2-2.0.5-win-x64.zip` |

> **Note:** `osx-x64` (Intel macOS) was dropped from the release matrix in **v2.0.4** after sustained runner instability. Intel-Mac users should run the Docker image or build from source until the leg is reinstated — see [docs/runbooks/macos-runner-triage.md](docs/runbooks/macos-runner-triage.md) for the triage plan and current status. `linux-arm64` and `win-arm64` are also not built in v2; track the ADR in [docs/adr/](docs/adr/) for plans to reintroduce them.

### Docker (GHCR)

Secondary option — native AOT is recommended for latency-sensitive use.

```bash
docker pull ghcr.io/schwartzkamel/azure-openai-cli:latest
docker run --rm --env-file .env ghcr.io/schwartzkamel/azure-openai-cli:latest "Hello world"
```

## Documentation

### Architecture & decisions
- [ARCHITECTURE.md](ARCHITECTURE.md) — system design, tool registry, squad internals
- [AGENTS.md](AGENTS.md) — fleet dispatch pattern and the 25-agent roster
- [docs/adr/](docs/adr/) — Architecture Decision Records

### Operating the CLI
- [docs/prerequisites.md](docs/prerequisites.md) — required environment variables (single source of truth)
- [docs/use-cases.md](docs/use-cases.md) — end-to-end workflow recipes (indexes the per-mode guides)
- [docs/persona-guide.md](docs/persona-guide.md) — persona + Squad reference (`--persona`, `.squad.json`, memory)
- [docs/espanso-ahk-integration.md](docs/espanso-ahk-integration.md) — text expansion setup
- [docs/cost-optimization.md](docs/cost-optimization.md) — token budgeting and per-persona cost profiles

### Security
- [SECURITY.md](SECURITY.md) — threat model and reporting
- [docs/verifying-releases.md](docs/verifying-releases.md) — cosign / attestation verification

### Accessibility
- [docs/accessibility.md](docs/accessibility.md) — `NO_COLOR`, `--raw`, exit codes, keyboard-only workflows, known gaps

### Release & migration
- [CHANGELOG.md](CHANGELOG.md) — release history
- [docs/migration-v1-to-v2.md](docs/migration-v1-to-v2.md) — user-facing v1 → v2.0.0 upgrade notes
- [docs/v2-migration.md](docs/v2-migration.md) — internal MAF-adoption phase plan
- [CONTRIBUTING.md](CONTRIBUTING.md) — dev workflow and PR expectations

### Internationalization
- [docs/i18n.md](docs/i18n.md) — `InvariantGlobalization` contract, USD-only cost policy, non-ASCII / RTL / CJK notes, reserved `--locale` flag

### Glossary
- **Ralph mode (autonomous Wiggum loop)** — agentic self-correcting loop: run task → validate → feed errors back → retry. See [use-cases-ralph-squad.md](docs/use-cases-ralph-squad.md).

## License

[MIT](LICENSE). Third-party attributions in [NOTICE](NOTICE). Contributions welcome — see [CONTRIBUTING.md](CONTRIBUTING.md), the [Code of Conduct](CODE_OF_CONDUCT.md), and the roll call in [CONTRIBUTORS.md](CONTRIBUTORS.md).
