# Azure OpenAI CLI ⚡

> A **5ms-startup, 9 MB single-binary** Azure OpenAI agent for text injection, shell automation, and scripted AI workflows.

[![CI](https://github.com/SchwartzKamel/azure-openai-cli/actions/workflows/ci.yml/badge.svg)](https://github.com/SchwartzKamel/azure-openai-cli/actions/workflows/ci.yml)
[![Release](https://github.com/SchwartzKamel/azure-openai-cli/actions/workflows/release.yml/badge.svg)](https://github.com/SchwartzKamel/azure-openai-cli/actions/workflows/release.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![Platforms](https://img.shields.io/badge/platforms-linux%20%7C%20macOS%20%7C%20windows-informational)](#install)
[![GHCR](https://img.shields.io/badge/ghcr.io-azure--openai--cli-2496ED?logo=docker)](https://github.com/SchwartzKamel/azure-openai-cli/pkgs/container/azure-openai-cli)

![It's alive!](img/its_alive_too.gif)

## Why

- 🚀 **5.4 ms cold start** — Native AOT single-file binary, fast enough to feel synchronous inside text expanders.
- 🧰 **5 execution modes** — one-shot prompts, tool-calling agent, autonomous self-correcting loops, named personas with persistent memory, raw-pipe mode for Espanso/AHK.
- 🔒 **Security hardened** — shell-injection blocklist, SSRF protection on `web_fetch`, file-read denylist, bounded sub-agent recursion. See [SECURITY.md](SECURITY.md).
- 🖥️ **Cross-platform** — Pre-built AOT binaries for Linux (glibc/musl/arm64), macOS (x64/arm64), and Windows (x64/arm64).
- 🧪 **538 passing tests**, .NET 10, `Azure.AI.OpenAI 2.1.0` stable.

## Quickstart

```bash
git clone https://github.com/SchwartzKamel/azure-openai-cli && cd azure-openai-cli
make setup && make install        # installs ~/.local/bin/az-ai
cp azureopenai-cli/.env.example .env && $EDITOR .env   # add Azure creds
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

Scripting tip: `az-ai --version --short` emits bare semver (e.g. `1.8.0`) — ideal for packaging scripts, release automation, and shell `$()` substitutions.

## Performance

Measured on `linux-x64`, median of 10 runs of `--version`:

| Build | Cold start | Binary size | Notes |
|-------|-----------:|------------:|-------|
| **Native AOT** | **5.4 ms** | ~9 MB | Default via `make install`. No .NET runtime required. |
| ReadyToRun | ~54 ms | ~70 MB | `make publish-r2r`. JIT-assisted, needs runtime. |
| Docker (Alpine) | ~400 ms | ~120 MB | Container + runtime cold start. |

The AOT binary is ~75× faster to start than the Docker image — the difference between "feels instant in Espanso" and "noticeable lag on every trigger".

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

Set via environment, `.env` file, or `~/.azureopenai-cli.json`. Precedence: **CLI flag > user config > env > default**. Inspect the effective config with `az-ai --config show`.

| Variable | Required | Default | Description |
|----------|:--------:|--------:|-------------|
| `AZUREOPENAIENDPOINT` | ✅ | — | Azure OpenAI resource endpoint |
| `AZUREOPENAIAPI` | ✅ | — | Azure OpenAI API key |
| `AZUREOPENAIMODEL` | ✅ | — | Comma-separated deployment names (first = default) |
| `SYSTEMPROMPT` |  | *(built-in)* | Default system prompt |
| `AZURE_MAX_TOKENS` |  | `10000` | Max output tokens (1–128000) |
| `AZURE_TEMPERATURE` |  | `0.55` | Sampling temperature (0.0–2.0) |
| `AZURE_TIMEOUT` |  | `120` | Streaming timeout (seconds) |

Switch models on the fly: `az-ai --models`, `az-ai --set-model gpt-4o` (persisted to `~/.azureopenai-cli.json`).

Keeping token spend sane — model selection, caching, and per-persona budgets: [docs/cost-optimization.md](docs/cost-optimization.md).

## Security

The CLI is meant to be given shell and file access inside agent mode, so defense-in-depth matters. `shell_exec` blocks a denylist of destructive commands and enforces timeouts; `web_fetch` is HTTPS-only with SSRF filtering against private/link-local ranges; `read_file` refuses sensitive paths and caps read size; `delegate_task` recursion is depth-capped. Credentials are never baked into the binary or Docker image — always injected at runtime.

Full threat model and hardening checklist: [SECURITY.md](SECURITY.md). Report vulnerabilities per the policy there. To cryptographically verify a downloaded binary, container, or SBOM against the build attestations, see [docs/verifying-releases.md](docs/verifying-releases.md).

## Install

### Pre-built binaries

Download for your platform from [Releases](https://github.com/SchwartzKamel/azure-openai-cli/releases):

| Platform | Artifact |
|----------|----------|
| Linux x64 (glibc) | `azure-openai-cli-linux-x64.tar.gz` |
| Linux x64 (musl / Alpine) | `azure-openai-cli-linux-musl-x64.tar.gz` |
| Linux arm64 | `azure-openai-cli-linux-arm64.tar.gz` |
| macOS x64 / arm64 | `azure-openai-cli-osx-{x64,arm64}.tar.gz` |
| Windows x64 / arm64 | `azure-openai-cli-win-{x64,arm64}.zip` |

### Docker (GHCR)

Secondary option — native AOT is recommended for latency-sensitive use.

```bash
docker pull ghcr.io/schwartzkamel/azure-openai-cli:latest
docker run --rm --env-file .env ghcr.io/schwartzkamel/azure-openai-cli:latest "Hello world"
```

## Documentation

- [ARCHITECTURE.md](ARCHITECTURE.md) — system design, tool registry, squad internals
- [AGENTS.md](AGENTS.md) — fleet dispatch pattern and the 25-agent roster
- [CHANGELOG.md](CHANGELOG.md) — release history
- [SECURITY.md](SECURITY.md) — threat model and reporting
- [docs/espanso-ahk-integration.md](docs/espanso-ahk-integration.md) — text expansion setup
- [docs/use-cases.md](docs/use-cases.md) — end-to-end workflow recipes
- [docs/verifying-releases.md](docs/verifying-releases.md) — cosign / attestation verification
- [docs/cost-optimization.md](docs/cost-optimization.md) — token budgeting and per-persona cost profiles
- [CONTRIBUTING.md](CONTRIBUTING.md) — dev workflow and PR expectations

## License

[MIT](LICENSE). Third-party attributions in [NOTICE](NOTICE). Contributions welcome — see [CONTRIBUTING.md](CONTRIBUTING.md), the [Code of Conduct](CODE_OF_CONDUCT.md), and the roll call in [CONTRIBUTORS.md](CONTRIBUTORS.md).
