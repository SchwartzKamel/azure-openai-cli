#!/usr/bin/env python3
"""Portable startup-time benchmark harness for azure-openai-cli binaries.

Promoted from the v1.x single-scenario harness to a first-class pre-merge
perf gate (Bania, todo `bania-v2-03`). Measures cold-start wall-clock of a
native AOT (or framework-dependent) binary over a configurable sample count,
supports warm-up discard, emits percentiles (p50 / p90 / p95 / p99 / p99.9)
and can sweep a matrix of invocation flags in one run.

Defaults are tuned to the reference rig documented in
`docs/perf/reference-hardware.md` (malachor, i7-10710U, linux-x64).

Typical use:

    # quick smoke (N=100, 5 warm-up)
    python3 scripts/bench.py dist/aot/az-ai-v2

    # full pre-merge gate run (N=500, flag matrix, JSON out)
    python3 scripts/bench.py dist/aot/az-ai-v2 --n 500 --warmup 5 \\
        --flag-matrix --json > docs/perf/runs/$(date +%Y%m%d-%H%M).json

Stdout is human-readable by default; add `--json` for a machine-readable
bundle (scenarios, percentiles, full raw samples, env fingerprint) suitable
for CI artefacts and PR-diff comments.
"""
from __future__ import annotations

import argparse
import json
import os
import platform
import statistics
import subprocess
import sys
import time
from pathlib import Path


# Flag matrix used when --flag-matrix is passed. Ordered so the "no flags"
# baseline is always the first scenario for easy before/after reading.
FLAG_MATRIX: list[tuple[str, list[str]]] = [
    ("help-no-flags", ["--help"]),
    ("help-otel", ["--help", "--otel"]),
    ("help-metrics", ["--help", "--metrics"]),
    ("help-otel-metrics", ["--help", "--otel", "--metrics"]),
]


def run_once(binary: Path, args: list[str]) -> float:
    """Return wall-clock ms for one invocation. Stdout/stderr suppressed."""
    start = time.perf_counter()
    proc = subprocess.run(
        [str(binary), *args],
        capture_output=True,
        check=False,
    )
    elapsed_ms = (time.perf_counter() - start) * 1000.0
    if proc.returncode != 0:
        sys.stderr.write(
            f"warning: {binary.name} {' '.join(args)} exited {proc.returncode} "
            f"(stderr: {proc.stderr[:200]!r})\n"
        )
    return elapsed_ms


def percentile(sorted_samples: list[float], p: float) -> float:
    if not sorted_samples:
        return float("nan")
    idx = min(len(sorted_samples) - 1, int(len(sorted_samples) * p))
    return sorted_samples[idx]


def summarise(samples: list[float]) -> dict:
    s = sorted(samples)
    return {
        "n": len(samples),
        "min": min(samples),
        "p50": statistics.median(samples),
        "mean": statistics.mean(samples),
        "p90": percentile(s, 0.90),
        "p95": percentile(s, 0.95),
        "p99": percentile(s, 0.99),
        "p99_9": percentile(s, 0.999),
        "max": max(samples),
        "stddev": statistics.stdev(samples) if len(samples) > 1 else 0.0,
    }


def bench_scenario(
    binary: Path, args: list[str], n: int, warmup: int
) -> dict:
    """Run one scenario; discard `warmup` iterations, measure `n`."""
    for _ in range(warmup):
        run_once(binary, args)
    samples = [run_once(binary, args) for _ in range(n)]
    return {
        "args": args,
        "warmup": warmup,
        "summary": summarise(samples),
        "samples_ms": samples,
    }


def env_fingerprint(binary: Path) -> dict:
    """Record just enough about the rig to flag apples-vs-oranges later."""
    fp = {
        "binary": str(binary),
        "binary_size_bytes": binary.stat().st_size,
        "platform": platform.platform(),
        "machine": platform.machine(),
        "python": platform.python_version(),
        "cpu_count_logical": os.cpu_count(),
    }
    # Best-effort — these files only exist on Linux.
    try:
        with open("/proc/cpuinfo") as f:
            for line in f:
                if line.startswith("model name"):
                    fp["cpu_model"] = line.split(":", 1)[1].strip()
                    break
    except OSError:
        pass
    try:
        gov = Path(
            "/sys/devices/system/cpu/cpu0/cpufreq/scaling_governor"
        ).read_text().strip()
        fp["cpu_governor"] = gov
    except OSError:
        pass
    return fp


def format_human(scenarios: list[dict], fp: dict) -> str:
    lines = []
    lines.append(f"Binary:     {fp['binary']}")
    lines.append(
        f"Size:       {fp['binary_size_bytes'] / 1024 / 1024:.2f} MiB "
        f"({fp['binary_size_bytes']} bytes)"
    )
    lines.append(f"Host:       {fp.get('cpu_model', '?')} "
                 f"| {fp['platform']} | governor={fp.get('cpu_governor', '?')}")
    lines.append("")
    header = (
        f"{'scenario':<22} {'N':>4} {'min':>7} {'p50':>7} {'mean':>7} "
        f"{'p90':>7} {'p95':>7} {'p99':>7} {'p99.9':>7} {'max':>7} {'sigma':>6}"
    )
    lines.append(header)
    lines.append("-" * len(header))
    for sc in scenarios:
        s = sc["summary"]
        label = sc.get("label") or " ".join(sc["args"])
        lines.append(
            f"{label:<22} {s['n']:4d} "
            f"{s['min']:7.3f} {s['p50']:7.3f} {s['mean']:7.3f} "
            f"{s['p90']:7.3f} {s['p95']:7.3f} {s['p99']:7.3f} "
            f"{s['p99_9']:7.3f} {s['max']:7.3f} {s['stddev']:6.3f}"
        )
    lines.append("")
    lines.append("All figures in ms. Warm-up iterations discarded from stats.")
    return "\n".join(lines)


def main() -> int:
    p = argparse.ArgumentParser(
        description=__doc__,
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    p.add_argument("binary", type=Path, help="path to binary to benchmark")
    p.add_argument(
        "-n", "--n", "--runs",
        dest="n", type=int, default=100,
        help="measurement iterations (default 100; use 500 for pre-merge)",
    )
    p.add_argument(
        "-w", "--warmup", type=int, default=5,
        help="warm-up iterations discarded from stats (default 5)",
    )
    p.add_argument(
        "--args", nargs=argparse.REMAINDER, default=["--help"],
        help="args to pass to binary (default: --help); ignored with --flag-matrix",
    )
    p.add_argument(
        "--flag-matrix", action="store_true",
        help="run preset matrix: [no-flags, --otel, --metrics, --otel --metrics]",
    )
    p.add_argument(
        "--json", action="store_true",
        help="emit machine-readable JSON to stdout instead of human table",
    )
    ns = p.parse_args()

    if not ns.binary.exists():
        sys.stderr.write(f"error: {ns.binary} does not exist -- build it first\n")
        return 2

    if ns.flag_matrix:
        plan: list[tuple[str, list[str]]] = FLAG_MATRIX
    else:
        plan = [(" ".join(ns.args), ns.args)]

    scenarios = []
    for label, args in plan:
        sc = bench_scenario(ns.binary, args, ns.n, ns.warmup)
        sc["label"] = label
        scenarios.append(sc)

    fp = env_fingerprint(ns.binary)
    bundle = {
        "env": fp,
        "n": ns.n,
        "warmup": ns.warmup,
        "scenarios": scenarios,
    }

    if ns.json:
        json.dump(bundle, sys.stdout, indent=2)
        sys.stdout.write("\n")
    else:
        print(format_human(scenarios, fp))
    return 0


if __name__ == "__main__":
    sys.exit(main())
