# Architecture

> **Status: stub.** The v1 architecture document has been archived to
> [`docs/archive/ARCHITECTURE-v1.md`](docs/archive/ARCHITECTURE-v1.md). That
> document describes the v1 source tree (`azureopenai-cli/`), single-`Program.cs`
> layout, Alpine `runtime-deps:9.0-preview` base, and `Azure.AI.OpenAI 2.1.0`
> dependency -- **none of which match v2**. It is kept for historical reference
> only; do not treat it as authoritative for the current release.

## Where the v2 architecture lives

The v2 system (`azureopenai-cli-v2/`, shipped in v2.0.0) is documented across
several focused files rather than a single monolith. Start here:

| Topic | Canonical doc |
|---|---|
| v2 scope, behavior contracts, and what changed vs v1 | [`docs/release-notes-v2.0.0.md`](docs/release-notes-v2.0.0.md) |
| Migration guide (v1 → v2 flags, env vars, exit codes) | [`docs/migration-v1-to-v2.md`](docs/migration-v1-to-v2.md) |
| Use-case walkthroughs (standard, `--agent`, Ralph, Squad, config) | [`docs/use-cases.md`](docs/use-cases.md) and siblings (`use-cases-agent.md`, `use-cases-ralph-squad.md`, `use-cases-standard.md`, `use-cases-config-integration.md`) |
| Configuration reference (env vars, `~/.azureopenai-cli.json`, precedence) | [`docs/config-reference.md`](docs/config-reference.md) |
| Security posture, threat model, vulnerability reporting | [`SECURITY.md`](SECURITY.md) and [`docs/security/`](docs/security/) |
| Observability, logs, metrics, OTEL wiring | [`docs/observability.md`](docs/observability.md) |
| AOT / trim investigation, binary-size budget | [`docs/aot-trim-investigation.md`](docs/aot-trim-investigation.md) |
| Architecture Decision Records | [`docs/adr/`](docs/adr/) |
| Release runbook (cut, publish, hash-sync) | [`docs/runbooks/release-runbook.md`](docs/runbooks/release-runbook.md) |

## Quick orientation (v2)

- **Source tree:** `azureopenai-cli-v2/` -- `Program.cs`, `Ralph/`, `Squad/`,
  `Tools/`, `Observability/`, `Cache/`, `Theme.cs`, `UserConfig.cs`,
  `JsonGenerationContext.cs`. The v1 tree (`azureopenai-cli/`) is
  maintenance-only per [`CONTRIBUTING.md`](CONTRIBUTING.md).
- **Distribution:** self-contained AOT binaries (`linux-x64`,
  `linux-musl-x64`, `osx-arm64`, `win-x64`) via GitHub Releases; Docker
  image at `ghcr.io/schwartzkamel/azure-openai-cli/az-ai-v2`. macOS Intel
  (`osx-x64`) is no longer in the release matrix as of v2.0.4 -- see
  [`CHANGELOG.md`](CHANGELOG.md).
- **Credentials:** `.env` is **never baked into images**. It is injected at
  runtime via `--env-file` or bind-mount. See
  [`SECURITY.md`](SECURITY.md) §Credential handling and
  [`README.md`](README.md) §Security.
- **Agent framework:** v2 uses **Microsoft Agent Framework**
  (`Microsoft.Agents.AI`) in-process for tool-calling and Ralph
  iterations -- it does **not** shell out to child CLIs via `Process.Start`
  for subagent delegation. See
  [`docs/release-notes-v2.0.0.md`](docs/release-notes-v2.0.0.md).

## Contributing new architecture content

New architecturally significant decisions land as ADRs in
[`docs/adr/`](docs/adr/) -- see the ADR index there for format and examples.
Deep-dive design docs go under `docs/` next to the topic-specific files
above, not into this stub.
