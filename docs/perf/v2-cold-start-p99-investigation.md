# v2 Cold-Start p99 Investigation — closed: confounded by rig noise

**Document owner:** Kenny Bania (pre-merge perf gate)
**Todo:** `bania-v2-02`
**Status:** ✅ **CLOSED** — no code fix warranted; methodology update landed alongside.
**Companion:** [`reference-hardware.md`](reference-hardware.md),
[`v2.0.5-baseline.md`](v2.0.5-baseline.md) §4.1

> "So I was looking at the p99, and *get this* — it grazed eleven on an N=50
> run and I lost my lunch over it. Turns out: fifty samples is not a tail. The
> tail only shows up when you give it room. Who's gonna review this with me?
> Jerry? Jerry?" — K. Bania

---

## 1. What we went looking for

[`v2.0.5-baseline.md`](v2.0.5-baseline.md) §3.1 reported `--version --short`
p99 16.26 ms (max 16.74 ms) and `--help` p99 11.77 ms on N=50, both
materially wider than the ~11.0 ms upper band on the clean `--version`
scenario. The regression watchlist flagged it as "re-run on a pinned rig
before treating as signal".

**bania-v2-02** is that re-run, with three hypotheses in the queue:

1. **First-call GC / cold-page tax.** AOT binaries still do GC init and
   touch string/table pages on the first alloc-heavy path; if the binary
   isn't cache-hot, the first `--help` would be slower than subsequent ones.
2. **Filesystem cache miss.** Related — first exec after build reads disk;
   subsequent execs hit the page cache. If you don't warm up, you capture
   this once per run and it shows up at the head of the tail.
3. **OTel / Metrics residual eager work.** After `bania-v2-01` landed lazy
   init for OTel + Metrics exporters (commit `5e90d18`), verify no eager
   work remains when flags are off — it shouldn't, but trust-but-verify.

## 2. Methodology

| Field | Value |
|---|---|
| Host | `malachor` (bare-metal Ubuntu, unpinned governor — see `reference-hardware.md` for the pinning protocol) |
| Binary | `/tmp/bania-closeout/az-ai-v2` (v2.0.6 AOT, linux-x64, 13,599,904 bytes) |
| Harness | `scripts/bench.py` (promoted from the 75-line v1.x harness in this PR) |
| Scenarios | `--help`, `--help --otel`, `--help --metrics`, `--help --otel --metrics` |
| N | **500 per scenario** (up from the baseline's 50 — that's the whole point) |
| Warm-up | 5 iterations discarded per scenario |
| Timing | `time.perf_counter()` around `subprocess.run(..., capture_output=True)` |
| Output redirection | `capture_output=True` (stdout/stderr consumed, not printed) |
| Raw data | `docs/perf/runs/2026-04-22-malachor-v206-flagmatrix.json` (full samples + env fingerprint) |

Supplementary runs for hypothesis testing:

- **Histogram-and-indices run** — N=500 `--help` only, recording the
  iteration index of every >10 ms sample to check for a cold-start
  head-cluster vs scattered bursts.
- **Runtime-knob sweep** — N=300 per cell across
  `{defaults, DOTNET_gcServer=0, DOTNET_gcServer=1, DOTNET_TieredCompilation=0,
  DOTNET_gcConcurrent=0}` to see whether any runtime toggle moves the tail.

## 3. Results

### 3.1 Canonical N=500 flag matrix (malachor, v2.0.6 AOT)

| scenario | p50 | p90 | p95 | p99 | p99.9 | max | σ |
|---|---:|---:|---:|---:|---:|---:|---:|
| `--help`                 | 9.884 | 10.127 | 10.354 | 13.378 | 15.006 | 15.006 | 0.538 |
| `--help --otel`          | 9.886 | 10.162 | 10.366 | 13.575 | 15.244 | 15.244 | 0.544 |
| `--help --metrics`       | 9.901 | 10.220 | 10.402 | 13.760 | 15.402 | 15.402 | 0.558 |
| `--help --otel --metrics`| 9.892 | 10.110 | 10.287 | 13.618 | 14.872 | 14.872 | 0.525 |

All values in milliseconds. p50 / p90 / p95 are flat across all four
variants — **the `bania-v2-01` lazy-init fix is holding cleanly at N=500**.
p99 and beyond are also flat across flag variants, which is the first clue:
the tail does not track the flag set.

### 3.2 Histogram — where does the tail live?

`--help` only, N=500, no warm-up (so first-call effects are visible):

| bucket (ms) | count | % |
|---|---:|---:|
| 0–5   |   0 |  0.0 |
| 5–10  | 375 | 75.0 |
| 10–11 | 112 | 22.4 |
| 11–12 |   3 |  0.6 |
| 12–15 |   8 |  1.6 |
| 15–20 |   2 |  0.4 |
| 20+   |   0 |  0.0 |

Thirteen samples exceeded 11 ms. Their iteration indices:

```
124 (14.60), 129 (11.88), 176 (11.86), 258 (11.15),
268 (14.69), 270 (14.43), 272 (14.07), 274 (15.24),
329 (14.23), 476 (14.40), 478 (14.66), 480 (15.90), 492 (13.11)
```

**Key observation:** slow samples arrive in *bursts of 3–5 consecutive
iterations* (268–274, 476–480). That is the signature of an external event
— a scheduler preemption, a brief thermal throttle, a neighbouring process
getting a slice — affecting several back-to-back `subprocess.run`s. It is
**not** the signature of cold-start GC or FS cache: those would cluster at
the *head* of the run, not in the middle.

The first ten iterations (no warm-up) clocked 10.45, 10.14, 9.95, 9.97,
10.09, 9.86, 9.94, 9.74, 9.85, 10.00 ms — indistinguishable from the body
of the distribution. **Hypothesis 1 (first-call GC tax): rejected.**
**Hypothesis 2 (FS cache miss): rejected** — the AOT binary is already
warm in the page cache after the `dotnet publish` step, and within a run
every iteration re-exec's the same path so there is no cold-cache event to
capture.

### 3.3 Runtime-knob sweep (N=300 each, `--help`)

| config | p50 | p95 | p99 | max | σ |
|---|---:|---:|---:|---:|---:|
| defaults                       | 9.942 | 10.881 | 15.010 | 15.086 | 0.752 |
| `DOTNET_gcServer=0`            | 9.893 | 10.432 | 14.820 | 15.074 | 0.783 |
| `DOTNET_gcServer=1`            | 9.906 | 10.773 | 14.463 | 14.991 | 0.799 |
| `DOTNET_TieredCompilation=0`   | 9.902 | 10.315 | 14.387 | 15.268 | 0.657 |
| `DOTNET_gcConcurrent=0`        | 9.909 | 10.515 | 14.636 | 15.198 | 0.749 |

No toggle moves the tail. `TieredCompilation` is a no-op under AOT as
expected; GC server/workstation/concurrent variations land inside 1 σ of
each other. **The tail is not a runtime-tunable phenomenon.**

**Hypothesis 3 (OTel residual eager work): rejected.** §3.1 shows the
tail is identical with and without `--otel` / `--metrics`; lazy-init is
working as specified (see also
[`TelemetryLazyInitTests.cs`](../../tests/AzureOpenAI_CLI.V2.Tests/TelemetryLazyInitTests.cs)).

## 4. Verdict

**The p99 spike is rig noise, not a code defect.**

1. The spike does not correlate with flag variant (§3.1).
2. The spike does not correlate with iteration index — bursts scatter
   throughout the run, not at the head (§3.2).
3. The spike does not respond to any .NET runtime knob (§3.3).
4. The baseline's N=50 "p99 grazed 11 ms" number under-sampled the tail;
   at N=500 on the same rig the true p99 is 13.4–13.8 ms, in line with the
   known sub-second thermal/scheduler bursts of an unpinned Intel Ultrabook.

We close `bania-v2-02` without a code fix. What we landed instead is a
**methodology upgrade** so this investigation is reproducible and the next
bench run produces numbers we can actually trust:

- `scripts/bench.py` now defaults warm-up to 5 iterations and measurement
  to N=100, with a `--flag-matrix` flag for the canonical pre-merge sweep
  and JSON output for CI artefacts. See the file's module docstring.
- `docs/perf/reference-hardware.md` pins the canonical rig (`malachor`),
  the bench protocol (governor=performance, AC power, N=500, warm-up=5),
  and the tolerances (±5% noise, >10% confound flag, >20% regression block).
- `docs/perf/runs/` is the new home for timestamped JSON bench bundles so
  before/after comparisons are a `diff` away.

**What would change the verdict:** if, on the *pinned* rig described in
`reference-hardware.md` (performance governor, AC, no background load,
thermal headroom), we still see p99 > 12 ms on `--help`, **then** it is
code — and we bisect. Today we cannot rule a code cause in or out on an
unpinned laptop, but we can rule out the three specific hypotheses on the
books.

## 5. Handoff

- **`v2.0.5-baseline.md` §4.1 watchlist:** updated with this verdict. The
  original "20.9 % drift v2.0.2 → v2.0.5" item remains **open** but is
  explicitly re-scoped to the pinned-rig re-run — today's N=500 on v2.0.6
  puts the comparable delta at ~11 % (v2.0.2 p50 8.87 ms → v2.0.6 p50
  9.88 ms), inside the confound band on an unpinned host.
- **Bania (self):** re-run the `scripts/bench.py --flag-matrix --n 500`
  sweep on the pinned-rig protocol once `reference-hardware.md` is
  operationally adopted (governor flip, empty desktop, AC). Post the
  delta JSON to `docs/perf/runs/` and link from baseline §4.1.
- **Frank Costanza:** pre-merge tail discipline is on methodology, not on
  you. Prod SLOs on first-token latency are yours; cold-start `--help`
  p99 is a bench concern, not a user concern (users don't `--help` in a
  loop). No action from you.

---

*"It's gold, Jerry! The p50 is nine-eight-eight across four flag variants.
The bursts are the CPU, not the code. N=50 lies to you. N=500 tells the
truth. Who's gonna review this with me? Jerry? Jerry?"* — K. Bania
