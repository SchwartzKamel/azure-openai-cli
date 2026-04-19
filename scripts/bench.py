#!/usr/bin/env python3
"""Portable startup-time benchmark for azure-openai-cli binaries.

Measures cold-start of --version (or a user-specified arg) over N runs,
reports min/median/p90/max in milliseconds. Works on Linux, macOS, WSL,
and Windows (any host with python3 on PATH).
"""
from __future__ import annotations

import argparse
import statistics
import subprocess
import sys
import time
from pathlib import Path


def run_once(binary: Path, args: list[str]) -> float:
    start = time.perf_counter()
    proc = subprocess.run(
        [str(binary), *args],
        capture_output=True,
        check=False,
    )
    elapsed_ms = (time.perf_counter() - start) * 1000.0
    if proc.returncode != 0:
        sys.stderr.write(
            f"warning: {binary.name} exited {proc.returncode} "
            f"(stderr: {proc.stderr[:200]!r})\n"
        )
    return elapsed_ms


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("binary", type=Path, help="path to binary to benchmark")
    parser.add_argument("-n", "--runs", type=int, default=10, help="measurement runs (default 10)")
    parser.add_argument("-w", "--warmup", type=int, default=2, help="warm-up runs (default 2)")
    parser.add_argument(
        "--args",
        nargs=argparse.REMAINDER,
        default=["--version"],
        help="args to pass to binary (default: --version)",
    )
    ns = parser.parse_args()

    if not ns.binary.exists():
        sys.stderr.write(f"error: {ns.binary} does not exist — build it first\n")
        return 2

    for _ in range(ns.warmup):
        run_once(ns.binary, ns.args)

    samples = [run_once(ns.binary, ns.args) for _ in range(ns.runs)]
    samples.sort()

    size_kb = ns.binary.stat().st_size / 1024.0
    size_mb = size_kb / 1024.0

    print(f"Binary:     {ns.binary}")
    print(f"Size:       {size_mb:.1f} MB ({size_kb:.0f} KB)")
    print(f"Runs:       {ns.runs} (after {ns.warmup} warm-up)")
    print(f"Args:       {' '.join(ns.args)}")
    print()
    print(f"  min:      {samples[0]:7.2f} ms")
    print(f"  median:   {statistics.median(samples):7.2f} ms")
    print(f"  mean:     {statistics.mean(samples):7.2f} ms")
    p90_index = max(0, int(len(samples) * 0.9) - 1)
    print(f"  p90:      {samples[p90_index]:7.2f} ms")
    print(f"  max:      {samples[-1]:7.2f} ms")
    return 0


if __name__ == "__main__":
    sys.exit(main())
