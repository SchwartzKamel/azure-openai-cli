# FR-005: Shell Integration & Output Intelligence

**Priority:** P1 — High  
**Impact:** Makes the tool feel native to the terminal, not bolted on  
**Effort:** Medium (2-3 days)  
**Category:** Developer Experience / Virality

---

## The Problem

Right now, the CLI outputs raw text to stdout with no awareness of *what* it's outputting or *where* it's going. Every response looks the same whether it's code, a shell command, an explanation, or a list. The user has to:

1. **Manually copy code blocks** from a wall of text
2. **Manually run shell commands** the AI suggests (copy, paste, hope it's safe)
3. **Read unformatted markdown** as raw text — `**bold**` shows as `**bold**`, not **bold**
4. **Get no visual distinction** between code, prose, and warnings

Compare this to how ChatGPT, GitHub Copilot, or even `glow` render output — with syntax highlighting, clear code block boundaries, and actionable buttons. Terminal tools don't need to be ugly.

---

## The Proposal

### 1. Markdown-Aware Terminal Rendering

Detect when stdout is a TTY (not piped) and render markdown with ANSI formatting:

```
# Before (current):
Here's a Python function:
```python
def hello():
    print("Hello, world!")
```
You can run it with `python hello.py`.

# After (with rendering):
Here's a Python function:
  ┌────────────────────────────
  │ def hello():
  │     print("Hello, world!")
  └────────────────────────────
You can run it with python hello.py.
```

**Implementation options:**
- Use ANSI escape codes directly (zero dependencies, full control)
- Integrate `Spectre.Console` (.NET library for rich terminal output — actively maintained, MIT license)

**Key rendering rules:**
- Bold, italic, inline code → ANSI styles
- Code blocks → bordered, optionally syntax-highlighted
- Lists → clean indentation
- When stdout is piped (not a TTY) → output raw text as today (preserves composability)

### 2. Shell Command Mode (`--exec` / `--shell`)

The killer feature for daily-driver adoption. When the user asks for a shell command, don't just print it — offer to run it:

```bash
$ az-ai --shell "find all Python files modified in the last week"

  find . -name "*.py" -mtime -7

  [Enter] Run  |  [e] Edit  |  [c] Copy  |  [Esc] Cancel
```

This is how GitHub Copilot CLI (`gh copilot suggest`) works, and it's the feature people demo on Twitter.

**Safety model:**
- **NEVER** auto-execute. Always show the command first and require explicit confirmation.
- Display commands in a visually distinct box so they're not confused with explanatory text.
- Log executed commands to `~/.azureopenai-cli/history.log` for audit.
- Optionally support a `--dry-run` mode that only prints, never prompts to execute.

### 3. Clipboard Integration

When a response contains code, offer a quick copy:

```bash
$ az-ai "regex to validate email addresses"

  ^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$

  [Copied to clipboard]
```

**Implementation:**
- Detect single code block responses
- If `--copy` flag is set, or response is a single code block and stdout is a TTY, auto-copy
- Use `xclip`/`xsel` (Linux), `pbcopy` (macOS), or `clip.exe` (Windows/WSL)
- Always print to stdout as well (clipboard is a bonus, not a replacement)

### 4. Output Format Flags

Let users control output format explicitly:

```bash
# Default: smart rendering (markdown in TTY, raw when piped)
az-ai "explain Docker networking"

# Raw mode: plain text, no formatting (for scripting)
az-ai --raw "explain Docker networking"

# JSON mode: structured output (for programmatic consumption)
az-ai --json "list 5 Linux commands for disk usage"
# {"response": "...", "model": "gpt-4o", "tokens": 150, "duration_ms": 1200}

# Code-only mode: extract just the code blocks
az-ai --code "write a Python Fibonacci function"
# Outputs ONLY the code, no explanation. Perfect for:
# az-ai --code "write hello world in Python" > hello.py
```

The `--code` flag is particularly powerful. It turns the CLI into a code generator that can write directly to files:

```bash
az-ai --code "Python script to resize images in a directory" > resize.py
az-ai --code "Dockerfile for a Node.js app" > Dockerfile
az-ai --code "GitHub Actions workflow for Python CI" > .github/workflows/ci.yml
```

---

## What This Enables

### The Social Media Moment

The features in this proposal are **demo-able in a 15-second GIF**:

1. User types `az-ai --shell "compress all PNGs in this directory"`
2. Beautifully formatted command appears in a box
3. User hits Enter
4. Command runs, PNGs are compressed
5. 🎉

That's a tweet. That's a Reddit post. That's a "look what I found" Slack message. The current tool — which outputs plain text and requires the user to manually copy-paste — has no demo moment.

### The Workflow Integration

```bash
# Morning routine: AI-powered git workflow
alias morning='az-ai --code "changelog entry for these changes: $(git log --oneline -5)" >> CHANGELOG.md'

# Quick script generation
az-ai --code "bash script to backup my dotfiles to S3" > backup.sh && chmod +x backup.sh

# Infrastructure as Code
az-ai --code "Terraform module for an Azure Function App" > main.tf
```

---

## Implementation Priority

| Sub-feature | Effort | Impact | Ship Order |
|---|---|---|---|
| `--raw` and `--code` flags | Small | High | 1st |
| TTY-aware markdown rendering | Medium | High | 2nd |
| `--shell` execute mode | Medium | Very High (viral) | 3rd |
| `--json` output | Small | Medium | 4th |
| Clipboard integration | Small | Medium | 5th |

---

## Why This Is P1

These features are what transform "useful" into "delightful." The raw capabilities of the CLI are already solid — it connects to Azure, streams responses, manages models. But the *output experience* is where competitors are winning.

**Charmbracelet's `mods`** has gorgeous terminal rendering via Glamour/Lipgloss. **GitHub Copilot CLI** has the shell-execute-with-confirmation flow. **`sgpt`** has `--shell` and `--code` modes.

This tool's Azure-native identity gives it a privileged position in enterprise environments where those tools may not be approved. But the output experience needs to match the expectations set by those tools. Otherwise the Azure-native advantage gets burned on a bad first impression.

---

## Exit Criteria

- [ ] Markdown rendering active when stdout is a TTY
- [ ] Raw text output when stdout is piped (backward compatible)
- [ ] `--raw` flag forces plain text output
- [ ] `--code` flag extracts and outputs only code blocks
- [ ] `--shell` flag shows command in a box with run/edit/copy/cancel options
- [ ] Shell commands require explicit confirmation before execution
- [ ] `--json` flag outputs structured JSON with metadata
- [ ] Update `--help` and README with new flags
