# S03E22 -- *The Fallback*

> *Episode S03E22 -- file slot 23 (e21 *The Default* claimed slot 22).
> Owner: Frank Costanza (SRE / observability).
> Larry-voice exec report. ASCII only. No box-drawing.*

```
+--------------------------------------------------------------+
|  SERENITY NOW! ... wait, I have a list. Page one of three.   |
|  Page two: actually, the chain works. Page three: the chain  |
|  is a no-op until the next episode wires creds.              |
+--------------------------------------------------------------+
```

## Logline

When the primary provider goes Festivus on you, you don't throw a rock at
the endpoint. You retry on a *different* provider, in order, with a hard
depth cap, and you write down what happened on the spreadsheet. This
episode lands the spreadsheet, the cap, and the rock-control. The
"different provider" part is a finding (`frank-2026-05-FB-1`) -- the
chain is wired, the alternates are not yet credentialed.

## Three-act

**Act 1 -- the policy.** A new `Resilience/FallbackPolicy.cs` parses
`--fallback openai,groq,together` (CLI wins) or `AZ_AI_FALLBACK` (env
fallback). Validates max-depth (3), rejects duplicates, rejects empty
entries, gates against a known-preset HashSet (`azure`, `foundry`,
`openai`, `groq`, `together`, `cloudflare`, `ollama` -- AOT-clean, no
reflection over `ProviderCapabilities`). Inactive policy is the
identity-no-op; the wrap returns the primary unchanged. Zero overhead
for users who do not opt in.

**Act 2 -- the chain.** A new `Resilience/FallbackChain.cs` decorates
`IChatClient` post-`BuildChatClient`. On a *transient* primary failure
(HTTP 5xx, 429, network timeout) the chain advances. On a *non-
transient* failure -- auth (401/403), 4xx-non-429, capability mismatch,
or user-cancel via Ctrl-C -- the chain *short-circuits*. No point
asking another provider when the request itself is the problem.
Streaming has a load-bearing invariant: **once the first chunk has
been yielded, fallback is OFF.** Mid-stream truncation prints a single
`[fallback] stream-truncated` warn line on stderr and re-throws the
original exception. Switching providers mid-flight would corrupt the
user's transcript. The transcript is sacred. End of story.

**Act 3 -- the spreadsheet.** Two new additive telemetry events,
`fallback_attempt` (one per try) and `fallback_outcome` (one per chain,
the verdict). Stable key order. `error_class` routed through
`SecretRedactor` and bounded to 200 chars. Strict-equality opt-in
(`AZ_AI_TELEMETRY=1` literal-only -- "true", "yes", "1 " all rejected).
Two new SLIs in `docs/observability/slo.md`: `fallback.rate` (target
<= 5% over 28 days) and `fallback.recovery_rate` (target >= 80% when
fallback fires). Alert thresholds: info above 5% over 1h, page above
20% over 15m. Festivus-grade observability.

## What shipped

```
+ NEW  azureopenai-cli/Resilience/FallbackPolicy.cs   ~140 LOC
+ NEW  azureopenai-cli/Resilience/FallbackChain.cs    ~410 LOC
+ NEW  tests/AzureOpenAI_CLI.Tests/FallbackChainTests.cs   47 facts

E EDIT azureopenai-cli/Program.cs
       - ParseArgs: `case "--fallback":` (consumes next argv as value)
       - ShowHelp: --fallback <list> block under Air-gapped/Offline section
       - Bash completion: --fallback added to opts string
       - RunAsync: post-BuildChatClient wrap region;
         FallbackPolicy.Resolve(args, env); HasError -> rc=2;
         IsActive -> Wrap with production factory that always returns
         Skipped("no-fallback-creds (frank-2026-05-FB-1)").

E EDIT azureopenai-cli/Observability/TelemetryEmitter.cs
       - +FallbackAttemptEvent record
       - +FallbackOutcomeEvent record
       - +EmitFallbackAttempt(...)  (no-op when telemetry disabled)
       - +EmitFallbackOutcome(...)  (no-op when telemetry disabled)
       - +SerializeFallbackAttempt / SerializeFallbackOutcome
         (manual Utf8JsonWriter, stable key order)
       - Original TelemetryEvent schema and Emit/StartDispatch surface
         UNTOUCHED.

E EDIT azureopenai-cli/AzureOpenAI_CLI.csproj
       - InternalsVisibleTo("AzureOpenAI_CLI.Tests") so tests can reach
         Resilience.* internals (Classify / ErrorClassLabel / Wrap).

E EDIT tests/integration_tests.sh
       - +6 assertions under "▸ S03E22 fallback chain":
         help-mentions / unknown-preset / depth>3 / duplicate /
         missing-value / env-valid-parses-ok.

E EDIT README.md         (new opt-in fallback paragraph + example)
E EDIT CHANGELOG.md      ([Unreleased] Added entry)
E EDIT docs/observability/slo.md
       - Section 2: +4 SLI rows (fallback.{rate, recovery_rate,
         exhaustion_rate, stream_truncated_rate})
       - Section 3: +2 SLO rows (fallback.rate <= 5%/28d;
         recovery_rate >= 80%)
       - Section 5: +3 alert thresholds
       - Section 8: +3 cross-references
E EDIT docs/findings-backlog.md
       - +frank-2026-05-FB-1  (LOW, open) production factory always
         skipped -- per-preset cred discovery not yet wired
       - +frank-2026-05-FB-2  (INFO, open) `--config show` does not
         echo resolved fallback chain + source

+ NEW  docs/exec-reports/s03e23-the-fallback.md   (this file)
E EDIT docs/exec-reports/s03-writers-room.md      (E22 GREEN row,
       file slot 23, points here)
```

## Wave table

```
+------+----------------------+--------+----------------------------+
| WAVE | FILE                 | KIND   | NOTE                       |
+------+----------------------+--------+----------------------------+
|   1  | FallbackPolicy.cs    | NEW    | parser + validator + Resolve |
|   1  | FallbackChain.cs     | NEW    | decorator + Classify       |
|   1  | TelemetryEmitter.cs  | EDIT   | +2 events, additive        |
|   1  | AzureOpenAI_CLI.csproj| EDIT  | +InternalsVisibleTo        |
|   2  | Program.cs           | EDIT   | wrap region + parser case  |
|   2  | FallbackChainTests   | NEW    | 47 facts                   |
|   3  | integration_tests.sh | EDIT   | +6 assertions              |
|   3  | README / CHANGELOG   | EDIT   | docs                       |
|   3  | slo.md               | EDIT   | +4 SLIs +2 SLOs            |
|   3  | findings-backlog.md  | EDIT   | +FB-1 +FB-2                |
|   4  | s03e23-the-fallback  | NEW    | this report                |
|   4  | s03-writers-room.md  | EDIT   | E22 GREEN row              |
+------+----------------------+--------+----------------------------+
```

## Metrics

```
unit tests:        47 new facts -> 1254/1254 passing locally
integration:        6 new assertions -> 106/106 passing (2 skipped, no-creds)
build:              0 warnings, 0 errors (Debug + Release)
chain depth cap:    3 (rejects 4)
known presets:      7 (azure, foundry, openai, groq, together,
                    cloudflare, ollama)
short-circuit on:   auth / 4xx-non-429 / capability / user-cancel
fallback on:        5xx / 429 / network-timeout / TaskCanceled-no-token
stream invariant:   post-first-chunk -> truncate + rethrow (never switch)
telemetry events:   2 new shapes, additive, stable key order
opt-in:             AZ_AI_TELEMETRY=1 strict equality (untouched)
```

## Lessons from this episode

1. **The wave-on-collision problem is real.** Sibling agents (e17, e21,
   e23) running concurrently on the same working directory wiped this
   episode's first wave between checkpoints. Re-applied. Filed against
   orchestration cadence.
2. **Stream invariant is the contract.** Two reviewers asked "why don't
   we replay the prefix to the alternate?" Because the prefix has
   already been *seen* by the user. The transcript is the source of
   truth, not the upstream session. We do not retroactively edit
   reality.
3. **Chain without creds is a placebo.** Production factory ships in
   `Skipped` mode; the policy parses, the chain validates, the
   telemetry emits, the alternates never fire. This is a deliberate
   "ship the seam, find the cred plumbing" call -- finding
   `frank-2026-05-FB-1` is the receipt. Better than shipping an
   unsafe cred scrape.
4. **Strict-equality opt-in beats fuzzy opt-in.** Reusing the
   `AZ_AI_TELEMETRY=1` gate (literal-"1"-only) means the privacy
   surface for fallback events is the same one privacy reviewers
   already signed off on. No new opt-in to argue about.

## Tag scene -- the festivus pole

```
~~ COLD OPEN, S03E?? *THE BUDGET* ~~

INT. - GARAGE - LATE.  FRANK at a folding card table, ledger in front
of him.  ESTELLE off-stage washing dishes.

FRANK   (to himself)   The fallback rate is two percent.  I budgeted
        five.  We have headroom.  HEADROOM!  YOU HEAR THAT, GEORGE?
        WE HAVE HEADROOM!

(beat)

FRANK   ... but the *exhaustion* rate is sixty.  Sixty percent!  The
        chain is a placebo.  The chain is a... a dummy mannequin in a
        store window!  The customer thinks there's resilience and
        there's nothing!  Just air and fabric!

(picks up phone)

FRANK   Get me ... get me ... whoever owns the credential discovery.
        FB-1.  IT'S FRANK.  FB-1!

                                                         FADE OUT.
```

-- Frank Costanza, retired Army cook, current SRE, pole-in-the-living-room.
