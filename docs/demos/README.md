# `docs/demos/` — Asciinema Scripts & Recording Guide

> Picture it: a terminal, a curious newcomer, a GIF that loads in under a second and tells the whole story before the scroll wheel turns. That is what this folder is for.

This directory holds **playable, rehearsed terminal demos** for `az-ai-v2`. Each script under [`scripts/`](scripts/) is a self-contained bash file that types its own commands at a watchable pace, runs the real binary, and shows real output. Pair it with `asciinema` to record, and with `agg` (or `svg-term`) to export to GIF/SVG for the README and the site.

## Contents

| File | Purpose |
|------|---------|
| [`scripts/01-standard-prompt.sh`](scripts/01-standard-prompt.sh) | Default mode — streaming answer with spinner. The hero shot. |
| [`scripts/02-raw-espanso.sh`](scripts/02-raw-espanso.sh) | `--raw` mode piped into a simulated Espanso expansion. |
| [`scripts/03-agent-tool-calling.sh`](scripts/03-agent-tool-calling.sh) | `--agent` mode with a real `shell_exec` tool call. |
| [`hero-gif.md`](hero-gif.md) | Short-form reference for the top-of-README hero GIF. |
| [`hero-gif-recording.md`](hero-gif-recording.md) | **Full operator runbook** — locked parameters (88×18, JetBrains Mono 18, monokai, 1.25×), asciinema + agg pipeline, VHS alternative, `make demo-hero-gif`. Baseline v2.0.6. |
| `recordings/` | Raw `.cast` files from `asciinema`. Regenerable; small enough to commit. |
| `images/` | Rendered `.gif` / `.svg` outputs consumed by docs. |

## Prerequisites

```bash
# asciinema — records the terminal session
sudo apt install asciinema        # Debian/Ubuntu
brew install asciinema            # macOS
pipx install asciinema            # anywhere Python is available

# agg — renders .cast → animated .gif (recommended)
cargo install --git https://github.com/asciinema/agg

# svg-term-cli — alternative .cast → animated .svg (lighter, sharper on Retina)
npm install -g svg-term-cli
```

You also need a working `az-ai-v2` on `PATH` (install from a v2 release archive, `brew install schwartzkamel/tap/az-ai-v2`, or `scoop install schwartzkamel/az-ai-v2`) and a populated `.env` with valid Azure OpenAI credentials. Scripts 01 and 03 hit the live API; script 02 can be demoed offline by stubbing the pipe (see its inline comments).

## Recording workflow

All paths below are relative to the repository root.

### 1. Record a cast

```bash
asciinema rec docs/demos/recordings/01-standard.cast \
  --cols 100 --rows 24 \
  --title "az-ai-v2 — standard prompt" \
  --command "bash docs/demos/scripts/01-standard-prompt.sh"
```

- `--cols 100 --rows 24` keeps the frame small enough to embed without horizontal scroll on GitHub.
- `--command` makes the recording stop automatically when the script exits. No stray `exit` keystrokes at the end.
- Re-record until it is clean. The scripts are deterministic on the bash side — any wobble will be network latency from the API.

### 2. Render to GIF

```bash
agg docs/demos/recordings/01-standard.cast \
    docs/demos/images/01-standard.gif \
    --font-size 16 \
    --theme monokai \
    --speed 1.0
```

Tweakables:

| Flag | When to change it |
|------|-------------------|
| `--speed 1.25` | If the recording feels sluggish on playback. |
| `--theme` | `monokai`, `solarized-dark`, `dracula`, `github-dark`. Match the README tone. |
| `--font-family "JetBrains Mono"` | Only if the renderer has the font installed locally. |

### 3. (Optional) Render to SVG

```bash
svg-term --in docs/demos/recordings/01-standard.cast \
         --out docs/demos/images/01-standard.svg \
         --window --no-cursor
```

SVG is sharper and smaller but does not autoplay in GitHub Markdown. Use it on the project site, not in `README.md`.

### 4. Embed in docs

In markdown:

```markdown
![Standard prompt demo](docs/demos/images/01-standard.gif)
```

Keep the `alt` text descriptive — screen readers and broken-image states both benefit.

## Naming conventions

- `NN-kebab-slug.{sh,cast,gif,svg}` — two-digit prefix defines display order in the docs.
- Prefix `01-` is the hero; reserve it for the most universally useful demo.
- Never rename a published asset. Add a new one and update links — the old URL lives on in stars, blogs, and social cards.

## Re-recording checklist

Before you hit `Ctrl-D` to end a cast:

- [ ] Terminal cleared (`clear`) immediately before `asciinema rec` — no leaking previous output.
- [ ] Prompt is plain (`PS1='$ '`), no branch names, no hostnames, no emoji clutter.
- [ ] `.env` loaded, `az-ai-v2 --version` works, network is up.
- [ ] No secrets in scrollback — `history -c` before recording if in doubt.
- [ ] Window resized to match `--cols/--rows`. Mismatched sizes cause wrapping artifacts in the GIF.

See [`hero-gif.md`](hero-gif.md) for the short-form incantation behind `img/its_alive_too.gif`, and [`hero-gif-recording.md`](hero-gif-recording.md) for the full operator runbook (locked parameters, VHS alternative, `make demo-hero-gif` target).

## See also — DevRel context

These scripts are the raw material; the surfaces that consume them live one directory up.

- [`../speaker-bureau.md`](../speaker-bureau.md) — bios, COI disclosure, and the three talk abstracts these demos pair with.
- [`../devrel/livestream-checklist.md`](../devrel/livestream-checklist.md) — pre-show environment setup (terminal 120×40, theme, env-var sanity), live risk-reduce rules, and the asciinema fallback to reach for when the WiFi dies.
- [`../devrel/swag-brief.md`](../devrel/swag-brief.md) — vendor-ready brand constraints for the booth-in-a-box that ships next to these demos.
- [`../announce/`](../announce/) — stable-URL announcement copy. Cite *these* from a blog post, not a v1-era archive.

— *Keith Hernandez. The demo is the asset. The checklist is the seatbelt.*
