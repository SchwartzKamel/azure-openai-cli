# i18n test corpus

*Sub-document of [docs/i18n.md](../i18n.md) §7 (contributor guidelines).
Maintained by Babu Bhatt. Closes audit finding L-04 (test corpus) and
feeds into M-04 (non-ASCII examples).*

This file is the canonical list of non-ASCII strings contributors and
reviewers use to exercise the CLI's text handling. Copy-paste freely.
Every string here has been chosen for a specific class of bug.

---

## 1. Why a fixture list exists

Without a shared list, each contributor picks their favourite non-ASCII
string (`café`, `日本語`, `🙂`) and never covers the nasty ones
(combining marks, surrogate pairs, BiDi overrides, ZWJ sequences). The
fixtures below are the **adversarial** corpus: if a change survives all
of them byte-identically, the i18n contract is intact.

Running these is **not** gated in CI today -- see [§7](#7-promotion-to-gate).
The gate is the next step; the corpus is the prerequisite.

---

## 2. Fixture catalogue

Each row lists a string, the codepoint structure, and the class of bug
it exercises. Use them in unit tests, manual smoke runs, and PR
descriptions when touching text paths.

### 2.1 Latin-1 / Latin Extended

| String     | Hex (UTF-8)                   | Exercises                          |
|------------|-------------------------------|------------------------------------|
| `Prüfung`  | `50 72 c3 bc 66 75 6e 67`     | German umlaut (common).            |
| `Straße`   | `53 74 72 61 c3 9f 65`        | Sharp-s (ß, U+00DF).               |
| `Ångström` | `c3 85 6e 67 73 74 72 c3 b6 6d` | Å + ö mixed.                    |
| `naïve`    | `6e 61 c3 af 76 65`           | Dieresis on i.                     |
| `résumé`   | `72 c3 a9 73 75 6d c3 a9`     | Precomposed acute.                 |

### 2.2 Combining-character forms

| String            | Structure                        | Exercises                          |
|-------------------|----------------------------------|------------------------------------|
| `café` (NFC)      | `63 61 66 c3 a9`                 | Precomposed é (U+00E9).            |
| `café` (NFD)      | `63 61 66 65 cc 81`              | e + combining acute (U+0301).      |
| `न्` (Devanagari) | `e0 a4 a8 e0 a5 8d`              | Base + virama (grapheme cluster).  |
| `1̵`              | `31 cc b5`                       | Combining overlay on ASCII digit.  |

**Critical:** `café` NFC vs NFD must never be silently normalised by us.
See [rtl-audit.md §5](rtl-audit.md#5-what-would-break-the-byte-transparent-contract).

### 2.3 CJK (Chinese / Japanese / Korean)

| String     | Script              | Exercises                          |
|------------|---------------------|------------------------------------|
| `日本語`    | CJK Unified         | Wide (2-cell) rendering.           |
| `中文`      | CJK Unified         | Two-character sanity check.        |
| `한국어`    | Hangul syllables    | Korean composed syllables.         |
| `한국어` (jamo) | `ᄒ ᅡ ᆫ ᄀ ᅮ ᆨ ᄋ ᅥ ᄋ ᅥ` | Decomposed Hangul (12 code points). |
| `Ａ Ｂ Ｃ`   | Full-width Latin    | Looks like ASCII, isn't (U+FF21+). |

### 2.4 RTL (right-to-left)

| String     | Script   | Exercises                                |
|------------|----------|------------------------------------------|
| `مرحبا`    | Arabic   | Contextual joining forms.                |
| `שלום`     | Hebrew   | RTL base.                                |
| `سلام`     | Persian  | Extended Arabic (includes U+067E etc.).  |
| `עִבְרִית`  | Hebrew + niqqud | RTL with combining vowel points.   |
| `Mixed: az-ai is כלי CLI` | Mixed LTR/RTL | BiDi run transitions.    |

### 2.5 Emoji & ZWJ sequences

| String     | Codepoints                             | Exercises                |
|------------|----------------------------------------|--------------------------|
| `🙂`       | `U+1F642`                              | Simple emoji, 1 cluster. |
| `👍🏽`      | `U+1F44D U+1F3FD`                      | Skin-tone modifier.      |
| `👨‍👩‍👧‍👦` | `U+1F468 U+200D U+1F469 U+200D U+1F467 U+200D U+1F466` | ZWJ family. |
| `1️⃣`      | `U+0031 U+FE0F U+20E3`                 | Variation selector.      |
| `🇯🇵`      | `U+1F1EF U+1F1F5`                      | Regional indicator flag. |

### 2.6 Control / adversarial

| String        | Codepoint(s)       | Exercises                        |
|---------------|--------------------|----------------------------------|
| `a‌b`         | `61 U+200C 62`     | Zero-width non-joiner.           |
| `a‍b`         | `61 U+200D 62`     | Zero-width joiner in text.       |
| `a‎b` / `a‏b` | `U+200E` / `U+200F` | LTR mark / RTL mark.            |
| `hello‮dlrow` | `U+202E` override  | Trojan Source / BiDi override.   |
| `𝐀`           | `U+1D400`          | Surrogate pair (>BMP).           |
| `\uFEFF...`   | `U+FEFF` BOM       | Leading BOM tolerance (input).   |

These are not user-friendly strings; they exist to probe assumptions.
Expected behaviour: **pass through byte-identically**, do not strip, do
not normalise, do not crash.

---

## 3. Running the corpus manually

```bash
# Pipe every fixture through --dry-run and diff the stdout bytes.
./scripts/test-i18n.sh
```

See [`scripts/test-i18n.sh`](../../scripts/test-i18n.sh). It:

1. Feeds each fixture as a prompt to `az-ai-v2 --dry-run --raw` (no
   network; the dry-run path echoes the prompt it would send).
2. Compares the output byte-for-byte with the input.
3. Fails loudly on mismatch.

It does **not** exercise model responses (those aren't deterministic).
It exercises the CLI's own input handling, encoding, and echo.

---

## 4. Unit-test placement (future)

When promoted to a CI gate, fixtures should live in:

```text
tests/AzureOpenAICliV2.Tests/Fixtures/i18n-corpus.txt   (one string per line, NFC)
tests/AzureOpenAICliV2.Tests/Fixtures/i18n-corpus-nfd.txt   (NFD variants)
tests/AzureOpenAICliV2.Tests/I18nRoundTripTests.cs           (asserts byte-identity)
```

The corpus file is UTF-8, LF-only, no BOM (enforced by `.editorconfig`
already). Each line is one fixture; empty lines and `#`-prefixed comment
lines are ignored.

---

## 5. What "pass" means

For every fixture `X`:

1. `echo -n "$X" | wc -c` equals the byte length of the UTF-8 encoding.
2. `az-ai-v2 --dry-run --raw "$X"` emits a body containing `X` as a
   contiguous byte sequence (no normalisation, no stripping).
3. `az-ai-v2 --dry-run --json "$X" | jq -r '.messages[0].content'`
   round-trips to `X`.
4. A config file containing `X` as a persona name, system prompt, or
   memory entry loads and serialises back to the same bytes.

Any divergence is a regression.

---

## 6. What "fail" means

Examples of real regressions the corpus would catch:

- Adding `.Trim()` on a prompt -- `\uFEFF` BOM, `\u200E` LTR mark, or
  trailing combining marks get silently eaten.
- Switching a `TextWriter` to default encoding on Windows -- half the
  fixtures become mojibake.
- Adding `string.Normalize(NormalizationForm.FormC)` -- NFC/NFD fixtures
  diverge, surrogate-pair fixtures may or may not (depends on the pair).
- Swapping `UTF8Encoding(false)` for `UTF8Encoding(true)` -- a BOM gets
  prepended to output.

---

## 7. Promotion to gate

Today: guideline. Tomorrow: gate. The gate looks like:

```yaml
# .github/workflows/i18n-corpus.yml (future)
- name: Run i18n byte-identity corpus
  run: ./scripts/test-i18n.sh
```

Blockers for gating:

- `--dry-run` must be guaranteed network-free (currently is; Newman's
  territory -- coordinate before gating).
- `--dry-run` must be guaranteed deterministic across platforms (CRLF
  vs LF -- confirm on Windows runner).

When both are confirmed, add the workflow and flip the guideline to a
requirement.

---

## 8. Cross-refs

- [docs/i18n.md](../i18n.md) -- top-level contract and TL;DR.
- [docs/i18n/rtl-audit.md](rtl-audit.md) -- RTL byte-transparency.
- [docs/i18n/cjk-notes.md](cjk-notes.md) -- wide-character and emoji caveats.
- [scripts/test-i18n.sh](../../scripts/test-i18n.sh) -- the runner.
