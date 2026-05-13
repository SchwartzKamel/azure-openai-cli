# az-ai -- Translated quick-start guides

Best-effort translations of the English quick-start. Native-speaker
review is welcome -- file an issue referencing `s04off1-the-translation`.

| Language | Code | File |
|----------|------|------|
| Japanese | ja | [quick-start.ja.md](quick-start.ja.md) |
| Chinese (Simplified) | zh-CN | [quick-start.zh.md](quick-start.zh.md) |
| Spanish | es | [quick-start.es.md](quick-start.es.md) |
| Korean | ko | [quick-start.ko.md](quick-start.ko.md) |

---

## What is translated, what is not

Only the quick-start path is translated today:
install, set credentials, first command, next steps.

**Not yet translated:**

- Full CLI strings (`--help`, `--doctor`, error messages) -- future episode.
  Requires a resource-file architecture compatible with Native AOT.
- The interactive setup wizard prompts.
- Architecture docs, ADRs, exec reports, proposals.

Command names and environment variable names stay in ASCII because they are
universal. `az-ai`, `AZUREOPENAIENDPOINT`, `--raw`, `--agent` are the same
in every language; translating them would break copy-paste workflows.

---

## Quality and review status

All four translations carry a "best-effort" marker and an explicit call
to file an issue. None of them should be treated as production-grade
until a native speaker has reviewed and signed off. Target: before v3.0.

The underlying encoding infrastructure is already correct:

- `Console.OutputEncoding` / `Console.InputEncoding` set to UTF-8 at startup.
- `InvariantGlobalization=true` -- no accidental locale dependency.
- `ReadFileTool` NFKC-normalizes paths (handles macOS NFD filenames for CJK and Korean).
- All string comparisons use `StringComparison.Ordinal` -- no Turkish-I or culture-
  sensitive folding.

Users piping CJK, Hangul, or accented Latin characters through `az-ai` get byte-
identical output. See `docs/i18n/cjk-notes.md` for the full engineering reference.

---

## Adding a new language

1. Create `docs/i18n/quick-start.<code>.md` following the structure of an
   existing translation (H1 title, blockquote translation note, H2 sections for
   Install / Set credentials / First command / Next steps, H2 Translation notes).
2. Keep command names and env-var names in ASCII. Translate surrounding prose only.
3. Mark uncertain phrases with `[?]` and explain them in the "Translation notes"
   section at the bottom of the file.
4. Add a row to the table in this file.
5. The four translation files are exempt from the ASCII smart-quote/em-dash grep
   (CJK and accented Latin characters are required content). The index file
   (this file) still follows ASCII rules.
6. Run `NODE_OPTIONS="--max-old-space-size=4096" npx markdownlint-cli2 "docs/i18n/**/*.md"`
   and confirm 0 errors before committing.
7. Open a PR and request native-speaker review. Tag the issue filed in the
   translation note at the head of the file.

---

## Planning reference

This translation set was introduced in S04 off-roster special *The Translation*.
See `docs/episode-briefs/s04off1-the-translation.md` for the full brief, including
the explicit deferral list (RTL languages, gettext/.resx framework, locale-aware
formatting) and the future-episodes roadmap.
