# Reference Hardware — `malachor` (canonical pre-merge bench rig)

**Document owner:** Kenny Bania (pre-merge perf gate)
**Todo:** `bania-v2-03`
**Status:** ✅ **CURRENT** — canonical rig for all numbers cited in
[`v2.0.5-baseline.md`](v2.0.5-baseline.md),
[`v2-cold-start-p99-investigation.md`](v2-cold-start-p99-investigation.md),
and any `docs/perf/runs/*.json` bundle produced by
[`scripts/bench.py`](../../scripts/bench.py).

> "Noise is the enemy of signal. Warm up, repeat, discard outliers, publish
> the methodology. And for the love of Newman, set the governor." — K. Bania

---

## 1. Why this doc exists

Until `bania-v2-03`, v2 perf numbers were collected on an i7-10710U laptop
under inconsistent load: browser open, IDE open, battery vs AC drifting,
thermal governor unpinned. Intra-host run-to-run noise was high enough
(> 1.5 ms on p99) that small regressions hid inside the confound band.
[`v2-cold-start-p99-investigation.md`](v2-cold-start-p99-investigation.md)
is the direct evidence that the tail on this rig is *OS noise*, not code.

This document is the protocol that makes the same rig produce
*apples-to-apples* numbers across PRs. If the protocol is not followed,
the numbers are noise — do not file regressions off unprotocolled runs.

## 2. Primary reference rig — `malachor`

| Field | Pinned value |
|---|---|
| Hostname | `malachor` |
| Form factor | Ultrabook (dev laptop pressed into bench duty; see §5 for the "why not CI runner" note) |
| CPU | Intel® Core™ i7-10710U (Comet Lake, 6 cores / 12 threads, 1.10 GHz base / 4.70 GHz turbo) |
| CPU online logical | 8 (kernel reports; BIOS / firmware-side heterogeneous scheduling) |
| Arch | `x86_64` (45-bit physical / 48-bit virtual) |
| RAM | 16 GB DDR4 |
| Storage | NVMe SSD (Samsung PM981a class) |
| OS | Ubuntu 24.04.4 LTS (Noble) |
| Kernel | `6.8.0-106-generic` or later 6.8.x |
| .NET SDK | `10.0.201` (commit `4d3023de60`) |
| .NET Host | `10.0.5` (commit `a612c2a105`) |
| RID | `linux-x64` |
| Shell | bash 5.x |
| Python | 3.11+ (for `scripts/bench.py`) |

## 3. Bench-mode preparation (mandatory)

Run before a canonical measurement set. Any deviation gets documented in
the run JSON's `notes` field or the numbers do not go in a baseline doc.

1. **AC power** — not battery. Unplugged laptops throttle aggressively.
2. **CPU scaling governor = `performance`**:

    ```bash
    sudo cpupower frequency-set -g performance
    # verify:
    cpupower frequency-info | grep 'current policy'
    ```

3. **No other CPU-heavy processes** — quit browsers, IDEs, Docker
   Desktop, Slack, Zoom. `top` should show the bench as the only
   non-kernel consumer > 1 % CPU.
4. **Swap is either disabled or ≥ 2× measurement set size** (a 500-sample
   run is tiny — ~10 MiB — so default swap is fine; flagged here for
   larger memory-trend runs).
5. **Thermal headroom** — laptop lid open, fans unobstructed. If the
   chassis is warm from a prior build, wait 60 s before the measurement
   starts. Record `sensors | head -20` output into the run notes if any
   core is already above 70 °C at idle.
6. **ASLR** — leave at kernel default (`2`). We are measuring the
   production shape, not a reverse-engineering shape. Flagging ASLR off
   only makes sense when profiling layout-sensitive micro-ops, and those
   are not what `scripts/bench.py` measures.
7. **Desktop compositor idle** — no video playing, no animations, no
   screensaver about to kick in. Set a 60-minute idle-screen timeout for
   the duration of the run.
8. **Record `cpufreq-info | head -30` output** alongside the JSON bundle
   if any core is throttled or the governor differs from `performance`.

## 4. Bench protocol (canonical)

The canonical pre-merge sweep, to be run before and after any change
suspected of moving cold-start:

```bash
# 1. Build fresh AOT binary on the same machine (never bench a cross-rig binary):
dotnet publish azureopenai-cli-v2/AzureOpenAI_CLI_V2.csproj \
    -c Release -r linux-x64 --self-contained /p:PublishAot=true \
    -o /tmp/bench

# 2. Let the box settle (FS cache warm, thermal idle):
sleep 10

# 3. Canonical sweep — N=500 measured, 5 warm-up, flag matrix, JSON out:
python3 scripts/bench.py /tmp/bench/az-ai-v2 \
    --n 500 --warmup 5 --flag-matrix --json \
    > docs/perf/runs/$(date +%Y-%m-%d-%H%M)-malachor-flagmatrix.json

# 4. Human-readable snapshot alongside (same knobs):
python3 scripts/bench.py /tmp/bench/az-ai-v2 \
    --n 500 --warmup 5 --flag-matrix \
    | tee docs/perf/runs/$(date +%Y-%m-%d-%H%M)-malachor-flagmatrix.txt
```

Or equivalently: `make bench` (see [`Makefile`](../../Makefile), target
`bench`, which wires the same invocation with a dated log destination).

### Protocol parameters

| Parameter | Value | Rationale |
|---|---|---|
| N (measured) | 500 | Required to resolve p99 and p99.9 reliably; N=50 under-samples the tail (proven in the p99 investigation). |
| Warm-up | 5 | Discard FS cache + kernel exec-path warm-up iterations. First-call cold effects are not part of steady-state measurement. |
| Scenarios | `--help`, `--help --otel`, `--help --metrics`, `--help --otel --metrics` | Covers the "do we regress the unflagged hot path?" + "does OTel/Metrics reintroduce eager work?" questions in one sweep. |
| Cool-down between scenarios | implicit (subprocess spawn overhead ≈ 10 ms; no action needed) | The next scenario's 5-iteration warm-up absorbs any residual state. |
| Cool-down between full sweeps (re-runs) | ≥ 10 s | Only needed when comparing two full sweeps back-to-back. |

## 5. Tolerances and gate thresholds

All deltas are computed on the **same rig, same protocol, same binary
variant** — cross-rig numbers are context, not signal.

| Delta band (vs `main` baseline) | Status | Action |
|---|---|---|
| within ±5 % | noise | merge freely, no mention needed |
| 5 – 10 % | confound flag | PR comment must acknowledge; re-run once; if reproduces, document suspected cause |
| 10 – 20 % | **regression flag** | blocks merge unless waived with named owner + rationale + follow-up ticket |
| > 20 % | **regression block** | hard block; no waiver; root-cause before merge |

Applies to p50, p95, p99 on `--help` (any flag variant) and to AOT binary
size. A single-scenario regression outside these bands is still a
regression — the gate is per-scenario, not per-sweep-average.

Expected run-to-run noise on this rig, under the protocol, from
[`v2-cold-start-p99-investigation.md`](v2-cold-start-p99-investigation.md):

- p50 repeatability: σ ≈ 0.05 ms across back-to-back sweeps
- p95 repeatability: σ ≈ 0.15 ms
- p99 repeatability: σ ≈ 1.5 ms (tail noise dominates; use p95 for gating,
  p99 for trend watching only)

## 6. Alternate rigs (cross-check only, not authoritative)

Numbers from these rigs may appear in `docs/perf/archive/` but **do not
gate PRs**. They exist to catch "it's faster on newer silicon but we
shipped the slow path anyway" failures.

- **Wilhelm rig** — WSL2 on Intel Ultra 7 265H (v2.0.0 reference host).
  Retained for continuity with `docs/perf-baseline-v2.md`. WSL2 adds a
  translation layer that invalidates absolute-ms comparison with
  `malachor`; only use for ratio checks (e.g., "is v2.0.7 faster than
  v2.0.6 on the same WSL2 rig by the same %?").
- **CI runner (future)** — slot reserved for the GitHub Actions
  `ubuntu-24.04` class once `bania-v2-03`'s CI job lands. CI-runner
  numbers will be 1.5–2× slower than `malachor` in absolute terms and
  will have their own tolerance band to be calibrated on first run.

## 7. Why not a dedicated CI runner today?

Honest answer: we don't have the budget for a self-hosted pinned runner,
and the hosted `ubuntu-24.04` class is itself noisy (shared-tenant VM,
variable neighbours). `malachor` under this protocol is a *better*
controlled environment than a hosted CI runner today for pre-merge
micro-bench work — the protocol is the thing, not the silicon.

The plan, when a pinned runner exists:

1. Run this same protocol on the pinned runner for two weeks to
   establish its noise band (σ per percentile).
2. Recalibrate §5 tolerances for that rig.
3. Switch `malachor` to "developer self-check" status; CI runner becomes
   authoritative.

Until then: `malachor` + this protocol is the gate.

---

*"You want to file a regression? Fine. Show me the governor. Show me the
N. Show me the JSON in `docs/perf/runs/`. Then we talk. Otherwise it's
just a number you typed in a commit message. Numbers are gold, Jerry.
Numbers without protocol are mud."* — K. Bania
