# Docs Audit — Accessibility Segment

**Date:** 2026-04-22
**Auditor:** Mickey Abbott (accessibility & CLI ergonomics)
**Scope:** `README.md`, `docs/**`, help-text references, image assets
**Target version:** v2.0.4
**Non-goals:** No source edits. Documentation audit only.

> *Us little guys gotta stick together.* This audit is for the screen-reader
> user parsing your Espanso-triggered output, the colorblind sysadmin who
> just pulled the pre-built binary, the CI log-grepper who does not want
> ANSI escape soup, and the keyboard-only power user who is precisely the
> audience `az-ai --raw` was built for.

---

## 1. Executive Summary

- **Findings:** 0 Critical (in the "it crashes for assistive tech" sense) · **2 High** · **5 Medium** · **4 Low** · **3 Informational**
- **Counts of severity include missed-opportunity a11y wins, not just defects.** The binary itself is silent-by-design and already clean (see `docs/accessibility-review-v2.md`). The **gap is in docs**: commitments the code already honors are invisible to users who need them.
- **Top recommendation (highest leverage):** Add a short **"Accessibility"** section to `README.md` (or a top-level `docs/accessibility.md`) that documents `NO_COLOR` support, the `--raw` silent-by-design contract, exit-code discipline, and the Espanso/AHK keyboard-workflow angle. The code already delivers all of it; the docs don't tell anyone. One page unblocks the entire audience.
- **Positive header:** v2's help-text, section ordering, emoji-redundant-with-text policy, `--raw` pipe contract, and `Theme.cs` NO_COLOR precedence are already aligned with the commitments below. This is mostly a *publishing* problem, not an engineering problem.

---

## 2. Prose-vs-Code Verification

Prompt asked: does prose match code for `NO_COLOR`?

- **Code (`azureopenai-cli-v2/Theme.cs:108–142`):** implements the full precedence — `NO_COLOR` (non-empty) beats everything, `TERM=dumb` blocks ANSI, `CLICOLOR=0` beats `FORCE_COLOR`, `FORCE_COLOR`/`CLICOLOR_FORCE` override auto-detect, default off when stdout is redirected. 7-rule precedence, test-seam gated. This is textbook.
- **User-facing prose:** **does not exist.** `grep -rni "NO_COLOR" README.md docs/prerequisites.md docs/espanso-ahk-integration.md docs/use-cases.md docs/cost-optimization.md` returns **zero hits**. Mentions of `NO_COLOR` live only in `docs/accessibility-review-v2.md` (internal review), `docs/launch/v2.0.0-*.md` (contributor thanks), and `docs/proposals/FR-020/FR-022` (future work).
- **Mismatch verdict:** Code is correct; **docs are silent**. Users cannot discover the contract without reading the source. See **M-001**.

---

## 3. Findings

### HIGH

#### H-001 — No user-discoverable `NO_COLOR` / `FORCE_COLOR` / `TERM=dumb` documentation
- **Files:** `README.md` (entire), `docs/prerequisites.md`, `docs/cost-optimization.md`, `docs/espanso-ahk-integration.md`
- **Problem:** The v2 binary honors the full color-contract precedence (see §2), but **no user-facing doc mentions it**. A user running `NO_COLOR=1 az-ai --raw …` inside a CI log pipeline has no way to know the guarantee exists short of reading `Theme.cs`. The only surface-level mention lives in `docs/accessibility-review-v2.md` (internal cutover review) and `docs/launch/v2.0.0-contributor-thanks.md:46` (a credits line).
- **Proposed fix:** Add a row to the `README.md:102` environment table:
  ```
  | `NO_COLOR` | — | *unset* | Set to any non-empty value to suppress ANSI color (https://no-color.org/). Also honored: `TERM=dumb`, `CLICOLOR=0`. Force color when output is piped: `FORCE_COLOR=1` or `CLICOLOR_FORCE=1`. |
  ```
  And/or add a dedicated `docs/accessibility.md` (which `docs/accessibility-review-v2.md:148` already recommends shipping but is still absent).
- **Severity:** High. Code correctness is wasted if users don't know the knob exists.
- **WCAG 2.1 ref:** SC 1.4.1 *Use of Color* (authoring); [no-color.org](https://no-color.org/) informal standard.

#### H-002 — Animated hero GIF (`img/its_alive_too.gif`) lacks text equivalent / static fallback / captions
- **Files:** `README.md:12` — `![It's alive!](img/its_alive_too.gif)`
- **Problem:** The only alt text is the tagline `It's alive!`, which is evocative but **non-descriptive** — a screen reader announces "It's alive!" with no indication that this is a terminal animation demonstrating `az-ai`'s sub-6 ms startup. There is **no static screenshot fallback** for users on bandwidth-constrained connections, `img.unloaded` states, or text-browser (lynx/w3m) readers. The GIF has **no captions** showing what commands were run. `docs/demos/hero-gif.md` documents *how* the GIF is regenerated but not how to produce an accessible substitute.
- **Proposed fix:** (a) Replace alt text with a description: `alt="Animated terminal recording: 'az-ai --version' returns in 5.4 ms, followed by a short prompt that streams a response in under one second."`. (b) Commit a static PNG of the final frame alongside and reference it as a `<picture>`-style fallback, or link "Static screenshot: [img/its_alive_still.png]" below. (c) Add a plain-text transcript of the commands shown.
- **Severity:** High. This is the **first non-badge content** in the README and the only piece a screen reader cannot parse.
- **WCAG 2.1 ref:** SC 1.1.1 *Non-text Content* (Level A), SC 1.2.5 *Audio Description (Prerecorded)* doesn't apply (no audio) but the spirit of SC 1.2.3 does — provide an alternative for time-based media.

### MEDIUM

#### M-001 — `--raw` mode's screen-reader benefit is a major selling point that is never sold
- **Files:** `README.md:33–45` (Execution Modes), `docs/espanso-ahk-integration.md:32–47` ("Why `--raw` Matters")
- **Problem:** The `--raw` section frames the flag as defense against Espanso/AHK **capturing spinner artifacts**. That is correct, but it **buries the lede for assistive tech**: `--raw` emits zero ANSI, zero cursor-hide codes, zero spinner chrome — making it the ideal mode for TTS engines that choke on escape soup and for sysadmins on high-latency SSH (the original "little-guys" audience). `docs/proposals/FR-020-nvidia-nim-provider-per-trigger-routing.md:171` already uses the phrase "screen-reader safe" for `--stream-mode atomic`, so the team knows this angle exists — it just hasn't landed in user-facing docs.
- **Proposed fix:** Add a sentence to `docs/espanso-ahk-integration.md:47`:
  > `--raw` is also the recommended mode for screen-reader users and for any pipeline feeding output to a TTS engine, a braille display, or a low-bandwidth SSH session — it emits no ANSI escapes, no cursor-hide codes, and no spinner glyphs.
- **Severity:** Medium. Missed-opportunity a11y win, not a defect.

#### M-002 — Espanso + AHK keyboard-only workflows are not advertised as an accessibility strength
- **Files:** `README.md:76–94`, `docs/espanso-ahk-integration.md` (entire)
- **Problem:** Espanso and AutoHotkey are **fundamentally keyboard-triggered** — users type `:aifix` in any text field and the CLI runs. This is textbook **keyboard-only workflow** accessibility (WCAG 2.1 SC 2.1.1 *Keyboard*) and a major differentiator vs. mouse-dependent GUI AI plugins. Currently it's framed only as a productivity win.
- **Proposed fix:** Add a bullet to the "Why" list in `README.md:14–20`:
  > - ⌨️ **Keyboard-only friendly** — Espanso / AHK triggers, `$EDITOR`-driven config, no GUI dependency, no mouse affordances anywhere in the tool surface.
  (Keep the ⌨️ emoji **redundant** with the text label, per the project's emoji-with-text-equivalent rule — see §4 positive finding P-2.)
- **Severity:** Medium. Sales miss with a11y flavor.

#### M-003 — No man page; no `--help` text mirrored in docs for linear reading
- **Files:** Repo-wide — no `docs/man/` directory exists. `docs/proposals/FR-020-nvidia-nim-provider-per-trigger-routing.md:256` references `docs/man/az-ai.1` as **future work**. `README.md:43` says "Full flag reference: `az-ai --help`" — that's the only pointer.
- **Problem:** (a) No `man 1 az-ai` for sysadmins who expect `man <tool>` to work. (b) The `--help` text itself is not **copy-pasted into a docs page**, so a screen-reader user who hasn't installed the binary yet (e.g., evaluating the tool from the README) has no way to read the flag reference linearly. Every flag-reference lookup requires executing the binary.
- **Proposed fix:** (a) Track `docs/man/az-ai.1` as its own issue/ADR (already scoped in FR-020 — link it from the audit). (b) As an immediate low-cost fix, commit `docs/cli-help.md` generated by `az-ai --help` (and `--help` for each subcommand form) so it can be searched, linked, and read by a screen reader without running the binary. Regenerate in CI.
- **Severity:** Medium. Expected convention not met; readable-without-execution is a non-obvious but high-value a11y property.
- **Standards ref:** POSIX.1-2017 §12 (Utility Argument Syntax), `man-pages(7)` conventions.

#### M-004 — Exit-code contract not documented in a user-facing single-source-of-truth
- **Files:** None. `README.md`, `docs/prerequisites.md`, `docs/use-cases.md` — silent on exit codes. Scattered commentary in `docs/launch/*`, `docs/proposals/FR-020:244`, and audit files.
- **Problem:** Exit codes are **the scripting contract** — Espanso, AHK, CI gates, shell conditionals all depend on meaningful non-zero codes. The release-notes prose says "every v1 exit code is unchanged" but never says **what those codes are**. `docs/accessibility-review-v2.md:65` notes SIGINT yields exit 130 (POSIX-standard), but a user writing `if az-ai ... ; then` has no table to consult. FDR's audit (`docs/audits/fdr-v2-dogfood-2026-04-22.md`) and FR-020 both reference sentinel codes (42–45 in FR-020) without a central table.
- **Proposed fix:** Add an "Exit Codes" section to `docs/prerequisites.md` (or a new `docs/exit-codes.md`) listing each documented code, its meaning, and whether it's stable across minor releases. Cross-link from `README.md#espanso--autohotkey`.
- **Severity:** Medium. Affects scripting reliability, which is the audience.

#### M-005 — Status-glyph use in audit docs (🟢🟡🔴) is color-by-shape but not by text
- **Files:** `docs/accessibility-review-v2.md:30–68` (status column), `docs/audits/fdr-v2-dogfood-2026-04-22.md` (if it follows same pattern)
- **Problem:** The accessibility review itself uses colored circle emoji as its primary status indicator. Colorblind users get a dark circle regardless of hue; the semantic load is on emoji color alone in some rows where the prose follow-up doesn't re-state "PASS / CONCERN / FAIL". Most rows *do* include redundant prose, so this is not a blocking defect, but the audit-of-the-auditor is worth making consistent.
- **Proposed fix:** Where 🟢/🟡/🔴 appears as a status cell, append the text label: `🟢 PASS`, `🟡 CONCERN`, `🔴 FAIL`. Same pattern audit docs already follow in their "Verdict §N" summary lines.
- **Severity:** Medium (within docs-audit scope — the doc claiming a11y compliance must itself model the practice).
- **WCAG 2.1 ref:** SC 1.4.1 *Use of Color* (Level A).

### LOW

#### L-001 — README "Why" list uses emoji as bullet-prefix decoration
- **Files:** `README.md:16–20`
- **Problem:** 🚀 🧰 🔒 🖥️ 🧪 each precede a **redundant text label** (`cold start`, `execution modes`, `Security hardened`, `Cross-platform`, `tests`). A screen reader will announce "rocket 5.4 ms cold start" etc. — the emoji adds nothing harmful, and the meaning survives when emoji is stripped. This is **already within policy** (`docs/accessibility-review-v2.md` §2.1: emoji must be redundant with text). Logging as Low only because consistency with the "⌨️ keyboard-friendly" bullet proposed in M-002 should be enforced — any new bullet added must follow the redundancy rule.
- **Proposed fix:** No change required; document the rule explicitly in `CONTRIBUTING.md` or a `.github/contracts/color-contract.md`-style doc (FR-022 references the latter). Freeze the policy so it doesn't regress in v2.1.
- **Severity:** Low.

#### L-002 — `README.md:104–106` "Required" column uses ✅ only
- **Files:** `README.md:102–111` (env-var table)
- **Problem:** The `Required` column shows `✅` for required rows and nothing (em-dash or blank) for optional rows. Screen readers typically announce `✅` as "white heavy check mark" — the meaning survives (a checkmark in a required column is conventional), but the mapping is inferential. A screen reader user hears "row: AZUREOPENAIENDPOINT, white heavy check mark, dash, Azure OpenAI resource endpoint" — functional but not great.
- **Proposed fix:** Replace `✅` with the word `yes` (or leave ✅ and add a table caption clarifying the column legend). Consistency with the emoji-redundancy rule argues for `✅ yes`.
- **Severity:** Low.

#### L-003 — Nerd Font / Powerline glyphs — not used, but not *ruled out* for future contributors
- **Files:** Repo-wide (no Nerd Font references found in docs)
- **Problem:** The project currently uses only standard Unicode emoji, which render on modern terminals without special fonts. This is **good**, but there is no written policy forbidding Nerd Font / Powerline glyphs (private-use-area codepoints with `` / `` / `` etc.) in future demos or docs. A future contributor adding a flashy `docs/demos/` screen could inadvertently require a font most users don't have.
- **Proposed fix:** Add a one-liner to `docs/demos/README.md` (or a future `docs/accessibility.md`): "No private-use-area glyphs (Nerd Font, Powerline) in screencasts or docs — only standard Unicode. If a character does not render in `xterm -fa Monospace`, it does not ship."
- **Severity:** Low (preventive).

#### L-004 — Terminal width assumptions not stated; wide tables may reflow poorly at 80 cols
- **Files:** `README.md:35–41` (Execution Modes, 3-column), `README.md:102–111` (env-var table, 4-column), `docs/espanso-ahk-integration.md:38–44` (`--raw` comparison, 3-column)
- **Problem:** GitHub's Markdown rendering handles reflow, but users reading via `cat README.md`, `glow README.md` on a narrow TTY, or `lynx` get column overflow. No doc states a width target (80 / 100 / 120 cols). The CLI itself has no stated width adaptation contract.
- **Proposed fix:** Add a line to `CONTRIBUTING.md`: "Markdown tables and fenced code blocks target 100-column terminals; long example lines should wrap or use continuation backslashes at 100." Separately, document the CLI's behavior when `COLUMNS < 80` (future work — scoped to `docs/accessibility.md`).
- **Severity:** Low.

### INFORMATIONAL

#### I-001 — Locale / keyboard-layout assumptions in AHK and Espanso examples
- **Files:** `docs/espanso-ahk-integration.md` (entire)
- **Note:** Trigger strings like `:aifix`, `:grammar`, `:email` are ASCII and layout-independent. AHK hotkeys in examples use `^+!j` (Ctrl+Shift+Alt+J) — on non-US-QWERTY layouts, `j` is still `j` in AHK's scan-code mode, but some layouts reposition it. The docs don't discuss this. Flagging as informational because Espanso triggers (the primary path) are layout-safe; only the AHK power-user corner has potential friction.
- **No action required** unless a user reports a layout issue. Consider a one-line note in the AHK section if it ever becomes FAQ.

#### I-002 — Badges in README have implicit alt text
- **Files:** `README.md:5–10`
- **Note:** The CI / Release / License / .NET / Platforms / GHCR badges use image syntax `[![CI](...)](...)` — the `CI`, `Release`, `License: MIT`, `.NET`, `Platforms`, `GHCR` strings serve as alt text. Screen readers get "link, image, CI" etc. — meaning preserved. This is fine.
- **No action required.**

#### I-003 — `docs/demos/README.md:88` already enforces alt-text discipline for rendered demos
- **Files:** `docs/demos/README.md:88`
- **Note:** Quoted: *"Keep the `alt` text descriptive — screen readers and broken-image states both benefit."* Good. This policy should be **hoisted** to the top-level `docs/accessibility.md` once it exists, so it applies to any future screenshot anywhere in docs — not only demo artifacts. See also P-4 below.
- **No action required for this audit**; consume into the future `docs/accessibility.md` consolidation.

---

## 4. Positive Findings (credit where due)

- **P-1 — Silent-by-design baseline.** The v2 binary has no spinner, no cursor-hide escapes, no persistent TUI. `docs/accessibility-review-v2.md:34` documents this and it is the single biggest reason a11y compliance is "trivially clean" out of the box. Preserve this property religiously.
- **P-2 — Emoji-with-text-equivalent rule is already honored.** Every emoji in user-facing output (`🎭 Persona: {Name}`, `✅ PASSED`, `❌ FAILED`, `✓ Squad initialized`) is **redundant with adjacent text**. Strip the emoji and meaning survives. Documented in `docs/accessibility-review-v2.md:47`.
- **P-3 — `--raw` mode is a byte-clean stable contract.** `docs/espanso-ahk-integration.md:32–47` and `docs/launch/v2.0.0-announcement.md:9` both commit to byte-identical `--raw` output across versions. This is exactly the scriptability + assistive-tech property Mickey cares about.
- **P-4 — `docs/demos/README.md:88` already requires descriptive alt text.** Policy exists, just needs to be generalized beyond `docs/demos/`.
- **P-5 — Quickstart is keyboard-only.** `README.md:22–29` uses `$EDITOR` (honors user preference — no hardcoded `vim`/`nano`), `make` targets, and shell pipes. No "click here" affordances.
- **P-6 — `Theme.cs` 7-rule color precedence is exhaustive and test-seamed.** The code models `NO_COLOR` / `FORCE_COLOR` / `CLICOLOR` / `TERM=dumb` / TTY-detection correctly (`azureopenai-cli-v2/Theme.cs:108–142`). This is a textbook implementation; docs just need to catch up (H-001).
- **P-7 — Internal a11y review already exists.** `docs/accessibility-review-v2.md` is a solid cutover-gate audit. It is **internal / review-flavored**, not user-facing — but it's a strong foundation for the `docs/accessibility.md` user-facing doc recommended below.

---

## 5. Top Recommendation

**Ship `docs/accessibility.md` (user-facing) with five sections:**

1. **Color contract** — `NO_COLOR`, `FORCE_COLOR`, `CLICOLOR[_FORCE]`, `TERM=dumb`, TTY-detect behavior. Point at `azureopenai-cli-v2/Theme.cs`.
2. **`--raw` contract** — byte-clean, no ANSI, no spinner, stable across minor releases. Call out assistive-tech / SSH / low-bandwidth angle explicitly.
3. **Exit-code table** — documented, scriptable, stable (see M-004).
4. **Keyboard-only workflows** — Espanso, AHK, `$EDITOR`, no mouse affordances (see M-002).
5. **Known gaps & roadmap** — man page (M-003), terminal-width behavior (L-004), future `--plain`/`--ascii` if emoji ever stops being redundant (`accessibility-review-v2.md:54`).

This single page turns implemented-but-invisible a11y work into advertised commitments, unblocks H-001 / M-001 / M-002 / M-003 / M-004 simultaneously, and gives future PRs a contract to regress-test against.

---

## 6. Methodology & Caveats

- Source inspection only; no screen reader (Orca / VoiceOver / NVDA) was run against rendered output.
- GitHub.com rendering of README tables and emoji was not verified in a real browser during this audit — conclusions about table reflow (L-004) assume plain-text `cat`/`lynx` consumers.
- `docs/man/` does not exist; `find . -name "*.1"` returns only git refs. Man-page gap (M-003) is confirmed.
- Binary was not executed for this audit; help-text not captured. M-003 fix would close that loop.
- Cross-reference with `docs/accessibility-review-v2.md` (2026-cutover-era) preserved — findings here are **doc-layer**, not code-layer. The two documents are complementary.

---

*Us little guys gotta stick together.* — M.A.
