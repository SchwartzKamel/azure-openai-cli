# az-ai Espanso + AutoHotkey Kit (WSL edition)

Turnkey text-expansion kit for [`az-ai`](https://github.com/SchwartzKamel/azure-openai-cli)
on a **WSL-on-Windows** setup. Every snippet is paste-ready, uses `--raw`, routes
spinner noise to `/dev/null` / `NUL`, and sends user input over **stdin** (never
shell-interpolated args).

> **Latency budget:** plan for **2–3 s** per response in steady state. First call
> after a warm-less WSL distro may add ~1 s. Azure round-trip dominates; local
> overhead is <100 ms on either path below.

---

## 0. What's in this kit

| File | Purpose |
|------|---------|
| `espanso/ai.yml`               | Espanso matches for **Option A** (Espanso runs inside WSL). |
| `espanso/ai-windows-to-wsl.yml`| Espanso matches for **Option B** (Espanso on Windows → `wsl.exe`). |
| `ahk/az-ai.ahk`                | AutoHotkey v2 script for global Windows hotkeys → WSL. |
| `wsl-wrapper/az-ai-wrap.sh`    | Optional login-shell wrapper so Windows callers inherit env vars. |

Pick **one** Espanso option (A or B). AHK is independent and complements either.

> **No GPU? No problem.** This kit works with **just Azure creds** — no
> Docker, no systemd, no NIM. Follow §1 and skip the entire "Local NIM
> (optional)" section at the bottom. Every trigger (`:aifix`, `:airw`,
> `:aitldr`, `:aiexp`, `:aic`, `:ai`) routes to Azure with a 2–3 s budget
> and no local footprint. The local-NIM path is a **later upgrade** for
> users on Blackwell-class hardware who want sub-second `:aifix` / `:airw`;
> see [`docs/nim-setup.md`](../../docs/nim-setup.md) when you're ready.

---

## 1. One-time WSL prerequisites

All commands below run **inside WSL** (Ubuntu 24.04 assumed).

### 1.1 Install the Linux AOT binary

```bash
# From the repo root, or download from Releases:
cd ~/tools/azure-openai-cli
make publish-aot
sudo install -m 0755 dist/aot/AzureOpenAI_CLI /usr/local/bin/az-ai
az-ai --help | head -5
```

### 1.2 Export env vars in `~/.bashrc`

```bash
cat >> ~/.bashrc <<'BASHRC'

# --- az-ai (Azure OpenAI CLI) ---
export AZUREOPENAIENDPOINT="https://YOUR-RESOURCE.openai.azure.com"
export AZUREOPENAIAPI="YOUR-API-KEY"
export AZUREOPENAIMODEL="gpt-4o-mini"
BASHRC

source ~/.bashrc
```

> Keep credentials inside WSL only — do **not** forward them through `WSLENV`
> from the Windows side. Smaller blast radius, simpler compliance story.

### 1.3 Verify the happy path

```bash
echo "say hello in 3 words" | az-ai --raw 2>/dev/null
# → should print ~3 words, no spinner, no trailing newline, exit 0
```

If that fails, **fix it before continuing** — every path below depends on it.

### 1.4 Install the optional wrapper (recommended for Option B and AHK)

`wsl.exe` does **not** start a login shell by default, so `~/.bashrc` isn't
sourced and env vars are missing. Install the wrapper:

```bash
sudo install -m 0755 wsl-wrapper/az-ai-wrap.sh /usr/local/bin/az-ai-wrap
# Smoke test:
echo "test" | /usr/local/bin/az-ai-wrap --raw 2>/dev/null
```

Windows-side callers (Espanso-on-Windows, AHK) should invoke `az-ai-wrap`
instead of `az-ai`. Inside WSL, either one works.

### 1.5 Install a clipboard tool (Option A only)

WSL2 with WSLg ships Wayland; `wl-clipboard` is the most reliable:

```bash
sudo apt install -y wl-clipboard xclip
# Fallback chain used in ai.yml: wl-paste → xclip → powershell.exe Get-Clipboard
```

---

## 2. Option A — Espanso runs **inside WSL** (Linux-native)

**Best for:** primary text work in Linux GUI apps, VS Code Remote-WSL, WSL
terminals, Linux browsers under WSLg.

### Install

```bash
curl -fsSL https://github.com/federico-terzi/espanso/releases/latest/download/espanso-debian-x11-amd64.deb \
  -o ~/espanso.deb
sudo apt install -y ~/espanso.deb
espanso service register
espanso start
```

### Drop in the match file

```bash
mkdir -p ~/.config/espanso/match
cp espanso/ai.yml ~/.config/espanso/match/ai.yml
espanso restart
```

### Smoke test

1. In any text field (WSL terminal, VS Code, Linux Firefox under WSLg) type:
   `:ai ` → a form pops up → type `say hi` → submit → response pastes.
2. Copy any sentence with a typo, then type `:aifix` → corrected text replaces
   the trigger.

---

## 3. Option B — Espanso on **Windows**, calls WSL

**Best for:** primary text work in Windows-native apps (Outlook, Teams, Edge,
Word, Notepad).

### Install Espanso for Windows

Download from <https://espanso.org/install/>, run the installer, then:

```powershell
espanso service register
espanso start
```

### Drop in the match file

```powershell
# %APPDATA% resolves to C:\Users\<you>\AppData\Roaming
copy espanso\ai-windows-to-wsl.yml %APPDATA%\espanso\match\ai.yml
espanso restart
```

### Smoke test

1. In Outlook / Teams / Notepad type `:ai ` → form → `say hi` → response pastes.
2. Copy a sentence with a typo → type `:aifix` → corrected text pastes.

If the first call takes ~1 s longer than subsequent ones, that's the WSL2 VM
warming up — this is expected.

---

## 4. AutoHotkey v2 on Windows → WSL

Complements either Espanso option (or stands alone).

### Install

1. Install **AutoHotkey v2** from <https://www.autohotkey.com/>. (**Not v1** —
   the script uses v2-only syntax.)
2. Install `az-ai-wrap` inside WSL (section 1.4).
3. Double-click `ahk/az-ai.ahk`, or drop a shortcut in `shell:startup` for
   auto-start on login.

### Hotkeys

| Hotkey         | Action                                                        |
|----------------|---------------------------------------------------------------|
| `Ctrl+Shift+A` | Prompt box → free-form query → paste at cursor.               |
| `Ctrl+Shift+E` | Explain currently selected text/code in 2–3 sentences.        |
| `Ctrl+Shift+G` | Grammar/spelling fix on selected text, replace in place.      |
| `Ctrl+Shift+S` | Summarize selected text in 2 sentences.                       |

### Smoke test

1. Select a sentence in Word with a typo → `Ctrl+Shift+G` → it rewrites.
2. Press `Ctrl+Shift+A` → type `capital of France?` → response pastes at cursor.

---

## 5. Troubleshooting

### 5.1 "endpoint/api key not found" when triggered, works in shell

`wsl.exe` starts a **non-login, non-interactive** shell; `~/.bashrc` may be
skipped. Use `az-ai-wrap` (section 1.4) — it sources `~/.bashrc` explicitly
before forwarding args.

### 5.2 Env-var inheritance across WSL boundary

Do **not** rely on `WSLENV` for credentials; keep them inside WSL. If you
genuinely need a Windows env var visible inside WSL:

```powershell
setx WSLENV "SOME_VAR/u:$env:WSLENV"
```

(But for `az-ai` secrets — don't. Put them in `~/.bashrc` inside WSL.)

### 5.3 Clipboard bridging

| Where you are | Read clipboard                  | Write clipboard                   |
|---------------|----------------------------------|------------------------------------|
| WSL (WSLg)    | `wl-paste` / `xclip -o`          | `wl-copy` / `xclip -selection c`   |
| WSL (any)     | `powershell.exe Get-Clipboard`   | `clip.exe`                          |
| Windows CMD   | `powershell -c Get-Clipboard`    | `clip`                              |
| AHK v2        | `A_Clipboard`                    | `A_Clipboard := "text"`             |

`ai.yml` uses a fallback chain: `wl-paste → xclip → powershell.exe Get-Clipboard`.

### 5.4 CRLF / newline handling

PowerShell's `Get-Clipboard` emits **CRLF**. When piped into `az-ai` that's
fine (the model tolerates it), but if the *output* goes back through a
Windows-aware tool you may see doubled newlines. `ai-windows-to-wsl.yml` pipes
clipboard via PowerShell and lets WSL strip CRs; if you see stray `^M` in
pasted output, add `| tr -d '\r'` at the end of the command.

### 5.5 Spinner / stats leaking into pasted text

Every command in this kit routes stderr to `/dev/null` (Linux) or `NUL`
(Windows). If you customize a snippet, **keep the redirect** — and keep
`--raw`. Defense-in-depth against both spinner and usage-stats leakage.

### 5.6 Input sanitization — why stdin not args

Form-entered text and clipboard contents are piped via **stdin** in every
trigger. This means a user typing `$(rm -rf ~)` into a form pops up as literal
text — it is **not** evaluated by the shell, because it never appears inside
the command string. Do **not** refactor triggers to interpolate form values
directly into `cmd:` strings; that reintroduces a shell-injection surface.

The one place where Espanso text substitution is unavoidable (the form body
going into a heredoc) uses a **quoted heredoc delimiter** (`<<'__AZ_AI_EOF__'`),
which disables all shell expansion inside the body.

### 5.7 WSL binary lives on a Windows drive

Don't install `az-ai` into `/mnt/c/...`. Every syscall pays for 9P translation
and cold-start balloons from ~10 ms to ~150 ms. Install into `/usr/local/bin`
on the ext4 root.

### 5.8 Quick diagnostic checklist

```bash
# Inside WSL:
which az-ai && which az-ai-wrap
echo $AZUREOPENAIENDPOINT ; echo $AZUREOPENAIMODEL
echo "ping" | az-ai --raw 2>/dev/null

# From Windows PowerShell:
wsl.exe -e /usr/local/bin/az-ai-wrap --raw "ping" 2>$null
"ping" | wsl.exe -e /usr/local/bin/az-ai-wrap --raw 2>$null
```

All four should print a short response in under 3 s.

---

## 6. Decisions the operator should confirm

- **Option A vs B.** Where do you do most of your text work? Linux-side →
  Option A (simpler, faster). Windows-side apps → Option B.
- **`gpt-4o-mini` as the default model.** Fastest, cheapest, plenty smart for
  text-expansion transforms. Override with `AZUREOPENAIMODEL` if you want
  `gpt-4o`.
- **Persona system (`:aic`).** The `:aic` trigger assumes you've run
  `az-ai --squad-init` at some point and have a `reviewer` persona. If not,
  it falls back to a plain `--system` prompt automatically — see the comment in
  `ai.yml`.
- **Hotkey collisions.** `Ctrl+Shift+S` is "Save As" in some apps. Remap in
  `ahk/az-ai.ahk` if needed (e.g., to `Ctrl+Alt+S`).
