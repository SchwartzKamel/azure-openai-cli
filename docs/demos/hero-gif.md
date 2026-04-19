# Regenerating the Hero GIF (`img/its_alive_too.gif`)

> It was the summer we shipped. The README needed a heartbeat — one looping
> rectangle of motion at the top of the page that said, in under three
> seconds, *this works, and it works fast*. That rectangle is the hero GIF.

The asset lives at [`img/its_alive_too.gif`](../../img/its_alive_too.gif) and is referenced from the main `README.md`. To regenerate it — for a refreshed color theme, a new tagline prompt, or a faster binary — use the standard-prompt demo script as the source:

```bash
# 1. Record (narrow frame reads better on GitHub's mobile view)
asciinema rec docs/demos/recordings/hero.cast \
  --cols 88 --rows 18 \
  --title "az-ai — it's alive" \
  --command "bash docs/demos/scripts/01-standard-prompt.sh"

# 2. Render to GIF at a slight speed-up so the loop feels snappy
agg docs/demos/recordings/hero.cast \
    img/its_alive_too.gif \
    --font-size 18 \
    --theme monokai \
    --speed 1.25

# 3. Verify size — keep under ~1.5 MB so GitHub inlines it without lazy-loading
ls -lh img/its_alive_too.gif
```

## Notes from the field

- **Do not rename the output file.** The filename is linked from blog posts, release notes, and social cards outside this repo. A new look replaces the bytes, not the URL.
- **Re-record on release cadence, not commit cadence.** The GIF is a marketing asset; churn degrades external link caches and feels like thrash.
- **Color theme should match the README's mood.** `monokai` reads warm and confident; `github-dark` matches the page chrome; `dracula` pops on Twitter/X cards.
- **Frame budget.** Aim for 3–6 seconds. Anything longer and the reader scrolls past before the punchline lands.
- **Audit before commit.** Open the GIF, scrub through frame-by-frame. No API keys. No hostnames. No stray shell history. Peterman would never ship a typo, and neither do we.

## See also

- [`README.md`](README.md) — full recording workflow for all demos.
- [`scripts/01-standard-prompt.sh`](scripts/01-standard-prompt.sh) — the source script used above.
