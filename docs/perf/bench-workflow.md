# Bench Workflow -- when to run which target

Three `make bench*` targets, three different jobs. Pick the right one for
the moment; don't burn 5 minutes on a sweep when 5 seconds answers your
question, and don't trust 5 seconds when the answer matters.

## Quick reference

| Target              | N   | Warm-up | Flag matrix | Output                                | Wall-clock (ref rig) | Bench-grade?              |
|---------------------|-----|---------|-------------|---------------------------------------|----------------------|---------------------------|
| `make bench-quick`  | 50  | 0       | no          | stdout text only                      | ~5–10 s              | No — directional smoke    |
| `make bench`        | 100 | 5       | no          | stdout text only                      | ~30 s                | Indicative, not gating    |
| `make bench-full`   | 500 | 5       | yes         | text + JSON to `docs/perf/runs/<date>` | ~5–10 min            | Yes — canonical, pinned   |

"Reference rig" wall-clocks are from `malachor` (i7-10710U, linux-x64) per
[`docs/perf/reference-hardware.md`](reference-hardware.md). Your laptop will
differ; what matters is the *order of magnitude* gap between the targets.

## When to run which

### `make bench-quick` -- pre-commit / dev loop

Use it as a sanity check before you commit a change you suspect could
move cold-start. It runs 50 iterations of `--help`, no warm-up, no flag
matrix, and prints one line of percentiles to stdout. Nothing is written
to disk.

It will **not** catch a 5 % regression. It *will* catch the kind of
oh-no-I-shipped-a-300-ms-static-ctor regression that any human would
notice with the naked eye. That's its job.

### `make bench` -- mid-PR confirmation

Once you've narrowed in on a suspect change and want a slightly less
noisy read, `make bench` does N=100 with 5 warm-up iterations. Still
single-scenario, still stdout-only, still not authoritative -- but the
warm-up + larger N tightens p95 / p99 enough to argue about a 10–20 %
change. Don't file a regression on this alone; promote to `bench-full`
on the pinned rig before claiming numbers in a PR description.

### `make bench-full` -- pre-merge / release

The canonical sweep. N=500, 5 warm-ups, the full flag matrix
(`--help`, `--help --otel`, `--help --metrics`, `--help --otel --metrics`),
text + JSON written to `docs/perf/runs/<date>-<host>-flagmatrix.{txt,json}`.

Run this **on the pinned reference rig** under the protocol in
[`docs/perf/reference-hardware.md`](reference-hardware.md):
governor=`performance`, AC power, no other workloads. These are the
only numbers that belong in a release note, in
[`docs/perf/v2.0.5-baseline.md`](v2.0.5-baseline.md), or in a
regression argument.

## CI's `bench-canary` job is **not** authoritative

Every push / PR fires the `bench-canary` job in `.github/workflows/ci.yml`,
which runs `make bench-quick` on a shared `ubuntu-latest` runner and posts
the table to the GitHub Actions step summary.

Treat that summary as a **directional smoke test only**. Shared-VM runners
exhibit ±30 % wall-clock jitter from neighbour-tenant noise alone. The job
intentionally:

- runs with `continue-on-error: true` on the bench step (a slow run cannot
  redden CI),
- depends on `build-and-test` (no point benching code that doesn't compile),
- never writes to `docs/perf/runs/` (the pinned-rig directory stays clean),
- labels its summary section `## bench-canary (directional only)` so nobody
  cites it as a baseline.

If `bench-canary` ever shows a number that scares you, don't argue with it
-- reproduce it with `make bench-full` on the pinned rig and *then* argue.

## Adding a new bench scenario

`scripts/bench.py` owns the harness. New scenarios go in the `FLAG_MATRIX`
list at the top of that file and become part of `make bench-full`. The
quick / mid targets stay single-scenario by design -- their job is speed,
not coverage.
