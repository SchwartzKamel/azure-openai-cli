# Text Expansion Integration — Espanso & AutoHotKey

> **TL;DR:** Type a trigger phrase anywhere → get AI-generated text back in-place.  
> This is the PRIMARY use case of `az-ai`: a headless text generation backend for hotkey-driven workflows.

---

## Table of Contents

1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Espanso Configuration](#espanso-configuration)
4. [AutoHotKey Configuration](#autohotkey-configuration)
5. [Performance Tips](#performance-tips)
6. [Troubleshooting](#troubleshooting)
7. [Advanced: Persona Integration](#advanced-persona-integration)

---

## Overview

### What This Does

`az-ai` is a CLI that calls Azure OpenAI and prints the response to stdout. When combined with a text expansion tool like [Espanso](https://espanso.org) or [AutoHotKey](https://www.autohotkey.com), it creates an invisible AI layer across your entire OS:

- You're writing an email → type `:aifix` → your clipboard text gets grammar-corrected and pasted back
- You're in Slack → type `:ai ` → a form pops up → you type a question → the answer replaces your trigger
- You're coding → select a function, hit `Ctrl+Shift+E` → a 2-sentence explanation appears

No browser tabs. No context switches. The AI meets you where you already are.

### Why `--raw` Matters

By default, `az-ai` shows a braille spinner on stderr while waiting for the first token, and appends token usage stats after the response. That's great for interactive terminal use — terrible for text injection.

The `--raw` flag strips all of it:

| Behavior | Default | `--raw` |
|----------|---------|---------|
| Spinner on stderr | ✅ (if TTY) | Suppressed |
| Token usage stats | ✅ (on stderr) | Suppressed |
| Trailing newline | ✅ | Suppressed |
| Response text on stdout | ✅ | ✅ |

**Always use `--raw` for text expansion.** Without it, Espanso/AHK may capture spinner artifacts or extra newlines.

> **Note:** When Espanso or AHK invokes `az-ai` via a shell command, stderr is typically redirected, which already suppresses the spinner. The `--raw` flag provides a defense-in-depth guarantee — use it regardless.

### Performance Expectations

| Scenario | Latency | Notes |
|----------|---------|-------|
| Short prompt, short response (1-2 sentences) | ~1-2s | gpt-4o-mini recommended |
| Grammar fix on a paragraph | ~2-3s | Model reads + rewrites |
| Code explanation (2-3 sentences) | ~2-3s | Depends on code length |
| Long creative writing | 5-10s+ | Use `--max-tokens` to cap |

Latency is dominated by Azure API round-trip and token generation. The CLI itself adds <100ms overhead. For faster responses, see [Performance Tips](#performance-tips).

---

## Prerequisites

### 1. Install `az-ai`

You need the `az-ai` binary accessible from your shell. Two options:

**Option A: Native binary (recommended for text expansion — fastest startup)**

Download from [GitHub Releases](https://github.com/SchwartzKamel/azure-openai-cli/releases), extract, and place on your `PATH`:

```bash
# Linux
tar xzf azure-openai-cli-linux-x64.tar.gz
sudo mv AzureOpenAI_CLI /usr/local/bin/az-ai
chmod +x /usr/local/bin/az-ai

# macOS (Apple Silicon)
tar xzf azure-openai-cli-osx-arm64.tar.gz
sudo mv AzureOpenAI_CLI /usr/local/bin/az-ai
chmod +x /usr/local/bin/az-ai
```

**Option B: Docker alias (works but adds ~200ms container startup)**

```bash
make alias
# Adds: alias az-ai='docker run --rm --env-file .env azure-openai-cli'
```

### 2. Set Environment Variables

`az-ai` needs these environment variables to connect to Azure OpenAI:

```bash
export AZUREOPENAIENDPOINT="https://your-resource.openai.azure.com"
export AZUREOPENAIAPI="your-api-key-here"
export AZUREOPENAIMODEL="gpt-4o-mini"
```

Add them to your shell profile (`~/.bashrc`, `~/.zshrc`) so they persist across sessions and are available to Espanso/AHK when it spawns a shell.

> **macOS/Espanso note:** Espanso may not inherit your shell's environment. If commands fail with credential errors, use the `shell: bash` param and source your profile explicitly — see the [Troubleshooting](#troubleshooting) section.

### 3. Verify It Works

```bash
echo "test" | az-ai --raw
```

You should see a short AI response with no spinner, no decorations, no trailing newline. If this works, you're ready for integration.

---

## Espanso Configuration

[Espanso](https://espanso.org) is a cross-platform text expander. Create or edit the match file at:

- **Linux/macOS:** `~/.config/espanso/match/ai.yml`
- **Windows:** `%APPDATA%\espanso\match\ai.yml`

### Basic Configuration

```yaml
# ~/.config/espanso/match/ai.yml
# Azure OpenAI CLI text expansion triggers
# Docs: https://github.com/SchwartzKamel/azure-openai-cli

matches:

  # ── Free-form AI prompt (with input form) ─────────────────────
  # Type ":ai " (with trailing space) anywhere → form pops up → AI responds
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

  # ── Fix grammar & spelling (from clipboard) ───────────────────
  # Copy text → type ":aifix" → corrected text is pasted
  - trigger: ":aifix"
    replace: "{{output}}"
    vars:
      - name: output
        type: shell
        params:
          cmd: "xclip -selection clipboard -o | az-ai --raw --system 'Fix grammar and spelling. Output ONLY the corrected text, nothing else.'"

  # ── Professional email rewrite ─────────────────────────────────
  # Copy rough draft → type ":aiemail" → polished email appears
  - trigger: ":aiemail"
    replace: "{{output}}"
    vars:
      - name: output
        type: shell
        params:
          cmd: "xclip -selection clipboard -o | az-ai --raw --system 'Rewrite this as a professional email. Keep the same meaning and tone but make it polished and well-structured. Output ONLY the email text, nothing else.'"

  # ── Explain code (from clipboard) ──────────────────────────────
  # Copy a code snippet → type ":aiexplain" → brief explanation appears
  - trigger: ":aiexplain"
    replace: "{{output}}"
    vars:
      - name: output
        type: shell
        params:
          cmd: "xclip -selection clipboard -o | az-ai --raw --max-tokens 200 --system 'Explain this code briefly in 2-3 sentences. Be precise and technical. Output ONLY the explanation.'"

  # ── Summarize text (from clipboard) ────────────────────────────
  # Copy any text → type ":aisum" → 1-2 sentence summary
  - trigger: ":aisum"
    replace: "{{output}}"
    vars:
      - name: output
        type: shell
        params:
          cmd: "xclip -selection clipboard -o | az-ai --raw --max-tokens 150 --system 'Summarize in 1-2 sentences. Be concise. Output ONLY the summary.'"

  # ── Translate to English (from clipboard) ──────────────────────
  - trigger: ":aien"
    replace: "{{output}}"
    vars:
      - name: output
        type: shell
        params:
          cmd: "xclip -selection clipboard -o | az-ai --raw --system 'Translate the following to English. Output ONLY the translation, nothing else.'"

  # ── Make text more concise (from clipboard) ────────────────────
  - trigger: ":aishort"
    replace: "{{output}}"
    vars:
      - name: output
        type: shell
        params:
          cmd: "xclip -selection clipboard -o | az-ai --raw --system 'Make this text more concise. Cut unnecessary words. Preserve meaning. Output ONLY the shortened text.'"

  # ── Generate commit message (from clipboard) ───────────────────
  # Copy a git diff → type ":aicommit" → conventional commit message
  - trigger: ":aicommit"
    replace: "{{output}}"
    vars:
      - name: output
        type: shell
        params:
          cmd: "xclip -selection clipboard -o | az-ai --raw --max-tokens 100 --temperature 0.3 --system 'Write a concise conventional commit message for this diff. Format: type(scope): description. Output ONLY the commit message, no explanation.'"
```

### macOS Variants

macOS uses `pbpaste` instead of `xclip`. Replace the clipboard command in each trigger:

```yaml
# macOS — replace all 'xclip -selection clipboard -o' with 'pbpaste'
matches:

  - trigger: ":aifix"
    replace: "{{output}}"
    vars:
      - name: output
        type: shell
        params:
          cmd: "pbpaste | az-ai --raw --system 'Fix grammar and spelling. Output ONLY the corrected text, nothing else.'"

  - trigger: ":aiemail"
    replace: "{{output}}"
    vars:
      - name: output
        type: shell
        params:
          cmd: "pbpaste | az-ai --raw --system 'Rewrite this as a professional email. Keep the same meaning and tone but make it polished and well-structured. Output ONLY the email text, nothing else.'"

  - trigger: ":aiexplain"
    replace: "{{output}}"
    vars:
      - name: output
        type: shell
        params:
          cmd: "pbpaste | az-ai --raw --max-tokens 200 --system 'Explain this code briefly in 2-3 sentences. Be precise and technical. Output ONLY the explanation.'"

  - trigger: ":aisum"
    replace: "{{output}}"
    vars:
      - name: output
        type: shell
        params:
          cmd: "pbpaste | az-ai --raw --max-tokens 150 --system 'Summarize in 1-2 sentences. Be concise. Output ONLY the summary.'"

  - trigger: ":aicommit"
    replace: "{{output}}"
    vars:
      - name: output
        type: shell
        params:
          cmd: "pbpaste | az-ai --raw --max-tokens 100 --temperature 0.3 --system 'Write a concise conventional commit message for this diff. Format: type(scope): description. Output ONLY the commit message, no explanation.'"
```

### Windows Variants

On Windows, use `powershell -c Get-Clipboard` and set the `shell` param explicitly:

```yaml
# Windows — use PowerShell for clipboard access
matches:

  - trigger: ":aifix"
    replace: "{{output}}"
    vars:
      - name: output
        type: shell
        params:
          cmd: "powershell -c \"Get-Clipboard | az-ai --raw --system 'Fix grammar and spelling. Output ONLY the corrected text, nothing else.'\""
          shell: cmd

  - trigger: ":aiemail"
    replace: "{{output}}"
    vars:
      - name: output
        type: shell
        params:
          cmd: "powershell -c \"Get-Clipboard | az-ai --raw --system 'Rewrite this as a professional email. Keep the same meaning and tone but make it polished and well-structured. Output ONLY the email text, nothing else.'\""
          shell: cmd

  - trigger: ":aiexplain"
    replace: "{{output}}"
    vars:
      - name: output
        type: shell
        params:
          cmd: "powershell -c \"Get-Clipboard | az-ai --raw --max-tokens 200 --system 'Explain this code briefly in 2-3 sentences. Be precise and technical. Output ONLY the explanation.'\""
          shell: cmd
```

> **Tip:** On Windows, if `az-ai` isn't on your `PATH`, use the full path to the binary:  
> `C:\\Users\\you\\tools\\AzureOpenAI_CLI.exe`

---

## AutoHotKey Configuration

[AutoHotKey v2](https://www.autohotkey.com/) provides global hotkeys on Windows. Save the following as `ai-text-expansion.ahk`:

```ahk
; ============================================================
; Azure OpenAI CLI — AutoHotKey v2 Text Expansion
; ============================================================
; Hotkeys:
;   Ctrl+Shift+A   — Free-form AI prompt (input box)
;   Ctrl+Shift+F   — Fix grammar of selected text
;   Ctrl+Shift+E   — Explain selected code
;   Ctrl+Shift+R   — Rewrite selected text as professional email
;   Ctrl+Shift+S   — Summarize selected text
; ============================================================
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
    ; Escape double quotes in both inputs
    escaped := StrReplace(text, '"', '\"')
    sysEscaped := StrReplace(systemPrompt, '"', '\"')
    cmd := 'echo "' escaped '" | az-ai --raw --system "' sysEscaped '"'
    if (extraFlags != "")
        cmd := 'echo "' escaped '" | az-ai --raw ' extraFlags ' --system "' sysEscaped '"'
    return RunWaitOne(cmd)
}

; ── Ctrl+Shift+A — Free-form AI prompt ──────────────────────
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

; ── Ctrl+Shift+F — Fix grammar of selected text ─────────────
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

; ── Ctrl+Shift+E — Explain selected code ────────────────────
^+e:: {
    text := GetSelectedText()
    if (text = "")
        return
    result := AiTransform(text
        , "Explain this code briefly in 2-3 sentences. Be precise and technical."
        , "--max-tokens 200")
    if (result != "") {
        A_Clipboard := result
        Send "^v"
    }
}

; ── Ctrl+Shift+R — Rewrite as professional email ────────────
^+r:: {
    text := GetSelectedText()
    if (text = "")
        return
    result := AiTransform(text
        , "Rewrite this as a professional email. Keep the same meaning. Output ONLY the email text.")
    if (result != "") {
        A_Clipboard := result
        Send "^v"
    }
}

; ── Ctrl+Shift+S — Summarize selected text ───────────────────
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

### AHK Usage

1. Save the script as `ai-text-expansion.ahk`
2. Double-click to run (requires AHK v2 installed)
3. **Optional:** Place a shortcut in your Startup folder (`shell:startup`) to auto-launch on login

### AHK Hotkey Reference

| Hotkey | Action | Input |
|--------|--------|-------|
| `Ctrl+Shift+A` | Free-form AI prompt | Typed in dialog box |
| `Ctrl+Shift+F` | Fix grammar & spelling | Selected text |
| `Ctrl+Shift+E` | Explain code | Selected text |
| `Ctrl+Shift+R` | Rewrite as professional email | Selected text |
| `Ctrl+Shift+S` | Summarize text | Selected text |

---

## Performance Tips

### 1. Always Use `--raw`

The `--raw` flag skips spinner rendering and token stats. When stderr is already redirected (common in Espanso/AHK), the savings are minimal (~5ms). But `--raw` also skips the trailing newline, which prevents blank-line artifacts in pasted text. **Always use it for text expansion.**

### 2. Cap Output Length with `--max-tokens`

Shorter responses complete faster. For text expansion, you rarely need the default 10,000 token limit:

```yaml
# Grammar fix: response ≈ input length. 500 tokens covers most paragraphs.
cmd: "... | az-ai --raw --max-tokens 500 --system '...'"

# Summaries: 1-2 sentences. 150 tokens is plenty.
cmd: "... | az-ai --raw --max-tokens 150 --system '...'"

# Code explanations: 2-3 sentences. 200 tokens.
cmd: "... | az-ai --raw --max-tokens 200 --system '...'"

# Commit messages: one line. 100 tokens.
cmd: "... | az-ai --raw --max-tokens 100 --system '...'"
```

### 3. Use a Fast Model

For text expansion, latency matters more than raw intelligence. Use your fastest deployment:

```bash
# In your .env or shell profile — use gpt-4o-mini for expansion
export AZUREOPENAIMODEL="gpt-4o-mini"
```

Or if you have multiple models configured, override per-trigger with `--set-model` (or use different env vars in separate scripts):

| Task | Recommended Model | Why |
|------|-------------------|-----|
| Grammar fixes, translation | gpt-4o-mini | Fast, accurate for mechanical tasks |
| Email rewriting | gpt-4o-mini | Good enough for style adjustments |
| Code explanation | gpt-4o | Better reasoning for complex code |
| Creative writing | gpt-4o | More nuanced output |

### 4. Tune Temperature by Task

| Temperature | Best For | Example |
|-------------|----------|---------|
| `0.2 – 0.3` | Factual corrections, translations, commit messages | `:aifix`, `:aicommit` |
| `0.5 – 0.6` | Email rewrites, summaries (default: 0.55) | `:aiemail`, `:aisum` |
| `0.7 – 0.9` | Creative writing, brainstorming | Free-form prompts |

```yaml
# Low temp for grammar — deterministic, consistent
cmd: "... | az-ai --raw --temperature 0.3 --system '...'"

# Higher temp for creative tasks
cmd: "... | az-ai --raw --temperature 0.8 --system '...'"
```

### 5. Write System Prompts That End with "Output ONLY..."

The biggest latency and quality killer for text expansion is a chatty model that adds preamble like "Sure, here's the corrected text:" or "Here you go!". End every system prompt with a strict output directive:

```
Fix grammar and spelling. Output ONLY the corrected text, nothing else.
```

This saves tokens (faster), prevents pasted junk (cleaner), and improves reliability.

### 6. Native Binary > Docker for Expansion

Docker adds ~200-300ms of container startup overhead per invocation. For text expansion where every millisecond matters, use the native binary:

```bash
# Native binary: ~100ms startup
az-ai --raw "Hello"

# Docker: ~300-400ms startup
docker run --rm --env-file .env azure-openai-cli --raw "Hello"
```

If you installed via `make alias`, consider switching to a native binary for expansion triggers while keeping Docker for agentic/Ralph workflows.

---

## Troubleshooting

### Environment Variables Not Found

**Symptom:** `az-ai` returns an error about missing endpoint or API key when triggered from Espanso, but works fine in your terminal.

**Cause:** Espanso spawns a new shell that doesn't inherit your `.bashrc`/`.zshrc` environment.

**Fix (Linux):** Set env vars in `~/.profile` or `/etc/environment` (these are read by login shells and most desktop environments):

```bash
# ~/.profile
export AZUREOPENAIENDPOINT="https://your-resource.openai.azure.com"
export AZUREOPENAIAPI="your-api-key"
export AZUREOPENAIMODEL="gpt-4o-mini"
```

Then log out and back in.

**Fix (macOS):** Use `launchctl setenv` or add to `~/.zprofile`:

```bash
launchctl setenv AZUREOPENAIENDPOINT "https://your-resource.openai.azure.com"
launchctl setenv AZUREOPENAIAPI "your-api-key"
launchctl setenv AZUREOPENAIMODEL "gpt-4o-mini"
```

**Fix (Espanso — explicit shell sourcing):** Add the `shell` parameter to force Espanso to use a login shell:

```yaml
- trigger: ":aifix"
  replace: "{{output}}"
  vars:
    - name: output
      type: shell
      params:
        cmd: "source ~/.bashrc && xclip -selection clipboard -o | az-ai --raw --system 'Fix grammar and spelling. Output ONLY the corrected text, nothing else.'"
        shell: bash
```

### Binary Not on PATH

**Symptom:** `command not found: az-ai`

**Fix:** Use the full path to the binary in your Espanso/AHK config:

```yaml
# Espanso — full path
cmd: "xclip -selection clipboard -o | /usr/local/bin/az-ai --raw --system '...'"
```

```ahk
; AHK — full path
RunWaitOne('C:\Users\you\tools\AzureOpenAI_CLI.exe --raw "' prompt '"')
```

### Timeout on Long Prompts

**Symptom:** Espanso shows no output or inserts blank text after a long wait.

**Cause:** Espanso's default shell command timeout may be shorter than the Azure API response time.

**Fix:** Set a longer timeout in the Espanso trigger and cap `--max-tokens`:

```yaml
- trigger: ":aifix"
  replace: "{{output}}"
  vars:
    - name: output
      type: shell
      params:
        cmd: "xclip -selection clipboard -o | az-ai --raw --max-tokens 500 --system '...'"
        timeout: 15000  # 15 seconds (Espanso timeout in ms)
```

### Output Contains Extra Whitespace or Newlines

**Symptom:** Pasted text has leading/trailing blank lines.

**Fix:** Verify you're using `--raw` (suppresses the trailing newline). If the model itself adds whitespace, pipe through `sed`:

```yaml
cmd: "xclip -selection clipboard -o | az-ai --raw --system '...' | sed 's/^[[:space:]]*//;s/[[:space:]]*$//'"
```

### Quick Diagnostic Checklist

```bash
# 1. Is the binary on PATH?
which az-ai

# 2. Are env vars set?
echo $AZUREOPENAIENDPOINT
echo $AZUREOPENAIMODEL

# 3. Does raw mode work?
echo "say hello" | az-ai --raw

# 4. Does clipboard piping work? (Linux)
echo "test text" | xclip -selection clipboard
xclip -selection clipboard -o | az-ai --raw --system "Repeat this text exactly."

# 5. Does clipboard piping work? (macOS)
echo "test text" | pbcopy
pbpaste | az-ai --raw --system "Repeat this text exactly."
```

---

## Advanced: Persona Integration

The [persona system](../README.md#performing_arts-persona-system--ai-team-members) lets you invoke specialized AI team members, each with their own system prompt, tool access, and persistent memory. You can combine personas with Espanso triggers for role-specific text expansion.

### Persona Triggers

```yaml
# ~/.config/espanso/match/ai-personas.yml
# Persona-powered text expansion triggers

matches:

  # ── Code review of clipboard content ───────────────────────
  - trigger: ":aireview"
    replace: "{{output}}"
    vars:
      - name: output
        type: shell
        params:
          cmd: "xclip -selection clipboard -o | az-ai --raw --persona reviewer"

  # ── Architecture advice ────────────────────────────────────
  - trigger: ":aiarch"
    replace: "{{output}}"
    vars:
      - name: "form1"
        type: form
        params:
          layout: "Architecture question: {{question}}"
      - name: output
        type: shell
        params:
          cmd: "az-ai --raw --persona architect '{{form1.question}}'"

  # ── Security audit of clipboard code ───────────────────────
  - trigger: ":aisec"
    replace: "{{output}}"
    vars:
      - name: output
        type: shell
        params:
          cmd: "xclip -selection clipboard -o | az-ai --raw --max-tokens 300 --persona security"

  # ── Technical writing assist ───────────────────────────────
  - trigger: ":aidocs"
    replace: "{{output}}"
    vars:
      - name: output
        type: shell
        params:
          cmd: "xclip -selection clipboard -o | az-ai --raw --persona writer"

  # ── Auto-route: let the CLI pick the right persona ─────────
  - trigger: ":aiauto"
    replace: "{{output}}"
    vars:
      - name: "form1"
        type: form
        params:
          layout: "Task: {{task}}"
      - name: output
        type: shell
        params:
          cmd: "az-ai --raw --persona auto '{{form1.task}}'"
```

> **Note:** Persona triggers use `--persona` which loads a system prompt and (optionally) enables tools. For text expansion, this adds minimal overhead since tool-calling only activates with `--agent`. The persona's system prompt is loaded from `.squad.json` — no extra API calls.

### Persona + Custom Flags

You can combine `--persona` with temperature and token overrides:

```yaml
# Precise code review (low temp, capped output)
cmd: "... | az-ai --raw --persona reviewer --temperature 0.2 --max-tokens 400"

# Creative writing assist (high temp)
cmd: "... | az-ai --raw --persona writer --temperature 0.8"
```

---

## Quick Reference Card

| Trigger | Tool | Action | Input Source |
|---------|------|--------|-------------|
| `:ai ` | Espanso | Free-form prompt | Form dialog |
| `:aifix` | Espanso | Grammar/spelling fix | Clipboard |
| `:aiemail` | Espanso | Professional email rewrite | Clipboard |
| `:aiexplain` | Espanso | Code explanation | Clipboard |
| `:aisum` | Espanso | Summarize text | Clipboard |
| `:aien` | Espanso | Translate to English | Clipboard |
| `:aishort` | Espanso | Make text concise | Clipboard |
| `:aicommit` | Espanso | Git commit message | Clipboard |
| `:aireview` | Espanso | Code review (persona) | Clipboard |
| `Ctrl+Shift+A` | AHK | Free-form prompt | Dialog box |
| `Ctrl+Shift+F` | AHK | Grammar fix | Selected text |
| `Ctrl+Shift+E` | AHK | Explain code | Selected text |
| `Ctrl+Shift+R` | AHK | Email rewrite | Selected text |
| `Ctrl+Shift+S` | AHK | Summarize | Selected text |
