# `docs/talks/` -- Speaker Packages

Conference and meetup talk packages for `azure-openai-cli`. Each talk
gets a directory (or, for older shorter packets, a single file). A
package is everything a speaker needs to submit a CFP and walk on
stage: abstract, bio, demo script, slide outline, stage notes.

Owner: Keith Hernandez (DevRel & Conference Speaking), with guest
turns from J. Peterman (catalog-grade copy) and Elaine Benes (slide
text consistency, no jargon undefined on first use).

## Talks in this directory

| Talk | Status | Format | Files |
|------|--------|--------|-------|
| WSL + Espanso | CFP-ready | 15 min lightning + Q&A | [`wsl-espanso.md`](wsl-espanso.md) |
| Living Off the Land: Per-OS Credential Storage in a Single-Binary CLI | CFP-ready (package complete; not yet submitted) | 25-30 min + Q&A | [`lolbin-credentials/`](lolbin-credentials/) |

## Package shape (for new talks)

A directory under `docs/talks/<slug>/` containing:

- `abstract.md` -- long form (~150 words) and short form (~50 words).
- `speaker-bio.md` -- long form (~80 words) and short form (~25 words).
- `demo-script.md` -- scripted to the keystroke, with fallback lines.
- `slide-outline.md` -- per-slide outline with time budget that sums.
- `stage-notes.md` -- pre-flight checklist and known failure modes.

For shorter lightning talks, a single combined file (like
`wsl-espanso.md`) is acceptable. Decide based on whether the demo has
fewer than three beats; if it does, single file is fine.

## Reserved slots (future talks)

Open ideas in priority order. Claim one by opening a PR with the
package directory.

- "The First-Run Wizard: UX for the First Sixty Seconds of Your CLI"
- "AOT Cold Start Budgets: How Small Is Small Enough?"
- "Personas in a CLI: The Squad Pattern for Stateless Tools"
- "Subagents Without Tears: Recursion Caps and Delegation Limits"

## Conventions

- ASCII only. No smart quotes, em-dashes, or en-dashes. Use `--` for
  em-dash, `-` for en-dash, straight quotes throughout.
- Fenced code blocks must declare a language tag.
- Lists have blank lines around them.
- Speaker bios are project-anonymous. Use `<Speaker Name>` and
  `<contact handle>` placeholders so any cast member can give the
  talk.

## What this directory is not

- Not a slide deck repository. Slide visuals (SVG, PDF, PPTX) live
  with the speaker, not in this repo. Outlines only.
- Not a demo video archive. Recorded demos live wherever the
  conference puts them.
- Not a blog post archive. Marketing copy lives with J. Peterman's
  catalog deliverables, cross-linked from the abstract when relevant.
