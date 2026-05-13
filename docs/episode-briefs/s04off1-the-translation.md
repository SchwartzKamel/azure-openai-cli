> **Status: GREENLIT 2026-05-13 -- Larry David. Off-roster special. Cast: Babu Bhatt lead.**

# S04 Off-Roster Special -- The Translation

> Log line: Four translated quick-starts (Japanese, Chinese, Spanish, Korean) grounded
> in az-ai's existing UTF-8 foundation. Documentation, not code. Babu's first lead.

---

You are reading the planning brief for **S04 Off-Roster Special *The Translation*** for
`azure-openai-cli`. Working directory: `/home/tweber/tools/azure-openai-cli`.
Branch: `main`.

---

## Casting

- **Lead: Babu Bhatt** -- i18n/localization specialist. Owns the translated quick-start
  documents, the i18n index, and this brief. First lead in the project (S03 cast-balance
  correction per `writers-room-cast-balance` skill Rule 5: supporting players lead at
  least one episode per season).
- **Guest: Puddy** -- QA / test engineer. Owns the parallel-track
  `tests/AzureOpenAI_CLI.Tests/I18n/CjkRoundTripTests.cs`. Canonical Rule 5 pairing:
  Babu (translation) + Puddy (adversarial test coverage). Puddy does not touch docs;
  Babu does not touch tests.
- **Queued (Wave 2, not dispatched): Lloyd Braun** -- junior developer lens. Reviews
  translated quick-starts for onboarding clarity after merge. Specifically: are
  install steps unambiguous for a non-native English reader who has never used a
  .NET AOT binary? Queue does not block this episode's ship.

---

## Theme

The user directive is simple and overdue: Japanese, Chinese, Spanish, and Korean support.
The prior i18n work in this project (CJK notes, RTL audit, test corpus) established that
az-ai passes non-ASCII bytes through cleanly. That foundation is the right one. This
episode documents it for non-English speakers and gives them a translated entry point.

Japanese is the primary user directive -- it ships first in the file listing and gets
the most detailed per-language notes. Chinese (Simplified, zh-CN), Spanish, and Korean
are first-class parallel deliverables, not afterthoughts.

The cost of leaving this undone: non-English speakers staring at an English README, not
knowing whether the tool handles their language at all. The CJK notes doc exists but
is an internal engineering reference, not a user-facing quick-start. This episode closes
that gap. No code changes required. The infrastructure is already correct.

The second part of the theme is documentation of what already works: `InvariantGlobalization=true`,
UTF-8 stdio, NFKC path normalization. These were engineering decisions made without
ever being written down in user-facing language. This brief and the quick-starts make
them explicit.

---

## Scope

### In

- 4 translated quick-start docs: `docs/i18n/quick-start.{ja,zh,es,ko}.md`
- Translation index: `docs/i18n/README.md`
- This planning brief: `docs/episode-briefs/s04off1-the-translation.md`
- CJK + Spanish round-trip test coverage (Puddy, parallel track -- not in this brief's
  deliverables but in scope for the off-roster special as a whole)
- Reaffirmation (in this brief) that `InvariantGlobalization=true` + UTF-8 stdio is
  the correct and sufficient foundation for quick-start users
- A "translation review wanted" marker at the head of each translated file, with an
  explicit issue-filing call to action

### Out (explicit deferral list)

- **Localized CLI strings** (`--help`, `--doctor`, error messages): requires a resource
  file architecture (gettext or .resx), a design decision about AOT compatibility, and
  a localization pipeline. Future episode -- acceptance bar: every user-facing string
  has a resource key; at least `ja` and `zh-CN` are translated.
- **gettext / .resx framework**: no dependency added today.
- **RTL languages (Arabic, Hebrew, Persian)**: `docs/i18n/rtl-audit.md` already covers
  byte-transparency for RTL. A first-class RTL quick-start requires bidi-safe terminal
  rendering notes and a right-to-left Markdown structural review. Future episode.
- **Locale-aware number / date / currency formatting**: would conflict with
  `InvariantGlobalization=true`. The trade-off is intentional; changing it requires
  a performance and correctness audit. Future episode.
- **Automated translation pipelines** (machine translation APIs, sync scripts):
  deferred pending native-speaker review quality bar.
- **Traditional Chinese (zh-TW)**: not in the user directive. Defaulting to zh-CN
  (Simplified). zh-TW ships as a separate episode if requested.
- **README.md edits**: top-level README links to translations are a follow-up; a
  parallel agent may own README.md. Filed as a Wave 2 todo.

---

## Languages -- per-language notes

### Japanese (ja) -- primary

Japanese has three scripts in active use: Hiragana (phonetic, native words/grammar),
Katakana (phonetic, loanwords and emphasis), and Kanji (logographic, from Chinese).
Technical documentation typically uses all three, with Katakana used heavily for
borrowed technical terms (`インストール` for "install", `エンドポイント` for "endpoint").

**Rendering notes:**

- CJK ideographs are East Asian Width: Wide -- they occupy 2 terminal cells per
  glyph. `docs/i18n/cjk-notes.md` §2.1 covers the terminal-width implications.
  The quick-start itself does not depend on column-aligned output, so width math
  is not a user-facing concern here.
- The existing `az-ai` CLI output is ASCII; CJK only appears in user prompts and
  model responses. No rendering regression introduced by this episode.
- NFKC normalization in `ReadFileTool` handles Japanese filenames containing
  full-width characters or combining dakuten (e.g., `テスト` vs composed vs decomposed
  forms). Quick-start users will not encounter this unless they have CJK path names
  in their env config file -- unlikely but handled.
- Puddy's test suite (parallel track) covers: `az-ai "日本語で返してください"` round-trip,
  Hiragana/Katakana/Kanji all preserved byte-identical, no mojibake on UTF-8 stdout.

**Translation quality:** best-effort. Technical vocabulary (install, credentials,
endpoint, API key) uses standard Japanese IT terminology. Grammar reviewed against
common technical documentation patterns. Native-speaker review wanted before v3.0;
"translation review wanted" marker at file head with issue-filing link.

### Chinese (zh-CN, Simplified)

The user directive said "Chinese" without specifying script. Defaulting to Simplified
Chinese (zh-CN), which is the standard in mainland China, Singapore, and most
international Chinese-language technical communities. Traditional Chinese (zh-TW,
used in Taiwan, Hong Kong, Macau) deferred to a future episode.

**Rendering notes:**

- Same East Asian Width considerations as Japanese -- identical terminal-cell math.
- Simplified Chinese filenames normalize correctly under NFKC (no compatibility
  decompositions common in Simplified script paths).
- The test corpus in `docs/i18n/test-corpus.md` includes Simplified Chinese fixtures
  (`你好世界`, `简体中文测试`). Puddy's parallel suite extends this.

**Translation quality:** best-effort. Standard Simplified Chinese technical vocabulary.
Native-speaker review wanted; marked at file head.

### Spanish (es)

Spanish is the Latin-script outlier in this set. It is the easiest to verify
mechanically (accented characters are a subset of Latin Extended-A, all in the BMP)
and the most widely spoken non-English language in many az-ai user demographics
(US, LATAM, Spain).

**UTF-8 round-trip:** Accented characters (`a e i o u n`, with tildes and acute
accents, plus inverted `?` and `!`) are all multi-byte in UTF-8 but well within the
"no surrogates" safe zone. The existing UTF-8 pipeline handles them without any
special casing.

**Required characters in the translation:** `a e i o u n` with accents (acute),
inverted question mark (`?`), inverted exclamation mark (`!`). All present in the
Spanish quick-start.

**ASCII validation note:** the 4 translation files are **exempt** from the ASCII
smart-quote/em-dash grep per the brief's coordination constraints. Accented Latin
characters are required content, not style violations.

**Translation quality:** high confidence. Spanish grammar and technical vocabulary
are well within Babu's scope. Accent placement verified against common technical
documentation conventions. Still marked "review wanted" as a matter of policy.

### Korean (ko)

Korean uses the Hangul script -- an alphabet (strictly, an alphasyllabary) where
characters are composed into syllabic blocks. Unicode represents Hangul as either
precomposed syllable blocks (U+AC00..U+D7A3, the standard) or as decomposed jamo
sequences (U+1100..U+11FF). NFKC normalization in `ReadFileTool` normalizes
decomposed jamo to precomposed blocks, which is correct behavior.

**Rendering notes:**

- Hangul syllable blocks are East Asian Width: Wide. Same 2-cell terminal rendering
  concern as CJK. Same conclusion: quick-start users are unaffected.
- Korean filenames in NFKC-decomposed form (macOS sometimes produces these) will
  normalize correctly under `ReadFileTool`'s existing normalization pass.
- Puddy's test suite should include at least one Korean fixture (`안녕하세요` round-trip,
  NFC and NFD forms). Coordinate with the `test-corpus.md` fixture catalogue.

**Translation quality:** good for standard technical vocabulary. Korean IT terminology
uses a mix of native Korean and anglicized loanwords in Hangul transcription
(`인스톨` or `설치` for install -- the latter is preferred in formal docs).
Uncertain phrases marked `[?]` in the document with a footer explanation. Native-speaker
review wanted; marked at file head.

---

## Existing infrastructure audit

What already works in az-ai's favor for non-ASCII/non-English usage:

| Component | Behavior | Source |
|-----------|----------|--------|
| `<InvariantGlobalization>true</InvariantGlobalization>` | Locks the runtime to invariant culture. No accidental dependency on system locale. String comparisons are ordinal, not culture-sensitive. | `azureopenai-cli/AzureOpenAI_CLI.csproj:15` |
| `Console.OutputEncoding = Encoding.UTF8` | Ensures stdout writes UTF-8 on all platforms, including Windows `conhost.exe`. On Unix this is typically a no-op but harmless. | `azureopenai-cli/Program.cs:147` |
| `Console.InputEncoding = Encoding.UTF8` | Ensures stdin reads UTF-8. Critical for prompts that include CJK, accented Latin, or Hangul typed directly. | `azureopenai-cli/Program.cs:148` |
| `ReadFileTool` NFKC normalization | Normalizes file paths before access. Defends against Unicode homoglyph attacks AND normalizes macOS NFD filenames. | `azureopenai-cli/Tools/ReadFileTool.cs` |
| `ProcessStartInfo.StandardOutputEncoding = Encoding.UTF8` | Ensures child-process output (via `shell_exec`, etc.) is decoded as UTF-8, not the system default code page. | `azureopenai-cli/Tools/ShellExecTool.cs` |
| `StringComparison.Ordinal` / `OrdinalIgnoreCase` | All string comparisons in the codebase use ordinal mode. No Turkish-I bug, no culture-sensitive case folding. | Project-wide convention |
| JSON serialization | `AppJsonContext` produces valid UTF-8 JSON. Unicode code points above U+007F are preserved as literal UTF-8 bytes, not `\uXXXX` escapes (both forms are valid; literal is more readable). | `azureopenai-cli/JsonGenerationContext.cs` |

**Assessment:** The foundation is correct and complete for the quick-start use case.
Users who pipe CJK or accented-Latin text through `az-ai` get byte-identical output.
No architecture changes are required for this episode. The quick-starts document this
reality; they do not require it to be built.

---

## What ships in this off-roster special

| File | Type | Description |
|------|------|-------------|
| `docs/episode-briefs/s04off1-the-translation.md` | NEW | This brief |
| `docs/i18n/README.md` | NEW | Translation index with language table and "adding a new language" guide |
| `docs/i18n/quick-start.ja.md` | NEW | Japanese quick-start (primary language per user directive) |
| `docs/i18n/quick-start.zh.md` | NEW | Simplified Chinese quick-start |
| `docs/i18n/quick-start.es.md` | NEW | Spanish quick-start |
| `docs/i18n/quick-start.ko.md` | NEW | Korean quick-start |

No files are edited. No code changes. No test files. No CHANGELOG edit (exec-report
agent handles narrative). No README.md edit (parallel-agent constraint).

---

## Future episodes

Explicitly enumerated, in priority order:

1. **Full CLI-string localization** -- `--help`, `--doctor`, `[ERROR]` prefix, wizard
   prompts. Requires a resource-file architecture compatible with Native AOT
   (most likely embedded JSON resource keys, not .resx). Acceptance bar: every
   user-facing string has a resource key; `ja` and `zh-CN` shipped; `es` and `ko`
   queued.
2. **zh-TW (Traditional Chinese)** -- straightforward once zh-CN ships; primarily
   a vocabulary review pass. Acceptance bar: native-speaker sign-off.
3. **RTL languages (Arabic, Hebrew, Persian)** -- requires bidi-safe terminal output
   analysis (coordinate with Mickey on terminal-width math and screen-reader
   behavior), Markdown structural review for RTL layout, and native-speaker QA.
   The `docs/i18n/rtl-audit.md` doc is the starting point.
4. **Native-speaker review pass** -- all 4 languages in this episode carry a
   "best-effort, review wanted" marker. A future episode lifts those markers after
   community or contracted native-speaker review. Target: before v3.0.
5. **Automated translation pipeline** -- machine-translation bootstrap with human
   review overlay. Deferred until the resource-key architecture (item 1) ships;
   translating strings in isolation before keys are stable produces churn.
6. **Locale-aware date/number/currency** -- requires revisiting `InvariantGlobalization=true`.
   Trade-off analysis needed. Out of scope until a concrete user complaint arises.

---

## Acceptance criteria

1. All 4 quick-start files render correctly in a UTF-8 terminal (manual smoke test:
   `cat docs/i18n/quick-start.ja.md` produces legible Japanese without mojibake).
2. `NODE_OPTIONS="--max-old-space-size=4096" npx markdownlint-cli2 "docs/i18n/**/*.md" "docs/episode-briefs/s04off1-the-translation.md"`
   exits 0 with 0 errors.
3. `docs/i18n/README.md` links to all 4 translation files; all links resolve.
4. ASCII validation passes on `docs/i18n/README.md` and this brief
   (translation files are exempt per coordination constraints).
5. Puddy's parallel test suite (`CjkRoundTripTests.cs`) passes for all 4 languages.
   This criterion is owned by Puddy's track; Babu's ship is not blocked on it but
   the off-roster special is not complete until both tracks merge.

---

## Dispatch plan

Single wave. All deliverables in this list are authored by Babu Bhatt in this
dispatch:

- `docs/episode-briefs/s04off1-the-translation.md` (this file)
- `docs/i18n/README.md`
- `docs/i18n/quick-start.ja.md`
- `docs/i18n/quick-start.zh.md`
- `docs/i18n/quick-start.es.md`
- `docs/i18n/quick-start.ko.md`

Puddy's `CjkRoundTripTests.cs` is a parallel-track deliverable filed under the
`babu-cjk-tests` dispatch. The two tracks are independent; no merge ordering
constraint except that both should be on `main` before the off-roster special is
marked complete in the writers' room.

Lloyd Braun review is queued for follow-up (Wave 2), not dispatched in this wave.

Commit: single commit, `Skip-Exec-Report` trailer (off-roster planning + content
drop; exec-report ships post-Puddy-tests). Push with rebase-on-reject.

---

## Files MAY touch

- `docs/episode-briefs/s04off1-the-translation.md` (NEW)
- `docs/i18n/README.md` (NEW)
- `docs/i18n/quick-start.ja.md` (NEW)
- `docs/i18n/quick-start.zh.md` (NEW)
- `docs/i18n/quick-start.es.md` (NEW)
- `docs/i18n/quick-start.ko.md` (NEW)

## Files MUST NOT touch

- `README.md` -- parallel-agent constraint; top-level README is in flight
  for other S04 changes.
- `CHANGELOG.md` -- exec-report agent handles narrative.
- `docs/exec-reports/` -- exec report ships after Puddy's tests merge.
- `azureopenai-cli/Registry/*` -- Kramer owns this in S04E01 Wave 1.
- `azureopenai-cli/Program.cs` -- Kramer owns in S04E01 Wave 1.
- `azureopenai-cli/JsonGenerationContext.cs` -- Kramer owns in S04E01 Wave 1.
- `azureopenai-cli/AzureOpenAI_CLI.csproj` -- Kramer owns in S04E01 Wave 1.
- `docs/model-cards/` -- Elaine owns in S04E01 Wave 1.
- `docs/adr/ADR-012-model-registry-seam.md` -- Elaine owns in S04E01 Wave 1.
- `tests/` -- any file under tests/ is out of scope (Puddy's parallel track
  owns `CjkRoundTripTests.cs`; Babu does not touch test files).
- Any `.cs`, `.csproj`, or `.sh` file.

---

## Validation

```bash
# Markdownlint -- must exit 0
NODE_OPTIONS="--max-old-space-size=4096" npx markdownlint-cli2 \
  "docs/i18n/**/*.md" \
  "docs/episode-briefs/s04off1-the-translation.md"

# ASCII validation on non-translation files (translation files are exempt)
grep -nP '[\x{2018}\x{2019}\x{201C}\x{201D}\x{2013}\x{2014}]' \
  docs/i18n/README.md \
  docs/episode-briefs/s04off1-the-translation.md

# Smoke test: UTF-8 renders correctly
cat docs/i18n/quick-start.ja.md | head -5
cat docs/i18n/quick-start.zh.md | head -5
cat docs/i18n/quick-start.ko.md | head -5
```

---

## On completion

```sql
UPDATE todos SET status = 'done' WHERE id = 's04off-babu-brief-and-translations';
```

Return: commit SHA, files created with line counts, language-specific quality
self-assessment, markdownlint result, push result.
