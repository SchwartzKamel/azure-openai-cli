# Agent Mode — Use Cases & Examples

> Comprehensive working examples for every agent mode feature in `az-ai`
> (v2.0.0, Microsoft Agent Framework).

## Prerequisites

See [`prerequisites.md`](prerequisites.md) for the required environment
variables (`AZUREOPENAIENDPOINT`, `AZUREOPENAIAPI`, `AZUREOPENAIMODEL`).

## What changed in v2

Agent mode now runs on Microsoft Agent Framework (MAF). The CLI surface is
unchanged, but a few things are worth knowing up front:

- **Tool loop** — MAF's `ChatClientAgent` drives the round-by-round tool
  loop. Each round is one `RunStreamingAsync` call that may emit multiple
  parallel tool invocations; the CLI accounts for it as a single round.
  `--max-rounds` (default `5`, max `20`) caps rounds, not tool calls.
- **Persona overlay** — `--persona <name>` forces agent mode on, replaces
  the system prompt with the persona's, and replaces `--tools` with the
  persona's allow-list if the persona defines any. See
  [`persona-guide.md`](persona-guide.md).
- **`--json` mode** — errors flow as structured JSON on stdout with `error`,
  `message`, and `exit_code` fields. Stderr stays for the spinner and tool
  banners; pair with `2>/dev/null` for a clean machine-readable stream.
- **`--estimate`** — the cost estimator short-circuits **before** agent
  construction. You can ask for a dollar estimate on an agent prompt with no
  credentials loaded.
- **`--telemetry`** (opt-in) — OTel spans and per-call cost events go to
  stderr, unchanged by agent-mode framing.

---

## 1. Agent Mode Activation (`--agent`)

The `--agent` flag enables **agentic mode** — the model can autonomously call tools
(shell, file, web, clipboard, datetime, delegate) and reason across multiple rounds
instead of returning a single static response.

### Standard Mode (No Tools)

```bash
# Standard mode: the model guesses or refuses — no tool access
az-ai "What files are in my current directory?"
```

The model will either hallucinate a file listing or say it cannot access your filesystem.

### Agent Mode (Autonomous Tool Use)

```bash
# Agent mode: the model calls shell_exec → ls → returns real output
az-ai --agent "What files are in my current directory?"
```

Now the model calls `shell_exec` with `ls`, reads the real output, and answers
with your actual directory listing. The response is grounded in reality.

### When to Use Agent Mode

```bash
# Questions about your system — agent mode
az-ai --agent "How much disk space do I have left?"

# Questions about the world — agent mode with web
az-ai --agent "What is the latest version of Node.js?"

# Pure knowledge questions — standard mode is fine
az-ai "Explain the difference between TCP and UDP"
```

**Rule of thumb:** If the answer requires real-time data, file access, or system
interaction, use `--agent`. If it's a knowledge question, standard mode is faster.

---

## 2. Agent Round Limit (`--max-rounds`)

Each time the model calls one or more tools and gets results back counts as one
**round**. The default limit is **5 rounds**, and the max allowed is **20**.

### Default (5 Rounds)

```bash
# Default: up to 5 tool-calling rounds before the model must answer
az-ai --agent "Read my package.json, check what Node version I'm running, and list outdated packages"
```

This task may take 3 rounds:
1. `read_file` → package.json
2. `shell_exec` → `node --version`
3. `shell_exec` → `npm outdated`

Five rounds is usually enough.

### Custom Limit (3 Rounds)

```bash
# Restrict to 3 rounds for a tighter, faster response
az-ai --agent --max-rounds 3 "What's in my home directory?"
```

Use a low limit when the task is simple and you want a quick answer. The model
will prioritize getting to a final response within the budget.

### High Limit for Complex Tasks

```bash
# Allow 15 rounds for a deep investigation
az-ai --agent --max-rounds 15 "Audit all open ports on this machine and explain each service"
```

### Round Exhaustion

```bash
# Intentionally low limit on a complex task
az-ai --agent --max-rounds 1 "Read every .py file in src/, find security issues, and write a report"
```

If the model hits the round limit without producing a final text response, you'll see:

```
[WARN] Agent exhausted 1 tool-calling rounds without completing.
```

The CLI exits with code **1**. Increase `--max-rounds` or simplify the task.

### Validation

```bash
# Invalid: --max-rounds must be 1–20
az-ai --agent --max-rounds 0 "hello"
# → [ERROR] --max-rounds requires an integer 1-20

az-ai --agent --max-rounds 25 "hello"
# → [ERROR] --max-rounds requires an integer 1-20
```

---

## 3. Tool Selection (`--tools`)

By default, all tools are enabled. The `--tools` flag lets you restrict which
tools the model can call, using **short aliases** or full names.

| Alias       | Full Name        | Description                           |
|-------------|------------------|---------------------------------------|
| `shell`     | `shell_exec`     | Execute shell commands                |
| `file`      | `read_file`      | Read local files                      |
| `web`       | `web_fetch`      | Fetch HTTPS URLs                      |
| `clipboard` | `get_clipboard`  | Read system clipboard                 |
| `datetime`  | `get_datetime`   | Get current date/time with timezones  |
| `delegate`  | `delegate_task`  | Spawn a child agent for a sub-task    |

### Select Specific Tools

```bash
# Only allow shell and file tools — no network access
az-ai --agent --tools shell,file "Scan my project for TODO comments"

# Only web and datetime — safe for public-facing use
az-ai --agent --tools web,datetime "What time does the London Stock Exchange open in my timezone?"

# Only shell — locked down to command execution
az-ai --agent --tools shell "Show me the top 10 largest files in this directory"
```

### Why Restrict Tools?

1. **Security**: Remove `shell` to prevent any command execution.
2. **Focus**: Give the model only what it needs — fewer tools means fewer distractions.
3. **Cost control**: Fewer tool options means fewer round-trips.
4. **Compliance**: Remove `web` to ensure no data leaves your network.

```bash
# Read-only file analysis — no shell, no web, no clipboard
az-ai --agent --tools file "Review the code in src/auth.py for bugs"

# Network research only — no local system access
az-ai --agent --tools web,datetime "Research the latest CVEs for OpenSSL"

# Full lockdown — datetime only
az-ai --agent --tools datetime "What's the date 90 days from now?"
```

### Invalid Tool Names

```bash
# Missing --tools argument
az-ai --agent --tools
# → [ERROR] --tools requires comma-separated tool names (e.g., --tools shell,file,web)
```

Unrecognized tool names are silently ignored — only recognized tools are registered.

---

## 4. Shell Execution Tool (`shell_exec`)

Runs a shell command via `/bin/sh -c` and returns stdout. Hardened with a security
sandbox: **blocked commands, no shell substitution, pipe chain filtering, 10-second
timeout, 64KB output cap**.

### Real-World Examples

```bash
# Git history
az-ai --agent --tools shell "Show me the last 5 git commits with stats"
# → model calls: shell_exec { "command": "git log -5 --stat" }

# Directory listing
az-ai --agent --tools shell "What's in /var/log? Show sizes."
# → model calls: shell_exec { "command": "ls -lah /var/log" }

# Disk usage
az-ai --agent --tools shell "How much disk space is used?"
# → model calls: shell_exec { "command": "df -h" }

# System info
az-ai --agent --tools shell "What operating system and kernel am I running?"
# → model calls: shell_exec { "command": "uname -a" }

# Code metrics
az-ai --agent --tools shell "Count lines of code in all Python files"
# → model calls: shell_exec { "command": "find . -name '*.py' | xargs wc -l" }

# Network info
az-ai --agent --tools shell "What ports are currently listening?"
# → model calls: shell_exec { "command": "ss -tlnp" }

# Process list
az-ai --agent --tools shell "Show me the top 5 memory-consuming processes"
# → model calls: shell_exec { "command": "ps aux --sort=-%mem | head -6" }
```

### Blocked Commands (Safety Sandbox)

The tool blocks **24+ dangerous commands** to prevent destructive operations:

| Category          | Blocked Commands                                         |
|-------------------|----------------------------------------------------------|
| **Destructive**   | `rm`, `rmdir`, `mkfs`, `dd`, `format`, `del`, `fdisk`   |
| **System**        | `shutdown`, `reboot`, `halt`, `poweroff`                 |
| **Process**       | `kill`, `killall`, `pkill`                               |
| **Privilege**     | `sudo`, `su`, `passwd`, `crontab`                        |
| **Interactive**   | `vi`, `vim`, `nano`                                      |
| **Network**       | `nc`, `ncat`, `netcat`, `wget`                           |

```bash
# These will all be safely blocked:
az-ai --agent "Delete all files in /tmp"
# → Error: command 'rm' is blocked for safety.

az-ai --agent "Restart the server"
# → Error: command 'reboot' is blocked for safety.

az-ai --agent "Edit my .bashrc with vim"
# → Error: command 'vim' is blocked for safety.
```

### Shell Substitution Blocking

The tool prevents bypass attempts via shell expansion:

```bash
# $() command substitution — BLOCKED
# If the model tries: shell_exec { "command": "echo $(cat /etc/shadow)" }
# → Error: shell substitution ($() and backticks) is blocked for safety.

# Backtick substitution — BLOCKED
# If the model tries: shell_exec { "command": "echo `whoami`" }
# → Error: shell substitution ($() and backticks) is blocked for safety.

# Process substitution — BLOCKED
# If the model tries: shell_exec { "command": "diff <(ls dir1) <(ls dir2)" }
# → Error: process substitution and eval/exec are blocked for safety.

# eval/exec — BLOCKED
# If the model tries: shell_exec { "command": "eval 'rm -rf /'" }
# → Error: process substitution and eval/exec are blocked for safety.
```

### Pipe Chain Filtering

Every segment of a pipe chain is checked against the blocklist:

```bash
# Piping to a blocked command — BLOCKED
# If the model tries: shell_exec { "command": "echo test | rm" }
# → Error: command 'rm' is blocked for safety.

# Safe pipes work fine
az-ai --agent "Find the 10 biggest files and sort them"
# → model calls: shell_exec { "command": "du -ah . | sort -rh | head -10" }
```

### Timeouts & Output Cap

- **Timeout**: 10 seconds. Long-running commands are killed.
- **Stdout cap**: 64KB. Output beyond that is truncated with `... (output truncated)`.
- **Stderr cap**: 16KB (1/4 of stdout cap).

---

## 5. File Reading Tool (`read_file`)

Reads a local file's contents. Supports absolute paths, relative paths, and `~`
expansion. Symlinks are resolved and re-checked against the blocklist.

### Real-World Examples

```bash
# Read a config file
az-ai --agent --tools file "What's in my .gitignore?"
# → model calls: read_file { "path": ".gitignore" }

# Read source code
az-ai --agent --tools file "Review the code in src/main.py"
# → model calls: read_file { "path": "src/main.py" }

# Read with home directory expansion
az-ai --agent --tools file "Show me my SSH config"
# → model calls: read_file { "path": "~/.ssh/config" }
# → Error: access to '/root/.ssh/config' is blocked for security.

# Read log files
az-ai --agent --tools file,shell "Read the last 100 lines of app.log and find errors"
# → Round 1: shell_exec { "command": "tail -100 app.log" }  (or read_file if small)
# → Round 2: model analyzes and responds
```

### Path Traversal Protection

The tool **blocks access** to sensitive system files. Both the logical path and
the resolved symlink target are checked:

| Blocked Path            | What It Protects                   |
|-------------------------|------------------------------------|
| `/etc/shadow`           | Password hashes                    |
| `/etc/passwd`           | User account information           |
| `/etc/sudoers`          | Privilege escalation configuration |
| `/etc/hosts`            | DNS override file                  |
| `/root/.ssh`            | SSH keys and config                |
| `/proc/self/environ`    | Process environment variables      |
| `/proc/self/cmdline`    | Process command-line arguments     |

```bash
# Blocked paths — even if the model tries:
# read_file { "path": "/etc/shadow" }
# → Error: access to '/etc/shadow' is blocked for security.

# read_file { "path": "/proc/self/environ" }
# → Error: access to '/proc/self/environ' is blocked for security.
```

### Symlink Resolution

If a file is a symlink, the tool resolves it to the real target and re-checks:

```bash
# Symlink attack: link.txt → /etc/shadow
# read_file { "path": "link.txt" }
# → Error: access to 'link.txt' is blocked for security (symlink target is restricted).
```

### File Size Limit

```bash
# Files larger than 256KB are rejected
# read_file { "path": "huge-dump.sql" }
# → Error: file too large (524,288 bytes, max 262,144).
```

For large files, combine with shell:

```bash
az-ai --agent --tools shell "Show me the first 50 lines of huge-dump.sql"
# → model calls: shell_exec { "command": "head -50 huge-dump.sql" }
```

---

## 6. Web Fetching Tool (`web_fetch`)

Fetches a URL via HTTP GET. **HTTPS only.** Hardened with DNS-level SSRF protection,
redirect validation, 10-second timeout, and a 128KB response cap.

### Real-World Examples

```bash
# Fetch a webpage
az-ai --agent --tools web "Summarize the homepage of https://example.com"
# → model calls: web_fetch { "url": "https://example.com" }

# Fetch a JSON API endpoint
az-ai --agent --tools web "Get the current Bitcoin price from CoinGecko"
# → model calls: web_fetch { "url": "https://api.coingecko.com/api/v3/simple/price?ids=bitcoin&vs_currencies=usd" }

# Fetch documentation
az-ai --agent --tools web "What are the new features in Python 3.12?"
# → model calls: web_fetch { "url": "https://docs.python.org/3/whatsnew/3.12.html" }

# Fetch release info
az-ai --agent --tools web "What's the latest release of dotnet?"
# → model calls: web_fetch { "url": "https://api.github.com/repos/dotnet/runtime/releases/latest" }
```

### HTTPS-Only Enforcement

```bash
# HTTP URLs are rejected — no unencrypted traffic
# web_fetch { "url": "http://example.com" }
# → Error: only HTTPS URLs are allowed.

# Non-URL strings are rejected
# web_fetch { "url": "ftp://files.example.com/data.zip" }
# → Error: only HTTPS URLs are allowed.
```

### SSRF Protection (Private IP Blocking)

The tool resolves DNS **before** connecting and blocks private/loopback addresses.
It also re-checks **after** redirects to catch DNS rebinding attacks.

```bash
# Direct private IP — BLOCKED
# web_fetch { "url": "https://192.168.1.1/admin" }
# → Error: access to private/loopback addresses is blocked for security.

# Localhost — BLOCKED
# web_fetch { "url": "https://127.0.0.1:8080/api" }
# → Error: access to private/loopback addresses is blocked for security.

# Redirect to private IP — BLOCKED (post-redirect validation)
# https://evil.com → 302 → https://192.168.1.1/admin
# → Error: redirect to private/loopback address is blocked for security.

# Redirect to HTTP — BLOCKED
# https://example.com → 302 → http://example.com/page
# → Error: redirect to non-HTTPS URL is blocked for security.
```

Blocked IP ranges:
- `127.0.0.0/8` — Loopback
- `10.0.0.0/8` — Private (RFC 1918)
- `172.16.0.0/12` — Private (RFC 1918)
- `192.168.0.0/16` — Private (RFC 1918)
- `169.254.0.0/16` — Link-local
- `fd00::/8` — IPv6 unique local (RFC 4193)
- `fe80::/10` — IPv6 link-local

### Response Cap & Redirects

- **Response cap**: 128KB. Larger responses are truncated with `... (response truncated)`.
- **Max redirects**: 3 hops.
- **Timeout**: 10 seconds.

---

## 7. Clipboard Tool (`get_clipboard`)

Reads the current system clipboard text content. Takes **no parameters**.
Cross-platform: uses `xclip`/`xsel` on Linux, `pbpaste` on macOS,
`PowerShell Get-Clipboard` on Windows.

### Real-World Examples

```bash
# "What did I just copy?"
az-ai --agent "What's in my clipboard? Explain it."
# → model calls: get_clipboard {}
# → Returns clipboard contents, model explains them

# Code review from clipboard
az-ai --agent "Review the code I just copied for bugs"
# → model calls: get_clipboard {}
# → Model analyzes the pasted code

# Clipboard as context
az-ai --agent "I copied a URL — fetch it and summarize the page"
# → Round 1: get_clipboard {} → "https://example.com/article"
# → Round 2: web_fetch { "url": "https://example.com/article" }
# → Model summarizes
```

### Platform-Specific Commands

| Platform | Command                             |
|----------|-------------------------------------|
| Linux    | `xclip -selection clipboard -o` (preferred) or `xsel --clipboard --output` |
| macOS    | `pbpaste`                           |
| Windows  | `powershell -NoProfile -Command Get-Clipboard` |

### Limits

- **Max size**: 32KB. Larger clipboard contents are truncated with `... (clipboard content truncated)`.
- **Timeout**: 5 seconds.
- **Empty clipboard**: Returns `(clipboard is empty)`.

```bash
# If xclip/xsel isn't installed on Linux:
# → Error: 'xclip' not found. Install xclip or xsel for clipboard support.
```

---

## 8. DateTime Tool (`get_datetime`)

Returns the current date, time, and timezone. Accepts an optional **IANA timezone**
parameter (e.g., `America/New_York`, `Asia/Tokyo`, `Europe/London`).

### Real-World Examples

```bash
# Current local time
az-ai --agent --tools datetime "What time is it?"
# → model calls: get_datetime {}
# → 2025-01-15 14:32:07 -05:00 (Wednesday) — (UTC-05:00) Eastern Time (US & Canada)

# Specific timezone
az-ai --agent --tools datetime "What time is it in Tokyo?"
# → model calls: get_datetime { "timezone": "Asia/Tokyo" }
# → 2025-01-16 04:32:07 +09:00 (Thursday) — (UTC+09:00) Osaka, Sapporo, Tokyo

# Multiple timezone comparison
az-ai --agent --tools datetime "What time is it in London, New York, and Sydney?"
# → Round 1: get_datetime { "timezone": "Europe/London" }
#            get_datetime { "timezone": "America/New_York" }
#            get_datetime { "timezone": "Australia/Sydney" }
# → Model formats a comparison table
```

### Scheduling Help

```bash
# Meeting scheduling across timezones
az-ai --agent --tools datetime "I need to schedule a meeting at 3pm EST. What time is that in London, Tokyo, and Mumbai?"
# → Multiple get_datetime calls, model calculates offsets

# Day-of-week calculation
az-ai --agent --tools datetime "What day of the week is it today?"
# → get_datetime {} → includes day name in output (e.g., "Wednesday")
```

### Invalid Timezone

```bash
# Unknown timezone — graceful error
# get_datetime { "timezone": "Mars/Olympus_Mons" }
# → Error: unknown timezone 'Mars/Olympus_Mons'
```

### IANA Timezone Examples

| Region        | IANA ID                  |
|---------------|--------------------------|
| US Eastern    | `America/New_York`       |
| US Pacific    | `America/Los_Angeles`    |
| UK            | `Europe/London`          |
| Germany       | `Europe/Berlin`          |
| Japan         | `Asia/Tokyo`             |
| India         | `Asia/Kolkata`           |
| Australia     | `Australia/Sydney`       |
| Brazil        | `America/Sao_Paulo`      |

---

## 9. Delegate Task Tool (`delegate_task`)

Spawns a **child agent** process to handle a focused sub-task. The child is a
separate `az-ai --agent` invocation that runs autonomously and returns its result.

### Real-World Examples

```bash
# Research and summarize
az-ai --agent "Research the top 3 JavaScript frameworks in 2025 and summarize their pros and cons"
# → model calls: delegate_task { "task": "Research top 3 JavaScript frameworks in 2025, summarize pros/cons" }
# → Child agent uses web_fetch to research, returns summary
# → Parent agent formats final response

# Multi-step file analysis
az-ai --agent "Read all the Python files in src/ and identify any security vulnerabilities"
# → model may delegate: delegate_task { "task": "Read all .py files in src/ and identify SQL injection, XSS, or hardcoded credentials", "tools": "shell,file" }
```

### Tool Inheritance

Child agents can have their own tool restrictions:

```bash
# Parent delegates with restricted tools
# delegate_task { "task": "Fetch https://api.example.com/status and parse the response", "tools": "web" }
# → Child agent can ONLY use web_fetch — no shell, no file access
```

Default child tools: `shell,file,web,datetime` (clipboard excluded by default).

### Depth Cap (3 Levels)

Delegation is capped at **3 levels deep** to prevent infinite recursion:

```
Level 0: Parent agent (your CLI invocation)
  └─ Level 1: Child agent (delegate_task)
       └─ Level 2: Grandchild agent (delegate_task)
            └─ Level 3: Great-grandchild → BLOCKED
```

```bash
# If a level-3 agent tries to delegate:
# → Error: maximum delegation depth (3) reached. Complete this task directly instead of delegating.
```

The depth is tracked via the `RALPH_DEPTH` environment variable, incremented on
each delegation.

### Timeouts & Limits

- **Timeout**: 60 seconds per child agent.
- **Output cap**: 64KB from child stdout.
- Azure credentials are passed through to child processes automatically.

---

## 10. Parallel Tool Execution

When the model needs multiple pieces of data, it can call **multiple tools in a
single round**. The CLI executes them **concurrently** via `Task.WhenAll`.

### Real-World Example

```bash
# Ask for three unrelated things at once
az-ai --agent "Get the current time, read my .gitignore, and check disk space"
```

**Round 1** (parallel):
```
🔧 Round 1: get_datetime shell_exec read_file
```

The model sends three tool calls simultaneously:
1. `get_datetime {}` → current time
2. `shell_exec { "command": "df -h" }` → disk space
3. `read_file { "path": ".gitignore" }` → file contents

All three execute in parallel. Results come back together. The model synthesizes
a single response from all three results in one round.

### More Parallel Scenarios

```bash
# System health check — multiple tools in parallel
az-ai --agent "Give me a system health report: CPU load, memory usage, disk space, and uptime"
# → Round 1 (parallel):
#    shell_exec { "command": "uptime" }
#    shell_exec { "command": "free -h" }
#    shell_exec { "command": "df -h" }

# Multi-timezone query — parallel datetime calls
az-ai --agent --tools datetime "What time is it in all US timezones?"
# → Round 1 (parallel):
#    get_datetime { "timezone": "America/New_York" }
#    get_datetime { "timezone": "America/Chicago" }
#    get_datetime { "timezone": "America/Denver" }
#    get_datetime { "timezone": "America/Los_Angeles" }
```

Parallel execution is **automatic** — the model decides when to batch calls. You
don't need any special flag.

---

## 11. Agent Status Display

During agent mode, the CLI writes **status information to stderr** so it doesn't
pollute your stdout (important when piping `--json` output).

### What You'll See

```
⚡ Agent mode                       ← Startup indicator
🔧 Round 1: shell_exec read_file    ← Tool calls in this round
🔧 Round 2: web_fetch               ← Next round
                                     ← Status line cleared before text output
Here's what I found...               ← Final response on stdout
  [tokens: 1250→480, 1730 total]    ← Token usage (stderr)
```

### Breakdown

| Indicator | Meaning |
|-----------|---------|
| `⚡ Agent mode` | Agent loop started |
| `🔧 Round N: tool1 tool2` | Which tools were called in round N |
| `[tokens: X→Y, Z total]` | Input→Output token counts |

### Status Is stderr-Only

```bash
# Status goes to stderr — stdout is clean for piping
az-ai --agent "What time is it?" 2>/dev/null
# → Only the model's text response appears

# JSON mode suppresses the status display on stdout
az-ai --agent --json "What time is it?"
# → Clean JSON on stdout, status on stderr
```

### Suppressed When Redirected

When stderr is redirected (e.g., `2>/dev/null` or `2>agent.log`), status display
is automatically suppressed for clean scripting:

```bash
# Capture response, discard status
RESPONSE=$(az-ai --agent "Check disk space" 2>/dev/null)
echo "$RESPONSE"
```

---

## 12. Combined Agent Scenarios

Real-world multi-step workflows that demonstrate how tools chain together.

### Scenario: "Read my package.json and suggest updates"

```bash
az-ai --agent --tools file,shell "Read my package.json, check which dependencies are outdated, and suggest updates"
```

**Expected agent flow:**
```
⚡ Agent mode
🔧 Round 1: read_file               ← Reads package.json
🔧 Round 2: shell_exec              ← Runs npm outdated
🔧 Round 3: shell_exec              ← Runs npm view <pkg> version (for specific checks)
```

The model reads the file, runs `npm outdated`, cross-references versions, and
produces a summary table with recommended updates.

### Scenario: "Check what's running on port 8080 and kill it"

```bash
az-ai --agent --tools shell "What process is using port 8080?"
```

**Expected agent flow:**
```
⚡ Agent mode
🔧 Round 1: shell_exec              ← Runs ss -tlnp | grep 8080 (or lsof -i :8080)
```

The model identifies the process. Note: it **cannot** kill the process because
`kill` is blocked. It will tell you the PID and suggest you run `kill <PID>`
yourself. This is by design — the agent is read-only for destructive operations.

```
Found: node (PID 12345) is listening on port 8080.
To stop it, run: kill 12345
```

### Scenario: "Fetch weather data and summarize it"

```bash
az-ai --agent --tools web,datetime "What's the weather forecast for today? Use wttr.in"
```

**Expected agent flow:**
```
⚡ Agent mode
🔧 Round 1: get_datetime web_fetch   ← Gets current date + fetches https://wttr.in/?format=3
```

The model fetches `https://wttr.in?format=j1` (JSON format) or the text version,
parses the response, and gives you a human-friendly summary with the current date.

### Scenario: "Read all .py files and find security issues"

```bash
az-ai --agent --tools shell,file --max-rounds 10 "Find all Python files in src/, read each one, and identify security vulnerabilities like SQL injection, hardcoded secrets, or unsafe deserialization"
```

**Expected agent flow:**
```
⚡ Agent mode
🔧 Round 1: shell_exec              ← find src/ -name "*.py"
🔧 Round 2: read_file read_file     ← Reads multiple .py files in parallel
🔧 Round 3: read_file read_file     ← More files
🔧 Round 4: (final analysis)
```

The model discovers files with shell, reads them with the file tool (parallel
where possible), then produces a security audit report. Use `--max-rounds 10`
or higher since multi-file analysis needs more rounds.

### Scenario: "Set up project overview"

```bash
az-ai --agent --max-rounds 8 "Give me a complete overview of this project: read the README, check the directory structure, look at the main source files, and summarize the architecture"
```

**Expected agent flow:**
```
⚡ Agent mode
🔧 Round 1: read_file shell_exec    ← README.md + ls -la (parallel)
🔧 Round 2: shell_exec              ← find . -type f -name "*.cs" | head -20
🔧 Round 3: read_file read_file     ← Key source files
🔧 Round 4: (synthesis and response)
```

### Scenario: "DevOps health check"

```bash
az-ai --agent --tools shell,datetime "Run a full system health check: uptime, load, memory, disk, and network interfaces"
```

**Expected agent flow:**
```
⚡ Agent mode
🔧 Round 1: shell_exec shell_exec shell_exec get_datetime  ← All parallel
   uptime / free -h / df -h / current time
🔧 Round 2: shell_exec              ← ip addr (or ifconfig)

System Health Report — 2025-01-15 14:32 EST
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Uptime: 45 days, 3:21
Load:   0.42, 0.38, 0.35
Memory: 6.2 GB / 16 GB (38%)
Disk:   142 GB / 500 GB (28%)
```

### Scenario: "Multi-tool research with delegation"

```bash
az-ai --agent --max-rounds 8 "I need a comparison of Redis vs Memcached. Research both online, check if either is installed on my system, and give me a recommendation"
```

**Expected agent flow:**
```
⚡ Agent mode
🔧 Round 1: web_fetch web_fetch shell_exec  ← Research + check local installs (parallel)
🔧 Round 2: shell_exec                      ← redis-cli --version / memcached -h
🔧 Round 3: (synthesis)
```

Or the model might delegate the research portion:
```
🔧 Round 1: delegate_task shell_exec
   ├─ Child agent: researches Redis vs Memcached via web
   └─ Parent: checks local installations
🔧 Round 2: (combines child results with local findings)
```

---

## Quick Reference

```bash
# Basic agent mode
az-ai --agent "your question"

# With tool restriction
az-ai --agent --tools shell,file "your question"

# With round limit (default 5, max 20)
az-ai --agent --max-rounds 10 "your question"

# Combined flags
az-ai --agent --tools shell,file,web --max-rounds 8 "complex task description"

# JSON errors for scripting (content still streams on stdout)
az-ai --agent --json "your question"

# Quiet mode (suppress stderr banners/spinner)
az-ai --agent "your question" 2>/dev/null

# Predict cost without calling the API
az-ai --estimate --agent "your question"

# Persona overlay — implies --agent, persona's tools win over --tools
az-ai --persona security "audit src/auth.py"
```
