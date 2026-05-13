# A11Y review: `az-ai models` subcommand output

**Episode:** S04E04 *Reading Room* -- Wave 2
**Reviewer:** Mickey Abbott
**Surface:** the `az-ai models` subcommand tree built by
`AzureOpenAI_CLI.Cli.ModelsCommand` (`list`, `show <name>`, `capabilities`)
with the three output paths -- default table, `--json`, `--raw` -- plus the
empty-registry / unknown-model error tails.
**Status:** YELLOW. Six findings filed. Two are **BLOCKER-CANDIDATE** for
the v2.3.0 release contract: A11Y-MR-01 (help discoverability) and
A11Y-MR-03 (table renderer not wired -- CJK names misalign).
**Cross-link:** [ADR-014 Output formatting standard](../adr/ADR-014-output-formatting-standard.md);
prior review [REVIEW-capability-rejection.md](REVIEW-capability-rejection.md).

## Method

Binary built from `8aec375` (W1 + SP4 telemetry fix) with `dotnet build -c
Release azureopenai-cli/AzureOpenAI_CLI.csproj`, launched via the apphost at
`azureopenai-cli/bin/Release/net10.0/az-ai` with
`DOTNET_ROOT=/usr/lib/dotnet`. Each invocation was captured into separate
stdout/stderr files and inspected with `cat -A` (tabs/CR/EOL visible) and
`grep -P '\x1b'` (ANSI escapes). Three fixture registries: the embedded
seed (gpt-4o-mini, gpt-5.4-nano, llama-local); a CJK + RTL override
(`gpt-cjk-<2 CJK chars>`, `qwen-7b-zh`, `rtl-<4 Hebrew chars>`); a
shell-hostile override carrying `name: "ev'il-model"`; a synthetic 7-row
registry to exercise the capability overflow tail.

## 1. `az-ai models list` (default table)

```text
Name          Provider  Capabilities                                     Default    Allowlisted
------------  --------  -----------------------------------------------  ---------  -----------
gpt-4o-mini   azure     json_mode, streaming, system_prompt, tool_calls  (default)  (allow)
gpt-5.4-nano  azure     json_mode, streaming, system_prompt, tool_calls
llama-local   local     streaming, tool_calls                                       (allow)
```

**Screen-reader walkthrough (NVDA / VoiceOver / Orca, default verbosity):**

> "Name space space space ... Provider space space Capabilities space
> space ... Default space space Allowlisted. Dash dash dash ... blank.
> g p t dash four o dash mini space space azure space space json
> underscore mode comma streaming comma system underscore prompt comma
> tool underscore calls space space open paren default close paren space
> space open paren allow close paren."

Column labels and capability tags are pure ASCII words separated by spaces
and commas -- screen readers chunk them at word boundaries. The
`(default)` / `(allow)` markers are read as "open paren default close
paren" by all three readers; they survive a monochrome terminal and
convey their state by word, not by color.

**NO_COLOR confirmation:** `grep -cP '\x1b' list.out` = 0. Output is
byte-identical with and without `NO_COLOR=1` (verified by `cmp`). The
renderer emits no ANSI sequences, so `--no-color` and `NO_COLOR` are
honored by construction (invariant 1).

**Tab-free:** `grep -cP '\t' list.out` = 0 (invariant 2).

**Pipeable:**

```bash
az-ai models list | grep '^gpt'
# gpt-4o-mini   azure     json_mode, streaming, system_prompt, tool_calls  (default)
# gpt-5.4-nano  azure     json_mode, streaming, system_prompt, tool_calls
```

Stable left-anchored name column makes the row addressable.

**Empty-cell rendering:** verified -- when capabilities is the empty array
the cell prints the `Unknown` constant (`"unknown"`) per ADR-014. Default /
Allowlisted empties render as blank (the marker is the signal; absence is
the unmarked default state). Acceptable per criterion 4 in the brief.

## 2. `az-ai models list --json`

```json
[
  {
    "name": "gpt-4o-mini",
    "provider": "azure",
    "capabilities": ["json_mode", "streaming", "system_prompt", "tool_calls"],
    "default": true,
    "allowlisted": true
  }
]
```

Pretty-printed, two-space indent (System.Text.Json default), booleans not
strings, capabilities in ordinal-sorted canonical order. `jq '.[] |
select(.default) | .name'` returns `"gpt-4o-mini"` cleanly. Stderr is
empty under `--json` (the cardPath WARN that prints on the default table
path is suppressed; verified with a CJK registry that has missing
cardPath entries -- 0 stderr bytes).

**Screen-reader:** JSON is announced character-by-character at the
verbosity setting `--json` consumers typically use (a pipe to `jq`); not a
human-readable surface; no concerns.

## 3. `az-ai models list --raw`

`cat -A` rendering:

```text
gpt-4o-mini^Iazure^Ijson_mode,streaming,system_prompt,tool_calls^Itrue^Itrue$
gpt-5.4-nano^Iazure^Ijson_mode,streaming,system_prompt,tool_calls^Ifalse^Ifalse$
llama-local^Ilocal^Istreaming,tool_calls^Ifalse^Itrue$
```

Five TSV columns: name, provider, comma-joined caps, default(bool),
allowlisted(bool). One row per model, LF-terminated (no CRLF, no extra
trailing blank line). `cut -f1` / `awk -F'\t'` work without quoting.
Booleans are the literals `true`/`false` (lowercase, ordinal). No headers,
no markers, no truncation -- matches the criterion-3 raw contract.

## 4. `az-ai models show gpt-4o-mini`

```text
gpt-4o-mini
-----------
Provider       : azure
Family         : unknown
Capabilities   : json_mode, streaming, system_prompt, tool_calls
Context Window : 128000
Modalities     : unknown
Card Path      : docs/model-cards/azure-gpt-4o-mini.md
Cost Tier      : low
Default        : yes (default)
Allowlisted    : yes (allow)
```

Title + underline + a left-aligned key column padded to 14 chars,
colon-space separator, value. Screen readers announce the underline as
"dash dash dash dash dash dash dash dash dash dash dash" -- audible but
not informative. Acceptable: the underline doubles as a visual H2 and the
preceding title line carries the same information. The colon separator
gives Orca a reliable label/value pause.

`--json` form: AOT-safe, nullable fields (`card_path`, `cost_tier`)
omitted when unknown, snake_case keys per ADR-014 criterion 5.

`--raw` form: ten `key<TAB>value` lines, sentinel literal `unknown` for
missing fields. Round-trips through `awk -F'\t' '{kv[$1]=$2} END {print
kv["context_window"]}'` cleanly.

## 5. `az-ai models show <unknown>` (error path)

stderr:

```text
[ERROR] model 'no-such-model' not found in registry. Use 'az-ai models list' to see available models.
```

stdout empty, rc=2. Single line, 96 chars, pure ASCII. The `[ERROR]`
prefix + suggestion-to-list affordance matches the pattern from
S04E03's capability rejection (`REVIEW-capability-rejection.md`). The
`A11Y-CG-01` apostrophe escape risk in the model token is closed at
**load time** by Kramer's `3bd7f8d` -- a hostile name with `'` cannot
reach this code path. Verified directly (see Section 8).

## 6. `az-ai models capabilities`

```text
Capability     Models
-------------  --------------------------------------
json_mode      gpt-4o-mini, gpt-5.4-nano
streaming      gpt-4o-mini, gpt-5.4-nano, llama-local
system_prompt  gpt-4o-mini, gpt-5.4-nano
tool_calls     gpt-4o-mini, gpt-5.4-nano, llama-local
vision_in      unknown
vision_out     unknown
```

Inverted index; capability tags ordinal-sorted; per-row model lists
ordinal-sorted. Empty cells correctly render the `unknown` literal (not
`-`, not `n/a`, not blank). Capability tags are stable ASCII tokens
shared with `--tools` flag rejection messages -- a user grepping
`vision_in` here and in `--doctor` gets the same string.

**Overflow tail.** With a 7-row synthetic registry, two capabilities
overflow `CapabilitiesRowCap = 5`:

```text
streaming      m0-tool, m1-tool, m2-tool, m3-tool, m4-tool (2 more; see models list)
tool_calls     m0-tool, m1-tool, m2-tool, m3-tool, m4-tool (2 more; see models list)
```

Format is exactly `<head> (N more; see models list)` -- single
parenthesized clause, semicolon-separated, lowercase, no ellipsis glyph.
Screen reader: "open paren two more semicolon see models list close
paren". Matches ADR-014 prescription.

## 7. `az-ai models capabilities --json` / `--raw`

JSON: object keyed by capability name, value is an ordinal-sorted string
array. Empty arrays are emitted (`"vision_in": []`) -- distinguishes
"capability exists, no models" from "capability not in registry."

Raw: one `cap<TAB>csv` line per capability. For zero-hit capabilities the
value side is an empty string (trailing tab then newline). See A11Y-MR-04.

## 8. CJK / RTL / shell-hostile fixtures

**Shell-hostile reject (Kramer `3bd7f8d`):** registry override with
`name: "ev'il-model"` produces:

```text
[ERROR] registry rejected: model name 'ev'il-model' contains shell-hostile character at offset 2
rc=99
```

Fatal at load, never reaches the renderer. **Closes A11Y-CG-01 from
S04E03 as specified.** Note that the rejection message itself still
contains a bare apostrophe inside the single-quoted token (so the message
is not itself round-trippable through shell variables); however, this is
rc=99 *fatal* output read by a human looking at a misconfigured registry,
not stderr piped into Espanso. Acceptable.

**CJK names (Babu `ffb5513` -- `MeasureDisplayWidth`):** the override
carries `name: "qwen-7b-zh"` (pure ASCII; loads fine) and two
multi-codepoint names embedded via JSON `\u` escapes. The CJK row
**reaches the renderer and renders**, but the columns misalign because
the renderer is the wrong renderer. See A11Y-MR-03.

## 9. First-run discoverability

`az-ai models` with no subcommand emits a single line to stderr:

```text
[ERROR] models requires a subcommand: list | show | capabilities. Run 'az-ai models --help'.
```

rc=2. The hint at the end is **wrong** in operation: `az-ai models
--help` does not print the models help -- see A11Y-MR-01. The
combination of MR-01 and MR-02 leaves a first-time screen-reader user
with no path to discovery short of reading the source. This is the
single biggest a11y regression in Wave 2.

## Findings

### A11Y-MR-01 -- `models --help` and `models help` print the global help, not the models help

- **Severity:** P1 -- **BLOCKER-CANDIDATE** for v2.3.0.
- **Symptom:** the carefully-written `HelpRoot` string in
  `ModelsCommand.cs` (lines 497-519: subcommand list, flag table,
  examples, ADR-014 pointer) is **dead code** at runtime. `az-ai models
  --help` and `az-ai models help` both print the 220-line global
  `ShowHelp()` body that knows nothing about the `models` surface.
- **Root cause:** `Program.cs` line 136 has an ultra-early
  `args.Any(a => a is "--help" or "-h" or "help")` short-circuit that
  fires *before* the `args[0] == "models"` route at line 219.
- **Recommendation (do not implement; file for E05):** in `Program.cs`
  gate the early `--help` bailout on `args[0] is not "models"`, or
  move the `ModelsCommand.Run` dispatch above it. File-disjoint, one
  conditional. Then re-run this review against the help output.
- **Affected users:** every first-time user; every screen-reader user
  who follows the stderr "Run 'az-ai models --help'" affordance from
  MR-02; every CI script that scrapes `--help`.

### A11Y-MR-02 -- `models` with no subcommand is a dead end

- **Severity:** P2.
- **Symptom:** rc=2, single stderr line, no usage block. The stderr line
  *names* the three subcommands so it is technically discoverable, but
  the trailing hint ("Run 'az-ai models --help'") is broken by MR-01.
- **Recommendation (file for E05):** when no subcommand and no error
  flags, print `HelpRoot` to **stdout** and exit **0**. Matches
  `git`, `kubectl`, `docker`. Keep the rc=2 error form for *unknown*
  subcommands (which is correctly distinguished by `FailUsage`).
- **Cross-link:** fixes itself once MR-01 is fixed *and* the no-arg
  branch in `Run` is rewired to render help instead of failing.

### A11Y-MR-03 -- `ModelsCommand` uses a stub renderer; CJK / wide chars misalign

- **Severity:** P0 -- **BLOCKER-CANDIDATE** for v2.3.0.
- **Symptom:** Wave 1 shipped `Cli/TableRenderer.cs` (commits `2e0ff55`
  and `1a1dcfe`) wired to Babu's `EastAsianWidth.MeasureDisplayWidth`.
  Elaine's `Cli/ModelsCommand.cs` ships with an **inline private
  `RenderTableInternal`** that measures cell width with `string.Length`
  (codepoints, not display columns). The two `// >>> Kramer` swap
  markers at lines 295 and 314 were honored; the equivalent
  "swap to Mickey's renderer" handoff documented at lines 22-31 and
  430-437 of the same file was **not** -- the stub is still called from
  lines 149 and 289.
- **Reproduction:** override registry with a name containing 2 CJK
  characters (each is display-width 2). The `list` table renders the
  CJK row two columns wider than the underline; downstream cells in the
  same row shift right; subsequent ASCII rows look correctly aligned to
  the underline but **not** to the CJK row above them. Real output:

  ```text
  Name        Provider  Capabilities ...
  ----------  --------  -------------...
  gpt-cjk-XX  local     streaming
  qwen-7b-zh  local     streaming, tool_calls
  ```

  (XX represents 2 CJK characters; in the actual terminal the `local`
  on the CJK row is offset two columns right of the `local` on the
  qwen row. Verified with `cat | hexdump` on the captured output.)
- **Consequences for the 10 a11y invariants from
  TableRenderer.cs:** invariants 1 (no ANSI), 2 (no tabs), 5 (UTF-8
  clean), 7 (LF line ends) hold incidentally because the stub does not
  emit color/tabs either. Invariants 3 (display-width column
  alignment), 4 (combining-mark width), 6 (graceful empty-cell render),
  8 (last-column trim) are **bypassed entirely** -- they cannot be
  re-validated "in operation here" as the brief asks, because the
  surface never calls into them.
- **Recommendation (file for E05; this is a ~4-line change):** in
  `ModelsCommand.RenderTableInternal`, replace the body with
  `TableRenderer.Render(columns, rows, new TableRenderer.Options {
  NoColor = true });` (or whatever the public shape lands at). The
  two call sites at lines 149 and 289 do not change. Add a regression
  test asserting that a CJK-named row's column-2 cell starts at the
  same display column as the underline boundary.
- **Why BLOCKER-CANDIDATE:** ADR-014 explicitly promises
  EastAsianWidth-correct rendering as a v2.3.0 deliverable; the
  registry validator already accepts CJK names (only `'`, `"`, `\`, and
  C0/C1 controls are rejected per Kramer's `3bd7f8d`); the renderer is
  shipped and unused. A user with a single CJK card override hits this
  on first run.

### A11Y-MR-04 -- `capabilities --raw` zero-hit rows emit empty string instead of `unknown`

- **Severity:** NIT.
- **Symptom:** `cat -A` shows `vision_in^I$` and `vision_out^I$` --
  trailing tab, empty value. `show --raw` uses the literal `unknown`
  for the same semantic ("field has no value"). The raw contract is
  inconsistent across the three subcommands.
- **Recommendation (file for E05):** decide one convention and pin it
  in ADR-014. Two viable options: (a) emit `unknown` everywhere
  including capability raw, matching `show --raw`; (b) emit empty
  everywhere, matching TSV convention. Option (a) is friendlier to
  `grep`-based filters (`grep -v 'unknown$'`); option (b) is friendlier
  to numeric-aware parsers. I lean (a) for consistency with the
  already-shipped `show --raw`.

### A11Y-MR-05 -- trailing whitespace on non-final table rows

- **Severity:** NIT.
- **Symptom:** every non-final table row ends with
  `PadRight(widths[last])` spaces because the stub renderer
  unconditionally pads the last column. `grep -nE ' +$'` on the
  captured `list.out` and `caps.out` hits every body row.
- **Recommendation:** trim the trailing pad on the last column. This
  is invariant 8 in Mickey's TableRenderer; folded into the MR-03 fix
  (once the stub is gone, this finding disappears for free).

### A11Y-MR-06 -- `show` always renders `Family` and `Modalities` as `unknown`

- **Severity:** P2.
- **Symptom:** the `show` card has eight rows of metadata; two are
  *always* `unknown` in v2.3.0 because `ModelRegistryEntry` does not
  carry the fields yet (the source comment at lines 173-174 says
  "reserved for E05"). A user inspecting any model sees a
  half-populated card. Screen-reader announce is fine ("Family colon
  unknown. Modalities colon unknown.") -- the issue is *information
  density*, not accessibility per se.
- **Recommendation (file for E05):** either (a) suppress the two rows
  when the registry shape lacks them, or (b) keep them but document in
  `--help` / `HelpRoot` that they fill in once cards declare them.
  Coordinate with Costanza on whether the v2.3.0 contract advertises
  these two fields at all.

## Summary

- All three subcommands produce ANSI-free, tab-free (outside `--raw`),
  pure-ASCII output on the seed registry.
- Pipeability is sound for grep/awk/jq/cut on the three default fixtures.
- Stream separation (stdout = data, stderr = WARN/ERROR/INFO) is clean
  across all output modes including `--json`.
- The two BLOCKER-CANDIDATE findings (MR-01 help routing; MR-03 stub
  renderer) are both **single-file, file-disjoint** fixes in Wave 1
  files that are already on `main` -- they do not require new design.
- Six findings filed: A11Y-MR-01 (P1, BLOCKER-CANDIDATE),
  A11Y-MR-02 (P2), A11Y-MR-03 (P0, BLOCKER-CANDIDATE),
  A11Y-MR-04 (NIT), A11Y-MR-05 (NIT), A11Y-MR-06 (P2).

Us little guys gotta stick together.

Skip-Exec-Report: intra-episode S04E04 Wave 2
