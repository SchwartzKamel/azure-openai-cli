# S04E03 -- *The Capabilities*

> *The gate decides what a model can do before it tries to do it. The
> rejection is one line, ASCII, and shows you the way out. Three waves,
> six agents, one verified-PoC MEDIUM filed for E04 -- the registry
> finally has teeth.*

**Commit range:** `5e71331..HEAD` (this commit)
**Branch:** `main` (direct push)
**Runtime:** ~70 min wall-clock end-to-end
**Director:** Larry David (showrunner)
**Cast (in order of dispatch):**

| Wave | Agent | Role | Commit |
|------|-------|------|--------|
| 1 | Bookman (LEAD) | `CapabilityRejection.Build` + ADR-013 skeleton + 4 unit tests | `5914165` |
| 1 | Maestro (co-lead) | `CapabilityGate.Check` + single Program.cs insertion at line 751 | `eecfd74` (race-swept) |
| 1 | Kramer | `ModelRegistry.ModelsWithCapability(tag)` helper | `eecfd74` (race-swept) |
| 2 | Puddy | `CapabilityGateTests` -- 19 integration facts; suite 1387/0 | `befac7f` |
| 2 | Mickey | `CapabilityGateAccessibilityTests` + `REVIEW-capability-rejection.md` | `134b282` |
| 3 | FDR | ADR-013 adversarial-review appendix -- 11 findings, 0 CRITICAL | `6974851` |
| Close | Larry David (this report) | Exec-report + CHANGELOG | this commit |

Parallel docs-only dispatch (brief writers for the slate):

| Wave | Agent | Role | Commit |
|------|-------|------|--------|
| Prep | Elaine | S04E04 *Reading Room* DRAFT brief | `c0cae21` |
| Prep | Costanza | S04E05 *The Picker* DRAFT brief | `6fbc2e5` |
| Prep | Peterman | S04E07 *The Fallback* DRAFT brief | `eecfd74` |

## The pitch

S04E01 introduced typed model entries with capability tags. S04E02
made `--doctor` actually read the cards. But until tonight, the CLI
would happily accept `--tools` against a model that had never claimed
the `tool_calls` capability -- the upstream provider would then reject
the call with rc=99 and a generic 400, blaming Azure for what was
really a configuration mistake at the user's end.

S04E03 closes that gap. A startup-time gate, fired *after* model
resolution and persona pinning but *before* `BuildChatClient`, checks
each capability-bearing flag (`--tools`/`tool_calls`,
`--schema`/`json_mode`, `--stream`/`streaming` reserved,
`--system-prompt`/`system_prompt`) against the resolved model's
declared capability tags. On mismatch: rc=2, one-line ASCII rejection,
and a suggestion list filtered to the `AZUREOPENAIMODEL` allowlist
intersected with models that support the missing capability. Empty
intersection emits `no configured model supports this; see --doctor`.

## What shipped

### Code (Wave 1)

- **`azureopenai-cli/Cli/CapabilityRejection.cs`** (NEW, Bookman, 4
  unit tests): pure function `Build(flag, capability, model,
  suggestions) -> string`. ASCII-only, single-line, prefix budget
  240 chars before the suggestion tail. Internal `Scrub` strips
  C0/C1 control chars (terminal-injection defense -- closes the
  same class of finding as S04E01's F-02).
- **`azureopenai-cli/Capabilities/CapabilityGate.cs`** (NEW, Maestro,
  155 lines): `Check(resolvedModel, opts, allowedModels) -> string?`.
  Returns null on pass-through; non-null rejection text on mismatch.
  First-miss-wins per the brief mapping table. Unregistered models
  pass through (documented decision -- backward-compat with users
  running custom deployments outside the seed registry).
- **`azureopenai-cli/Registry/ModelRegistry.cs`** (+20 lines,
  Kramer): one new helper `ModelsWithCapability(tag) -> string[]`,
  ordinal `Contains` against each entry's `Capabilities` array.
- **`azureopenai-cli/Program.cs`** (+4 lines, Maestro single
  insertion): the gate call site, post-persona-pin re-check, pre-
  client-construction. No other Program.cs edits all episode -- the
  one-insertion contract held.

### Tests (Wave 2)

- **`tests/AzureOpenAI_CLI.Tests/CapabilityRejectionTests.cs`** (NEW,
  Bookman, 4 unit tests): builder-level invariants -- length budget,
  ASCII-only, scrub correctness, no-suggestion fallback tail.
- **`tests/AzureOpenAI_CLI.Tests/CapabilityGateTests.cs`** (NEW,
  Puddy, 19 facts): integration-style; happy path, each of the four
  capability rows, allowlist intersection, persona-pin downstream
  ordering, sentinel detection. Cleverest fixture: `RegistryScope`
  reflects `Program.RegistryEntries` private setter for hermetic
  parallel-worker safety -- no env-var plumbing, no embedded-seed
  mutation.
- **`tests/AzureOpenAI_CLI.Tests/CapabilityGateAccessibilityTests.cs`**
  (NEW, Mickey, 7 facts): rejection-message ANSI-free, no tabs/CR,
  ASCII-printable, deterministic prefix, length-bound. Uses
  `str.IndexOf('\u001B') < 0` (not `Assert.DoesNotContain`) per the
  S04E02 lesson -- the C0 needle false-passes in xUnit's renderer.

**Suite total: 1387 passing, 0 failed, 0 skipped** (up from 1361
pre-episode).

### Docs (Wave 1 + 3)

- **`docs/adr/ADR-013-capability-gate.md`** (NEW): Context, Decision,
  Consequences, Alternatives (incl. rejected `AZ_AI_DISABLE_CAPABILITY_GATE`
  env-var bypass), Mickey's accessibility-review subsection (with
  read-aloud transcript), FDR's adversarial-review appendix.
- **`docs/model-cards/REVIEW-capability-rejection.md`** (NEW, Mickey,
  ~180 lines): screen-reader walkthrough, NO_COLOR confirmation,
  colorblind/keyboard-only analysis, pipe-to-grep regex test.

### Latent bug found, deferred (not silently fixed)

- **F-EE-SP-001** (Puddy, LOW): `CapabilityGate.cs` line 106 uses
  `string.IsNullOrEmpty` for the system-prompt predicate while the
  other three rows use `string.IsNullOrWhiteSpace`. Whitespace-only
  `--system-prompt "   "` is treated as "set" and trips the gate;
  whitespace-only `--tools "   "` is not. Filed; not fixed; queued
  for E04.

## FDR findings (Wave 3 adversarial review)

ADR-013 appendix, 11 findings:

| Severity | Count | IDs |
|----------|-------|-----|
| CRITICAL | 0 | -- |
| HIGH | 0 | -- |
| MEDIUM | 1 | F-EE-AR-09 (verified PoC: registry override with 200 capability-bearing entries balloons rejection line to ~5 KB; brevity-contract breach, screen-reader DoS) |
| LOW | 4 | F-EE-AR-01, -02, -03, -07 |
| INFO | 2 | F-EE-AR-04, -05 (documented design decisions) |
| NIT | 2 | F-EE-AR-08, -11 |
| PASS | 2 | F-EE-AR-06 (persona-pin order verified), F-EE-AR-10 (no control-char injection in `--doctor` output) |

**No hotfix lane.** F-EE-AR-09 + Mickey's overlapping A11Y-CG-02 +
Puddy's F-EE-SP-001 + Mickey's A11Y-CG-01 (apostrophe in registry
name breaks shell-quoted propagation) all queue jointly for S04E04
*Reading Room* -- where bounded-output formatting is the whole point.

## Validation

- **`dotnet build`** -- 0 warnings, 0 errors.
- **`dotnet test`** -- 1387 passing, 0 failed.
- **`dotnet format --verify-no-changes`** -- clean.
- **`make preflight`** -- green (SP3 may have wired markdownlint into
  it; this run preceded SP3's commit but the manual `markdownlint-cli2`
  check is clean against every new file).
- **ASCII grep** -- clean across all new files.
- **AOT delta** -- not measured this episode (concurrent-push race
  during Wave 1 prevented baseline capture). Maestro's allocation
  profile (one `HashSet<string>`, one `List<string>`, no new records
  in `AppJsonContext`) is well within the brief's 25 KB cap. Bania
  has standing instructions to bench on the next aggregated drop.

## Risks accepted

- **Concurrent-push race** swept Wave 1 Maestro + Kramer code into
  Peterman's S04E07-brief commit (`eecfd74`). Code is byte-identical
  to spec and tests pass -- attribution is the only loss. Filed as
  F-SP1-04 in the SP1 exec-report; SP3 (Wilhelm) is adding pre-push
  hooks but cannot solve attribution for concurrent disjoint-file
  pushes. Future episodes should serialize the wave at the dispatch
  layer (one push window per wave, not per agent).
- **F-EE-SP-001 predicate inconsistency** ships unfixed. Cosmetic UX
  bug only; user-visible blast radius is one whitespace edge case
  that no realistic input would hit. E04 cleanup.

## What's next

- **S04SP3 *The Pre-Push*** (Wilhelm, still filming) -- markdownlint
  and ascii-check into `make preflight` and pre-push hook. Once landed,
  the trilogy that started with SP1 closes and CI hygiene becomes
  systemically enforced.
- **v2.3.0 release run** (`25829942927`, Lippman's retag on `c6185b6`
  with SP2's printf fix) -- in_progress at episode close. SP2 already
  verified the matrix legs all build; this run should publish the
  first GitHub Release since v2.2.0 (13 days dark).
- **S04E04 *Reading Room*** (Elaine LEAD) -- brief is GREENLIT-able
  on Elaine's `c0cae21`. Dispatching next. Folds in F-EE-AR-09 +
  A11Y-CG-01 + A11Y-CG-02 + F-EE-SP-001 as cleanup work.
- **S04E05 *The Picker*** (Costanza LEAD) -- brief at `6fbc2e5`.
- **S04E06+** -- per plan.md roster.

## Linked files

- `docs/episode-briefs/s04e03-the-capabilities.md` (brief, GREENLIT)
- `docs/adr/ADR-013-capability-gate.md` (new ADR + FDR appendix +
  Mickey a11y subsection)
- `docs/adr/ADR-012-model-registry-seam.md` (registry seam ADR --
  F-EE-SP-001 to be filed here in E04 prologue)
- `docs/model-cards/REVIEW-capability-rejection.md` (Mickey review)
- `azureopenai-cli/Capabilities/CapabilityGate.cs` (NEW, Maestro)
- `azureopenai-cli/Cli/CapabilityRejection.cs` (NEW, Bookman)
- `azureopenai-cli/Registry/ModelRegistry.cs` (Kramer helper)
- `azureopenai-cli/Program.cs` (Maestro single insertion)
- `tests/AzureOpenAI_CLI.Tests/CapabilityGateTests.cs` (NEW, Puddy)
- `tests/AzureOpenAI_CLI.Tests/CapabilityGateAccessibilityTests.cs`
  (NEW, Mickey)
- `tests/AzureOpenAI_CLI.Tests/CapabilityRejectionTests.cs` (NEW,
  Bookman)
- `docs/exec-reports/s04sp1-the-reruns.md` (CI special trilogy, act 1)
- `docs/exec-reports/s04sp2-the-stenographer.md` (CI special trilogy,
  act 2 -- Lippman)
