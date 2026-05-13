# ADR-014 -- Output Formatting Standard

- **Status:** GREENLIT (S04E04 *Reading Room*, Wave 1)
- **Date:** 2026-05-22
- **Author:** Elaine (lead, S04E04)
- **Co-leads / reviewers:** Mickey Abbott (a11y), Babu Bhatt (i18n width),
  Russell Dalrymple (UX), FDR (adversarial -- see Wave 3 appendix)
- **Linked:** ADR-012 (registry seam), ADR-013 (capability gate),
  brief `docs/episode-briefs/s04e04-reading-room.md`

## Context

The CLI is acquiring a class of read-only inspection commands -- starting
with `az-ai models {list,show,capabilities}` in S04E04 and continuing
with `--prefer` in S04E05 *The Picker* -- whose entire job is to render
internal state for two distinct audiences:

1. A human at a terminal, who needs aligned columns, sensible truncation,
   and markers that read aloud as words rather than punctuation noise.
2. A shell pipeline (`jq`, `grep`, `awk`), which needs a stable,
   machine-readable shape that survives appended fields and never has its
   keys reordered between releases.

Today's surfaces (`--doctor`, `--models`, persona list) each invented
their own formatting -- ad-hoc spacing, mixed marker glyphs (`*`, `(default)`,
unicode bullets), byte-boundary truncation that severs UTF-8 sequences,
and `(no card)` placeholders that read as prose asides instead of values.
Two findings from S04E02 (`(no card)` -> `unknown`; word-boundary
truncation) and one from S04E03 (A11Y-CG-01 -- shell-hostile chars in
model names breaking screen-reader output and shell quoting) all share
the same root cause: no canonical output contract.

ADR-014 fixes the root cause by writing the contract down. Every
read-only command added after this ADR follows the rules below. Existing
surfaces (`--doctor`, `--models`) are not retrofitted in S04E04 -- that
migration is a tracked finding, sequenced after the dual-surface
convention is proven against real output.

The decisions that follow apply equally to East-Asian-width text. CJK
fullwidth characters render in two display cells; combining marks render
in zero; ZWJ emoji sequences are one grapheme spanning many codepoints.
Naive `string.Length` misaligns all three. The renderer routes every
cell through Babu's `EastAsianWidth.DisplayWidth(string)` (S04off1) so
column boundaries land on grapheme breaks, not byte breaks.

## Decision

### Sub-command shape

Read-only inspection commands are exposed as `az-ai <noun> <verb>` rather
than `az-ai --<noun>-<verb>` flags. Sub-command trees nest under nouns
(`models list`, `models show`, `models capabilities`); legacy
single-flag inspection (`--models`, `--current-model`) is retained for
back-compat but is **not** the path new commands take.

### Column structure

Every table command emits a header row, a single dashed underline
(`-` repeated to column width), and zero or more body rows. Columns are
separated by two spaces; padding is right-padded with U+0020. No box
characters, no Unicode borders, no colour by default. The first column
is the primary identifier (Name, Capability, ...).

### Sort order

Default sort is alphabetical by the primary identifier using
`StringComparer.Ordinal`; ties break by registration / insertion order.
This is deterministic, diffable, and locale-free. `--sort` flags
(registration order, default-first) are deferred to a later episode if
a user actually asks; alphabetical is the safest default for captured-
output regression diffs.

### Marker glyphs

Markers are short ASCII words in parentheses:

- `(default)` -- this entry is the AZUREOPENAIMODEL primary
- `(allow)`   -- this entry is present in the AZUREOPENAIMODEL set

Markers are appended to the rightmost dedicated column for that signal
(`Default`, `Allowlisted`). They are **never** Unicode bullets, check
marks, asterisks, or boxed glyphs. A11Y rationale: screen readers
announce `(default)` as the word "default"; they announce `*` as
"asterisk" and `✓` as either "check mark" or silence depending on the
reader's symbol dictionary. Mickey owns the marker vocabulary; new
markers added by future ADRs must clear an a11y review before they ship.

### Empty-cell convention

Missing values render as the literal string `unknown`. Never `(no card)`,
never `n/a`, never empty. The placeholder is a value, not a parenthetical
aside. Closes the S04E02 polish observation. `--doctor` adopts the same
literal in its follow-on migration.

### Truncation

When a cell exceeds the column budget the renderer truncates at the last
whitespace or punctuation boundary before the budget, appends an ASCII
ellipsis `...` (three dots, not U+2026), and stops. If no boundary
exists, it falls back to a grapheme boundary using Babu's helper. The
byte-boundary path is removed, not retained as a fallback -- byte
truncation severs UTF-8 sequences and produces invalid output.

### Suggestion / inverted-index caps

`models capabilities` caps the Models column at five entries per
capability, suffixed `(N more; see models list)` where N is the
overflow count. Closes finding F-EE-AR-09 (unbounded suggestion lists
in capability-rejection messages); the same cap and tail text are reused
for any future capability-to-models surface. Five is empirical: it fits
an 80-column terminal without truncating most names and is long enough
to be useful.

### JSON shape

Each command's `--json` output is the canonical machine surface:

- `models list --json` -- bare JSON array of objects. Keys: `name`,
  `provider`, `capabilities` (string array), `default` (bool),
  `allowlisted` (bool). Object order matches table order.
- `models show <name> --json` -- single JSON object. Keys: `name`,
  `provider`, `family`, `capabilities`, `context_window`, `modalities`,
  `card_path`, `cost_tier`, `default`, `allowlisted`. Optional fields
  are `null`-skipped on serialize.
- `models capabilities --json` -- bare JSON object. Keys are capability
  tags in the canonical order from `ModelCapability.AllowedTags` (sorted
  ordinally); values are arrays of model names.

Property names are pinned with `[JsonPropertyName]` attributes on the
DTO records so the CamelCase default policy cannot drift the shape. All
DTOs are registered in `AppJsonContext` for AOT-safe serialization.

**JSON is append-only for the lifetime of v2.x.** Future episodes may
add keys; they may not rename, retype, or remove keys. This is the
contract a shell pipeline relies on. Breaking it is a major-version
event.

### `--raw` discipline

Under `--raw` every command emits header-less, marker-less,
truncation-less tuple lines. Tab-separated for `list` and `capabilities`;
key-tab-value for `show`. Boolean fields render as the literals `true` /
`false`. This is the surface Espanso / AHK / cron consumers parse.

### Shell-hostile model names

Registry-load rejects any model `Name` containing a single quote,
double quote, backslash, or a C0/C1 control codepoint
(`U+0000`-`U+001F`, `U+007F`-`U+009F`). Implemented in
`ModelRegistry.ValidateEntries` (Kramer's Wave 1 territory; see
ADR-012). The reject is fatal (rc=99). Rationale: model names flow
unescaped into rendered tables, screen-reader output, and shell-quoted
command examples in error messages. Filtering at the registry seam keeps
every downstream surface safe without per-surface escaping. Closes
A11Y-CG-01.

### Empty-registry and unknown-model paths

- Empty registry, `models list`: rc=0, single stderr line
  `[INFO] No models registered. See --doctor.`, no table.
- Unknown model, `models show <name>`: rc=2, single stderr line
  `[ERROR] model '<name>' not found in registry. Use 'az-ai models list'
  to see available models.`. rc=2 (not rc=1) because the input was
  well-formed and parseable -- the failure is a missing resource, not a
  malformed invocation.

### NO_COLOR / TTY

The renderer probes `NO_COLOR` (presence-only, value ignored, per the
NO_COLOR spec) and `Console.IsOutputRedirected`. Either signal disables
colour. S04E04 emits no colour; the probe is wired and tested so S04E05
inherits a working off-switch.

## Consequences

### Positive

- One contract, six surfaces (three sub-commands x table + JSON); future
  read-only commands inherit the rules with zero re-litigation.
- A11Y guarantees are localised to two ADRs (ADR-014 markers, ADR-013
  rejection text) instead of scattered across N call sites.
- JSON pipelines have a documented, append-only contract; consumers can
  pin against v2.x and trust the shape.
- The `(no card)` and byte-truncation S04E02 findings close as a
  side-effect of adopting the standard; A11Y-CG-01 closes via the
  registry-seam reject in ADR-012.

### Negative

- Two existing surfaces (`--doctor`, `--models`) drift from the standard
  until their migration ADR lands. The drift is intentional -- prove the
  convention first, retrofit second.
- An ASCII-only marker discipline forbids visually distinctive glyphs
  some users will prefer. The a11y tradeoff is not negotiable.
- The renderer's display-width path costs ~3-6 KB of AOT binary versus a
  byte-length implementation. Bania's pre-merge gate has the bound at
  35 KB total delta for the episode; we are well inside it.

## Alternatives considered

### JSON by default, table on `--table`

Rejected. The default consumer is a human at a terminal; defaulting to
JSON would mean every interactive run pipes through `jq` to be legible.

### Top-level `az-ai list`, `az-ai show`

Rejected. A bare `list` verb at the top level collides with the
ergonomic expectation that top-level verbs act on the prompt
(`az-ai "summarise this"`). Nesting under `models` (and later `prompts`,
`caches`, `personas`) keeps the noun-verb tree predictable.

### Shell-completion-driven listing

Rejected. Completion files are a derivative output, not a primary one;
generating them from the registry is fine, but using completion as the
primary surface would force every consumer through a shell layer.

## Adversarial review (FDR, S04E04 Wave 3)

> Open section; FDR appends here in Wave 3. Inputs of interest: 4096-char
> model names (rejected at registry-load), RTL-override codepoints in
> names (rejected as C1 control via U+202E filter? -- FDR confirms),
> NUL bytes in card paths (rejected as control codepoint), capabilities
> lists of 200 entries (rendered with the 5-cap + tail), and any
> grapheme that survives `EastAsianWidth.DisplayWidth` but mis-aligns
> in a real terminal.

## Backlog (carried forward from Wave 1)

- **F-EE-SP-001** (CapabilityGate.cs predicate inconsistency -- mixed
  `IsNullOrEmpty` / `IsNullOrWhiteSpace`). One-line fix; out of scope
  for E04 (different file, different owner). Filed to Maestro as a
  drive-by ask. If unaddressed by E05 greenlight, Elaine picks it up.
- **Migrate `--doctor` and `--models` to the ADR-014 contract.** Tracked
  finding; not an E04 deliverable.
- **Kramer's `EnumerateInOrder()` / `TryFind()` helpers.** `ModelsCommand`
  currently reads `Program.RegistryEntries` directly with a local linear
  scan. When Kramer's Wave 1 helpers land, swap the two marked spots in
  `Cli/ModelsCommand.cs` (`SortedEntries`, `TryFindEntry`) to delegate.
  Semantics are identical; the swap is a refactor, not a behaviour
  change.
- **Mickey's `TableRenderer.cs`.** `ModelsCommand` currently uses a
  private `RenderTableInternal` helper that aligns columns with
  `string.Length`. ASCII-safe for the embedded seed registry. When
  Mickey lands, replace the two `RenderTableInternal` call sites with
  `TableRenderer.Render(columns, rows, options)` and delete the
  internal helper.
