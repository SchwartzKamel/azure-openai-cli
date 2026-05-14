**Status:** GREENLIT (Larry David sign-off at S04E07 Wave 1 dispatch)

**Date:** 2026-06-04 (target film date; Frank Costanza leads when GREENLIT)
**Lead:** Frank Costanza -- SRE; owns the fallback policy and the streaming invariant
**Co-lead:** Newman -- security review of retry-amplification against the tenant key
**Support:** David Puddy -- deterministic chaos corpus and the mock client that fails on schedule
**Dependencies:** S04E01 *The Registry* (on `main`); S04E02 *Embedded Cards* (on `main`); S04E03 *The Capabilities* (on `main` -- the gate must apply per-alternate); S04E05 *The Picker* (resolver pick is the first model attempted)

# S04E07 -- The Fallback

> Log line: It is 2:47 AM in Bangalore. The trigger fires. Azure says 429.
> Today the keystroke dies. Tomorrow it does not.

---

You are filming **S04E07 *The Fallback*** for `azure-openai-cli`. Working
directory: `/home/tweber/tools/azure-openai-cli`. Branch: `main`.

---

## The story

Picture it: Bangalore, 2:47 AM. A developer named Anjali reaches for an
Espanso trigger -- `:summarize` -- expecting `az-ai` to draft the
code-review reply she was going to write tomorrow before standup. Azure
throws 429. The keystroke fails. The terminal blinks at her. The reply
does not write itself. She types it by hand, badly, and goes back to bed
angry at us.

This is the moment of truth for any text-expansion CLI: not the happy
path, the 429 path. LangChain ships fallback chains. The Vercel AI SDK
ships them. LiteLLM ships them by default and treats them as the
baseline, not the upgrade. `az-ai` does not. Tonight we fix that.

Fallback chains are not a feature. They are an expectation. We will meet
it -- with determinism, with telemetry, with budget discipline, with the
streaming invariant intact -- and we will do it without surrendering the
headless contract that brought our users here. One keystroke in, one
reply out, even when the first model is on fire.

---

## The pitch (engineering substance)

When a configured model fails -- 5xx, 429, or a connection timeout --
`az-ai` today errors out and the headless caller (Espanso, AHK, cron, CI
hook) sees a broken text expansion. No second attempt on the same model;
no attempt against the next configured model in `AZUREOPENAIMODEL`.

We add a cascading fallback chain. The resolver's E05 pick is tried first
with retry-with-backoff against the transient-error set. On exhaustion we
fall through to the next model in `AZUREOPENAIMODEL` list order, skipping
models already tried and skipping models that fail the E03 capability
gate without burning a retry budget. We stop on success, on wall-clock
budget expiry, or on list exhaustion. A single WARN line on stderr
summarises the chain for operators; a `_fallback_chain` field in JSON
output does the same for machine callers.

The chain is best-effort, deterministic in test, and silent when the
first model works -- still the overwhelming majority of requests. Nobody
pays a latency tax for the 429 case unless the 429 case happens to them.

---

## Scope (in)

- Retry-with-backoff on the resolver's chosen model for the transient
  error set; cascading fallback through `AZUREOPENAIMODEL` in declared
  order, skipping the already-tried resolver pick.
- E03 capability gate enforced per fallback candidate (skip without
  retry; capability mismatch is not transient).
- Hard wall-clock ceiling across the chain, env-var configurable.
- Streaming invariant: fallback fires only before the first token.
  Post-first-token failures propagate untouched.
- TRACE log per attempt; one summary WARN on stderr on multi-hop
  success; both suppressed when `--raw` AND `--json` are set.
- JSON-mode response includes a `_fallback_chain` array recording each
  model tried and an outcome label.
- Exit-code discipline: rc=0 success (incl. via fallback); rc=2 pre-call
  capability mismatch; rc=3 chain exhaustion.
- xUnit suite driven by Puddy's chaos mock that fails on a deterministic
  schedule (HTTP code + attempt index).
- ADR-015 records the policy doctrine and rejected alternatives.

## Scope (out)

- Cross-provider failover (Azure OpenAI -> Azure AI Foundry). That is
  S05 territory; the dispatch table is not the chain.
- Persistent circuit-breaker state across invocations. A daemon-resident
  open-circuit memory is its own episode.
- User-configurable retry classifier (which status codes count as
  transient). The set is closed for E07 and lives in code.
- A configurable per-model retry budget. One env var, one number, applied
  uniformly.
- UI for inspecting the chain after the fact (`--last-chain`). Future.
- Reconciliation with the S03E22 `Resilience/FallbackChain.cs` surface
  (see Open Questions); E07 either replaces, extends, or formalises it.
  Frank's call at greenlight.

---

## Acceptance criteria

1. `make preflight` exits 0.
2. Retries per model default to 2, configurable via
   `AZ_AI_FALLBACK_RETRIES` (no CLI flag in E07).
3. Backoff is exponential with full jitter, capped at 5000 ms per
   inter-retry sleep.
4. The transient-error trigger set is exactly: HTTP 429, 500, 502, 503,
   504, and `TaskCanceledException`/`OperationCanceledException` raised
   by HTTP timeout. All other 4xx are hard failures and skip the
   retry-on-same-model phase.
5. Fallback order is `AZUREOPENAIMODEL` declaration order. The E05
   resolver pick is tried first; remaining candidates are walked in
   list order, skipping any already attempted.
6. Each fallback candidate is run through the E03 capability gate
   before any network call. Capability-mismatched candidates are
   skipped and do not consume the retry budget.
7. One TRACE log line per attempt with model name, attempt index, and
   outcome label. One stderr WARN summary on multi-hop success, single
   line, ASCII only, suppressed when **both** `--raw` and `--json` are
   set so JSON-only callers still get stderr diagnostics.
8. Hard ceiling of 30000 ms total wall-clock across all retries and
   fallbacks for the chain, configurable via
   `AZ_AI_FALLBACK_BUDGET_MS`. On budget expiry the chain ends with
   rc=3 and the partial chain is reported.
9. Streaming mode (`--stream`): fallback fires only if the primary call
   fails before the first `StreamingChatCompletionUpdate` is yielded.
   After the first token, failures propagate to the caller with the
   primary error class preserved.
10. JSON mode (`--json`): the response object carries a
    `_fallback_chain` array listing each model attempted, the outcome
    label (`success`, `transient-retry-exhausted`, `hard-fail`,
    `capability-skip`, `budget-expired`), and the attempt count per
    model.
11. Exit codes: rc=0 on success including success-via-fallback; rc=2
    when the resolver's pick fails the capability gate at startup
    (matches E03); rc=3 when the entire chain is exhausted without a
    successful response.
12. xUnit suite (Puddy) covers: same-model retry exhaustion, one-hop
    fallback success, two-hop fallback success, capability-skip in the
    middle of the chain, budget-expiry mid-retry, hard-4xx-no-retry,
    streaming-pre-first-token fallback, streaming-post-first-token
    propagation. Each case asserts the exit code and the
    `_fallback_chain` shape.
13. `make publish-aot` binary grows by no more than 35 KB versus the
    pre-episode baseline. Bania baselines pre- and post-merge.
14. `ADR-015-fallback-policy.md` records the policy, the closed
    transient-error set, the env-var surface, and the explicit
    rejection of a `--no-fallback` CLI flag (env-var-only by design --
    falls back to the same posture as E03's gate).

---

## Dispatch plan (sub-agents and files)

| Wave | Agent | Files (file-disjoint) | Scope |
|------|-------|-----------------------|-------|
| 1 | **Frank Costanza (lead)** | `azureopenai-cli/Reliability/FallbackChain.cs` (NEW or formalised from S03E22 surface -- Frank decides at greenlight); single insertion call in `azureopenai-cli/Program.cs` (post-resolver, post-capability-gate, pre-client-construction) | Core chain executor: retry policy, backoff with jitter, transient classifier, capability-aware candidate walk, wall-clock budget, streaming pre-first-token invariant, TRACE/WARN emission, `_fallback_chain` payload. |
| 1 | **Newman (co-lead)** | `docs/adr/ADR-015-fallback-policy.md` security appendix; no production code edits in Wave 1 | Retry-amplification threat model: a single keystroke must not produce N billable calls without a ceiling the operator can see. Reviews the env-var surface for foot-guns (a `BUDGET_MS=0` foot-shot, a `RETRIES=999` foot-shot). Flags abuse vectors against the tenant key. |
| 2 | **Puddy** | `tests/AzureOpenAI_CLI.Tests/FallbackChainTests.cs` (NEW); `tests/AzureOpenAI_CLI.Tests/Fixtures/ChaosChatClient.cs` (NEW) | Deterministic mock `IChatClient` that fails on a configured schedule (status code by attempt index, optional stream truncation). One mock, one schedule per test, no time-of-day flakes. Covers every Acceptance #12 case. |
| 2 | **Kenny Bania** | `bench/FallbackChainBench.cs` or equivalent; no production edits | p99 latency tail of the happy path (no fallback). The chain must add < 0.5 ms p99 to a successful first call. If it does not, Frank rolls back to a lazier policy wrapper. |
| 2 | **Morty Seinfeld** | Append to ADR-015 cost appendix; no code edits | Cost amplification math: 1 keystroke -> N billable calls in the worst case. Documents the worst-case spend, the default-bounded spend, and the env-var ceiling. |
| 3 | **FDR** | Append-only adversarial appendix to ADR-015 | Chaos scenarios: every model returns 429; one model hangs at the connection phase eating the budget; mid-stream connection reset; a candidate that fails the capability gate after passing it at startup (registry hot-reload). No code touches in Wave 3. |
| 3 | **Mickey** | Review of the WARN summary line; may file findings | a11y of the multi-hop WARN: ASCII only, NO_COLOR honoured, screen-reader friendly. No ANSI escape sequences in the summary. |

Shared-file note: only Frank touches `Program.cs` in Wave 1, and only
for the single insertion call after the resolver and the capability
gate. The chain executor lives in its own file so reviewers can audit
the policy surface without reopening the hot path.

---

## Risks and mitigations

- **Cost amplification.** One keystroke can fan out to N billable calls.
  *Mitigation:* hard retry-count default of 2, hard wall-clock budget of
  30 s, both env-var clamped to sane maxima. Morty owns the cost
  appendix in ADR-015.
- **Latency tail.** The happy path must not pay a fallback tax.
  *Mitigation:* Bania's pre-merge bench enforces < 0.5 ms p99 added
  latency on the success path. If we cannot meet it, the chain ships
  opt-in via env var and the default stays as today.
- **Capability-gate interaction.** If every fallback candidate fails
  the E03 gate, the user sees chain-exhausted with no clear signpost.
  *Mitigation:* the exhaustion message names the gate as the reason for
  any capability-skip entry and points at `--doctor`.
- **Streaming half-state.** Mid-stream retries are user-visible
  garbage; refusing to retry mid-stream is a partial response.
  *Mitigation:* the streaming invariant is closed: fallback only
  before the first token. After the first token, the error
  propagates with the primary error class preserved. Frank's call,
  documented in ADR-015.
- **Telemetry vs `--raw`.** Headless callers depend on `--raw` being
  output-only. *Mitigation:* the multi-hop WARN is suppressed when
  `--raw` AND `--json` are both set; structured callers (JSON only)
  still get stderr diagnostics. Mickey reviews the wording.
- **Hot-reload drift.** A candidate that passed the gate at resolver
  time may not pass at fallback time if the registry is re-read.
  *Mitigation:* gate result captured once at chain start, reused
  for the duration. Documented in ADR-015.

---

## AOT delta budget

**Target: <= 35 KB.** The chain adds one new class
(`FallbackChain`), the chaos mock lives in test-only assemblies, and
the `_fallback_chain` JSON shape adds a small `AppJsonContext`
registration. No new embedded resources, no reflection paths. If the
binary grows past 35 KB, check for an accidental
`System.Linq.Expressions` pull-in via a fluent retry-policy DSL --
hand-roll the backoff loop instead. Bania baselines pre- and post-merge.

---

## Transient-error set (canonical for E07)

| Trigger | Class | Retry on same model? | Fallback to next? |
|---------|-------|----------------------|-------------------|
| HTTP 429 | transient | yes | yes after retries |
| HTTP 500 | transient | yes | yes after retries |
| HTTP 502 | transient | yes | yes after retries |
| HTTP 503 | transient | yes | yes after retries |
| HTTP 504 | transient | yes | yes after retries |
| `TaskCanceledException` (timeout) | transient | yes | yes after retries |
| HTTP 4xx other (400, 401, 403, 404, 422) | hard | no | no |
| Capability mismatch (pre-call) | hard | no | skip and continue |
| Mid-stream connection reset | hard | no | no (post-first-token) |
| Pre-first-token streaming failure | transient | yes | yes after retries |

Adding a row to this table after E07 ships requires an ADR-015
amendment and a new Puddy test case.

---

## WARN summary format (Frank owns the wording)

Single line, ASCII only, written to stderr only on multi-hop success,
suppressed when both `--raw` and `--json` are set:

```text
[WARN] fallback: gpt-5.4-mini (429 x2) -> gpt-4o-mini (ok) in 2143 ms
```

On chain exhaustion the message is an `[ERROR]` written by
`ErrorAndExit`:

```text
[ERROR] All configured models failed: gpt-5.4-mini (429 x2), gpt-4o-mini (503 x2), llama-local (capability-skip). See --doctor.
```

No preamble. No markdown. No `Sure, here is what happened.` The chain
reports the chain.

---

## Open questions (for Frank at greenlight)

- **Polly retry policies in code?** No. `grep Polly` across
  `azureopenai-cli/` and the `.csproj` returns zero matches. The chain
  is hand-rolled.
- **Prior fallback surface from S03E22?** Yes.
  `azureopenai-cli/Resilience/FallbackChain.cs` already exists (~558
  lines, header dated S03E22). It implements a best-effort
  retry-against-alternates policy of similar shape to this brief
  (capability gate per alternate, streaming pre-first-token invariant,
  WARN-per-hop suppressed under `--raw`). E07 must either formalise it,
  rename it under `Reliability/`, or treat E07 as the doctrine episode
  that documents what is already shipping. Frank's call. The brief
  assumes a `Reliability/` rename; if Frank disagrees the path stays
  and ADR-015 records the choice.
- **`--no-fallback` CLI flag, or env-var only?** Brief recommends
  env-var only (`AZ_AI_FALLBACK_RETRIES=0` disables same-model retry;
  `AZ_AI_FALLBACK_BUDGET_MS=0` disables fallback entirely). Matches
  E03's gate-bypass posture. Frank may overrule.
- **Streaming semantics.** Brief proposes hard cut-off at first token;
  Frank refines if telemetry says users tolerate one mid-stream re-roll.

---

## References

- ADR-012 -- model registry seam (E01)
- ADR-013 -- capability gate (E03)
- ADR-015 -- fallback policy doctrine (NEW; this episode authors it)
- `azureopenai-cli/Resilience/FallbackChain.cs` -- S03E22 prior art;
  E07 formalises or supersedes
- `docs/episode-briefs/s04e03-the-capabilities.md` -- gate contract
  the chain must respect per candidate
- `docs/episode-briefs/s04e05-the-picker.md` (assumed on `main`) --
  the resolver pick is the first model in the chain
- `.github/agents/frank.agent.md` -- SRE doctrine; Frank leads
- `.github/agents/newman.agent.md` -- security review of the
  amplification surface
- `.github/agents/puddy.agent.md` -- chaos mock authorship
- `.github/skills/episode-brief.md` -- canonical brief format
  (this file follows it)

---

## Validation

```bash
grep -nP '[\x{2018}\x{2019}\x{201C}\x{201D}\x{2013}\x{2014}]' \
  docs/episode-briefs/s04e07-the-fallback.md  # 0 matches required
NODE_OPTIONS="--max-old-space-size=4096" \
  npx markdownlint-cli2 docs/episode-briefs/s04e07-the-fallback.md
```

---

## On completion

```sql
UPDATE todos SET status = 'done' WHERE id = 's04e07-brief';
```

Return: commit SHA, line count, opening hook, and Polly status.
