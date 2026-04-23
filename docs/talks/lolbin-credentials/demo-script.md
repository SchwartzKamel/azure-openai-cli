# Demo Script -- LOLBin Credentials

> Scripted to the keystroke. The live demo is the load-bearing wall of
> this talk; if it falls, the talk falls. Read this twice the night
> before. Read it once more in the green room.

**Total wall-clock estimate:** 8 minutes 30 seconds (target 9:00, hard
cap 10:00). Pad of 30 seconds is the only improvisation budget.

**Demo machine:** A laptop with a working Linux distro (libsecret /
GNOME keyring available) AND a macOS partition or sibling Mac on the
podium. If only one OS is available live, run the second OS from a
pre-recorded asciinema cast (see Fallbacks).

## Setup (60 seconds, before slide "Demo")

What is on screen:

- One terminal window, full-screen, dark background, white text,
  font size 22pt minimum. No tmux split. No IDE. No browser.
- Shell prompt is a single character: `$ `. No path, no git branch,
  no emoji, no two-line prompt. Audience cannot read those.
- Working directory is `~/demo` and is empty. Verify with `ls`.
- The CLI binary `azureopenai-cli` is on `$PATH`. Verify with
  `which azureopenai-cli`. Show this once on slide; do not retype.
- `NO_COLOR=1` is exported if the room projector mangles ANSI. Decide
  in pre-flight (see `stage-notes.md`), not on stage.

The "before" state: no config file exists. Verify with
`ls ~/.azureopenai-cli.json` -- expect "No such file or directory."
Say out loud: "Clean machine. No key anywhere. Watch where it lands."

**Do not improvise:** do not open a second terminal "just to show
something." Two terminals on a projector at 22pt is unreadable. One
terminal, one demo, one beat at a time.

## Beat 1 -- First-run wizard (2:30)

**Type:**

```bash
azureopenai-cli "hello"
```

**What the audience sees:** The wizard prompts for endpoint, API key,
default model. Type fake-but-plausible values. The endpoint is
`https://demo.openai.azure.com`. The key is `sk-demo-not-a-real-key`.
The model is `gpt-4o-mini`.

**Patter while typing (one breath each):**

- "First run. No config. The CLI notices and walks me through it."
- "I am typing a fake key. The real one lives in the OS keystore in
  about ten seconds. You will not see it again after this prompt."

**At the end of the wizard, the CLI confirms** that the key was stored
in the platform vault (libsecret on Linux). Say: "That confirmation
line is the whole talk. The key is gone from this terminal. It is in
the OS vault."

**Fallback if this breaks live:** "The wizard is the same shape on
every platform -- five prompts, then a confirmation. I will show you
the stored result in the next beat, which is what actually matters."
Skip ahead to Beat 2 with a pre-seeded config.

**Do not improvise:** do not paste a real key, even a rotated one.
Even one you rotated this morning. Use the literal string above.

## Beat 2 -- Linux libsecret store (2:30)

**Type:**

```bash
secret-tool search --all service azureopenai-cli
```

**What the audience sees:** One entry, with attributes (`service`,
`account`), and a value field that `secret-tool` will print. The
"value" is the demo key string from Beat 1. Audience sees that the
key is in libsecret, not in `~/.azureopenai-cli.json`.

**Then type:**

```bash
cat ~/.azureopenai-cli.json
```

**What the audience sees:** A short JSON file with endpoint and model
but no `api_key` field. This is the punchline of the Linux beat.

**Patter:**

- "Config on disk. Endpoint, model, no key. The key lives one process
  boundary away, in the vault the OS already manages."
- "When I run the CLI again, it shells out to `secret-tool lookup` and
  the key shows up in this process's memory and nowhere else."

**Fallback if libsecret is broken on the demo box:** Switch to the
file-mode fallback: `azureopenai-cli --auth-mode file "hello"` and
narrate the degraded path. Say: "Same UX, weaker storage, and the CLI
warns you on every invocation. Vault-first, file-fallback."

**Do not improvise:** do not run `secret-tool` against your real
keyring. The demo machine has a scratch keyring; your laptop does not.

## Beat 3 -- macOS `security` CLI store (2:30)

**Switch to the macOS terminal.** This is the slowest moment of the
demo; rehearse the switch.

**Type:**

```bash
security find-generic-password -s azureopenai-cli -a default -w
```

**What the audience sees:** macOS may prompt for the user password to
release the keychain item. That prompt IS the demo. Say: "macOS asks
me before releasing the secret. The CLI cannot lie its way past this
prompt; the operating system is the gate." After unlocking, the demo
key string prints.

**Then type:**

```bash
cat ~/.azureopenai-cli.json
```

Same shape as Linux: endpoint, model, no key.

**Patter:**

- "Same CLI, same config shape, different vault. Linux libsecret on
  Linux, macOS Keychain on macOS, Credential Manager on Windows.
  One binary, three backends, picked at runtime."
- "This is what 'living off the land' means here -- the CLI does not
  carry crypto. It borrows the OS's."

**Fallback if the macOS partition does not boot, the Mac is not on
stage, or the keychain unlock prompt does not appear:** Play the
pre-recorded asciinema cast (`demos/lolbin-mac.cast`, prepared the
morning of). Narrate over it as if live. Do not pretend it is live.

**Do not improvise:** do not type your real macOS account password
into the keychain prompt on stage. The demo account has its own login
and its own password. Use that one.

## Wrap (60 seconds)

Return to slides. The takeaway slide says, in one line: "Your CLI
should not own the secret. The OS already does." Read it. Pause.
Advance to Q&A.

## Wall-clock budget

| Segment | Budget | Cumulative |
|---------|--------|------------|
| Setup    | 1:00 | 1:00 |
| Beat 1   | 2:30 | 3:30 |
| Beat 2   | 2:30 | 6:00 |
| Beat 3   | 2:30 | 8:30 |
| Wrap     | 1:00 | 9:30 |
| Pad      | 0:30 | 10:00 |

If you are past 4:30 at the end of Beat 1, cut the `cat` step in
Beats 2 and 3 and narrate it instead. Do not cut the vault prompt --
that IS the demo.
