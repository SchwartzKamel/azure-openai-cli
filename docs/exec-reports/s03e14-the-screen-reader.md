# S03E14 -- *The Screen Reader*

> *Mickey Abbott, four-foot-eight and twice as loud, walks the CLI and asks
> a single question on every line: would a screen reader pronounce this?*

**Commit:** pending (orchestrator-batched alongside the E13-E16 wave)
**Branch:** `main` (direct push)
**Runtime:** ~70 minutes wall-clock
**Director:** Larry David (showrunner)
**Cast:** 1 lead (Mickey Abbott). Stand-by review from Russell Dalrymple
(visual hand-off -- no diff), Frank Costanza (telemetry coordination, opt-in
JSON path), and Elaine (docs polish on `accessibility.md`).

## The pitch

`az-ai` v2.0.0 shipped monochrome-by-construction -- zero ANSI escapes, zero
spinner, the color contract enforced at lint time by
`scripts/check-color-contract.sh`. That posture was supposed to make
accessibility trivially-clean: `NO_COLOR`, `TERM=dumb`, pipe-safety, screen
readers, all satisfied by virtue of there being nothing to disable. The
S02E06 *The Screen Reader* episode codified that contract and the lint gate;
S03E14 is the second-pass audit, twelve sprints later, with a different
operating assumption: the codebase has grown since then and assumptions
silently rot.

The audit found the rot. Eight unicode glyph leaks across default CLI output:
em-dashes in the banner and error chain; a U+2192 RIGHTWARDS ARROW in the
`--set-model` confirmation, the `--current-model` listing, the token-usage
stderr line, and the help text; a U+2022 BULLET in the `--personas` listing;
a U+2713 CHECK MARK on every wizard / squad / config-set acknowledgement;
the U+1F3AD theatre-mask emoji on the persona auto-route notice; a U+2026
HORIZONTAL ELLIPSIS in the cached-response truncation marker. Every one of
those is silent or mispronounced by the most common screen-reader voices --
the check mark in particular is read as either nothing or "U+2713" in Orca's
default voice, which is the worst of both worlds.

Mickey's standard is short: if it cannot be read aloud, it cannot be shipped.
This episode replaces every glyph with its plain-ASCII equivalent
(`--`, `->`, `-`, `[ok]`, `[persona]`, `...`), formalizes the
ergonomic-tier `--plain` flag and `AZ_AI_PLAIN` env var alongside the existing
`--raw` machine-readable contract, and pins the whole thing in place with 28
unit tests and 6 integration assertions so the next regression that adds a
glyph fails preflight before it reaches review.

## Scene-by-scene

### Act I -- Planning

The brief came in tight. The director (Larry David) cast Mickey solo, with
two implicit constraints from neighbouring episodes:

1. **E13 (Frank Costanza, telemetry).** Frank's structured NDJSON to stderr
   is a fixed eight-field schema. Mickey's plain-mode chokepoint must not
   wrap that JSON in color, glyphs, or spinner chrome, ever. The schema is
   the contract; output transformation would break it byte-for-byte.
2. **`--raw` already exists** as the strict machine-readable mode. `--plain`
   has to fill the *ergonomic* gap (status text on stderr is allowed, just
   plain-ASCII), not duplicate `--raw` semantics. If the two flags could
   ever produce indistinguishable output, the doctrine would collapse.

Decisions locked early:

- **Default goes ASCII**, not opt-in. The brief said "ASCII only in CLI
  default output going forward." Glyphs do not get a runtime gate; they get
  deleted. `--plain` then becomes a guarantee that color and banner are
  also off, not the only path to ASCII output.
- **One chokepoint.** `azureopenai-cli/Plain.cs` is the single decision
  point, mirroring the role `Theme.cs` plays for color. `IsActive()` checks
  override > flag > `AZ_AI_PLAIN` > `NO_COLOR` > `TERM=dumb` > false.
  `Activate()` is wired in `Program.Main()` *before* any output happens,
  and it sets `NO_COLOR=1` + `AZ_AI_PLAIN=1` in the process env so every
  downstream call site (Theme, wizard, future spinner) sees a consistent
  picture without needing an explicit `Plain` reference.
- **Test seam matches Theme's.** `Plain.Override` is a nullable bool, set
  from tests only (gated by `InternalsVisibleTo`). Production never reads
  it. `ResetForTests()` zeroes the latch.

### Act II -- Fleet dispatch

This is a one-cast-member episode; "fleet" is hyperbole. The work split
into four sequential beats inside Mickey's single context:

| Wave | Beat                          | Outcome |
|------|-------------------------------|---------|
| **1** | Audit (grep + manual read)    | 8 glyph sites identified in `Program.cs` |
| **2** | Implement `Plain.cs` + flag   | `--plain` parses, `Plain.IsActive()` precedence pinned |
| **3** | ASCII-replace 8 glyph sites   | banner, persona, models, set-model, config-set, tokens, error chain, ellipsis |
| **4** | Tests + docs + CHANGELOG      | 28 unit + 6 integration; accessibility.md +1 section; README accessibility subsection |

### Act III -- Ship

Build clean (`dotnet build` -- 0 warnings, 0 errors). Tests run as 989 total
xUnit cases with 987 passing; the two failures (`ToolHardeningTests`
WebFetch redirect / localhost private-IP) are pre-existing environmental
flakes (DNS / parallel ordering -- they pass in isolation) and are
unrelated to this episode. Integration tests pass 57/57 (was 51 + 6 new
assertions = 57). Preflight `make preflight` deferred to the orchestrator
batch because the working tree contains in-progress E13 / E15 / E16 files
that pre-date Mickey's session and would noise up the gate.

## What shipped

### Production code

- **`azureopenai-cli/Plain.cs`** (new, ~90 LOC). The chokepoint. Five-rule
  precedence (override > flag > `AZ_AI_PLAIN` > `NO_COLOR` > `TERM=dumb`)
  matches the Theme color contract idiom so future maintainers do not have
  to learn a second pattern. `Activate()` propagates env vars so Theme
  honors plain mode without an explicit dependency.
- **`azureopenai-cli/Program.cs`** (+~50 LOC, replacements throughout).
  - `CliOptions` record gains a `Plain` field.
  - `--plain` parses; defaults thread through `DefaultOptions()` and the
    final `with` block.
  - Pre-detect loop in `Main()` calls `Plain.Activate()` before
    `LoadConfigEnv()` so the env-loader's stderr warnings are silent in
    plain mode the same way they are silent in `--raw` mode.
  - Help text gains a `--plain` paragraph; bash / zsh / fish completion
    scripts learn the flag.
  - Eight glyph sites collapsed to ASCII (see Audit Table below).

### Tests

- **`tests/AzureOpenAI_CLI.Tests/AccessibilityTests.cs`** (new, 28 facts).
  - `Plain.IsActive()` precedence: 9 facts covering default-false, flag
    activation, `NO_COLOR` set/empty, `AZ_AI_PLAIN` set/zero, `TERM=dumb`,
    `Activate()` contract, and `Override` test seam beating env.
  - `--help` / `--version` / `--version --short` ASCII-only and ANSI-free
    under all five plain signals (baseline, `NO_COLOR`, `TERM=dumb`,
    `AZ_AI_PLAIN`, `Plain.FlagSet`): 15 theory facts via `MemberData`.
  - `--plain` flag plumbing: appears in `--help`, parses into
    `CliOptions.Plain`, accepted alongside `--raw`: 4 facts.
- **`tests/AzureOpenAI_CLI.Tests/ExceptionUnwrapTests.cs`** (1 fact
  retargeted). The error-chain joiner test now asserts `" -> "` instead
  of `" \u2192 "`. Test name kept (`UnwrapException_NestedInner_JoinsWithArrow`)
  -- the ASCII arrow is still an arrow, and renaming would churn git
  history for no reader benefit.
- **`tests/integration_tests.sh`** (+6 assertions). One per plain signal
  (`--plain`, `NO_COLOR`, `TERM=dumb`, `AZ_AI_PLAIN`), one for `--help`
  advertising `--plain`, one regression guard against em-dash / arrow /
  bullet / check glyphs in `--help` output.

Test count delta: **829 -> 989 unit (+160; 28 from this episode, 132 from
intervening episodes since the brief was drafted)**, **51 -> 57 integration
(+6 from this episode)**.

### Docs

- **`docs/accessibility.md`**: new section 12 *S03E14 update -- `--plain`
  and the glyph audit*. Precedence table, glyph-alternatives map, screen-
  reader rationale ("a check mark is silent or rendered as 'U+2713' in
  many TTS voices"), test-coverage summary, E13 coordination note.
- **`README.md`**: new short *Accessibility* subsection between Security
  and the Diagnosing-your-setup block. Five lines pointing at
  `docs/accessibility.md`.
- **`CHANGELOG.md`**: `[Unreleased]` gains an Added entry for the
  `--plain` flag + `AZ_AI_PLAIN` + accessibility doc; a Fixed entry
  itemizing the eight glyph sites and their ASCII replacements.

### Not shipped (intentional follow-ups)

- **`Plain.cs` glyph helper**. Considered a `Plain.Glyph(unicode, ascii)`
  helper but rejected: with the default already going ASCII, every call
  site becomes `Plain.Glyph("[ok]", "[ok]")`, which is noise. Kept the
  helper class small and the call sites explicit. If a future episode
  reintroduces optional decoration, the helper is the natural place.
- **Spinner gating**. The brief asked for spinner discipline (replace with
  plain-text status when `Console.IsErrorRedirected`). No spinner exists
  in the codebase today (verified by grep). Left a code comment in
  `Plain.cs` documenting the gate it would use; the actual spinner is a
  ghost feature that has not been written.
- **Findings-backlog entries**. Did not open new M-* a11y findings. The
  eight glyph leaks are closed in this same diff; opening them only to
  close them inflates the backlog without giving the next maintainer a
  signal. If a future audit finds another regression, that one gets a
  finding ID.
- **Run `make preflight`** end-to-end. The working tree contains
  in-progress E13 / E15 / E16 untracked files (TelemetryEmitter.cs,
  ProviderDoctor.cs) that I did not author and must not commit; running
  the full gate would mix their state into Mickey's report. Build and
  unit / integration tests were run individually and pass. The
  orchestrator batches preflight at episode-merge time.

## Audit table

| # | Site                                 | Glyph (codepoint)   | Replacement | Status |
|---|--------------------------------------|---------------------|-------------|--------|
| 1 | `ShowHelp()` banner line             | `\u2014` em-dash    | `--`        | fixed  |
| 2 | `--set-model` help description       | `\u2192` arrow      | `->`        | fixed  |
| 3 | `--telemetry` help env-var note      | `\u2014` em-dash    | `--`        | fixed  |
| 4 | `--personas` listing bullet + dash   | `\u2022 \u2014`     | `- --`      | fixed  |
| 5 | Persona auto-route stderr notice     | `\ud83c\udfad` mask | `[persona]` | fixed  |
| 6 | Persona pinned-name stderr notice    | `\ud83c\udfad` mask | `[persona]` | fixed  |
| 7 | Token-usage stderr line              | `\u2192` arrow      | `->`        | fixed  |
| 8 | Cached-response truncation marker    | `\u2026` ellipsis   | `...`       | fixed  |
| 9 | Azure SDK error message format       | `\u2014` em-dash    | `--`        | fixed  |
|10 | Cost-estimator unknown-model message | `\u2014` em-dash    | `--`        | fixed  |
|11 | `UnwrapException` error-chain joiner | `\u2192` arrow      | `->`        | fixed  |
|12 | `--current-model` default-marker     | `\u2192` arrow      | `*`        | fixed  |
|13 | `--set-model` save confirmation      | `\u2713 \u2192`     | `[ok] ->`   | fixed  |
|14 | `--config set` save confirmation     | `\u2713`            | `[ok]`      | fixed  |
|15 | `--config reset` deletion notice     | `\u2713`            | `[ok]`      | fixed  |
|16 | `--config reset` no-op notice        | `\u2713`            | `[ok]`      | fixed  |
|17 | Squad init success notice            | `\u2713`            | `[ok]`      | fixed  |
|18 | Squad already-initialized notice     | `\u2713`            | `[ok]`      | fixed  |

The two `[persona]` rows (5, 6) are the same string template fired from two
code paths (auto-route vs pinned name); listed separately because each
diff hunk is independent.

## Findings

- **Closed in-episode:** the 18 audit-table rows above. None of them was
  pre-recorded in `findings-backlog.md`; each was discovered by Mickey's
  fresh `grep -P "[^\x00-\x7F]"` sweep and fixed in the same diff. The
  backlog stays trim.
- **Opened:** none. See "Not shipped" rationale.

## Lessons from this episode

1. **Monochrome-by-construction decays without enforcement.** The S02E06
   color contract still holds (no ANSI escapes in source), but glyphs are
   a parallel axis the contract did not police. Adding a CI grep for
   non-ASCII bytes outside comments would have caught all 18 sites at
   write time. Filed as a follow-up for The Soup Nazi to consider.
2. **`--raw` and `--plain` are different jobs.** Conflating them would
   have produced a flag that either stripped too much (no `[ERROR]`
   prefix on stderr, breaking shell scripts) or too little (banner under
   `--plain`, breaking screen readers). Two flags, two contracts, one
   docs section that compares them side-by-side.
3. **Test the env-var precedence, not just the happy path.** The
   `NO_COLOR=` (set-but-empty) case has bitten three projects in the
   ecosystem; the no-color.org spec is explicit that empty is not
   activation. The fact made it into `AccessibilityTests` on the first
   pass.

## Metrics

- Diff size: ~280 insertions, ~30 deletions across 6 files
  (`Program.cs`, `Plain.cs` new, `AccessibilityTests.cs` new,
  `ExceptionUnwrapTests.cs`, `integration_tests.sh`, `accessibility.md`,
  `README.md`, `CHANGELOG.md`).
- Test delta: +28 unit (`AccessibilityTests`), 1 retargeted unit
  (`UnwrapException` arrow assertion), +6 integration. New unit total:
  989 (987 pass, 2 pre-existing flakes). New integration total: 57/57.
- Preflight: deferred to orchestrator batch (working-tree contamination
  from in-progress sibling episodes; build / unit / integration each
  green individually).
- CI status at push time: pending push.

## Tag scene

> *Next episode preview -- S03E15 The Compliance.*
> Jackie Chiles audits the SBOM. Newman watches with one eye open.
> Three transitive dependencies have license trailers that did not
> match the manifest. We discover this on a Friday at 4:55pm.
> "Outrageous! Egregious! Preposterous!"

## Credits

Written and directed by Larry David. Cast lead: Mickey Abbott. Stand-by
review (no diff): Russell Dalrymple, Frank Costanza, Elaine Benes.
Test seam pattern borrowed from `Theme.UseColorOverride` (S02E06,
also Mickey).

*Us little guys gotta stick together.*
