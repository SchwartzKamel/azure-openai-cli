#!/usr/bin/env bash
# tests/findings-backlog-lint-test.sh
#
# Regression test for the findings-backlog gate in
# scripts/exec-report-check.sh.
#
# Builds a throw-away repo skeleton in a temp directory, lays out a
# single fixture audit + a backlog, and drives the gate.
#
# Asserts:
#   1. Audit with unindexed CRITICAL finding -> gate exits non-zero
#      and the failure output names the missing ID.
#   2. Audit with indexed CRITICAL finding   -> gate exits zero.
#
# Run via:  make findings-backlog-test
# Or:       bash tests/findings-backlog-lint-test.sh
#
# Exit 0 = both assertions pass. Exit 1 = either assertion failed.

set -uo pipefail

repo_root=$(git rev-parse --show-toplevel)
gate_script="$repo_root/scripts/exec-report-check.sh"
fixtures="$repo_root/tests/fixtures/findings-backlog"

if [ ! -x "$gate_script" ] && [ ! -f "$gate_script" ]; then
    echo "[findings-backlog-test] FAIL: gate script not found at $gate_script" >&2
    exit 1
fi

work=$(mktemp -d -t findings-backlog-test.XXXXXX)
trap 'rm -rf "$work"' EXIT

# Skeleton: fake repo with the gate script under scripts/ and an
# initialized git repo so `git rev-parse` succeeds inside the gate.
mkdir -p "$work/scripts" "$work/docs/audits"
cp "$gate_script" "$work/scripts/exec-report-check.sh"
git -C "$work" init -q
git -C "$work" -c user.email=t@e.st -c user.name=test commit -q --allow-empty -m init

run_gate() {
    # Invoke the gate from inside the fixture repo. Phase 2 (push-range
    # check) returns 0 cleanly when there is no upstream / origin/main,
    # which is exactly our state, so any failure must come from Phase 1
    # (findings-backlog gate).
    ( cd "$work" && bash scripts/exec-report-check.sh ) >"$work/out" 2>"$work/err"
    echo $?
}

fail=0

# --- Negative case ---------------------------------------------------------
cp "$fixtures/audit-with-unindexed-finding.md" \
   "$work/docs/audits/audit-with-unindexed-finding.md"
# Backlog is intentionally absent for the negative case (or could be
# present without the row). Use an empty backlog to exercise the
# "row not found" branch unambiguously.
cat >"$work/docs/findings-backlog.md" <<'EOF'
# Findings Backlog (empty for negative test)

| ID | Source | Severity | State | Owner | Title | Last update |
|---|---|---|---|---|---|---|
EOF

rc=$(run_gate)
if [ "$rc" = "0" ]; then
    echo "[findings-backlog-test] FAIL: negative fixture should fail the gate but exit was 0" >&2
    fail=1
else
    if grep -q 'T-1' "$work/err" && grep -q 'findings-backlog gate' "$work/err"; then
        echo "[findings-backlog-test] OK: negative fixture failed the gate as expected (exit $rc)"
    else
        echo "[findings-backlog-test] FAIL: negative fixture failed but output did not name T-1" >&2
        cat "$work/err" >&2
        fail=1
    fi
fi

# --- Positive case ---------------------------------------------------------
rm -f "$work/docs/audits/"*.md
cp "$fixtures/audit-with-indexed-finding.md" \
   "$work/docs/audits/audit-with-indexed-finding.md"
cp "$fixtures/backlog-with-T-1.md" "$work/docs/findings-backlog.md"

rc=$(run_gate)
if [ "$rc" = "0" ]; then
    echo "[findings-backlog-test] OK: positive fixture passed the gate as expected"
else
    echo "[findings-backlog-test] FAIL: positive fixture should pass but exit was $rc" >&2
    cat "$work/err" >&2
    fail=1
fi

if [ "$fail" -eq 0 ]; then
    echo "[findings-backlog-test] all assertions passed"
    exit 0
else
    exit 1
fi
