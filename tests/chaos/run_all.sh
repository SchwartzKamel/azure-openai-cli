#!/usr/bin/env bash
# Master runner — executes every chaos script and aggregates results.
set -u
here="$(cd "$(dirname "$0")" && pwd)"
source "$here/_lib.sh"
init_results
export ROOT WORK BIN RESULTS
chmod +x "$here"/*.sh || true
for s in "$here"/0[1-9]_*.sh "$here"/10_*.sh; do
  echo; echo "### $(basename "$s")"
  bash "$s" || true
done
echo
echo "=== RESULTS (tsv: $RESULTS) ==="
column -t -s $'\t' "$RESULTS" | head -200
