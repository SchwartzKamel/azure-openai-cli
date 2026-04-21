#!/usr/bin/env bash
# FDR chaos drill — shared helpers.
# Usage: source ./_lib.sh from any attack script.

set -u
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
WORK="$ROOT/tests/chaos/artifacts"
# Prefer the snapshotted binary in $WORK so dotnet test in 07a can't vaporize it.
if [ -x "$WORK/az-ai-v2" ]; then
  BIN="$WORK/az-ai-v2"
else
  BIN="$ROOT/azureopenai-cli-v2/bin/Release/net10.0/linux-x64/publish/az-ai-v2"
fi
RESULTS="$WORK/results.tsv"
LOG() { printf "\n=== %s ===\n" "$*"; }

# run_attack <id> <label> -- <cmd...>
# Records exit code + stderr/stdout prefix into $RESULTS as TSV.
run_attack() {
  local id="$1"; shift
  local label="$1"; shift
  [ "$1" = "--" ] && shift
  local outf="$WORK/${id}.out"
  local errf="$WORK/${id}.err"
  : > "$outf"; : > "$errf"
  # Run under a tight timeout (some attacks could hang); never let it run unbounded.
  timeout --preserve-status 20s "$@" >"$outf" 2>"$errf"
  local rc=$?
  local out1k err1k
  out1k=$(head -c 1024 "$outf" | tr '\0' '?' | tr -d '\r')
  err1k=$(head -c 1024 "$errf" | tr '\0' '?' | tr -d '\r')
  printf '%s\t%s\t%s\t%q\t%q\n' "$id" "$label" "$rc" "$out1k" "$err1k" >> "$RESULTS"
  printf "[%s] rc=%s  label=%s\n" "$id" "$rc" "$label"
  printf "  stderr: %s\n" "${err1k:0:200}"
}

init_results() {
  mkdir -p "$WORK"
  : > "$RESULTS"
  printf 'id\tlabel\trc\tstdout_1k\tstderr_1k\n' >> "$RESULTS"
}
