#!/usr/bin/env bash
# Integration tests for Azure OpenAI CLI
# Runs the compiled binary end-to-end (no Azure credentials needed for these tests)
set -euo pipefail

PASS=0
FAIL=0
CLI="dotnet run --project azureopenai-cli/ --"

red()   { printf '\033[31m%s\033[0m\n' "$*"; }
green() { printf '\033[32m%s\033[0m\n' "$*"; }

assert_exit() {
    local desc="$1" expected="$2"
    shift 2
    local actual
    set +e
    eval "$*" > /dev/null 2>&1
    actual=$?
    set -e
    if [ "$actual" -eq "$expected" ]; then
        green "  ✓ $desc (exit $actual)"
        PASS=$((PASS + 1))
    else
        red "  ✗ $desc (expected exit $expected, got $actual)"
        FAIL=$((FAIL + 1))
    fi
}

assert_output_contains() {
    local desc="$1" pattern="$2"
    shift 2
    local output
    set +e
    output=$(eval "$*" 2>&1)
    set -e
    if echo "$output" | grep -qi "$pattern"; then
        green "  ✓ $desc"
        PASS=$((PASS + 1))
    else
        red "  ✗ $desc (output missing '$pattern')"
        FAIL=$((FAIL + 1))
    fi
}

assert_json_field() {
    local desc="$1" field="$2"
    shift 2
    local output
    set +e
    output=$(eval "$*" 2>&1)
    set -e
    if echo "$output" | grep -q "\"$field\""; then
        green "  ✓ $desc"
        PASS=$((PASS + 1))
    else
        red "  ✗ $desc (JSON missing field '$field')"
        FAIL=$((FAIL + 1))
    fi
}

echo "═══════════════════════════════════════════"
echo " Azure OpenAI CLI — Integration Tests"
echo "═══════════════════════════════════════════"
echo ""

# ── Flags & Help ──────────────────────────────
echo "▸ Flags & Help"
assert_exit "--help exits 0" 0 "$CLI --help"
assert_exit "-h exits 0" 0 "$CLI -h"
assert_exit "--version exits 0" 0 "$CLI --version"
assert_exit "-v exits 0" 0 "$CLI -v"
assert_exit "--models exits 0" 0 "$CLI --models"
assert_exit "--current-model with no config exits 1" 1 "$CLI --current-model"
assert_exit "no args exits 1" 1 "$CLI"
assert_output_contains "--help shows Usage" "Usage" "$CLI --help"
assert_output_contains "--version shows v1" "v1" "$CLI --version"

# ── Config ────────────────────────────────────
echo ""
echo "▸ Config"
assert_exit "--config show exits 0" 0 "$CLI --config show"
assert_output_contains "--config show has Temperature" "Temperature" "$CLI --config show"
assert_output_contains "--config show has Max Tokens" "Max Tokens" "$CLI --config show"
assert_output_contains "--config show has Timeout" "Timeout" "$CLI --config show"

# ── Input Validation ──────────────────────────
echo ""
echo "▸ Input Validation"
assert_exit "oversized prompt exits non-zero" 1 "$CLI \$(python3 -c \"print('x' * 33000)\")"
assert_exit "--set-model no arg exits 1" 1 "$CLI --set-model"
assert_exit "--temperature no value exits 1" 1 "$CLI --temperature"
assert_exit "--max-tokens no value exits 1" 1 "$CLI --max-tokens"
assert_exit "--temperature bad value exits 1" 1 "$CLI --temperature notanumber 'test'"

# ── Stdin Piping ──────────────────────────────
echo ""
echo "▸ Stdin Piping"
# With no Azure creds, these will fail at the API call stage (exit 1),
# but they should NOT hang — that's what we're really testing
assert_exit "empty stdin + no args exits 1" 1 "echo '' | $CLI"

# ── JSON Mode ─────────────────────────────────
echo ""
echo "▸ JSON Mode"
assert_exit "--json no prompt exits non-zero" 1 "$CLI --json"
assert_json_field "--json error has error field" "error" "$CLI --json"
assert_json_field "--json error has message field" "message" "$CLI --json"

# ── Docker ────────────────────────────────────
echo ""
echo "▸ Docker"
if command -v docker &> /dev/null; then
    assert_exit "docker --help exits 0" 0 "docker run --rm azure-openai-cli:test --help"
    assert_exit "docker --version exits 0" 0 "docker run --rm azure-openai-cli:test --version"
    assert_exit "docker --config show exits 0" 0 "docker run --rm azure-openai-cli:test --config show"
    assert_output_contains "docker --help shows Usage" "Usage" "docker run --rm azure-openai-cli:test --help"
else
    echo "  ⊘ Docker not available, skipping"
fi

# ── Summary ───────────────────────────────────
echo ""
echo "═══════════════════════════════════════════"
TOTAL=$((PASS + FAIL))
if [ "$FAIL" -eq 0 ]; then
    green " All $TOTAL tests passed!"
else
    red " $FAIL/$TOTAL tests failed"
fi
echo "═══════════════════════════════════════════"

exit "$FAIL"
