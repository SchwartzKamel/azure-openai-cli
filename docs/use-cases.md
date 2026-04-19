# Azure OpenAI CLI — Complete Use Cases Guide

> **54+ features** across 6 operational modes, with real-world examples for every one.

## Table of Contents

| Mode | Features | Doc |
|------|----------|-----|
| [Standard Mode](#standard-mode) | Prompting, streaming, --raw, --json, tokens, --system, --schema, exit codes | [use-cases-standard.md](use-cases-standard.md) |
| [Agent Mode](#agent-mode) | --agent, 6 built-in tools, parallel execution, tool selection | [use-cases-agent.md](use-cases-agent.md) |
| [Ralph + Squad Mode](#ralph--squad-mode) | --ralph, --validate, personas, auto-routing, memory | [use-cases-ralph-squad.md](use-cases-ralph-squad.md) |
| [Config + Integration + Security](#config--integration--security) | Models, Espanso, AHK, pipelines, sandboxing, SSRF | [use-cases-config-integration.md](use-cases-config-integration.md) |

---

## Quick Start

```bash
# Set up credentials
export AZUREOPENAIENDPOINT="https://your-resource.openai.azure.com/"
export AZUREOPENAIAPI="your-api-key"
export AZUREOPENAIMODEL="gpt-4o"

# Basic prompt
az-ai "explain quantum computing in one sentence"

# Pipe text through AI
echo "teh quik brown fox" | az-ai --raw --system "Fix spelling"

# Agent mode with tools
az-ai --agent "what files are in my home directory?"

# Ralph autonomous loop
az-ai --ralph --validate "python -m pytest" "Fix the failing test in test_auth.py"

# Squad persona
az-ai --persona security "audit this codebase for vulnerabilities"
```

---

## Standard Mode

14 features covering basic prompting through structured output.

:point_right: **[Full Standard Mode Use Cases →](use-cases-standard.md)**

### Highlights

```bash
# Stream with token tracking
az-ai "write a haiku about Kubernetes"
# Output: haiku text + [tokens: 42→18, 60 total]

# Raw mode for Espanso/AHK (no formatting)
az-ai --raw "one-liner about Docker"

# JSON output with jq
az-ai --json "explain REST" | jq '.response'

# Structured output with schema
az-ai --schema '{"type":"object","properties":{"name":{"type":"string"},"age":{"type":"integer"}},"required":["name","age"]}' \
  "Extract: John Smith is 42 years old"

# Custom system prompt + piped input
cat error.log | az-ai --system "You are a DevOps expert. Diagnose this error."

# Temperature control
az-ai -t 0.0 "What is 2+2?"          # Deterministic
az-ai -t 1.5 "Write a poem about AI"  # Creative
```

---

## Agent Mode

12 features with 6 built-in tools for autonomous multi-step tasks.

:point_right: **[Full Agent Mode Use Cases →](use-cases-agent.md)**

### Highlights

```bash
# Let the AI use tools autonomously
az-ai --agent "what's running on port 8080?"

# Restrict tools for safety
az-ai --agent --tools file,datetime "summarize my README.md"

# Multi-step workflow
az-ai --agent "read package.json, check for outdated deps, and suggest updates"

# Web research
az-ai --agent --tools web "fetch https://api.github.com/zen and explain it"

# Limit rounds for cost control
az-ai --agent --max-rounds 3 "quick system health check"
```

**Built-in Tools:** `shell_exec` · `read_file` · `web_fetch` · `get_clipboard` · `get_datetime` · `delegate_task`

---

## Ralph + Squad Mode

13 features for autonomous loops and AI team personas.

:point_right: **[Full Ralph + Squad Use Cases →](use-cases-ralph-squad.md)**

### Highlights

```bash
# Self-correcting loop with validation
az-ai --ralph --validate "dotnet test" "Fix the NullReferenceException in UserService.cs"

# Task from file
az-ai --ralph --validate "npm run build" --task-file feature-spec.md

# Initialize squad
az-ai --squad-init

# Use a specific persona
az-ai --persona coder "implement binary search in Python"
az-ai --persona security "audit Tools/WebFetchTool.cs"

# Auto-route to best persona
az-ai --persona auto "review the authentication flow for security issues"

# List available personas
az-ai --personas
```

**Default Personas:** `coder` · `reviewer` · `architect` · `writer` · `security`

---

## Config + Integration + Security

17 features covering configuration, text expansion, and hardened security.

:point_right: **[Full Config + Integration + Security Use Cases →](use-cases-config-integration.md)**

### Highlights

```bash
# Model management
az-ai --models                    # List all models
az-ai --set-model gpt-4o-mini    # Switch to fast model
az-ai --config show               # Show full config

# Pipeline integration
git diff | az-ai --raw --system "Summarize these changes in one paragraph"
cat error.log | az-ai --raw --system "What went wrong?"
az-ai --json "explain X" | jq -r '.response'

# Docker (secure, non-root)
docker run --rm -e AZUREOPENAIENDPOINT -e AZUREOPENAIAPI -e AZUREOPENAIMODEL \
  ghcr.io/schwartzkamel/azure-openai-cli "hello world"
```

---

## Feature Coverage Matrix

| # | Feature | Mode | Flag/Mechanism |
|---|---------|------|----------------|
| 1 | Basic prompting | Standard | positional args |
| 2 | Stdin piping | Standard | pipe `\|` |
| 3 | Streaming + spinner | Standard | default |
| 4 | Token tracking | Standard | stderr display |
| 5 | Raw mode | Standard | `--raw` |
| 6 | JSON output | Standard | `--json` |
| 7 | Temperature | Standard | `-t`, `--temperature` |
| 8 | Max tokens | Standard | `--max-tokens` |
| 9 | System prompt | Standard | `--system` |
| 10 | Structured output | Standard | `--schema` |
| 11 | Timeout | Standard | `AZURE_TIMEOUT` |
| 12 | Exit codes | Standard | 0/1/2/3/99 |
| 13 | Prompt size limit | Standard | 32K chars |
| 14 | Retry logic | Standard | auto backoff |
| 15 | Agent mode | Agent | `--agent` |
| 16 | Round limit | Agent | `--max-rounds` |
| 17 | Tool selection | Agent | `--tools` |
| 18 | shell_exec | Agent | tool |
| 19 | read_file | Agent | tool |
| 20 | web_fetch | Agent | tool |
| 21 | get_clipboard | Agent | tool |
| 22 | get_datetime | Agent | tool |
| 23 | delegate_task | Agent | tool |
| 24 | Parallel tools | Agent | auto |
| 25 | Agent status | Agent | stderr |
| 26 | Ralph mode | Ralph | `--ralph` |
| 27 | Validation | Ralph | `--validate` |
| 28 | Task file | Ralph | `--task-file` |
| 29 | Max iterations | Ralph | `--max-iterations` |
| 30 | Ralph log | Ralph | `.ralph-log` |
| 31 | Squad init | Squad | `--squad-init` |
| 32 | Persona select | Squad | `--persona` |
| 33 | Auto-routing | Squad | `--persona auto` |
| 34 | List personas | Squad | `--personas` |
| 35 | Persona memory | Squad | `.squad/history/` |
| 36 | Custom personas | Squad | `.squad.json` |
| 37 | Config show | Config | `--config show` |
| 38 | List models | Config | `--models` |
| 39 | Set model | Config | `--set-model` |
| 40 | Current model | Config | `--current-model` |
| 41 | User config | Config | `~/.azureopenai-cli.json` |
| 42 | Env precedence | Config | flags > env > config |
| 43 | .env support | Config | `.env` file |
| 44 | Espanso | Integration | YAML triggers |
| 45 | AutoHotKey | Integration | AHK v2 scripts |
| 46 | Pipelines | Integration | shell pipes |
| 47 | Help | Help | `--help`, `-h` |
| 48 | Version | Help | `--version`, `-v` |
| 49 | Shell sandbox | Security | blocked cmds |
| 50 | File sandbox | Security | blocked paths |
| 51 | SSRF protection | Security | 3-layer defense |
| 52 | Input validation | Security | size/range limits |
| 53 | Docker security | Security | non-root + env |
| 54 | DotEnv resilience | Standard | try-catch |

---

*Generated for Azure OpenAI CLI — 54 features, 6 modes, ~3,600 lines of examples. See [CHANGELOG](../CHANGELOG.md) for the currently released version.*
