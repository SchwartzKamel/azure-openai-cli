# S03E18 -- *The Capability Gate*

> *Three providers, six capabilities, zero confused 500s.*
> *If the model cannot do it, do not send it.*

**Slot:** S03E18 (S03 Arc 4 -- Multi-Provider Hardening)
**Lead:** Costanza (Product Manager / architectural eye)
**Co-stars:** Kramer (registry shape), Frank Costanza (telemetry wiring),
Newman (refusal-path leak guard), Lt. Bookman (error-message brevity),
Sue Ellen Mischke (the Groq tool-call roster that started this whole
mess)
**Verdict:** GREEN. No CRITICAL / HIGH / MEDIUM. Three LOW/INFO
follow-ups.
**Findings:** `costanza-2026-05-CG-1`, `CG-2`, `CG-3`
**Test deltas:** +33 unit + 5 integration over the v2.1.4 baseline
(1034 unit + 73 integration -> 1067 unit + 78 integration).

---

## Cold open

INT. WRITERS' ROOM. COSTANZA pacing. Coffee in one hand, terminal in
the other. He has clearly been here a while.

COSTANZA: I'm telling you, Larry. I clicked --agent. I clicked --agent
on a Groq model. The model didn't have tools. The model has *never*
had tools. The model came back with a 400 that said "tool_calls is
not a known parameter." It did not say what was wrong. It said what
was *missing*. There is a difference!

LARRY DAVID: Okay.

COSTANZA: A user gets that error and they think *they* did something
wrong. They didn't. The CLI did. The CLI knew. The CLI knew the model
was a Groq 8B and the CLI sent the tools array anyway.

LARRY: Did the CLI know?

COSTANZA: Of course the CLI knew! It picked the preset! It read
AZ_AI_COMPAT_MODELS! It dispatched through OpenAiCompatAdapter! It
*had the preset name in its hand* and it sent the request anyway.

LARRY: So you want a gate.

COSTANZA: I want a gate. A registry. A table. *(slaps a printed
matrix on the table)* Three providers. Six -- well, four, but I am
shipping four with two reserved -- capabilities. A 4-by-3 grid that
the dispatcher consults *before* it constructs the wire payload. If
the model does not advertise the capability, we refuse. Friendly
error. Names the override env var. Exit code 2.

LARRY: Two?

COSTANZA: Two. One is "the user's input was wrong." Two is "the
configuration is wrong." This is configuration. This is two.

LARRY: Fine. You have one episode.

COSTANZA: I have an *architectural eye*, Larry, this is a doctrine.

LARRY: One episode.

CUT TO: *az-ai --agent --model llama-3.1-8b-instant "hi"* on a
terminal. The error reads:

    [ERROR] groq:llama-3.1-8b-instant does not support tool_calls.
    Pick a model with capability or set
    AZ_AI_CAPABILITY_OVERRIDES=groq:llama-3.1-8b-instant:tool_calls=true
    to override.

COSTANZA: (off-camera) That's a sentence. *That* is a sentence.

---

## Episode summary

S03E18 ships the capability gate that S03E09 *The Compat* and S03E16
*The Allowlist* implicitly assumed but never built. The compat seam
gave us four built-in presets and a routing rule. The allowlist seam
gave us a refusal verdict for unsafe endpoints. Neither knew anything
about the *model's* feature surface. So a `--agent` request against a
Groq 8B-instant model would clear every preflight and surface, twelve
seconds later, as a 400 from `api.groq.com` that mentioned "tools" by
its OpenAI-wire-protocol name. The user never saw which file to edit.

The gate is three small pieces:

1. **`CapabilityDescriptor`** -- a flat C# `record` of four boolean
   feature flags (`ToolCalls`, `Streaming`, `Vision`, `JsonMode`)
   plus an informational `MaxContextTokens?`. Two static factories:
   `Conservative()` (everything false) and `Permissive()` (everything
   true). The factories are the whole tier doctrine: when in doubt,
   say no.

2. **`ProviderCapabilities`** -- a static registry with two lookup
   layers (preset default; model-specific override) and an
   env-var-driven override layer on top. `Get(preset, model)` is the
   one entry point. `Mismatch(preset, model, capability)` is the
   exception factory. A pure-function override parser
   (`ParseOverrides`) handles the comma-separated user input with a
   warn-and-skip policy on malformed entries.

3. **Dispatch wiring** -- right after `BuildChatClient` succeeds in
   `Program.RunAsync`, the dispatcher asks the registry for the
   `(preset, model)` capabilities and refuses if the request needs
   something the model does not advertise. `--agent`, `--ralph`, and
   personas-with-tools all map to "needs tool_calls"; vision is
   reserved (no current CLI surface emits image content into a chat
   request, but the gate is wired); `--schema` maps to "needs
   json_mode" with graceful degradation -- warn, do not fail.

`OpenAiCompatAdapter.Build` got one new responsibility: when the
preset name is unknown to the registry, warn to stderr that
`Conservative()` is being applied. Silent under `--raw` / `--json`
(machine surfaces stay clean). The warning names the override env
var so the user knows the escape hatch exists.

Telemetry (E13) integrates by reusing the existing `DispatchScope`:
on a capability-gate refusal we call `SetOutcome("client_error",
CapabilityMismatchException.ErrorClass)` and then `Emit()` *before*
`ErrorAndExit` -- because `ErrorAndExit` calls `Environment.Exit`
and the finally would never run. The `error_class` constant is
exposed as a public string on the exception type so the telemetry
schema stays stable across episodes that touch this path.

---

## The matrix as shipped

The 2026-05 snapshot. `costanza-2026-05-CG-1` tracks the review
cadence finding -- this table is correct *today*; quarterly review is
non-optional.

| Preset      | ToolCalls           | Streaming           | Vision  | JsonMode |
|-------------|---------------------|---------------------|---------|----------|
| azure       | yes                 | yes                 | yes     | yes      |
| foundry     | yes                 | yes                 | yes     | yes      |
| openai      | yes                 | yes                 | yes     | yes      |
| groq        | no (preset default) | yes                 | no      | yes      |
| together    | no                  | yes                 | no      | yes      |
| cloudflare  | no                  | yes                 | no      | no       |

Model-specific overrides (these *beat* the preset default):

| Preset | Model                       | Effect                                        |
|--------|-----------------------------|-----------------------------------------------|
| openai | gpt-3.5-turbo               | Vision -> false (text-only)                   |
| openai | o1-preview                  | Streaming -> false, JsonMode -> false         |
| openai | o1-mini                     | ToolCalls -> false, Streaming -> false        |
| groq   | llama-3.1-70b-versatile     | ToolCalls -> true                             |
| groq   | llama-3.3-70b-versatile     | ToolCalls -> true                             |

`together` and `cloudflare` ship with no per-model rows -- the
preset-default is conservative by intent. First user override against
either is a signal that we should have known; that is finding
`costanza-2026-05-CG-2`.

`azure` and `foundry` are permissive on purpose. The user controls
the deployment behind those endpoints; the CLI cannot tell whether
their `gpt-4o` deployment is the August or November snapshot, much
less whether it has the vision extension turned on. We trust the
caller and let the wire give them the truth if they are wrong.

---

## The override env var

`AZ_AI_CAPABILITY_OVERRIDES` is a comma-separated list of
`preset:model:capability=bool` items. Case-insensitive on every
field. Whitespace tolerant around items, around the colon, around
the equals sign. Booleans accept `true` / `false` / `1` / `0`.
Capability identifiers accept the doc-form (`tool_calls`,
`json_mode`) or the camel-form (`toolcalls`, `jsonmode`).

Example: a user reports that Together's `meta-llama-3.1-70b-instruct`
deployment *does* speak tools today. The conservative default refuses.
The user does not need to wait for a release:

    export AZ_AI_CAPABILITY_OVERRIDES=together:meta-llama-3.1-70b-instruct:tool_calls=true

Multiple entries work the same way:

    export AZ_AI_CAPABILITY_OVERRIDES="together:m1:tool_calls=true,together:m2:vision=true,cloudflare:@cf/x:json_mode=true"

Malformed entries warn to stderr (silent under `--raw` / `--json`)
and are skipped. A typo never breaks dispatch. Examples that warn
and skip:

    no-equals-sign                           # missing '='
    too:few=true                             # only 2 colon parts
    a:b:c=notabool                           # bad value
    a:b:unknown_cap=true                     # unknown capability name
    openai:m:tool_calls=                     # empty value

The warn-and-skip policy is deliberate: a user who intends to
override `groq:llama-70b:tool_calls=true` and accidentally types
`groq:llama-70b:tool-calls=true` (hyphen instead of underscore) gets
a stderr warning they can read, not a silent dispatch failure they
cannot diagnose.

---

## Wire path: where the gate fires

`Program.cs` line ~595 (the BuildChatClient site in `RunAsync`):

    var chatClient = BuildChatClient(endpoint, apiKey, model, opts.Json);
    if (chatClient == null) return 1;

    // S03E18 -- The Capability Gate.
    var capsCheck = ProviderCapabilities.Get(telProvider, model);
    bool needsTools = opts.AgentMode || opts.RalphMode
        || (activePersona != null && activePersona.Tools.Count > 0);
    if (needsTools && !capsCheck.ToolCalls)
    {
        var ex = ProviderCapabilities.Mismatch(telProvider, model, "tool_calls");
        telScope.SetOutcome("client_error", CapabilityMismatchException.ErrorClass);
        telScope.Emit();
        return ErrorAndExit(ex.Message, 2, jsonMode: opts.Json);
    }

`telProvider` is the existing `ResolveDispatchInfo` output -- the
gate reuses the telemetry-side preset resolution rather than
duplicating the routing decision tree. That keeps the gate aligned
with telemetry: if telemetry sees a request as `groq`, the gate
refuses it as `groq`. If a future episode adds a routing leg, the
gate picks it up for free.

The vision arm of the gate is wired with `needsVision = false`
today. There is no current CLI surface that emits image content
into a chat request (the `--image` flag is image *generation*, not
vision input), so the gate would never fire from production. But
the seam is in place and the message shape is identical so a future
flag does not need a Program.cs touch -- only a single boolean flip.

The `json_mode` arm degrades gracefully because the cost of failing
hard there is wrong: a user asks for a JSON schema, the model is on
a preset where we do not advertise json_mode, but the model will
*usually* still produce JSON-shaped output if the prompt is good
enough. Refusing at preflight blocks too many working flows. We
warn instead, the request goes through as a regular completion, and
the user gets to decide whether the output was good enough.

---

## Tests

`CapabilityGateTests.cs` ships 33 facts under the `ConsoleCapture`
collection (sequentialised because the override env var and the
warn-sink path both touch process-global state). Coverage:

- Factory shape: `Conservative()` is all-false; `Permissive()` is
  all-true. Both have null `MaxContextTokens`.
- Registry lookup, every preset: azure / foundry / openai / groq /
  together / cloudflare. Each preset's documented default exercised.
- Model-specific narrowings: `openai:gpt-3.5-turbo` no vision;
  `openai:o1-preview` no streaming; `openai:o1-mini` no tools or
  streaming; `groq:llama-3.1-70b-versatile` and
  `groq:llama-3.3-70b-versatile` flip tool_calls true.
- Case-insensitivity on both preset and model dimensions.
- Unknown preset / empty preset -> exact `Conservative()` equality.
- `HasPreset` true for the six built-ins, false for unknown / empty.
- Override parser: null / empty / whitespace; single entry; multiple
  entries with mixed whitespace; malformed entries (no equals; too
  few colon parts; bad bool; unknown capability; empty value) -- all
  warn-once and skip; mixed good+bad returns only good.
- Override application: flips a default true; flips a default false;
  override targeting a different model leaves the requested tuple
  alone; override is case-insensitive.
- Override env-var path: `AZ_AI_CAPABILITY_OVERRIDES` actually mutates
  what `Get` returns under a process env scope.
- Mismatch exception: friendly message contains preset:model, the
  capability name, and the override env var name; exception carries
  preset / model / capability properties; `ErrorClass` constant is
  the stable string `"CapabilityMismatch"`.

Five integration assertions in `tests/integration_tests.sh`:

1. `--help` exits 0 even with `AZ_AI_CAPABILITY_OVERRIDES` set
   (gate is dispatch-side, not parser-side).
2. Tool-call request to `groq:llama-3.1-8b-instant` (a model that
   does not advertise tools) -> exit 2 + stderr contains "does not
   support tool_calls".
3. With the override env set
   (`groq:llama-3.1-8b-instant:tool_calls=true`), the same request
   passes the gate -- it then fails on the bogus endpoint, but with
   a different exit code, proving the gate let it through.
4. The refusal message names `AZ_AI_CAPABILITY_OVERRIDES` so the
   user can self-rescue.
5. The refusal path emits no `Bearer ` or `sk-` token shape (Newman
   leak-guard, ported from S03E26).

---

## Conflict notes vs. concurrent agents

Two background agents in flight when S03E18 dispatched. Coordination
followed the `shared-file-protocol` skill (orchestrator-owned files
respected; touch-list disjoint where possible).

- **e20-the-switch** owns Program.cs *argument parser* and
  `Preferences`. S03E18 touched Program.cs only at the dispatch
  site (post-parser, post-BuildChatClient). No edits to the arg-
  parsing block (lines 990-1540 in the original file). No edits to
  `Preferences.cs`. No new flag added in this episode -- the only
  user surface is the `AZ_AI_CAPABILITY_OVERRIDES` env var and the
  registry, both new files. **Predicted clean merge.**

- **e25-the-rotation** owns the new `az-ai creds rotate` subcommand
  and the env-file rewriter. S03E18 did not touch credential
  storage, the env-file loader, or any subcommand routing. The
  capability gate reads `AZ_AI_CAPABILITY_OVERRIDES` directly via
  `Environment.GetEnvironmentVariable` -- no env-file integration,
  no `~/.config/az-ai/env` parsing dependency. **Predicted clean
  merge.**

Both touch-lists were verified before the episode landed. If e20
introduces a new `CliOptions` field that the gate should consult
(e.g. `opts.Vision = true`), wiring is a one-line addition at the
gate site -- no registry change required.

---

## Latency note

The capability gate adds one in-process dictionary lookup per
dispatch -- `O(1)`, no allocations on the hot path (the override env
var is parsed at most once per `Get` call; result is a small
`List<OverrideEntry>`). Bania will own the formal benchmark in the
next perf sweep, but back-of-envelope: this is firmly under the
1ms first-token latency budget. The only way the gate is *not* a
latency win is if every request was previously succeeding -- and
the entire point of the episode is that some requests were
previously dying twelve seconds later in a wire 4xx.

---

## Findings filed

`costanza-2026-05-CG-1` (LOW) -- the capability matrix is a 2026-05
snapshot in time. Provider model rosters churn (Groq's tool-call
support list expanded twice in the last 90 days). Need a quarterly
review cadence; track upstream changelogs for openai / groq /
together / cloudflare. *Open.*

`costanza-2026-05-CG-2` (LOW) -- `together` and `cloudflare` ship
with conservative preset-default only. No per-model rows. The first
user override against either is a smell that we should have known.
Coordinate with Sue Ellen's competitive briefings to pick the
high-traffic deployments and land model-specific entries. *Open.*

`costanza-2026-05-CG-3` (INFO) -- future enhancement: lightweight
HEAD / OPTIONS probe at first call to autodetect tool-calling
support per (preset, model), cached in
`~/.cache/az-ai/capabilities.json` with a TTL. Adds a network
round-trip on cold path; revisit after S03E19's first-hour latency
budget lands. *Open.*

---

## What we did not ship

- **No new CLI flag.** `--capability-list` would be a fine
  diagnostic but it belongs in the `--doctor` family, not in this
  episode. Punt.
- **No persistence.** The override env var is the only knob.
  `~/.azureopenai-cli.json` does not learn a `capabilities` key
  this episode. Costanza floated it; Mr. Pitt asked what the
  proposal looked like; the answer was "the env var, persisted",
  which is not a proposal, it is a duplicate.
- **No probe.** `costanza-2026-05-CG-3` documents this as future
  work. The reasoning: a probe means a network round-trip on cold
  path, and S03 Arc 4 has been ruthless about cold-path adds.
- **No `--vision` flag.** The vision arm is wired and tested at
  the registry level; the dispatch site reads `needsVision = false`
  because no flag emits image content today. When a vision flag
  ships, the gate is one boolean change.

---

## Tag-scene preview

INT. WRITERS' ROOM. LATER. Costanza, victorious, leaning back.

COSTANZA: That gate. *That gate*. I sketched it on a napkin in
1997.

KRAMER: (entering) Hey buddy. So if the model says it cannot do
tools and we override it, and *then* the model still cannot do
tools, what happens?

COSTANZA: That is the override path. The user said yes. The wire
will say no. They will get a 4xx. That is on them.

KRAMER: But like... what if they did not mean it.

COSTANZA: Kramer, you cannot accidentally type a comma-separated
preset:model:capability=bool string.

KRAMER: I have done it. Twice. This morning.

COSTANZA: Larry. *(turning)* Larry, can we lock the door.

LARRY DAVID: (off-camera, walking past) Next week. *The Fallback*.

CUT TO BLACK.

> *Next episode preview -- S03E22 The Fallback: when a primary
> provider fails mid-stream, do we retry against a sibling model in
> the same preset, fail fast, or surface a `WARN` with a continue
> hint?*

---

## Sign-off

**Costanza:** Episode green. Three providers, six capabilities,
zero confused 500s. The override env var is the user's escape
hatch; the registry is the team's promise. Next sweep, the
matrix gets a review cadence -- *that* is the part where the
season finale finds out whether we have been honest with our
users.

**Test count delta:** 1034 unit + 73 integration -> 1067 unit + 78
integration.

**ADR:** None this episode. The gate is an implementation of the
existing ADR-010 compat seam doctrine, not a new architectural
boundary. If the future probe (CG-3) ships, *that* is an ADR.

**Episode owner:** Costanza
**Date:** 2026-05-19
