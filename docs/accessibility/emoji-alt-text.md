# Emoji alt-text policy

> *If a screen reader can't tell me what that 🚀 means, it's just noise.* -- M.A.

This appendix is the project-wide alt-text and emoji policy. It pairs
with the `emoji-alt-text` todo (scheduled for a later wave) and is the
reference contributors should cite in PR review.

The short version, memorized first:

> **Every emoji must be redundant with adjacent text. Strip the emoji
> and the meaning survives. No emoji-only signals, ever.**

That is the rule. Sections 1-5 spell out what it means in practice.

---

## 1. Where emoji are allowed

| Surface                     | Emoji allowed? | Notes                                       |
|-----------------------------|:--------------:|---------------------------------------------|
| `README.md` prose           | Yes            | Redundant-with-text only.                   |
| `docs/**.md`                | Yes            | Same rule.                                  |
| Release notes / CHANGELOG   | Yes            | Section dividers OK if text label follows.  |
| PR / issue templates        | Yes            | No emoji-only status cells.                 |
| CLI `--help` output         | **No**         | See [`help-text-tone.md`](help-text-tone.md). |
| CLI runtime stdout/stderr   | Limited        | Only where redundant with adjacent text.    |
| Man pages                   | **No**         | `groff` has poor emoji rendering.           |
| Shell completion scripts    | **No**         | Shells render inconsistently.               |
| Log files / JSON output     | **No**         | Breaks `grep`, breaks parsers.              |

---

## 2. Redundant-with-text -- what it looks like

### 2.1 Status signals

**Do:**

```
✅ PASS -- 247 tests green
🟡 CONCERN -- 3 flaky tests, see #412
🔴 FAIL -- build broken on main
```

A screen reader that renders emoji announces "white heavy check mark
PASS" -- the "PASS" carries the load; the emoji is garnish.

**Don't:**

```
| Build | `🟢` |
| Tests | `🟡` |
| Lint  | `🔴` |
```

Status-by-emoji-alone fails three audiences at once: monochrome
terminals see three identical circles, colorblind users cannot
distinguish green from red, TTS announces "large green circle" with
zero semantic cargo. Add `PASS` / `WARN` / `FAIL` text.

### 2.2 Decorative bullets

**Do:**

```markdown
- 🚀 **Fast cold start** -- 5.4 ms on `az-ai --version`.
- 🔒 **Security hardened** -- no outbound calls at startup.
- ⌨️ **Keyboard-only friendly** -- Espanso / AHK / `$EDITOR`.
```

Strip the emoji: the bullets still read as a feature list. That is
the test.

**Don't:**

```markdown
- 🚀 5.4 ms
- 🔒 hardened
- ⌨️ keyboard
```

Terse-as-a-tweet. Strip the emoji and the meaning collapses. A screen
reader user gets "rocket five point four milliseconds" -- is that
speed, memory, latency, cost?

---

## 3. Alt-text rules for images

Every `![alt](path)` image reference must have substantive alt text.
Guidelines, in priority order:

1. **Describe the content and purpose, not the filename.** `alt="GIF"`
   is not alt text; `alt="Animated terminal showing az-ai --version
   returning in 5.4 ms"` is.
2. **Include any text shown in the image.** If the image is a
   screenshot of output, transcribe the key lines.
3. **State the demo claim.** A benchmark screenshot's alt text should
   say what the screenshot is evidence of.
4. **Skip redundant "image of".** Screen readers already announce
   "image"; don't start alt text with `Image of`.
5. **Be under ~125 characters where possible.** For longer
   descriptions, use the image + a caption paragraph + a transcript
   link, not a megabyte of alt text.
6. **Decorative-only images use `alt=""`.** This tells screen readers
   to skip them rather than announcing the filename.

### 3.1 Worked examples

**Bad:**

```markdown
![screenshot](img/fast.png)
```

**Better:**

```markdown
![Benchmark chart: az-ai startup is 5.4 ms, v1 was 1300 ms, a 240× speedup](img/fast.png)
```

**Best (for a hero image):**

```markdown
![Animated terminal recording: `az-ai --version` returns in 5.4 ms,
then a short prompt streams a response in under one second](img/hero.gif)

> Static frame: [img/hero-still.png](img/hero-still.png).
> Commands shown: `az-ai --version`, then `az-ai "capital of France?"`.
```

Three layers of accessibility: descriptive alt text, a static PNG
fallback for bandwidth-constrained or text-only browsers, and a plain
prose transcript for the commands.

---

## 4. Forbidden patterns

- **Emoji-as-section-marker without a text heading.**
  `## 🚀` -- the screen reader announces "rocket"; no section name.
- **Emoji in links.** `[🚀](docs/fast.md)` -- the link name is one
  glyph; TTS has nothing to announce but the codepoint.
- **Emoji in file names.** `docs/🚀fast.md` breaks shell completion,
  breaks some filesystems, breaks grep output.
- **Emoji modifiers / skin tones / ZWJ sequences in runtime output.**
  Complex emoji have terminal rendering bugs; stick to basic BMP
  emoji in docs.
- **Emoji as table-cell state with no legend.** See §2.1 above.
- **Private-use-area glyphs (Nerd Font, Powerline).** These render as
  tofu on default terminals. Not emoji; forbidden everywhere.

---

## 5. Quick self-review checklist

Before you merge a doc PR, ask:

1. [ ] Does every emoji sit next to text that carries the same
       meaning?
2. [ ] If I strip all emoji, does the document still make sense?
3. [ ] Do table cells use emoji-plus-label (`🟢 PASS`), never emoji
       alone?
4. [ ] Are all `![](…)` references descriptive, not filename-echoes?
5. [ ] Any private-use glyphs? (There should be none.)
6. [ ] Is there any emoji in `--help`, man pages, JSON output,
       log output, or completion scripts? (There should be none.)

If every box is ticked: ship it. If not: fix, then ship.

---

## 6. Cross-links

- [`docs/accessibility.md`](../accessibility.md) §1 and §5 --
  higher-level commitments.
- [`docs/accessibility/help-text-tone.md`](help-text-tone.md) -- why
  `--help` is emoji-free.
- [`.github/contracts/color-contract.md`](../../.github/contracts/color-contract.md)
  -- if present, the coded policy.
- [`docs/demos/README.md`](../demos/README.md) -- alt-text rule as
  applied to screencasts and GIFs.

---

*Every emoji pays rent in text. No rent, no room.* -- M.A.
