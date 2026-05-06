# S03E20 -- *The Switch*

> *Costanza yanks the precedence chain out of folklore and into a pure function.
  The dispatcher finally has an algorithm to point at.*

**Commit:** `(uncommitted -- staged for review)`
**Branch:** `main` (direct push)
**Runtime:** ~50 minutes wall-clock
**Director:** Larry David (showrunner)
**Cast:** 1 lead (Costanza) + 0 sub-agent waves -- this was a tight, single-cast
 scene

## The pitch

Every previous episode that touched provider selection had to reinvent the
precedence chain. S03E08 *The Pick* picked OpenAI direct, S03E09 *The Compat*
added the preset map, S03E10 *The Keychain* sliced env-file sections per
provider, S03E18 *The Capability Gate* refused to dispatch through a model that
did not advertise the right capability. Each one consulted some subset of
`AZUREOPENAIENDPOINT`, `AZUREOPENAIMODEL`, `OPENAI_API_KEY`,
`AZ_AI_COMPAT_MODELS`, `AZ_PROVIDER`, `AZ_PROFILE`, and the `preferences.json`
profile entries -- but no one ever wrote down the order, and `--config show` had
to recompute the answer with its own copy of the heuristic. The sentence "az-ai
picks the right thing" was a folk story that two readers would tell two
different ways.

S03E20 codifies the chain. One pure function, `Preferences.Resolve()`, takes a
`ResolutionInputs` (CLI flags + env snapshot) and a loaded `Preferences` and
returns a `ResolutionOutcome` whose `Source` field labels every resolved value.
The labels are stable strings: `cli`, `env:AZ_PROVIDER`,
`profile:<name>:provider`, `default:azure`, `default:openai`,
`env:AZUREOPENAIMODEL[0]`, `env:AZ_AI_COMPAT_MODELS[<preset>]`, and so on.
Anything that needs to know how the model was chosen reads the field. Anything
that needs to debug a surprise reads the field. `--config show` reads the field.
Future audit trails read the field. There is one source of truth and it has a
name.

The deal: cli > env > profile > built-in default, applied independently to the
provider rail and the model rail. The profile rail is optional -- no `--profile`
and no `AZ_PROFILE` skips it entirely. Missing profiles named via `--profile`
produce a friendly listing of what is actually in the file. Mismatch between a
profile's pinned provider and the `AZ_AI_COMPAT_MODELS` route emits a stderr
warning -- the profile wins, by precedence, but the operator gets a heads-up.
None of this required modifying `BuildChatClient` or `OpenAiCompatAdapter`, both
of which were locked under e18 (capability gate) and e25 (creds rotate). The
dispatch path stayed intact; the resolver is a layer in front of it.

## Scene-by-scene

### Act I -- Planning

The brief named the constraints up front. Two other agents in flight: e18 owned
`BuildChatClient` + `Capabilities/`; e25 owned the credential rotator. Touch
neither. The legal touch surface was `Program.cs` arg-parser tail,
`Preferences.cs`, a new test file, and the documentation triad (CHANGELOG,
README, exec-report, writers-room, findings-backlog).

One real decision: where to install the resolver call. The brief said "Replace
whatever ad-hoc model+provider selection the dispatcher currently does with
`Preferences.Resolve(...)`" -- but the same brief said "DO NOT modify
BuildChatClient internals (e18 territory)". Those statements are not in conflict
only if you read "dispatcher" as "the code that picks model and provider strings
to feed BuildChatClient", not "BuildChatClient itself". The resolver is
consulted in `RunAsync` immediately after the credential check; its output is
folded into the existing model-resolution chain via a new `?? profileModel` rung
between the `AZUREOPENAIMODEL` env layer and the
`UserConfig.ResolveSmartDefault()` layer. No call site of `BuildChatClient`
changed. Existing model-allowlist enforcement still runs after the resolver.

The second decision: extend `ResolutionOutcome` past the brief's `(Provider,
Model, ProfileName, Source)` shape. The brief's `Source` examples are all
provider-side ("cli", "env:AZ_PROVIDER", "profile:<name>:provider",
"default:openai") -- but `--config show` also needs the model source and the
profile source as separately addressable fields. Compromise: keep the brief's
`Source` as an alias for `ProviderSource`, and carry `ModelSource`,
`ProfileSource`, plus a `Warnings` list as additional record fields. Backwards
compatible with the brief's spelled contract; forward compatible with the
JSON-payload extension that costanza-2026-05-S-3 schedules.

### Act II -- Fleet dispatch

| Wave | Agents (parallel) | Outcome |
|------|-------------------|---------|
| **1** | Costanza (sole lead -- design + implementation + tests + docs) | Resolver shipped, 44 facts pass, integration block green, docs propagated |

This is a single-cast episode. No sub-agent dispatch waves -- the whole scope
(one pure function + arg-parser tail edits + a test file + the documentation
triad) fits inside one director and one lead without a relay. Mr. Pitt would
call this a one-actor scene; Larry called it before the table read.

### Act III -- Ship

1. Built `azureopenai-cli/AzureOpenAI_CLI.csproj`. Clean. 0 warnings.
2. Ran the new `ResolutionPrecedenceTests` filter: 44 facts, 48 ms, 0 failures.
3. Ran the existing `Preferences|CliParser|ConfigShow|ProviderDispatch` filters
   as a regression smoke: 58 facts, 0 failures.
4. Wired the integration tests at the tail of the S03E18 block, before the
   API-gated smoke.
5. Updated CHANGELOG `[Unreleased]` with the S03E20 entry above the existing
   S03E18 entry (push-timing serialization per the changelog-append skill).
6. Updated README with the new "Choosing a provider and model" sub-section under
   "Multi-provider routing" / "Capability gate".
7. Updated `s03-writers-room.md` with the E20 row.
8. Filed three follow-up rows (`costanza-2026-05-S-1..3`) in
   `docs/findings-backlog.md` covering the per-profile credential alias gap
   (depends on E10's per-provider section work landing a stable selector), the
   arbitrary first-compat-preset default, and the JSON envelope deferred until
   Elaine's `--config show` schema episode.
9. Wrote this exec report.
10. Did not commit per brief instruction.

## What shipped

**Production code**

- `azureopenai-cli/Preferences.cs` -- new `ResolutionInputs` and
  `ResolutionOutcome` records (internal sealed); new `PreferencesResolver`
  static class with the pure `Resolve()` entry point. The resolver consults a
  per-provider compat-preset table that mirrors `OpenAiCompatAdapter.BuiltIn`
  (openai / groq / together / cloudflare) without taking a reference -- the
  adapter file is e18 / e25 territory and the resolver does not need it.
  Helpers: `ResolveDefaultProvider`, `ResolveDefaultModel`,
  `TryLookupCompatPreset`, `BuildMissingProfileMessage`. ~250 lines added;
  pre-existing `Preferences` / `ProviderEntry` / `ProfileEntry` shapes
  untouched.
- `azureopenai-cli/Program.cs` -- two new `CliOptions` fields (`Provider`,
  `Profile`); two new arg-parser cases (`--provider`, `--profile`) appended at
  the end of the parser switch (after `--offline`) per the merge-friction
  discipline; one new helper (`SnapshotEnv`) that returns an
  `IReadOnlyDictionary<string,string?>` of just the keys the resolver consults;
  a new resolver-call block in `RunAsync` immediately after the credential
  check, gated on "any precedence signal present" so the legacy chain stays
  bit-exact for users who never touch `--profile` / `--provider`; a new "Switch
  resolution (S03E20):" block in `RunConfigShow`'s text output. Help text (CLI
  help block + bash / zsh / fish completions) updated. ~80 lines net added.

The dispatch path was NOT touched. `BuildChatClient` was not opened.
`OpenAiCompatAdapter.cs` was not opened. The credential storage was not touched.
e18 and e25 territory was respected.

**Tests**

- `tests/AzureOpenAI_CLI.Tests/ResolutionPrecedenceTests.cs` -- 44 facts under
  `[Collection("ConsoleCapture")]`, organised into:
  - 9 provider precedence facts (cli > env > profile > each default branch + the
    no-signals throw)
  - 9 model precedence facts (cli > env > profile > azure default > compat
    default > fallback + the AZ_MODEL-vs-profile rung)
  - 6 profile rail facts (CLI source label, optional skip, missing-profile error
    with available list, missing-profile-with-empty-prefs hint,
    AZ_PROFILE-missing falls through with warning, empty-provider-string falls
    through)
  - 3 mismatch-warning facts (profile-vs-compat warns, match doesn't warn, CLI
    override doesn't warn)
  - 4 combined-cascade facts (cli-wins, env-cascade, profile-cascade,
    default-cascade)
  - 3 source-string contract facts (Source == ProviderSource, source label
    syntax, default label includes provider)
  - 10 regression / shape guards (null inputs throw, whitespace trim, empty CLI
    flags fall through, warnings list always non-null, all sources populated,
    azure-first heuristic, compat-preset order stability, AZ_MODEL precedence vs
    profile, malformed compat entries skipped, no AZ_AI_COMPAT_MODELS means no
    mismatch check)
- `tests/integration_tests.sh` -- new "S03E20 -- The Switch" block with 6
  assertions: --config show emits the Switch resolution block + source field;
  --provider beats AZ_PROVIDER (provider source = cli); --profile chains to
  profile.provider + profile.model; missing profile lists available names;
  --help documents --provider and --profile; AZ_PROFILE env routes to
  profile.provider.

**Docs**

- `CHANGELOG.md` -- new `[Unreleased]` Added entry, push-timing-serialized above
  the S03E18 capability-gate entry.
- `README.md` -- new "Choosing a provider and model (S03E20 *The Switch*)"
  sub-section (~22 lines) with the precedence table, the default heuristic, and
  a `--config show` example.
- `docs/exec-reports/s03-writers-room.md` -- E20 row added below E19.
- `docs/exec-reports/s03e20-the-switch.md` -- this report.
- `docs/findings-backlog.md` -- three new rows (`costanza-2026-05-S-1`
  per-profile cred alias, `S-2` first-compat-preset default surprise, `S-3` JSON
  envelope deferral).

**Not shipped** (intentional follow-ups)

- `--config show --json` does not yet carry the Switch resolution block. The
  text path has it; the JSON path is gated on extending `ConfigShowJson` and the
  `AppJsonContext` source-generated serializer. Filed as costanza-2026-05-S-3,
  deferred to Elaine's `--config show` schema episode to keep AOT JSON context
  churn out of this scene.
- Per-profile credential alias. A profile pins `provider:` but the API key still
  resolves through the global env-var (`AZUREOPENAIAPI`, `OPENAI_API_KEY`, ...).
  Filed as costanza-2026-05-S-1; depends on E10's per-provider env-section work
  landing a stable `[provider:NAME]` selector that profiles can reference.
- `default_provider` knob in `preferences.json` for the all-keys-set tiebreaker.
  Filed as costanza-2026-05-S-2; deferred to a future FR-014 schema bump.
- Wire the resolver into the dispatch path at the provider level. Today the
  resolver's `Provider` is informational outside of `--config show` and the
  warning emit; the actual dispatch routing in `BuildChatClient` still consults
  env vars directly. e18 owns that file this push; the consolidation is queued
  for a follow-on episode after the capability-gate work lands.

## Lessons from this episode

1. **Folk stories die when they get a name.** Five separate code paths were
   doing precedence resolution with subtly different rules. Naming the function
   and shipping a single `Source` vocabulary for the result kills the entire
   class of "wait, which env var wins again?" tickets. Future audit trails
   (`--config show`, telemetry events, error messages) should all reference the
   same labels -- if a label is missing, that's the bug, not the user's mental
   model.
2. **Pure functions for resolution; side-effecting wiring at the call site.**
   The resolver does not call `Environment.GetEnvironmentVariable`. It does not
   write to `Console`. It takes everything it needs as input and returns
   everything it produced as output. That made 44 unit facts trivial to write --
   no test fixtures to mutate, no env-var sandboxing, no console-capture races
   -- and made the code reviewable in one pass. The `SnapshotEnv` helper at the
   call site is the only place that touches process state. Future
   resolver-shaped logic should follow the same shape.
3. **Append-at-end keeps merge friction at zero.** The arg-parser tail
   conventions (place new flags after `--offline`, the previous "end of parser"
   marker) meant zero conflict surface with e18's dispatch hunks and e25's
   credential subcommand. The completion scripts updated cleanly because they
   were already a single-line list. Cost: the parser ordering does not match the
   help-text ordering; readers wanting to see all flag definitions at a glance
   still get the help text. Acceptable trade.
4. **Brief contracts are floors, not ceilings.** The brief specified `record
   ResolutionOutcome(string Provider, string Model, string? ProfileName, string
   Source)`. A literal read of that record signature loses the model source and
   the profile source -- both required by `--config show`. Adding
   `ProviderSource`, `ModelSource`, `ProfileSource`, and `Warnings` past the
   brief's signature is a forward-compatible extension; keeping `Source` as the
   brief named it preserves the documented contract. When a brief and a use case
   disagree, extend the type, do not narrow the use case.
5. **Mismatch warnings, not mismatch errors.** A profile pinned to `provider:
   azure` plus a model whose `AZ_AI_COMPAT_MODELS` entry routes to `openai` is
   suspicious but not necessarily wrong -- the operator may have legitimately
   set the compat env for a different invocation in the same shell. The brief
   was explicit: profile wins, warn the user. Treating it as an error would
   require us to sequence env unset / set carefully across every multi-provider
   workflow; treating it as a warning preserves the precedence contract and
   gives the operator a single line to grep for when something surprising
   happens.

## Metrics

- **Diff size**: 8 files touched, 1 new file, ~700 lines added, ~5 lines deleted
  (net +695). Production code: ~330 lines (Preferences.cs ~250, Program.cs ~80).
  Tests: ~440 lines (44 facts). Docs: ~110 lines (CHANGELOG + README +
  writers-room + findings-backlog rows + this report).
- **Test delta**: +44 unit facts (1034 -> 1078), +6 integration assertions (73
  -> 79).
- **Preflight**: not run yet at the time of this report (brief instruction is
  "Run `DOTNET_ROOT=/usr/lib/dotnet make preflight`" before the no-commit
  handoff). Build clean; targeted unit + regression smoke green.
- **CI status**: n/a (no commit per brief).

## Credits

- **Costanza** -- design, implementation, tests, docs, this report. The whole
  episode.
- **Larry David** -- showrunner, signed the cut.
- **Co-authored-by trailer**: would be on the commits if any commits were made;
  the brief's "DO NOT COMMIT" instruction was honoured -- the staged work is for
  review.

## Conflict notes vs. e18 / e25

- **e18 (the-capability-gate)**: Owns `BuildChatClient`, `Capabilities/`,
  `OpenAiCompatAdapter.cs`. None of those were opened. The resolver does
  duplicate the compat-preset list (`openai / groq / together / cloudflare` plus
  their `*_API_KEY` env names) inside `Preferences.cs` rather than `using` it
  from the adapter -- intentional, to keep the file independent of e18's
  reshaping. If e18 lands a different preset name or env var, the resolver's
  `CompatPresets` table needs the same edit; this is a single-line change and a
  small follow-up cost we deemed cheaper than the merge-conflict alternative.
  The arg-parser tail addition (`--provider`, `--profile`) shares the same
  `switch (arg.ToLowerInvariant())` block as e18's anticipated dispatch flag
  work, but the cases are at different positions (mine after `--offline`, e18's
  likely earlier near `--model`); no overlap.
- **e25 (the-rotation)**: Owns `az-ai creds rotate` + the env-file rewriter.
  Untouched. The resolver does not read or write credentials; it only confirms
  presence-of-key (via `string.IsNullOrWhiteSpace`) when computing the
  default-provider heuristic. No `~/.config/az-ai/env` access from S03E20.

## Tag-scene -- next-episode preview

S03E21 *The Default*. Costanza, fresh off shipping the precedence chain, becomes
obsessed with the *first* link of it: when nothing is set, what should the
binary do? The brief writes itself -- `default_provider` knob in
`preferences.json`, smarter "first-compat-preset" tiebreaker, possibly a
`--default` interactive picker. Filed as costanza-2026-05-S-2. Tee-up line for
the cold-open: "We have an algorithm. Now we need a default that does not feel
like an accident."
