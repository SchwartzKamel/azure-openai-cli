# Keyboard-only workflows

> *No mouse. No problem. No excuses.* -- M.A.

This appendix expands [`docs/accessibility.md` §4](../accessibility.md)
with worked examples for users who cannot (or will not) use a pointing
device. The `az-ai-v2` binary has **no mouse affordances anywhere** --
everything that follows is the supported path, not a workaround.

Audience: motor-impaired users driving the CLI via switch input or
sticky-keys; blind users whose screen reader is keyboard-first; remote
ops engineers on a bare SSH session; terminal-purists on principle.

---

## 1. The four input channels

`az-ai-v2` accepts prompts via four keyboard-only channels. Pick the
one that fits your disability profile, ergonomics, or environment.

| Channel              | Best for                                    | Example                                   |
|----------------------|---------------------------------------------|-------------------------------------------|
| Positional argument  | Short one-liners                            | `az-ai-v2 "capital of France?"`           |
| Stdin pipe           | Long prompts, clipboard paste, file input   | `cat prompt.md \| az-ai-v2 --raw`         |
| `--task-file` (Ralph)| Multi-line specs read from disk             | `az-ai-v2 --ralph --task-file task.md`    |
| `$EDITOR` round-trip | Composing prose without leaving the shell   | `$EDITOR /tmp/p && az-ai-v2 < /tmp/p`     |

All four paths are **equivalent for content**. `--raw` is safe to
combine with any of them.

---

## 2. Stdin piping -- the universal fallback

If your input device cannot comfortably produce a long quoted argument
(sticky-key users typing `"` and `\` is painful; switch-input users
cannot chord at all), **pipe instead**. Every shell supports it, every
screen reader understands it, and it bypasses argv length limits.

```sh
# Heredoc -- multi-line prompt, no quoting gymnastics
az-ai-v2 --raw <<'EOF'
Summarize the attached log.
Focus on errors after 14:00 UTC.
EOF

# Clipboard paste on Linux (X11 or Wayland)
xclip -selection clipboard -o | az-ai-v2 --raw
wl-paste                    | az-ai-v2 --raw

# Clipboard on macOS
pbpaste                     | az-ai-v2 --raw

# File
cat bug-report.md           | az-ai-v2 --raw

# Ring buffer -- the last command's output, re-submitted
dmesg | tail -50            | az-ai-v2 --raw "explain this kernel log"
```

Note the positional prompt (`"explain this kernel log"`) *combines*
with piped stdin: `az-ai-v2` concatenates them in the documented order
(see `Program.cs`, `BuildPrompt`). This matters for screen-reader
users: the spoken prompt reads naturally when the short instruction
sits on the command line and the long payload flows on stdin.

---

## 3. `$EDITOR` integration -- composing without leaving the shell

For prompts longer than a few lines, the **correct** keyboard-only
pattern is "compose in your editor of choice, then pipe". `az-ai-v2`
intentionally does **not** launch an editor itself -- that would couple
the CLI to a specific editor and break screen readers that don't
integrate with TUI editors cleanly.

The idiomatic pattern respects `$EDITOR`, which honors user preference
(vim, nano, emacs, `ed`, `code --wait`, braille-friendly editors):

```sh
# Portable one-shot: compose, then submit
tmpfile=$(mktemp --suffix=.md)
${EDITOR:-vi} "$tmpfile"
az-ai-v2 --raw < "$tmpfile"
rm -f "$tmpfile"
```

A reusable shell function (add to `~/.bashrc` or `~/.zshrc`):

```sh
aiedit() {
    local f
    f=$(mktemp --suffix=.md) || return 1
    "${EDITOR:-vi}" "$f" || { rm -f "$f"; return 1; }
    # If the file is empty, bail -- don't send a wasted request.
    [ -s "$f" ] || { rm -f "$f"; echo "aiedit: empty prompt, skipping" >&2; return 2; }
    az-ai-v2 --raw "$@" < "$f"
    local rc=$?
    rm -f "$f"
    return $rc
}
```

Usage: `aiedit`, or `aiedit --persona coder`, or `aiedit --model fast`.
Exit codes from `az-ai-v2` propagate untouched -- the shell function is
transparent to scripting.

`ed`-compatible, too: for users whose accessibility stack works best
with line-oriented editors, `ed` + a heredoc is fully supported.
Nothing in the pipeline assumes a TUI editor.

---

## 4. Ralph mode and `--task-file`

Ralph mode (`--ralph`) is the keyboard-only agent path. You write the
task to a file once, in whatever editor works for you, and Ralph loops
until a validation command passes -- no interactive TUI, no mouse, no
ambiguity:

```sh
$EDITOR task.md                                   # compose
az-ai-v2 --ralph --task-file task.md \
         --validate "dotnet test"                 # run until tests pass
```

This is the pattern preferred by power users on assistive tech -- it is
deterministic, auditable, and re-runnable with exactly zero GUI.

---

## 5. Espanso + AutoHotkey -- the keyboard-only accelerator

[Espanso](https://espanso.org/) (cross-platform) and
[AutoHotkey](https://www.autohotkey.com/) (Windows) are the
*intended* keyboard-only front-ends. The full setup lives in
[`docs/espanso-ahk-integration.md`](../espanso-ahk-integration.md); the
short version:

- You type a trigger (e.g. `:aifix`) into **any text field** -- the OS
  itself routes input; no mouse.
- Espanso/AHK shells out to `az-ai-v2 --raw`, captures stdout, and
  pastes it back where your cursor was.
- Because `--raw` emits only content (no ANSI, no spinner), the paste
  is clean -- no escape-soup, no cursor-hide codes, no TTS choking.

For screen-reader users: this is the single highest-leverage
accessibility win in the project. A trigger like `:grammar` turns any
editable field in any application into an AI-augmented surface,
without ever touching a mouse, without launching a web UI, and without
leaving the text buffer your reader is already focused on.

---

## 6. Shell completions -- tab-to-discover

`az-ai-v2 --completions bash|zsh|fish` emits a completion script on
stdout. Source it, and tab-completion covers every flag and every
registered model alias. For keyboard-only users this is a primary
discovery mechanism -- far more accessible than scrolling through
`--help`.

```sh
# One-shot
source <(az-ai-v2 --completions bash)

# Permanent (bash)
az-ai-v2 --completions bash > ~/.local/share/bash-completion/completions/az-ai-v2

# Permanent (zsh)
az-ai-v2 --completions zsh  > ~/.zsh/completions/_az-ai-v2
```

Screen readers announce tab-completion results cleanly in most shells
(zsh's `menuselect` is the easiest to hear; bash with `readline`'s
`show-all-if-ambiguous` is also good).

---

## 7. What is explicitly NOT required

To pre-empt the "but surely you need X" questions:

- **No GUI.** Not for config, not for auth, not for model selection.
- **No browser.** Device-code OAuth is **not** the auth path;
  `AZUREOPENAIAPI` (API key) or a service-principal env var is.
- **No TUI.** No ncurses, no termbox, no cursor manipulation. The
  binary writes to stdout and stderr in a scrollback-safe way.
- **No focus-stealing popups.** There are no popups. There is no UI.
- **No drag-and-drop.** Paste via stdin, always.
- **No "click to copy".** stdout *is* the output; redirect it.

If you find any affordance that *requires* a mouse -- that is a bug.
Please file it through [`SECURITY.md`](../../SECURITY.md) channels;
accessibility bugs are triaged at crash severity.

---

*Us little guys gotta stick together.* -- M.A.
