# S02E08 -- *The Translation*

> Babu Bhatt walks the entire CLI string by string, points at every
> mid-sentence interpolation, and stands up the project glossary so
> Lloyd Braun never has to ask what RTL means again.

**Commit:** `65c68e1` (parent) -> docs commits this episode
**Branch:** `main` (direct push, per `.github/skills/commit.md`)
**Runtime:** ~25 minutes
**Director:** Larry David (showrunner)
**Cast:** Babu Bhatt (lead, i18n / l10n), Elaine Benes (guest, table
structure and prose), Lloyd Braun (guest, glossary owner and the kid
who actually asked what i18n means)

## The pitch

The CLI has shipped two major versions of user-facing English without
anyone ever asking whether a translation effort would be tractable.
Babu reviewed the codebase and the answer is: not yet, and here's
exactly why. This episode produces the map -- a string-by-string
inventory and a per-file classification of locale-agnostic,
translation-ready, and needs-work strings -- without doing any of the
refactor work itself. Scope discipline is the entire point of the
episode.

The secondary goal: stand up `docs/glossary.md`. The codebase tosses
around AOT, DPAPI, libsecret, RTL, CJK, MCP, TPM/RPM, and SSRF
constantly and a new contributor has nowhere to look. Lloyd Braun owns
the glossary in S02 going forward; this episode seeds it with eleven
entries.

## Scene-by-scene

### Act I -- Inventory

Grepped `Console.WriteLine`, `Console.Write`, `Console.Error.*`, and
`throw new ...Exception(` across `azureopenai-cli/`. Useful surprises:

- `Squad/`, `Tools/`, `ConsoleIO/` have **zero** direct console
  output. All user-facing text routes through `Program.cs`, which
  makes the eventual extraction surface much smaller than it could be.
- `FirstRunWizard.cs` has the highest density of category-(c) strings
  (six out of fourteen lines I tagged), all in the help-text and
  prompt area. The validation-result lines are uniformly clean.
- The Ralph mode banners are mostly fine; the trap is the
  `iteration(s)` plural shortcut at `Program.cs:1799` and `:1822`.

### Act II -- Categorize

Three buckets, defined in `docs/i18n-audit.md`:

- **(a) Locale-agnostic** -- env-var names, JSON output, version
  strings, control sequences. Never wrap in a translation function.
- **(b) Translation-ready** -- stable sentences, optionally with a
  trailing or leading interpolated value that does not split a clause.
- **(c) Needs work** -- mid-sentence interpolation, English plural
  shortcuts, column-aligned help text assuming single-cell glyphs,
  RTL-unsafe emoji-prefixed lines, locale-sensitive number formatting
  via `:F0` against current culture.

Per-file tables in the audit doc. Elaine kept the columns to four
(Source location, String, Category, Notes) so each row reads in one
line at standard terminal width.

### Act III -- Unicode correctness check

Read `FirstRunWizard.ReadMaskedFromConsole` (lines 287-334). Three
edge cases:

1. **BMP characters** -- safe. `char.IsControl` filters control codes,
   everything else is appended.
2. **Surrogate pairs** (emoji, supplementary plane) -- safe. High and
   low surrogate arrive as two `ConsoleKeyInfo` events; both are
   non-control; both are appended; `new string(buf.ToArray())`
   reconstructs valid UTF-16.
3. **NBSP (U+00A0) trailing paste artifact** -- safe. `key.Trim()` at
   line 267 uses `char.IsWhiteSpace`, which includes NBSP, so it gets
   stripped.

One narrow edge case remains: a **lone surrogate** (high without low,
or vice versa) from a paste that a misbehaving terminal split mid-
pair. Would survive into the saved credential as invalid UTF-16. NOT
a one-line fix -- requires `char.IsSurrogate` pair-completeness
validation in the read loop. Filed as a follow-up below; explicitly
NOT fixed this episode per the brief's "do not modify the masked-input
read path unless one-line".

`FirstRunWizard.cs` was not touched.

### Act IV -- Audit doc

`docs/i18n-audit.md` written. Babu intro at the top, "Lloyd asks"
callout for i18n vs l10n in the second section, scheme, four per-file
tables, Unicode subsection, "Next steps" enumerating the seven
refactors a real l10n effort would need, and an explicit "Out of
scope" closer.

### Act V -- Glossary

`docs/glossary.md` created. Eleven seed entries, one H3 each,
alphabetical, ASCII-only. Footer documents the conventions for future
appenders. Lloyd owns this file going forward.

### Act VI -- Lloyd callout

Inserted near the top of `i18n-audit.md` as a dedicated subsection:
"What's the difference between i18n and l10n?" with a one-paragraph
plain-English answer that cross-links to the glossary.

### Act VII -- Ship

CHANGELOG: two bullets under `[Unreleased] > Added`, surgical edit.
Pre-validate confirmed no smart quotes, em-dashes, or en-dashes in any
new file. Two commits, push to `main`.

## What shipped

**Production code** -- none. Scope discipline.

**Tests** -- none. No code changed.

**Docs**:

- `docs/i18n-audit.md` (new, ~250 lines)
- `docs/glossary.md` (new, ~95 lines)
- `CHANGELOG.md` (two bullets)
- `docs/exec-reports/s02e08-the-translation.md` (this file)

**Not shipped** (intentional follow-ups):

- No translations. Zero `.po` / `.resx` / resource files.
- No `--locale` flag.
- No string refactors -- the audit names the seven refactor targets;
  each is a separate future episode.
- No CJK / RTL test fixtures.
- The masked-input read path was not modified. The lone-surrogate
  edge case is filed below as a follow-up; not a blocker for any
  realistic input source.

## Lessons from this episode

1. **The `iteration(s)` shortcut is the most common anti-pattern in
   the codebase.** Two occurrences in Ralph mode; the same author wrote
   both. Worth a lint rule eventually -- a regex for `\([sS]\)` in any
   `Console.Write*` argument would catch the family.
2. **Column alignment via padding-spec strings (`,-12`) is invisible
   to grep for "i18n problems" but is the single biggest blocker to
   ever shipping CJK output.** Three sites: `--personas`, `--help`,
   `--config show`. They all share a future renderer.
3. **`:F0` against current culture is a latent bug today, not just an
   i18n one** -- the retry line at `Program.cs:1445` will print
   `1,0s` on a `de-DE` machine, which is inconsistent with the rest
   of the CLI's invariant numeric output. Worth flagging to Frank
   independently of any l10n work.
4. **The masked-key path is more Unicode-safe than I expected.** The
   simple `List<char>` + `Trim()` combo handles the realistic cases
   correctly. The lone-surrogate gap is theoretical for any sane
   terminal; do not over-engineer it.
5. **Tools and Squad have zero user-facing strings.** This is the
   right architecture for a future l10n effort and was not on
   purpose -- it fell out of the JSON-output discipline. Worth
   preserving as we add more tools.

## Metrics

- Diff size: 3 files changed, ~360 insertions, 1 deletion.
  - `docs/i18n-audit.md` -- new (~250 lines).
  - `docs/glossary.md` -- new (~95 lines).
  - `CHANGELOG.md` -- +9 / -1 (two bullets prepended to Unreleased).
  - `docs/exec-reports/s02e08-the-translation.md` -- new.
- Test delta: none. No code changed.
- Preflight: not required (docs-only). Pre-validated for smart
  quotes, em-dashes, and en-dashes -- clean.
- CI status at push time: docs-only commits; CI runs build/test/format
  on the unchanged codebase. Expected pass.

## Follow-ups for the showrunner

- **`fix(wizard):` lone-surrogate validation in `ReadMaskedFromConsole`.**
  Edge case: paste with mismatched UTF-16 surrogate. Two-line fix
  (track `_pendingHighSurrogate` between reads, validate pair
  completeness, drop or buffer the orphan). Not urgent; no realistic
  terminal produces this. Suggest folding into a future "wizard
  hardening" episode.
- **`fix(perf):` `:F0` should use `CultureInfo.InvariantCulture`** at
  `Program.cs:1445`. One-line change. Independent of any l10n work --
  this is a "consistency with the rest of the binary" bug today.
- **The seven refactor targets** in `docs/i18n-audit.md` "Next steps"
  should each become a separate future episode if and when l10n is
  formally on the roadmap. Not committing the team to any of it.

## Credits

- **Babu Bhatt** -- lead. Authored every category-(c) finding in the
  per-file tables, including the Arabic list-separator (U+060C)
  catch and the German verb-final critique of the wizard welcome
  line.
- **Elaine Benes** -- guest. Owned the four-column table layout and
  the "Out of scope" closer phrasing. Caught the inconsistent use of
  "translation-ready" vs "ready for translation" in the first draft.
- **Lloyd Braun** -- guest. Owns `docs/glossary.md` going forward.
  Asked the i18n-vs-l10n question that became the callout.
- **Larry David** -- showrunner. Cast the episode and signed off on
  the no-refactor scope discipline.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
