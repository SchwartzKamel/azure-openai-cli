#!/usr/bin/env bash
# Integration tests for Azure OpenAI CLI
# Runs the compiled binary end-to-end (no Azure credentials needed for most tests).
#
# Dual-tree window (v1 + v2):
#   V1_BIN — legacy CLI (deleted at cutover; `run_v1_tests` can go with it)
#   V2_BIN — next-gen CLI (az-ai-v2 assembly; survives the binary rename)
#
# Post-cutover: delete `run_v1_tests` + its invocation, repoint V2_BIN if
# the artifact is renamed, and this script keeps working.
set -euo pipefail

# Dual-binary paths (override via env for CI / post-rename).
V1_BIN="${V1_BIN:-./azureopenai-cli/bin/Release/net10.0/AzureOpenAI_CLI}"
V2_BIN="${V2_BIN:-./azureopenai-cli-v2/bin/Release/net10.0/az-ai-v2}"

PASS=0
FAIL=0
SKIP=0
# v1 still uses `dotnet run` — the v1 block owns that invocation path.
CLI="dotnet run --project azureopenai-cli/ --"

red()    { printf '\033[31m%s\033[0m\n' "$*"; }
green()  { printf '\033[32m%s\033[0m\n' "$*"; }
yellow() { printf '\033[33m%s\033[0m\n' "$*"; }

pass() { green "  ✓ PASS: $1"; PASS=$((PASS + 1)); }
fail() { red   "  ✗ FAIL: $1 — $2"; FAIL=$((FAIL + 1)); }
skip() { yellow "  ⊘ SKIP: $1 — $2"; SKIP=$((SKIP + 1)); }

# ── New-style helpers (used by v2 block; safe for v1 to use too) ───────────
assert_exit_code() {
    # assert_exit_code <name> <expected> <cmd...>
    local name="$1" expected="$2"; shift 2
    local actual
    set +e; "$@" >/dev/null 2>&1; actual=$?; set -e
    if [ "$actual" -eq "$expected" ]; then pass "$name"
    else fail "$name" "expected exit $expected, got $actual"; fi
}

assert_contains() {
    # assert_contains <name> <pattern> <cmd...>   (grep -F, stderr+stdout merged)
    local name="$1" pattern="$2"; shift 2
    local out
    set +e; out=$("$@" 2>&1); set -e
    if printf '%s' "$out" | grep -qF -- "$pattern"; then pass "$name"
    else fail "$name" "output missing '$pattern'"; fi
}

assert_equals() {
    # assert_equals <name> <expected> <cmd...>   (compares stdout exactly)
    local name="$1" expected="$2"; shift 2
    local actual
    set +e; actual=$("$@" 2>/dev/null); set -e
    if [ "$actual" = "$expected" ]; then pass "$name"
    else fail "$name" "expected '$expected', got '$actual'"; fi
}

assert_stderr_json() {
    # assert_stderr_json <name> <required_field> <cmd...>
    # Asserts stderr is valid JSON containing a given field.
    local name="$1" field="$2"; shift 2
    local errf
    errf=$(mktemp)
    set +e; "$@" >/dev/null 2>"$errf"; set -e
    if ! python3 -c "import json,sys; d=json.load(open(sys.argv[1])); assert '$field' in d" "$errf" >/dev/null 2>&1; then
        fail "$name" "stderr is not valid JSON with field '$field' (got: $(head -c 200 "$errf"))"
        rm -f "$errf"; return
    fi
    rm -f "$errf"
    pass "$name"
}

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

# ═══════════════════════════════════════════════════════════════════════════
# run_v1_tests — DELETE THIS FUNCTION AT CUTOVER
# Everything between here and the matching `}` is v1-only. The v2 block below
# is self-contained and survives the cutover.
# ═══════════════════════════════════════════════════════════════════════════
run_v1_tests() {
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo " v1 — legacy CLI (dotnet run --project azureopenai-cli/)"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

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
assert_exit "--temperature 3.0 exits 1 (above range)" 1 "$CLI --temperature 3.0 'test'"
assert_exit "--temperature -1 exits 1 (below range)" 1 "$CLI --temperature -- -1 'test'"
assert_exit "--max-tokens -1 exits 1 (below range)" 1 "$CLI --max-tokens -1 'test'"
assert_exit "--max-tokens 0 exits 1 (below range)" 1 "$CLI --max-tokens 0 'test'"
assert_exit "--max-tokens 200000 exits 1 (above range)" 1 "$CLI --max-tokens 200000 'test'"
assert_output_contains "--temperature 3.0 shows range error" "between 0.0 and 2.0" "$CLI --temperature 3.0 'test'"
assert_output_contains "--max-tokens 0 shows range error" "between 1 and 128000" "$CLI --max-tokens 0 'test'"

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

# ── Adversarial Cases (Puddy) ─────────────────
# "Gotta test it. Either it works or it doesn't."
# All cases here must fail fast at validation or missing-config.
# No real Azure calls — creds are deliberately stripped.
echo ""
echo "=== Adversarial Cases (Puddy) ==="

# Hide .env to simulate hostile environments. The dotenv loader overwrites.
_adv_env_moved=false
if [ -f .env ]; then
    mv .env .env.adv_bak
    _adv_env_moved=true
fi
_adv_restore_api="${AZUREOPENAIAPI:-}"
_adv_restore_ep="${AZUREOPENAIENDPOINT:-}"
_adv_restore_mdl="${AZUREOPENAIMODEL:-}"
unset AZUREOPENAIAPI AZUREOPENAIENDPOINT AZUREOPENAIMODEL 2>/dev/null || true

echo ""
echo "▸ Missing Required Env"
# No API key anywhere — should fail cleanly, not stack-trace.
assert_exit "no AZUREOPENAIAPI exits non-zero" 99 "$CLI 'hello'"
assert_output_contains "no AZUREOPENAIAPI shows API key error" "API key" "$CLI 'hello'"
# Stack traces leak ' at ' frames — assert they don't surface.
set +e
_adv_out=$($CLI 'hello' 2>&1)
set -e
if echo "$_adv_out" | grep -qE '^\s+at [A-Za-z].*\(.*\)'; then
    red "  ✗ no AZUREOPENAIAPI leaked a stack trace"
    FAIL=$((FAIL + 1))
else
    green "  ✓ no AZUREOPENAIAPI produces clean error (no stack trace)"
    PASS=$((PASS + 1))
fi

echo ""
echo "▸ Malformed Endpoint URL"
# API key present but endpoint is garbage — must exit cleanly, not hang or crash.
# Exit code varies (1 = arg/config validation, 99 = cred/endpoint failure) — both acceptable.
set +e
timeout 30 bash -c "AZUREOPENAIAPI=fake-key AZUREOPENAIENDPOINT=not-a-url AZUREOPENAIMODEL=gpt-4 $CLI 'hello' > /dev/null 2>&1"
_adv_ep_rc=$?
set -e
if [ "$_adv_ep_rc" -eq 124 ]; then
    red "  ✗ malformed endpoint hung (timeout)"
    FAIL=$((FAIL + 1))
elif [ "$_adv_ep_rc" -ne 0 ]; then
    green "  ✓ malformed endpoint exits non-zero cleanly (exit $_adv_ep_rc)"
    PASS=$((PASS + 1))
else
    red "  ✗ malformed endpoint unexpectedly succeeded (exit 0)"
    FAIL=$((FAIL + 1))
fi
set +e
_adv_ep_out=$(AZUREOPENAIAPI=fake-key AZUREOPENAIENDPOINT=not-a-url AZUREOPENAIMODEL=gpt-4 $CLI 'hello' 2>&1)
set -e
if echo "$_adv_ep_out" | grep -qE '^\s+at [A-Za-z].*\(.*\)'; then
    red "  ✗ malformed endpoint leaked a stack trace"
    FAIL=$((FAIL + 1))
else
    green "  ✓ malformed endpoint produces clean error (no stack trace)"
    PASS=$((PASS + 1))
fi

echo ""
echo "▸ Temperature Out-Of-Range (99)"
# Spec'd edge — validation must reject BEFORE any API call.
assert_exit "--temperature 99 exits 1" 1 "$CLI --temperature 99 'test'"
assert_output_contains "--temperature 99 shows range error" "between 0.0 and 2.0" "$CLI --temperature 99 'test'"

echo ""
echo "▸ Huge Prompt Boundary (1MB)"
# 1MB of 'x'. With no creds, should fail fast with API key error — not hang, not truncate.
# Guard with a timeout so a hang fails loudly instead of wedging CI.
set +e
timeout 30 bash -c "python3 -c \"print('x' * 1048576)\" | $CLI > /dev/null 2>&1"
_adv_big_rc=$?
set -e
if [ "$_adv_big_rc" -eq 124 ]; then
    red "  ✗ 1MB piped prompt hung (timeout)"
    FAIL=$((FAIL + 1))
elif [ "$_adv_big_rc" -ne 0 ]; then
    green "  ✓ 1MB piped prompt failed cleanly (exit $_adv_big_rc, no hang)"
    PASS=$((PASS + 1))
else
    green "  ✓ 1MB piped prompt succeeded cleanly (exit 0)"
    PASS=$((PASS + 1))
fi

echo ""
echo "▸ Invalid Model Name"
# Deployment name that doesn't exist. Without creds we still expect the
# missing-API-key path (exit 99, "API key" message) — NOT a null-ref / stack trace.
assert_exit "--model invalid exits non-zero" 99 "$CLI --model not-a-real-deployment 'hi'"
set +e
_adv_mdl_out=$($CLI --model not-a-real-deployment 'hi' 2>&1)
set -e
if echo "$_adv_mdl_out" | grep -qiE 'NullReferenceException|Object reference not set'; then
    red "  ✗ --model invalid leaked a null-ref"
    FAIL=$((FAIL + 1))
else
    green "  ✓ --model invalid produces routed error (no null-ref)"
    PASS=$((PASS + 1))
fi

# Restore env
if [ "$_adv_env_moved" = true ] && [ -f .env.adv_bak ]; then
    mv .env.adv_bak .env
fi
[ -n "$_adv_restore_api" ] && export AZUREOPENAIAPI="$_adv_restore_api"
[ -n "$_adv_restore_ep" ] && export AZUREOPENAIENDPOINT="$_adv_restore_ep"
[ -n "$_adv_restore_mdl" ] && export AZUREOPENAIMODEL="$_adv_restore_mdl"

return 0
} # end run_v1_tests — DELETE THIS FUNCTION AT CUTOVER

# ═══════════════════════════════════════════════════════════════════════════
# run_v2_tests — survives the cutover (v2 becomes the default).
# If/when the v2 binary is renamed, just repoint V2_BIN. This block does not
# depend on `dotnet run` or any v1-era path.
# ═══════════════════════════════════════════════════════════════════════════
run_v2_tests() {
    echo ""
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo " v2 — az-ai-v2 ($V2_BIN)"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

    if [ ! -x "$V2_BIN" ]; then
        if [ "${INTEGRATION_BUILD:-0}" = "1" ]; then
            echo "  INTEGRATION_BUILD=1 → building v2 binary via 'make dotnet-build'…"
            make dotnet-build >/dev/null 2>&1 || {
                yellow "  ⊘ skipping v2 integration: build failed"
                SKIP=$((SKIP + 1)); return 0
            }
        fi
    fi
    if [ ! -x "$V2_BIN" ]; then
        yellow "  ⊘ skipping v2 integration: binary not found at $V2_BIN"
        yellow "    run 'make dotnet-build' (or set INTEGRATION_BUILD=1)"
        SKIP=$((SKIP + 1))
        return 0
    fi

    # Hermetic HOME + strip any ambient creds so tests don't accidentally hit the API.
    local v2_home; v2_home=$(mktemp -d)
    local _v2_env_moved=false
    if [ -f .env ]; then mv .env .env.v2_bak; _v2_env_moved=true; fi
    local _v2_api="${AZUREOPENAIAPI:-}" _v2_ep="${AZUREOPENAIENDPOINT:-}" _v2_mdl="${AZUREOPENAIMODEL:-}"
    unset AZUREOPENAIAPI AZUREOPENAIENDPOINT AZUREOPENAIMODEL 2>/dev/null || true
    # Pin a price-table-known model for --estimate tests (no API call made).
    export AZUREOPENAIMODEL=gpt-4o-mini

    # Restore handler
    _restore_v2_env() {
        if [ "$_v2_env_moved" = true ] && [ -f .env.v2_bak ]; then mv .env.v2_bak .env; fi
        unset AZUREOPENAIMODEL
        [ -n "$_v2_api" ] && export AZUREOPENAIAPI="$_v2_api"
        [ -n "$_v2_ep"  ] && export AZUREOPENAIENDPOINT="$_v2_ep"
        [ -n "$_v2_mdl" ] && export AZUREOPENAIMODEL="$_v2_mdl"
        rm -rf "$v2_home"
    }
    trap _restore_v2_env RETURN

    # ── 1. --help ─────────────────────────────────────────────────────────
    echo ""
    echo "▸ Help / Version / Completions"
    assert_exit_code "v2 --help exits 0" 0 "$V2_BIN" --help
    local help_out; help_out=$("$V2_BIN" --help 2>&1)
    for phrase in "Azure OpenAI" "--agent" "--raw" "--telemetry" "--estimate" "--persona"; do
        if printf '%s' "$help_out" | grep -qF -- "$phrase"; then
            pass "v2 --help contains '$phrase'"
        else
            fail "v2 --help contains '$phrase'" "phrase not found in --help output"
        fi
    done

    # ── 2. --version matches 2.x.y ────────────────────────────────────────
    assert_exit_code "v2 --version exits 0" 0 "$V2_BIN" --version
    local ver_out; ver_out=$("$V2_BIN" --version 2>&1)
    if printf '%s' "$ver_out" | grep -qE '2\.[0-9]+\.[0-9]+'; then
        pass "v2 --version matches 2.x.y"
    else
        fail "v2 --version matches 2.x.y" "got: $ver_out"
    fi

    # ── 3. --version --short is exactly "2.0.0\n" (Gate 2) ────────────────
    assert_equals "v2 --version --short is exactly 2.0.0 (Gate 2)" "2.0.0" "$V2_BIN" --version --short

    # ── 4. --completions bash ─────────────────────────────────────────────
    assert_exit_code "v2 --completions bash exits 0" 0 "$V2_BIN" --completions bash
    local first_line
    first_line=$("$V2_BIN" --completions bash 2>/dev/null | head -1)
    if printf '%s' "$first_line" | grep -qE '^# bash completion for az-ai-v2'; then
        pass "v2 --completions bash starts with bash-completion header"
    else
        fail "v2 --completions bash starts with bash-completion header" "got first line: $first_line"
    fi

    # ── 5. --completions zsh ──────────────────────────────────────────────
    assert_exit_code "v2 --completions zsh exits 0" 0 "$V2_BIN" --completions zsh
    first_line=$("$V2_BIN" --completions zsh 2>/dev/null | head -1)
    if printf '%s' "$first_line" | grep -q '^#compdef'; then
        pass "v2 --completions zsh starts with #compdef"
    else
        fail "v2 --completions zsh starts with #compdef" "got first line: $first_line"
    fi

    # ── 6. --estimate prints USD figure ───────────────────────────────────
    echo ""
    echo "▸ Estimator (no API call)"
    assert_exit_code "v2 --estimate exits 0" 0 "$V2_BIN" --estimate "hello world"
    local est_out; est_out=$("$V2_BIN" --estimate "hello world" 2>&1)
    if printf '%s' "$est_out" | grep -qE '\$0\.'; then
        pass "v2 --estimate prints a \$0. USD figure"
    else
        fail "v2 --estimate prints a \$0. USD figure" "no \$0. in output: $est_out"
    fi

    # ── 7. --estimate --json has required fields ──────────────────────────
    local est_json; est_json=$("$V2_BIN" --estimate --json "hello" 2>&1)
    if printf '%s' "$est_json" | python3 -c "
import json, sys
d = json.loads(sys.stdin.read())
for f in ('model', 'input_tokens_est', 'total_usd_max'):
    assert f in d, f
" >/dev/null 2>&1; then
        pass "v2 --estimate --json has model/input_tokens_est/total_usd_max"
    else
        fail "v2 --estimate --json has model/input_tokens_est/total_usd_max" "got: $est_json"
    fi

    # ── 12. --raw --estimate → single line, no banner ─────────────────────
    local raw_out; raw_out=$("$V2_BIN" --raw --estimate "hi" 2>/dev/null)
    local raw_lines; raw_lines=$(printf '%s\n' "$raw_out" | awk 'END{print NR}')
    if [ "$raw_lines" -eq 1 ]; then
        pass "v2 --raw --estimate emits exactly 1 line"
    else
        fail "v2 --raw --estimate emits exactly 1 line" "got $raw_lines lines: $raw_out"
    fi
    if printf '%s' "$raw_out" | grep -qiE 'cost estimate|input tokens|NO API CALL'; then
        fail "v2 --raw --estimate suppresses banner text" "banner leaked: $raw_out"
    else
        pass "v2 --raw --estimate suppresses banner text"
    fi

    # ── 8 & 9. --set-model / --current-model / --models round-trip ────────
    echo ""
    echo "▸ Model alias round-trip (hermetic HOME)"
    assert_exit_code "v2 --set-model testAlias=testDeployment exits 0" 0 \
        env HOME="$v2_home" "$V2_BIN" --set-model testAlias=testDeployment
    assert_equals "v2 --current-model returns testAlias" "testAlias" \
        env HOME="$v2_home" "$V2_BIN" --current-model
    assert_exit_code "v2 --models exits 0" 0 env HOME="$v2_home" "$V2_BIN" --models
    assert_contains "v2 --models lists seeded alias" "testAlias" \
        env HOME="$v2_home" "$V2_BIN" --models
    assert_contains "v2 --models lists seeded deployment" "testDeployment" \
        env HOME="$v2_home" "$V2_BIN" --models

    # ── 10. Invalid flag (non-JSON) ───────────────────────────────────────
    echo ""
    echo "▸ Invalid flag handling"
    # v2 must exit nonzero and surface an [ERROR] on stderr.
    local inv_stderr inv_rc
    set +e; inv_stderr=$("$V2_BIN" --nope 2>&1 1>/dev/null); inv_rc=$?; set -e
    if [ $inv_rc -ne 0 ]; then
        pass "v2 --nope exits nonzero"
    else
        fail "v2 --nope exits nonzero" "exited 0"
    fi
    if printf '%s' "$inv_stderr" | grep -qF '[ERROR]'; then
        pass "v2 --nope has [ERROR] on stderr"
    else
        fail "v2 --nope has [ERROR] on stderr" "stderr was: $inv_stderr"
    fi

    # ── 11. Invalid flag + --json → valid JSON with 'error' field ─────────
    # NOTE: task spec expected JSON on stderr, but v2 emits structured errors
    # on stdout (consumer pipes to jq). Test matches actual v2 behavior.
    local inv_json_stdout inv_json_rc
    set +e; inv_json_stdout=$("$V2_BIN" --json --nope 2>/dev/null); inv_json_rc=$?; set -e
    if [ $inv_json_rc -ne 0 ]; then
        pass "v2 --json --nope exits nonzero"
    else
        fail "v2 --json --nope exits nonzero" "exited 0"
    fi
    if printf '%s' "$inv_json_stdout" | python3 -c "
import json, sys
d = json.loads(sys.stdin.read())
assert 'error' in d, 'no error field'
" >/dev/null 2>&1; then
        pass "v2 --json --nope emits valid JSON with 'error' field"
    else
        fail "v2 --json --nope emits valid JSON with 'error' field" "got: $inv_json_stdout"
    fi

    # ── 13. --tools datetime --help does not leak tool list to stderr ─────
    echo ""
    echo "▸ Tools help hygiene"
    local tools_stderr; tools_stderr=$("$V2_BIN" --tools datetime --help 2>&1 1>/dev/null)
    if [ -z "$tools_stderr" ]; then
        pass "v2 --tools datetime --help has empty stderr"
    else
        fail "v2 --tools datetime --help has empty stderr" "stderr leaked: $tools_stderr"
    fi

    # ── 14. Cancellation: SIGINT → exit 3 ─────────────────────────────────
    echo ""
    echo "▸ Cancellation"
    if [ -z "${AZUREOPENAIENDPOINT:-}" ] || [ -z "${AZUREOPENAIAPI:-}" ]; then
        skip "v2 SIGINT → exit 3" "requires AZUREOPENAIENDPOINT + AZUREOPENAIAPI"
    else
        "$V2_BIN" --agent "long task that should be cancelled" >/dev/null 2>&1 &
        local pid=$!
        sleep 1
        kill -INT "$pid" 2>/dev/null || true
        set +e; wait "$pid"; local rc=$?; set -e
        if [ "$rc" -eq 3 ]; then
            pass "v2 SIGINT → exit 3"
        else
            fail "v2 SIGINT → exit 3" "got exit $rc"
        fi
    fi

    # ── API-gated smoke (skip unless creds present) ───────────────────────
    if [ -z "${AZUREOPENAIENDPOINT:-}" ] || [ -z "${AZUREOPENAIAPI:-}" ]; then
        skip "v2 real API call" "AZUREOPENAIENDPOINT/AZUREOPENAIAPI not set"
    fi

    trap - RETURN
    _restore_v2_env
}

# ═══════════════════════════════════════════════════════════════════════════
# Orchestrate
# ═══════════════════════════════════════════════════════════════════════════
run_v1_tests     # <-- delete this line at cutover
run_v2_tests

# ── Summary ───────────────────────────────────
echo ""
echo "═══════════════════════════════════════════"
TOTAL=$((PASS + FAIL))
if [ "$FAIL" -eq 0 ]; then
    green " All $TOTAL tests passed! ($SKIP skipped)"
else
    red " $FAIL/$TOTAL tests failed ($SKIP skipped)"
fi
echo "═══════════════════════════════════════════"

exit "$FAIL"
