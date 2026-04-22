# Performance Baselines — Index

Canonical directory for tagged-release perf snapshots. The **current** baseline is the latest row below; everything else is historical reference.

## Status

| Release | Baseline doc | Host rig | Status |
|---|---|---|:---:|
| **v2.0.5** | [`v2.0.5-baseline.md`](./v2.0.5-baseline.md) | `malachor` (Ubuntu 24.04, i7-10710U, .NET SDK 10.0.201) | ✅ **CURRENT** |
| v2.0.2 (dogfood, not a tagged baseline) | [`../benchmarks/bania-v2-dogfood-2026-04-22.md`](../benchmarks/bania-v2-dogfood-2026-04-22.md) | `malachor` (same) | 📦 archived (superseded) |
| v2.0.0 (cutover gate) | [`../perf-baseline-v2.md`](../perf-baseline-v2.md) | `AB-DM4MFJ4` (WSL2, Intel Ultra 7 265H, .NET 10.0.106) | 📦 archived (superseded) |

## Archive policy

- Tagged-release baselines (every `vX.Y.Z` that ships to users) live under `docs/perf/` as `vX.Y.Z-baseline.md`.
- When a newer tagged baseline is published, the prior file is **not renamed or moved** — it stays at its original path so the ~25 cross-references from runbooks, ADRs, launch notes, and CHANGELOG entries keep resolving. The `Status` column here is the source of truth for which doc is current.
- Unscheduled / dogfood / spike benches live under [`docs/benchmarks/`](../benchmarks/) and are never promoted to `docs/perf/`.
- Future snapshots taken by the CI perf job (see `bania-v2-03`) will be dropped into [`archive/`](./archive/) as dated CSV/MD pairs — see that directory's README.

## How to read a baseline doc

Every file under this directory must contain, at minimum:

1. **Environment pinning table** — kernel, CPU, OS, .NET SDK, RID, AOT flags, thermal/governor state.
2. **Methodology** — harness, warmup count, measured-run count, timing precision, what was discarded.
3. **Raw results** — per scenario: N, min, p50, mean, p90, p95, p99, max, σ. No reporting a single number without the distribution it came from.
4. **Comparison section** — prior tagged baseline, honest delta commentary, confounders called out explicitly.
5. **Reproducibility block** — the exact shell commands to rebuild and re-measure.
6. **Limitations** — what was not measured and why.

If a doc lands without all six, it doesn't get merged. That's the standard.

## Pre-merge perf gates (current, per v2.0.0 cutover, restated in v2.0.5 §4)

| Gate | Threshold | Source |
|---|---|---|
| Binary size (AOT single-file, linux-x64) | ≤ 1.50× v1 (9,294,968 B) | [`../perf-baseline-v2.md`](../perf-baseline-v2.md) §4 |
| Cold-start p95 (`--help` / `--version --short`, AOT) | ≤ 1.25× v1 **or** absolute ≤ 25 ms on reference rig | same |
| RSS | ≤ 1.50× v1 | same |
| PR regression flag | ≥ 5% on any tracked metric | agent brief (Bania) |
| PR regression block | ≥ 10% on any tracked metric | agent brief (Bania) |

## Open follow-ups

- **`bania-v2-03`** — fold AOT mode into [`scripts/bench.sh`](../../scripts/bench.sh) so one harness measures both the JIT DLL and the shipping AOT binary.
- **CI perf job** — wire the harness into a pinned linux-x64 runner, post a PR-diff comment with the regression table vs `main`.
- **Cross-RID** — win-x64 and osx-arm64 baselines to complement linux-x64.
- **Statistical significance** — add Mann-Whitney U (or equivalent) on the v_prev vs v_current sample distributions before flagging a <5% drift as "regression".

---

*Means are for amateurs. The tail is where users actually live.* — K. Bania
