# S03E17 -- *The Server*

> *Don't tell me about your model. Tell me where it's listening.*

**Commit:** pending (do-not-commit per brief; user owns the push)
**Branch:** main (working tree)
**Runtime:** ~ 95 minutes wall clock (twice-clobbered tree;
re-application + recovery dominate the gap; see Act III)
**Director:** Larry David (showrunner)
**Cast:** 1 lead (Kramer) + 0 sub-agents (the work was tight: one
preset, one capability row, one mirror entry, one test class).

**Slot:** S03E17 (S03 Arc 5 -- Hardening & Demo Prep), file slot 21
(E17 file slot already burned by S03E17 *The Stream*; this episode
ships to `s03e21-the-server.md`. Title-vs-file drift documented inline
in the writers' room ledger).
**Lead:** Kramer (compat presets / Docker / Azure OpenAI / local
inference)
**Co-stars:** Costanza (parallel e22 *The Default* shipping into
`Preferences.cs`; conflict survived clean -- see Act III), Newman
(loopback gate from S03E16 reused verbatim), Frank Costanza (S03E18
capability gate reused verbatim), Jerry (ProviderDoctor probe from
S03E15 auto-discovers the new preset for free), Elaine (README copy +
exec-report polish), Mr. Wilhelm (ADR-010 still binding -- no new ADR
filed; the preset extension fits the existing seam).
**Verdict:** GREEN. No CRITICAL / HIGH / MEDIUM. One LOW finding filed
(`kramer-2026-05-LCPP-1`, model-resolution gap on the Preferences seam
-- fine on the dispatch path; surfaces as a polish gap on the
fallback-chain path).
**Findings resolved:** -- (this episode opens one; no prior backlog
entry was scoped to the llamacpp preset).
**Test deltas:** +25 unit (`LlamaCppPresetTests`) + 4 integration
(`tests/integration_tests.sh` -> S03E17 block). Full unit suite:
1207 / 1207 pass. Full integration suite: 100 / 100 pass (2 skipped,
both real-API gated). Preflight reports all gates green.

---

## Log line

The OpenAI-compat seam already speaks the wire protocol. The local-
provider gate already says yes-or-no. The capability gate already says
what's safe to ask for. All `llama-server` needed was a preset row, a
default capability profile, and a model-name fallback so a one-flag
invocation works. We added those, wired the loopback gate to refuse it
without opt-in, wired the capability gate to refuse tool-calls without
override, and gave ProviderDoctor a probe row for free.

---

## Cold open

INT. KRAMER'S APARTMENT. Mid-afternoon. The fridge hums. A laptop is
balanced on a stack of cereal boxes. KRAMER reads the writers' room
brief out loud.

KRAMER: "S03E17. *The Server*. llama.cpp's `llama-server` -- pointed at
<http://localhost:8080>. They want a preset. They want a Conservative
capability row. They want tests. They want the loopback gate to *say
no*."

KRAMER (to no-one): "Giddyup."

He opens `OpenAiCompatAdapter.cs`. The cloudflare row is right there.
He taps the screen.

KRAMER: "These pretzels are making me thirsty -- and this preset record
needs four more knobs."

CUT TO: terminal. The build is green.

---

## What shipped

### 1. `OpenAiCompatPreset` extended (additive only)

The record gained four optional fields (default values keep cloud
presets untouched):

- `EndpointEnvVar` -- `AZ_AI_<PRESET>_ENDPOINT` for runtime URL
  override. llama.cpp operators run `llama-server` on whatever port
  they like.
- `RequiresApiKey` (default `true`) -- llama-server is unauthenticated
  by default; `Build()` substitutes a placeholder credential when this
  is false so the OpenAI SDK's `ApiKeyCredential` ctor stays happy.
- `ModelEnvVar` -- `AZ_AI_<PRESET>_MODEL` for served-model name.
- `DefaultModel` -- last-rung fallback so `--model llamacpp` (no env
  exports) resolves to the literal `"llamacpp"`. (llama-server ignores
  the field anyway; only one model is loaded at a time.)

### 2. `BuiltIn["llamacpp"]` added

```text
new("llamacpp",
    new Uri("http://localhost:8080/v1"),
    "AZ_AI_LLAMACPP_API_KEY",
    EndpointEnvVar: "AZ_AI_LLAMACPP_ENDPOINT",
    RequiresApiKey: false,
    ModelEnvVar:    "AZ_AI_LLAMACPP_MODEL",
    DefaultModel:   "llamacpp")
```

### 3. `Build()` rewrites: blank-model fallback + optional API key + endpoint override

Three additive branches, each gated on the new preset fields. The
cloudflare branch is untouched. The HTTPS-only-for-non-loopback
posture is *preserved* -- the endpoint override path still runs
through `IsLoopback(endpoint)` and the SSRF allowlist
(`EndpointAllowlist.Check`). An operator who points
`AZ_AI_LLAMACPP_ENDPOINT` at a public-IP HTTP URL gets refused
*even with* `AZ_AI_LOCAL_PROVIDERS=1` set. Opt-in only relaxes the
private-range gate, not the wire-encryption posture.

### 4. `ResolveModel(preset, requested)` helper

Static helper. Precedence: explicit non-blank `requested` > preset's
`ModelEnvVar` (if exported and non-blank) > `DefaultModel`. Returns
null when no source resolves so the caller can decide whether that's
fatal. The `Build()` seam treats null as fatal.

### 5. `ProviderCapabilities.PresetDefaults["llamacpp"]`

Conservative profile: `tool_calls=false, streaming=true, vision=false,
json_mode=false, MaxContextTokens=8192`. The override hatch is
unchanged: `AZ_AI_CAPABILITY_OVERRIDES=llamacpp:<model>:tool_calls=true`
flips per (preset, model). The `HasPreset("llamacpp")` returning true
suppresses the "unknown capability profile" stderr warning that would
otherwise spam every dispatch.

### 6. `Preferences.CompatPresets[]` mirror entry

Single row added to the tuple-array mirror so `--config show` and the
`KnownProviders[]` enumeration know about `llamacpp`. e22 was actively
shipping into this same file; the row coexists cleanly with the
ResolutionInputs / LocalLoopbackPorts / IsKnownProvider extensions.
(Confirmed: e22's `LocalLoopbackPorts[]` table includes
`("llamacpp", 8080)`, so the rung-3 default-detection path picks the
preset up for free.)

### 7. Tests

**Unit (`tests/AzureOpenAI_CLI.Tests/LlamaCppPresetTests.cs`):**
17 facts expanding to 25 cases (Theory inlines on the
case-insensitive resolve test). Coverage:

- Preset shape (BuiltIn registration; field-by-field assertions on
  every new knob; case-insensitive resolution; ResolveOrThrow listing).
- `ResolveModel` precedence (requested > env > default; blank
  fallthrough to env; missing-env fallthrough to default; preset
  with no fallbacks returns null).
- `Build()` happy paths (no API key + loopback opt-in; blank model
  -> default; endpoint override honored; API key set + used).
- `Build()` failure paths -- the *fail-the-fail* discipline:
  - loopback without opt-in throws `ArgumentException` naming both
    `compat preset 'llamacpp'` and `Refusing to dispatch`.
  - malformed `AZ_AI_LLAMACPP_ENDPOINT` throws `InvalidOperationException`
    naming the offending env var.
  - non-loopback HTTP override (e.g. `http://example.com/v1`) refused
    even with `AZ_AI_LOCAL_PROVIDERS=1`.
- Capability gate: Conservative profile asserted; `HasPreset` true;
  override flips `tool_calls`.
- Endpoint allowlist: BlockLoopback without opt-in; Allow with opt-in;
  Allow on alt-port (8123) override with opt-in.
- ProviderDoctor: probe row emitted when `AZ_AI_COMPAT_MODELS` routes
  a model to `llamacpp`; **inverse guard** asserts no phantom row
  appears when no compat models are exported.

Test class env-var save/restore covers fourteen vars (including the
Azure stub credential set so other side tests don't pollute state).
`[Collection("ConsoleCapture")]` keeps it serial alongside the rest of
the env-var suite.

**Integration (`tests/integration_tests.sh` S03E17 block):**

1. `--doctor` table emits `compat:llamacpp` row + `localhost:8080`.
2. `--doctor --json` emits `compat:llamacpp` row.
3. Capability gate refusal (`--agent --model llamacpp`) names
   `AZ_AI_CAPABILITY_OVERRIDES` and exits non-zero.
4. Loopback gate refusal (no `AZ_AI_LOCAL_PROVIDERS=1`,
   `--model llamacpp`) names `AZ_AI_LOCAL_PROVIDERS` and exits
   non-zero.

Each assertion uses `env -i HOME PATH DOTNET_ROOT ...` so unrelated
host env state does not leak into the surface under test. Azure stub
creds (`AZUREOPENAIENDPOINT` / `AZUREOPENAIAPI` / `AZUREOPENAIMODEL`)
are exported on the gate-refusal cases so the early credential check
clears before dispatch.

---

## Act III -- Two clobbers, one clean recovery

The interesting part of this episode wasn't the code. It was the
working tree.

**First wipe** (mid-implementation): files I had edited a moment
earlier (`OpenAiCompatAdapter.cs`, `ProviderCapabilities.cs`,
`integration_tests.sh`, `LlamaCppPresetTests.cs`) showed mtimes "just
now" with my changes gone. `git status` revealed `Preferences.cs`
modified by something else, plus an untracked `ADR-011-...md`. Working
hypothesis: a sibling agent (e22) ran a workspace reset before
applying their own diffs, taking my untracked file with it and
checking out HEAD on the rest. The `Preferences.cs` row I had added
survived only because e22's edits to the same file kept the row in the
diff.

**Second wipe** (mid-test-run): re-applied all four files; while a
test command was queueing, files vanished a second time. e22's
`Resilience/FallbackChain.cs` showed up in the tree with compile
errors against a `TelemetryEmitter` API that didn't yet exist. Build
failed hard.

**Recovery:** waited 60s, built clean -- e22 had landed their
matching `TelemetryEmitter` extension. Re-applied my changes a third
time. They stuck. Tests passed.

**No work was lost permanently.** The two wipes cost wall-clock time
but no decisions; everything was reproducible from the brief +
existing presets + test discipline.

**For the writers' room (do not file as a finding -- it's a process
note):** parallel-cast episodes touching adjacent files want a merge
protocol. e22 + e23 + e17 all touched seams within a 200-line radius
of each other; the lack of a "lock the rung you're shipping into"
convention turned what should have been a clean three-way merge into
two surprise resets. Mr. Wilhelm should grade this in the season
retro.

---

## Verification

```text
DOTNET_ROOT=/usr/lib/dotnet make preflight
...
Passed!  - Failed:     0, Passed:    25, Skipped:     0, Total:    25, ...
...
===========================================
 All 100 tests passed! (2 skipped)
===========================================
[preflight] all gates green -- safe to commit
```

- format-check: clean
- color-contract-lint: clean
- dotnet-build (Release): 0 warnings, 0 errors
- unit suite: 1207 / 1207 pass (full project; 25 of those are the new
  `LlamaCppPresetTests`)
- integration suite: 100 / 100 pass (2 skipped, both real-API gated)
- exec-report-check: clean (this file's structure validated)

---

## Findings filed

**`kramer-2026-05-LCPP-1` (LOW, open):** `AZ_AI_LLAMACPP_MODEL` is
honored at the `OpenAiCompatAdapter.Build()` seam (via the new
`ResolveModel` helper) but is *not* threaded through
`Preferences.ResolveDefaultModel`. For the happy path (`--model
llamacpp` -> dispatch) this is invisible: the env var still resolves
at Build time. Gap surfaces only when a future caller wants to ask
"what model would Preferences pick for the llamacpp preset?" before
dispatch. Suggested fix: extend `ResolveDefaultModel` to consult the
preset's `ModelEnvVar` / `DefaultModel` when the resolved provider is
a compat preset. Out of scope for this episode (would touch the e22
fallback-chain seam I was told to stay out of).

---

## Tag scene

INT. KRAMER'S APARTMENT. Same laptop. Same cereal-box stack. The PR
description draft is on screen. KRAMER reads it back to himself,
satisfied.

KRAMER: "Twenty-five unit. Four integration. One LOW finding -- and
*that* one's a polish issue, not a bug. Loopback gate said no. Capability
gate said no. ProviderDoctor said yes when asked. Preferences said
nothing -- which is exactly what it was supposed to say."

He stands. He turns. He bursts back through the door he just came in
through, because that's what Kramer does.

KRAMER (off-screen, calling back): "*Giddyup.*"

FADE OUT.
