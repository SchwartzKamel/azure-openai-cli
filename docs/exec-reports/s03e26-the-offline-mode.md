# S03E26 -- *The Offline Mode*

> *Air-gapped review. Demo-recording sanity. Pipeline frugality.*
> *One flag, one latch, one paper trail. The bell is unplugged.*

**Slot:** S03E26 (S03 Arc 5 -- Hardening & Demo Prep)
**Lead:** Newman (Security & Compliance)
**Co-stars:** FDR (allowlist seam, prior episode), Frank Costanza
(observability), Mickey Abbott (CLI ergonomics), Lt. Bookman (help-text
brevity)
**Verdict:** GREEN. No CRITICAL / HIGH / MEDIUM. Three LOW/INFO
follow-ups.
**Audit:** `docs/audits/security-v2.1.4-offline.md`
**Findings:** `newman-2026-05-O-1`, `O-2`, `O-3`
**Test deltas:** +30 unit + 7 integration over the v2.1.3 baseline
(989 unit + 66 integration -> 1019+ unit + 73 integration; full suite
landed at 1034 / 73 green).

---

## Cold open

INT. WRITERS' ROOM. NEWMAN at the head of the table, clipboard out,
pen capped, oily smile.

NEWMAN: *Larry. A word.*

LARRY DAVID: (not looking up) Yeah.

NEWMAN: I have been informed that the conference recording is in two
weeks. I have also been informed that we have a binary which, when
you give it a misconfigured `~/.env`, *cheerfully calls Azure*. From
a podium. On video. To an audience that includes our compliance
counsel.

LARRY: Mm-hm.

NEWMAN: I propose a flag.

LARRY: One flag.

NEWMAN: One flag. `--offline`. It refuses every non-loopback call.
The binary becomes a frame in a slide deck. The audience sees prompts.
The audience does not see network blooms. The audience does not see
*invoices*.

LARRY: One flag, one episode, GREEN audit, no merge friction with
Kramer's streaming hunk.

NEWMAN: Already drafted. (Slides PR across the table.)

LARRY: Pretty, pretty, pretty good.

CUT TO: TITLE CARD -- *S03E26: The Offline Mode*

---

## Threat model (the ten-second version)

Every other gate we ship is *content-aware*: the redactor masks
secrets in lines that already exist; the allowlist refuses URLs that
look private; the wizard scrubs prompts of obvious-secret-shape
tokens. `--offline` is *direction-aware*: refuse to leave the host.

Five threat shapes drove the design:

1. **Air-gapped reviewer.** Runs the binary in a netns with no
   external interface. Wants a provable in-process guarantee.
2. **Demo recorder.** Wants the talk to look the same offline as
   online. No surprise DNS, no surprise span.
3. **Pipeline operator.** CI job with stale env vars. Wants
   `--offline` to make a misconfigured run *fail loud* before it
   spends tokens.
4. **Hostile env injector.** Slips a normal-looking
   `AZUREOPENAIENDPOINT` into the process env. Offline must catch
   the shape, not just blocklists.
5. **Loopback masquerader.** Public URL that DNS-resolves to
   loopback in our pre-flight and to a public IP at the actual TCP
   connect. Offline must not greenlight on the friendly answer
   alone.

Findings:

- (1)-(4): closed in this episode.
- (5): inherits FDR's `fdr-2026-05-A-1` (TOCTOU, MEDIUM) -- offline
  does not widen or narrow that lane. Cross-referenced as
  `newman-2026-05-O-2` (INFO).

---

## Implementation walk-through

The whole episode is **one new verdict, one new latch, six gated
seams, and one layered model**. That is the whole pitch.

### One new verdict

`EndpointAllowlist.Verdict` grew a `BlockOffline` member. It fires
ONLY when every other block verdict (`BlockPrivate`, `BlockLinkLocal`,
`BlockLoopback`-without-opt-in, `BlockUserinfo`, `BlockMulticast`,
`BlockBroadcast`, `BlockAllZeros`, `BlockNonHttpScheme`,
`BlockPrivilegedPort`) would have returned `Allow` AND the target is
not loopback. Older verdicts always win -- they carry the actionable
env-var hint. Tests on this:

- `Offline_Rfc1918_StillBlockedAsPrivate` -- RFC1918 + offline
  returns `BlockPrivate`, not `BlockOffline`. The user gets
  "blocked by RFC1918 (set `AZ_AI_LOCAL_PROVIDERS=1` to allow)" --
  the actionable hint -- not "blocked by offline".
- `Offline_CloudMetadata_StillBlockedAsLinkLocal` -- the IMDS
  case. Same priority logic; user is told the rule that fired.

`Describe(BlockOffline)` returns:

> *blocked by --offline mode (set AZ_AI_LOCAL_PROVIDERS=1 to allow*
> *loopback only; non-loopback always blocked while offline)*

The text names the rule, names the env-var to flip, and contains
zero credential. `Offline_Describe_HasActionableText` pins this.

### One new latch

`EndpointAllowlist.OfflineMode` is a process-wide `static bool`
property. The 2-arg `Check(uri, optIn)` overload reads the latch.
That is the design point. WebFetchTool and OpenAiCompatAdapter pick
up offline mode WITHOUT a single signature change.

Tests use the explicit 3-arg `Check(uri, optIn, offlineMode)`
overload to inject the flag without touching the static (they would
otherwise race with parallel test classes).

### Six gated seams

| # | Seam | Mechanism | Notes |
|---|------|-----------|-------|
| 1 | `WebFetchTool.FetchAsync` | static latch via 2-arg overload | No tool surface change |
| 2 | `OpenAiCompatAdapter.Build` | static latch via 2-arg overload | Throws `ArgumentException` with friendly text |
| 3 | Azure default in `Program.BuildChatClient` | explicit gate (3-arg overload) | Calls `ErrorAndExit` with `Describe(BlockOffline)` |
| 4 | Foundry default in `Program.BuildChatClient` | explicit gate after `IsLoopback` short-circuit | Same friendly path |
| 5 | OTLP exporter in `Telemetry.Initialize` | explicit gate before pipeline build | Silent degrade; stderr `TelemetryEmitter` keeps working |
| 6 | `PrewarmAsync` (network half) | gated under `!opts.Offline` | `PrewarmCompatAsync` (build-only, no network) still runs as a perf nicety |

Plus one *reporting* surface: `ProviderDoctor.ProbeAsync` returns
`dns: blocked-offline`, `healthy: false` for non-loopback rows when
the latch is on. Loopback rows are not falsely marked.

### Layered model (the hardest call to get right)

`--offline` does NOT relax `AZ_AI_LOCAL_PROVIDERS=1`. Loopback hosts
still need the opt-in. This was the decision Larry signed off on:

LARRY: One flag should not unlock another flag.

NEWMAN: Agreed. The opt-in is consent for "I know I am running a
local model and accept the risks." Offline is consent for "I know I
am air-gapped and refuse non-loopback." They are independent axes.

The tests pin BOTH directions:

- `Offline_HttpLocalhost_NoOptIn_StillBlockLoopback` -- offline +
  loopback URL + no opt-in -> still `BlockLoopback`.
- `Offline_HttpLocalhost_WithOptIn_Allowed` -- offline + loopback +
  opt-in -> `Allow`.
- `Offline_HttpsPublic_Blocked` -- offline + public + no opt-in ->
  `BlockOffline`.
- `Offline_HttpsPublic_Blocked` (with opt-in toggled) -- offline +
  public + opt-in -> still `BlockOffline`. Opt-in does not relax
  offline.

### Strict-equality env

`AZ_AI_OFFLINE=1` activates the gate; "true" / "yes" / "1 " /
"01" / unset all leave it OFF. Mirrors `AZ_AI_TELEMETRY` and
`AZ_AI_LOCAL_PROVIDERS`.

Theory case: `OfflineEnv_StrictEqualityOnly`, six inline rows.
Integration row: `AZ_AI_OFFLINE=true does not enable (strict-equality '1' only)`.

The strict equality is *fail-closed against typos*. An operator who
types `AZ_AI_OFFLINE=tru` (typo) gets normal egress, which is the
right answer -- offline is a *consent flag*, and consent under typo
is not consent.

### Early latch (the doctor footgun)

The CLI dispatches `--doctor` *inside the parser loop* (line ~1075),
before `opts` is finalized. So a naive implementation of
`--offline --doctor` would dispatch the doctor with the latch still
unset, and the doctor would dutifully report `dns: ok` for an Azure
endpoint that should be blocked.

The fix: a tiny pre-scan in `Main()` that walks `args` for `--offline`
and `AZ_AI_OFFLINE=1` BEFORE `LoadConfigEnv`, and sets the latch.
This is additive (Kramer's S03E17 streaming work touches a different
region of `Main()`).

The integration test that pins this: `--offline --doctor` (Azure env)
exits 1 and emits `blocked-offline`. It would have failed without the
pre-scan.

---

## Test deltas

### Unit tests

- `EndpointAllowlistTests.cs`: +16 facts + 6 theory rows (S03E26
  block):

      Offline_HttpsPublic_Blocked
      Offline_HttpsPublic_BareIp_Blocked
      Offline_HttpLocalhost_NoOptIn_StillBlockLoopback
      Offline_HttpLocalhost_WithOptIn_Allowed
      Offline_Loopback127_WithOptIn_Allowed
      Offline_IPv6Loopback_WithOptIn_Allowed
      Offline_AzureShape_Endpoint_Blocked
      Offline_Rfc1918_StillBlockedAsPrivate
      Offline_CloudMetadata_StillBlockedAsLinkLocal
      Offline_DnsRebinding_MixedRecords_Blocked
      Offline_DnsResolves_Public_Blocked
      Offline_DnsResolves_AllLoopback_Allowed
      Offline_OffByDefault_DoesNotChangeBehavior
      Offline_StaticLatch_PicksUpFromTwoArgOverload
      Offline_Describe_HasActionableText
      OfflineEnv_StrictEqualityOnly  (Theory, 6 rows)

- New `tests/AzureOpenAI_CLI.Tests/OfflineModeTests.cs` (9 facts,
  `[Collection("ConsoleCapture")]`):

      ParseArgs_OfflineFlag_SetsOfflineTrue
      ParseArgs_OfflineFlag_DefaultsFalse
      ParseArgs_OfflineEnv_StrictEqualityOnly_LiftsToOptions
      Adapter_Build_OfflineBlocksPublicHttpsEndpoint
      Adapter_Build_OfflinePlusOptIn_LoopbackPresetSucceeds
      Adapter_Build_OfflineWithoutOptIn_LoopbackStillBlocked
      Doctor_Offline_AzureProvider_ReportsBlockedOffline
      Doctor_Offline_LocalhostProvider_NotMarkedOffline
      WebFetchTool_OfflineRefusesAnyHttpsUrl

The Adapter / Doctor / WebFetch tests *also* assert that no
secret-shape token (`sk-stub-not-real-redacted`, AZUREOPENAIAPI
value) appears in the error path. Defense-in-depth on the redactor
seam.

### Integration tests

`tests/integration_tests.sh` gained a "S03E26 The Offline Mode"
block with 7 hermetic assertions (every one wrapped in `env -i` so
the host's real Azure env can never leak in):

      --offline --help exits 0
      --offline --doctor (no providers) exits 0
      --offline --doctor reports blocked-offline (rc=1)
      AZ_AI_OFFLINE=1 env (no flag) gates same as --offline
      --offline --doctor --json emits blocked-offline + all_healthy=false
      AZ_AI_OFFLINE=true does not enable (strict-equality '1' only)
      --offline --doctor emits no Bearer/sk- secret shape

All seven green on first run.

### Suite totals

- Unit: 1034 / 1034 (was 989). Delta is +30 from this episode and
  some adjacent landing tests counted in the same window.
- Integration: 73 / 73 (was 66). Delta is +7 from this episode.

---

## Findings filed

| ID | Severity | Title | Owner | Lane |
|----|----------|-------|-------|------|
| `newman-2026-05-O-1` | LOW (doc) | offline is a logical gate, not a kernel boundary; recommend `unshare -n` pairing for paranoid runs | Newman + Elaine | S03E27 doc sweep |
| `newman-2026-05-O-2` | INFO (cross-ref) | offline does not close the v2.1.3 TOCTOU lane (`fdr-2026-05-A-1`) | FDR + Newman | closes when A-1 mitigation ships |
| `newman-2026-05-O-3` | LOW (UX) | silent OTLP degrade under `--offline`; surface a `--verbose` stderr note | Frank Costanza + Newman | S03E27 telemetry sweep |

All three are forward-hardening lanes. None of them changes the
v2.1.4 verdict (still GREEN).

Cross-references:

- `fdr-2026-05-A-1` (TOCTOU, MEDIUM, open): unchanged.
- `fdr-2026-05-A-2` (IPv4 short-form parser drift, LOW, open):
  unchanged.
- `fdr-2026-05-A-3` (env-var name "local providers" overstates
  scope, LOW, open): adjacent to the new `AZ_AI_OFFLINE` env-var
  name. Lloyd Braun's S03E27 onboarding sweep should surface BOTH
  names together.

---

## Conflict notes (Mr. Pitt orchestration)

- **Kramer (S03E17 *The Stream*).** Touches `Program.cs` streaming
  dispatch (different region of `Main`) and `OpenAiCompatAdapter`
  (separate file region). My changes are additive: parser branch at
  end of `ParseArgs`, gates in `BuildChatClient` (different region
  from streaming dispatch). No textual collision; CI rebase
  expected to be a no-op merge.
- **Jerry (S03E24 *The CVE Log, Per Provider*).** Touches CI / Make
  / `scripts/`. No source overlap. The new `AZ_AI_OFFLINE` env-var
  is documented in CHANGELOG and README; not Jerry's scope.
- **Frank Costanza (Telemetry).** The OTLP gate I added in
  `Telemetry.Initialize` is one `if` block; it neither alters the
  schema nor the stderr `TelemetryEmitter`. Frank reviewed offline
  via DM: signed off. `O-3` (verbose-mode stderr note) is Frank's to
  land in the next telemetry sweep.

---

## Files touched

Production:

- `azureopenai-cli/Net/EndpointAllowlist.cs`
- `azureopenai-cli/Program.cs`
- `azureopenai-cli/Observability/Telemetry.cs`
- `azureopenai-cli/Cli/ProviderDoctor.cs`

Tests:

- `tests/AzureOpenAI_CLI.Tests/EndpointAllowlistTests.cs`
- `tests/AzureOpenAI_CLI.Tests/OfflineModeTests.cs` (new)
- `tests/integration_tests.sh`

Docs:

- `docs/audits/security-v2.1.4-offline.md` (new)
- `docs/exec-reports/s03e26-the-offline-mode.md` (this file)
- `docs/exec-reports/s03-writers-room.md` (E26 row appended)
- `docs/findings-backlog.md` (O-1 / O-2 / O-3 rows appended)
- `CHANGELOG.md` (`[Unreleased]` Added entry)
- `README.md` (Air-gapped operation subsection)

---

## Operator-facing summary

```
$ az-ai --offline --doctor
provider: azure
endpoint: https://contoso.cognitiveservices.azure.com/
dns: blocked-offline
creds_present: true
models_configured: 1
healthy: false
```

```
$ az-ai --offline --raw "hello"
[ERROR] blocked by --offline mode (set AZ_AI_LOCAL_PROVIDERS=1 to
allow loopback only; non-loopback always blocked while offline)
```

```
# Air-gapped demo against a loopback Ollama
$ AZ_AI_LOCAL_PROVIDERS=1 az-ai --offline --model ollama-llama3 \
    "summarize this paragraph"
(works; talks only to 127.0.0.1:11434)
```

```
# Paranoid operator -- pair with kernel-level isolation
$ unshare -n az-ai --offline --doctor
(no syscall can reach a non-loopback socket regardless of
in-process logic)
```

---

## What we still owe

Three lanes:

1. **`O-1` doc sweep** -- README + `docs/hardening.md` "Offline depth
   model" paragraph. Lane: S03E27 doc sweep.
2. **`O-3` UX polish** -- `--verbose` stderr note when both
   `AZ_AI_TELEMETRY=1` and `--offline` are set. Lane: S03E27
   telemetry sweep, owner Frank.
3. **`fdr-2026-05-A-1` mitigation** -- pin resolved IP into custom
   `SocketsHttpHandler.ConnectCallback`. Closes both A-1 and `O-2`.
   Lane: separate hardening episode after S03E27.

---

## Tag scene -- next episode preview

INT. WRITERS' ROOM. KEITH HERNANDEZ at the whiteboard. NEWMAN's
clipboard now in J. PETERMAN's hands.

KEITH HERNANDEZ: I'm Keith Hernandez. The recording is in a week.
We need a *demo*.

J. PETERMAN: A demo. A *catalog* of demos. Each one a story. Each
one running on a single binary, no dependencies, no surprises. The
operator types a prompt. The CLI answers. The audience leans
forward.

NEWMAN: (off-camera) `--offline` is on the whole time.

J. PETERMAN: *Of course.* No bell shall ring during the talk.

KEITH HERNANDEZ: Conference WiFi? Untrusted. unshare -n? Belt and
suspenders. Stage demo? `AZ_AI_LOCAL_PROVIDERS=1 az-ai --offline
--model ollama-llama3 "..."`. We rehearse it. We record it. We pin
the asciinema cast. We ship.

LARRY: One episode.

KEITH HERNANDEZ: One episode.

CUT TO BLACK.

> *Next episode -- S03E27: The Demo.*
