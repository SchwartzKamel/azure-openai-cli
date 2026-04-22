# v2 SLO Proposal -- `az-ai-v2`

**Status:** **Proposed.** These are soft targets, not hard SLAs. Hard SLAs require sign-off from Costanza (PM).
**Owner:** Frank Costanza (SRE).
**Tuning cadence:** revisit at **day-30 post-v2.0.0** with real data, then quarterly.
**Cross-links:**
- [`docs/ops/v2-sre-runbook.md`](v2-sre-runbook.md) §2 -- already carries the detailed SLO-1..SLO-6 catalog grounded in the perf baseline. This document is the **summary + proposal tier** aimed at external readers; the runbook is the operator reference.
- [`docs/perf-baseline-v2.md`](../perf-baseline-v2.md) -- data underpinning the cold-start targets.
- [`docs/ops/telemetry-schema-v2.0.0.md`](telemetry-schema-v2.0.0.md) -- the measurement plumbing (opt-in).
- [`docs/observability.md`](../observability.md) -- the user-facing telemetry contract.

> **Targets, not promises.** v2.0.0 has no fleet data yet. Every number below is either measured in CI or provisional. v2.0.x will tune based on day-30 data. That's the deal.

---

## How to read this doc

- **Target** = the number we aim to hit in steady state.
- **Measurement** = where the SLI comes from -- CI harness, opt-in telemetry, or external signal.
- **Confidence** = `grounded` (measured on CI reference hardware with ≥ 50 runs) / `provisional` (extrapolated; will be recalibrated at day-30).
- **Enforcement** = what we actually do when we breach. Hard fail (block PR / rollback), soft fail (page, investigate, but don't rollback), or monitor (track in the release-watch issue).

There are no hard SLAs in this document. Operations on the `az-ai-v2` CLI are best-effort; the CLI is a client, not a hosted service.

---

## SLO-A -- Cold-start latency (AOT binary, local execution)

**Scope:** `--help` / `--version` / `--estimate` (no credential resolution, no network).

| Metric | Target | Confidence | Source |
|---|---|---|---|
| p50 cold-start | ≤ **20 ms** | grounded (CI reference runner) | `scripts/bench.sh --aot` *(planned -- see note)* |
| p99 cold-start | ≤ **100 ms** | provisional | p95 is already bounded at 25 ms by the runbook; p99 ceiling will be pinned after 2.0.1 CI data |
| p95 cold-start | ≤ **25 ms** (authoritative) | grounded | `docs/ops/v2-sre-runbook.md` §2 SLO-1 |

**Measurement.** CI runs `scripts/bench.sh --aot` on the reference runner; 50-run rolling window.

> ⚠️ **`scripts/bench.sh --aot` is planned, not shipped.** Today's
> `scripts/bench.sh` times `dotnet <dll>` (framework-dependent / JIT),
> not the shipping AOT binary, and has no `--aot` flag. The AOT
> baseline numbers in [`docs/perf-baseline-v2.md`](../perf-baseline-v2.md)
> were collected via a separate companion loop. `--aot` mode is
> scheduled under [`bania-v2-03`]; until then AOT cold-start is
> measured with `python3 scripts/bench.py dist/aot/<bin>`. Track:
> [`docs/audits/docs-audit-2026-04-22-bania.md`](../audits/docs-audit-2026-04-22-bania.md) C2 / H2.
**Enforcement.** Hard fail (PR block) if p95 exceeds 25 ms over 3 consecutive CI runs; see `docs/ops/v2-sre-runbook.md` SLO-1 for the error-budget mechanics.
**Open question at freeze:** p99 is provisional because we have not yet collected enough runs to separate CI-runner noise from real tail latency. Day-30 data gates the real number.

---

## SLO-B -- First-token latency (chat mode, real call)

**Scope:** `az-ai-v2 "hello"` standard chat path against the baseline Azure OpenAI deployment named in the CI secrets; measured end-user-perceived: process start → first streamed token on stdout.

| Metric | Target | Confidence | Source |
|---|---|---|---|
| p50 first-token | ≤ **2 000 ms** | provisional | opt-in telemetry `azai.chat.duration` histogram, filtered to first-token via client-side timer |
| p95 first-token | ≤ **5 000 ms** | provisional | same |

**Measurement.** Opt-in only. SLI is derived from fleet data contributed by users who enable `--telemetry` or `AZ_TELEMETRY=1`. CI runs a single smoke call per release which contributes one data point -- not enough for an SLO, but enough for regression sniffing.

**Hard carve-outs (excluded from the SLI numerator/denominator):**
- User-auth errors (exit 3, HTTP 401/403).
- Config errors (exit 2).
- SIGINT (exit 130).
- Azure OpenAI regional incidents -- SLO does not cover Microsoft's availability, only the client overhead.

**Enforcement.** Monitor only. A p95 breach opens an issue for Kenny Bania to evaluate. We do not block releases on first-token latency because the latency is dominated by Azure-side behavior we do not own.
**Revisit at day-30** with a min-50-sample baseline before promoting these to grounded.

---

## SLO-C -- Error rate (non-user-error invocations)

**Scope:** telemetry-reported invocations, **excluding** `--help`, `--version`, `--estimate`, and invocations that exit with a user-classified code (2 config, 3 auth, 130 SIGINT).

| Metric | Target | Confidence |
|---|---|---|
| Error rate (exit ∉ {0, 2, 3, 130}) | < **0.5%** | provisional |

**Mapping to the runbook.** This is the public-facing summary of `v2-sre-runbook.md` §2 SLO-4 (real-call success ≥ 99.5%) plus SLO-6 (crash rate ≤ 0.1%). The runbook stays authoritative on window, burn rate, and page thresholds.
**Measurement.** Opt-in telemetry only. We have no view on users who do not opt in, and we do not want one.
**Enforcement.** Soft fail -- page on-call; consult the runbook's SLO-4 / SLO-6 mechanics for rollback triggers.

---

## SLO-D -- Release pipeline: tag → GHCR live

**Scope:** time from `git push origin vX.Y.Z` (tag signed per `release-v2-playbook.md`) to `ghcr.io/schwartzkamel/azure-openai-cli:vX.Y.Z` returning a 200 manifest for anonymous pull.

| Metric | Target | Confidence |
|---|---|---|
| p50 tag-to-live | ≤ **20 min** | grounded (rehearsed in `v2-tag-rehearsal-report.md`) |
| p95 tag-to-live | ≤ **30 min** | provisional |

**Measurement.** Manually timestamped in `docs/ops/v2.0.0-day-one-baseline.md` §6 at each release. Automate after v2.0.x if we ship > 3 releases.
**Enforcement.** Monitor. A breach probably means CI runner pressure or a matrix-job timeout; investigate but do not block.

---

## SLO-E -- Cost-schema emission completeness (opt-in)

**Scope:** among users who opted into `--metrics` / `--telemetry`, the fraction of LLM-calling invocations that produce exactly one valid cost-event line on stderr.

| Metric | Target | Confidence |
|---|---|---|
| Cost-event emission rate | ≥ **99.9%** | provisional |
| Cost-event schema validity (§4 of `telemetry-schema-v2.0.0.md`) | 100% | grounded (source-gen serialization, no reflection) |

**Measurement.** Opt-in. Derived by comparing `azai.chat.duration` counts (OTel meter) with cost-event line counts (stderr log shipper) for the same invocation.
**Enforcement.** Soft fail. Morty's spreadsheet depends on this; an emission-rate breach is a finance observability issue, not a runtime availability issue.

---

## Non-goals / explicitly out of scope

- **Azure OpenAI availability.** Microsoft's SLA. We don't own it; we don't SLA it.
- **User network latency.** Not measured, not targeted.
- **Third-party package managers' propagation time.** Homebrew tap, Scoop bucket, Nix flake -- Bob Sacamano's ecosystem latency, not ours.
- **Documentation site uptime.** This repo is the docs site. GitHub's availability is our availability.
- **Per-feature latency SLOs.** Not yet. v2.0.x focuses on the CLI-wide envelope; feature-grained SLOs come with feature-grained telemetry proposals (and those require schema bumps per `telemetry-schema-v2.0.0.md` §7).

---

## Error budget hygiene

Every grounded SLO has an error budget defined in `v2-sre-runbook.md` §2. Rules:

- **Error budgets are real.** When a budget is blown on a grounded SLO, reliability work pauses feature merges touching that surface until the budget recovers.
- **Provisional SLOs do not gate feature work.** They collect data. Breaches go into the release-watch issue and the day-30 review.
- **At day-30 review,** every provisional SLO either promotes to grounded (with a real error budget) or is demoted to "monitor only" if the signal is too noisy for a useful SLO.

---

## Day-30 review checklist (v2.0.0 + 30d)

- [ ] Collect 30 days of CI benchmark data; re-baseline cold-start p50/p95/p99.
- [ ] Collect opt-in telemetry summary (aggregated, no raw). Estimate opt-in rate. If < 1% opted in, note that fleet-wide SLO claims are not statistically sound.
- [ ] Promote or demote each provisional target.
- [ ] File a PR adding real numbers with a `slos-v2` CHANGELOG entry.
- [ ] Schedule the next review 90 days out. Costanza (PM) signs off if any target is retitled from "proposed" to "agreed".
- [ ] Add an entry to the Festivus post-mortem if any target was a problem that quarter. Airing of Grievances -- I got a lotta problems with my own SLO targets, and now you're gonna hear about 'em.

---

**SERENITY NOW. Propose the target. Ship the measurement. Tune on real data. Don't promise what you can't measure.**
