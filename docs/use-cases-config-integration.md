# Use Cases: Configuration, Integration & Security

> Working examples for every config, integration, and security feature of the Azure OpenAI CLI (`az-ai`).

---

## Table of Contents

1. [Configuration Management](#configuration-management)
   - [--config show](#1---config-show)
   - [--models / --list-models](#2---models----list-models)
   - [--set-model](#3---set-model-name)
   - [--current-model](#4---current-model)
   - [User Config File](#5-user-config-file-azureopenai-clijson)
   - [Environment Variable Precedence](#6-environment-variable-precedence)
   - [.env File Support](#7-env-file-support)
2. [Integration Features](#integration-features)
   - [Espanso Text Expansion](#8-espanso-text-expansion)
   - [AutoHotKey Integration](#9-autohotkey-integration)
   - [Pipeline Integration](#10-pipeline-integration)
3. [Help & Versioning](#help--versioning)
   - [--help / -h](#11---help---h)
   - [--version / -v](#12---version---v)
4. [Security Features](#security-features)
   - [Shell Command Sandboxing](#13-shell-command-sandboxing)
   - [File Access Sandboxing](#14-file-access-sandboxing)
   - [SSRF Protection](#15-ssrf-protection)
   - [Input Validation](#16-input-validation)
   - [Docker Security](#17-docker-security)

---

## Prerequisites

See [`prerequisites.md`](prerequisites.md) for the required environment
variables (`AZUREOPENAIENDPOINT`, `AZUREOPENAIAPI`, `AZUREOPENAIMODEL`). For
multi-model setups on this page, `AZUREOPENAIMODEL` accepts a comma-separated
list (e.g. `gpt-4o,gpt-4o-mini`).

---

## Configuration Management

### 1. --config show

Display the effective configuration with source attribution for every value. This command does **not** require valid Azure credentials -- it reads local config only.

```bash
az-ai --config show
```

**Expected output:**

```
Azure OpenAI CLI Configuration
===============================
  Endpoint:      https://your-resource.openai.azure.com (env)
  Model:         gpt-4o (config/env)
  Temperature:   0.55 (default)
  Max Tokens:    10000 (default)
  Timeout:       120s (default)
  System Prompt: You are a secure, concise CLI assistant. Keep answe... (default)
  Config File:   /home/you/.azureopenai-cli.json
```

Each value shows its source in parentheses: `cli flag`, `config`, `env`, or `default`.

**Override a value and see it reflected:**

```bash
az-ai --config show --temperature 0.9 --system "You are a pirate."
```

```
  Temperature:   0.9 (cli flag)
  System Prompt: You are a pirate. (cli flag)
```

**When to use:** Debug unexpected behavior. If the model is responding too creatively, run `--config show` to check whether temperature is being overridden by a config file or environment variable you forgot about.

---

### 2. --models / --list-models

List all configured model deployments. The active model is marked with `→` and `*`.

```bash
az-ai --models
```

**Expected output (multi-model setup):**

```
Available models:
→ gpt-4o *
  gpt-4o-mini

Config file: /home/you/.azureopenai-cli.json
```

**Setting up multiple models:** Use a comma-separated list in the environment variable:

```bash
export AZUREOPENAIMODEL="gpt-4o,gpt-4o-mini,gpt-4-turbo"
```

**When no models are configured:**

```bash
# With AZUREOPENAIMODEL unset:
az-ai --models
```

```
No models configured.
Configure models in your .env file using AZUREOPENAIMODEL (comma-separated for multiple):
  AZUREOPENAIMODEL=gpt-4,gpt-35-turbo,gpt-4o
```

---

### 3. --set-model \<name\>

Switch the active model. The selection persists in `~/.azureopenai-cli.json`.

**Switch to gpt-4o-mini for fast tasks:**

```bash
az-ai --set-model gpt-4o-mini
```

```
Active model set to: gpt-4o-mini
```

**Switch back to gpt-4o for complex reasoning:**

```bash
az-ai --set-model gpt-4o
```

```
Active model set to: gpt-4o
```

**Attempt to set an unconfigured model:**

```bash
az-ai --set-model gpt-3.5-turbo
```

```
[ERROR] Model 'gpt-3.5-turbo' not found in available models.
Available models:
  - gpt-4o
  - gpt-4o-mini
```

**When to use each model:**

| Model | Best For | Latency | Cost |
|-------|----------|---------|------|
| `gpt-4o` | Complex reasoning, code review, analysis | Higher | Higher |
| `gpt-4o-mini` | Grammar fixes, translation, quick Q&A, text expansion | Lower | Lower |

**Workflow -- switch model based on task:**

```bash
# Morning: code review mode
az-ai --set-model gpt-4o
git diff HEAD~3 | az-ai "review these changes for bugs"

# Quick fix: switch to fast model for text expansion
az-ai --set-model gpt-4o-mini
echo "teh quikc brown fox" | az-ai --raw --system "Fix spelling"
```

---

### 4. --current-model

Display the currently active model without listing all models.

```bash
az-ai --current-model
```

```
Current model: gpt-4o
```

**When no model is set:**

```bash
az-ai --current-model
```

```
No active model set.
Use --set-model <model-name> to select a model, or configure AZUREOPENAIMODEL in your .env file.
```

**Scripting use case -- check before running a batch:**

```bash
#!/bin/bash
current=$(az-ai --current-model 2>/dev/null)
if [[ "$current" != *"gpt-4o-mini"* ]]; then
    echo "Switching to fast model for batch processing..."
    az-ai --set-model gpt-4o-mini
fi
```

---

### 5. User Config File (~/.azureopenai-cli.json)

The CLI persists user preferences to `~/.azureopenai-cli.json`. On Unix systems, the file is created with `600` permissions (owner read/write only).

**Full JSON structure:**

```json
{
  "ActiveModel": "gpt-4o",
  "AvailableModels": ["gpt-4o", "gpt-4o-mini"],
  "Temperature": 0.7,
  "MaxTokens": 5000,
  "TimeoutSeconds": 90,
  "SystemPrompt": "You are a senior engineer. Be concise and precise."
}
```

**What's persisted vs. environment-only:**

| Setting | Config File | Env Var | Notes |
|---------|:-----------:|:-------:|-------|
| Active model | ✅ `ActiveModel` | `AZUREOPENAIMODEL` | Config stores the selection; env provides the list |
| Available models | ✅ `AvailableModels` | `AZUREOPENAIMODEL` | Env re-initializes on each run |
| Temperature | ✅ `Temperature` | `AZURE_TEMPERATURE` | Config overrides env |
| Max tokens | ✅ `MaxTokens` | `AZURE_MAX_TOKENS` | Config overrides env |
| Timeout | ✅ `TimeoutSeconds` | `AZURE_TIMEOUT` | Config overrides env |
| System prompt | ✅ `SystemPrompt` | `SYSTEMPROMPT` | Config overrides env |
| Endpoint | ❌ No | `AZUREOPENAIENDPOINT` | Environment only -- never persisted |
| API key | ❌ No | `AZUREOPENAIAPI` | Environment only -- never persisted (security) |

**Manually editing the config file:**

```bash
# View current config
cat ~/.azureopenai-cli.json | python3 -m json.tool

# Edit with your preferred editor
nano ~/.azureopenai-cli.json
```

> **Security note:** API keys and endpoints are intentionally excluded from the config file. They should only come from environment variables or `.env` files that are excluded from version control.

**Error handling:** If the config file has invalid JSON, the CLI prints a warning to stderr and falls back to defaults:

```
[WARNING] Config file has invalid JSON, using defaults: ...
```

---

### 6. Environment Variable Precedence

The full precedence chain (highest to lowest priority):

```
CLI flags  >  Config file  >  Env vars  >  Defaults
```

**Example: temperature resolution**

```bash
# Default temperature is 0.55

# 1. Set env var
export AZURE_TEMPERATURE=0.3

# 2. Config file has: "Temperature": 0.7

# 3. CLI flag overrides everything:
az-ai --temperature 1.0 --config show
#   Temperature:   1.0 (cli flag)

# Without CLI flag, config wins over env:
az-ai --config show
#   Temperature:   0.7 (config)

# Without config, env wins:
# (after removing Temperature from ~/.azureopenai-cli.json)
az-ai --config show
#   Temperature:   0.3 (env)

# Without anything, default wins:
# (after unsetting AZURE_TEMPERATURE)
az-ai --config show
#   Temperature:   0.55 (default)
```

**System prompt precedence example:**

```bash
# Env var default
export SYSTEMPROMPT="You are helpful."

# Per-invocation override
az-ai --system "You are a pirate." "What is Docker?"
# → The pirate prompt wins for this invocation only
```

**All environment variables:**

| Variable | Purpose | Default |
|----------|---------|---------|
| `AZUREOPENAIENDPOINT` | Azure OpenAI resource URL | *(required)* |
| `AZUREOPENAIAPI` | API key | *(required)* |
| `AZUREOPENAIMODEL` | Model deployment name(s), comma-separated | *(required)* |
| `AZURE_TEMPERATURE` | Default temperature (0.0-2.0) | `0.55` |
| `AZURE_MAX_TOKENS` | Default max output tokens | `10000` |
| `AZURE_TIMEOUT` | Request timeout in seconds | `120` |
| `SYSTEMPROMPT` | Default system prompt | `"You are a secure, concise CLI assistant..."` |

---

### 7. .env File Support

The CLI auto-loads a `.env` file from the current working directory on every run. Values in the `.env` file **overwrite** existing environment variables.

**Sample `.env` file:**

```dotenv
# .env -- Azure OpenAI CLI configuration
# Place in your project root. Loaded automatically.

# Required: Azure OpenAI connection
AZUREOPENAIENDPOINT=https://your-resource.openai.azure.com
AZUREOPENAIAPI=your-api-key-here
AZUREOPENAIMODEL=gpt-4o,gpt-4o-mini

# Optional: Override defaults
AZURE_TEMPERATURE=0.5
AZURE_MAX_TOKENS=8000
AZURE_TIMEOUT=90
SYSTEMPROMPT=You are a senior engineer. Be concise and precise.
```

**How auto-loading works:**

1. The CLI looks for `.env` in the current working directory
2. If found, all variables are loaded with `overwriteExistingVars: true`
3. If missing or malformed, the CLI silently continues (`.env` is optional)

**Per-project configuration:**

```bash
# Project A -- uses gpt-4o for code review
cd ~/projects/backend
cat .env
# AZUREOPENAIMODEL=gpt-4o
# SYSTEMPROMPT=You are a backend code reviewer specializing in C# and .NET.

# Project B -- uses gpt-4o-mini for docs
cd ~/projects/docs
cat .env
# AZUREOPENAIMODEL=gpt-4o-mini
# SYSTEMPROMPT=You are a technical writer. Be clear and concise.
```

> **⚠️ Security:** Always add `.env` to your `.gitignore` to prevent committing API keys:
> ```bash
> echo ".env" >> .gitignore
> ```

---

## Integration Features

### 8. Espanso Text Expansion

[Espanso](https://espanso.org) is a cross-platform text expander. Type a trigger phrase anywhere on your OS → get AI-generated text back in-place. **Always use `--raw`** for text expansion (suppresses spinner, stats, and trailing newline).

**Config file locations:**

| OS | Path |
|----|------|
| Linux | `~/.config/espanso/match/ai.yml` |
| macOS | `~/.config/espanso/match/ai.yml` |
| Windows | `%APPDATA%\espanso\match\ai.yml` |

#### Trigger 1: Grammar Fix (`:aifix`)

Copy text to clipboard → type `:aifix` → corrected text replaces the trigger.

**Linux:**

```yaml
# ~/.config/espanso/match/ai.yml
matches:
  - trigger: ":aifix"
    replace: "{{output}}"
    vars:
      - name: output
        type: shell
        params:
          cmd: "xclip -selection clipboard -o | az-ai --raw --temperature 0.3 --system 'Fix grammar and spelling. Output ONLY the corrected text, nothing else.'"
```

**macOS:**

```yaml
matches:
  - trigger: ":aifix"
    replace: "{{output}}"
    vars:
      - name: output
        type: shell
        params:
          cmd: "pbpaste | az-ai --raw --temperature 0.3 --system 'Fix grammar and spelling. Output ONLY the corrected text, nothing else.'"
```

**Windows:**

```yaml
matches:
  - trigger: ":aifix"
    replace: "{{output}}"
    vars:
      - name: output
        type: shell
        params:
          cmd: "Get-Clipboard | az-ai --raw --temperature 0.3 --system 'Fix grammar and spelling. Output ONLY the corrected text, nothing else.'"
          shell: powershell
```

#### Trigger 2: Professional Email Rewrite (`:aiemail`)

Copy a rough draft → type `:aiemail` → polished, professional version.

```yaml
  - trigger: ":aiemail"
    replace: "{{output}}"
    vars:
      - name: output
        type: shell
        params:
          cmd: "xclip -selection clipboard -o | az-ai --raw --system 'Rewrite this as a professional email. Keep the same meaning and tone but make it polished and well-structured. Output ONLY the email text, nothing else.'"
```

#### Trigger 3: Code Explanation (`:aiexplain`)

Copy a code snippet → type `:aiexplain` → brief technical explanation.

```yaml
  - trigger: ":aiexplain"
    replace: "{{output}}"
    vars:
      - name: output
        type: shell
        params:
          cmd: "xclip -selection clipboard -o | az-ai --raw --max-tokens 200 --system 'Explain this code briefly in 2-3 sentences. Be precise and technical. Output ONLY the explanation.'"
```

#### Trigger 4: Summarization (`:aisum`)

Copy any text → type `:aisum` → 1-2 sentence summary.

```yaml
  - trigger: ":aisum"
    replace: "{{output}}"
    vars:
      - name: output
        type: shell
        params:
          cmd: "xclip -selection clipboard -o | az-ai --raw --max-tokens 150 --system 'Summarize in 1-2 sentences. Be concise. Output ONLY the summary.'"
```

#### Trigger 5: Translation to English (`:aien`)

Copy text in any language → type `:aien` → English translation.

```yaml
  - trigger: ":aien"
    replace: "{{output}}"
    vars:
      - name: output
        type: shell
        params:
          cmd: "xclip -selection clipboard -o | az-ai --raw --system 'Translate the following to English. Output ONLY the translation, nothing else.'"
```

#### Trigger 6: Commit Message from Diff (`:aicommit`)

Copy a git diff → type `:aicommit` → conventional commit message.

```yaml
  - trigger: ":aicommit"
    replace: "{{output}}"
    vars:
      - name: output
        type: shell
        params:
          cmd: "xclip -selection clipboard -o | az-ai --raw --max-tokens 100 --temperature 0.3 --system 'Write a concise conventional commit message for this diff. Format: type(scope): description. Output ONLY the commit message, no explanation.'"
```

#### Trigger 7: Free-form Prompt (`:ai `)

Type `:ai ` (with trailing space) → a form pops up → type any question → AI answer replaces the trigger.

```yaml
  - trigger: ":ai "
    replace: "{{output}}"
    vars:
      - name: "form1"
        type: form
        params:
          layout: "AI Prompt: {{prompt}}"
      - name: output
        type: shell
        params:
          cmd: "az-ai --raw '{{form1.prompt}}'"
```

#### Performance Tips for Espanso

- **Always use `--raw`** -- prevents spinner/stats artifacts in pasted text
- **Cap `--max-tokens`** -- shorter responses complete faster (150 for summaries, 200 for explanations)
- **Use `--temperature 0.3`** for deterministic tasks (grammar fixes, translations)
- **End system prompts with "Output ONLY..."** -- prevents chatty preamble from the model
- **Use `gpt-4o-mini`** -- faster for mechanical text tasks (grammar, translation, summaries)

---

### 9. AutoHotKey Integration

[AutoHotKey v2](https://www.autohotkey.com/) provides global hotkeys on Windows. Save the following as `ai-hotkeys.ahk`:

#### Hotkey 1: Ctrl+Shift+A -- Prompt Input Box

Opens an input dialog, sends your prompt to `az-ai`, pastes the response.

```ahk
#Requires AutoHotkey v2.0

; ── Helper: run a command and return stdout ──────────────────
RunWaitOne(command) {
    shell := ComObject("WScript.Shell")
    exec := shell.Exec(A_ComSpec ' /c ' command)
    return RTrim(exec.StdOut.ReadAll(), "`r`n")
}

; ── Helper: get selected text via clipboard ──────────────────
GetSelectedText() {
    saved := A_Clipboard
    A_Clipboard := ""
    Send "^c"
    if !ClipWait(2)
        return ""
    text := A_Clipboard
    A_Clipboard := saved
    return text
}

; ── Helper: run az-ai on text with a system prompt ──────────
AiTransform(text, systemPrompt, extraFlags := "") {
    escaped := StrReplace(text, '"', '\"')
    sysEscaped := StrReplace(systemPrompt, '"', '\"')
    cmd := 'echo "' escaped '" | az-ai --raw --system "' sysEscaped '"'
    if (extraFlags != "")
        cmd := 'echo "' escaped '" | az-ai --raw ' extraFlags ' --system "' sysEscaped '"'
    return RunWaitOne(cmd)
}

; ── Ctrl+Shift+A -- Free-form AI prompt ──────────────────────
^+a:: {
    ib := InputBox("Enter your prompt:", "Azure OpenAI CLI", "w400 h120")
    if ib.Result = "Cancel"
        return
    result := RunWaitOne('az-ai --raw "' StrReplace(ib.Value, '"', '\"') '"')
    if (result != "") {
        A_Clipboard := result
        Send "^v"
    }
}
```

#### Hotkey 2: Ctrl+Shift+F -- Fix Selected Text

Select text in any application → press `Ctrl+Shift+F` → grammar-corrected text replaces it.

```ahk
; ── Ctrl+Shift+F -- Fix grammar of selected text ─────────────
^+f:: {
    text := GetSelectedText()
    if (text = "")
        return
    result := AiTransform(text
        , "Fix grammar and spelling. Output ONLY the corrected text, nothing else."
        , "--temperature 0.3")
    if (result != "") {
        A_Clipboard := result
        Send "^v"
    }
}
```

#### Hotkey 3: Ctrl+Shift+S -- Summarize Clipboard

Copies selected text → summarizes it → pastes the summary.

```ahk
; ── Ctrl+Shift+S -- Summarize selected text ───────────────────
^+s:: {
    text := GetSelectedText()
    if (text = "")
        return
    result := AiTransform(text
        , "Summarize in 1-2 sentences. Be concise. Output ONLY the summary."
        , "--max-tokens 150")
    if (result != "") {
        A_Clipboard := result
        Send "^v"
    }
}
```

#### AHK Hotkey Reference

| Hotkey | Action | Input Source |
|--------|--------|-------------|
| `Ctrl+Shift+A` | Free-form AI prompt | Typed in dialog box |
| `Ctrl+Shift+F` | Fix grammar & spelling | Selected text |
| `Ctrl+Shift+S` | Summarize text | Selected text |

**Setup:**
1. Install [AutoHotKey v2](https://www.autohotkey.com/)
2. Save the full script as `ai-hotkeys.ahk`
3. Double-click to run
4. Optional: place a shortcut in `shell:startup` to auto-launch on login

---

### 10. Pipeline Integration

`az-ai` reads stdin and accepts pipe input natively. Combine it with any CLI tool.

#### Summarize Git Changes

```bash
git diff HEAD~3 | az-ai --raw --system "Summarize these code changes in 3 bullet points"
```

#### Generate a Commit Message from Staged Changes

```bash
git diff --cached | az-ai --raw --temperature 0.3 --max-tokens 100 \
  --system "Write a conventional commit message for this diff. Format: type(scope): description"
```

#### Parse an API Response

```bash
curl -s https://api.github.com/repos/dotnet/runtime/releases/latest \
  | az-ai --raw --system "Parse this JSON response. What version was released and when?"
```

#### Diagnose a Log File

```bash
cat /var/log/app/error.log | az-ai --raw --system "What's wrong? Identify the root cause and suggest a fix."
```

#### Structured JSON Output for Scripting

```bash
az-ai --json "List 3 benefits of Docker" | jq '.response'
```

#### Chain Multiple Tools

```bash
# Find TODO comments and get a prioritized summary
grep -rn "TODO" src/ | az-ai --raw --system "Prioritize these TODOs by severity. List the top 3."
```

```bash
# Review a specific file
cat src/auth/login.cs | az-ai --raw --system "Review this code for security issues"
```

#### Pipeline Scripting Pattern

```bash
#!/bin/bash
# review-pr.sh -- AI-assisted PR review
branch="${1:-HEAD}"

echo "## Code Changes"
git diff main..."$branch" | az-ai --raw --system "Review this diff for bugs and security issues. Be concise."

echo ""
echo "## Commit Summary"
git log main..."$branch" --oneline | az-ai --raw --system "Summarize these commits in one paragraph."
```

---

## Help & Versioning

### 11. --help / -h

Display full usage information organized by section.

```bash
az-ai --help
# or
az-ai -h
```

**Expected output:**

```
Azure OpenAI CLI

Usage:
  <prompt>              Send a prompt to the AI
  --models              List available models (* marks active)
  --current-model       Show the currently active model
  --set-model <name>    Set the active model
  --version, -v         Show version information
  --help, -h            Show this help message

Options:
  --json                Output response as JSON (for scripting)
  -t, --temperature <v> Override temperature (0.0-2.0)
  --max-tokens <n>      Override max output tokens
  --system <prompt>     Override system prompt for this invocation
  --schema <json>      Enforce structured output with JSON schema (strict mode)
  --raw               Output raw text only (no spinner, no formatting). For Espanso/AHK.
  --config show         Display effective configuration and exit

Agent Mode:
  --agent               Enable agentic mode (model can call tools)
  --tools <list>        Comma-separated tool names to enable (default: all)
                        Available: shell,file,web,clipboard,datetime
  --max-rounds <n>      Max tool-calling rounds (default: 5, max: 20)

Piping:
  echo "question" | azureopenai-cli
  git diff | azureopenai-cli "review this code"
  cat file.md | azureopenai-cli "summarize this"

Examples:
  azureopenai-cli "Explain quantum computing"
  azureopenai-cli --models
  azureopenai-cli --set-model gpt-4o
  azureopenai-cli --json "What is Docker?"
  echo "code" | azureopenai-cli --json "review this"
  azureopenai-cli --agent "what time is it in Tokyo?"
  azureopenai-cli --agent "summarize ~/notes.md"
  azureopenai-cli --agent --tools shell "run git log -5 and summarize"

Ralph Mode:
  --ralph              Enable Ralph mode (autonomous Wiggum loop)
  --validate <cmd>     Validation command to run after each iteration
  --task-file <path>   Read task prompt from file instead of args
  --max-iterations <n> Maximum Ralph loop iterations (default: 10, max: 50)

Persona Mode:
  --persona <name>     Use a named persona from .squad.json (or 'auto' for routing)
  --personas           List available personas
  --squad-init         Initialize Squad in current directory
```

**Sections explained:**

| Section | Purpose |
|---------|---------|
| **Usage** | Core commands -- prompt, model management, meta commands |
| **Options** | Per-invocation flags that modify behavior |
| **Agent Mode** | Autonomous tool-calling mode (shell, file, web, clipboard, datetime) |
| **Piping** | Stdin examples for pipeline workflows |
| **Ralph Mode** | Autonomous coding loop with validation |
| **Persona Mode** | AI team member system with specialized roles |

---

### 12. --version / -v

Display the CLI version.

```bash
az-ai --version
# or
az-ai -v
```

**Expected output:**

```
Azure OpenAI CLI v1.0.0
```

**Scripting -- check minimum version:**

```bash
version=$(az-ai --version 2>&1)
echo "$version"
# Azure OpenAI CLI v1.0.0
```

---

## Security Features

The CLI implements defense-in-depth security across all tool-use surfaces. These protections activate in **agent mode** (`--agent`) when the model can call tools autonomously.

### 13. Shell Command Sandboxing

When the model uses the `shell_exec` tool, commands are filtered through multiple security layers.

#### Blocked Commands

The following commands are **always blocked** -- they can destroy data or escalate privileges:

| Command | Category | Why Blocked |
|---------|----------|-------------|
| `rm`, `rmdir`, `del` | Destructive | Delete files/directories |
| `dd` | Destructive | Raw disk write -- can overwrite entire drives |
| `mkfs`, `fdisk`, `format` | Destructive | Filesystem creation/partitioning -- wipes disks |
| `shutdown`, `reboot`, `halt`, `poweroff` | System | Shuts down the host machine |
| `kill`, `killall`, `pkill` | Process | Terminate arbitrary processes |
| `sudo`, `su` | Privilege escalation | Gain root access |
| `passwd` | Privilege escalation | Change user passwords |
| `crontab` | Persistence | Schedule recurring commands (could create backdoors) |
| `vi`, `vim`, `nano` | Interactive | Block interactive editors that hang the process |
| `nc`, `ncat`, `netcat` | Network | Open raw network connections (reverse shells) |
| `wget` | Network | Download arbitrary files (use `curl` via the web_fetch tool instead) |

**Example -- blocked direct command:**

```bash
# In agent mode, if the model tries:
#   shell_exec: { "command": "rm -rf /tmp/data" }
# Response: "Error: command 'rm' is blocked for safety."
```

#### Pipe Chain Blocking

Every segment of a pipe chain is checked. A blocked command anywhere in the chain is denied:

```bash
# Model tries to hide rm behind a pipe:
#   shell_exec: { "command": "find . -name '*.log' | xargs rm" }
# Response: "Error: command 'rm' is blocked for safety."

# Also blocked across semicolons and &&:
#   shell_exec: { "command": "echo hello; rm -rf /" }
# Response: "Error: command 'rm' is blocked for safety."
```

#### Shell Substitution Blocking

`$()` and backtick substitution are blocked to prevent filter bypass:

```bash
# Model tries to hide a command inside substitution:
#   shell_exec: { "command": "$(echo rm) -rf /" }
# Response: "Error: shell substitution ($() and backticks) is blocked for safety."

#   shell_exec: { "command": "`echo rm` -rf /" }
# Response: "Error: shell substitution ($() and backticks) is blocked for safety."
```

#### eval/exec and Process Substitution Blocking

`eval`, `exec`, and process substitution (`<(…)`, `>(…)`) are all rejected with the same error message: **"Error: process substitution and eval/exec are blocked for safety."**

| Input | Behavior | Error |
|-------|----------|-------|
| `eval 'rm -rf /'` | Rejected | `Error: process substitution and eval/exec are blocked for safety.` |
| `exec rm -rf /` | Rejected | `Error: process substitution and eval/exec are blocked for safety.` |
| `cat <(rm -rf /)` | Rejected | `Error: process substitution and eval/exec are blocked for safety.` |

#### Additional Shell Protections

- **10-second timeout**: Long-running commands are killed after 10 seconds
- **64 KB output cap**: Prevents memory exhaustion from infinite output (`yes`, `/dev/urandom`)
- **stdin closed immediately**: Prevents interactive commands from hanging

---

### 14. File Access Sandboxing

The `read_file` tool blocks access to sensitive system files.

#### Blocked Paths

| Path | Why Blocked |
|------|-------------|
| `/etc/shadow` | Password hashes -- could enable offline cracking |
| `/etc/passwd` | User account information |
| `/etc/sudoers` | Privilege escalation configuration |
| `/etc/hosts` | Network configuration -- could reveal internal hostnames |
| `/root/.ssh` | SSH private keys -- could enable remote access |
| `/proc/self/environ` | Process environment variables -- contains API keys |
| `/proc/self/cmdline` | Command line arguments -- may contain secrets |

**Example -- blocked file access:**

```bash
# In agent mode, if the model tries:
#   read_file: { "path": "/etc/shadow" }
# Response: "Error: access to '/etc/shadow' is blocked for security."

#   read_file: { "path": "/root/.ssh/id_rsa" }
# Response: "Error: access to '/root/.ssh/id_rsa' is blocked for security."
```

#### Symlink Resolution

The tool resolves symlinks before checking the blocked list, preventing bypass via symbolic links:

```bash
# Even if a symlink points to a blocked path:
#   ln -s /etc/shadow /tmp/totally-safe.txt
#   read_file: { "path": "/tmp/totally-safe.txt" }
# Response: "Error: access to '/tmp/totally-safe.txt' is blocked for security (symlink target is restricted)."
```

#### File Size Limit

Files larger than **256 KB** are rejected to prevent memory exhaustion:

```bash
# read_file: { "path": "/var/log/huge-app.log" }
# Response: "Error: file too large (5,242,880 bytes, max 262,144)."
```

---

### 15. SSRF Protection

**The attack:** In agent mode, a malicious prompt could instruct the model to fetch `http://169.254.169.254/latest/meta-data/` -- the cloud metadata endpoint that returns IAM credentials, instance identity, and other secrets. This is a Server-Side Request Forgery (SSRF) attack.

The `web_fetch` tool has three layers of SSRF protection:

#### Layer 1: HTTPS Only

```bash
# HTTP is rejected:
#   web_fetch: { "url": "http://169.254.169.254/metadata" }
# Response: "Error: only HTTPS URLs are allowed."

# Non-HTTPS schemes also blocked:
#   web_fetch: { "url": "ftp://internal.corp/data" }
# Response: "Error: only HTTPS URLs are allowed."
```

#### Layer 2: Private IP Blocking (Pre-Request DNS Resolution)

Before making any request, the tool resolves the hostname and blocks private/internal IPs:

```bash
# Cloud metadata endpoints:
#   web_fetch: { "url": "https://169.254.169.254/..." }
# Response: "Error: access to private/loopback addresses is blocked for security."

# Internal network:
#   web_fetch: { "url": "https://192.168.1.100/admin" }
# Response: "Error: access to private/loopback addresses is blocked for security."

# Localhost:
#   web_fetch: { "url": "https://127.0.0.1:8080/secret" }
# Response: "Error: access to private/loopback addresses is blocked for security."
```

**Blocked IP ranges:**

| Range | Type |
|-------|------|
| `127.0.0.0/8` | Loopback |
| `10.0.0.0/8` | RFC 1918 private |
| `172.16.0.0/12` | RFC 1918 private |
| `192.168.0.0/16` | RFC 1918 private |
| `169.254.0.0/16` | Link-local (cloud metadata) |
| `fd00::/8` | IPv6 unique local |
| `fe80::/10` | IPv6 link-local |
| `::1` | IPv6 loopback |

#### Layer 3: Post-Redirect Validation

After following redirects (max 3), the final URL is re-validated:

```bash
# A public URL that redirects to an internal IP:
#   web_fetch: { "url": "https://evil.com/redirect-to-metadata" }
#   (evil.com redirects to http://169.254.169.254/...)
# Response: "Error: redirect to non-HTTPS URL is blocked for security."

# Or redirects to a private IP over HTTPS:
# Response: "Error: redirect to private/loopback address is blocked for security."
```

#### Additional Web Protections

- **10-second timeout**: Prevents slow-loris attacks
- **128 KB response cap**: Prevents memory exhaustion
- **Max 3 redirects**: Limits redirect chains

---

### 16. Input Validation

The CLI validates all inputs to prevent abuse and excessive API costs.

#### Prompt Size Limit (32,000 characters)

```bash
# A prompt exceeding 32K characters:
python3 -c "print('x' * 33000)" | az-ai
# Error: Prompt too long (33000 chars). Maximum allowed is 32000 chars.
```

#### Stdin Size Limit (1 MB)

```bash
# Piping a file larger than 1 MB:
dd if=/dev/zero bs=1048577 count=1 2>/dev/null | az-ai "analyze this"
# Error: stdin input exceeds 1 MB limit.
```

#### Temperature Range (0.0 - 2.0)

```bash
# Temperature out of range:
az-ai --temperature 3.0 "hello"
# [ERROR] Temperature must be between 0.0 and 2.0

az-ai --temperature -0.5 "hello"
# [ERROR] Temperature must be between 0.0 and 2.0
```

#### Max Tokens Range (1 - 128,000)

```bash
# Max tokens out of range:
az-ai --max-tokens 0 "hello"
# [ERROR] Max tokens must be between 1 and 128000

az-ai --max-tokens 200000 "hello"
# [ERROR] Max tokens must be between 1 and 128000
```

#### Max Rounds Range (1 - 20)

```bash
# Agent mode round limit:
az-ai --agent --max-rounds 25 "do something"
# [ERROR] --max-rounds requires an integer 1-20
```

---

### 17. Docker Security

The Dockerfile implements container security best practices.

#### Non-Root Execution

The container runs as `appuser`, not root:

```dockerfile
# From the Dockerfile:
RUN addgroup --system appgroup \
 && adduser --system --ingroup appgroup appuser
# ...
USER appuser
```

Even if the model's shell_exec tool were somehow exploited, the damage is limited to the unprivileged user's scope.

#### Credentials via Environment Only

API keys are **never** baked into the Docker image. They must be injected at runtime:

```bash
# Recommended: use an env file
docker run --rm --env-file .env azure-openai-cli "What is Docker?"

# Alternative: pass individual variables
docker run --rm \
  -e AZUREOPENAIENDPOINT="https://your-resource.openai.azure.com" \
  -e AZUREOPENAIAPI="your-api-key" \
  -e AZUREOPENAIMODEL="gpt-4o" \
  azure-openai-cli "What is Docker?"

# Mount .env as read-only volume
docker run --rm \
  -v /path/to/.env:/app/.env:ro \
  azure-openai-cli "What is Docker?"
```

#### Minimal Attack Surface

The runtime image is Alpine-based (`runtime-deps:10.0-alpine`), which has:
- No shell beyond what's needed
- No package managers in the final image
- Minimal OS libraries

#### Full Recommended Docker Run Command

```bash
docker run --rm \
  --read-only \
  --tmpfs /tmp:noexec,nosuid,size=64m \
  --env-file .env \
  --memory 256m \
  --cpus 0.5 \
  --security-opt no-new-privileges:true \
  azure-openai-cli "Your prompt here"
```

| Flag | Purpose |
|------|---------|
| `--rm` | Remove container after exit |
| `--read-only` | Filesystem is read-only |
| `--tmpfs /tmp:noexec,nosuid` | Writable tmp with no-exec |
| `--env-file .env` | Inject credentials at runtime |
| `--memory 256m` | Cap memory usage |
| `--cpus 0.5` | Cap CPU usage |
| `--security-opt no-new-privileges:true` | Prevent privilege escalation inside container |
