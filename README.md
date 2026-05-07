# Azure OpenAI CLI ⚡

> A **sub-15 ms cold-start, ~13 MiB single-binary** Azure OpenAI agent for text injection, shell automation, and scripted AI workflows.

[![CI](https://github.com/SchwartzKamel/azure-openai-cli/actions/workflows/ci.yml/badge.svg)](https://github.com/SchwartzKamel/azure-openai-cli/actions/workflows/ci.yml)
[![Release](https://github.com/SchwartzKamel/azure-openai-cli/actions/workflows/release.yml/badge.svg)](https://github.com/SchwartzKamel/azure-openai-cli/actions/workflows/release.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![Platforms](https://img.shields.io/badge/platforms-linux%20%7C%20macOS%20%7C%20windows-informational)](#install)
[![GHCR](https://img.shields.io/badge/ghcr.io-azure--openai--cli-2496ED?logo=docker)](https://github.com/SchwartzKamel/azure-openai-cli/pkgs/container/azure-openai-cli)

## Why

- 🚀 **10.7 ms p50 cold start** -- Native AOT single-file binary (12.97 MiB, linux-x64), fast enough to feel synchronous inside text expanders. Measured on v2.0.6, laptop reference rig -- see [docs/perf/v2.0.5-baseline.md](docs/perf/v2.0.5-baseline.md).
- 🧰 **6 execution modes** -- one-shot prompts, tool-calling agent, autonomous self-correcting loops, named personas with persistent memory, image generation, raw-pipe mode for Espanso/AHK.
- 🔒 **Security hardened** -- shell-injection blocklist, SSRF protection on `web_fetch`, file-read denylist, bounded sub-agent recursion. See [SECURITY.md](SECURITY.md).
- 🖥️ **Cross-platform** -- Pre-built AOT binaries for Linux (glibc/musl/arm64), macOS (x64/arm64), and Windows (x64/arm64).
- 🧪 **1,510+ passing tests** (1,025 v1 + 485 v2 xUnit, plus ~174 bash integration assertions), .NET 10, `Azure.AI.OpenAI 2.1.0` stable.

## First run

Run `az-ai` with no credentials configured and it drops you into a short interactive wizard. As of S03E11 *The Wizard, Reprise* the wizard is provider-aware: pick a default provider (azure, openai, groq, together, cloudflare), enter that provider's credentials, and optionally loop to add a second:

```text
$ az-ai

Welcome to az-ai setup!
This wizard will configure your providers and save them to
  /home/user/.config/az-ai/env
(file mode 0600 on Unix; existing files are backed up first).

Default provider:
    1) azure
  * 2) openai
    3) groq
    4) together
    5) cloudflare
Pick [openai]: 1

Azure OpenAI endpoint URL: https://contoso.openai.azure.com/
Azure OpenAI API key (input hidden): ********************************
Azure model deployment name(s), comma-separated [gpt-4o-mini]: gpt-4o,gpt-4o-mini

Add another provider? [y/N] (remaining: openai, groq, together, cloudflare): y
Which provider:
  * 1) openai
    2) groq
    3) together
    4) cloudflare
Pick [openai]:
Openai API key (input hidden): ****************************
Openai model name(s), comma-separated [gpt-4o-mini]: gpt-4o-mini

Add another provider? [y/N] (remaining: groq, together, cloudflare): n

Configuration saved to /home/user/.config/az-ai/env
Default provider: azure
Providers configured: azure, openai
```

The wizard writes `~/.config/az-ai/env` with E10-format `[provider:NAME]` sections plus a default unsectioned block carrying `AZUREOPENAIENDPOINT`, `AZUREOPENAIAPI`, `AZUREOPENAIMODEL`, and `AZ_AI_COMPAT_MODELS` for back-compat. Compat model strings are validated through `OpenAiCompatAdapter.ParseCompatModels` before the file is written, so a typo never makes it to disk. If a file already exists, the wizard prompts to back it up to `env.bak.<timestamp>` before overwriting; identical re-runs are no-ops (no spurious backup).

- The wizard **auto-runs** when creds are missing *and* you're on an interactive terminal. Scripts, pipes, `--raw`, `--json`, and containers bypass it -- they fail loud with an `[ERROR]` and exit code 1, so nothing in your CI/Espanso/AHK setup loops on closed stdin.
- Re-run it anytime with `az-ai --setup` (aliases: `--init-wizard`, bare `setup`).
- **Environment variables still win.** `AZUREOPENAIENDPOINT` / `AZUREOPENAIAPI` / `AZUREOPENAIMODEL` override stored config every time -- the wizard is for humans, env vars are for machines.

### Where is my key stored?

| OS | Location | Backing |
|---|---|---|
| Windows | `%USERPROFILE%\.azureopenai-cli.json` | Encrypted with DPAPI (user-scoped) |
| macOS | `~/.azureopenai-cli.json` + login Keychain | Apple Keychain (service `az-ai`) |
| Linux | `~/.azureopenai-cli.json` | libsecret (GNOME Keyring / KDE Wallet) when `/usr/bin/secret-tool` and a DBus session are present; otherwise plaintext, file mode `0600` |
| Docker / CI | env vars only | No on-disk storage |

On Linux, `az-ai` prefers libsecret when it's available on your desktop session (GNOME Keyring, KDE Wallet via the libsecret bridge) -- no key on disk, just a non-secret fingerprint. On headless boxes, minimal installs, or containers -- anywhere `/usr/bin/secret-tool` or `DBUS_SESSION_BUS_ADDRESS` is missing -- it falls back to plaintext at `0600`, same posture as AWS CLI, GitHub CLI, and Azure CLI. Honest trade-off over security theater; the compensating control is rotation.

### Key rotation

The Azure OpenAI key ring gives you two active keys per resource, which means you can rotate with **zero downtime**:

1. In the Azure portal, open your resource → **Keys and Endpoint** → **Regenerate Key 2**.
2. Swap your local config to Key 2 (`az-ai --init`, paste the new key).
3. Regenerate Key 1 to finish the cycle.

Recommendations:

- Rotate every **~90 days** as a baseline.
- Revoke immediately if a device is lost or you suspect compromise.
- For higher assurance, skip on-disk storage entirely and use env vars / Docker secrets / a secret manager -- env always takes precedence.

## Quickstart

```bash
git clone https://github.com/SchwartzKamel/azure-openai-cli && cd azure-openai-cli
make setup && make install        # installs ~/.local/bin/az-ai
cp azureopenai-cli/.env.example .env && $EDITOR .env   # add Azure creds (template is shared across v1/v2)
az-ai --raw "Summarize this file in 5 words: $(cat README.md)"
```

You need an Azure OpenAI resource -- grab the [endpoint](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/create-resource), a [deployed model](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/deploy-models), and the [API key](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/create-resource#retrieve-key-and-endpoint).

## Execution Modes

| Mode | Flag | One-liner |
|------|------|-----------|
| **Standard** | *(default)* | Streaming chat completion with spinner and token stats. |
| **Raw** | `--raw` | Clean stdout only -- designed for Espanso, AutoHotkey, and shell pipes. |
| **Agent** | `--agent` | Model can call tools: `shell`, `file`, `web`, `clipboard`, `datetime`, `delegate`. |
| **Ralph** | `--ralph` | Autonomous loop -- agent retries against a validator (`--validate "dotnet test"`) until it passes. |
| **Persona / Squad** | `--persona <name>` | Named AI team members with per-persona system prompts, tools, and persistent memory in `.squad/`. |
| **Image** | `--image` | Generate an image from a text prompt (DALL-E / FLUX.2-pro via the same provider dispatch). |

Full flag reference: `az-ai --help`.

Scripting tip: `az-ai --version --short` emits bare semver (e.g. `2.0.0`) -- ideal for packaging scripts, release automation, and shell `$()` substitutions.

### New in v2.0.0

| Flag | What it does |
|------|--------------|
| `--json` | Machine-readable output. Errors go to stdout as structured JSON with `error`, `message`, and `exit_code` fields. |
| `--schema <json>` | Capture a JSON schema for structured output (wire enforcement lands in 2.1.x). |
| `--max-rounds <n>` | Agent tool-call cap. Default `5`, range `1-20`. |
| `--config <path>` | Use an alternate config file instead of `./.azureopenai-cli.json` or `~/.azureopenai-cli.json`. |
| `--config set/get/list/reset/show` | Full CRUD for the persistent user config (e.g. `az-ai --config set defaults.temperature=0.3`). |
| `--completions <bash\|zsh\|fish>` | Emit a shell-completion script to stdout. Source it or drop it into your completions dir. |
| `--models`, `--list-models`, `--current-model`, `--set-model <alias>=<deployment>` | Persist alias → deployment mappings in `~/.azureopenai-cli.json`. First `--set-model` also becomes the default. |
| `--telemetry` (or `AZ_TELEMETRY=1`) | Opt-in OpenTelemetry spans + per-call cost events on stderr. Zero overhead when off. |
| `--estimate` / `--dry-run-cost` / `--estimate-with-output <n>` | Predict USD cost for a prompt **without calling the API**. Short-circuits before credential resolution -- safe for CI budget gates. |
| `--persona <name\|auto>` | Named persona routing -- now wired end-to-end via `SquadCoordinator` and `PersonaMemory`. Personas may pin a `provider` and/or `model` field in `.squad.json` (S03E23 *The Persona, Multi-Provider*); the pin sits between profile and default in the precedence chain (`cli > env > profile > persona > default`) and is validated at load time against the known-providers list. Missing creds for a pinned provider drop the pin and warn (silent under `--raw` / `--json`). See [docs/persona-guide.md](docs/persona-guide.md). |

Upgrading from v1.9.x? See [docs/migration-v1-to-v2.md](docs/migration-v1-to-v2.md). Nothing breaks; a lot adds.

### Telemetry (opt-in)

Default off. Set `AZ_AI_TELEMETRY=1` (strict-equality on the literal string `"1"` -- not `"true"`, not `"yes"`, not `"1 "`) to emit one structured NDJSON event per dispatch on stderr. Eight fields: `event_id`, `ts`, `model`, `provider`, `dispatch_path`, `latency_ms_bucket`, `outcome`, `error_class`. Never emits prompts, completions, tokens, API keys, endpoints, file paths, or stack traces. Sample event:

```json
{"event_id":"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee","ts":"2026-05-09T12:34:56.789Z","model":"gpt-4o-mini","provider":"azure","dispatch_path":"azure-default","latency_ms_bucket":"250","outcome":"success","error_class":null}
```

Schema, SLOs, privacy charter, and the upstream-pricing review cadence: [docs/observability/slo.md](docs/observability/slo.md).

## Performance

Cold-start and binary-size figures for v2.0.5 are being re-measured on the current release matrix. See [docs/perf/v2.0.5-baseline.md](docs/perf/v2.0.5-baseline.md) for current numbers -- v1.8 legacy figures are no longer representative and have been removed here pending that refresh.

Relative ordering is unchanged: **Native AOT** (`make install`, no .NET runtime) is the fastest and smallest; **ReadyToRun** (`make publish-r2r`) is JIT-assisted and requires the runtime; **Docker (Alpine)** pays container + runtime cold-start overhead on every invocation. The AOT binary remains the only option fast enough to feel synchronous inside text expanders like Espanso and AutoHotkey.

Contributors: see [docs/perf/bench-workflow.md](docs/perf/bench-workflow.md) for which `make bench*` target to run when (`bench-quick` for the pre-commit loop, `bench` mid-PR, `bench-full` pre-merge / release).

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

Most humans should just run `az-ai` and let the [first-run wizard](#first-run) handle it. This section is for everything downstream of that: precedence rules, the full env-var surface, and the scripted-setup path.

Precedence (highest → lowest): **CLI flag > environment variable > user config > built-in default** (`gpt-4o-mini` for model). An explicit `--config <path>` takes priority over `./.azureopenai-cli.json`, which takes priority over `~/.azureopenai-cli.json`. Inspect the effective config with `az-ai --config show`.

The binary also auto-loads `~/.config/az-ai/env` at startup (shell `export KEY="value"` format, written by `make setup-secrets`). Existing env vars are not overwritten, so your shell profile still wins. This is critical for non-login-shell contexts like Espanso, AHK, and cron where your profile isn't sourced.

### Per-provider sections (S03E10 *The Keychain*)

The env file accepts optional INI-style section headers so credentials for each provider live in their own namespace and cannot accidentally cross-contaminate. The default (unsectioned) content keeps working unchanged -- existing files do **not** need to be edited.

```text
# Default section -- shell-export compatible, back-compat with every existing file.
export AZUREOPENAIENDPOINT="https://my-resource.openai.azure.com/"
export AZUREOPENAIAPI="azure-key-here"
export AZUREOPENAIMODEL="gpt-4o,gpt-4o-mini"

# Per-provider section -- keys are namespaced by section header.
[provider:openai]
API_KEY=sk-openai-key-here          # -> sets OPENAI_API_KEY

[provider:groq]
API_KEY=gsk_groq-key-here           # -> sets GROQ_API_KEY

[provider:cloudflare]
API_TOKEN=cf_token-here             # -> sets CLOUDFLARE_API_TOKEN
```

Recognised provider sections: `azure`, `openai`, `foundry`, `groq`, `together`, `cloudflare`. Unknown sections emit a `[WARNING]` to stderr (silent under `--raw` / `--json`) and are skipped without aborting startup. The default section keeps shell-source compatibility -- `source ~/.config/az-ai/env` still works for any unsectioned content.

Full env-var reference (single source of truth): [docs/prerequisites.md](docs/prerequisites.md).

### Power user / scripted setup

If you're deploying in a container, wiring this into CI, or just prefer env vars, skip the wizard by exporting the three required variables -- env vars always win over stored config:

```bash
export AZUREOPENAIENDPOINT="https://my-resource.openai.azure.com/"
export AZUREOPENAIAPI="<key>"
export AZUREOPENAIMODEL="gpt-4o,gpt-4o-mini"
```

Or drop them in a `.env` file (`cp azureopenai-cli/.env.example .env`) and source it -- the same template still works for both v1 and v2.

| Variable | Required | Default | Description |
|----------|:--------:|--------:|-------------|
| `AZUREOPENAIENDPOINT` | ✅ | -- | Azure OpenAI resource endpoint |
| `AZUREOPENAIAPI` | ✅ | -- | Azure OpenAI API key |
| `AZUREOPENAIMODEL` | ✅ | -- | Comma-separated deployment names (first = default, all = allowed set) |
| `SYSTEMPROMPT` |  | *(built-in)* | Default system prompt |
| `AZURE_MAX_TOKENS` |  | `10000` | Max output tokens (1-128000) |
| `AZURE_TEMPERATURE` |  | `0.55` | Sampling temperature (0.0-2.0) |
| `AZURE_TIMEOUT` |  | `120` | Streaming timeout (seconds) |
| `AZ_TELEMETRY` |  | *unset* | Set to `1` to enable OTel + cost events (equivalent to `--telemetry`) |
| `AZURE_FOUNDRY_ENDPOINT` |  | -- | Azure AI Foundry / GitHub Models endpoint URL (enables multi-provider routing) |
| `AZURE_FOUNDRY_KEY` |  | -- | API key for the Foundry endpoint |
| `AZURE_FOUNDRY_MODELS` |  | -- | Comma-separated model names routed to Foundry instead of Azure OpenAI |
| `AZURE_IMAGE_MODEL` |  | -- | Image model deployment name. Resolution: `AZURE_IMAGE_MODEL` > first model in `AZURE_FOUNDRY_MODELS` > chat model fallback |

Switch models on the fly: `az-ai --models`, `az-ai --set-model gpt-4o` (persisted to `~/.azureopenai-cli.json`).

Keeping token spend sane -- model selection, caching, and per-persona budgets: [docs/cost-optimization.md](docs/cost-optimization.md).

### Multi-provider routing (Foundry / GitHub Models)

Set `AZURE_FOUNDRY_ENDPOINT`, `AZURE_FOUNDRY_KEY`, and `AZURE_FOUNDRY_MODELS` to route specific models through Azure AI Foundry or GitHub Models instead of Azure OpenAI. Any model listed in `AZURE_FOUNDRY_MODELS` is dispatched to the Foundry endpoint via `FoundryAuthPolicy` (swaps Bearer auth to `api-key` header); all other models use the default Azure OpenAI path. Configure interactively with `make set-foundry ENDPOINT=... KEY=... MODELS=...` and inspect with `make providers`. See [docs/adr/ADR-005-foundry-routing.md](docs/adr/ADR-005-foundry-routing.md).

### OpenAI-compatible providers (S03E09 *The Compat*)

ADR-010 ships an OpenAI-compat seam: any provider that speaks the OpenAI `/v1/chat/completions` wire protocol shows up as a *preset* against `OpenAiCompatAdapter`. Built-in presets: `openai`, `groq`, `together`, `cloudflare`, `llamacpp`. Each preset names the env var it reads its API key from -- credentials never live in code or config.

Route specific models with `AZ_AI_COMPAT_MODELS` (comma-separated `preset:model` pairs). **Precedence:** Azure Foundry allowlist (`AZURE_FOUNDRY_MODELS`) wins, then OpenAI-compat allowlist, then default Azure OpenAI. Example:

```bash
export AZ_AI_COMPAT_MODELS="openai:gpt-4o-mini,groq:llama-3.1-70b"
export OPENAI_API_KEY="sk-..."
export GROQ_API_KEY="gsk-..."
az-ai --model gpt-4o-mini "Summarize this changelog"   # routes to api.openai.com
az-ai --model llama-3.1-70b "Same, but on Groq"        # routes to api.groq.com
```

Cloudflare Workers AI additionally requires `CLOUDFLARE_ACCOUNT_ID` (substituted into the endpoint URL). See [docs/adr/ADR-010-first-non-azure-cloud.md](docs/adr/ADR-010-first-non-azure-cloud.md).

### Capability gate (S03E18 *The Capability Gate*)

The CLI ships a provider+model feature matrix and a dispatch-time gate that refuses requests the downstream model cannot honour. Tool-call requests against models that do not advertise tool-calling, and vision requests against text-only models, fail fast with a friendly error and exit code `2` -- no more confused 4xx surfacing through the wire. `--schema` against a model without `json_mode` warns to stderr and degrades to a regular completion.

Override the matrix when our snapshot is wrong:

```bash
# I know this Together model handles tool-calls; let it through.
export AZ_AI_CAPABILITY_OVERRIDES="together:meta-llama-3.1-70b-instruct:tool_calls=true"
```

Format is comma-separated `preset:model:capability=bool` (capabilities: `tool_calls`, `streaming`, `vision`, `json_mode`). Malformed entries warn and are skipped.

### Choosing a provider and model (S03E20 *The Switch*)

Three knobs, one documented precedence chain. CLI flag wins, then env var, then the named profile in `preferences.json`, then a built-in default. The same chain runs for the provider rail and the model rail; the profile rail is optional (skipped entirely when no `--profile` and no `AZ_PROFILE` is set).

```text
provider: --provider  >  AZ_PROVIDER  >  profile.provider  >  default heuristic
profile : --profile   >  AZ_PROFILE   >  (none)
model   : --model     >  AZ_MODEL     >  profile.model     >  AZUREOPENAIMODEL[0] / AZ_AI_COMPAT_MODELS[provider] / fallback
```

The default heuristic for provider follows a documented six-rung ladder (ADR-011, S03E22 *The Default*): (1) `azure` when both `AZUREOPENAIENDPOINT` and `AZUREOPENAIAPI` are set; (2) the single preset whose `AZ_AI_<PRESET>_ENDPOINT` is set when exactly one is present; (3) when ≥2 preset endpoints are set, `AZ_AI_LOCAL_PROVIDERS=1`, and at least one endpoint URL points to a loopback host on a known local-runtime port (ollama 11434, llamacpp 8080, lmstudio 1234), the first such preset alphabetically with the label `default:<preset>:local-detected`; (4) `openai` when `OPENAI_API_KEY` is set; (5) the alphabetically first preset endpoint when ≥2 are set and no other signal applies (with a `multiple-presets-no-cli-no-profile-no-env-pin` warning so you know the cascade went there); (6) `azure:fallback` when nothing matched (BuildChatClient will then fail closed with a friendly error). The match is URL-string only -- no socket probe; ProviderDoctor (`--diag`) keeps the live probe. `--config show` reports every rail's source label so you can see exactly where the resolved value came from:

```bash
$ az-ai --profile work --config show | grep -A4 'Switch resolution'
Switch resolution (S03E20):
  source:           profile:work:provider
  provider source:  profile:work:provider
  model source:     profile:work:model
  profile source:   cli

# Override a profile's provider for one invocation:
$ az-ai --profile work --provider openai "draft a release note"
```

### Makefile targets

The core targets (`make setup`, `make install`, `make test`, `make preflight`) are documented in [CONTRIBUTING.md](CONTRIBUTING.md). Additional management targets:

| Target | Purpose |
|--------|---------|
| `make models` | List allowed models from env file |
| `make add-model MODEL=<name>` | Add a deployment to the allowlist |
| `make remove-model MODEL=<name>` | Remove a deployment from the allowlist |
| `make providers` | Show configured providers (Azure OpenAI + Foundry) |
| `make set-foundry ENDPOINT=... KEY=... MODELS=...` | Configure Foundry/GitHub Models provider |
| `make load-env` | Print the `source` command for `~/.config/az-ai/env` |
| `make run-native ARGS="..."` | Run the native binary with env auto-loaded |
| `make espanso-install` | Install hardened Espanso config for WSL Path B |
| `make espanso-test` | Verify Espanso can reach az-ai through WSL |
| `make set-image-model MODEL=<name>` | Set the default image model deployment name |

## Image Generation

`--image` switches from chat completion to image generation. Works with both Azure OpenAI (DALL-E) and Foundry (FLUX.2-pro) via the same provider dispatch.

```bash
# Generate an image (saves PNG to temp file, copies to clipboard)
az-ai --image "a cat in a top hat, oil painting"

# Specify dimensions and output path
az-ai --image --size 512x512 --output cat.png "a cat in space"

# Pipe-friendly: base64 on stdout, no file saved
echo "sunset" | az-ai --image --raw | base64 -d > sunset.png
```

| Flag | Purpose |
|------|---------|
| `--image` | Enable image generation mode |
| `--output <path>` | Save PNG to an explicit file (default: temp file with timestamp) |
| `--size <WxH>` | Image dimensions, e.g. `1024x1024`, `512x512` |

Output behavior:

- **Default:** saves PNG to a temp file, copies to clipboard, prints the file path to stdout.
- **`--raw`:** writes base64 to stdout (pipe-friendly, no file saved).
- **`--json`:** emits `{"image":"/path","clipboard":true,"bytes":12345}`.

`--image` cannot be combined with `--agent`, `--ralph`, `--persona`, or `--schema`.

Set the image model deployment with `AZURE_IMAGE_MODEL` or `make set-image-model MODEL=<name>`.

## Security

The CLI is meant to be given shell and file access inside agent mode, so defense-in-depth matters. `shell_exec` blocks a denylist of destructive commands and enforces timeouts; `web_fetch` is HTTPS-only with SSRF filtering against private/link-local ranges; `read_file` refuses sensitive paths and caps read size; `delegate_task` recursion is depth-capped. Credentials are never baked into the binary or Docker image -- always injected at runtime.

**Local providers (S03E16).** The OpenAI-compat dispatch path passes every preset URL through `Net/EndpointAllowlist.cs` before constructing a client. Loopback, RFC1918, link-local (including the cloud metadata service at `169.254.169.254`), and IPv6 ULA endpoints are blocked by default; set `AZ_AI_LOCAL_PROVIDERS=1` (strict equality) to opt in to local runtimes such as Ollama or `llama-server`. Multicast, broadcast, userinfo URLs, and privileged ports stay blocked even with opt-in. Audit details: [docs/audits/security-v2.1.3-allowlist.md](docs/audits/security-v2.1.3-allowlist.md).

**Air-gapped operation (S03E26).** Pass `--offline` (or set `AZ_AI_OFFLINE=1`, strict equality) to forbid every non-loopback provider call -- Azure SDK, Foundry SDK, OpenAI-compat, `web_fetch`, the OTLP exporter, and the prewarm probe all gate on the same latch. The flag is layered on top of the loopback opt-in: `--offline` does NOT relax `AZ_AI_LOCAL_PROVIDERS=1`, so demos and air-gapped reviews can still talk to a local Ollama. `--doctor` reflects the gate row by row (`dns: blocked-offline`, `healthy: false`) so the state is auditable from outside the process.

```sh
# Air-gapped demo against a loopback Ollama
AZ_AI_LOCAL_PROVIDERS=1 az-ai --offline --model ollama-llama3 "..."
```

For paranoid runs (a guarantee that no syscall can reach a non-loopback socket), pair the flag with kernel-level isolation: `unshare -n az-ai --offline ...` on Linux. The flag is a logical gate, not a network namespace. Audit details: [docs/audits/security-v2.1.4-offline.md](docs/audits/security-v2.1.4-offline.md).

**Best-effort fallback chain (S03E22).** Pass `--fallback openai,groq` (or set `AZ_AI_FALLBACK=openai,groq`, CLI wins) to opt in to a fallback chain. When the primary provider returns a transient failure (5xx / 429 / network timeout) the chain is tried in order, max 3 alternates, no duplicates. Auth (401/403), other 4xx, capability mismatches, and user-cancel (Ctrl-C) all short-circuit -- no point asking another provider when the request itself is the problem. Streaming has a load-bearing invariant: once the first chunk has been yielded, fallback is OFF; mid-stream truncation prints a one-line `[fallback] stream-truncated` warn on stderr and re-throws the original exception. The transcript is never corrupted by a provider switch mid-flight. Default is off; opt-in only. Telemetry adds two opt-in event shapes (`fallback_attempt`, `fallback_outcome`) under the existing `AZ_AI_TELEMETRY=1` strict-equality gate. Reliability charter: [docs/observability/slo.md](docs/observability/slo.md) §2 / §5 (the new `fallback.rate` SLI + alert thresholds).

```sh
# Try OpenAI then Groq if Azure 5xx's
az-ai --fallback openai,groq --model gpt-4o-mini "..."
```

Full threat model and hardening checklist: [SECURITY.md](SECURITY.md). Report vulnerabilities per the policy there. To cryptographically verify a downloaded binary, container, or SBOM against the build attestations, see [docs/verifying-releases.md](docs/verifying-releases.md).

### Security & supply chain

Every PR regenerates `dist/sbom.json` and a provider-attributed CVE
report (`dist/provider-cve-report.json`) via
[`.github/workflows/sbom.yml`](.github/workflows/sbom.yml). Trivy
findings are bucketed into `azure` / `openai` / `shared` per
[`scripts/provider-deps.json`](scripts/provider-deps.json), so an
operator on the default Azure path can read past a compat-only CVE
without losing the signal. Per-provider severity tolerances and triage
cadence: [docs/security/cve-policy.md](docs/security/cve-policy.md).
Local run: `make cve-report` (requires Trivy + jq).

### Rotating credentials

`az-ai --rotate-creds [provider]` (S03E25) rotates the API key for one
configured provider in `~/.config/az-ai/env`. The flow takes a
timestamped backup (`env.bak.<ISO-8601-Z>`, never overwritten -- bumps
to `.bak.<ts>.1` on collision), then atomically writes the new file
(tmp + rename) under mode 0600. The key is read with hidden input and
never logged on any success, failure, or exception path; every textual
line is routed through `SecretRedactor`. Interactive only -- refuses
under `--raw` or when stdin/stdout are redirected. To change the
endpoint or add a new provider, run `az-ai --setup` instead.

## Accessibility

`az-ai` honors [no-color.org](https://no-color.org) (`NO_COLOR`),
`TERM=dumb`, `FORCE_COLOR`, and `CLICOLOR` / `CLICOLOR_FORCE`. The
`--plain` flag (and `AZ_AI_PLAIN=1` env var, S03E14) suppress banner /
color / unicode glyphs / spinner for screen-reader, low-bandwidth, and
CI-log consumers; default CLI output is ASCII-only and ANSI-free. See
[docs/accessibility.md](docs/accessibility.md) for the full policy,
glyph-alternatives table, supported screen readers (Orca, NVDA, JAWS,
VoiceOver), and the plain-mode test matrix.

### Diagnosing your setup

`az-ai --doctor` (S03E15) probes every configured provider and prints a
health table -- DNS reachability, credential presence (boolean only,
never the value), and model-allowlist count. No authenticated API call
is ever issued; output is routed through `SecretRedactor` as
defense-in-depth. Add `--json` for machine-readable output or `--plain`
for ASCII key:value stanzas. Exit code 0 = all healthy, 1 = at least
one unhealthy.

```text
$ az-ai --doctor
+--------------------+----------------------------------------------+-------------+-------+--------+
| provider           | endpoint                                     | dns         | creds | models |
+--------------------+----------------------------------------------+-------------+-------+--------+
| azure              | https://example.cognitiveservices.azure.c... | ok          | yes   | 2      |
| compat:openai      | https://api.openai.com/v1                    | ok          | yes   | 1      |
+--------------------+----------------------------------------------+-------------+-------+--------+
all 2 provider(s) healthy
```

## Install

### Pre-built binaries

Download for your platform from [Releases](https://github.com/SchwartzKamel/azure-openai-cli/releases). Filenames follow the `az-ai-<version>-<rid>` scheme (v2.2.0 shown):

| Platform | Artifact |
|----------|----------|
| Linux x64 (glibc) | `az-ai-2.2.0-linux-x64.tar.gz` |
| Linux x64 (musl / Alpine) | `az-ai-2.2.0-linux-musl-x64.tar.gz` |
| macOS (Apple Silicon) | `az-ai-2.2.0-osx-arm64.tar.gz` |
| Windows x64 | `az-ai-2.2.0-win-x64.zip` |

> **Note:** `osx-x64` (Intel macOS) was dropped from the release matrix in **v2.0.4** after sustained runner instability. Intel-Mac users should run the Docker image or build from source until the leg is reinstated -- see [docs/runbooks/macos-runner-triage.md](docs/runbooks/macos-runner-triage.md) for the triage plan and current status. `linux-arm64` and `win-arm64` are also not built in v2; track the ADR in [docs/adr/](docs/adr/) for plans to reintroduce them.

### Docker (GHCR)

Secondary option -- native AOT is recommended for latency-sensitive use.

```bash
docker pull ghcr.io/schwartzkamel/azure-openai-cli:latest
docker run --rm --env-file .env ghcr.io/schwartzkamel/azure-openai-cli:latest "Hello world"
```

## Documentation

📚 **Start here:** [docs/README.md](docs/README.md) -- the full docs map (architecture, ops, security, perf, process, ADRs, exec reports, proposals).

### Demo

Run the Season 3 finale demo end-to-end (mock-only, no real API
calls, ~30 seconds wall-clock):

```bash
DOTNET_ROOT=/usr/lib/dotnet make publish-aot && make install
bash scripts/demo/season3-finale.sh
```

Five acts -- Setup, Switch, Rules, Fallback, Curtain Call -- with 22
asserted invariants. See [scripts/demo/README.md](scripts/demo/README.md)
for the asciinema recording recipe, and
[docs/season-recaps/season-3-recap.md](docs/season-recaps/season-3-recap.md)
for the marketing-grade season retrospective.

### Architecture & decisions

- [ARCHITECTURE.md](ARCHITECTURE.md) -- system design, tool registry, squad internals
- [AGENTS.md](AGENTS.md) -- fleet dispatch pattern and the 25-agent roster
- [docs/adr/](docs/adr/) -- Architecture Decision Records

### Operating the CLI

- [docs/prerequisites.md](docs/prerequisites.md) -- required environment variables (single source of truth)
- [docs/use-cases.md](docs/use-cases.md) -- end-to-end workflow recipes (indexes the per-mode guides)
- [docs/persona-guide.md](docs/persona-guide.md) -- persona + Squad reference (`--persona`, `.squad.json`, memory)
- [docs/espanso-ahk-integration.md](docs/espanso-ahk-integration.md) -- text expansion setup
- [docs/cost-optimization.md](docs/cost-optimization.md) -- token budgeting and per-persona cost profiles
- [docs/onboarding/local-providers.md](docs/onboarding/local-providers.md) -- the first hour with a local model on your laptop (Ollama walkthrough; S03E19)

### Security

- [SECURITY.md](SECURITY.md) -- threat model and reporting
- [docs/verifying-releases.md](docs/verifying-releases.md) -- cosign / attestation verification

### Accessibility

- [docs/accessibility.md](docs/accessibility.md) -- `NO_COLOR`, `--raw`, exit codes, keyboard-only workflows, known gaps

### Release & migration

- [CHANGELOG.md](CHANGELOG.md) -- release history
- [docs/migration-v1-to-v2.md](docs/migration-v1-to-v2.md) -- user-facing v1 → v2.0.0 upgrade notes
- [docs/v2-migration.md](docs/v2-migration.md) -- internal MAF-adoption phase plan
- [CONTRIBUTING.md](CONTRIBUTING.md) -- dev workflow and PR expectations

### Internationalization

- [docs/i18n.md](docs/i18n.md) -- `InvariantGlobalization` contract, USD-only cost policy, non-ASCII / RTL / CJK notes, reserved `--locale` flag

### Glossary

- **Ralph mode (autonomous Wiggum loop)** -- agentic self-correcting loop: run task → validate → feed errors back → retry. See [use-cases-ralph-squad.md](docs/use-cases-ralph-squad.md).

## License

[MIT](LICENSE). Third-party attributions in [NOTICE](NOTICE). Contributions welcome -- see [CONTRIBUTING.md](CONTRIBUTING.md), the [Code of Conduct](CODE_OF_CONDUCT.md), and the roll call in [CONTRIBUTORS.md](CONTRIBUTORS.md).
