# ADR-015 -- Fallback policy doctrine

- **Status:** Accepted (S04E07 *The Fallback*, Wave 1)
- **Date:** 2026-06-04
- **Authors:** Frank Costanza (policy surface), Newman (security appendix)
- **Co-leads / reviewers:** David Puddy (chaos corpus, Wave 2),
  Kenny Bania (latency tail, Wave 2), Morty Seinfeld (cost appendix,
  Wave 2), FDR (adversarial appendix, Wave 3), Mickey Abbott (WARN
  a11y, Wave 3)
- **Linked:** ADR-012 (registry seam, E01), ADR-013 (capability gate,
  E03), ADR-014 (output formatting, E04), brief
  `docs/episode-briefs/s04e07-the-fallback.md`

## Context

It is 2:47 AM in Bangalore. The trigger fires. Azure says 429. Today
the keystroke dies and the user types the reply by hand.

`az-ai` is a headless CLI whose primary callers are text-expansion
runtimes (Espanso, AHK, cron, CI hooks). These callers have no retry
loop of their own -- when the binary exits non-zero, the keystroke is
lost. A single 429 from the upstream provider therefore costs a user
visible failure, not a recoverable hiccup. Competing surfaces (Vercel
AI SDK, LangChain, LiteLLM) ship cascading fallback as a baseline, not
an upgrade.

This ADR records the doctrine for how `az-ai` adds a cascading
fallback chain on top of two existing decisions:

- **ADR-012** establishes the model registry seam: `AZUREOPENAIMODEL`
  is a comma-separated allowlist with declaration order as the
  canonical preference order. The chain walks this list.
- **ADR-013** establishes the capability gate: a candidate model must
  satisfy the user's expressed requirement flags (streaming, tools,
  schema, vision) before any network call. The chain re-applies the
  gate per candidate so an unfit fallback is skipped without burning
  retry budget or billing the tenant.

The chain composes with both: the resolver's E05 pick is attempted
first, the gate is re-applied per fallback candidate, and the
declaration order from ADR-012 supplies the walk order. No new
contract surface is invented at this layer; the chain is an
orchestrator over decisions ADR-012 and ADR-013 already locked.

A prior surface exists. `azureopenai-cli/Resilience/FallbackChain.cs`
was authored in S03E22 and ships a similar shape (capability-aware
walk, streaming pre-first-token invariant, WARN-per-hop suppressed
under `--raw`). E07 formalises that surface, writes its doctrine
down, and documents the env-var contract that operators rely on.

## Decision

The fallback policy is implemented in
`azureopenai-cli/Resilience/FallbackChain.cs` (formalised from the
S03E22 surface; rename to `Reliability/` is an open question for
Frank in Wave 2). The policy surface is the meat of the Frank-owned
half of this ADR; outlined here, defined operationally in code.

### Defaults

- **Same-model retries:** 2 attempts after the initial call (so 3
  total calls against the primary before falling through). Backoff
  is exponential with full jitter, capped at 5000 ms per sleep.
- **Wall-clock budget:** 30 000 ms across all retries and fallbacks
  for the chain. On budget expiry the chain ends with rc=3.
- **Walk order:** resolver pick first, then `AZUREOPENAIMODEL`
  declaration order with the resolver pick skipped on the second
  pass.
- **Per-candidate capability gate:** ADR-013 evaluator runs before
  any network call for each candidate. Mismatches skip without
  consuming retry budget.

### Transient-error set (closed for E07)

HTTP 429, 500, 502, 503, 504, and `TaskCanceledException` /
`OperationCanceledException` raised by HTTP timeout. All other 4xx
(400, 401, 403, 404, 422) are hard failures: no same-model retry, no
fallback. Adding a row requires an ADR-015 amendment and a Puddy
test case.

### Env-var overrides (the operator-visible surface)

| Variable | Default | Clamp | Semantics |
|----------|---------|-------|-----------|
| `AZ_AI_FALLBACK_RETRIES` | `2` | `[0, 10]` | Same-model retry count. `0` disables same-model retry but does not disable fallback. |
| `AZ_AI_FALLBACK_BUDGET_MS` | `30000` | `[0, 600000]` | Wall-clock ceiling across the chain. `0` disables fallback entirely. |

There is no `--no-fallback` CLI flag. The env-var-only posture
mirrors ADR-013's gate-bypass posture: configuration belongs in
`~/.config/az-ai/env`, not in every keystroke. Rejection of the CLI
flag is recorded in Alternatives Considered below.

### Streaming invariant

Fallback fires only **before** the first
`StreamingChatCompletionUpdate` is yielded. After the first token,
the primary error class propagates untouched to the caller. The
chain never re-rolls a partially streamed response.

### Telemetry surface

- One TRACE NDJSON line per attempt with `model`, `attempt_index`,
  `reason_code`, and `ts_ms`.
- One single-line WARN on stderr on multi-hop success, suppressed
  when **both** `--raw` and `--json` are set.
- A `_fallback_chain` array in JSON-mode responses recording each
  model attempted and an outcome label
  (`success`, `transient-retry-exhausted`, `hard-fail`,
  `capability-skip`, `budget-expired`).

### Exit-code discipline

- `rc=0` on success, including success via fallback.
- `rc=2` when the resolver pick fails the capability gate at startup
  (matches ADR-013).
- `rc=3` when the chain is exhausted without a successful response
  (new with this ADR).

## Consequences -- security appendix (Newman)

The fallback chain converts one keystroke into up to N billable
provider calls. The security review treats that amplification factor
as the primary risk and pins down the operator-visible ceilings,
foot-guns, and abuse vectors that follow from it. The streaming
invariant and the telemetry shape are reviewed for silent rebill and
prompt leakage respectively.

### Retry amplification threat model

A single user keystroke (one Espanso trigger, one AHK hotkey, one
cron invocation) MUST NOT produce N billable provider calls without
a ceiling the operator can see and override. The chain composes two
multipliers:

- **Same-model retries:** up to `AZ_AI_FALLBACK_RETRIES + 1` calls
  per candidate (default 3).
- **Candidate fan-out:** up to `len(AZUREOPENAIMODEL)` candidates,
  minus capability-skip entries (which do not bill).

Worst-case call count per keystroke is therefore
`(RETRIES + 1) * |AZUREOPENAIMODEL|`, capped above by
`AZ_AI_FALLBACK_BUDGET_MS / min_per_call_latency`. The wall-clock
budget is the hard ceiling; the retry count is the per-candidate
ceiling. Both are operator-visible env vars with sane defaults and
documented clamps.

### Default-bounded spend

With shipped defaults (`RETRIES=2`, `BUDGET_MS=30000`) and a typical
two-model `AZUREOPENAIMODEL` list, the worst-case bill from one
prompt is bounded by:

```text
worst_case_calls = (2 + 1) * 2 = 6 calls
worst_case_cost  = 6 * per_call_cost(model)
worst_case_time  = min(6 * per_call_latency, 30000 ms)
```

For a three-model list at default retries the call ceiling is 9.
Operators who run longer chains (cost-tier escalation patterns) are
expected to set `AZ_AI_FALLBACK_BUDGET_MS` explicitly. Morty owns the
cost appendix in Wave 2 and writes the per-tier math.

### Env-var foot-guns

Two values deserve explicit documentation because their natural
reading misleads:

- `AZ_AI_FALLBACK_BUDGET_MS=0` means **no fallback at all**, not
  "infinite budget". The chain executes only the resolver pick with
  its own retries (or zero retries if `RETRIES=0`). This is the
  documented disable-switch. It is not a misnomer for "no ceiling";
  there is no env-var spelling for "no ceiling" by design.
- `AZ_AI_FALLBACK_RETRIES=999` is **clamped to 10 by the code**
  before the chain runs. The clamp is silent at config-load time and
  surfaces in the next TRACE NDJSON line as `clamped: true`. A user
  who sets 999 expecting heroic resilience gets the documented
  ceiling instead of an unbounded billing event. The clamp is a
  security control, not a usability nicety; operators who want more
  than 10 retries must amend this ADR and the code together.

Both env vars are read once at startup by `LoadConfigEnvFrom()` and
captured into the chain at construction. A mid-process re-read is
not supported in E07; hot-reload of the budget is its own episode.

### Abuse vectors against the tenant key

The tenant's Azure OpenAI key (`AZUREOPENAIAPI`) is the asset under
threat. The relevant abuse path is **upstream amplification**: a
misconfigured or hostile upstream returning 429 forever causes every
candidate to be retried up to `RETRIES + 1` times against a single
keystroke. The default ceiling (6 calls for a two-model list) is the
mitigation. The chain runs each candidate at most
`RETRIES + 1` times per outer attempt and then surfaces the failure;
there is no outer retry loop above the chain.

Confirmed against the S03E22 surface: a permanent 429 from every
candidate produces exactly `(RETRIES + 1) * |AZUREOPENAIMODEL|`
calls and then exits rc=3. If Frank's Wave 1 implementation deviates
from this -- in particular if any outer loop re-enters the chain
after exhaustion -- Newman files a finding via the
`findings-backlog` skill and blocks Wave 2 sign-off.

Adjacent vectors considered and dismissed for E07:

- **Credential exfiltration via fallback:** the chain reuses the same
  `AZUREOPENAIAPI` for every candidate routed to Azure OpenAI; no
  per-candidate key, no per-candidate endpoint. A compromised
  candidate cannot harvest a different key. Cross-provider failover
  (Azure OpenAI -> Foundry) is explicitly out of scope for E07; that
  is S05 territory and gets its own threat model.
- **Cache-poisoning via chain replay:** there is no on-disk cache of
  prior chain decisions. Each invocation starts fresh.

### Capability re-check semantics

When fallback selects the next candidate, **the capability gate runs
again** against that candidate's registry entry before any network
call is dispatched. This is the documented contract. The gate result
is computed per candidate at chain time, not cached from resolver
time. Rationale: candidate N may advertise different capabilities
than candidate 1 (a vision-flagged prompt routed to a non-vision
fallback must skip, not 4xx after the network round-trip).

**Open question for Frank:** if the registry is hot-reloaded between
the resolver's gate evaluation and the chain's re-evaluation of the
resolver pick, the two answers may disagree. The brief proposes
capturing the resolver-time gate result and reusing it for the
primary; Newman recommends re-evaluating once at chain start and
treating the resolver-time result as advisory only. Frank decides at
Wave 2 sign-off; the test cases in Puddy's suite must pin whichever
choice ships.

### Streaming pre-first-token invariant (operator contract)

The contract written for operators and headless callers:

> Once bytes flow to the caller, the chain stops. There is no silent
> rebill, no second model getting paid for half a response, no
> mid-stream re-roll. A failure after the first token propagates
> with the primary error class preserved and the same rc the primary
> would have produced.

Implication for billing: a partial streamed response counts as one
billable call. The chain cannot turn it into two. This is the
property that lets us call the chain "best-effort" without
qualifying every cost statement.

## Compliance and privacy

The TRACE NDJSON surface emits the following fields per attempt and
**only** the following fields:

- `model` -- the deployment name being attempted (string, ASCII).
- `attempt_index` -- integer, zero-based.
- `reason_code` -- outcome label from the closed enum above.
- `ts_ms` -- monotonic milliseconds since chain start.
- `clamped` -- boolean, present only on the first line when an
  env-var was clamped.

The surface **does not emit** prompt text, response text, tokens,
token counts, message roles, system-prompt content, tool-call
arguments, schema payloads, or any other request body field. This is
an explicit non-secret-leakage guarantee. A future telemetry
extension that wants to add a body field requires an ADR-015
amendment and Newman review; the field list above is closed in the
same posture as the transient-error set.

The TRACE stream is opt-in, off by default, and never written to
disk by the CLI itself. Operators who pipe TRACE to a log collector
own that collector's retention policy. The CLI's responsibility ends
at stderr.

## Alternatives considered

- **No fallback at all (the pre-E07 status quo).** Rejected. ADR-013
  gives users a capability gate so they can declare requirements via
  flags; the gate's promise is "I will refuse a wrong model, not
  silently degrade". Without fallback the gate forces the user to
  choose between strictness (rc=2 on first 429) and laxity (no
  requirements declared). Fallback is the third option the gate's
  promise implies: try the declared list in order, refuse any that
  cannot meet the requirements, surface only when none can.
- **Always-fallback-no-cap.** Rejected. Without a wall-clock budget
  a single keystroke can run for minutes and bill for every second.
  The default 30 s ceiling is short enough that an Espanso user
  notices and long enough that a healthy second candidate has time
  to answer. The clamp on `AZ_AI_FALLBACK_BUDGET_MS` to 600 000 ms
  (10 minutes) is the hard upper bound; operators who need more are
  out of scope.
- **Fallback only on HTTP 429.** Rejected. Field telemetry from
  comparable CLIs and the team's own incident history put 5xx
  (especially 503 during regional restarts) at higher frequency
  than 429 for the Azure OpenAI surface. A 429-only chain would
  miss the most common transient class. The closed transient set
  above covers both.
- **`--no-fallback` CLI flag.** Rejected. The headless-caller
  contract is that one keystroke produces one binary invocation
  with stable behaviour configured at the env layer. Adding a flag
  every Espanso trigger must remember to set is the wrong default.
  Operators who want fallback off set `AZ_AI_FALLBACK_BUDGET_MS=0`
  in `~/.config/az-ai/env` once and forget it.
- **Polly retry policies.** Rejected. `grep Polly` across
  `azureopenai-cli/` and the `.csproj` returns zero matches; the
  AOT delta budget cannot absorb a reflection-heavy fluent retry
  DSL. The chain is hand-rolled. See AOT delta budget in the brief.

## References

- `docs/episode-briefs/s04e07-the-fallback.md` -- the E07 brief
  this ADR records
- `docs/adr/ADR-012-model-registry-seam.md` -- registry walk order
- `docs/adr/ADR-013-capability-gate.md` -- per-candidate gate
  semantics
- `docs/adr/ADR-014-output-formatting-standard.md` -- ASCII / NO_COLOR
  contract the WARN summary inherits
- `azureopenai-cli/Resilience/FallbackChain.cs` -- prior surface
  authored in S03E22; E07 formalises
- `azureopenai-cli/Resilience/FallbackPolicy.cs` -- policy struct
  the chain consumes
- `.github/skills/findings-backlog.md` -- the procedure Newman
  follows if Wave 1 implementation deviates from this ADR

No feature proposal exists for fallback in `docs/proposals/` as of
this ADR; this ADR is the doctrine entry point. If a future FR is
authored, it links back here.

## Open questions handed to Frank (Wave 2 review)

1. **Path rename.** Brief proposes
   `azureopenai-cli/Reliability/FallbackChain.cs`; the file ships at
   `azureopenai-cli/Resilience/FallbackChain.cs` today. Pick one and
   amend the path references throughout this ADR before Wave 2
   sign-off.
2. **Capability re-check on the resolver pick.** Re-evaluate at chain
   start (Newman's recommendation) or reuse the resolver-time
   result? The Puddy suite must pin whichever choice ships.
3. **Per-attempt timeout vs chain budget.** The brief documents a
   chain-wide `AZ_AI_FALLBACK_BUDGET_MS`. Does each individual call
   inherit a per-call timeout, or does it inherit the remaining
   chain budget? A 30 s chain budget with no per-call timeout lets
   one hung connection consume the whole budget; a per-call timeout
   prevents that but adds a second env var.
4. **TRACE NDJSON schema versioning.** The field list above is
   closed. Does the first emitted line carry a `schema_version: 1`
   field so downstream collectors can detect future amendments
   without re-parsing every line?
5. **Clamp visibility.** Should clamped env-var values raise a
   stderr WARN at startup in addition to the `clamped: true` TRACE
   field? Operators who set `RETRIES=999` and expect 999 retries
   currently get silent clamping. Newman recommends a one-time WARN
   when `--raw` is not set.

These are not blockers for the Wave 1 deliverable; they are the
exact set of decisions Frank owns at Wave 2 sign-off.

## Cost amplification appendix (Morty)

A single user keystroke -- one `az-ai "..."` invocation from
Espanso, AHK, cron, or a shell prompt -- is the budgetary unit
this appendix accounts for. Without fallback, one keystroke
produces exactly one provider call. With the fallback chain,
one keystroke can produce up to N provider calls, where N is
bounded by the env-clamped policy in
`azureopenai-cli/Resilience/RetryEnvelope.cs` (`RETRIES` clamped
to `[0, 10]`, `BUDGET_MS` clamped to `[0, 60000]`).

### Default-bounded worst case

With the shipped default `AZ_AI_FALLBACK_RETRIES=2` (= 3 attempts
per candidate) and a candidate allowlist of length K, the worst
case is:

```text
calls_per_keystroke = (RETRIES + 1) * K = 3 * K
```

For the canonical configuration
`AZUREOPENAIMODEL=gpt-5.4,gpt-4o,gpt-4o-mini` (K = 3), that is
**9 calls per keystroke** in the absolute worst case (every
attempt on every candidate exhausts retries before the chain
fails out).

### Per-keystroke cost formula

For a representative cheap input model -- gpt-4o-mini at a
placeholder list price of `$0.15` per million tokens (note:
exact published rates drift; this section is structure, not
pricing-of-record -- substitute the current rate when budgeting)
-- and a representative 200-token prompt + 200-token completion
(400 tokens billable per call):

```text
worst_case_cost = calls * (tokens_per_call / 1_000_000) * rate
                = 9 * (400 / 1_000_000) * $0.15
                = $0.00054 per keystroke
```

At a sustained 1000 keystrokes/day (heavy Espanso macro user),
that is `$0.54/day` or roughly `$16/month` -- and that is the
**worst case**, assuming every fallback exhausts. The realistic
steady-state (see below) is one to two orders of magnitude
cheaper.

### Env-clamp absolute worst case

The clamps in `RetryPolicy.FromEnvironment` permit
`AZ_AI_FALLBACK_RETRIES=10` (the max), and nothing in the policy
caps K -- an unusually long allowlist of K = 10 is permitted.
That yields:

```text
calls_per_keystroke_max = (10 + 1) * 10 = 110 calls
```

This is the absolute ceiling the env clamps allow per
keystroke. At the same per-call rate above, that is
`110 * 0.0000600 = $0.0066 per keystroke` worst case. Operators
who set both knobs to the maximum should know what they signed
up for.

### Practical realism

The amplification factor in steady state is **1.0**, not 3K or
110. Most invocations succeed on attempt 1 of candidate 1, which
means the fallback chain adds zero billable overhead in the
success case -- the additional candidates and retries are never
called. Bania's Wave 2 latency tail bench confirms the
performance side of the same observation: success-path latency
is unchanged from pre-fallback. The cost amplification only
materializes during incidents (provider 429/503 bursts), which
is precisely when the fallback chain is earning its keep -- the
alternative is keystroke loss.

### Recommendations by caller tier

- **Default (`RETRIES=2`)** is conservative but correct for
  general use. One retry covers the typical transient 429/503
  hiccup; the second retry covers the rarer back-to-back miss
  before falling over to the next candidate.
- **High-volume automation users** (Espanso macros, AHK
  hotkeys, shell completion hooks) should set
  `AZ_AI_FALLBACK_RETRIES=0` to disable retries entirely. These
  callers re-issue the keystroke themselves on failure (the user
  just types again), so per-call retry buys nothing and
  multiplies the worst-case bill by 3x.
- **Long-running agent loops** (E08 ralph, S05 multi-step
  workflows) should keep the default. The loop cannot
  cheaply re-issue a failed step, and the exponential backoff
  inside the retry envelope naturally smooths bursty
  rate-limit responses without operator intervention.
- **Paranoid cost-cap operators** should clamp at the wrapper
  level (a daily token budget enforced before `az-ai` is
  invoked). In-process cost caps are out of scope for E07; the
  S05 cost-cap episode will revisit whether to add a
  `AZ_AI_DAILY_TOKEN_CAP`-style hard stop.

### Observability hook

Every fallback hop emits a TelemetryEmitter NDJSON line. Frank's
Wave 3 work (per AC#10) will add a `_fallback_chain` payload to
JSON-mode output summarizing the hops for that invocation. The
operator can sum hops across the NDJSON stream (or the JSON
payload) to compute the **actual** per-invocation amplification
factor for their workload, rather than relying on the worst-case
math above. Empirical amplification is the number FinOps
should budget against once telemetry is wired; this appendix
is the upper bound that bookends it.

## Adversarial scenarios appendix (FDR)

Newman wrote the security frame; Morty bounded the bill. This
appendix catalogues the *failure modes* the fallback chain has to
absorb without operator surprise. Each entry maps an attack or
chaos scenario to the **current behaviour** of the W1 executor +
W2 chaos-tested envelope, the **blast radius** an operator should
expect, and a **mitigation status** (mitigated / accepted residual
/ open). Coverage in Puddy's W2 chaos suite
(`tests/AzureOpenAI_CLI.Tests/FallbackChainChaosTests.cs`, commit
`ecb23ad`) is flagged inline; uncovered scenarios are tagged
**TEST GAP** for a Wave-4 or S04E08 follow-up.

### 1. Total 429 mob -- every candidate rate-limited

- **Mechanism:** All K candidates in the chain return HTTP 429
  simultaneously (regional rate-limit storm, tenant-wide quota hit,
  or an upstream incident burning quota across providers).
- **Current behaviour:** Each candidate exhausts its
  `RETRIES+1` attempts with exponential backoff, then the chain
  advances. After the last candidate the user sees the final
  429 surfaced as the underlying error.
- **Blast radius:** Up to `(RETRIES+1) * K` upstream calls per
  invocation -- the worst case Morty bounded at 9x for default
  settings (3 candidates * 3 attempts). User-visible latency
  approaches `BUDGET_MS` if backoff is unbounded by the budget.
- **Mitigation:** Tenant/key visibility through `TelemetryEmitter`
  WARN lines (one per hop) lets the operator detect a
  cross-candidate quota storm and stop re-trying. Env clamp
  `BUDGET_MS<=60000` caps the worst-case wall-clock.
- **Status:** Accepted residual. Single-candidate variant is
  covered by `AllAttemptsTransient_SurfacesUnderlyingError`
  (line 142). **TEST GAP:** no multi-candidate "total 429"
  test in the W2 suite -- the chaos suite is single-`IChatClient`
  scoped; the FallbackChain integration test that exercises
  multiple candidates lives in a separate fixture and does not
  drive every candidate into 429 at once. Flag for Puddy's W4
  pickup.

### 2. Connection-phase hang

- **Mechanism:** One candidate hangs indefinitely on DNS resolution,
  TCP SYN, or TLS handshake -- the canonical "black hole" failure
  mode (silently dropped packets, mid-handshake stall, captive
  portal). No exception fires; the call just never returns.
- **Current behaviour:** The wall-clock `BUDGET_MS` budget check
  in the retry envelope terminates the entire chain when the
  budget is exceeded. The hanging candidate is abandoned via
  the `CancellationToken` plumbed into the envelope's
  per-attempt call.
- **Blast radius:** Up to `BUDGET_MS` (default 5000ms) burned on
  one candidate before the chain gives up. All later candidates
  in the chain are starved -- the operator sees a budget-exhausted
  error, not a fallback.
- **Mitigation:** Open. A per-call HTTP timeout (separate from
  `BUDGET_MS`) would let the chain abandon one bad candidate
  faster and still try the next one within the same budget.
  This is the W2 open question Frank flagged in his
  RetryEnvelope review.
- **Status:** **Open** -- tracked as the W2 open question. Partial
  coverage by `BudgetExhausted_ThrowsFallbackBudgetExhaustedException`
  (line 285), which proves the budget *terminates* the chain but
  uses a `Status(503)` failure mode rather than a true connection
  hang. **TEST GAP:** no chaos-suite mode that simulates a
  DNS/TCP/TLS-phase hang (e.g. a `FailureMode.Hang(TimeSpan)`).

### 3. Mid-stream connection reset

- **Mechanism:** Candidate streams 50 tokens of a response, then the
  TCP connection is reset (server crash, load balancer drain,
  intermediate proxy timeout).
- **Current behaviour:** Per the streaming pre-first-token retry
  invariant (W1 acceptance criterion): once a token has been
  surfaced to the user, no retry fires. The partial response is
  emitted; the exception terminates the stream.
- **Blast radius:** User sees a truncated output. No double-billing
  for re-prompting the model with a prefix.
- **Mitigation:** Documented as **feature, not bug** -- the
  alternative (silently retrying after partial output) would
  double-bill the user for prefix tokens and risk emitting two
  distinct completions concatenated.
- **Status:** Mitigated by design. Fully covered by
  `Streaming_PostFirstTokenFailure_DoesNotRetry_PartialSurfaced`
  (line 258) and its pre-first-token sibling
  `Streaming_PreFirstTokenFailure_Retries` (line 239).

### 4. Capability-gate hot-reload race

- **Mechanism:** Registry reloads between the resolver's
  capability-gate check and the chain's chat call. A candidate
  that satisfied the gate at startup (e.g. claimed `tools`
  capability) is hot-reloaded to a definition that drops the
  capability. The chain still attempts it.
- **Current behaviour:** The capability gate is checked at chain
  *entry*, not per attempt. The now-invalid candidate is called;
  the provider returns a 400 (or 422) for the unsupported
  capability; the W2 classifier treats 4xx-except-429 as
  non-transient; the chain advances to the next candidate.
- **Blast radius:** One wasted call to the now-invalid candidate
  per invocation while the stale gate persists. No retries (4xx
  is non-transient), so the cost is bounded at one round-trip.
- **Mitigation:** A per-attempt gate re-check would close the
  race but doubles the gate latency on every hop. **Backlog
  item** -- not worth the latency tax for a race window measured
  in seconds (the registry hot-reload debounce is on the order
  of the file-watcher quanta).
- **Status:** **Open / accepted residual.** **TEST GAP:** no
  chaos-suite test simulates a registry mutation between
  `Pick(...)` and `GetResponseAsync(...)`. The
  `HardError400_NoRetry_SurfacesImmediately` test (line 166)
  covers the *outcome* (one wasted call, chain advances) but
  not the *mechanism* (mid-flight registry mutation).

### 5. Backoff-pumping DoS

- **Mechanism:** An attacker who controls the upstream's responses
  (e.g. via prompt injection in a previous step of an agent loop
  that influences which model is called next, or a compromised
  endpoint returning crafted 503s with `Retry-After` headers)
  drives the retry envelope into its worst-case attempt count
  to inflate the bill and stall the loop.
- **Current behaviour:** Env clamps enforce `RETRIES<=10` and
  `BUDGET_MS<=60000` regardless of attacker-supplied headers.
  The classifier ignores `Retry-After` for backoff scheduling
  (W2 uses fixed exponential with jitter; honouring
  `Retry-After` was rejected in the W1 design).
- **Blast radius:** Bounded at 11 calls per candidate, 60s
  wall-clock, total bill cap from Morty's appendix.
- **Mitigation:** Mitigated. The clamps are belt-and-suspenders
  against operator misconfiguration **and** adversarial input.
- **Status:** Mitigated. Covered by
  `EnvRetries999_ClampedToMaxRetriesAllowed` (line 386) and
  `EnvBudgetMs999999_ClampedToMaxBudgetMs` (line 400).

### 6. Header-injection via WARN

- **Mechanism:** A model name (from registry, env, or CLI flag)
  contains terminal-injection bytes -- ANSI CSI escapes, carriage
  returns to overwrite previous output, NUL bytes -- that reach
  the `[WARN] fallback:` stderr emission.
- **Current behaviour:** `RetryEnvelope.EmitWarn` (lines 479-490)
  interpolates `_model` directly into the WARN string without
  scrubbing. The `ModelRegistry.ValidateEntries` loader
  (lines 188-235) rejects model names containing quotes,
  backslash, C0, or C1 bytes at load time (rc=99), so registry-fed
  names are filtered upstream. **However** the WARN emission
  site does not enforce its own invariant.
- **Blast radius:** If the upstream filter is ever bypassed (a
  future code path that sets `_model` from `ChatOptions.ModelId`
  or a synthetic name), raw ANSI sequences hit the operator
  terminal.
- **Mitigation:** **Open (defense-in-depth).** Filed as
  `F-FALLBACK-FDR-01` -- the fix is to mirror the
  F-S04E04-04 `ScrubForDisplay` pattern at the WARN emission
  site. The W2 test
  `WarnLine_AsciiOnly_NoAnsi_ContainsModelAttemptsOutcome`
  (line 443) only exercises a clean model name (`"gpt-test"`)
  and therefore does not catch this; it validates the
  *format string* is ASCII, not that the *inputs* are scrubbed.
- **Status:** Open. **TEST GAP** -- no chaos-suite test feeds a
  model name containing `\x1B[31m` and asserts the WARN line
  emits `?` substitutes.

### 7. Budget = 1ms edge case

- **Mechanism:** Operator sets `AZ_AI_FALLBACK_BUDGET_MS=1`
  (typo, debugging, or testing the lower bound).
- **Current behaviour:** First attempt fires unconditionally
  (the budget is checked *after* each call, not before the
  first). Post-call budget check trips; no retry; chain
  advances or surfaces the underlying error.
- **Blast radius:** Equivalent to `RETRIES=0` -- one attempt per
  candidate, no exponential ladder. Acceptable behaviour: a
  1ms budget effectively disables retry without disabling the
  initial call.
- **Mitigation:** Documented behaviour. The "first attempt is
  free" invariant is intentional (Costanza's W1 design): a
  zero-budget operator should not be unable to make any LLM
  call at all.
- **Status:** Mitigated. Partial coverage by
  `EnvBudgetMs0_DisablesEnvelope_WrapReturnsInnerUnchanged`
  (line 365) and `BudgetExhausted_...` (line 285). **Minor
  TEST GAP:** specifically `BUDGET_MS=1` (not 0, not 5000) is
  not exercised; the boundary between "envelope disabled" and
  "envelope active but starved" is one integer apart.

### 8. Cross-candidate auth drift

- **Mechanism:** Candidate 1 is Azure OpenAI (header
  `api-key: <key>`); candidate 2 is Azure AI Foundry (header
  `Authorization: Bearer <key>`). A misconfigured operator
  sets the wrong key format for candidate 2 (e.g. paste of an
  AOAI key into `AZURE_FOUNDRY_KEY`).
- **Current behaviour:** Candidate 2 returns 401. The classifier
  treats 401 as non-transient. The chain advances (or surfaces
  401 if it was the last candidate).
- **Blast radius:** One 401 round-trip surfaced with the Foundry
  candidate name in the WARN line. This is **helpful diagnostic
  output** -- the operator can grep stderr for `outcome=401
  model=foundry-*` and identify the misconfigured provider
  immediately.
- **Mitigation:** Documented. The fast-fail behaviour is correct;
  401 should not be retried (the auth header is not going to
  spontaneously become correct).
- **Status:** Mitigated. Covered in spirit by
  `HardError400_NoRetry_SurfacesImmediately` (line 166), though
  the test uses 400 rather than 401. **Minor TEST GAP:**
  401-specific test with a cross-candidate auth swap would
  document the diagnostic-output story explicitly.

### 9. Cancellation-token poisoning

- **Mechanism:** Caller passes a `CancellationToken` that is
  already cancelled before the first attempt fires (legitimate
  pattern -- caller bailed out during prompt assembly; not
  malicious so much as racy).
- **Current behaviour:** `OperationCanceledException` propagates
  before the first attempt. No upstream call is made. No retry
  schedule is started.
- **Blast radius:** Zero. No tokens billed, no provider hit, no
  WARN line emitted.
- **Mitigation:** Correct behaviour by construction (the
  envelope honours its caller's cancellation contract).
- **Status:** Mitigated. Covered in spirit by
  `ExternalCancellation_MidBackoff_ThrowsOperationCanceled_NotBudgetExhausted`
  (line 414), which proves cancellation wins over budget
  exhaustion mid-flight. **Minor TEST GAP:** the
  pre-first-attempt variant (token cancelled *before* the
  envelope is entered) is not separately asserted; the
  mid-backoff test implies it but does not exercise it.

### 10. Slow-loris streaming (creative scenario)

- **Mechanism:** Candidate accepts the request and starts the
  stream, but emits one byte every 4900ms -- just inside the
  default `BUDGET_MS=5000` per attempt yet effectively a denial
  of service. Each individual chunk arrives "in time"; the total
  response takes forever and consumes all subsequent candidates'
  budget.
- **Current behaviour:** The first token resets the
  pre-first-token retry gate (scenario 3); no retry fires.
  The chain has no concept of *inter-token* timeout, only of
  total wall-clock budget. The total budget eventually expires
  mid-stream; the partial output is what the user sees.
- **Blast radius:** Up to `BUDGET_MS` of stalled UX, no
  cross-candidate fallback (stream was started). User sees a
  truncated response with no clear signal that it was a
  slow-loris versus a genuine truncation.
- **Mitigation:** **Open / accepted residual.** Inter-token
  timeout (e.g. abandon stream if no token arrives in 1000ms)
  is out of scope for E07 and would interact subtly with
  legitimate long-thinking-pause models. Flag for S05 cost-cap
  episode alongside the per-call timeout (scenario 2).
- **Status:** Open. **TEST GAP** -- no chaos-suite test
  simulates a slow-trickle stream. Would need a
  `FailureMode.SlowStream(TimeSpan perToken)` extension to
  `ChaosChatClient`.

### Summary -- test gaps for Puddy / Wave 4 / S04E08

| #  | Scenario                       | Status     | Test gap?         |
|----|--------------------------------|------------|-------------------|
| 1  | Total 429 mob                  | Accepted   | Yes (multi-cand.) |
| 2  | Connection-phase hang          | Open       | Yes               |
| 3  | Mid-stream RST                 | Mitigated  | No                |
| 4  | Capability-gate hot-reload     | Open       | Yes               |
| 5  | Backoff-pumping DoS            | Mitigated  | No                |
| 6  | Header-injection via WARN      | Open       | Yes (F-FALLBACK-FDR-01) |
| 7  | Budget = 1ms                   | Mitigated  | Minor             |
| 8  | Cross-candidate auth drift     | Mitigated  | Minor             |
| 9  | Cancellation poisoning         | Mitigated  | Minor             |
| 10 | Slow-loris streaming           | Open       | Yes               |

Four open items (2, 4, 6, 10), one filed finding
(`F-FALLBACK-FDR-01`), seven distinct test gaps. None of the
open items are blockers for the W3 release -- they are residuals
worth their own episodes, not regressions in W1/W2 work.
