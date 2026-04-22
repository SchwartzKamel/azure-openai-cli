# Accessibility

> *Us little guys gotta stick together.* — M.A.

This page is the user-facing accessibility contract for `az-ai` (v2+). It
documents what the tool **actually does today** — not what it might do
someday. Everything below is grep-verifiable against the source tree. If
the code ever drifts from this page, the code is wrong; file a bug.

Audience:

- Screen-reader users (Orca, NVDA, JAWS, VoiceOver) piping output to TTS.
- Colorblind users (deuteranopia / protanopia / tritanopia).
- Sysadmins on 300-baud SSH / tmux-over-mosh / text browsers.
- CI log-grepers who do **not** want ANSI escape soup in their artifacts.
- Keyboard-only power users (Espanso, AHK, `$EDITOR` — no mouse anywhere).
- Anyone writing shell scripts that branch on `$?`.

If you need to reach a human, see [SECURITY.md](../SECURITY.md) for the
contact paths — accessibility bugs are welcome through the same channel
and get the same triage as crashes.

---

## 1. The color contract

`az-ai` honors the [no-color.org](https://no-color.org/) informal
standard, plus `TERM=dumb`, `CLICOLOR`, `CLICOLOR_FORCE`, and
`FORCE_COLOR` — the superset of conventions you'd expect from a
well-behaved POSIX tool. Color is **garnish, never the entrée**: every
piece of information conveyed by color is also conveyed by adjacent text
(see §5 for the one known gap).

### Precedence — what wins over what

The runtime order is implemented in
[`azureopenai-cli-v2/Theme.cs:108-142`](../azureopenai-cli-v2/Theme.cs).
Seven rules, evaluated top-to-bottom on every color decision. The first
rule that matches wins:

| # | Condition                              | Effect             | Rationale                                            |
|---|----------------------------------------|--------------------|------------------------------------------------------|
| 1 | `NO_COLOR` set to any non-empty value  | **No color, ever** | [no-color.org](https://no-color.org/) spec — presence alone is not enough; empty string does **not** disable. |
| 2 | `TERM=dumb`                            | No color           | Emacs `M-x shell`, `tramp`, legacy serial consoles. |
| 3 | `CLICOLOR=0`                           | No color           | BSD convention. Beats `FORCE_COLOR` — order matters. |
| 4 | `FORCE_COLOR` non-empty and not `"0"`  | Force color        | Node/CI convention. Overrides TTY auto-detect.       |
| 5 | `CLICOLOR_FORCE=1`                     | Force color        | BSD convention. Overrides TTY auto-detect.           |
| 6 | stdout is **not** a TTY                | No color           | Default for pipes, redirects, subshells.             |
| 7 | stdout **is** a TTY                    | Color              | The default interactive path.                        |

Two things to notice and remember:

- **`NO_COLOR` is absolute.** It beats `FORCE_COLOR`, `CLICOLOR_FORCE`,
  and the TTY check. If a user sets `NO_COLOR`, color is off — period.
- **`CLICOLOR=0` beats `FORCE_COLOR`.** This is intentional. A user who
  explicitly says "no color" via `CLICOLOR=0` is not overridden by a
  blanket CI env that sets `FORCE_COLOR=1`.

There is one additional branch above the list — an internal test seam
(`UseColorOverride`) — that exists only for deterministic unit tests and
is never touched in production. It is not a user-facing knob.

### Worked examples

```bash
# 1. Respect a user who wants plain output everywhere. Always works.
NO_COLOR=1 az-ai "summarize this log"

# 2. Piping to cat / less / a file -> no ANSI by default.
az-ai "summarize this log" | cat
az-ai "summarize this log" > out.txt       # out.txt is clean

# 3. CI that captures terminal output and *wants* ANSI preserved
#    (e.g. GitHub Actions log renderer, asciinema):
FORCE_COLOR=1 az-ai "summarize" | tee run.log

# 4. User who hates color globally — set once in shell rc:
#    echo 'export NO_COLOR=1' >> ~/.bashrc
#    Everything is plain, forever. FORCE_COLOR=1 will not override it.

# 5. Legacy Emacs shell / dumb terminal:
TERM=dumb az-ai "summarize"                 # plain, regardless of TTY
```

### What this means for screen readers

ANSI escape sequences (`ESC[31m`, `ESC[0m`, cursor-hide `ESC[?25l`, etc.)
are **noise** to a TTS engine. If your screen-reader setup pipes CLI
output to the synthesizer without stripping escapes, set `NO_COLOR=1`
once in your shell rc and forget about it. Or use `--raw` (§2), which is
stricter still.

---

## 2. The `--raw` contract

`--raw` is the stricter superset of "no color." It is the contract
Espanso, AutoHotkey, pipes to `xclip`, pipes to `say` / `espeak`, and
screen-reader workflows should rely on.

When `--raw` is set, `az-ai` emits:

- **Zero ANSI escapes** — no color, no cursor-hide, no carriage-return
  rewrite tricks. Pure UTF-8 text.
- **Zero spinner frames** — no `⠋⠙⠹⠸`, no rotating characters, no
  backspace-and-rewrite animation fighting the screen reader.
- **Zero stderr warnings** — a malformed config file does not spam
  stderr in raw mode; `UserConfig.Load(quiet: opts.Raw)` respects the
  caller's promise of stderr cleanliness (see
  [`Program.cs:141`](../azureopenai-cli-v2/Program.cs)).
- **Zero version banner** — no `az-ai v2.0.x` preamble.
- **Zero token-usage footer** — the `~123 in / ~456 out tokens` stderr
  line is suppressed.
- **Pure model output, nothing else.** That is the contract.

`--raw` is **gold** for screen-reader users. No ANSI, no spinner
animation, no chrome — just the model's bytes. The same property that
makes it safe for Espanso's "type this into the active text field"
workflow makes it safe to pipe to a TTS engine or a braille display.

### When to use `--raw`

```bash
# Espanso / AHK / any "insert into active field" flow:
az-ai --raw "fix this grammar: $clipboard"

# TTS pipeline — pure text into espeak, no escape-code choke:
az-ai --raw "explain this diff" | espeak-ng

# Shell script variable capture:
answer="$(az-ai --raw "one-word-answer: ready or not?")"

# Braille display via brltty's text device:
az-ai --raw "summarize" > /var/run/brltty/in
```

### When *not* to use `--raw`

- Interactive terminal use where you *want* the color and spinner —
  skip `--raw`, the TTY path is already screen-reader-clean if you also
  set `NO_COLOR=1`.
- When you need the `[ERROR] stdin exceeds 1 MB limit.` class of stderr
  diagnostics — raw silences them by design.

### Stability

`--raw` output is byte-identical across patch releases. If we change
what `--raw` emits in a way that breaks a consumer, that is a breaking
change and goes through a major-version bump. This is a real contract,
not a best-effort.

---

## 3. Exit codes

Exit codes are the scripting contract — Espanso triggers, AHK hotkeys,
CI gates, and `if az-ai … ; then` shell conditionals all depend on a
meaningful, stable non-zero code. The full table:

| Exit | Meaning                                         | When you see it                                                                 |
|-----:|-------------------------------------------------|---------------------------------------------------------------------------------|
| `0`  | Success                                         | Model replied, tool calls resolved, Ralph validation passed.                   |
| `1`  | Runtime error                                   | Model call failed, config invalid, tool execution failed, Ralph hit `--max-iterations` without a clean validation, stdin exceeded the 1 MB limit, `--default-model` asked but none configured. This is the generic "something broke" code. |
| `2`  | CLI usage error                                 | Unknown flag, bad value for `--max-iterations` (not in 1–50), missing required positional. Parser bails before the model is ever called. |
| `130`| SIGINT (Ctrl+C)                                 | User interrupted. POSIX-standard `128 + SIGINT(2)`. `Console.CancelKeyPress` handler at [`Program.cs:406`](../azureopenai-cli-v2/Program.cs). |

### Guarantees

- `0` means **success**. If you see `0`, trust it.
- Non-zero is **meaningful** — never a cosmetic "something printed a
  warning" false alarm.
- `130` specifically means the user pressed Ctrl+C. Scripts should
  generally not retry on 130 (the user said stop).
- The above codes are stable across minor releases. New codes may be
  added in future; existing meanings will not shift.

### Scripting patterns

```bash
# Simple gate:
if az-ai --raw "lint this" > suggestions.txt ; then
    echo "got suggestions"
else
    case "$?" in
        1)   echo "model or config error" >&2 ;;
        2)   echo "bad invocation — check your flags" >&2 ;;
        130) echo "interrupted" >&2 ;;
        *)   echo "unexpected exit: $?" >&2 ;;
    esac
fi

# Never retry on SIGINT:
for attempt in 1 2 3; do
    az-ai --raw "$prompt" && break
    [ "$?" = 130 ] && exit 130
    sleep $((attempt * 2))
done
```

### Finding exit sites in source

Grep for the audit yourself:

```bash
grep -n 'return 1;\|return 2;\|Environment\.Exit' \
  azureopenai-cli-v2/Program.cs azureopenai-cli-v2/*.cs
```

If you find an exit path that isn't in the table above, that's a docs
bug. File it.

---

## 4. Keyboard-only workflows

`az-ai` is keyboard-native from first principles. There is **no GUI**,
**no TUI**, **no cursor-addressable interface**, and **no mouse
affordance** anywhere in the tool surface. Every workflow below is
driveable from a keyboard on a text-only terminal.

### The tool itself

- Pure stdin → stdout. Input is `-p`/`--prompt` argument, a positional
  string, or piped on stdin. No interactive prompts, no "press any
  key," no curses-style menus.
- Config is edited via `$EDITOR` — the tool honors whatever the user
  has configured (Vim, Emacs, nano, `ed`, Helix, whatever). No
  hard-coded editor assumption.
- `--help` and `--version` both return immediately. No pager is
  launched; output goes to stdout and the process exits. You can pipe
  `--help` into `less` yourself if you want paging.

### Espanso (keyboard-only trigger expansion)

Espanso is itself a pure-keyboard tool — you type a trigger like `:ai`
in any text field and it replaces the trigger with the tool's output.
No mouse, no focus-grabbing UI, no modal dialog. This is a WCAG 2.1 SC
2.1.1 *Keyboard* workflow by construction. See
[docs/espanso-ahk-integration.md](espanso-ahk-integration.md) for setup.

### AutoHotkey (Windows keyboard hotstrings)

Same shape: a keystroke or hotstring triggers a shell-out to `az-ai
--raw`. AHK runs entirely on keyboard events. The CLI never pops a
window, never steals focus.

### WSL + Windows clipboard

Keyboard-only interop across the WSL boundary works:

```bash
# Read Windows clipboard from WSL without leaving the keyboard:
prompt="$(powershell.exe -NoProfile Get-Clipboard | tr -d '\r')"
answer="$(az-ai --raw "$prompt")"

# Write back to Windows clipboard:
printf '%s' "$answer" | clip.exe
```

Both `powershell.exe` and `clip.exe` are keyboard-reachable from inside
any WSL shell — no Alt-Tab required.

### Shell completions

If/when shell completions ship, they install into the standard
locations and are driven by `<Tab>` — no mouse. The current shipped
completions (if any for your shell) are listed in
[CONTRIBUTING.md](../CONTRIBUTING.md) and the man page (§5 roadmap).
Completion is a keyboard-only ergonomics win by definition.

### What this tool deliberately does *not* have

- No mouse-only affordance anywhere.
- No "click to copy" button — you already have `clip.exe`, `xclip`,
  `pbcopy`, `wl-copy`.
- No modal dialog that steals focus.
- No cursor-addressable TUI (no ncurses, no Spectre.Console prompt
  widgets).
- No persistent foreground daemon that needs tray-icon interaction.

If a future change would introduce any of these, it requires a docs
update *here* and sign-off from accessibility review. That's the
contract.

---

## 5. Known gaps

Honest list. None of these are crashes for assistive tech — the
silent-by-design baseline keeps the tool functional for everyone
today — but they are real gaps and they are tracked.

- **Emoji used alone in some docs tables.** A handful of status cells
  in audit documents under `docs/` use bare `🟢` / `🟡` / `🔴` or `✅`
  with no adjacent text label. A screen reader will announce "white
  heavy check mark" or "large green circle," which is functional but
  not ideal. Tracked as the `emoji-alt-text` todo (joint Babu + Mickey
  pass). User-facing output from the binary itself is already compliant
  — every emoji is redundant with text.

- **No shipped man page.** `man az-ai` does not currently work because
  no `.1` file is packaged. The text of `--help` is the only reference.
  A `docs/man/az-ai.1` is scoped as future work; when it lands it will
  be generated from the same source as `--help` and shipped in every
  release tarball.

- **Screen-reader testing is not automated in CI.** We run the
  `NO_COLOR` and `--raw` contract tests on every build, but we do not
  run output through Orca / NVDA / VoiceOver under test. Aspirational.
  Until then, we rely on the silent-by-design baseline and the
  `--raw` contract — both of which are automatically verified.

- **Screenshots in docs are not paired with high-contrast variants.**
  If README or docs gain screenshots, only one rendering exists
  today. Dark-mode / high-contrast pairs are aspirational. All current
  `README.md` badges already have text alt-text via the badge-label
  pattern; no action needed there.

- **No `--locale` flag.** Output is en-US. Error messages, log lines,
  and the `--help` text are all English. Locale-aware number and date
  formatting and translated strings are tracked under Babu's
  [docs/i18n.md](i18n.md) and the `docs-i18n` todo. This is explicit
  future work, not an accidental omission.

- **Terminal-width adaptation is implicit.** The tool streams model
  output verbatim; it does not reflow to `$COLUMNS`. For most prose
  this is fine (the model's line breaks are usually reasonable). For
  wide tables in model output on a 40-column mobile-SSH session, your
  terminal's wrap is what you get. A future `--wrap=<N>` flag is on the
  wish list.

- **No `--plain` / `--ascii` flag.** The current `--raw` mode still
  emits UTF-8. A user on a strict-ASCII terminal (some embedded SSH
  clients, very old serial consoles) will see mojibake for any non-ASCII
  the model produces. Today the workaround is `az-ai --raw … | iconv
  -f utf-8 -t ascii//TRANSLIT`. A first-class flag is future work.

If you hit a gap that isn't in this list, that's a bug — please file
it. Accessibility bugs are triaged at the same severity as crashes.

---

## See also

- [`azureopenai-cli-v2/Theme.cs:108-142`](../azureopenai-cli-v2/Theme.cs)
  — the color precedence, in code, as ground truth.
- [docs/espanso-ahk-integration.md](espanso-ahk-integration.md) —
  keyboard-only trigger setup for Espanso and AHK.
- [docs/i18n.md](i18n.md) — Babu's internationalization contract and
  roadmap.
- [SECURITY.md](../SECURITY.md) — how to report issues (accessibility
  bugs welcome on the same channel).
- [no-color.org](https://no-color.org/) — the informal `NO_COLOR` spec
  we honor.
- [WCAG 2.1](https://www.w3.org/TR/WCAG21/) — the authoring references
  cited in the accompanying audit (`docs/audits/`).

---

*Color is garnish, never the entrée. Information must survive
monochrome. If it can't be read aloud, it can't be shipped.* — M.A.
