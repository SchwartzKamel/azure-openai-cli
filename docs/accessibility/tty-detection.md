# TTY detection — worked examples

> *Color is garnish, never the entrée. The plate tells you which.* — M.A.

`docs/accessibility.md` §1 states the seven-rule color precedence in
the abstract. This appendix shows the same rules in action, command by
command, with the exact observed output class. Pair with
[`azureopenai-cli-v2/Theme.cs:108-142`](../../azureopenai-cli-v2/Theme.cs)
for the ground-truth implementation.

**Legend** (used in every table below):

- **CLEAN** — zero ANSI escapes, zero cursor-hide codes, zero
  spinner. Safe for TTS, braille, CI logs, and `grep`.
- **CHROME** — ANSI color, section headers, and (where not suppressed
  by `--raw`) a subtle progress affordance on stderr.
- **CHROME-NC** — CHROME but with color stripped; structure still
  emitted.

---

## 1. The five scenarios that matter

### 1.1 Interactive terminal, default

```sh
az-ai-v2 "hello"
```

- stdout: **CHROME** (TTY detected, no env overrides).
- stderr: CHROME (status prefixes, token usage).
- Why: rule 7 — stdout is a TTY, no suppress-env set.

### 1.2 Piped to another command

```sh
az-ai-v2 "hello" | tee out.txt
az-ai-v2 "hello" | less
az-ai-v2 "hello" > out.txt
```

- stdout: **CLEAN** (rule 6 — stdout is not a TTY).
- stderr: still CHROME *unless* `--raw` is set — status text goes to
  a terminal if stderr is still a TTY, which is the common case for
  `cmd | tee`. This is the correct POSIX behavior: stderr is for the
  human, stdout is for the pipeline.
- If both stdout and stderr are redirected (e.g. `>out 2>err`), both
  fall to CLEAN automatically; no env flag needed.

### 1.3 Explicit opt-out — `NO_COLOR`

```sh
NO_COLOR=1 az-ai-v2 "hello"            # interactive, no color
NO_COLOR=1 az-ai-v2 "hello" | less     # piped, still no color
NO_COLOR=1 FORCE_COLOR=1 az-ai-v2 "hi" # NO_COLOR WINS
```

- stdout (interactive): **CHROME-NC** — structure preserved, color
  stripped. Rule 1 fires first.
- Last line: `FORCE_COLOR=1` does **not** override. That is the
  [no-color.org](https://no-color.org/) contract and it is the single
  most important property on this page.

### 1.4 Explicit opt-in — `--raw`

```sh
az-ai-v2 --raw "hello"                   # interactive, but CLEAN
az-ai-v2 --raw "hello" | pbcopy          # clipboard-safe
echo "prompt" | az-ai-v2 --raw | xclip   # double-pipe, still CLEAN
```

- stdout: **CLEAN** in every case. `--raw` short-circuits the TTY
  check and suppresses stderr chrome too.
- This is the **recommended mode** for:
  - Espanso / AutoHotkey consumers (pasted back into the focused app).
  - Screen readers (TTS engines choke on ANSI).
  - Braille displays (unstable byte stream = unstable braille cells).
  - High-latency SSH (each escape code is wasted bytes on a 300-baud
    or satellite link).
  - CI log artifacts (`grep` wants plain text).

### 1.5 `TERM=dumb` and Emacs `M-x shell`

```sh
TERM=dumb az-ai-v2 "hello"
```

- stdout: **CHROME-NC**. Rule 2 fires.
- Why this matters: Emacs shell-mode sets `TERM=dumb` automatically,
  as does `tramp`, as do many legacy serial-console profiles. A
  surprising number of blind users live in Emacs; honoring `TERM=dumb`
  means "just works" in that environment.

---

## 2. Decision matrix — what actually comes out

This table is the quick-reference. Read left to right: find the row
whose conditions match your invocation, read off the expected class.

| `--raw`? | stdout is TTY? | `NO_COLOR`? | `TERM`      | `CLICOLOR` | `FORCE_COLOR` | Result class |
|:--------:|:--------------:|:-----------:|-------------|:----------:|:-------------:|--------------|
| yes      | any            | any         | any         | any        | any           | **CLEAN**    |
| no       | any            | set         | any         | any        | any           | **CHROME-NC**|
| no       | any            | unset       | `dumb`      | any        | any           | **CHROME-NC**|
| no       | any            | unset       | not dumb    | `0`        | any           | **CHROME-NC**|
| no       | any            | unset       | not dumb    | unset/`1`  | set, not `0`  | **CHROME**   |
| no       | any            | unset       | not dumb    | `1`+force  | any           | **CHROME**   |
| no       | no             | unset       | not dumb    | unset      | unset         | **CLEAN**    |
| no       | yes            | unset       | not dumb    | unset      | unset         | **CHROME**   |

Rule ordering corresponds to the seven-rule list in
`docs/accessibility.md` §1. First match wins, evaluated top-to-bottom.

---

## 3. Verifying from the outside

You do not need to trust this doc — verify it on your system. All
three of the following commands should produce byte-identical stdout
(modulo the actual model response):

```sh
az-ai-v2 --raw "ping"                 > a.txt
az-ai-v2       "ping" > b.txt          # piped → CLEAN
NO_COLOR=1 az-ai-v2 "ping" > c.txt    # NO_COLOR + redirect → CLEAN

diff a.txt b.txt
diff a.txt c.txt
```

If any of those diffs is non-empty, the TTY / color contract is
broken — please file it as a bug with severity = crash.

For the interactive case, a visual check is to pipe through `cat -A`:

```sh
az-ai-v2 --raw "ping" | cat -A   # no ^[[... sequences, ever
```

`cat -A` makes control characters visible. `--raw` output must contain
none (other than `$` end-of-line markers for `\n`).

---

## 4. What about stderr?

Stderr follows the same seven rules independently — it is evaluated
against *stderr*'s TTY-ness, not stdout's. The common case (`cmd |
tee`) leaves stderr at a TTY and therefore CHROME, which is correct:
stderr is the human channel. `--raw` is the only flag that suppresses
stderr chrome unconditionally; use it when you want pipeline silence
on both channels.

---

## 5. What about color for "errors only"?

There isn't a half-off mode. Color is all-or-nothing. This is
deliberate: a partial palette is how colorblind regressions ship —
"error red is still red even with `NO_COLOR`" is the classic bug.
`az-ai-v2` refuses to emit *any* ANSI when any of the off-rules fire.
Errors still scream at you via text (`[ERROR]` prefix, exit code), not
via a lone red glyph.

---

## 6. Cross-links

- [`docs/accessibility.md`](../accessibility.md) — the canonical
  contract.
- [`docs/accessibility/keyboard-workflows.md`](keyboard-workflows.md)
  — pipes, `$EDITOR`, Espanso/AHK.
- [`docs/accessibility/low-bandwidth-ssh.md`](low-bandwidth-ssh.md) —
  why `--raw` is a byte-budget win, not just an a11y win.
- [`azureopenai-cli-v2/Theme.cs`](../../azureopenai-cli-v2/Theme.cs)
  — the seven rules, in code.

---

*If it can't be read aloud, it can't be shipped.* — M.A.
