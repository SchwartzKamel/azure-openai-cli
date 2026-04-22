# CJK & wide-character notes

*Sub-document of [docs/i18n.md](../i18n.md) §3. Maintained by Babu Bhatt.
Closes the CJK slice of audit findings L-04 and M-04.*

Chinese, Japanese, and Korean (CJK) text works through the CLI at the
byte level. Rendering it **in a column-aligned way** does not, and that
is a deliberate non-commitment. This file explains where the seams are.

---

## 1. What works

- CJK in prompts: `az-ai-v2 "日本語で短く返して。"`
- CJK in filesystem paths: `cd ~/文档 && az-ai-v2 ...`
- CJK in model output, including `--json` (Unicode is preserved; JSON
  never needs `\uXXXX` escaping for valid UTF-8 strings, but both forms
  round-trip).
- CJK in config files (`.az-ai-config`, `.squad.json`) -- persona names,
  system prompts, memory entries.
- CJK in persona names as far as filesystem hygiene goes -- see
  `docs/security-review-v2.md:285` for persona-name normalisation rules
  (Newman's territory).

---

## 2. What doesn't (and isn't promised to)

### 2.1 Terminal-width math

CJK ideographs are **East Asian Width: Wide** -- they occupy two
terminal cells for every one code point. The CLI does not perform
width-aware column math anywhere in its output. Implications:

- Table-drawing in help/status is done in ASCII only; no CJK in our
  own headers or column labels.
- When model output is embedded in a table-like presentation downstream
  (not by us), the consumer is responsible for width calculation.
- `az-ai-v2 --help | column -t` with CJK in any example output would
  misalign. We do not emit CJK in help; this is a deliberate
  [ASCII-safety contract](../i18n.md#10-ascii-safety-contract-for-cli-own-output).

### 2.2 Grapheme-cluster truncation

If we ever add a "truncate to N characters" feature (none today), it
**must** truncate on grapheme-cluster boundaries -- not on code-point
boundaries and certainly not on UTF-8 byte boundaries. The latter two
produce:

- Mojibake when a byte-slice splits a multi-byte sequence.
- "Half-emoji" bugs when a ZWJ sequence is split (see §3).
- Orphaned combining marks when a base character is split from its mark
  (see [test-corpus.md](test-corpus.md) §3).

This is a forward-looking note. Today we do not truncate display text;
we emit model bytes and let the terminal wrap.

### 2.3 Full-width vs half-width form confusion

Full-width ASCII (`ＡＢＣ`, `Ａ` = U+FF21) is visually similar to ASCII
but is a **distinct codepoint**. Users who paste full-width characters
into what is supposed to be an ASCII flag value (`--temperature`
`０．７` rather than `0.7`) will hit a parse failure. This is correct
behaviour; we do not silently NFKC-normalise user input because
normalisation of sensitive fields is a security-review concern
(coordinate with Newman).

---

## 3. Emoji, ZWJ sequences, and variation selectors

Emoji-related footguns, in increasing order of severity:

1. **Simple emoji** (`🙂` U+1F642): one code point, rendered as one glyph
   occupying two cells. Byte-safe in all our paths.
2. **Skin-tone modifier sequences** (`👍🏽`): two code points (`U+1F44D
   U+1F3FD`), **one grapheme cluster**. Splitting at any boundary other
   than the cluster edge produces garbage.
3. **Zero-Width-Joiner sequences** (`👨‍👩‍👧‍👦`): up to 7 code points
   joined by `U+200D`. These are a textbook truncation trap. Do not slice
   by code-point count.
4. **Variation selectors** (`1️⃣` = `1 U+FE0F U+20E3`): three code points,
   one cluster. The `U+FE0F` selector requests emoji presentation; strip
   it and the `1` renders as a boring digit.
5. **Flag sequences** (`🇯🇵` = two Regional Indicator codepoints): two
   code points, one cluster. Joining or splitting produces either
   another country or nonsense.

**Our guarantee:** we pass all of the above through byte-for-byte when
the model emits them. We do not claim pretty rendering in every
terminal -- that depends on the terminal's font stack.

---

## 4. Terminal coverage (observational)

| Terminal                     | CJK glyphs | Emoji ZWJ | Variation selectors | Notes                                |
|------------------------------|------------|-----------|---------------------|--------------------------------------|
| Windows Terminal (1.18+)     | ✅ correct  | ✅ most    | ✅                   | Emoji font fallback via Segoe UI Emoji. |
| iTerm2 (macOS)               | ✅          | ✅         | ✅                   |                                      |
| Alacritty                    | ✅          | partial   | partial             | Grapheme-cluster rendering is improving. |
| Kitty                        | ✅          | ✅         | ✅                   | Best-in-class for complex sequences. |
| GNOME Terminal / Konsole     | ✅          | ✅         | ✅                   | libvte / KDE renderer.               |
| `xterm` (default font)       | varies     | ❌         | ❌                   | Upgrade or install `xterm-unicode`.  |
| `conhost.exe` (legacy cmd)   | ❌          | ❌         | ❌                   | Use Windows Terminal.                |

Rendering gaps above are **not** CLI bugs.

---

## 5. Locale-aware collation -- we don't do it

If a future feature ever sorts CJK strings (none today), the naive
`string.Compare` with `InvariantGlobalization=true` falls back to an
**ordinal code-point comparison**, not a locale-collated order.
Implications:

- 日本語 sorted after ASCII `Z` (code points are higher).
- Japanese iroha/aiueo order is **not** supported.
- Chinese pinyin order is **not** supported.
- Korean Hangul jamo order is partly ordinal and partly locale-dependent;
  we do neither.

This is fine for byte-stable machine consumption. It is not fine for a
human-facing sorted list. We have no such list today. If we add one,
document the ordering choice there.

---

## 6. When a user reports "CJK is broken"

Diagnostic script:

```bash
# 1. Is it bytes-in, bytes-out?
echo -n "日本語" | xxd | head -1
#    →  e6 97 a5 e6 9c ac e8 aa 9e   (three three-byte UTF-8 sequences)

# 2. Does the CLI preserve them?
az-ai-v2 --raw --dry-run "日本語で短く返して。" 2>&1 | xxd | head

# 3. Is it the terminal?
echo "日本語" | cat
#    if this renders as ??? or boxes, the terminal is the issue.

# 4. Is it the locale?
locale  # LC_ALL should include a UTF-8 variant.
echo $LANG

# 5. Is it Windows cmd.exe? chcp 65001 before anything else.
```

Walk the user through this sequence before touching CLI code. Nine times
out of ten, it is the terminal or the code page.

---

## 7. Cross-refs

- [docs/i18n.md](../i18n.md) -- top-level contract.
- [docs/i18n/rtl-audit.md](rtl-audit.md) -- RTL byte-transparency.
- [docs/i18n/test-corpus.md](test-corpus.md) -- CJK fixtures we test against.
- [docs/accessibility-review-v2.md](../accessibility-review-v2.md) -- Mickey
  owns terminal-width math and screen-reader behaviour; this doc defers
  to his on rendering policy.
