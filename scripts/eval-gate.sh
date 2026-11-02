#!/usr/bin/env bash
# eval-gate.sh -- run the built-in eval corpora against a PINNED model and fail on regressions.
#
# Usage:
#   scripts/eval-gate.sh [run]        score every corpus in the policy; exit 1 on any failure
#   scripts/eval-gate.sh baseline     record a baseline per corpus for the pinned model
#
# Environment:
#   BIN                  the az-ai binary (default: az-ai)
#   POLICY               policy file (default: .github/eval-gate.json)
#   OUT                  output directory for per-corpus JSON (default: eval-gate-out)
#   BASELINE_DIR         where baselines live (default: eval/baselines)
#   EVAL_GATE_MODEL      the pinned model name. A gate with no pinned model compares nothing.
#   EVAL_GATE_REQUIRED   true | false (default false). When true, a gate that cannot run FAILS
#                        instead of skipping: green must never mean "nothing was checked".
#   AZUREOPENAIENDPOINT, AZUREOPENAIAPI   credentials for the model
#
# Exit codes: 0 every corpus passed (or the gate was not required and could not run);
#             1 a corpus failed, or the gate was required and could not run; 2 bad policy/usage.
set -euo pipefail

MODE="${1:-run}"
BIN="${BIN:-hopper}"
POLICY="${POLICY:-.github/eval-gate.json}"
OUT="${OUT:-eval-gate-out}"
BASELINE_DIR="${BASELINE_DIR:-eval/baselines}"
MODEL="${EVAL_GATE_MODEL:-}"
REQUIRED="${EVAL_GATE_REQUIRED:-false}"
SUMMARY="${GITHUB_STEP_SUMMARY:-/dev/null}"

case "$MODE" in
  run|baseline) ;;
  -h|--help) sed -n '2,22p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
  *) echo "usage: $0 [run|baseline]" >&2; exit 2 ;;
esac

say()  { printf '%s\n' "$*"; printf '%s\n' "$*" >> "$SUMMARY"; }
fail() { say "eval-gate: FAIL -- $*"; exit 1; }

# Read the policy with python3 (present on every runner and every dev box that runs the tests).
if ! POLICY_LINES=$(python3 - "$POLICY" <<'PY'
import json, sys
try:
    p = json.load(open(sys.argv[1]))
    repeat = int(p["repeat"])
    corpora = p["corpora"]
    assert 1 <= repeat <= 10, "repeat must be 1..10"
    assert corpora, "no corpora listed"
    print("repeat\t%d" % repeat)
    for c in corpora:
        fu = float(c["fail_under"])
        assert 0 <= fu <= 100, "fail_under must be 0..100"
        print("corpus\t%s\t%s" % (c["name"], c["fail_under"]))
except Exception as e:
    sys.stderr.write("policy error: %s\n" % e)
    sys.exit(2)
PY
); then
  echo "eval-gate: cannot read policy $POLICY" >&2
  exit 2
fi
REPEAT=$(printf '%s\n' "$POLICY_LINES" | awk -F'\t' '$1=="repeat"{print $2}')

# --- Can the gate run at all? -------------------------------------------------------------------
if [ -z "${AZUREOPENAIENDPOINT:-}" ] || [ -z "${AZUREOPENAIAPI:-}" ]; then
  if [ "$REQUIRED" = "true" ]; then
    fail "the gate is required but credentials (AZUREOPENAIENDPOINT, AZUREOPENAIAPI) are missing. Refusing to report green without running."
  fi
  say "## Eval gate: SKIPPED"
  say ""
  say "No credentials were available, so **nothing was checked**. This is not a pass."
  say "Set EVAL_GATE_REQUIRED=true to make a skipped gate fail."
  exit 0
fi

if [ -z "$MODEL" ]; then
  if [ "$REQUIRED" = "true" ]; then
    fail "the gate is required but EVAL_GATE_MODEL is not set. An unpinned model makes results incomparable."
  fi
  say "WARNING: EVAL_GATE_MODEL is not set; running with the default model, so results are not comparable across runs."
fi

mkdir -p "$OUT"
SAFE_MODEL=$(printf '%s' "${MODEL:-default}" | tr '/: ' '___')
MODEL_ARGS=()
[ -n "$MODEL" ] && MODEL_ARGS=(--model "$MODEL")

rm -f "$OUT/.rc" "$OUT/.bad"
printf '%s\n' "$POLICY_LINES" | awk -F'\t' '$1=="corpus"{print $2 "\t" $3}' | while IFS=$'\t' read -r name fail_under; do
  base="$BASELINE_DIR/$SAFE_MODEL.$name.json"
  args=(eval --corpus "$name" ${MODEL_ARGS[@]+"${MODEL_ARGS[@]}"} --repeat "$REPEAT" --fail-under "$fail_under" --json)
  if [ "$MODE" = "baseline" ]; then
    args+=(--write-baseline "$base")
  elif [ -f "$base" ]; then
    args+=(--baseline "$base")
  fi
  set +e
  "$BIN" "${args[@]}" > "$OUT/$name.json" 2> "$OUT/$name.err"
  rc=$?
  set -e
  printf '%s\t%s\n' "$name" "$rc" >> "$OUT/.rc"
done

# --- Summarise (python3 reads the per-corpus JSON) ------------------------------------------------
rm -f "$OUT/.summary"
python3 - "$OUT" "$MODE" "${MODEL:-default}" "$BASELINE_DIR" "$SAFE_MODEL" <<'PY' | tee -a "$SUMMARY"
import json, os, sys
out, mode, model, bdir, safe = sys.argv[1:6]
rcs = dict(l.rstrip("\n").split("\t") for l in open(os.path.join(out, ".rc")))
print("## Eval gate: %s (model `%s`)" % ("baselines written" if mode == "baseline" else "run", model))
print("")
print("| corpus | rc | result | score | flaky | baseline | note |")
print("|--------|----|--------|-------|-------|----------|------|")
bad = 0
for name, rc in rcs.items():
    path = os.path.join(out, name + ".json")
    try:
        r = json.load(open(path))
    except Exception:
        err = open(os.path.join(out, name + ".err")).read().strip().splitlines()[:1]
        print("| %s | %s | no report | - | - | - | %s |" % (name, rc, (err[0] if err else "no output")[:80]))
        bad += 1
        continue
    b = r.get("baseline")
    bl = "none" if b is None else "%d regression(s)" % len(b["regressions"])
    note = r.get("reason") or ""
    print("| %s | %s | %s | %s%% | %s | %s | %s |" % (name, rc, r["result"].upper(), r["score_percent"], r.get("flaky", 0), bl, note[:90]))
    if rc != "0":
        bad += 1
open(os.path.join(out, ".bad"), "w").write(str(bad))
PY
rm -f "$OUT/.rc"
bad=$(cat "$OUT/.bad" 2>/dev/null || echo 1)
rm -f "$OUT/.bad"
if [ "$MODE" = "run" ] && [ "$bad" -gt 0 ]; then
  fail "$bad corpus run(s) did not pass. See the table above and the per-corpus JSON in $OUT/."
fi
say ""
say "eval-gate: OK"
