# Architecture

> The v1 architecture document has been archived to
> [`docs/archive/ARCHITECTURE-v1.md`](docs/archive/ARCHITECTURE-v1.md).
> It is kept for historical reference only.

## Where the architecture lives

The system (`azureopenai-cli/`, shipped in v2.0.0) is documented across
several focused files rather than a single monolith. Start here:

| Topic | Canonical doc |
|---|---|
| v2 scope, behavior contracts, and what changed vs v1 | [`docs/release-notes-v2.0.0.md`](docs/release-notes-v2.0.0.md) |
| Migration guide (v1 → v2 flags, env vars, exit codes) | [`docs/migration-v1-to-v2.md`](docs/migration-v1-to-v2.md) |
| Use-case walkthroughs (standard, `--agent`, Ralph, Squad, config) | [`docs/use-cases.md`](docs/use-cases.md) and siblings (`use-cases-agent.md`, `use-cases-ralph-squad.md`, `use-cases-standard.md`, `use-cases-config-integration.md`) |
| Configuration reference (env vars, `~/.azureopenai-cli.json`, precedence) | [`docs/config-reference.md`](docs/config-reference.md) |
| Provider routing (Azure OpenAI vs Foundry dispatch) | [`docs/adr/ADR-005-foundry-routing.md`](docs/adr/ADR-005-foundry-routing.md) |
| Model resolution chain (allowlist, aliases, fallback) | [`docs/adr/ADR-009-default-model-resolution.md`](docs/adr/ADR-009-default-model-resolution.md) |
| Security posture, threat model, vulnerability reporting | [`SECURITY.md`](SECURITY.md) and [`docs/security/`](docs/security/) |
| Observability, logs, metrics, OTEL wiring | [`docs/observability.md`](docs/observability.md) |
| AOT / trim investigation, binary-size budget | [`docs/aot-trim-investigation.md`](docs/aot-trim-investigation.md) |
| Architecture Decision Records | [`docs/adr/`](docs/adr/) |
| Release runbook (cut, publish, hash-sync) | [`docs/runbooks/release-runbook.md`](docs/runbooks/release-runbook.md) |

## Quick orientation

- **Source tree:** `azureopenai-cli/` -- `Program.cs`, `Ralph/`, `Squad/`,
  `Tools/`, `Observability/`, `Cache/`, `Theme.cs`, `UserConfig.cs`,
  `JsonGenerationContext.cs`.
- **Distribution:** self-contained AOT binaries (`linux-x64`,
  `linux-musl-x64`, `osx-arm64`, `win-x64`) via GitHub Releases; Docker
  image at `ghcr.io/schwartzkamel/azure-openai-cli/az-ai`. macOS Intel
  (`osx-x64`) is no longer in the release matrix as of v2.0.4 -- see
  [`CHANGELOG.md`](CHANGELOG.md).
- **Startup sequence:** `DotEnv.Load()` → `LoadConfigEnv()`
  (`~/.config/az-ai/env`, shell `export KEY="value"` format, does not
  overwrite existing env vars) → arg parsing → telemetry init → `RunAsync`.
- **Credentials:** `.env` and `~/.config/az-ai/env` supply credentials at
  runtime. Neither is baked into images; inject via `--env-file` or
  bind-mount. See [`SECURITY.md`](SECURITY.md) §Credential handling and
  [`README.md`](README.md) §Security.
- **Client construction:** `BuildChatClient()` (Program.cs) is a factory
  that dispatches to **Azure OpenAI** (`AzureOpenAIClient`) or **Foundry**
  (`OpenAI.ChatClient` + `FoundryAuthPolicy`) based on env vars.
  `AZURE_FOUNDRY_ENDPOINT` + `AZURE_FOUNDRY_MODELS` opt a model into the
  Foundry path; everything else stays on Azure OpenAI. See
  [ADR-005](docs/adr/ADR-005-foundry-routing.md).
- **Image generation:** `BuildImageClient()` constructs an image client
  using the same dual-provider dispatch. `RunImageGeneration()` sends the
  prompt, saves the PNG (or emits base64 in `--raw` mode), and invokes
  `ClipboardImageWriter` to copy the result to the system clipboard.
  Model resolution: `AZURE_IMAGE_MODEL` > first model in
  `AZURE_FOUNDRY_MODELS` > chat model fallback. Works with Azure OpenAI
  (DALL-E) and Foundry (FLUX.2-pro).
- **Model resolution:** CLI flag → `AZUREOPENAIMODEL` (comma-separated;
  first = default, all = allowlist) → `UserConfig.ResolveSmartDefault()` →
  hardcoded fallback (`gpt-4o-mini`). Allowlist is enforced when multiple
  models are configured. See
  [ADR-009](docs/adr/ADR-009-default-model-resolution.md).
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
