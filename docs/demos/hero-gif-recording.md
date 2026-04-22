# Hero GIF Recording Runbook — `img/its_alive_too.gif` (v2.0.6 baseline)

> I'm Keith Hernandez. This is the runbook for the asset that sits at the top of
> `README.md` — the three-second loop that says *this works, and it works fast*
> before a visitor's scroll wheel turns. The script is rehearsed. The theme is
> chosen. The dimensions are locked. All you have to do is run it on a real
> terminal and commit the bytes.

**Asset:** [`img/its_alive_too.gif`](../../img/its_alive_too.gif)
**Source script:** [`scripts/01-standard-prompt.sh`](scripts/01-standard-prompt.sh) (v2-compliant after `87e37b2`)
**Pipeline:** `asciinema rec` → `agg` → `img/its_alive_too.gif`
**Owner:** Keith Hernandez (DevRel)
**Baseline:** v2.0.6 (`7eba772`)

This document exists because the sandbox that opens this repo is frequently
headless: no X server, no GPU, no real terminal the way a human has one. A
GIF rendered out of `agg` looks identical whether it ran in CI or on an
operator's laptop — but only if the inputs are identical. That's what this
runbook locks down.

---

## 1. Prerequisites (operator checklist)

Run this on a machine that has:

- [ ] A real terminal emulator (WezTerm, Alacritty, Kitty, iTerm2, Windows
      Terminal). **Not** a headless shell, not a tmux-in-CI scratch buffer.
- [ ] `asciinema` ≥ 2.4 on `PATH`.
- [ ] `agg` (asciinema gif generator) ≥ 1.4.3 on `PATH`.
- [ ] **JetBrains Mono** (Regular, 18px) installed system-wide. `agg` reads
      the font by family name from the platform font manager.
- [ ] A populated `.env` with working Azure OpenAI creds; `az-ai-v2
      --version --short` prints `2.0.6`.
- [ ] Network link capable of ≤300 ms Azure round-trip (anything slower
      makes the streaming look janky at `--speed 1.25`).
- [ ] `git` clean — no uncommitted changes that could leak into a
      screenshot accidentally.

### Tool install

```bash
# asciinema
sudo apt install asciinema          # Debian / Ubuntu / WSL Ubuntu
brew install asciinema              # macOS
pipx install asciinema              # anywhere Python is available

# agg (recommended renderer — fastest, cleanest output)
cargo install --git https://github.com/asciinema/agg

# Font
#   Linux:    sudo apt install fonts-jetbrains-mono   (or install from JetBrains)
#   macOS:    brew install --cask font-jetbrains-mono
#   Windows:  choco install jetbrainsmono             (or winget)
```

Verify:

```bash
asciinema --version          # expect 2.4.0 or newer
agg --version                # expect 1.4.3 or newer
fc-list | grep -i "JetBrains Mono"   # Linux/WSL — must print at least one line
az-ai-v2 --version --short   # must print 2.0.6 exactly
```

---

## 2. Locked recording parameters

These are **not** tunables. Changing them invalidates the asset's visual
continuity with blog posts, release notes, and social cards that already
embed the URL. If you want a new look, cut a new filename and update
`NOTICE-assets.md`; do not mutate these values in place.

| Parameter           | Value                                             |
|---------------------|---------------------------------------------------|
| Terminal size       | **88 columns × 18 rows** (narrow — reads on mobile) |
| Font family         | JetBrains Mono                                    |
| Font size (agg)     | 18                                                |
| Theme (agg)         | `monokai`                                         |
| Playback speed      | `1.25×`                                           |
| Prompt string       | `PS1='$ '` (plain dollar + space, no PS2 color)   |
| Shell               | `bash` (explicit — do not rely on operator login) |
| Source script       | `docs/demos/scripts/01-standard-prompt.sh`        |
| Frame budget        | 3–6 seconds final, after `--speed 1.25`           |
| Size ceiling        | **≤ 1.5 MB** (GitHub inlines without lazy-load)   |
| Output file         | `img/its_alive_too.gif` (do not rename)           |
| Intermediate `.cast`| `docs/demos/recordings/hero.cast` (committable)   |

---

## 3. Recording procedure

All paths below are **relative to the repo root**. Work on a clean checkout of
`main` at the tagged baseline (`git checkout v2.0.6` if you want a reproducible
retake against the shipped numbers).

### 3.1 Prepare a clean shell

```bash
# Open a fresh terminal window, size it to 88×18 exactly.
# In WezTerm/Alacritty/Kitty: use the config — do not eyeball.
# In iTerm2: Profiles → Window → Columns 88, Rows 18.

cd /path/to/azure-openai-cli
exec bash --noprofile --norc   # no aliases, no RC surprises
export PS1='$ '
export HISTFILE=/dev/null       # no scrollback ghosts in replays
history -c
clear
az-ai-v2 --version --short      # sanity — must print 2.0.6
clear
```

### 3.2 Record

```bash
asciinema rec docs/demos/recordings/hero.cast \
  --overwrite \
  --cols 88 --rows 18 \
  --title "az-ai-v2 — it's alive (v2.0.6)" \
  --command "bash docs/demos/scripts/01-standard-prompt.sh"
```

Notes:

- `--command` makes the recording end cleanly when the script's last `sleep`
  expires. No stray keystrokes, no `exit`.
- If the network stutters and the streaming looks janky, delete the cast and
  re-run. **Do not** edit the cast file by hand — it is a JSONL of timing
  events and human edits always show in the final GIF as a timing seam.

### 3.3 Render to GIF

```bash
agg docs/demos/recordings/hero.cast \
    img/its_alive_too.gif \
    --font-family "JetBrains Mono" \
    --font-size 18 \
    --theme monokai \
    --speed 1.25
```

### 3.4 Verify

```bash
ls -lh img/its_alive_too.gif            # must be ≤ 1.5M
file img/its_alive_too.gif              # must say "GIF image data, version 89a"
# Optional: open it in an image viewer and scrub through.
```

Checklist before commit:

- [ ] File size ≤ 1.5 MB.
- [ ] No API keys, no Azure endpoint hostnames, no personal `$HOME` paths
      visible in any frame.
- [ ] No shell history bleed (`history -c` done before record).
- [ ] Final frame is a completed response, not mid-token.
- [ ] Loops cleanly — no jarring cut back to `$` after three seconds of
      empty buffer.

If any checklist item fails: redo §3.1–§3.3. Do not ship a partial-pass
hero GIF — it is the first thing a new visitor sees.

---

## 4. Alternative renderer: VHS (charmbracelet)

`agg` is the primary pipeline. VHS is a viable alternative — it scripts the
terminal session declaratively instead of recording a live one, which helps
when the network is flaky and you want deterministic timing.

Install:

```bash
# macOS
brew install vhs

# Linux (Go ≥ 1.21 required)
go install github.com/charmbracelet/vhs@latest
```

VHS tape template (save as `docs/demos/hero.tape`, do **not** commit unless
the agg pipeline is retired):

```text
Output img/its_alive_too.gif
Set FontFamily "JetBrains Mono"
Set FontSize 18
Set Width 1056      # 88 cols × 12px
Set Height 360      # 18 rows × 20px
Set Theme "Monokai Pro"
Set PlaybackSpeed 1.25
Set TypingSpeed 20ms
Hide
Type "clear"  Enter
Show
Type "bash docs/demos/scripts/01-standard-prompt.sh"  Enter
Sleep 6s
```

Render:

```bash
vhs docs/demos/hero.tape
```

**If you ship VHS-rendered bytes:** update `NOTICE-assets.md` to reflect the
new renderer in the provenance line. License does not change.

---

## 5. Commit the asset

```bash
git add img/its_alive_too.gif docs/demos/recordings/hero.cast
git commit -m "chore(assets): refresh hero GIF (v2.0.6 baseline)"
```

Do **not** bundle the GIF refresh into a feature PR. It is a marketing
asset; it deserves its own commit with a clean diff so reviewers can
approve the bytes without unrelated code in frame.

---

## 6. Make target

A `make demo-hero-gif` target is wired for operators with the toolchain
installed. See the repo `Makefile`. The target:

1. Checks that `asciinema` and `agg` are on `PATH`.
2. If either is missing, prints the install instructions from §1 and exits
   with a non-zero status — it will **not** silently fall back to a stub.
3. Runs the record → render pipeline in §3.2–§3.3 with the locked
   parameters from §2.

This is a convenience wrapper. The canonical procedure is the checklist
above; the Make target is a safety net for the operator who is running this
five minutes before a talk and just wants the one command.

---

## 7. Re-recording cadence

- **On release:** re-record on every minor version bump (v2.1, v2.2, …).
  Patch bumps (v2.0.6 → v2.0.7) do **not** warrant a re-record unless the
  streaming shape changes.
- **On theme refresh:** if `README.md` changes its visual voice, coordinate
  with Russell Dalrymple before re-recording. The hero GIF is load-bearing
  for brand coherence.
- **Never on commit cadence:** churn degrades external link caches and
  feels like thrash.

---

## 8. Troubleshooting

| Symptom                                    | Likely cause / fix                                       |
|--------------------------------------------|----------------------------------------------------------|
| GIF is blurry / pixelated                  | Wrong font — agg silently falls back. `fc-list | grep JetBrains` must print. |
| Final GIF > 1.5 MB                         | Recording too long. Script is 3–6 s; if your recording is 20 s, the prompt hung — check network. |
| Streaming looks jumpy                      | Azure round-trip > 500 ms. Record on a better link or cache the prompt result upstream (script-side stubbing). |
| `agg` errors on `--font-family`            | Old agg (< 1.4.3). Upgrade: `cargo install --git https://github.com/asciinema/agg --force`. |
| Colors look washed out                     | `--theme monokai` not applied — confirm the flag spelled correctly. |
| Prompt shows `bash-5.2$` instead of `$`   | `PS1` not exported before `asciinema rec`. Redo §3.1.    |
| Hostname visible in a frame                | Your PS1 carried it in. Redo §3.1 — the `exec bash --noprofile --norc` step strips it. |

---

## 9. See also

- [`README.md`](README.md) — the full demo directory index.
- [`hero-gif.md`](hero-gif.md) — the short-form reference; this file is the
  long-form runbook.
- [`scripts/01-standard-prompt.sh`](scripts/01-standard-prompt.sh) — the
  source-of-truth demo script (v2-compliant since `87e37b2`).
- [`../devrel/livestream-checklist.md`](../devrel/livestream-checklist.md) —
  terminal hygiene rules that transfer 1:1 to hero-GIF recording.
- [`../../NOTICE-assets.md`](../../NOTICE-assets.md) — provenance, license,
  and attribution for `img/its_alive_too.gif`.

— *Keith Hernandez. Rehearse on a fresh machine. Ship clean bytes. Tip well.*
