# FR-003: Local User Preferences & First-Class Config Command

> **Status: Superseded by FR-014.** The flag surface and `~/.azureopenai-cli.json`
> reader shipped in v2.0.0 as part of FR-014's Phase 1 (legacy backward-compat
> reader). Future work on preferences, aliases, and directory overrides tracks
> in [FR-014](FR-014-local-preferences-and-multi-provider.md).

**Priority:** P1 -- High  
**Impact:** Makes the tool feel personal -- "my tool" vs "a tool"  
**Effort:** Small-Medium (1-2 days)  
**Category:** Configuration UX

---

## The Problem

The current `UserConfig` only stores one thing: which model is active. Everything else -- temperature, max tokens, system prompt, timeout -- is locked in the `.env` file, which is a shared, project-level artifact baked into the Docker image at build time.

This creates three friction points:

1. **Rebuilding the image to change a setting.** Want to try `temperature=0.9` for creative writing? Rebuild the entire Docker image. Want to switch system prompts? Rebuild. This is a ~30-second tax on every experiment.

2. **No per-user preferences.** Two developers sharing the same repo can't have different temperatures or system prompts without maintaining separate `.env` files and rebuilding separately.

3. **No discoverability.** Users don't know what they can configure without reading the README. There's no `--config` command that says "here's what you can tune and what it's currently set to."

The `.env` file was a solid v1 choice -- simple, portable, Docker-friendly. But user preferences belong in `UserConfig`, not in a build artifact.

---

## The Proposal

### 1. Expand `UserConfig` to Hold All Tunable Parameters

```json
{
  "ActiveModel": "gpt-4o",
  "AvailableModels": ["gpt-4", "gpt-35-turbo", "gpt-4o"],
  "Temperature": 0.55,
  "MaxTokens": 10000,
  "TimeoutSeconds": 120,
  "SystemPrompt": null,
  "Profiles": {
    "code": {
      "Temperature": 0.2,
      "SystemPrompt": "You are a precise code assistant. Output only code unless asked to explain."
    },
    "creative": {
      "Temperature": 0.95,
      "SystemPrompt": "You are a creative writing partner. Be vivid and imaginative."
    }
  },
  "ActiveProfile": null
}
```

**Precedence chain (highest wins):**
1. Command-line flags (`--temperature 0.9`)
2. Active profile settings
3. UserConfig defaults
4. Environment variables (`.env`)
5. Hardcoded defaults

### 2. Add a `--config` Command Suite

```bash
# Show all current settings and their sources
az-ai --config

# Output:
# Azure OpenAI CLI -- Configuration
# ─────────────────────────────────
# Endpoint:      https://my-resource.openai.azure.com  (env)
# Active Model:  gpt-4o                                 (user config)
# Temperature:   0.55                                   (default)
# Max Tokens:    10000                                  (default)
# Timeout:       120s                                   (default)
# System Prompt: "You are a secure, concise..."         (env)
# Config File:   ~/.azureopenai-cli.json
#
# Profiles:
#   code      -- temp=0.2, custom system prompt
#   creative  -- temp=0.95, custom system prompt

# Set a value
az-ai --config set temperature 0.8
az-ai --config set system-prompt "You are a DevOps expert."
az-ai --config set max-tokens 4000

# Reset to default
az-ai --config reset temperature

# Show where a specific setting comes from
az-ai --config get temperature
# Temperature: 0.8 (user config) -- default is 0.55
```

### 3. Add Inline Flags for One-Off Overrides

```bash
# Override temperature for a single query (no persistence)
az-ai --temperature 0.9 "Write a poem about Kubernetes"

# Override system prompt for a single query
az-ai --system "You are a pirate" "Explain Docker networking"

# Override max tokens
az-ai --max-tokens 500 "TL;DR of quantum computing"
```

These flags don't change the config file -- they apply only to the current invocation. This is how users experiment before committing to a setting.

### 4. Profiles for Context Switching

Profiles are named bundles of settings:

```bash
# Create a profile
az-ai --config profile create code --temperature 0.2 --system "Output only code."

# Activate a profile
az-ai --config profile use code

# Use a profile for one query without activating it
az-ai --profile creative "Write a haiku about recursion"

# List profiles
az-ai --config profile list
```

Profiles solve the "I need different settings for different tasks" problem without requiring the user to remember and re-type flags. A developer might use `code` during the day and `creative` in the evening.

---

## Implementation Notes

### Config File vs Environment Variable Interaction

The `.env` file should remain the source of truth for **credentials** (endpoint, API key). User preferences move to `UserConfig`. The precedence chain means the `.env` still works as a baseline, but UserConfig overrides are preferred.

```csharp
// In Program.cs, after loading both sources:
int maxTokens = config.MaxTokens                          // UserConfig
    ?? TryParseEnvInt("AZURE_MAX_TOKENS", null)           // .env
    ?? 10000;                                              // hardcoded default
```

### Runtime-Injected `.env` + User Config

Since the Makefile injects `.env` at runtime via `--env-file`, and `UserConfig` is at `~/.azureopenai-cli.json` inside the container, users need a volume mount to persist config across container runs:

```makefile
DOCKER_CMD := docker run --rm --env-file .env \
    -v $(HOME)/.azureopenai-cli.json:/home/appuser/.azureopenai-cli.json \
    $(FULL_IMAGE)
```

This should be the default in the Makefile. If the file doesn't exist on the host, Docker will create it as an empty file -- which `UserConfig.Load()` already handles gracefully.

---

## Why This Is P1

Configuration is how a user makes a tool *theirs*. Right now, this tool feels like a rental car -- you can drive it, but you can't adjust the seat. A developer who can't tweak the temperature or swap system prompts without rebuilding Docker images will never form the daily-driver habit that turns users into advocates.

The `--config` command also serves as **self-documentation**. Instead of reading a README table, users run `az-ai --config` and see everything in context, with sources. This reduces support questions and increases confidence.

The competitive angle: `aichat` has `roles` (equivalent to profiles). `sgpt` has `--temperature` flags. This proposal combines both: persistent profiles AND one-off flags. That's a better model than either.

---

## Exit Criteria

- [ ] `UserConfig` stores temperature, max tokens, timeout, and system prompt
- [ ] `az-ai --config` shows all settings with their sources
- [ ] `az-ai --config set <key> <value>` persists a setting
- [ ] `az-ai --temperature <val>` overrides for a single invocation
- [ ] `az-ai --system "<prompt>"` overrides system prompt for a single invocation
- [ ] Profiles can be created, listed, activated, and used per-invocation
- [ ] Makefile mounts `~/.azureopenai-cli.json` by default
- [ ] Existing `.env`-only workflows are unaffected (backward compatible)
