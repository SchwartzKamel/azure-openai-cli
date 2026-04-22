# Azure OpenAI CLI -- Use Cases

> Index of mode-by-mode recipes. Pick the mode you need, jump to the deep
> dive, and come back if your workflow spans more than one.

## Prerequisites

Set the three required env vars before running any example -- see
[`prerequisites.md`](prerequisites.md) (single source of truth):
`AZUREOPENAIENDPOINT`, `AZUREOPENAIAPI`, `AZUREOPENAIMODEL`.

```bash
az-ai "Explain quantum tunnelling in one sentence"        # standard
echo "teh quik brown fox" | az-ai --raw --system "Fix spelling"
az-ai --agent "What files are in /var/log?"               # agent
az-ai --ralph --validate "pytest" "Fix the failing test"  # autonomous loop
az-ai --persona security "Audit Tools/WebFetchTool.cs"    # named persona
```

Cost-check any prompt without spending a token:

```bash
az-ai --estimate --model gpt-4o "$(cat big-prompt.txt)"
# → {"model":"gpt-4o","input_tokens":…, "estimated_usd":…}
```

---

## Modes at a glance

| Mode              | Flag(s)                      | Model can call tools? | Autonomous loop? | Memory    | Primary guide                                                                 |
|-------------------|------------------------------|:---------------------:|:----------------:|-----------|-------------------------------------------------------------------------------|
| **Standard**      | *(default)*                  | ❌ No | ❌ No | ❌ No | [`use-cases-standard.md`](use-cases-standard.md)                              |
| **Raw**           | `--raw`                      | ❌ No | ❌ No | ❌ No | [`use-cases-standard.md`](use-cases-standard.md) (§ `--raw`)                  |
| **Agent**         | `--agent` [`--tools`]        | ✅ (6 built-ins)      | ❌ No | ❌ No | [`use-cases-agent.md`](use-cases-agent.md)                                    |
| **Ralph**         | `--ralph` [`--validate`]     | ✅ (implies `--agent`)| ✅ Yes | ❌ No | [`use-cases-ralph-squad.md`](use-cases-ralph-squad.md) (Part 1)               |
| **Persona/Squad** | `--persona <name\|auto>`     | ✅ (per persona)      | ❌ (unless `--ralph`) | ✅ (`.squad/`) | [`persona-guide.md`](persona-guide.md) + [`use-cases-ralph-squad.md`](use-cases-ralph-squad.md) (Part 2) |

Config, Espanso/AHK integration, pipelines, Docker, and the security sandbox
are cross-mode -- see [`use-cases-config-integration.md`](use-cases-config-integration.md).

Shared flags that apply to most modes: `--json`, `--raw`, `--system`,
`--temperature`, `--max-tokens`, `--timeout`, `--model`, `--schema`,
`--estimate`, `--telemetry`.

---

## What's new in v2

The 2.0.0 release adds nine flags and wires `--persona` end-to-end. Full
rundown in [`migration-v1-to-v2.md`](migration-v1-to-v2.md) §1. Headline
additions (also listed in `az-ai --help`):

- `--json` -- machine-readable errors and estimator output.
- `--version --short` -- bare semver (`2.0.0`), for packaging scripts.
- `--schema <json>` -- capture a JSON schema (wire enforcement deferred to 2.1.x).
- `--max-rounds <n>` -- agent tool-call cap; default 5, range 1-20.
- `--config <path>` and `--config set/get/list/reset/show` -- alternate-path
  overrides and CRUD for persisted preferences.
- `--completions <bash|zsh|fish>` -- shell completion scripts on stdout.
- `--models` / `--list-models` / `--current-model` / `--set-model
  <alias>=<deployment>` -- model-alias management, persisted to
  `~/.azureopenai-cli.json`.
- `--telemetry` (or `AZ_TELEMETRY=1`) -- opt-in OTel + cost events on stderr.
- `--estimate` / `--dry-run-cost` / `--estimate-with-output <n>` -- predicted
  USD without an API call. Short-circuits before credential resolution.
- `--persona <name|auto>` -- now fully wired via `SquadCoordinator` + memory.

---

## Pick your starting point

- **Just want to prompt a model?** → [`use-cases-standard.md`](use-cases-standard.md)
- **Need the model to run shell/file/web tools?** → [`use-cases-agent.md`](use-cases-agent.md)
- **Building an autonomous fix-until-green loop?** → [`use-cases-ralph-squad.md`](use-cases-ralph-squad.md) Part 1
- **Want named AI teammates with persistent memory?** → [`persona-guide.md`](persona-guide.md)
- **Setting up Espanso, AHK, or model aliases?** → [`use-cases-config-integration.md`](use-cases-config-integration.md)
- **Upgrading from v1.9.x?** → [`migration-v1-to-v2.md`](migration-v1-to-v2.md)
- **Wrangling token spend?** → [`cost-optimization.md`](cost-optimization.md)

---

*Azure OpenAI CLI -- v2.0.0. See [`CHANGELOG.md`](../CHANGELOG.md) for the
currently released version.*
