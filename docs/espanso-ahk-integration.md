# Text Expansion Integration -- Espanso & AutoHotKey

> **TL;DR:** Type a trigger phrase anywhere → get AI-generated text back in-place.  
> This is the PRIMARY use case of `az-ai`: a headless text generation backend for hotkey-driven workflows.

---

## Table of Contents

1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Espanso Configuration](#espanso-configuration) -- Linux / macOS / Windows / **WSL**
4. [AutoHotKey Configuration](#autohotkey-configuration)
5. [Loading Placeholder](#loading-placeholder) -- "yada yada yada" visual feedback
6. [Performance Tips](#performance-tips)
7. [Unicode and Encoding](#unicode-and-encoding) -- UTF-8 enforcement for non-ASCII text
8. [Troubleshooting](#troubleshooting)
9. [Power Triggers](#power-triggers----complex-text-expansion) -- web search, tone picker, structured extraction
10. [Advanced: Persona Integration](#advanced-persona-integration)

---

## Overview

### What This Does

`az-ai` is a CLI that calls Azure OpenAI and prints the response to stdout. When combined with a text expansion tool like [Espanso](https://espanso.org) or [AutoHotKey](https://www.autohotkey.com), it creates an invisible AI layer across your entire OS:

- You're writing an email → type `:aifix` → your clipboard text gets grammar-corrected and pasted back
- You're in Slack → type `:ai` → a form pops up → you type a question → the answer replaces your trigger
- You're coding → select a function, hit `Ctrl+Shift+E` → a 2-sentence explanation appears

No browser tabs. No context switches. The AI meets you where you already are.

### Why `--raw` Matters

By default, `az-ai` shows a braille spinner on stderr while waiting for the first token, and appends token usage stats after the response. That's great for interactive terminal use -- terrible for text injection.

The `--raw` flag strips all of it:

| Behavior | Default | `--raw` |
|----------|---------|---------|
| Spinner on stderr | ✅ (if TTY) | Suppressed |
| Token usage stats | ✅ (on stderr) | Suppressed |
| Trailing newline | ✅ Yes | Suppressed |
| Response text on stdout | ✅ Yes | ✅ Yes |

**Always use `--raw` for text expansion.** Without it, Espanso/AHK may capture spinner artifacts or extra newlines.

> **Note:** When Espanso or AHK invokes `az-ai` via a shell command, stderr is typically redirected, which already suppresses the spinner. The `--raw` flag provides a defense-in-depth guarantee -- use it regardless.

### Performance Expectations

| Scenario | Latency | Notes |
|----------|---------|-------|
| Short prompt, short response (1-2 sentences) | ~1-2s | gpt-4o-mini recommended |
| Grammar fix on a paragraph | ~2-3s | Model reads + rewrites |
| Code explanation (2-3 sentences) | ~2-3s | Depends on code length |
| Long creative writing | 5-10s+ | Use `--max-tokens` to cap |

Latency is dominated by Azure API round-trip and token generation. The AOT binary itself adds only ~11 ms of process startup (v2.0.6, linux-x64 p50 -- see [Performance Tips](#performance-tips)); ReadyToRun adds ~55 ms, and the Docker path adds ~400+ ms of container cold-start.

---

## Prerequisites

### 1. Install `az-ai`

You need the `az-ai` binary accessible from your shell. Two options:

**Option A: Native binary (recommended for text expansion -- fastest startup)**

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

`az-ai` needs three environment variables:

| Variable              | Value                                            |
|-----------------------|--------------------------------------------------|
| `AZUREOPENAIENDPOINT` | `https://your-resource.openai.azure.com`         |
| `AZUREOPENAIAPI`      | your API key (note: `API`, not `APIKEY`)         |
| `AZUREOPENAIMODEL`    | default deployment name (e.g. `gpt-4o-mini`)     |

**Never put these in `.azureopenai-cli.json`** -- that file is for model aliases and defaults, not credentials. Config files get committed, synced to OneDrive, or shared across a team; environment variables don't. See [`docs/config-reference.md`](./config-reference.md#security-notes).

> **v2.0.6+: Auto-load from `~/.config/az-ai/env`.** The `az-ai` binary now calls `LoadConfigEnv()` at startup, which reads `~/.config/az-ai/env` (shell `export KEY="value"` syntax) and sets any env vars that aren't already present. This means Espanso and AHK invocations no longer *strictly* need `bash -lc` or `source ~/.bashrc` to find credentials -- the binary finds them itself. `bash -lc` remains recommended (it also resolves PATH), but if credentials are in `~/.config/az-ai/env`, they'll load even from a bare `wsl.exe -e az-ai` invocation. Run `make setup-secrets` to create this file, or see [`docs/config-reference.md`](./config-reference.md#auto-loaded-env-file-configaz-aienv) for the format.

#### Linux / macOS / WSL -- secure storage

> **Fast path (recommended):** one-liner that auto-detects your OS:
>
> ```bash
> make setup-secrets
> ```
>
> That dispatches to the right script for your environment:
>
> - **Linux / macOS / WSL** → `scripts/setup-secrets.sh` (bash/zsh; chmod 600 or GPG)
> - **Windows (git-bash / MSYS / Cygwin)** → `scripts/setup-secrets.ps1` (user-scope env vars or DPAPI-encrypted file)
>
> Or call directly if you prefer:
>
> ```bash
> bash scripts/setup-secrets.sh              # Linux / macOS / WSL
> powershell -ExecutionPolicy Bypass -File scripts/setup-secrets.ps1   # Windows-native
> VERIFY_ONLY=1 bash scripts/setup-secrets.sh                          # re-check, no prompts
> .\scripts\setup-secrets.ps1 -VerifyOnly                              # Windows, no prompts
> bash scripts/unlock-secrets.sh             # Linux GPG tier -- prime cache after reboot
> ```
>
> Each wizard detects shell (bash/zsh/pwsh), prompts for endpoint + API key + model, lets you pick a storage tier (plaintext-chmod-600 / GPG-encrypted on Unix; user-scope-env / DPAPI-encrypted on Windows), installs an auto-source hook, and runs verification probes at the end. Re-run anytime to rotate keys or switch tiers -- idempotent. Native `az-ai setup` is planned for 2.1 ([FR-022](./proposals/FR-022-native-setup-wizard.md)). The manual walkthroughs below are the long form if you'd rather understand before running.

**Don't paste secrets directly into `~/.bashrc` or `~/.zshrc` if those are committed to a dotfiles repo.** Use one of the options below.

**Option A -- Separate secrets file (simple, recommended).** Keep the secrets out of your tracked rcfile:

```bash
# Create a private file (0600 = only you can read it)
umask 077
cat > ~/.azureopenai.env <<'EOF'
export AZUREOPENAIENDPOINT="https://your-resource.openai.azure.com"
export AZUREOPENAIAPI="your-api-key-here"
export AZUREOPENAIMODEL="gpt-4o-mini"
EOF
chmod 600 ~/.azureopenai.env

# Source it from your rcfile (safe to commit this one line)
echo '[ -f ~/.azureopenai.env ] && . ~/.azureopenai.env' >> ~/.bashrc
# or: >> ~/.zshrc
```

Add `.azureopenai.env` to `.gitignore` if your `$HOME` is under version control.

**Option B -- OS keyring (most secure, needs one helper).** Store the key in `gnome-keyring` / `libsecret` / macOS Keychain and pull it on shell start:

```bash
# One-time: save the secret
# Linux:
secret-tool store --label="Azure OpenAI API" service azure-openai user api-key
# macOS:
security add-generic-password -a "$USER" -s "azure-openai-api" -w

# In ~/.bashrc or ~/.zshrc:
export AZUREOPENAIENDPOINT="https://your-resource.openai.azure.com"
export AZUREOPENAIMODEL="gpt-4o-mini"
# Linux:
export AZUREOPENAIAPI="$(secret-tool lookup service azure-openai user api-key)"
# macOS:
export AZUREOPENAIAPI="$(security find-generic-password -a "$USER" -s azure-openai-api -w)"
```

The endpoint and default model aren't secrets -- leave them in the rcfile.

**Option C -- `direnv` (per-project).** If you run `az-ai` only in specific directories, use [`direnv`](https://direnv.net/) with a `.envrc` that isn't committed. Good for isolating work vs. personal keys.

**Espanso must see the variables.** Espanso spawns its own shell and may not inherit a login environment -- especially on macOS (launchd) and WSL (where Windows-side Espanso launches a non-interactive `wsl.exe`). Confirm with:

```bash
espanso cmd -- bash -lc 'env | grep AZUREOPENAI'
```

If nothing prints, see the [Troubleshooting](#environment-variables-not-found) section below.

#### Windows -- secure storage

> **Fast path (recommended):** the guided wizard handles both tiers below automatically.
>
> ```powershell
> powershell -ExecutionPolicy Bypass -File scripts\setup-secrets.ps1
> # Or from any shell in the repo root:  make setup-secrets
> ```
>
> The wizard prompts for endpoint / API key (hidden input) / model, asks you to pick Tier 1 (user-scope registry env vars -- same as Option A below) or Tier 2 (DPAPI-encrypted file + PowerShell `$PROFILE` hook -- same as Option B below), installs the hook, and verifies that a fresh `powershell.exe` (which is what espanso spawns) sees the vars. Idempotent; re-run to rotate. See [FR-022](./proposals/FR-022-native-setup-wizard.md) for the 2.1 native-subcommand roadmap.

Windows has no `.bashrc`. Pick one of these:

**Option A -- User-level environment variables (simplest).** These persist across reboots and are visible to every process *you* launch, including Espanso and AHK. They are **not** encrypted on disk but are scoped to your Windows user profile.

```powershell
# PowerShell -- run once. Sets User-level (not Machine-level) env vars.
[Environment]::SetEnvironmentVariable('AZUREOPENAIENDPOINT', 'https://your-resource.openai.azure.com', 'User')
[Environment]::SetEnvironmentVariable('AZUREOPENAIAPI',      'your-api-key-here',                      'User')
[Environment]::SetEnvironmentVariable('AZUREOPENAIMODEL',    'gpt-4o-mini',                            'User')
```

Or via GUI: **Win → "Edit environment variables for your account" → New…** under *User variables*. Restart Espanso / AHK / your terminal after setting -- running processes won't pick up changes.

> ⚠️ **Never use the `Machine` scope** for API keys. That makes them readable by every user and every service account on the box. Always `User`.
> ⚠️ **Avoid `setx` from `cmd.exe`** -- it silently truncates values over 1024 chars and mangles `%` characters. The PowerShell `[Environment]::SetEnvironmentVariable` call above has no such limit.

**Option B -- Windows Credential Manager (encrypted at rest, recommended for API keys).** Uses DPAPI so the value is only decryptable by your Windows user:

```powershell
# One-time: install the helper (run as your user, not Admin)
Install-Module -Name CredentialManager -Scope CurrentUser -Force
New-StoredCredential -Target 'AzureOpenAI' -UserName 'api' -Password 'your-api-key-here' `
                     -Persist LocalMachine

# Then in your PowerShell profile ($PROFILE):
$env:AZUREOPENAIENDPOINT = 'https://your-resource.openai.azure.com'
$env:AZUREOPENAIMODEL    = 'gpt-4o-mini'
$env:AZUREOPENAIAPI      = (Get-StoredCredential -Target 'AzureOpenAI').GetNetworkCredential().Password
```

To make the same secret available to **Espanso and AHK** (which don't run under your PowerShell profile), either:

1. Also set `AZUREOPENAIAPI` at User scope once (Option A) -- simpler, accept the plaintext-on-disk tradeoff; or
2. Have Espanso call `powershell -NoProfile -Command "..."` that pulls from Credential Manager on each invocation -- more secure, adds ~150 ms per expansion.

**Option C -- `.env` file loaded by your shell.** Mirror the Linux Option A pattern under your PowerShell `$PROFILE`:

```powershell
# %USERPROFILE%\.azureopenai.env  (NOT under your dotfiles repo)
$env:AZUREOPENAIENDPOINT = 'https://your-resource.openai.azure.com'
$env:AZUREOPENAIAPI      = 'your-api-key-here'
$env:AZUREOPENAIMODEL    = 'gpt-4o-mini'

# In $PROFILE:
$envFile = Join-Path $HOME '.azureopenai.env'
if (Test-Path $envFile) { . $envFile }
```

Restrict the ACL so only you can read it:

```powershell
icacls "$HOME\.azureopenai.env" /inheritance:r /grant:r "$env:USERNAME:(R,W)"
```

As with Option B, Espanso/AHK will **not** inherit `$PROFILE` -- they see the Windows User-scope environment only. If you're going to rely on those tools, Option A remains the pragmatic choice.

#### What not to do

- ❌ **Don't commit secrets to a dotfiles repo** -- even private ones end up forked, leaked in CI logs, or synced to Copilot-indexed clouds.
- ❌ **Don't paste into `.azureopenai-cli.json`** -- the config file has no credential slot by design.
- ❌ **Don't use Machine-scope env vars on shared Windows boxes** -- every user on the machine reads them.
- ❌ **Don't put the API key in a terminal title, PS1 prompt, or shell history** -- rotate immediately if it leaks.
- ✅ **Do rotate keys** in the Azure portal if any of the above happens. It takes 10 seconds.

### 3. Verify It Works

```bash
echo "test" | az-ai --raw
```

You should see a short AI response with no spinner, no decorations, no trailing newline. If this works, you're ready for integration.

**WSL users:** Run `make espanso-test` to verify the full Espanso integration chain -- it checks that `az-ai` is on PATH in a login shell, that env vars are present, and that `LoadConfigEnv` can auto-load `~/.config/az-ai/env` even without a shell profile.

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

> **⚠️ Quote-safety warning for the `:ai` form trigger.** The basic config above passes `{{form1.prompt}}` as a shell argument inside single quotes. If the user types a prompt containing a literal single quote (e.g. `don't`), the shell command breaks. The clipboard-based triggers (`:aifix`, `:aiemail`, etc.) are safe because input arrives via stdin pipe. For a hardened `:ai` trigger that pipes form input via a heredoc (immune to metacharacters), see the reference config in [`examples/espanso-ahk-wsl/espanso/ai.yml`](../examples/espanso-ahk-wsl/espanso/ai.yml).

> **Loading placeholder available.** The example configs in `examples/` include a "yada yada yada" loading placeholder that shows visual feedback while `az-ai` processes. See the [Loading Placeholder](#loading-placeholder) section for details.

### macOS Variants

macOS uses `pbpaste` instead of `xclip`. Replace the clipboard command in each trigger:

```yaml
# macOS -- replace all 'xclip -selection clipboard -o' with 'pbpaste'
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

> **Loading placeholder available.** On macOS, the placeholder uses `osascript` to inject keystrokes via System Events, which requires **Accessibility permission** for your terminal or Espanso. See [Loading Placeholder](#loading-placeholder) for the full pattern.

### Windows Variants

On Windows, use `powershell -c Get-Clipboard` and set the `shell` param explicitly:

```yaml
# Windows -- use PowerShell for clipboard access
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

### WSL Variants (the `:shaka:` build) 🤙

If you're on Windows but do your real work in WSL, you've got two clean paths. Both use the **Linux-native AOT binary** (v2 ships as `az-ai`, ~13 MiB, ~10.7 ms p50 cold start -- see [`docs/perf/v2.0.5-baseline.md`](perf/v2.0.5-baseline.md)) -- skip Docker entirely, skip the `.exe`, just run the native ELF. This is the *fastest* setup on any platform: no Docker daemon, no interop translation, pure syscalls.

**Path A -- Espanso running *inside* WSL** (recommended if you do most text work in a Linux GUI app, terminal, VS Code Remote-WSL, etc.)

Install Espanso natively in WSL, drop the Linux config, done. Zero Windows/Linux boundary -- same process tree:

```bash
# Inside WSL (Ubuntu/Debian-style)
curl -fsSL https://github.com/federico-terzi/espanso/releases/latest/download/espanso-debian-x11-amd64.deb \
  -o /tmp/espanso.deb
sudo apt install /tmp/espanso.deb
espanso service register
espanso start

# Put the AOT binary somewhere on PATH
sudo install -m 0755 dist/aot/AzureOpenAI_CLI /usr/local/bin/az-ai
az-ai --help  # should print in < 20ms
```

Use the **Linux/macOS Espanso config** from earlier in this doc as-is -- `xclip` and all. It works unmodified.

**Path B -- Espanso running on *Windows*, calling the WSL binary** (recommended if you need expansion inside Windows apps: Outlook, Teams, Edge, Notepad, the whole Win32 stack)

Windows Espanso shells out to `wsl.exe` to hop the boundary. The WSL2 VM stays warm between invocations, so the boundary adds roughly **20-80 ms** on top of the ~11 ms AOT cold start. Total local overhead is still <100 ms -- the Azure round-trip dwarfs it.

#### Which shell: `powershell`, `wsl`, or `cmd`?

Espanso natively supports `shell: powershell`, `shell: wsl`, and `shell: cmd` on Windows (see the [Espanso shell extension docs](https://espanso.org/docs/matches/extensions/#shell-extension)). For Path B, **use `shell: powershell`** (which also happens to be Espanso's Windows default -- you can omit it if you like, but setting it explicitly makes the config self-documenting).

Three reasons the PowerShell-direct pattern wins:

| Pattern | Invocation chain | Clipboard source | Verdict |
|---------|------------------|------------------|---------|
| **`shell: powershell`** *(recommended)* | PS → `wsl.exe bash -lc` → `az-ai` | `Get-Clipboard` (native) | ✅ Cleanest. One quoting layer, login shell loads env. |
| `shell: wsl` | `bash` → `powershell.exe`/`clip.exe` → pipe back → `az-ai` | Interop reach-back to Win32 | ⚠️ Works, but clipboard requires hopping *back* to Windows since `xclip` has no `DISPLAY` in headless WSL2. Net: same boundary crossings, reversed, plus fragile interop. |
| `shell: cmd` wrapping `powershell -Command "…"` | `cmd` → `powershell` → `wsl.exe` → `az-ai` | `Get-Clipboard` | ❌ Extra hop, nested quoting (cmd escapes *and* PS escapes), no upside. |

Why `shell: powershell` is right:

- **One quoting layer.** `cmd` wrapping `powershell -Command "…"` forces you to double-escape every inner double-quote (`\"…\""` in YAML turns into cmd-parsed `"…"`, then PS re-parses). With `shell: powershell`, the whole `cmd:` value *is* a PowerShell expression -- YAML handles the outer quoting and PS handles its own strings natively.
- **Native clipboard, correct encoding.** `Get-Clipboard` reads Windows's clipboard directly as a PowerShell string. `shell: wsl` can't do this without shelling back out to `powershell.exe` or `clip.exe` from inside bash, which is strictly more work for the same result.
- **Lower latency, same boundary.** You cross the Windows↔WSL boundary exactly once (`wsl.exe bash -lc …`), not twice. Dropping the `cmd` hop saves ~10-20 ms per invocation on cold shell spawns.
- **When `shell: cmd` is still appropriate:** basically never for this use case. If you ever need to chain `.bat` files or invoke a legacy tool that only parses cmd-style `%VAR%` expansion, that's the time. For `az-ai` + clipboard, it's pure overhead.

#### Why `wsl.exe bash -lc …` (not `wsl.exe -e …`)

The config below invokes `wsl.exe bash -lc "az-ai …"` rather than the arguably-simpler `wsl.exe -e az-ai …`. This is deliberate -- `bash -lc` is the difference between "works on the first try" and "fails silently in espanso":

1. **`bash -lc` launches a login shell**, which sources `~/.bash_profile` / `~/.profile` / `~/.bashrc` (in login order). That's how your `AZUREOPENAIENDPOINT` / `AZUREOPENAIAPI` end up in the environment. `wsl.exe -e` invokes a **non-login, non-interactive** shell that skips all of those rc files -- `az-ai` launches with no credentials, errors to stderr, and because espanso only captures stdout, **you see an empty replacement with no visible error**. Classic silent-fail trap.
2. **stdin forwarding via pipes is more reliable** through `bash -lc` than through `-e` on several older Windows builds. The `-e` flag bypasses the shell entirely; any stdin conversion quirks surface without a buffer in between.
3. **PATH resolution is free.** With a login shell you can just say `az-ai` and let `~/.local/bin` (or wherever your install landed) resolve. No hardcoded `/usr/local/bin/az-ai` that breaks the moment you move the binary.

If you *really* want to skip the login shell (say, to shave another 3-5 ms): put your two env vars in `/etc/environment` instead of `~/.bashrc` (Linux session-wide, WSL-compatible), set them on the Windows side and forward via `WSLENV` (Windows-scoped), or use `wsl.exe env AZUREOPENAIENDPOINT=… AZUREOPENAIAPI=… az-ai …`. None of these are as simple as `bash -lc`, which is why the config below uses the latter.

#### Full Path B WSL config (copy-paste)

Drop this into `%APPDATA%\espanso\match\ai-wsl.yml` -- it mirrors the Linux trigger set 1:1 (`:ai`, `:aifix`, `:aiemail`, `:aiexplain`, `:aisum`, `:aien`, `:aishort`, `:aicommit`, `:aic`, `:aiimg`, plus the power triggers: `:aiexpand`, `:aiweb`, `:aitone`, `:aibullets`, `:aidata`, `:aiflip`), routed through PowerShell -> `wsl.exe` -> the Linux AOT binary:

> **Hardened reference config available.** The config below is the simple `shell: powershell` version for readability. The production-hardened version in [`examples/espanso-ahk-wsl/espanso/ai-windows-to-wsl.yml`](../examples/espanso-ahk-wsl/espanso/ai-windows-to-wsl.yml) goes further: it pipes form input via stdin (heredoc), forces `[Console]::OutputEncoding` to UTF-8, and suppresses stderr at every layer. Use `make espanso-install` to deploy it directly to your Windows Espanso match directory.

```yaml
# %APPDATA%\espanso\match\ai-wsl.yml
# Windows Espanso → WSL → AOT az-ai (Path B)
# Clipboard via Get-Clipboard, one boundary crossing via wsl.exe.
matches:

  # ── Free-form AI prompt (with input form) ─────────────────────
  # Type ":ai " (with trailing space) → form pops up → AI responds.
  # No clipboard needed; prompt is passed as an argv to az-ai.
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
          cmd: "wsl.exe bash -lc \"az-ai --raw '{{form1.prompt}}'\""
          shell: powershell

  # ── Fix grammar & spelling (from clipboard) ───────────────────
  - trigger: ":aifix"
    replace: "{{output}}"
    vars:
      - name: output
        type: shell
        params:
          cmd: "Get-Clipboard | wsl.exe bash -lc \"az-ai --raw --system 'Fix grammar and spelling. Output ONLY the corrected text, nothing else.'\""
          shell: powershell

  # ── Professional email rewrite ─────────────────────────────────
  - trigger: ":aiemail"
    replace: "{{output}}"
    vars:
      - name: output
        type: shell
        params:
          cmd: "Get-Clipboard | wsl.exe bash -lc \"az-ai --raw --system 'Rewrite this as a professional email. Keep the same meaning and tone but make it polished and well-structured. Output ONLY the email text, nothing else.'\""
          shell: powershell

  # ── Explain code (from clipboard) ──────────────────────────────
  - trigger: ":aiexplain"
    replace: "{{output}}"
    vars:
      - name: output
        type: shell
        params:
          cmd: "Get-Clipboard | wsl.exe bash -lc \"az-ai --raw --max-tokens 200 --system 'Explain this code briefly in 2-3 sentences. Be precise and technical. Output ONLY the explanation.'\""
          shell: powershell

  # ── Summarize text (from clipboard) ────────────────────────────
  - trigger: ":aisum"
    replace: "{{output}}"
    vars:
      - name: output
        type: shell
        params:
          cmd: "Get-Clipboard | wsl.exe bash -lc \"az-ai --raw --max-tokens 150 --system 'Summarize in 1-2 sentences. Be concise. Output ONLY the summary.'\""
          shell: powershell

  # ── Translate to English (from clipboard) ──────────────────────
  - trigger: ":aien"
    replace: "{{output}}"
    vars:
      - name: output
        type: shell
        params:
          cmd: "Get-Clipboard | wsl.exe bash -lc \"az-ai --raw --system 'Translate the following to English. Output ONLY the translation, nothing else.'\""
          shell: powershell

  # ── Make text more concise (from clipboard) ────────────────────
  - trigger: ":aishort"
    replace: "{{output}}"
    vars:
      - name: output
        type: shell
        params:
          cmd: "Get-Clipboard | wsl.exe bash -lc \"az-ai --raw --system 'Make this text more concise. Cut unnecessary words. Preserve meaning. Output ONLY the shortened text.'\""
          shell: powershell

  # ── Generate commit message (from clipboard) ───────────────────
  # Copy a git diff → :aicommit → conventional commit message.
  # 100-token cap keeps the bill honest.
  - trigger: ":aicommit"
    replace: "{{output}}"
    vars:
      - name: output
        type: shell
        params:
          cmd: "Get-Clipboard | wsl.exe bash -lc \"az-ai --raw --max-tokens 100 --temperature 0.3 --system 'Write a concise conventional commit message for this diff. Format: type(scope): description. Output ONLY the commit message, no explanation.'\""
          shell: powershell
```

> **Loading placeholder available.** The example configs in `examples/` include a "yada yada yada" loading placeholder that shows visual feedback while `az-ai` processes. On Windows/WSL this uses `SendKeys`. See [Loading Placeholder](#loading-placeholder) for details.

**Gotchas -- read these before you spend an hour debugging:**

> **If `:aifix` fires but the replacement is blank or nothing happens at all** -- 95% of the time it's one of these three. Check in order:
>
> **(1) Env vars aren't reaching WSL.** The old `wsl.exe -e …` pattern doesn't source `~/.bashrc` so `AZUREOPENAIENDPOINT` / `AZUREOPENAIAPI` are missing; `az-ai` fails to stderr and espanso pastes an empty string. The config above uses `wsl.exe bash -lc …` to force a login shell -- if you've copy-pasted an older config, migrate. Verify: open PowerShell and run
>
> ```powershell
> Get-Clipboard | wsl.exe bash -lc "az-ai --raw --system 'Fix grammar.'"
> ```
>
> If you see `[ERROR] AZUREOPENAIENDPOINT is not set`, your env vars aren't in `~/.bashrc` (or wherever your login shell reads) -- fix that first.
>
> **(2) The `az-ai` binary isn't on PATH inside a login shell.** Test:
>
> ```powershell
> wsl.exe bash -lc "command -v az-ai"
> ```
>
> If that prints nothing, `az-ai` isn't in the PATH your login shell sees. Either install it to `/usr/local/bin` (`sudo cp ./az-ai /usr/local/bin/`) or add its directory to PATH in `~/.bashrc` (`export PATH="$HOME/tools/azure-openai-cli/dist/aot:$PATH"`).
>
> **(3) The clipboard is empty.** PowerShell's `Get-Clipboard` returns an empty string when the clipboard has image content, zero bytes, or was cleared by whatever espanso trigger fired before yours. Test:
>
> ```powershell
> Get-Clipboard
> ```
>
> If you see your text, continue. If not, select + copy something, then re-trigger.
>
> Run those three probes in order. The failure is always in one of them.

1. **Use `bash -lc`, not `wsl.exe -e`** -- see the callout above and the dedicated *"Why `wsl.exe bash -lc …`"* section. The old `-e` pattern skips login-shell initialization, credentials go missing, and every clipboard trigger fails silently. If you wrote your config before this migration, upgrade it.
2. **WSL default distro matters.** `wsl.exe bash -lc …` runs in whatever distro is default (`wsl --list --verbose` to check). If you've got multiple distros, pin it: `wsl.exe -d Ubuntu bash -lc "az-ai …"`.
3. **Credentials must live *inside* WSL** (e.g. `~/.azureopenai-cli.json`, `/etc/environment`, or sourced in `~/.bashrc` -- see the *Secure env-var storage* section above). The Windows process environment doesn't cross the `wsl.exe` boundary unless you explicitly forward with `WSLENV`. `bash -lc` sources `~/.bashrc` (via `~/.bash_profile` / `~/.profile`), which is the simplest way to get those vars into the environment `az-ai` sees.
4. **Clipboard encoding.** `Get-Clipboard` emits UTF-16 by default on some locales; if you see mojibake in non-ASCII input, force UTF-8 output from PowerShell before piping. Because `shell: powershell` means the whole `cmd:` value is already PowerShell, you can prepend the encoding setup inline -- no nested cmd+PS escaping:

   ```yaml
   cmd: "[Console]::OutputEncoding = [Text.UTF8Encoding]::new(); Get-Clipboard | wsl.exe bash -lc \"az-ai --raw --system '…'\""
   shell: powershell
   ```

5. **Path translation.** If you need to pass a Windows file path *to* `az-ai` in WSL, use `wslpath`: `wsl.exe bash -lc "az-ai --read-file $(wslpath -a 'C:\\temp\\file.txt')"`. Don't hand raw `C:\…` paths to a Linux binary.
6. **Keep the binary on the WSL side, not a mounted Windows drive.** Running `az-ai` from `/mnt/c/tools/` adds filesystem-translation overhead on every syscall -- that ~11 ms startup becomes ~150 ms. Install to `/usr/local/bin` (native ext4), always.
7. **Single-quote pitfall in the `:ai` form.** `{{form1.prompt}}` is templated by Espanso *before* PowerShell sees it. If the user types a prompt containing a single quote (`don't`), it'll break the PS string. Same limitation exists on the Linux side -- punt on it or pre-sanitize in a wrapper script if it matters.
8. **Cost note (Morty says):** the WSL path is 2-way text over stdin/stdout. Every byte of piped clipboard is an input token you pay for. A 32 KB clipboard is a 32 KB bill event. The CLI caps clipboard at 32 KB in agent mode (`Tools/GetClipboardTool.cs:12`), but here you're piping directly -- *you* enforce the cap with `--max-tokens` on output. Don't let a runaway paste become a runaway invoice.

**Performance summary -- WSL AOT path vs alternatives:**

| Setup                                    | Local overhead  | Runs on   | Notes                           |
|------------------------------------------|:---------------:|:---------:|---------------------------------|
| **WSL AOT (Path A -- Espanso in WSL)**    | **~11 ms**      | WSL       | Fastest. Pure Linux, no boundary.|
| **WSL AOT (Path B -- Windows → wsl.exe)** | **~30-90 ms**   | WSL       | Native Windows apps, warm WSL2. |
| Native Windows `.exe` (AOT Win build)    | ~15 ms          | Windows   | Requires `make publish-aot-win`. No `xclip`, no `bash`.|
| Docker on Windows                        | ~200-500 ms     | Docker    | Cold container start dominates. |
| Docker in WSL                            | ~100-300 ms     | WSL Docker| Still slower than native binary.|

**Bottom line:** on WSL you're running the same Linux AOT binary a Linux dev runs. 2× cheaper than Docker, 10× cheaper than cold `.exe`, identical code path. Bicking back bool. 🤙

---

## AutoHotKey Configuration

[AutoHotKey v2](https://www.autohotkey.com/) provides global hotkeys on Windows. Save the following as `ai-text-expansion.ahk`:

```ahk
; ============================================================
; Azure OpenAI CLI -- AutoHotKey v2 Text Expansion
; ============================================================
; Hotkeys:
;   Ctrl+Shift+A   -- Free-form AI prompt (input box)
;   Ctrl+Shift+F   -- Fix grammar of selected text
;   Ctrl+Shift+E   -- Explain selected code
;   Ctrl+Shift+R   -- Rewrite selected text as professional email
;   Ctrl+Shift+S   -- Summarize selected text
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

; ── Ctrl+Shift+E -- Explain selected code ────────────────────
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

; ── Ctrl+Shift+R -- Rewrite as professional email ────────────
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

## Loading Placeholder

### The Problem

When you type a trigger like `:aifix`, there's a 2-3 second gap while `az-ai` calls Azure OpenAI, streams the response, and hands it back to Espanso. During that gap you see... nothing. No spinner, no cursor change, no feedback. It looks like the trigger didn't fire.

### The "Yada Yada Yada" Solution

The example configs in `examples/` include an optional **loading placeholder** pattern: while `az-ai` processes the request, the text `yada yada yada` appears at the cursor position. When the real response arrives, the placeholder is backspaced away and replaced with actual output. (The name is on-brand -- this project names its [agent fleet](../AGENTS.md) after Seinfeld characters.)

The flow is:

1. **Type placeholder** -- use an OS-level keystroke API to inject `yada yada yada` at the cursor
2. **Run `az-ai`** -- capture the real response into a variable
3. **Clear placeholder** -- send enough `BackSpace` keystrokes to erase the placeholder text
4. **Espanso pastes** -- the real response replaces the trigger as normal

### Platform-Specific Mechanism

| Platform | Type placeholder | Clear placeholder | Requirement |
|----------|-----------------|-------------------|-------------|
| Windows (PowerShell) | `[System.Windows.Forms.SendKeys]::SendWait($ph)` | `[System.Windows.Forms.SendKeys]::SendWait("{BS N}")` | `System.Windows.Forms` (built-in) |
| Linux (X11) | `xdotool type --clearmodifiers "$ph"` | `xdotool key --repeat N BackSpace` | `xdotool` package |
| Linux (Wayland) | `wtype "$ph"` | `wtype -k BackSpace` (xN) | `wtype` package |
| macOS | `osascript -e 'tell app "System Events" to keystroke "$ph"'` | `osascript -e '... key code 51'` (xN) | Accessibility permission |

Where `$ph` is the placeholder string (default `yada yada yada`, 14 characters) and `N` is its length.

### Example (Linux / X11)

```bash
#!/usr/bin/env bash
# Wrapper: type placeholder → run az-ai → clear placeholder → print result
ph="yada yada yada"
xdotool type --clearmodifiers "$ph"
result=$(xclip -selection clipboard -o | az-ai --raw --system 'Fix grammar and spelling. Output ONLY the corrected text, nothing else.')
xdotool key --repeat ${#ph} BackSpace
echo "$result"
```

Use this as the `cmd:` in your Espanso trigger, or call it from a wrapper script.

### Customization

- **Change the placeholder text:** edit the `ph` variable. Any string works -- `yada yada yada` is just the default.
- **Disable the placeholder entirely:** remove the `xdotool` / `osascript` / `SendKeys` lines. The trigger works the same, just without visual feedback.

> **Wayland note:** `xdotool` does not work under Wayland compositors. Use [`wtype`](https://github.com/atx/wtype) instead: `wtype "$ph"` to type, `wtype -k BackSpace` repeated N times to clear. Most distros package it as `wtype`.

> **macOS note:** The `osascript` keystroke injection requires **Accessibility permission** for your terminal (or Espanso itself) in System Settings > Privacy & Security > Accessibility. Without it, the placeholder won't appear.

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
# In your .env or shell profile -- use gpt-4o-mini for expansion
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
| `0.2 - 0.3` | Factual corrections, translations, commit messages | `:aifix`, `:aicommit` |
| `0.5 - 0.6` | Email rewrites, summaries (default: 0.55) | `:aiemail`, `:aisum` |
| `0.7 - 0.9` | Creative writing, brainstorming | Free-form prompts |

```yaml
# Low temp for grammar -- deterministic, consistent
cmd: "... | az-ai --raw --temperature 0.3 --system '...'"

# Higher temp for creative tasks
cmd: "... | az-ai --raw --temperature 0.8 --system '...'"
```

### 5. Write System Prompts That End with "Output ONLY..."

The biggest latency and quality killer for text expansion is a chatty model that adds preamble like "Sure, here's the corrected text:" or "Here you go!". End every system prompt with a strict output directive:

```text
Fix grammar and spelling. Output ONLY the corrected text, nothing else.
```

This saves tokens (faster), prevents pasted junk (cleaner), and improves reliability.

### 6. Native Binary > Docker for Expansion

Docker adds ~200-300ms of container startup overhead per invocation. For text expansion where every millisecond matters, use the native binary:

```bash
# Native binary AOT: ~10.7 ms p50 startup (RECOMMENDED)
~/.local/bin/az-ai --raw "Hello"

# Native binary R2R: ~55ms startup
az-ai --raw "Hello"

# Docker: ~400-600ms startup (container cold-start overhead)
docker run --rm --env-file .env azure-openai-cli --raw "Hello"
```

**Build and install the Native AOT binary (Linux, macOS, WSL):**

```bash
make publish-aot      # builds dist/aot/az-ai (~13 MiB, ~10.7 ms p50 startup -- see docs/perf/v2.0.5-baseline.md)
make install          # copies to ~/.local/bin/az-ai
make bench            # measures cold-start (10 runs)
```

**Cross-platform builds (for distributing to teammates on other OSes):**

```bash
make publish-linux-x64       # Ubuntu/Debian/Fedora/WSL
make publish-linux-musl-x64  # Alpine
make publish-linux-arm64     # Raspberry Pi, ARM servers
make publish-osx-x64         # Intel Mac
make publish-osx-arm64       # Apple Silicon (M1/M2/M3)
make publish-win-x64         # Windows
make publish-win-arm64       # Windows ARM64
make publish-all             # build all 7 at once
```

Cross-builds use ReadyToRun (portable from any host). For maximum speed on each target, run `make publish-aot` natively on that OS.

If you installed via `make alias`, consider switching to a native binary for expansion triggers while keeping Docker for agentic/Ralph workflows.

---

## Unicode and Encoding

Non-ASCII text (CJK, accented Latin, Cyrillic, Arabic, emoji) requires
explicit UTF-8 encoding at every boundary in the Espanso -> PowerShell
-> WSL -> `az-ai` pipeline. This section documents the requirements and
the reasoning behind them.

### The Problem

A Windows user copies Japanese text to the clipboard and triggers
`:aifix`. The pipeline is:

```
Clipboard ("いい子") -> Get-Clipboard -> pipe to wsl.exe -> az-ai -> stdout -> Espanso
```

Without explicit encoding, the output is `???`. Three independent
encoding boundaries can corrupt the text:

1. **PowerShell pipe encoding.** `$OutputEncoding` in PS 5.1 defaults
   to ASCII. Any byte above 0x7F becomes `?` when piped to a native
   command like `wsl.exe`.
2. **Console host encoding.** `[Console]::OutputEncoding` controls how
   the console host encodes stdout back to the calling process
   (Espanso). If this is still set to the system code page, non-ASCII
   bytes are re-encoded through a lossy conversion.
3. **WSL locale.** A non-login WSL shell may inherit `LANG=` (empty),
   which defaults to `POSIX`/`C` locale. Some programs in the WSL
   environment may not handle UTF-8 correctly under this locale.

### The Fix

**Espanso PowerShell commands:** Prefix every `cmd:` that handles
non-ASCII content with:

```powershell
$OutputEncoding = [Text.UTF8Encoding]::new(); [Console]::OutputEncoding = [Text.UTF8Encoding]::new();
```

This sets both the pipe encoding and the console host encoding to UTF-8
for the duration of that command. The production-hardened configs in
[`examples/espanso-ahk-wsl/`](../examples/espanso-ahk-wsl/) include
these declarations in every trigger.

**WSL wrapper script:** `az-ai-wrap.sh` includes a locale fallback:

```bash
LANG=${LANG:-C.UTF-8}
```

This ensures the `C.UTF-8` locale is active even in non-login shells
where no profile files are sourced.

**The `az-ai` binary itself:** `Program.cs` sets
`Console.InputEncoding` and `Console.OutputEncoding` to UTF-8 at
startup. `ShellExecTool` and `GetClipboardTool` set
`StandardOutputEncoding = Encoding.UTF8` on every subprocess. These
are defense-in-depth -- the binary does not rely on the caller to
provide a correct encoding environment.

### Why Both PowerShell Variables Matter

| Variable | Controls | PS 5.1 default | PS 7+ default |
|----------|----------|-----------------|---------------|
| `$OutputEncoding` | Encoding used when piping to native commands | ASCII | UTF-8 |
| `[Console]::OutputEncoding` | Encoding used by the console host for stdout | System code page | UTF-8 |

Setting only one leaves the other boundary unprotected. For Espanso on
Windows with PS 5.1, both must be set explicitly. PS 7+ defaults to
UTF-8 for both, but setting them explicitly is harmless and makes the
config forward-compatible.

For the full encoding enforcement audit, see
[docs/i18n.md](i18n.md#utf-8-enforcement-points).

---

## Troubleshooting

### Environment Variables Not Found

**Symptom:** `az-ai` returns an error about missing endpoint or API key when triggered from Espanso, but works fine in your terminal.

**Cause:** Espanso spawns a new shell that doesn't inherit your `.bashrc`/`.zshrc` environment.

**Quickest fix (v2.0.6+):** If your credentials are in `~/.config/az-ai/env`, `az-ai` auto-loads them at startup via `LoadConfigEnv()` -- no shell profile required. Run `make setup-secrets` to create this file. Verify with:

```bash
# Simulates a bare invocation with no shell profile (exactly what Espanso does)
env -i HOME=$HOME PATH="$HOME/.local/bin:/usr/local/bin:/usr/bin:/bin" az-ai --version
```

If that works, your Espanso triggers will too. If you also need env vars visible to *other* tools, use the shell-profile options below.

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

**Fix (Espanso -- explicit shell sourcing):** Add the `shell` parameter to force Espanso to use a login shell:

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
# Espanso -- full path
cmd: "xclip -selection clipboard -o | /usr/local/bin/az-ai --raw --system '...'"
```

```ahk
; AHK -- full path
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

### Non-ASCII Text Shows as `???` or Mojibake

**Symptom:** Japanese, Chinese, Korean, accented, or other non-ASCII characters from the clipboard become `???`, `????`, or garbled sequences in the Espanso output.

**Cause:** PowerShell 5.1 defaults `$OutputEncoding` to ASCII. When `Get-Clipboard` returns non-ASCII text and pipes it to `wsl.exe`, PowerShell encodes the pipe as ASCII, replacing every non-ASCII character with `?`. A second variable, `[Console]::OutputEncoding`, controls how the console host encodes stdout bytes back to Espanso -- it must also be UTF-8.

**Fix:** Add both encoding declarations at the start of every Espanso PowerShell `cmd:` that handles non-ASCII content:

```yaml
cmd: "$OutputEncoding = [Text.UTF8Encoding]::new(); [Console]::OutputEncoding = [Text.UTF8Encoding]::new(); Get-Clipboard | wsl.exe bash -lc \"az-ai --raw --system 'Fix grammar and spelling. Output ONLY the corrected text, nothing else.'\""
```

Both variables must be set. Setting only `$OutputEncoding` fixes the pipe to `wsl.exe` but not the stdout back to Espanso; setting only `[Console]::OutputEncoding` fixes the return path but not the pipe. See [docs/i18n.md](i18n.md) for the full encoding enforcement table.

**Also check the WSL side:** If `az-ai` runs inside WSL via a non-login shell and `LANG` is unset, the C runtime may default to the `POSIX` locale. The `az-ai-wrap.sh` wrapper sets `LANG=${LANG:-C.UTF-8}` as a fallback. If you invoke `az-ai` directly (without the wrapper), add this to your WSL shell profile:

```bash
# ~/.bashrc or ~/.profile
export LANG="${LANG:-C.UTF-8}"
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

# 6. Full WSL integration check (binary + env vars + LoadConfigEnv)
make espanso-test
```

---

## Power Triggers -- Complex Text Expansion

Beyond the basic clipboard-in/text-out triggers, these demonstrate what's possible when you combine `az-ai` features like agent mode, web search, structured prompts, and multi-field forms.

### `:aiexpand` -- Expand on This

Copy text to clipboard, type `:aiexpand`. The AI takes your brief notes and adds depth, context, and supporting detail while keeping your voice.

**Use case:** You jotted down three bullet points in a meeting. Expand them into a full paragraph for the follow-up email.

### `:aiweb ` -- Web-Augmented Prompt

Type `:aiweb ` (with trailing space) and a form pops up. The AI uses **agent mode with the `web_fetch` tool** to actually search the web for current information before answering.

**Use case:** "What are the latest changes in .NET 10 Preview 4?" -- the AI fetches the actual blog post and synthesizes the answer with source links.

**How it works:** This trigger uses `--agent --tools web --max-rounds 5`, giving the LLM up to 5 tool-call rounds to fetch URLs and build a grounded answer. The placeholder says "searching the web..." instead of "yada yada yada" to set expectations for the longer wait.

### `:aitone` -- Rewrite in Any Tone

Copy text, type `:aitone`. A form appears with a **dropdown of 8 tones**:

- Professional
- Casual / Friendly
- Academic / Formal
- Concise / Direct
- Enthusiastic
- Diplomatic / Careful
- Sarcastic
- ELI5 (Explain Like I'm 5)

The selected tone gets interpolated into the system prompt. Same text, completely different register.

**Use case:** You wrote a blunt Slack message. Rewrite it as "Diplomatic / Careful" before hitting send.

### `:aibullets` -- Convert to Bullet Points

Copy a wall of text, type `:aibullets`. Gets back a clean, grouped bullet-point list with bold headings per topic.

**Use case:** Meeting notes pasted from a transcript. Instantly structured.

### `:aidata` -- Extract Key Facts

Copy any text, type `:aidata`. Extracts names, dates, numbers, decisions, and action items into a structured list grouped by category.

**Use case:** A vendor sent a 3-paragraph email. Pull out just the dates, dollar amounts, and action items.

### `:aiflip` -- Devil's Advocate

Copy an argument or proposal, type `:aiflip`. Returns counter-arguments, unstated assumptions, and blind spots. Not contrarian for its own sake -- intellectually honest pushback.

**Use case:** You're about to send an architecture proposal. Run it through `:aiflip` first to pre-empt objections.

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

> **Note:** Persona triggers use `--persona` which loads a system prompt and (optionally) enables tools. For text expansion, this adds minimal overhead since tool-calling only activates with `--agent`. The persona's system prompt is loaded from `.squad.json` -- no extra API calls.

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

| Trigger | Action | Input Source | Key Feature |
|---------|--------|-------------|-------------|
| `:ai ` | Free-form prompt | Form dialog | General-purpose |
| `:aifix` | Grammar/spelling fix | Clipboard | Low temp (0.3) |
| `:aiemail` | Professional email rewrite | Clipboard | Tone adjustment |
| `:aiexplain` | Code explanation | Clipboard | 200 token cap |
| `:aisum` | Summarize text | Clipboard | Concise output |
| `:aien` | Translate to English | Clipboard | Any language in |
| `:aishort` | Make text concise | Clipboard | Length reduction |
| `:aicommit` | Git commit message | Clipboard (diff) | Conventional format |
| `:aic` | Code review | Clipboard | Persona fallback |
| `:aiimg` | Generate image | Form dialog | Clipboard paste |
| **Power Triggers** | | | |
| `:aiexpand` | Expand / elaborate on text | Clipboard | Add depth + detail |
| `:aiweb ` | Web-augmented AI prompt | Form dialog | Agent + web_fetch |
| `:aitone` | Rewrite in chosen tone | Clipboard + form | Tone picker dropdown |
| `:aibullets` | Convert to bullet points | Clipboard | Structured output |
| `:aidata` | Extract key facts + data | Clipboard | Structured extraction |
| `:aiflip` | Devil's advocate analysis | Clipboard | Counter-arguments |
| `Ctrl+Shift+A` | Free-form prompt (AHK) | Dialog box | |
| `Ctrl+Shift+F` | Grammar fix (AHK) | Selected text | |
| `Ctrl+Shift+E` | Explain code (AHK) | Selected text | |
| `Ctrl+Shift+R` | Email rewrite (AHK) | Selected text | |
| `Ctrl+Shift+S` | Summarize (AHK) | Selected text | |
