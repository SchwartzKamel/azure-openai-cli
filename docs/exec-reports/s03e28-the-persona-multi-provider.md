# S03E28 -- *The Persona, Multi-Provider*

> *Same binary, three personas, three providers, one giddyup.*

**Commit:** pending (do-not-commit per brief; user owns the push)
**Branch:** main (working tree at 02eab8e + this episode)
**Runtime:** ~ 35 minutes wall clock
**Director:** Larry David (showrunner)
**Cast:** 1 lead (Kramer) + 0 sub-agents (tight scope; the precedents
were E20 *The Switch* and E21 *The Default*, both already in the tree;
the Persona rung's record fields had been pre-staged in Wave 9 and just
needed a real caller).

**Slot:** S03E23 (blueprint) -- file slot `s03e28-the-persona-multi-provider.md`
(arc4-5 numbering drift; reconciled later in the arc4-5-renumber sweep)
**Lead:** Kramer (engineering / squad / preferences)
**Co-stars:** Costanza (precedence chain author -- e20/e22), Newman
(security gate review -- e16/e26 boundaries unchanged), Mr. Wilhelm
(in-flight protocol -- redispatch self-detection guard), Elaine
(README + CHANGELOG copy)
**Verdict:** GREEN. No CRITICAL / HIGH / MEDIUM. Two LOW/INFO findings
filed (`kramer-2026-05-PMP-1`, `kramer-2026-05-PMP-2`).
**Findings resolved:** none (purely additive feature)
**Test deltas:** +42 unit facts, +5 integration assertions. Full unit
suite: 1299 / 1299 pass. Full integration suite: 111 / 111 pass (2
skipped, both real-API gated).

---

## Log line

A persona is a configuration. So let it carry the configuration. The
operator sets `provider: groq` and `model: llama-3.1-70b-versatile` on a
persona in `.squad.json`, invokes `--persona kramer`, and the resolver
folds those values into a new rung between profile and default --
*above* the heuristic that picks Azure when nothing else asks, *below*
the explicit knobs the operator twisted at the command line.

---

## Cold open

INT. WRITERS' ROOM. Late afternoon. The board reads:

```text
  E20 *The Switch*           GREEN   precedence chain codified
  E21 *The Default*          GREEN   six-rung heuristic
  E22 *The Fallback*         GREEN   chain wrap, opt-in
  E18 *The Capability Gate*  GREEN   refuse-fast on incompatible
  E16 *The Allowlist*        GREEN   loopback gate
  E26 *The Offline Mode*     GREEN   AZ_AI_OFFLINE=1
  ─────────────
  E23 *The Persona, ...*     ???
```

KRAMER bursts through the door, taps the `???`.

KRAMER: Larry. Personas have a system prompt. They have tools. They
have memory. What they don't have is *a provider*. Coder wants gpt-4o
on Azure. Reviewer wants llama-3.1-70b on Groq. Architect wants
o1-mini on OpenAI. Right now? *Same model*. Whatever the global chain
spits out.

LARRY: So you put `provider: groq` on the persona.

KRAMER: I put `provider: groq` on the persona. AND I put it BETWEEN
profile and default in the chain. CLI still wins. Env still wins.
Profile still wins. But default? Default loses to the persona's pin.

LARRY: And if creds for the pinned provider aren't set?

KRAMER: We *don't* fail. We warn once on stderr and fall through to
the default chain. Persona memory still loads. Operator gets a
persona, just not the one with the missing keys.

LARRY: Ship it. *(beat)* And don't touch the cap gate.

KRAMER: Cap gate stays in front. Allowlist stays in front. Offline
gate stays in front. The pin is a *configuration*, not an *escape
hatch*.

LARRY: Giddyup.

---

## Act I -- Discovery

The first thing Kramer found, before writing a line, was that Wave 9
had already done half the job: the `ResolutionInputs` record at
`Preferences.cs:248-281` already carried `PersonaName`, `PersonaProvider`,
and `PersonaModel` init-only properties; `PreferencesResolver.Resolve`
at line 471 already had a fully-implemented Persona rung sandwiched
between profile and default; the public accessors `IsKnownProvider`,
`KnownProvidersList`, and `GetCredEnvVarName` at lines 369-398 had
been added by E21 *exactly* for the squad-validation use case named
in this brief's S03E28 headers.

What was missing: a caller. The Persona rung's record fields had no
populated input. `PersonaConfig.Provider` did not exist. `SquadConfig`
did not validate. The Squad invocation site in `Program.cs` walked
straight from "persona resolved" to `BuildChatClient` without ever
consulting the resolver a second time.

So the implementation reduced to four wires:

1. Add `PersonaConfig.Provider` (sibling to existing `Model`).
2. Add `SquadConfig.Validate()` and call it from `Load()`.
3. Add `SquadCoordinator.ApplyPersonaPin(...)` -- the seam that folds
   a persona into a `ResolutionInputs` and handles missing-creds
   fall-through.
4. Wire the Squad invocation site (`Program.cs` after the persona is
   resolved) to call `ApplyPersonaPin` + re-`Resolve` when a pin is
   present, and override the resolved `model` when the persona rung
   wins.

Zero changes to `BuildChatClient`. Zero changes to capability/offline/
allowlist. Zero changes to `PersonaMemory`. Zero changes to the 32 KB
cap. The diff is small because the substrate was already there.

## Act II -- Fleet dispatch

Single-lead episode -- no sub-agent dispatch. Kramer carried the
implementation; the lead trusted prior work and confined edits to
the brief's allowlist.

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | Kramer (solo)     | Read-everything sweep: `.github/skills/{episode-brief,preflight,exec-report-format}.md`, `kramer.agent.md`, `AGENTS.md`, `s03-writers-room.md`, `s03e20`/`s03e22` exec reports, full `azureopenai-cli/Squad/` namespace, `Preferences.cs` (Wave 9 surface), Squad invocation site in `Program.cs`. No sub-agents -- the codebase context is the codebase. |
| **2** | Kramer (solo)     | Implementation: `PersonaConfig.Provider` + `SquadConfig.Validate` + `SquadCoordinator.ApplyPersonaPin` + Program.cs wire. Build green on first try. |
| **3** | Kramer (solo)     | 42 unit facts in `PersonaProviderPinTests.cs`. First run: 42/42 pass on first compile. |
| **4** | Kramer (solo)     | 5 integration assertions in `▸ S03E23 persona pin`. First run after fixing the absolute-path BIN resolution: 5/5 pass. |
| **5** | Kramer (solo)     | Docs: CHANGELOG, README, writers-room, findings-backlog. Then preflight: 1299 unit + 111 integration, all green, format clean, exec-report gate satisfied. |

## Act III -- Preflight + handoff

Preflight totals (`DOTNET_ROOT=/usr/lib/dotnet make preflight`):

- **format check:** clean
- **build:** 0 warnings, 0 errors (Release)
- **unit tests:** 1299 / 1299 pass, 0 skip (~6 min wall)
- **integration tests:** 111 / 111 pass, 2 skip (real-API gated)
- **exec-report check:** satisfied (this file)
- **findings backlog gate:** satisfied (no new gate-tier audit findings;
  the two new INFO/LOW rows are PMP-1/PMP-2, not gate-tier, so no
  exec-report-check requirement -- listed for transparency)

No commit (per brief: "Do NOT commit. Report files-touched, preflight
totals."). Files touched are listed in *What shipped* below; the user
owns the push.

---

## What shipped

### Production code

- `azureopenai-cli/Squad/SquadConfig.cs` -- new `Validate(string?
  sourcePath = null)` method walks personas, calls
  `PreferencesResolver.IsKnownProvider` on each non-null `Provider`,
  throws `InvalidOperationException` with persona name + bad value +
  source path + known-providers list. Called from `Load()` so the
  rejection happens once, up front. New field
  `PersonaConfig.Provider` (`[JsonPropertyName("provider")]`) sibling
  to the existing `Model`. Source-gen `AppJsonContext` already
  registered `PersonaConfig`, so no JSON-context edits needed.
- `azureopenai-cli/Squad/SquadCoordinator.cs` -- new public static
  helper `ApplyPersonaPin(ResolutionInputs baseInputs, PersonaConfig
  persona, IReadOnlyDictionary<string,string?> env, Action<string>?
  warnSink)`. Pure: no env reads outside the snapshot, no Console
  writes, no I/O. No-pin fast path returns `baseInputs` unchanged
  (`Assert.Same`-equal). Missing-creds path uses
  `PreferencesResolver.GetCredEnvVarName` to find the right env var,
  drops the provider pin, calls `warnSink` with a single line naming
  the persona + provider + env-var + the action ("falling through to
  the global default-provider chain"). `warnSink` is null-safe so
  `--raw` / `--json` callers pass `null` and stay silent. The model
  pin always survives -- a model-only pin is valid (e.g. "this
  persona always wants gpt-4o on whatever provider the chain picks").
- `azureopenai-cli/Program.cs` -- one focused region right after the
  persona is resolved (after the existing `Console.Error.WriteLine`
  that prints `[persona] NAME (ROLE)`). Loads `Preferences`, builds a
  `ResolutionInputs` from the CLI flags + env snapshot, calls
  `ApplyPersonaPin` (which adds the persona rung), calls
  `PreferencesResolver.Resolve` again, overrides `model` when
  `personaOutcome.ModelSource` starts with `persona:`, prints
  `[persona:NAME] provider -> '...'` / `[persona:NAME] model -> '...'`
  to stderr (silent under `--raw` / `--json`), forwards any
  resolver-emitted warnings. Re-validates the (possibly-updated) model
  against the `AZUREOPENAIMODEL` allowlist -- a persona pin does NOT
  bypass the allowlist gate.
- `azureopenai-cli/Preferences.cs` -- *unchanged this episode*. Wave 9
  already added `ResolutionInputs.PersonaName` / `.PersonaProvider` /
  `.PersonaModel` and the Persona rung in `Resolve`. The brief's
  "additive: reuse e21's IsKnownProvider/GetCredEnvVarName" rule was
  honored by *not duplicating* what Wave 9 staged.

### Tests

- `tests/AzureOpenAI_CLI.Tests/PersonaProviderPinTests.cs` (NEW) --
  42 unit facts across six concerns:
  - **Resolver precedence ladder** (11 facts): persona provider beats
    default, loses to cli / env / profile; persona model beats
    default, loses to cli / env / profile; null pin falls through;
    label format is `persona:<name>:<field>`; defensive `?` when name
    is null; model-only pin is valid.
  - **`SquadConfig.Validate` gate** (8 facts, including 7 [Theory]
    rows): accepts each known provider, rejects unknown with
    actionable message, rejects whitespace-padded values, allows null
    - empty, includes source path in error.
  - **`SquadConfig.Load` integration** (2 facts): rejects unknown
    provider at load time; accepts valid pin and round-trips.
  - **`SquadCoordinator.ApplyPersonaPin`** (8 facts): no-pin fast
    path returns unchanged; provider pin passes through with creds;
    missing creds drops + warns; null sink swallows; azure requires
    `AZUREOPENAIAPI`; foundry requires `AZURE_FOUNDRY_KEY`; model-only
    pin never warns; preserves base CLI fields.
  - **End-to-end** (2 facts): pin -> Resolve produces
    `default:openai` when creds mismatch; pin -> Resolve produces
    `persona:kramer:provider` when creds present.
  - **Capability + offline cross-checks** (4 facts): pinned
    conservative provider still loses tool_calls; pinned azure keeps
    them; offline gate strict-equality on `"1"`; offline off by
    default.
  - **Round-trip** (1 fact): provider + model survive
    `Save`/`Load` through `AppJsonContext`.
- `tests/integration_tests.sh ▸ S03E23 persona pin` (NEW block) -- 5
  bash assertions: --personas lists pinned config; unknown provider
  rejected with actionable error (rc=99 from the global
  `InvalidOperationException` handler); error names the offending
  persona; squad-init scaffold passes the new validator; empty
  provider/model strings are permissive (half-edited config safety
  net).

### Docs

- `docs/exec-reports/s03e28-the-persona-multi-provider.md` (NEW) --
  this file.
- `docs/exec-reports/s03-writers-room.md` -- new GREEN row for E23
  noting the file-slot drift (slot 28 because 22-27 already burned).
- `docs/findings-backlog.md` -- two new INFO/LOW rows
  (`kramer-2026-05-PMP-1`, `kramer-2026-05-PMP-2`).
- `CHANGELOG.md` -- `[Unreleased]` block extended with a
  feat(squad) entry, full prose, links to tests + findings.
- `README.md` -- the `--persona` row in the v2.0.0 table now
  references the persona pin + precedence chain + load-time
  validation + missing-creds fall-through.

### Not shipped

- **Env-rewrite shim for dispatch routing.** Filed as
  `kramer-2026-05-PMP-1`. Today, a persona pinning `provider: groq`
  produces a `persona:kramer:provider` source label and lights up the
  resolver's outcome correctly, but `BuildChatClient` still routes
  by `AZ_AI_COMPAT_MODELS` / `AZURE_FOUNDRY_MODELS`. Operator must
  also set the matching env to make the dispatch flip. Closes when a
  future episode either (a) teaches `BuildChatClient` to accept a
  resolved provider directly, or (b) ships an env-rewrite shim that
  injects an `AZ_AI_COMPAT_MODELS` entry for the persona's
  (provider, model) tuple at invocation time. Out of this episode's
  scope per the brief's denylist (`Program.cs main flow except Squad
  invocation site`).
- **`--config show` echo of the persona rung.** Filed as
  `kramer-2026-05-PMP-2`. The resolver returns the rung labels and
  the invocation site prints them to stderr, but the canonical
  config dump still only enumerates CLI/env/profile/default.
  Companion to `frank-2026-05-FB-2` (also-not-yet-echoed). Closes
  with whichever episode next refactors `--config show`.
- **Anthropic / Cohere / Mistral on the known-providers list.**
  Per ADR-010, Anthropic is deferred to S03 Arc 4 / S04 Arc 1 via
  placeholder FR-024. The `KnownProviders` array stays at the seven
  Wave 9 entries. Adding to the list is a one-line edit when the
  provider lands.
- **Telemetry event for persona-pin engagement.** Considered, declined
  this episode. The existing `dispatch` telemetry already carries
  `provider` and `dispatch_path`; emitting a separate `persona_pin`
  event before that telemetry has a real consumer would be premature.
  Open question for Frank Costanza when the SLI catalog grows past
  Arc 5.

---

## Lessons from this episode

1. **Wave 9 staged the substrate; this episode wired the caller.**
   The redispatch brief explicitly named `IsKnownProvider`,
   `KnownProvidersList`, `GetCredEnvVarName`, and the Persona rung
   record fields as already-present. Re-reading them before writing
   anything cut the diff in half. *Lesson:* "read everything first"
   in the brief is not boilerplate. Skipping it produces a duplicate
   `KnownProviders` array, which Wilhelm then catches at review time.
2. **Self-detection guard worked.** The first dispatch self-aborted
   on `list_agents` seeing its own entry. The redispatch brief named
   that failure mode in the front matter. This run did not call
   `list_agents`. *Lesson:* if a stateless agent's first move is to
   look for itself in a list, the look-up itself is the bug. Add
   the guard to the brief, not the agent.
3. **Missing-creds fall-through > hard fail.** The brief specified
   "fall through to global default with a stderr WARN (silent under
   --raw)." The temptation was to fail fast (the operator typed it
   wrong; tell them). The fall-through is correct: a persona's *job*
   is to set up an environment, but the **operator's** invocation
   intent is "run this persona." Refusing the run because of a
   typed-once `.squad.json` value would surprise more than it
   helps. Warn once, drop the pin, run the persona on the global
   default. The persona's memory still loads.
4. **Validate-at-load > validate-at-dispatch.** Putting `Validate()`
   in `Load()` means a typo in `.squad.json` is caught when the
   operator runs `--personas` (a fast, no-network operation), not
   later when they're mid-flow with `--persona kramer "fix the
   bug"`. The error message names the file path so they can
   `$EDITOR /path/.squad.json` straight to the line.
5. **Capability gate / offline gate / allowlist stay in front.**
   The brief was explicit: "NO special-case bypass." The
   implementation honored that by leaving the gate-call sites
   unchanged. The persona pin only affects *resolution*, not
   *enforcement*. A persona pinning a conservative provider still
   has to clear the cap gate at dispatch time. Two of the unit
   facts pin this contract.
6. **Single-file Program.cs region is the right scope.** The brief
   denylist said "Program.cs main flow except Squad invocation site
   (one focused region)". The actual diff was ~80 lines in a single
   contiguous region after the existing
   `Console.Error.WriteLine($"[persona] {NAME} ({ROLE})")` line.
   Reviewable. Greppable. No drive-by changes.

---

## Metrics

- **Diff size:** ~120 insertions C# (+ Provider field, + Validate, +
  ApplyPersonaPin, + Program.cs region), ~520 insertions tests
  (`PersonaProviderPinTests.cs`), ~120 insertions integration
  (`▸ S03E23 persona pin` block), ~10 insertions docs
  (CHANGELOG/README), ~3 row inserts (writers-room +
  findings-backlog), 1 new exec-report file. Net deletions: 0.
- **Test delta:** +42 unit (`PersonaProviderPinTests.cs`), +5
  integration. Total unit suite: 1299 / 1299 pass (was 1257 before
  this episode; +42 matches). Total integration suite: 111 / 111
  pass (was 106 before this episode; +5 matches).
- **Preflight:** all five gates green (format / build / test /
  integration / exec-report). Wall clock: ~6m24s for unit suite, ~12s
  for integration, format + build + exec-report-check
  near-instantaneous.
- **CI status at push time:** n/a (do-not-commit per brief).

---

## Credits

- **Kramer** -- lead, end to end. Implementation, tests, integration,
  docs.
- **Costanza** -- *in absentia* via E20 + E21 ground-truth. The
  Persona rung's spec lived in his precedence-chain comment block at
  `Preferences.cs:214-240`; the rung name and label format
  (`persona:<name>:<field>`) follow his convention. No new
  consultation needed -- the prior work was load-bearing.
- **Newman** -- *in absentia* via E16 + E26 boundaries. The
  loopback gate (`AZ_AI_LOCAL_PROVIDERS=1`), endpoint allowlist, and
  offline gate (`AZ_AI_OFFLINE=1`) are unchanged. Two of the unit
  facts cross-check the offline gate's strict-equality contract so
  any future regression there fails this suite first.
- **Mr. Wilhelm** -- *in absentia* via the redispatch brief. The
  self-detection guard at the top of the brief
  ("CRITICAL: SELF-DETECTION WARNING") closed the loop on the
  prior dispatch's self-abort.
- **Elaine** -- README + CHANGELOG copy reviewed against the
  exec-report-format skill. ASCII-clean throughout (run the grep:
  `grep -nP '[\u2018\u2019\u201C\u201D\u2013\u2014]'` on the new
  files returns nothing).
- **Lippman** -- not consulted. No version bump; this is an
  unreleased addition to the existing `[Unreleased]` block.
- **Co-authored-by trailer:** would carry
  `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`
  on the eventual commit. Not applied here per brief's
  do-not-commit instruction.

---

## Tag scene -- *what's coming next*

INT. WRITERS' ROOM. Same day, later. The board has one square left:

```text
  E27 *The Demo*       ???  Larry David, solo, season finale
```

LARRY: That's twenty-six. We have *one* episode left.

KRAMER: The demo. The whole Arc 5 retrospective. Doctor, switch,
default, fallback, persona pin -- *all* of it on the same screen.
One hermetic shell script, no real provider calls, just the
binary's own output rendered as a season recap.

LARRY: And nobody touches the codebase.

KRAMER: Nobody. Touches. The codebase. It's a curtain call. You
direct, you narrate, you sign the cut. The cast takes a bow.

LARRY: *(beat)* Pretty, pretty, pretty good.

*(LARRY uncaps the marker. Writes "S03E27 *The Demo*" in the empty
square. The board is full.)*

CUT TO: black.

THE END (of season 3).

---

## Cross-references

- [`s03-writers-room.md`](s03-writers-room.md) -- season ledger
  (E23 GREEN row noting file-slot drift)
- [`s03e22-the-default.md`](s03e22-the-default.md) -- six-rung
  default heuristic (E21 in slate, slot 22)
- [`s03e23-the-fallback.md`](s03e23-the-fallback.md) -- chain
  wrap (E22 in slate, slot 23)
- [`s03e20-the-switch.md`](s03e20-the-switch.md) -- the
  precedence chain itself
- [`../findings-backlog.md`](../findings-backlog.md) -- new
  PMP-1 / PMP-2 rows
- [`../adr/ADR-009-default-model-resolution.md`](../adr/) -- the
  precedence ADR (note: directory listing inferred; follow tree)
- [`../adr/ADR-011-default-provider-heuristic.md`](../adr/) --
  E21's six-rung heuristic source of truth
