**Status:** DRAFT (E03 still filming -- this brief unblocks casting only;
greenlight pending E03 wrap)

**Date:** 2026-05-22
**Target episode:** S04E04
**Target anchor:** v2.4.0
**Lead:** Elaine -- output formatting is documentation in code form;
third lead this season, per writers-room cast-balance plan
**Co-lead:** Mickey Abbott -- a11y owns the table; column alignment,
NO_COLOR, and screen-reader behaviour are the whole episode
**Dependencies:** S04E01 *The Registry* (GREENLIT, on `main`);
S04E02 *Embedded Cards* (filming); S04E03 *The Capabilities* (filming);
S04off1 *The Translation* (Babu's East-Asian-width helper -- consumed here)

# S04E04 -- Reading Room

> Log line: We built a registry. We loaded cards into it. We even taught
> the CLI to say no when a model cannot do the thing. We never gave the
> user a way to *read* the registry. Today we open the reading room.

---

You are filming **S04E04 *Reading Room*** for `azure-openai-cli`. Working
directory: `/home/tweber/tools/azure-openai-cli`. Branch: `main` at
greenlight.

---

## The pitch

By the time this episode films, the registry has three episodes of plumbing
behind it and zero episodes of presentation in front of it. The only path a
user has today to inspect what models are registered is `az-ai --doctor`,
which is a diagnostic firehose: provider health, endpoint resolution,
credential probe, allowlist parse, registry dump, capability matrix. The
registry chunk is buried in the middle and formatted for triage, not for
discovery.

`az-ai models` is the discovery surface. Read-only. No network. No provider
health checks. No credential probes. Three sub-commands, each one answering
a single question a user can ask out loud: what is registered, what does
one model look like, and which models do a given capability. The output is
a table by default and JSON on demand, because the second user of every
discovery surface is `jq`.

This episode is also the first place the polish observations from S04E02
land in the user's face. `--doctor` showed them but `--doctor` is noisy
enough that nobody noticed: the literal string `(no card)` for an
unregistered embedded card, and a byte-boundary truncation that cuts CJK
and emoji mid-grapheme. Both get fixed here, in the surface where they
matter most.

E04 is also the foundation for E05 *The Picker*. `--prefer <capability>`
needs an answer to "which models have this capability" that does not parse
table output. `models capabilities` returns the same data structure E05
will consume. Build it once, here, with a stable JSON shape, and E05
becomes a wiring episode instead of a data-model episode.

---

## Scope (in)

- `az-ai models list` -- tabular index of registered models.
- `az-ai models show <name>` -- full card view for one model.
- `az-ai models capabilities` -- inverted index, capability -> models.
- `--json` mode on all three sub-commands, AOT-safe via `AppJsonContext`.
- East-Asian-width-aware column alignment using Babu's S04off1 helper.
- ASCII-only default output; NO_COLOR honoured throughout.
- Default-model and allowlist marker glyphs (ASCII).
- Empty-registry handling: rc=0, single explanatory stderr line.
- Polish from S04E02 folded in: `(no card)` -> `unknown`; word-boundary
  truncation everywhere a string is shortened to fit a column.
- ADR-014 *Output Formatting Standard* drafted (not committed) -- proposes
  the table/JSON dual-surface convention for future read-only commands.

## Scope (out)

- `az-ai models add` / model creation -- future episode.
- `az-ai models rm` / model deletion -- future episode.
- `az-ai models pull` / remote registry sync -- explicitly punted; see
  open question 2.
- Caching of remote card fetches -- the registry is local and embedded;
  no network in E04.
- `--prefer <capability>` semantics -- S04E05 *The Picker*.
- Smart auto-selection from prompt content -- S04E05.
- Reconciliation with the legacy S03E18 `ProviderCapabilities` matrix --
  tracked finding from E03; not reopened here.
- Editing model cards from the CLI -- cards remain authoritative on disk.
- Colour output. ASCII-only by default; if colour is added later, it is
  opt-in and gated by terminal-capability detection, not by this episode.

---

## Acceptance criteria

1. `make preflight` exits 0.
2. `az-ai models list` prints a table with columns
   `Name | Provider | Capabilities | Default | Allowlisted` in that order,
   ASCII-only, with column widths computed against display width (not byte
   length) using Babu's East-Asian-width helper from S04off1.
3. `az-ai models list --json` emits a JSON array of objects with stable
   keys `name`, `provider`, `capabilities` (array of strings), `default`
   (bool), `allowlisted` (bool). Object order matches table order.
4. `az-ai models show <name>` prints a card view with sections
   Provider, Family, Capabilities, Context Window, Modalities, Card Path
   (relative to repo root). Unknown fields render as the literal `unknown`,
   never `(no card)`, never empty.
5. `az-ai models show <name> --json` emits a single JSON object with the
   same keys as the card view (snake_case: `context_window`, `card_path`).
6. `az-ai models show <unknown-name>` exits rc=2 with a single stderr line
   naming the unknown model and pointing at `az-ai models list`.
7. `az-ai models capabilities` prints a two-column table
   `Capability | Models` where the Models column is a comma-separated list,
   word-boundary truncated to fit the terminal width (never byte-boundary).
8. `az-ai models capabilities --json` emits a JSON object whose keys are
   capability tags and whose values are arrays of model names. Key order
   is the canonical order from `ModelCapability.AllowedTags`.
9. Sort order for `models list` and the Models column of
   `models capabilities` is stable and deterministic: alphabetical by name,
   ordinal comparison, ties broken by registration order. Documented in
   the `--help` text.
10. Empty registry: `az-ai models list` exits rc=0 with a single stderr
    line `[INFO] No models registered. See --doctor.` and no table.
11. Default marker glyph is the ASCII string `(default)` appended to the
    `Default` cell. Allowlist marker glyph is `(allow)` appended to the
    `Allowlisted` cell. Neither uses Unicode bullets, check marks, or
    boxed glyphs. Documented in ADR-014 draft.
12. `NO_COLOR=1` and absence of a TTY both suppress any future colour
    output; in E04 there is no colour to suppress, but the env probe is
    wired and tested so E05 inherits it.
13. All output paths respect `--raw`: under `--raw`, no headers, no
    marker glyphs, no truncation -- raw newline-separated tuples only.
14. AOT binary growth <= 30 KB versus the pre-E04 baseline. No new
    embedded resources. No new `AppJsonContext` types beyond the three
    DTOs required for `--json` output.
15. Sub-command help strings (`models`, `models list`, `models show`,
    `models capabilities`) are Bookman-tier brevity: one-line synopsis,
    one-paragraph description, examples block. Wording owned by Bookman.

---

## Dispatch plan (sub-agents and files)

| Wave | Agent | Files (file-disjoint) | Scope |
|------|-------|-----------------------|-------|
| 1 | **Elaine (lead)** | `azureopenai-cli/Cli/ModelsCommand.cs` (NEW); ADR-014 draft skeleton at `docs/adr/ADR-014-output-formatting-standard.md`; help-text strings (Bookman reviews wording) | Command surface, sub-command dispatch, table rendering call, JSON rendering, empty-registry path, `--raw` path. Owns the user-visible output end to end. |
| 1 | **Mickey (co-lead)** | `azureopenai-cli/Cli/TableRenderer.cs` (NEW); a11y test fixtures | Display-width column alignment, NO_COLOR/TTY probe (wired, no colour yet), screen-reader output review, marker-glyph ASCII discipline. File-disjoint from Elaine; Elaine calls into `TableRenderer`. |
| 1 | **Babu Bhatt** | `azureopenai-cli/Localization/EastAsianWidth.cs` -- consumed only; one-line addition if S04off1 left a TODO, otherwise read-only | Folds S04off1 width helper into `TableRenderer`. If the helper needs a new public surface, Babu adds it; nobody else touches that file. |
| 1 | **Kramer** | `azureopenai-cli/Registry/ModelRegistry.cs` -- add `EnumerateInOrder()` and `TryFind(string name, out entry)` if not already exposed; no other edits | Read-only registry helpers. File-disjoint from E03's `ModelsWithCapability` work; coordinate via shared-file protocol if E03 has not landed. |
| 1 | **Lt. Bookman** | `--help` strings only -- inline literals inside `ModelsCommand.cs`, supplied to Elaine as a unified diff fragment, never as a separate write | Brevity discipline on every help string. No `Sure, here is...`. Examples block is three lines max. |
| 2 | **Puddy** | `tests/AzureOpenAI_CLI.Tests/ModelsCommandTests.cs` (NEW); width fixtures in `tests/AzureOpenAI_CLI.Tests/Fixtures/CjkRegistry.json` | xUnit suite covering all 15 acceptance criteria. CJK-width assertions get their own theory with mixed half-width / full-width / combining-mark / emoji-ZWJ rows. `--json` shape assertions use `JsonDocument`, not regex. |
| 2 | **Mickey (a11y review)** | Review-only of rendered output; may file findings; no code edits in Wave 2 | NVDA / VoiceOver dry-read pass on the table; verifies marker glyphs read as words, not symbol noise. Findings go to Elaine for Wave 3 edits. |
| 3 | **FDR (optional)** | Append-only to ADR-014 (Adversarial appendix) | Adversarial inputs: 4096-character model name, RTL-override codepoints, NUL bytes in card paths, capabilities list of 200 entries. If anything renders weirdly, ADR-014 records the bound and Elaine adds a guard. |

Shared-file note: only Elaine touches `ModelsCommand.cs`. Only Mickey
touches `TableRenderer.cs`. Babu owns `EastAsianWidth.cs` outright.
Kramer's registry edits are additive and file-disjoint from E03's gate
work; if E03 has not landed at greenlight, hold Kramer's edits until E03
wraps and rebase. Bookman supplies help strings as a diff fragment; he
does not open `ModelsCommand.cs` directly.

---

## Risks and mitigations

- **Column alignment under CJK / emoji widths.** A full-width character
  is two display cells, a combining mark is zero, an emoji ZWJ sequence
  is one grapheme spanning multiple codepoints. Naive `string.Length`
  misaligns every one of them. *Mitigation:* `TableRenderer` calls
  Babu's `EastAsianWidth.DisplayWidth(string)` from S04off1 for every
  cell. Puddy's fixture exercises half-width, full-width, combining
  marks, and at least one ZWJ emoji.
- **Bania perf risk: `models list` must be O(n) over registry.** No
  synchronous I/O. No card re-parse. No reflection scan. The registry
  is already in memory after startup; this command reads it and prints
  it. *Mitigation:* baseline a 200-entry synthetic registry pre- and
  post-merge; fail the PR if `models list` exceeds 25 ms cold.
- **Russell brand consistency.** Other table-emitting surfaces today
  (`--doctor`, persona list) use ad-hoc formatting. *Mitigation:*
  ADR-014 draft proposes the dual-surface convention but does not
  retrofit prior commands; Russell signs off on the table style as the
  forward standard and files a finding to migrate older surfaces in a
  later season.
- **Truncation surprises the user.** A model with 14 capability tags
  overflows the column, gets truncated, and the user does not realise
  the list is incomplete. *Mitigation:* truncated cells end with the
  ASCII ellipsis `...` (three dots) and the full list is one
  `models show <name>` away. Documented in `--help`.
- **JSON shape churn between E04 and E05.** If E04 ships a JSON shape
  and E05 needs to extend it, additive changes are safe but reorders
  or renames break pipelines. *Mitigation:* ADR-014 declares the JSON
  shape append-only for the lifetime of v2.x; E05 may add keys, never
  rename or remove.
- **Allowlist semantics confusion.** A model can be registered but not
  in `AZUREOPENAIMODEL`; the table marks it `(allow)` only when it is.
  *Mitigation:* `--help` for `models list` includes one sentence
  distinguishing registration from allowlisting, with a pointer at
  `AZUREOPENAIMODEL` env-var docs.

---

## Polish observations folded in (from S04E02)

Two findings carried forward from S04E02 manifest most visibly in this
surface:

1. **`(no card)` -> `unknown`.** The placeholder appears in `--doctor`
   today and reads as a parenthetical aside instead of an answer. In
   `models show` it is the value of a field. The field value is
   `unknown` -- no parentheses, no negation, no apology. Same change
   applies to `--doctor` output for consistency; one-line patch,
   Kramer owns it as a follow-on to his registry helper additions.
2. **Word-boundary truncation.** Today `--doctor` truncates at byte
   boundaries, which severs UTF-8 sequences and CJK graphemes. The
   new `TableRenderer` truncates at the last whitespace or punctuation
   boundary before the budget; if no boundary exists, it falls back to
   grapheme-boundary truncation using Babu's helper. The byte path is
   removed, not retained as a fallback.

Both fixes are scoped to surfaces this episode touches. Migrating other
existing surfaces (persona list, agent trace) is a tracked finding, not
an E04 deliverable.

---

## Open questions

1. **JSON schema shape.** The acceptance criteria pin keys and types,
   but the wrapper is open: bare array vs `{ "models": [...] }`
   envelope. Bare array is friendlier to `jq`; envelope is friendlier
   to future pagination. Recommendation: bare array for `list` and
   `capabilities`, bare object for `show`. Decision goes in ADR-014
   before Wave 1 starts.
2. **`az-ai models pull <name>`?** Tempting to land remote card fetch
   here as a fourth sub-command. Recommendation: punt to a later
   episode. E04 stays read-only and offline; `pull` is a write surface
   and a network surface and earns its own brief.
3. **Default sort order: alphabetical or registration order?**
   Acceptance criterion 9 specifies alphabetical with registration-order
   tiebreak. Alternative: registration order with alphabetical fallback
   in `--json` only. Recommendation: alphabetical default, expose
   `--sort registration` later if a user actually asks for it. Decision
   is reversible; alphabetical is the safer default for diffability of
   captured output.

---

## Linked ADRs

- ADR-012 -- model registry seam (S04E01; foundational)
- ADR-013 -- capability gate (S04E03; drafting at time of this brief)
- ADR-014 -- output formatting standard (proposed in this episode;
  DRAFT only at greenlight, not committed until E04 wraps and the
  table/JSON conventions are proven against real output)

---

## References

- `docs/episode-briefs/s04e01-the-registry.md` -- registry foundation
- `docs/episode-briefs/s04e03-the-capabilities.md` -- capability gate
  sibling episode; reuses `ModelsWithCapability` helper indirectly
- `docs/episode-briefs/s04off1-the-translation.md` -- Babu's
  East-Asian-width helper consumed here
- `azureopenai-cli/Registry/ModelRegistry.cs` -- registry surface
- `azureopenai-cli/Registry/ModelCapability.cs` -- canonical tag order
- `.github/skills/episode-brief.md` -- canonical brief format
- `.github/agents/elaine.agent.md` -- lead persona; clarity standard
- `.github/agents/mickey.agent.md` -- a11y co-lead
- `.github/agents/babu.agent.md` -- i18n / width helper

---

## Validation

```bash
# ASCII punctuation -- 0 matches required
grep -nP '[\x{2018}\x{2019}\x{201C}\x{201D}\x{2013}\x{2014}]' \
  docs/episode-briefs/s04e04-reading-room.md

# Markdownlint clean
NODE_OPTIONS="--max-old-space-size=4096" \
  npx markdownlint-cli2 docs/episode-briefs/s04e04-reading-room.md
```

---

## Tension with E05 to reconcile at greenlight

E05 *The Picker* introduces `--prefer <capability>` and a model
auto-selector. The selector needs the same capability-to-models index
that `models capabilities --json` emits here. If E05's brief specifies
a different JSON shape for that index, one of the two has to give.
Recommendation: E04 owns the shape, E05 consumes it. ADR-014 freezes
the shape append-only so E05 can extend without breaking. Showrunner
confirms at greenlight that E05's draft is compatible; if not, E04
holds until reconciled.

---

## On greenlight

Status header flips to GREENLIT. Dependencies block flips to confirm
E03 wrapped. ADR-014 open question 1 resolves before Wave 1 starts.

## On completion

```sql
UPDATE todos SET status = 'done' WHERE id = 's04e04-brief';
```

Return to showrunner: commit SHA, line count, the three sub-commands
locked in, and any reconciliation needed against E05's brief.
