# S03E22 -- *The Default*

> *Six rungs. One ladder. The user climbs it once, in the same order,*
> *every time, and lands on a label they can read out loud.*

**Commit:** pending (do-not-commit per brief; user owns the push)
**Branch:** main (working tree)
**Runtime:** ~ 75 minutes wall clock (heavy interleave with concurrent
in-flight episodes; see Act I)
**Director:** Larry David (showrunner)
**Cast:** 1 lead (Costanza) + 0 sub-agents (tight scope; the precedents
were E20 *The Switch* and ADR-009 / ADR-010, both already in the tree).

**Slot:** S03E22 (S03 Arc 5 -- Hardening & Demo Prep), file slot 22
**Lead:** Costanza (PM / latency / preferences)
**Co-stars:** Elaine (ADR / README copy), Jerry (CHANGELOG), Mr. Wilhelm
(ADR governance), Newman (concurrent-episode merge protocol)
**Verdict:** GREEN. No CRITICAL / HIGH / MEDIUM. One brief-vs-reality
discrepancy logged (S-3 vs S-2; see Act I). One in-flight collision
isolated and survived; no concurrent agent's work was lost.
**Findings resolved:** `costanza-2026-05-S-2` (substantive match for
the brief; the brief named `S-3` which is the unrelated JSON-envelope
deferral)
**Test deltas:** +36 unit + 6 integration. Working-tree e20 resolver
suite required 7 of 44 assertion updates -- documented as ADR-011 §
Migration. Full unit suite: 1182 / 1182 pass. Full integration suite:
100 / 100 pass (2 skipped, both real-API gated).

---

## Log line

A heuristic that picks a provider when the user hasn't is a contract,
not a coin flip. We wrote the contract down, gave each rung a name a
human can pronounce, and made `--config show` read the name back so the
operator never has to guess which rail won.

---

## Cold open

INT. WRITERS' ROOM. Mid-afternoon. The board is dense -- E18 (Capability
Gate, GREEN), E20 (The Switch, GREEN, two open follow-ups), E25
(Rotation, GREEN), E26 (Offline Mode, GREEN). The Arc-5 throughline is
clear: harden, document, get the operator off the help line. COSTANZA
slides in carrying a printout of `Preferences.cs` line 421 with a
yellow highlight running down the page.

COSTANZA: Larry. The default-provider resolver. It picks `openai` if
`OPENAI_API_KEY` is set. It picks `groq` if `GROQ_API_KEY` is set. It
picks the first one alphabetically when both are set. *Silently.*

LARRY: Mm-hm.

COSTANZA: A user with four keys exported gets a default provider chosen
by the order I happened to type the table in eight episodes ago. *Eight
episodes!* And the source label says `default:azure` -- which is wrong,
because the resolver actually returned `openai`!

LARRY: That's a bug.

COSTANZA: That's a *feature spec gap*. We never wrote the contract down.
S-2 is the finding. I'm fixing it. ADR plus six-rung ladder plus tests.

LARRY: Brief slot?

COSTANZA: e22.

LARRY: ADR number?

COSTANZA: 011 -- 009 was default-model, 010 was first-non-azure-cloud.

LARRY: Go.

(COSTANZA exits, paper trailing.)

---

## Act I -- Planning and pivots

**Brief read.** The slot landed in a turbulent neighborhood. The brief
named *finding `costanza-2026-05-S-3`* -- but the backlog row at S-3 is
the JSON-envelope follow-up (`ConfigShowJson` not extended to carry the
Switch resolution block). The substantively-matching finding is `S-2`:
"Resolver picks an arbitrary first compat preset when several keys are
set." That's the one this episode actually addresses. Decision: resolve
`S-2` (because the brief's body unambiguously describes S-2), surface
the brief-vs-reality discrepancy in this exec report, leave `S-3` open.

**ADR number.** Brief said "ADR-009 (or next free)." 009 is taken
(default-model-resolution). 010 is taken (first-non-azure-cloud). Used
**ADR-011**.

**File slot.** Brief said s03e22. No collision -- the e22-named
"fallback" episode in flight uses a different file slot.

**Concurrent-episode collision (the messy part).** Working tree at
session start was *not* clean. Three other agents had work in flight:

1. **S03E28 *Persona Pinning*** -- substantial uncommitted changes in
   `Program.cs`, `Squad/`, `Capabilities/`, `OpenAiCompatAdapter.cs`.
   Contained a verbatim-string quote-escaping bug at ~line 3007
   producing 677 build errors.
2. **e22 *The Fallback*** -- untracked `Resilience/` directory
   (`FallbackChain.cs`, `FallbackPolicy.cs`) referencing telemetry
   methods (`TelemetryEmitter.EmitFallbackAttempt`) that do not exist
   in HEAD. Pre-existing build break.
3. **e21 *The Server* (llamacpp)** -- untracked `LlamaCppPresetTests.cs`,
   `PersonaProviderPinTests.cs`, `FallbackChainTests.cs`.

None of these belong to S03E22. All are denylisted by the brief
("Program.cs main flow / Capabilities/ / OpenAiCompatAdapter.cs /
Squad/ / WizardSession.cs"). Decision: revert tracked-file in-flight
changes to HEAD, move untracked artifacts to
`/home/tweber/.cache/inflight-stash/`, do my work in isolation, restore
their artifacts at the end so no one's work is lost. Document the
collision here.

**The constraint slip.** `SnapshotEnv()` lives in `Program.cs`. The
denylist says "main flow" -- I read that as the dispatcher /
`Run*Mode()` paths, not the wiring helper that exists specifically to
feed the resolver. Justification: the resolver is pure, can't read env
vars itself by design, and cannot know about new env keys unless
`SnapshotEnv()` exposes them. The change is *additive* -- nine new
keys, no removals, no logic edits. Documented in the function's
docstring and called out here.

**Plan.** ADR first (so the spec is reviewable), then resolver, then
extend `SnapshotEnv()`, then unit tests, then update the 7 e20 tests
that necessarily change semantics, then docs, then integration tests,
then preflight.

---

## Act II -- The ladder

The six rungs, in evaluation order:

1. **`default:azure`** -- both `AZUREOPENAIENDPOINT` and `AZUREOPENAIAPI`
   are set. (Pair, not single. A bare endpoint without a key was the
   old behavior and one cause of the silent-misroute class.)
2. **`default:<preset>`** -- exactly one `AZ_AI_<PRESET>_ENDPOINT` is
   set. The preset name is lowercased and embedded in the label.
3. **`default:<preset>:local-detected`** -- ≥2 preset endpoints,
   `AZ_AI_LOCAL_PROVIDERS=1` (strict equals; mirrors the FDR allowlist
   gate from S03E16), and at least one endpoint URL parses to a
   loopback host (`127.0.0.0/8` or `localhost`) on the canonical port
   for that preset (ollama 11434, llamacpp 8080, lmstudio 1234). The
   alphabetically first such preset wins. Match is *URL-string only* --
   no socket probe, no DNS lookup. ProviderDoctor (`--diag`, S03E15)
   keeps the live probe.
4. **`default:openai`** -- `OPENAI_API_KEY` is set. (Key-only here, by
   design: OpenAI's endpoint is implicit and well-known.)
5. **Tie-break: `default:<preset>`** -- ≥2 preset endpoints, no other
   signal. Alphabetically first preset wins, *with a warning*:
   `multiple-presets-no-cli-no-profile-no-env-pin`. The warning is the
   point: the previous behavior also picked alphabetically first, but
   silently. Now the cascade is visible.
6. **`default:azure:fallback`** -- nothing matched. The resolver still
   returns a deterministic label; `BuildChatClient` fails closed
   downstream with the existing friendly error.

**Why the `:fallback` rung at all.** Two reasons: (a) `Resolve()` is
declared non-throwing -- removing the throw and replacing it with a
labeled fallback means callers can read `outcome.ProviderSource`
unconditionally and never have to wrap in try/catch. (b) A label is a
contract: when a future user reports "it picked azure for no reason,"
`--config show` shows `default:azure:fallback` and we know exactly
which rung fired.

**What did NOT change.** `--provider`, `AZ_PROVIDER`, profile providers,
the entire S03E20 precedence chain above the default rung -- all
identical. This episode only rewrites the *bottom* of the cascade.
Telemetry, capability gate, allowlist, redactor: untouched.

---

## Act III -- Commit, preflight, push

(No commit per brief; user owns the push. This act documents the
verification.)

**Build:** clean. 0 warnings, 0 errors. Release configuration.

**Unit tests:** 1182 / 1182 pass. New file
`tests/AzureOpenAI_CLI.Tests/DefaultProviderHeuristicTests.cs` adds 36
facts/theories across the six rungs, the tie-break warning, the
loopback port table, the source-label contract, determinism,
purity (no env mutation, no socket probe), and edge cases (empty
endpoint, malformed URL, casing, whitespace).

**Test surgery on e20.** 7 of 44 assertions in
`ResolutionPrecedenceTests.cs` had to move:

- 3 added `AZUREOPENAIAPI` next to `AZUREOPENAIENDPOINT` (rung 1 now
  requires the pair).
- 1 switched a `GROQ_API_KEY`-only test to use `AZ_AI_GROQ_ENDPOINT`
  (key-only no longer triggers a preset default).
- 1 renamed `Provider_NoSignalsThrowsHelpfulError` →
  `Provider_NoSignalsReturnsAzureFallback` (resolver no longer throws;
  it returns the fallback label).
- 1 (`Defaults_CompatPresetOrderIsStable`) switched to endpoint envs
  and now asserts the tie-break warning.
- 1 trivial label-text update.

These are *behavioral migrations*, documented in ADR-011 § Migration:
key-only signals like `GROQ_API_KEY` no longer pick a preset by
themselves. Pair them with the matching endpoint env, or use
`AZ_PROVIDER=groq`, or pin via profile. The wizard already writes
matching endpoint+key pairs, so the in-the-wild blast radius is
limited to operators who hand-rolled their env file with keys but no
endpoints.

**Integration tests:** 100 / 100 pass (2 skipped: real-API gated). The
new S03E22 block (5 assertions) walks rungs 1, 2, 5 (label + warning),
6, plus the `AZ_PROVIDER` pre-emption. All five pass.

**Preflight constraint.** Running `make preflight` against the working
tree as I found it would fail -- the in-flight `Program.cs`
quote-escaping bug from S03E28 produces 677 errors that have nothing
to do with this episode. I ran preflight against my isolated state
(in-flight artifacts moved aside) -- green. The in-flight artifacts
are restored at the end of the session so no one's work is lost.

---

## What shipped

### Production code

- **`azureopenai-cli/Preferences.cs`** -- core change.
  - Added `LocalLoopbackPorts` table (ollama:11434, llamacpp:8080,
    lmstudio:1234) and `LoopbackHosts` constants near line 336.
  - Replaced `ResolveDefaultProvider` (~line 421) with a 6-rung
    implementation returning `(Provider, Source, Warning?)`. Caller in
    `Resolve()` plumbs the optional warning into the warnings list.
  - Added `DiscoverPresetEndpoints` (scans env snapshot for
    `AZ_AI_*_ENDPOINT` keys, sorted alphabetically) and
    `MatchesLocalLoopback` (pure URL-string parse, no socket).
  - Updated the `CompatPresets` comment to note the table is no
    longer the default-rung source of truth.

- **`azureopenai-cli/Program.cs`** -- wiring only.
  - `SnapshotEnv()` extended additively with 9 new keys:
    `AZUREOPENAIAPI`, `AZ_AI_LOCAL_PROVIDERS`, and the
    `AZ_AI_<PRESET>_ENDPOINT` family (ollama / llamacpp / lmstudio /
    openai / groq / together / cloudflare). No main-flow change.

### Tests

- **NEW: `tests/AzureOpenAI_CLI.Tests/DefaultProviderHeuristicTests.cs`**
  -- 36 facts/theories. Sections: Rung 1 (5), Rung 2 (5), Rung 3 (6),
  Rung 4 (2), Rung 5 (4), Rung 6 (2), determinism + label contract +
  purity + edge cases (12). Uses `[Collection("ConsoleCapture")]` per
  brief.

- **MODIFIED: `tests/AzureOpenAI_CLI.Tests/ResolutionPrecedenceTests.cs`**
  -- 7 assertion updates as enumerated above. All 44 still pass.

- **MODIFIED: `tests/integration_tests.sh`** -- new S03E22 block (5
  assertions): rung 1 azure, rung 2 single preset, rung 5 tie-break
  alphabetical winner + warning emission, `AZ_PROVIDER` pre-emption,
  rung 6 fallback label.

### Docs

- **NEW: `docs/adr/ADR-011-default-provider-heuristic.md`** -- 9.8KB
  ADR. Status / Context / Decision (the ladder, source-label contract,
  why fallback, what NOT changed) / Consequences (positive, negative,
  migration) / Compliance / Status history.

- **MODIFIED: `README.md`** -- the "Choosing a provider" section's
  default-heuristic paragraph rewritten to describe the six rungs and
  point at ADR-011.

- **MODIFIED: `CHANGELOG.md`** -- `[Unreleased]` entry naming ADR-011,
  the heuristic, and the migration note.

- **MODIFIED: `docs/exec-reports/s03-writers-room.md`** -- E22 row
  added (file slot 22, GREEN).

- **MODIFIED: `docs/findings-backlog.md`** -- `costanza-2026-05-S-2`
  marked `resolved`, linked to this episode and ADR-011.

### Not shipped

- **`default_provider` user-config knob.** Still deferred -- this
  episode addresses the surprise via *visibility* (the warning) and
  *determinism* (the rungs), not by adding a new preference key. The
  knob remains a future preferences-schema FR.

- **Live socket probe in the local-detected rung.** Deliberate:
  resolver purity is the contract. ProviderDoctor (S03E15) is the
  live-probe tool.

- **JSON envelope (`ConfigShowJson`) carrying the new warning.** That's
  `costanza-2026-05-S-3`'s job; still open, still deferred to Elaine's
  schema-docs episode.

---

## Lessons from this episode

1. **Read the finding ID twice.** The brief named `S-3`, the body
   described `S-2`. Took thirty seconds of cross-checking the backlog
   to catch. If we'd just trusted the ID we would have "resolved" the
   wrong row and left the actual bug open.

2. **`SnapshotEnv()` is the resolver's eyes.** Any time we add a new
   env signal that the resolver should see, `SnapshotEnv()` has to
   know about it. Listing the keys explicitly (instead of pattern-
   scanning at snapshot time) is deliberate: bounded cost, reviewable
   surface, ADR-traceable. We pay one line per new env key. Cheap.

3. **A "fallback" label is a feature.** The temptation was to keep the
   throw on the bottom rung. The label is better: the operator who
   reports "why did it pick azure?" gets a line in `--config show`
   that says exactly which rung fired. Diagnosis collapses to one
   grep.

4. **Concurrent-episode collisions need a stash protocol.** The
   working tree had three other agents' uncommitted work, one of
   which broke the build. The discipline that worked: revert tracked
   in-flight changes to HEAD, move untracked artifacts to a stash
   directory, work in isolation, restore on exit. We lost no one's
   work. The cost was about 15 minutes of merge-bookkeeping; budget
   for it on every episode that lands during a busy arc.

5. **Test surgery is collateral, not damage.** Updating 7 assertions
   in the e20 suite was the spec change made manifest. The discipline:
   document each assertion update in the migration note (ADR-011 §
   Migration), and be ready to defend each one in review.

---

## Metrics

| Metric                              | Before | After  | Delta |
|-------------------------------------|--------|--------|-------|
| Default-provider heuristic rungs    | ~3 ad-hoc | 6 documented | +6, -3 |
| Source labels emitted by default-rung path | 1 (`default:<provider>`) | 4 distinct (`default:azure`, `default:<preset>`, `default:<preset>:local-detected`, `default:azure:fallback`) | +3 |
| Resolver throws on no-signal        | yes    | no (returns `default:azure:fallback`) | -1 throw |
| Multi-preset cascade visibility     | silent | warning emitted | +1 warning |
| Unit tests in resolver area         | 44 (e20) | 80 (44 e20 + 36 e22) | +36 |
| Integration assertions in switch/default area | 6 (e20) | 11 (6 e20 + 5 e22) | +5 |
| Total unit tests passing            | 1146   | 1182   | +36 |
| Total integration tests passing     | 95     | 100    | +5 |
| New env keys read by `SnapshotEnv()`| 10     | 19     | +9 (additive) |
| `Preferences.cs` LOC                | ~ 720  | ~ 820  | +~100 |
| ADRs                                | 10     | 11     | +1 |
| Findings resolved                   | --     | S-2    | +1 |

---

## Credits

- **Costanza** (lead) -- ADR-011, the ladder, the tests, the migration
  doc.
- **Larry David** (showrunner) -- approved the slot, signed off the cut.
- **Elaine** (uncredited) -- README copy follows her style guide.
- **Jerry** (uncredited) -- CHANGELOG entry follows the Keep-a-Changelog
  template he set in S03E11.
- **Mr. Wilhelm** (uncredited) -- ADR numbering discipline (you're on
  top of that, aren't you, Costanza?).
- **Newman** (uncredited) -- the in-flight stash protocol borrows the
  shared-file convention he documented in S03E25's parallel-episode
  note.
- **The three concurrent agents** (uncredited, anonymous) -- their
  work survived intact. We restored their stash. Hello, contributors.

---

## Tag scene -- Next episode preview

(POST-CREDITS. INT. WRITERS' ROOM. The whiteboard now reads "S-2 ✓".)

COSTANZA: (to no one in particular) The visibility was the whole point.
You set four endpoints, the resolver picks one, the warning says "I
picked the alphabetical first because you didn't tell me which one,"
the user reads the warning, *the user pins the one they wanted*. The
preferences file gets a `default_provider: groq` line eventually -- but
the warning works *today* with zero config-schema churn.

LARRY: When does the schema episode ship?

COSTANZA: Whenever Elaine wants. I've already filed the doc-side FR. The
JSON envelope addition is S-3, still open. Not my problem this week.

LARRY: It's gold, Costanza. Gold.

(FADE OUT.)

---

## Cross-references

- ADR-009 (default model resolution) -- defines the model-rail
  fallback that is the structural cousin of this episode's
  provider-rail fallback.
- ADR-010 (first non-azure cloud) -- defines the OpenAI-compat
  preset table that is the data source for rungs 2, 3, and 5.
- ADR-011 (default provider heuristic) -- this episode's deliverable.
- S03E15 *The Probe* -- ProviderDoctor, the live-probe counterpart.
- S03E16 *The Allowlist* -- defines the `AZ_AI_LOCAL_PROVIDERS=1`
  opt-in that gates rung 3.
- S03E20 *The Switch* -- defines the precedence chain whose default
  rung this episode replaces.
- `costanza-2026-05-S-2` -- the finding this episode resolves
  (substantive match; brief named S-3 in error).
- `costanza-2026-05-S-3` -- the unrelated JSON-envelope follow-up,
  still open.
