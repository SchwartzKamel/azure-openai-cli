#!/usr/bin/env bash
# scripts/bench.sh — pre-merge startup benchmark harness for azure-openai-cli.
#
# Measures cold-start shape on the espanso/AHK hot path:
#   * --help
#   * --version --short     (Gate 2 validation command)
#   * --estimate "hello world"   (v2 only — estimator overhead)
#   * Full flag-parse path  (exercises ParseArgs + host build-up to env check)
#   * Stdin-capped --help   (verifies stdin cap doesn't block help)
#   * Max-RSS via /usr/bin/time -v
#   * On-disk DLL size
#
# Numbers are wall-clock from just-before-exec to after-exit (bash `date +%s%N`),
# printed in milliseconds. %e from /usr/bin/time is too coarse (10 ms) for the
# cold-start range we care about.
#
# Usage:
#   scripts/bench.sh --binary <dll-path> [--runs 50] [--warmup 2]
#   scripts/bench.sh --compare <v1-dll> <v2-dll> [--runs 50] [--warmup 2]
#
# Output: CSV to stdout. One row per (binary, scenario).
# Columns: binary,scenario,runs,mean_ms,p50_ms,p95_ms,min_ms,max_ms,stddev_ms,rss_kb,size_bytes,notes

set -euo pipefail

RUNS=50
WARMUP=2
MODE=""
V1=""
V2=""
BINARY=""

usage() {
    cat <<EOF
Usage:
  $0 --binary <dll> [--runs N] [--warmup N]
  $0 --compare <v1-dll> <v2-dll> [--runs N] [--warmup N]
EOF
    exit 2
}

# ---- arg parse ----
while [[ $# -gt 0 ]]; do
    case "$1" in
        --runs)    RUNS="$2"; shift 2 ;;
        --warmup)  WARMUP="$2"; shift 2 ;;
        --binary)  MODE="single"; BINARY="$2"; shift 2 ;;
        --compare) MODE="compare"; V1="$2"; V2="$3"; shift 3 ;;
        -h|--help) usage ;;
        *) echo "Unknown arg: $1" >&2; usage ;;
    esac
done
[[ -z "$MODE" ]] && usage

DOTNET="${DOTNET:-dotnet}"
command -v "$DOTNET" >/dev/null || { echo "dotnet not found" >&2; exit 1; }
command -v /usr/bin/time >/dev/null || { echo "/usr/bin/time not found" >&2; exit 1; }

# ---- stats helper (awk: mean, p50, p95, min, max, stddev) ----
stats() {
    # stdin: one number per line (ms, float)
    awk '
        { a[NR]=$1; s+=$1 }
        END {
            n=NR
            if (n==0) { print "0,0,0,0,0,0"; exit }
            mean=s/n
            # sort
            for(i=1;i<=n;i++) for(j=i+1;j<=n;j++) if(a[i]>a[j]){t=a[i];a[i]=a[j];a[j]=t}
            p50_i=int((n+1)*0.50); if(p50_i<1)p50_i=1; if(p50_i>n)p50_i=n
            p95_i=int((n+1)*0.95); if(p95_i<1)p95_i=1; if(p95_i>n)p95_i=n
            p50=a[p50_i]; p95=a[p95_i]
            mn=a[1]; mx=a[n]
            ss=0; for(i=1;i<=n;i++) ss+=(a[i]-mean)*(a[i]-mean)
            sd=(n>1)?sqrt(ss/(n-1)):0
            printf "%.2f,%.2f,%.2f,%.2f,%.2f,%.2f\n", mean, p50, p95, mn, mx, sd
        }'
}

# ---- single timed run: prints ms float to stdout ----
# $1: dll path; $@: args to pass to dll.  stdin of caller is forwarded.
time_one() {
    local dll="$1"; shift
    local start end
    start=$(date +%s%N)
    "$DOTNET" "$dll" "$@" >/dev/null 2>&1 || true
    end=$(date +%s%N)
    awk -v s="$start" -v e="$end" 'BEGIN{ printf "%.3f\n", (e-s)/1e6 }'
}

# Variant that feeds N bytes of stdin.
time_one_stdin() {
    local dll="$1"; local bytes="$2"; shift 2
    local start end
    start=$(date +%s%N)
    head -c "$bytes" /dev/urandom | "$DOTNET" "$dll" "$@" >/dev/null 2>&1 || true
    end=$(date +%s%N)
    awk -v s="$start" -v e="$end" 'BEGIN{ printf "%.3f\n", (e-s)/1e6 }'
}

# Measure max RSS (kB) via /usr/bin/time -v for a single invocation.
rss_one() {
    local dll="$1"; shift
    /usr/bin/time -v "$DOTNET" "$dll" "$@" >/dev/null 2> /tmp/bench_rss.$$ || true
    awk '/Maximum resident set size/ {print $NF}' /tmp/bench_rss.$$
    rm -f /tmp/bench_rss.$$
}

# ---- scenario runner ----
# $1 label; $2 dll; $3 "stdin_bytes" or "-" ; rest: args.
# Also accepts an exit-code expectation; we always tolerate non-zero (env var checks etc).
run_scenario() {
    local label="$1"; local dll="$2"; local stdin_bytes="$3"; shift 3
    local samples=()
    local i total=$((WARMUP + RUNS))
    for ((i=1; i<=total; i++)); do
        local t
        if [[ "$stdin_bytes" == "-" ]]; then
            t=$(time_one "$dll" "$@")
        else
            t=$(time_one_stdin "$dll" "$stdin_bytes" "$@")
        fi
        if (( i > WARMUP )); then
            samples+=("$t")
        fi
    done
    local st
    st=$(printf "%s\n" "${samples[@]}" | stats)
    # RSS from one extra invocation (noisy but representative).
    local rss
    if [[ "$stdin_bytes" == "-" ]]; then
        rss=$(rss_one "$dll" "$@")
    else
        rss=$(head -c "$stdin_bytes" /dev/urandom | /usr/bin/time -v "$DOTNET" "$dll" "$@" >/dev/null 2> /tmp/bench_rss.$$ || true; awk '/Maximum resident set size/ {print $NF}' /tmp/bench_rss.$$; rm -f /tmp/bench_rss.$$)
    fi
    local size=""
    [[ -f "$dll" ]] && size=$(stat -c '%s' "$dll")
    # fields: binary,scenario,runs,mean,p50,p95,min,max,sd,rss_kb,size,notes
    printf "%s,%s,%d,%s,%s,%s,\n" \
        "$(basename "$dll")" "$label" "$RUNS" "$st" "$rss" "$size"
}

# CSV header
echo "binary,scenario,runs,mean_ms,p50_ms,p95_ms,min_ms,max_ms,stddev_ms,rss_kb,size_bytes,notes"

bench_one() {
    local dll="$1"
    [[ -f "$dll" ]] || { echo "missing: $dll" >&2; exit 1; }
    local is_v2=0
    [[ "$dll" == *"az-ai-v2"* || "$dll" == *"V2"* || "$dll" == *"v2"* ]] && is_v2=1

    run_scenario "help"                       "$dll" "-" --help
    run_scenario "version-short"              "$dll" "-" --version --short
    if (( is_v2 )); then
        run_scenario "estimate-hello"         "$dll" "-" --estimate "hello world"
    fi
    # Full parse path — both v1 & v2 accept these flags; dies early on env check.
    run_scenario "parse-heavy"                "$dll" "-" --tools shell,file,web --max-rounds 10 --persona coder --json -- help-trigger
    # Stdin-capped help (should not block; cap is 32 KB).
    run_scenario "help-stdin-1k"              "$dll" "1024"  --help
    run_scenario "help-stdin-10k"             "$dll" "10240" --help
    run_scenario "help-stdin-32k"             "$dll" "32768" --help
}

case "$MODE" in
    single)  bench_one "$BINARY" ;;
    compare) bench_one "$V1"; bench_one "$V2" ;;
esac
