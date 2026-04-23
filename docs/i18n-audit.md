# i18n Readiness Audit

> You think the strings are fine? You are a very bad man! Half of them
> are concatenated mid-sentence and the help text aligns columns by
> assuming every glyph is one cell wide. -- Babu Bhatt

This audit inventories every user-facing string in the v1 CLI binary
and classifies it for internationalization readiness. It does **not**
translate anything, refactor anything, or add a `--locale` flag. It is
the map a future l10n effort needs before it can begin.

## Lloyd asks: what's the difference between i18n and l10n?

**i18n** (internationalization) is the engineering work that makes a
codebase *ready* for translation: pulling strings out of source,
replacing concatenation with full sentences, picking number and date
formatters that respect locale. **l10n** (localization) is the
follow-up work that actually delivers translations and locale-specific
behavior for a particular target like `de-DE` or `ja-JP`. i18n is
preparation; l10n is delivery. You cannot do l10n competently if the
i18n hasn't been done first -- which is exactly the situation this
audit documents. (See `docs/glossary.md` for the long-form definitions
of both terms, plus RTL, CJK, and friends.)

## Categorization scheme

Every user-facing string falls into one of three buckets.

### (a) Locale-agnostic

Pure technical output: env-var names, JSON keys, numeric IDs, file
paths, version strings. These do not need translation and should not
be wrapped in any future translation function. Examples:
`AZUREOPENAIENDPOINT`, `1.8.0`, `gpt-4o`, JSON output from `--json`.

### (b) Translation-ready

Stable strings with at most a single trailing or leading interpolated
value, where the surrounding sentence stays grammatically intact in
other locales. Safe to extract as-is into a future resource file.
Example: `"Saved to {path} (mode 0600)"` -- the `{path}` slot does
not break the sentence in German, French, Japanese, or Arabic because
it is grammatically a noun reference at the end of a clause.

### (c) Needs work

Any of the following anti-patterns:

- **Mid-sentence concatenation or interpolation** that splits a clause
  whose word order changes across locales.
- **English plural shortcuts** like `"3 iteration(s)"` -- breaks in
  languages with two-form (Russian) or no-form (Japanese) plurals.
- **Column-aligned help text** assuming monospaced single-cell glyphs;
  CJK wide chars and RTL runs both break alignment.
- **Inline emoji as semantic markers** (status icons, prefixes) where
  the emoji participates in a directional run that flips under RTL.
- **String-built command examples** that mix English imperative verbs
  inside otherwise-translated text.

## Per-file audit

### `azureopenai-cli/Program.cs`

| Source location | String (truncated) | Category | Notes |
|---|---|---|---|
| 478 | `parseError.Message` | (b) | Already a single composed message; safe to extract once `CliParseError` builds full sentences. |
| 522-524 | `"Squad initialized! ..."`, `"Edit .squad.json ..."` | (b) | Three separate full lines, no concatenation. Translation-ready. Emoji prefix is decorative; LTR-safe. |
| 528 | `"Squad already initialized (.squad.json exists)."` | (b) | Filename in trailing parenthetical; safe. |
| 539 | `"No .squad.json found. Run --squad-init first."` | (b) | Imperative in second sentence; translates cleanly. |
| 542-547 | `"Squad: {Team.Name}"`, `"  {Name,-12} {Role,-20} {Description}"` | (c) | Column-aligned with `,-12` / `,-20` width specifiers. Assumes single-cell glyphs; CJK names and Arabic role names will misalign. |
| 621 | `"Error: stdin input exceeds 1 MB limit."` | (b) | Unit `MB` is locale-agnostic but the prefix `Error:` should come from a centralized error helper. |
| 711, 741 | `$"Auto-routed to: {Name} ({Role})"`, `$"Persona: {Name} ({Role})"` | (b) | Trailing parenthetical pattern. Safe. |
| 822 | `$"\r{spinnerChars[i]} Thinking..."` | (b) | Spinner glyph + verb. The verb "Thinking" is the unit of translation; the spinner glyph is locale-agnostic. |
| 869, 901, 913, 1561, 1620 | `"\r              \r"` | (a) | Cursor / clear control sequence. Width is hard-coded in cells; safe for ASCII spinners but will under-clear if the spinner verb becomes a CJK string. Flag for the refactor. |
| 926, 1470, 1635 | `JsonSerializer.Serialize(...)` | (a) | Machine-readable output; never translate. |
| 934, 1643 | `$"  [tokens: {prompt}->{completion}, {total} total]"` | (c) | Three numbers and a unit interleaved with English glue words. Plural form on `total` is implicit. Needs templating. |
| 980 | `"[cancelled] Exited on user interrupt (CTRL+C)."` | (b) | Bracketed tag + sentence + parenthetical. Safe; `CTRL+C` is locale-agnostic shorthand. |
| 1010, 1041, 1158, 1212, 1224, 1460 | `"[ERROR] ..."` family | (b)/(c) | Mostly stable sentences. Centralize via `ErrorAndExit` helper so the `[ERROR]` prefix is one localizable token. 1224's `$"Model '{name}' not found ..."` mid-sentence interpolation is borderline -- (c) for grammars where adjective placement varies. |
| 1025, 1029 | `semver`, `$"Azure OpenAI CLI v{semver}"` | (a)/(b) | Bare semver is locale-agnostic. The branded line is translation-ready. |
| 1170-1186 | `"Available models:"`, `$"{prefix}{model}{marker}"`, `$"Config file: {path}"` | (b) | Header + list + trailer. Safe. |
| 1196-1228 | `"No active model set."` etc. | (b) | All full sentences. Translation-ready. |
| 1239-1314 | `--help` output | (c) | Heavy column alignment via padding spaces (`  --json                Output ...`). Width assumes single-cell glyphs. Also embeds inline shell examples that should NOT be translated even when surrounding labels are. Mixed translatable / locale-agnostic content per line means a future refactor needs to split each row into label + description. |
| 1263 | `"CTRL+C                graceful cancellation -- flushes state, exits 130"` | (c) | Same column-alignment issue. |
| 1377-1389 | Config display block (`"  Endpoint:      {endpoint} ({source})"`) | (c) | Right-padded labels + trailing source-of-config parenthetical. Word order does not survive German, where the verb-final pattern shifts the parenthetical. |
| 1378 | `"==============================="` | (a) | Decorative ASCII rule. Locale-agnostic; safe. |
| 1445 | `$"\r⏳ Retry {attempt}/{maxRetries} in {delay.TotalSeconds:F0}s..."` | (c) | Three locale concerns: (1) `{a}/{b}` reads right-to-left in RTL contexts and the reader will see "of total slash current"; (2) `s` suffix is English; (3) `:F0` formats the number with the *current culture* -- on a `de-DE` machine this prints `1,0s`, which is inconsistent with the rest of the CLI's invariant numeric output. |
| 1521 | `"Agent mode"` | (b) | Two-word label with leading emoji. Safe; emoji is in an LTR neutral run. |
| 1593 | `$"\rRound {round}: "` | (b) | Stable label. |
| 1658 | `$"\r[WARN] {msg}"` | (b) | Tag + payload. Safe. |
| 1686-1851 | Ralph mode banners (`"Ralph mode -- Wiggum loop active"`, iteration headers, validation status) | (b)/(c) | Banner lines are full sentences (b). The iteration header `$"--- Iteration {n}/{max} ---"` is (c) for the same `n/max` ordering reason as the retry line. |
| 1799, 1822 | `$"Ralph complete after {iteration} iteration(s)"` | (c) | Classic `iteration(s)` plural shortcut. Russian needs three forms; Japanese has none. Use ICU MessageFormat or equivalent at refactor time. |
| 1813, 1821, 1833 | `$"Validating: {cmd}... "`, `"PASSED"`, `$"FAILED (exit {n})"` | (b) | Single-word status tokens; very safe. |

### `azureopenai-cli/Setup/FirstRunWizard.cs`

| Source location | String (truncated) | Category | Notes |
|---|---|---|---|
| 83 | `"Welcome to az-ai! Let's get you set up. (takes ~30 seconds)"` | (c) | Trailing duration parenthetical reads naturally in English; in German the verb-final clause makes the parenthetical attach to the wrong noun. Recommend splitting into a separate "Estimated time: 30 seconds" line. |
| 136 | `"Setup aborted -- no changes saved."` | (b) | Stable sentence. (Note: contains an em-dash today; pre-existing docs-lint debt, NOT this episode's scope.) |
| 162 | `$"Failed to save credential: {ex.Message}"` | (b) | Label + payload. The payload is an exception message which is itself unlocalized; flag as a known limitation. |
| 173, 177 | `$"Saved to {configPath} (API key DPAPI-encrypted for current user)"`, `$"Saved to {configPath} (mode 0600)"` | (b) | Trailing parenthetical pattern. Safe. |
| 179 | `"Run 'az-ai --config show' anytime to inspect settings."` | (c) | Embedded English imperative + literal command name. The command name MUST stay literal; the surrounding sentence wants translation. Split via a placeholder. |
| 209-210 | `"Azure OpenAI endpoint URL"`, `"  e.g. https://my-resource.openai.azure.com/"` | (b) | Label + example. The `e.g.` abbreviation is Latin-derived; some locales prefer a localized abbreviation. |
| 224 | `"Must be a valid https:// URL, e.g. https://my-resource.openai.azure.com/"` | (b) | Same `e.g.` note. |
| 227 | `"Too many invalid endpoints. Aborting."` | (b) | Two short sentences. Safe. |
| 256 | `"Your key will be masked as you type. Press Enter when done."` | (b) | Two full sentences. Safe. The word "Enter" refers to the key cap and is conventionally not translated. |
| 258 | `"API key (input hidden)"` | (b) | Label + parenthetical. Safe. |
| 271 | `"API key cannot be empty."` | (b) | Stable sentence. |
| 277 | `$"API key is shorter than expected ({key.Length} chars; Azure keys are typically 84)."` | (c) | Two interpolated numbers in a parenthetical clause; `chars` abbreviation is English; semicolon usage varies by locale. Refactor to two sentences. |
| 278, 433 | `"Use it anyway? [y/N]"`, `"Try again? [y/N]"`, `"Save creds anyway without validation? [y/N]"` | (c) | The `[y/N]` shortcut is English-specific; `ConfirmYesAsync` only matches `y`/`yes`. A localized prompt needs a localized accepted-input set in lockstep. |
| 343-344 | `"Model deployment name (comma-separated for multiple)"`, `"  e.g. gpt-4o,gpt-4o-mini"` | (c) | "comma-separated" is locale-sensitive: Arabic conventionally uses U+060C as the list separator. The parser accepts only U+002C. Document the constraint or accept both. |
| 357 | `"At least one model deployment name is required."` | (b) | Stable sentence. |
| 374, 390 | `"Testing connection... "`, `$"authenticated ({model} responded in {ms}ms)"` | (b)/(c) | Status header is (b). The completion line is (c): `ms` suffix is English; the parenthetical reorders awkwardly when the model name is a non-Latin string. |
| 395, 406, 411, 414, 419 | Validation failure lines | (b) | Stable sentences. Pre-existing em-dashes are docs-lint debt. |
| 448 | `"Setup cancelled -- no changes saved."` | (b) | Same em-dash note. |

### `azureopenai-cli/Credentials/*.cs`

| Source location | String (truncated) | Category | Notes |
|---|---|---|---|
| `*CredentialStore.cs` `throw new ArgumentException("API key must not be null, empty, or whitespace.", ...)` | (b) | Reaches the user via `CredentialStoreException.Message` rendered by the wizard at line 162. Safe wording; refactor target shared across stores. |
| `*CredentialStore.cs` `throw new CredentialStoreException("Failed to spawn {binary}: {ex.Message}", ...)` | (b) | Label + payload pattern. Safe to extract. |
| `Program.cs:377,382,388` `throw new ...Exception("Azure OpenAI <X> is not set...")` | (b) | Stable sentences; only surfaced if config validation fails before normal flow. |

### `azureopenai-cli/Tools/*.cs`

No `Console.*` calls. Tool output is JSON-only and locale-agnostic by
construction. The exception messages thrown from `ShellExecTool`,
`DelegateTaskTool`, and `GetClipboardTool` reach the user only via the
agent-mode error path; they fall under category (b).

### `azureopenai-cli/Squad/*.cs`, `azureopenai-cli/ConsoleIO/*.cs`

No user-facing strings; all output is routed through `Program.cs`.

## Unicode correctness in the masked-input read

`FirstRunWizard.ReadMaskedFromConsole` (lines 287-334) reads the API
key one `ConsoleKeyInfo` at a time via `Console.ReadKey(intercept:
true)` and accumulates `ki.KeyChar` into a `List<char>`. Three Unicode
edge cases were checked:

1. **Surrogate pairs** (any char outside the BMP, e.g. an emoji
   accidentally pasted into the key field). `Console.ReadKey` delivers
   the high surrogate and the low surrogate as two separate events.
   `char.IsControl` returns false for both, so both are appended. The
   final `new string(buf.ToArray())` reconstructs a valid UTF-16
   string. **No corruption.**
2. **Non-breaking space (U+00A0) as a paste artifact.** `char.IsControl`
   returns false; the NBSP is buffered. The subsequent `key.Trim()`
   call (line 267) uses `char.IsWhiteSpace`, which DOES include U+00A0.
   Trailing NBSP is stripped. **No corruption.**
3. **Lone surrogate** (e.g. a paste truncated mid-pair by a terminal
   that mishandles UTF-16). Would be appended as a single non-control
   char and survive into the saved credential as an invalid UTF-16
   string. This is a real but extremely narrow edge case; it is not a
   one-line fix (the right fix is to validate `char.IsSurrogate` pair
   completeness before appending). Filed as a follow-up in the exec
   report; intentionally NOT fixed in this episode.

The masked-input path is **not** modified by this episode.

## Next steps (NOT done in this episode)

A real l10n effort would need, in order:

1. **Centralize `[ERROR]` and `[WARN]` prefixes** through a single
   helper so the bracketed tag is one localizable token.
2. **Refactor the column-aligned help text** (`Program.cs:1239-1314`,
   `--personas` listing at `:547`, config display at `:1377-1389`)
   into label/description pairs that a renderer can lay out with
   wcwidth-correct padding for CJK and direction-aware ordering for
   RTL.
3. **Eliminate the `iteration(s)` plural shortcut** at `Program.cs:1799,
   1822` -- the canonical fix is a MessageFormat-style plural selector.
4. **Split mid-sentence interpolations** flagged as (c): the retry
   line at `:1445`, the config-source parentheticals at `:1377-1389`,
   the wizard's "shorter than expected" line at `FirstRunWizard.cs:277`,
   and the `Run 'az-ai --config show'` line at `:179`.
5. **Localize the `[y/N]` confirmation prompts** in lockstep with the
   `ConfirmYesAsync` accepted-input set.
6. **Decide on the list-separator policy** for the multi-model prompt:
   either accept both `U+002C` and `U+060C`, or document that the
   delimiter is a syntactic constant and not natural language.
7. **Add CJK and RTL test fixtures** for the help renderer, persona
   listing, and config display once any of the above ships.

None of the above lands in S02E08. This episode produced the map; the
refactor is a separate episode.

## Out of scope

- No translations were added. Zero `.po` / `.resx` / resource files.
- No `--locale` flag.
- No string was refactored. The audit names problems; the fixes ship
  later.
- No CJK / RTL test fixtures.
- The masked-input path was not modified. The lone-surrogate edge
  case is a follow-up, not a blocker.
