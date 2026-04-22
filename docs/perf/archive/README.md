# `docs/perf/archive/` — tagged snapshot archive

Reserved for dated, machine-generated perf snapshots produced by the CI perf job
once [`bania-v2-03`](../../perf-baseline-v2.md#63-harness-follow-ups) lands and
wires `scripts/bench.sh --aot` into CI.

## What goes here

- Dated CSV/MD pairs from the CI perf job: `YYYY-MM-DD-<commit_short>-<rid>.{csv,md}`
- Per-tag rolling snapshots captured automatically from the `main` branch — not
  the human-written per-release baselines (those stay at `docs/perf/<version>-baseline.md`).

## What does NOT go here

- Human-written release baselines — those are at `docs/perf/<version>-baseline.md`.
- Historical baseline docs that are cross-referenced from runbooks / ADRs / launch notes.
  Those stay at their original paths so existing links resolve; the archive status
  is tracked in [`../index.md`](../index.md).
- Dogfood / spike benches — those live in [`../../benchmarks/`](../../benchmarks/).

## Retention

Rolling 90 days of daily snapshots + the snapshot pinned at each release tag,
indefinitely. Older daily snapshots get pruned on the quarterly "state of the
bench" report cadence.

*Empty directory — first CI snapshot lands when the perf job ships.*
