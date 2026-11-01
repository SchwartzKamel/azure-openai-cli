# Git Ecosystem, Agent Interop & Lightweight CLI Use Cases

> Concrete recipes for using `az-ai` inside **Git hooks**, **PR review pipelines**, **custom JSON manifest plugins**, and **MCP (Model Context Protocol) agent workflows**, plus how `az-ai` stacks up against the 2026 CLI AI ecosystem.

---

## Part 1: Lightweight Git & Shell Automation Recipes

Because `az-ai` is a **~13 MiB Native AOT binary with ~10.7 ms cold start**, it has zero Python/Node interpreter startup tax. That makes it fast enough to run synchronously inside Git hooks, shell keybindings, and Unix pipes.

### 1. Instant Conventional Commit Generator (`prepare-commit-msg` hook)

With combined `stdin` + argument piping and the built-in `commit-message` pattern (`-p commit-message`), `az-ai` can draft your commit message from staged changes:

```bash
# One-liner in your terminal:
git diff --cached | az-ai -p commit-message --raw > .git/COMMIT_EDITMSG && git commit -e -F .git/COMMIT_EDITMSG
```

Or drop it into `.git/hooks/prepare-commit-msg` (executable via `chmod +x`):

```bash
#!/usr/bin/env bash
COMMIT_MSG_FILE="$1"
COMMIT_SOURCE="$2"

# Only generate when no -m message, template, or merge commit is active
if [ -z "$COMMIT_SOURCE" ]; then
  DIFF="$(git diff --cached --diff-algorithm=minimal)"
  if [ -n "$DIFF" ]; then
    printf "%s" "$DIFF" | az-ai -p commit-message --raw --prewarm > "$COMMIT_MSG_FILE.tmp" \
      && cat "$COMMIT_MSG_FILE" >> "$COMMIT_MSG_FILE.tmp" \
      && mv "$COMMIT_MSG_FILE.tmp" "$COMMIT_MSG_FILE"
  fi
fi
```

### 2. Pre-Push Security & Bug Gate (`pre-push` hook with `--schema` + `--json`)

Combine `--schema` (strict wire JSON schema enforcement) with `--json` to create a deterministic, machine-parsed pre-push security audit on outgoing commits:

```bash
#!/usr/bin/env bash
set -euo pipefail

SCHEMA='{"type":"object","properties":{"safe":{"type":"boolean"},"blocking_issues":{"type":"array","items":{"type":"string"}}},"required":["safe","blocking_issues"],"additionalProperties":false}'

VERDICT=$(git diff origin/main...HEAD | az-ai -p security-audit --schema "$SCHEMA" --json "Audit this diff for hardcoded secrets, SSRF, or injection bugs.")

SAFE=$(printf "%s" "$VERDICT" | jq -r '.response | fromjson | .safe')
if [ "$SAFE" != "true" ]; then
  echo "❌ Security gate blocked push:" >&2
  printf "%s" "$VERDICT" | jq -r '.response | fromjson | .blocking_issues[]' >&2
  exit 1
fi
```

### 3. Direct Code Generation to Files (`--code`)

`--code` strips markdown prose and ``` fences so stdout is pure runnable code:

```bash
# Generate a script or config directly into a file
git log -n 20 --oneline | az-ai --code "Write a bash script that groups these commits by Conventional Commit type" > scripts/summarize-commits.sh

# Pipe an existing file in + write refactored code out
cat Dockerfile | az-ai --code "Harden this Dockerfile for non-root execution and multi-stage build" > Dockerfile.hardened
```

### 4. CI FinOps Budget Gate (`--estimate --json`)

Before running an expensive LLM job over a large diff in GitHub Actions, check the exact estimated token cost offline (no API key required):

```bash
COST_JSON=$(git diff origin/main...HEAD | az-ai --estimate --estimate-with-output 1000 --model gpt-4o --json "Review this PR")
MAX_USD=$(printf "%s" "$COST_JSON" | jq -r '.total_usd_max')
echo "Estimated worst-case PR review cost: \$${MAX_USD}"
```

### 5. Real-Time Streaming into Other Tools (`--jsonl`)

When an external dashboard, IDE extension, or parent script wants live token-by-token progress *plus* final token telemetry at the end, use `--jsonl`:

```bash
git show HEAD | az-ai -p explain-code --jsonl | while IFS= read -r line; do
  TYPE=$(printf "%s" "$line" | jq -r '.type')
  if [ "$TYPE" = "delta" ]; then
    printf "%s" "$(printf "%s" "$line" | jq -r '.text')"
  elif [ "$TYPE" = "done" ]; then
    printf "\n[completed in %sms, %s tokens]\n" \
      "$(printf "%s" "$line" | jq -r '.duration_ms')" \
      "$(printf "%s" "$line" | jq -r '.total_tokens')" >&2
  fi
done
```

---

## Part 2: Extending `az-ai` with Zero-Reflection Git Tool Plugins (FR-012)

You can give `az-ai --agent` and `az-ai --ralph` custom Git tools **without recompiling the AOT binary** by dropping JSON manifests into `~/.config/azureopenai-cli/tools/` (or repo-local `./.azureopenai-cli/tools/` with `--allow-local-plugins`).

Example `~/.config/azureopenai-cli/tools/git_status_short.json`:

```json
{
  "name": "git_status_short",
  "description": "Show short git status and current branch for the repository.",
  "command": ["git", "status", "--short", "--branch"],
  "timeout_seconds": 5,
  "dangerous": false
}
```

Example `~/.config/azureopenai-cli/tools/git_log_file.json`:

```json
{
  "name": "git_log_file",
  "description": "Show recent git commits touching a specific file path.",
  "parameters": {
    "type": "object",
    "properties": {
      "count": { "type": "integer", "description": "Number of commits to show (e.g. 5)" },
      "path":  { "type": "string",  "description": "Relative file path" }
    },
    "required": ["path"]
  },
  "command": ["git", "log", "--oneline", "-n", "{count}", "--", "{path}"],
  "timeout_seconds": 10,
  "dangerous": false
}
```

Verify and run:

```bash
# 1. Inspect all built-in + custom plugin tools as JSON
az-ai --list-tools --json | jq '.[] | {name, source, dangerous}'

# 2. Let the agent use your custom git tools with a hard 3-round ceiling
az-ai --agent --tools file,git_status_short,git_log_file --max-rounds 3 \
  "Which files are modified right now, and who last touched them?"
```

**Security guarantees:**

- Commands execute via `ProcessStartInfo.ArgumentList` (discrete argv entries, **never** `/bin/sh -c`), so `{path}` containing `; rm -rf /` cannot inject shell commands.
- `AZUREOPENAIAPI` and other secrets are scrubbed from the child process environment by default.
- Built-in tool names (`read_file`, `shell_exec`, etc.) can never be shadowed by a plugin.

---

## Part 3: Agent-to-Agent Interop via MCP (`az-ai mcp serve` & `--mcp-server`)

### A. Plugging `az-ai` into Claude Code, GitHub Copilot CLI, or Cursor (`az-ai mcp serve`)

Add `az-ai` to your host agent's MCP configuration (`~/.claude.json`, `.copilot/mcp-config.json`, or `.cursor/mcp.json`):

```json
{
  "mcpServers": {
    "az-ai": {
      "command": "az-ai",
      "args": ["mcp", "serve"]
    }
  }
}
```

What the host agent immediately gains over `stdio` JSON-RPC 2.0 (supporting both newline-delimited JSON and `Content-Length` framing):

- **Tools (`tools/list`, `tools/call`):** `read_file`, `web_fetch`, `get_datetime`, `get_clipboard`, `delegate_task` (in-process Azure OpenAI sub-agent), plus any custom manifest plugins. (`shell_exec` is omitted unless you explicitly pass `--allow-shell`).
- **Prompts (`prompts/list`, `prompts/get`):** All 8 built-in patterns (`commit-message`, `code-review`, `security-audit`, `summarize`, etc.), custom user patterns, and all `.squad.json` personas (`persona:coder`, `persona:reviewer`, etc.).
- **Resources (`resources/list`, `resources/read`):** Active `.squad.json` (`squad://config`) and per-persona persistent session memory (`squad://memory/<persona>`).

### B. Plugging External MCP Servers *into* `az-ai` (`--mcp-server`, `az-ai mcp tools/call`)

`az-ai` also acts as an outbound MCP stdio client:

```bash
# 1. Inspect tools exposed by any MCP server
az-ai mcp tools "npx -y @modelcontextprotocol/server-filesystem ." --json

# 2. Call an MCP tool directly from a shell script
az-ai mcp call "az-ai mcp serve" get_datetime '{"timezone":"UTC"}' --json

# 3. Bridge external MCP servers into az-ai's autonomous Ralph loop
az-ai --ralph \
  --mcp-server "fs=npx -y @modelcontextprotocol/server-filesystem ." \
  --validate "dotnet test" \
  "Fix the failing unit test and verify the build passes"
```

---

## Part 4: How `az-ai` Compares Across the Git / CLI Ecosystem (2026)

| Dimension | `az-ai` (v2.1+) | `aichat` (Rust) | `crush` (Go, ex-`mods`) | `fabric` (Go) | `llm` / `sgpt` (Python) | `gh copilot` / `claude` / `codex` |
|---|---|---|---|---|---|---|
| **Cold Start (p50)** | **~10.7 ms** (.NET 10 Native AOT) | ~30-60 ms | ~40-90 ms | ~40-80 ms | ~350-900 ms (Python VM tax) | ~200-500 ms (Node/bundled runtime) |
| **Binary Footprint** | **~13 MiB single binary** | ~12-18 MiB | ~25-35 MiB | ~25+ MiB | Requires Python + venv | 50-150+ MiB |
| **Git Pipe Ergonomics (`git diff \| tool "prompt"`)** | ✅ Bounded 1 MB `stdin` + positional prompt merge | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes | ⚠️ Varies by headless flag |
| **Machine Output (`--json`, `--jsonl`, `--code`, `--schema`)** | ✅ **All 4** (AOT source-generated JSON) | ⚠️ `--code` + text; no `--jsonl` envelope | ⚠️ Text / TUI primary | ⚠️ Text primary | `llm` has `--schema`; `sgpt` has `--code` | `claude -p --output-format json/stream-json` |
| **Curated Prompt Patterns (`-p commit-message`)** | ✅ Built-in + `~/.config/.../patterns/*.json` | ✅ Roles | ❌ No | ✅ **Category leader** (100s of patterns) | ⚠️ Via plugins | ⚠️ Custom slash commands |
| **Custom CLI Tool Plugins (Zero-Compile)** | ✅ JSON manifests (`ArgumentList` sandboxed) | ✅ `llm-functions` (bash/argc) | ❌ MCP only | ❌ No | ✅ Python `pluggy` | ✅ Hooks / MCP |
| **MCP Support** | ✅ **Both Client & Server** (`stdio` NDJSON + `Content-Length`) | ✅ Client | ✅ Client | ⚠️ Partial | ⚠️ Via bridges/plugins | ✅ Client (and `claude mcp serve`) |
| **Autonomous Validation Loop** | ✅ `--ralph --validate "<cmd>"` | ❌ No | ❌ Interactive TUI | ❌ No | ❌ No | ✅ Full-auto / Autopilot |
| **Pre-Flight Cost Estimator (Offline)** | ✅ `--estimate --json` (zero API call) | ❌ No | ❌ No | ❌ No | ❌ No | ❌ Post-hoc only |
| **License & Seat Cost** | **MIT ($0 seat + Azure PAYG)** | MIT / Apache-2.0 ($0) | FSL-1.1-MIT (source-available) | MIT ($0) | MIT / Apache-2.0 ($0) | Paid seats ($10-$100/mo) or vendor-locked |

---

*See also: [`use-cases.md`](use-cases.md), [`competitive-analysis.md`](competitive-analysis.md), and [`examples/git-automation/`](../examples/git-automation/).*
