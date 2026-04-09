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
assert_exit "--current-model exits 0 or 1" 0 "$CLI --current-model || test \$? -eq 1"
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

# ── Agent Mode ─────────────────────────────────
echo ""
echo "=== Agent Mode Tests ==="

echo ""
echo "▸ Agent Help & Flag Presence"
assert_output_contains "--help shows Agent Mode section" "Agent Mode" "$CLI --help"
assert_output_contains "--help shows --agent flag" "[-]-agent" "$CLI --help"
assert_output_contains "--help shows --tools flag" "[-]-tools" "$CLI --help"
assert_output_contains "--help shows --max-rounds flag" "[-]-max-rounds" "$CLI --help"
assert_output_contains "--help shows available tools list" "shell,file,web,clipboard,datetime" "$CLI --help"

echo ""
echo "▸ Agent Argument Validation"
assert_exit "--agent without prompt exits 1" 1 "$CLI --agent"
assert_exit "--tools without value exits 1" 1 "$CLI --tools"
assert_exit "--max-rounds 0 exits 1 (below minimum)" 1 "$CLI --agent --max-rounds 0 'test'"
assert_exit "--max-rounds 21 exits 1 (above maximum)" 1 "$CLI --agent --max-rounds 21 'test'"
assert_exit "--max-rounds negative exits 1" 1 "$CLI --agent --max-rounds -1 'test'"
assert_exit "--max-rounds non-integer exits 1" 1 "$CLI --agent --max-rounds abc 'test'"
assert_exit "--max-rounds without value exits 1" 1 "$CLI --agent --max-rounds"
assert_output_contains "--max-rounds 0 shows error message" "integer 1-20" "$CLI --agent --max-rounds 0 'test'"
assert_output_contains "--tools missing value shows error" "comma-separated" "$CLI --tools"

echo ""
echo "▸ Agent JSON Validation (no prompt)"
assert_exit "--json --agent without prompt exits 1" 1 "$CLI --json --agent"
assert_json_field "--json --agent no prompt has error field" "error" "$CLI --json --agent"
assert_json_field "--json --agent no prompt has message field" "message" "$CLI --json --agent"

echo ""
echo "▸ Agent No-Credentials"
# Temporarily hide .env to simulate missing Azure credentials.
# The dotenv loader uses overwriteExistingVars, so the file must be absent.
_env_moved=false
if [ -f .env ]; then
    mv .env .env.integration_bak
    _env_moved=true
fi
# Unset any ambient cred variables so there is no fallback
_restore_api="${AZUREOPENAIAPI:-}"
_restore_ep="${AZUREOPENAIENDPOINT:-}"
_restore_mdl="${AZUREOPENAIMODEL:-}"
unset AZUREOPENAIAPI AZUREOPENAIENDPOINT AZUREOPENAIMODEL 2>/dev/null || true

assert_exit "--agent with prompt, no creds exits 99" 99 "$CLI --agent 'test prompt'"
assert_output_contains "--agent no-creds error mentions API key" "API key" "$CLI --agent 'test prompt'"
assert_exit "--json --agent no-creds exits 99" 99 "$CLI --json --agent 'test prompt'"
assert_json_field "--json --agent no-creds has error field" "error" "$CLI --json --agent 'test prompt'"
assert_json_field "--json --agent no-creds has message field" "message" "$CLI --json --agent 'test prompt'"
assert_output_contains "--json --agent no-creds mentions API key" "API key" "$CLI --json --agent 'test prompt'"

# Restore .env and env vars
if [ "$_env_moved" = true ] && [ -f .env.integration_bak ]; then
    mv .env.integration_bak .env
fi
[ -n "$_restore_api" ] && export AZUREOPENAIAPI="$_restore_api"
[ -n "$_restore_ep" ] && export AZUREOPENAIENDPOINT="$_restore_ep"
[ -n "$_restore_mdl" ] && export AZUREOPENAIMODEL="$_restore_mdl"

echo ""
echo "▸ Agent + Version Coexistence"
assert_exit "--version still exits 0 after agent tests" 0 "$CLI --version"
assert_output_contains "--version still shows v1" "v1" "$CLI --version"

# ── Structured Output (--schema) ──────────────
echo ""
echo "▸ Structured Output (--schema)"
assert_output_contains "--help shows --schema flag" "[-]-schema" "$CLI --help"
assert_exit "--schema without value exits 1" 1 "$CLI --schema"
assert_output_contains "--schema missing value shows error" "requires a JSON schema" "$CLI --schema"
assert_exit "--schema with invalid JSON exits non-zero" 1 "$CLI --schema '{invalid}' 'test'"
assert_output_contains "--schema invalid JSON shows error" "Invalid JSON schema" "$CLI --schema '{invalid}' 'test'"

# ── Ralph Mode ──────────────────────────────────
echo ""
echo "=== Ralph Mode Tests ==="

echo ""
echo "▸ Ralph Help & Flag Presence"
assert_output_contains "--help shows Ralph Mode section" "Ralph Mode" "$CLI --help"
assert_output_contains "--help shows --ralph flag" "[-]-ralph" "$CLI --help"
assert_output_contains "--help shows --validate flag" "[-]-validate" "$CLI --help"
assert_output_contains "--help shows --task-file flag" "[-]-task-file" "$CLI --help"
assert_output_contains "--help shows --max-iterations flag" "[-]-max-iterations" "$CLI --help"

echo ""
echo "▸ Ralph Argument Validation"
assert_exit "--ralph without prompt exits 1" 1 "$CLI --ralph"
assert_exit "--validate without value exits 1" 1 "$CLI --ralph --validate"
assert_exit "--task-file missing file exits 1" 1 "$CLI --ralph --task-file /nonexistent/file.md 'test'"
assert_exit "--max-iterations 0 exits 1" 1 "$CLI --ralph --max-iterations 0 'test'"
assert_exit "--max-iterations 51 exits 1" 1 "$CLI --ralph --max-iterations 51 'test'"
assert_exit "--max-iterations negative exits 1" 1 "$CLI --ralph --max-iterations -1 'test'"
assert_exit "--max-iterations non-integer exits 1" 1 "$CLI --ralph --max-iterations abc 'test'"
assert_exit "--max-iterations without value exits 1" 1 "$CLI --ralph --max-iterations"
assert_exit "--task-file without value exits 1" 1 "$CLI --ralph --task-file"
assert_output_contains "--validate missing value shows error" "requires a command" "$CLI --ralph --validate"
assert_output_contains "--max-iterations 0 shows error message" "between 1 and 50" "$CLI --ralph --max-iterations 0 'test'"
assert_output_contains "--task-file missing shows error" "requires a file" "$CLI --ralph --task-file"

echo ""
echo "▸ Ralph JSON Validation (no prompt)"
assert_exit "--json --ralph without prompt exits 1" 1 "$CLI --json --ralph"
assert_json_field "--json --ralph no prompt has error field" "error" "$CLI --json --ralph"
assert_json_field "--json --ralph no prompt has message field" "message" "$CLI --json --ralph"

echo ""
echo "▸ Ralph + Version Coexistence"
assert_exit "--version still exits 0 after ralph tests" 0 "$CLI --version"
assert_output_contains "--version still shows v1 after ralph tests" "v1" "$CLI --version"

# ── Squad / Persona Mode ───────────────────────
echo ""
echo "=== Squad / Persona Mode Tests ==="

# Help flag tests run from repo root (no cd needed)
echo ""
echo "▸ Persona Help Flags"
assert_output_contains "--help shows --persona flag" "[-]-persona" "$CLI --help"
assert_output_contains "--help shows --personas flag" "[-]-personas" "$CLI --help"
assert_output_contains "--help shows --squad-init flag" "[-]-squad-init" "$CLI --help"
assert_output_contains "--help shows Persona Mode section" "Persona Mode" "$CLI --help"

echo ""
echo "▸ Persona Argument Validation"
assert_exit "--persona without value exits 1" 1 "$CLI --persona"
assert_output_contains "--persona missing shows error" "requires a name" "$CLI --persona"

# Work in a temp directory so we don't pollute the repo root
_squad_test_dir=$(mktemp -d)
_orig_dir=$(pwd)
_abs_proj="$(cd azureopenai-cli && pwd)"
_CLI_ABS="dotnet run --project $_abs_proj --"
cd "$_squad_test_dir"

echo ""
echo "▸ Squad Init & Persona Listing"
assert_exit "--squad-init exits 0" 0 "$_CLI_ABS --squad-init"
assert_exit "--squad-init idempotent exits 0" 0 "$_CLI_ABS --squad-init"
assert_exit "--personas exits 0 after init" 0 "$_CLI_ABS --personas"
assert_output_contains "--personas lists coder" "coder" "$_CLI_ABS --personas"
assert_output_contains "--personas lists reviewer" "reviewer" "$_CLI_ABS --personas"
assert_output_contains "--personas lists architect" "architect" "$_CLI_ABS --personas"
assert_output_contains "--personas lists writer" "writer" "$_CLI_ABS --personas"
assert_output_contains "--personas lists security" "security" "$_CLI_ABS --personas"
assert_exit "--persona unknown exits non-zero" 0 "! $_CLI_ABS --persona nonexistent 'test'"

# Cleanup squad test dir
cd "$_orig_dir"
rm -rf "$_squad_test_dir"

echo ""
echo "▸ Persona No-Squad-Json"
# In repo root there's no .squad.json, so --personas should fail
assert_exit "--personas with no .squad.json exits 1" 1 "$CLI --personas"

# ── Docker ────────────────────────────────────
echo ""
echo "▸ Docker"
if command -v docker &> /dev/null && docker image inspect azure-openai-cli:test &> /dev/null; then
    assert_exit "docker --help exits 0" 0 "docker run --rm azure-openai-cli:test --help"
    assert_exit "docker --version exits 0" 0 "docker run --rm azure-openai-cli:test --version"
    assert_exit "docker --config show exits 0" 0 "docker run --rm azure-openai-cli:test --config show"
    assert_output_contains "docker --help shows Usage" "Usage" "docker run --rm azure-openai-cli:test --help"
else
    echo "  ⊘ Docker image not available, skipping"
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
