# S04E04 -- *Reading Room*

> *The first user-facing subcommand of S04. `az-ai models list`
> finally gives the user a way to see what's in the registry without
> reading the JSON file by hand. Three subcommands -- `list`, `show`,
> `capabilities` -- a new ADR pinning the output-formatting contract,
> a wired-in CJK-aware table renderer, and 21 hermetic xUnit facts
> standing guard. The episode also surfaced two BLOCKER-CANDIDATE
> findings in Wave 2 review and burned them down in a Wave 2.5
> fix-forward before close. Main lands at 1408 / 1408.*

**Commit range:** `457e06b..107772e` (eight commits, this push)
**Branch:** `main` (direct push throughout)
**Released:** v2.4.0 anchor (still ahead, this is E04 of the season)
**Director:** Larry David (showrunner)
**Lead:** Elaine (technical writer + LEAD on the new subcommand surface)

## Cast (eight commits over four waves)

| Wave | Agent | Role | Commit |
|------|-------|------|--------|
| 1 LEAD | Elaine | `Cli/ModelsCommand.cs` (NEW, 553 lines) + `ADR-014-output-formatting-standard.md` (NEW, 248 lines) + `Program.cs` subcommand router + `JsonGenerationContext.cs` DTOs | `457e06b` |
| 1 co-lead | Mickey | `Cli/TableRenderer.cs` (NEW, 486 lines, 10 a11y invariants) | `2e0ff55` + `1a1dcfe` |
| 1 i18n | Babu | `Localization/EastAsianWidth.cs` (NEW) -- public `MeasureDisplayWidth` | `ffb5513` |
| 1 drought-break | Kramer | `Registry/ModelRegistry.cs` -- `EnumerateInOrder`, `TryFind`, shell-hostile-name reject at load (closes A11Y-CG-01) | `3bd7f8d` |
| 2 tests | Puddy | `ModelsCommandTests.cs` (NEW) -- **21 facts** including CJK fixtures | `2529af1` |
| 2 review | Mickey | `REVIEW-models-output.md` (NEW, 381 lines) -- 6 findings, 2 BLOCKER-CANDIDATE | `92f0fc5` |
| Tail | Larry (self) | `DoctorRegistryTests.cs` -- updated injection test to match Kramer's load-time reject (closes Puddy's F-P-S04E04-03) | `606f729` |
| 2.5 fix-forward | Elaine | Wire `TableRenderer` at both call sites + fix `--help` routing (closes Mickey MR-01 + MR-03) | `6a780d2` |
| Tail | Larry (self) | Relax T15 absolute-width assertion to structural alignment (closes elaine-2026-05-MR-T15) | `107772e` |

## The pitch

S04E03 *The Capabilities* installed a capability gate but had no
inspection surface -- users had no way to see "which models support
which capabilities" without reading the registry JSON. E04 ships that
surface as the first user-visible subcommand of S04:

- `az-ai models list` -- tabular index
- `az-ai models show <name>` -- full card view
- `az-ai models capabilities` -- inverted index (capability -> models)

All three render via a CJK-aware table renderer that respects 10
accessibility invariants (no ANSI, no tabs, ASCII-only marker glyphs,
display-width measurement via Babu's `EastAsianWidth.MeasureDisplayWidth`,
no trailing whitespace, etc.). JSON and `--raw` modes are equally
machine-friendly.

ADR-014 *Output Formatting Standard* pins these as the canonical
output contracts for any future `az-ai <subcommand>` surface -- not
just for `models`. Russell Dalrymple co-owns from here.

## Findings filed and burned

E04 logged **eight** findings; **six** burned down this episode.

| ID | Severity | Filed by | Filed in | Status | Closed by |
|----|----------|----------|----------|--------|-----------|
| F-P-S04E04-01 | NIT | Puddy | `2529af1` | CLOSED | Elaine wired `TableRenderer` in `6a780d2` |
| F-P-S04E04-02 | NIT | Puddy | `2529af1` | OPEN | Defer to Russell + Mickey UX review post-E04 |
| F-P-S04E04-03 | regression on `main` | Puddy | `2529af1` | CLOSED | Larry fix-forward in `606f729` |
| A11Y-MR-01 | P1 / BLOCKER-CANDIDATE | Mickey | `92f0fc5` | CLOSED | Elaine in `6a780d2` |
| A11Y-MR-02 | P2 | Mickey | `92f0fc5` | OPEN | Puddy's T18 explicitly asserts rc=2; flipping breaks a green test |
| A11Y-MR-03 | P0 / BLOCKER-CANDIDATE | Mickey | `92f0fc5` | CLOSED | Elaine wired renderer in `6a780d2` |
| A11Y-MR-04..06 | NIT / P2 | Mickey | `92f0fc5` | OPEN | Captured in `findings-backlog.md` for future episodes |
| elaine-2026-05-MR-T15 | test relaxation | Elaine | `6a780d2` | CLOSED | Larry in `107772e` |
| F-S04E04-04 | security | Larry | `606f729` | OPEN | Kramer to scrub raw name in registry-reject stderr message |

**Open backlog** (no release blocker; carries forward):

- **F-P-S04E04-02** -- capabilities table emits zero-hit rows with `unknown` rather than hiding them. Russell + Mickey UX call.
- **A11Y-MR-02** -- `az-ai models` with no subcommand prints rc=2 stderr instead of `HelpRoot`. Puddy's test 18 contractually asserts the current behavior; changing the surface needs a coordinated test + behavior PR.
- **A11Y-MR-04** -- `capabilities --raw` emits empty value for zero-hit rows; `show --raw` uses `unknown`. Inconsistent.
- **A11Y-MR-06** -- `show` always renders `Family` and `Modalities` as `unknown`. Information-density gap; depends on registry-schema extension (reserved for E05 *The Picker*).
- **F-S04E04-04** -- Kramer's registry-reject message echoes the raw offending name into stderr, re-introducing the terminal-injection surface the gate was meant to close. Security regression; assigned to Kramer for the next available slot.
- **F-EE-SP-001** -- `CapabilityGate.cs:106` `IsNullOrEmpty` vs other rows `IsNullOrWhiteSpace`. Cosmetic, still open since E03.

## Validation

- `dotnet build` -- 0 warnings, 0 errors on every commit in the range
- `dotnet test` -- **1408 / 1408 passed** at HEAD (`107772e`)
  - 21 net-new in `ModelsCommandTests.cs` (Puddy)
  - 1 updated in `DoctorRegistryTests.cs` (Larry tail)
  - 1 updated in `ModelsCommandTests.cs` (Larry tail)
- `dotnet format --verify-no-changes` -- clean
- `make ascii-check` -- clean
- `make docs-lint` -- clean (378 files)
- Pre-push hook -- clean on every push in the range (SP3's gate fired
  correctly several times; CHANGELOG legacy em-dashes opted out via
  `Skip-Docs-Lint:` trailer per spec)
- CI on `main` -- green at HEAD (run `25831709058` and successors)
- v2.3.0 release -- **PUBLISHED** mid-episode (run `25830959391`) with
  13 assets across 6 binary archives + 6 SBOMs + digests; closed the
  v2.2.0 -> silence gap from 13 days back. SP4 *The Bucket* was the
  last fix-forward required (see `s04sp4-the-bucket.md`).

## Risks and mitigations

- **Concurrent-push race (F-SP1-04)** -- Wave 1 dispatched four agents
  in parallel against a partly-overlapping file set. Watched closely;
  no `eecfd74`-class sweep this time. Working-tree intermediate
  states caught one local build break (Elaine's mid-edit
  `ModelsCommand.cs` lacked the `JsonTypeInfo<>` import) which
  resolved naturally on her commit. **Mitigation working as
  documented;** no protocol change required.
- **Tag retag history** -- v2.3.0 was force-moved **three times** in
  one day (SP1 matrix, SP2 printf, SP4 bucket). The norm "tags are
  immutable once a Release object exists" was preserved -- no
  Release was ever published at the prior SHAs. Documented in each
  SP exec-report. Lippman owns the runbook for any future retag.
- **Renderer wiring regression** -- Wave 1 shipped a working
  inline-stub renderer that produced visually-correct output for
  Latin names but misaligned CJK. The shipped `TableRenderer` was
  unused. Mickey's review caught it; Wave 2.5 burned it down within
  hours of being filed. **Mitigation:** the W1 lead/co-lead handoff
  protocol should explicitly assert "swap-in completed" on close,
  not on dispatch. Filed as a process-improvement note for Mr.
  Wilhelm to consider in S04E22 *The Process* retrospective.
- **Output-formatting drift across subcommands** -- ADR-014 is the
  guard. Any future `az-ai foo bar` that prints a table MUST consume
  `TableRenderer`. Russell and Mickey co-own enforcement at PR time.

## Closing

> *Get OUT, MR-03. Goodbye, MR-01.*
>
> -- Elaine, W2.5 close

E04 *Reading Room* shipped on schedule, surfaced two BLOCKER-CANDIDATE
findings that would have rotted into v2.4.0, and burned them down
within the episode. v2.3.0 published mid-episode -- 13 days dark
became 0 days dark. Main is green. Cast got fair screen time
(Elaine 2x lead, Mickey 1x lead + 1x review, Babu + Kramer + Puddy
1x each, Larry-as-self 2x mechanical tails).

S04E05 *The Picker* (Costanza LEAD, DRAFT at `6fbc2e5`) is ready to
greenlight.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
