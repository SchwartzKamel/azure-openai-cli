# ADR-008 — Benchmarking Policy for GPU / Non-CI-Gated Providers

- **Status**: Proposed — 2026-04-23
- **Deciders**: Bania (perf), Jerry (DevOps), Kramer (eng)
- **Related**:
  - [ADR-006 — NVIDIA NIM / NVFP4 Provider Integration](./ADR-006-nvfp4-nim-integration.md) — originating decision
  - [ADR-003 — Behavior-Driven Development in xUnit](./ADR-003-behavior-driven-development.md) — test-gate precedent
  - [ADR-007 — Security guardrails for third-party HTTP providers](./ADR-007-third-party-http-provider-security.md)

## Context

az-ai's existing perf gates (cold-start TTFT, binary size, AOT footprint) run on GitHub Actions and block merge at the project-standard 5% warn / 10% fail thresholds. That model works because every gated signal can be reproduced on a commodity GHA runner.

**It does not work for GPU-backed providers.**

GitHub Actions runners have no Blackwell silicon, no NVFP4 tensor cores, no CUDA, no NIM container runtime. A "perf gate" on ADR-006's NIM provider run in GHA would be measuring nothing relevant — at best mock latency, at worst theatre. The same applies to any future provider that depends on user-side hardware we can't reproduce in CI (Apple Metal, AMD ROCm, Intel Arc, custom Triton deployments).

At the same time, we still have to catch regressions in the code we *do* own: HTTP request construction, bearer-auth injection, SSE stream parsing, callback dispatch, retry logic. Those are az-ai's responsibility regardless of what's on the other end of the socket.

ADR-006 sketched a two-tier answer inline. This ADR promotes it to project-wide policy so every future GPU-dependent provider inherits the same rules.

## Decision

**Every provider that depends on hardware az-ai's CI cannot provision is benchmarked in two tiers: a mock-backend tier that is CI-gated, and a real-hardware tier that is manual and explicitly not a merge gate.**

### Tier 1 — Mock-backend bench, CI-gated

Each GPU-dependent provider ships with a mock server that mimics the provider's wire protocol at fixed, deterministic latency. For FR-020 (NIM) this is `scripts/bench_mock_nim.py` — a local HTTP server emitting OpenAI-compatible chat-completions with a scripted SSE stream and a fixed per-token delay.

The bench measures **az-ai's contribution to the end-to-end call**: request build, auth header injection, stream parse, callback dispatch, tool-call parsing. Not model inference — that is held constant by the mock.

Tier 1 runs on every PR. Thresholds match the existing project standard:

- **≥ 5% regression vs baseline** — warn (annotates the PR).
- **≥ 10% regression vs baseline** — block (fails the merge gate).

Mock-bench baselines live at `benchmarks/mock/<provider>.baseline.json` and are committed. Rebaseline PRs require a brief justification in the commit message.

### Tier 2 — Real-hardware bench, manual / nightly self-hosted

Real-GPU numbers are produced by one of:

- A **nightly self-hosted runner** labelled e.g. `gpu-nvfp4`, opt-in per provider, if and when org hardware exists.
- **Manual runs** on contributor or maintainer hardware, checked in under `benchmarks/manual/<date>-<host>.json`.

Tier 2 is **never** a merge gate. A Tier 2 regression opens an issue; it does not block a PR.

### Mandatory fields for every Tier 2 measurement

Every manual or nightly measurement must record:

| Field | Example |
|---|---|
| `date` | `2026-05-01T14:22:10Z` |
| `host` | `dev-rig-wsl2` |
| `gpu` | `RTX PRO 3000 Blackwell (mobile)` |
| `vram_gb` | `12` |
| `driver` | `CUDA 12.8` |
| `provider_version` | `nvcr.io/nim/nvidia/gemma-4-9b-it-nvfp4@sha256:…` |
| `sample_size` | `100` |
| `warmup_runs` | `10` |
| `metric` | `ttft_p50_ms` |
| `value` | `94` |
| `variance` | `p95=138, p99=181, stdev=24` |

Bare means are rejected — "the p50 was 94 ms" without sample size, variance, and hardware is not a measurement, it is a rumour. The schema is enforced by a JSON-schema lint on `benchmarks/manual/*.json`.

### Published-claim policy

Any latency or throughput number that leaves `benchmarks/` — README tables, blog posts, conference talks, docs pages — **must cite the source measurement row**: hardware, sample size, variance, provider version. No exceptions.

Every `docs/providers/<provider>.md` for a GPU-dependent provider carries the banner:

> **Performance characterization**
> Numbers in this page are manual / community-reported. They are not CI-gated. Hardware, sample size, and variance are listed alongside every value. If a number lacks those fields, it is not a measurement — treat it as a rumour.

### Metrics catalogue

The following metrics are the recommended minimum for a GPU-dependent provider's Tier 2 sheet:

- **TTFT** — cold (container cold), warm (container hot, model loaded).
- **Throughput** — steady-state tokens per second, per concurrency level `{1, 4, 16, 64}`.
- **TTFT under load** — p50/p95/p99 at each concurrency level.
- **Container cold start** — wall-clock from `docker run` to first-token readiness.
- **Model load time** — weights → VRAM residency, reported separately.
- **VRAM residency** — idle and peak, in GB.
- **az-ai overhead** — wall-clock delta between az-ai invocation and the first byte received from the provider (Tier 1 measures this; Tier 2 cross-checks).

Not every provider will populate every field on day one. The schema treats missing fields as `null`, not absent — so omissions are visible in rollups rather than silently assumed zero.

## Consequences

### Positive

- CI signal stays honest. We only gate on numbers we can reproduce.
- Real-hardware claims come with provenance attached; a user reading the docs knows exactly what rig produced the number.
- Future GPU providers (Metal, ROCm, Arc, Triton deployments) inherit the structure without re-negotiation per FR.

### Negative

- Tier 2 regressions can slip for days if nobody runs the manual bench. Mitigation: a monthly checklist item in the release-readiness doc forces a manual bench on the reference rig.
- Maintaining a mock server per provider is ongoing work. Mitigation: mocks live next to the provider adapter in the tree; updating one updates the other in the same PR.
- Self-hosted GPU runners, if/when we add them, introduce their own ops burden (cost, on-call, key rotation). Deferred until a provider genuinely needs nightly signal.

## Alternatives Considered

- **Gate real-GPU perf in CI via paid cloud runners (e.g. Lambda, Modal).** Rejected: cost, queue times, and "which Blackwell SKU is the baseline?" churn. A provider's CI bill exceeding its user base is a smell.
- **Don't bench GPU providers at all — let users do it.** Rejected: plumbing regressions (stream parsing, auth) are az-ai's problem and would go undetected. Tier 1 catches those cheaply.
- **Publish bare means without variance.** Rejected: every "gold" claim without a reproducer becomes a support ticket six months later. Variance + sample size are required for a reason.

## References

- [ADR-003 — Behavior-Driven Development in xUnit](./ADR-003-behavior-driven-development.md) — test-gate thresholds precedent
- [ADR-006 appendix §A.7 — Bania's perf memo (verbatim)](./ADR-006-appendix-roundtable.md#a7--kenny-bania-perf)
- Existing `docs/benchmarks.md` and `make bench` targets
