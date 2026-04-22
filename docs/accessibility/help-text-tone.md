# Help-text tone audit

> *Your help text is a contract. And yes, I read it aloud.* -- M.A.

This appendix records the tone rules applied to every flag description
emitted by `az-ai-v2 --help` (see `Program.cs`, `PrintHelp`). It is
the screen-reader-first style guide.

The audit pairs with the `help-text-tone` finding in
[`docs/audits/docs-audit-2026-04-22-mickey.md`](../audits/docs-audit-2026-04-22-mickey.md).
The current `--help` text was reviewed against these rules on
2026-04-22. Known residuals are listed in §5.

---

## 1. Rules

### 1.1 One verb form, applied consistently

Flag descriptions use the **bare imperative** third-person indicative
("Suppress", "Emit", "Enable", "Cache", "Print"). They do not mix
styles ("Suppresses…" in one line, "Will suppress…" in the next).
Screen readers read the shape of the sentence; consistency lets the
listener anchor on the verb.

**Do:** `--raw    Suppress all non-content output.`
**Don't:** `--raw    Suppresses output (use for scripts).`

### 1.2 No emoji in `--help` output

`--help` is pure text. No ✅, no 🚀, no 📦. Reasons:

- Screen readers announce `✅` as "white heavy check mark" -- five
  syllables for zero information when the semantic is already in the
  surrounding text.
- Terminals without emoji fonts (many CI runners, minimal containers,
  serial consoles) render emoji as `??` or tofu glyphs -- the line
  becomes unreadable where it used to be useful.
- Terminal width math breaks: most emoji are "wide" (W property per
  UAX #11), so column alignment drifts.

Emoji **are** allowed in Markdown docs under the emoji-redundant-with-text
rule (see `.github/contracts/color-contract.md` and
`emoji-alt-text.md` in this directory). They are not allowed in
`--help` output.

### 1.3 Environment variables in parentheses

Where a flag has an env-var equivalent, cite it in the form
`(env: VAR_NAME)` with no other decoration. Screen readers will
announce the parenthesis; that is the cue.

**Do:** `--model, -m <alias|name>  Model deployment or alias (env: AZUREOPENAIMODEL)`

### 1.4 Defaults in parentheses, after env

**Do:** `--timeout <seconds>  Request timeout (env: AZURE_TIMEOUT, default: 120)`

### 1.5 No control characters. Ever.

No tabs in help text -- only spaces. No `\r`. No backspace cursor
tricks. `cat -A az-ai-v2-help.txt` must be clean modulo `$` markers.

### 1.6 Column alignment uses spaces only

Flag column is left-padded to a stable width (currently 26 chars).
All padding is spaces. Lines longer than the terminal wrap naturally;
the help text does not hand-wrap to 80 columns because that assumes a
width it cannot verify. Users on narrow terminals get soft wrap, which
is the right behavior.

### 1.7 Section headers are plain-text

Sections are rendered as `Core Options:`, `Agent / Tools:`,
`Persona / Squad Mode:` -- no Markdown, no underlines, no ANSI. This
keeps `--help` output identical in CHROME and CLEAN classes (see
[`tty-detection.md`](tty-detection.md)).

### 1.8 Examples use real command names and quote styles

The `Examples:` block uses `az-ai-v2 …` exactly, with straight
double-quotes (`"`). No smart quotes, no backtick substitutions, no
`$ ` prompt prefix (which some users' screen readers announce as
"dollar sign space" every line).

### 1.9 Flag synonyms are grouped on one line

`--models, --list-models` -- the primary name first, synonym second,
comma-separated. Screen reader users hear "dash dash models comma dash
dash list dash models" -- terse and unambiguous.

### 1.10 No sales copy

`--help` is reference, not marketing. "Blazing fast" and similar
language do not appear. If a flag needs context, it goes in
`docs/`, not here.

---

## 2. Audit method

For the 2026-04-22 pass:

1. Capture: `az-ai-v2 --help > /tmp/help.txt`.
2. Strip -- verify no ANSI, no emoji:
   `rg -n '\x1b|\p{Emoji_Presentation}' /tmp/help.txt` → empty.
3. Read aloud end-to-end with a screen reader (or `espeak-ng -f
   /tmp/help.txt`).
4. Width check: `awk '{ if (length > 100) print NR": "length }'
   /tmp/help.txt` -- lines over 100 columns are acceptable only inside
   the Configuration block where the precedence note is intentionally
   long.
5. Verb consistency: `rg -n '^\s+--' /tmp/help.txt | awk -F'  +' '{
   print $2 }' | rg -i '^[A-Z][a-z]+s\b' || echo OK` -- flags starting
   their description with a 3rd-person-singular verb are flagged for
   review.

---

## 3. Verbs used in current `--help` (2026-04-22 snapshot)

Inventoried from `Program.cs` `PrintHelp`. The surviving set after
the audit:

`Show`, `Emit`, `Enable`, `Suppress`, `Sampling`, `Request`,
`Max(imum)`, `System`, `Enforce`, `Comma-separated`, `Route`, `List`,
`Initialize`, `Persist`, `Read`, `Use`, `Print`, `Include`, `Fire`,
`Cache(d)`, `Skipped`, `Export`.

`Fire`, `Sampling`, and `Skipped` are the three non-imperative edges
-- justified by the specific flags they describe (`--prewarm` is a
fire-and-forget; `--temperature` describes a noun; `--cache`'s skip
list is a condition). Flagged as acceptable; see §5.

---

## 4. Anti-patterns we actively watch for

- `"Like X, but…"` -- comparative descriptions fail for users new to
  both X and the tool.
- `"(deprecated)"` without a replacement pointer.
- `"Recommended for most users"` -- every user thinks they are in
  "most", so this is noise.
- `"This will…"` / `"This flag will…"` -- subject is always the flag;
  "this" is filler.
- `"Pro tip:"`, `"Note:"`, `"N.B."` -- side-channel content belongs in
  `docs/`, not here.

---

## 5. Known residuals (accepted)

None blocking. The three non-imperative edges in §3 are intentional
and small. Re-run the audit before each release per the checklist in
[`docs/accessibility.md`](../accessibility.md) §Known gaps.

---

*Your error message is 80 characters and there's a tab character in
it. NOT ACCEPTABLE.* -- M.A.
