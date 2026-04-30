#!/usr/bin/env bash
# Integration tests for Azure OpenAI CLI
# Runs the compiled binary end-to-end (no Azure credentials needed for most tests).
set -euo pipefail

# Binary path (override via env for CI / post-rename).
BIN="${BIN:-./azureopenai-cli/bin/Release/net10.0/az-ai}"

PASS=0
FAIL=0
SKIP=0

red()    { printf '\033[31m%s\033[0m\n' "$*"; }
green()  { printf '\033[32m%s\033[0m\n' "$*"; }
yellow() { printf '\033[33m%s\033[0m\n' "$*"; }

pass() { green "  ✓ PASS: $1"; PASS=$((PASS + 1)); }
fail() { red   "  ✗ FAIL: $1 — $2"; FAIL=$((FAIL + 1)); }
skip() { yellow "  ⊘ SKIP: $1 — $2"; SKIP=$((SKIP + 1)); }

# ── Assertion helpers ───────────
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

# ── Espanso YAML structural lint (fast fail-fast — runs before binary tests) ─
if [ -x scripts/lint-espanso-yml.sh ] || [ -f scripts/lint-espanso-yml.sh ]; then
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo " Espanso YAML lint"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    set +e
    bash scripts/lint-espanso-yml.sh examples/espanso-ahk-wsl/espanso/ai-windows-to-wsl.yml
    lint_rc=$?
    set -e
    if [ "$lint_rc" -eq 0 ]; then
        pass "espanso-yml-lint: ai-windows-to-wsl.yml"
    else
        fail "espanso-yml-lint: ai-windows-to-wsl.yml" "lint exited $lint_rc"
        exit 1
    fi
fi

# ═══════════════════════════════════════════════════════════════════════════
# run_bin_tests — integration tests against the compiled az-ai binary.
# ═══════════════════════════════════════════════════════════════════════════
run_bin_tests() {
    echo ""
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo " az-ai ($BIN)"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

    if [ ! -x "$BIN" ]; then
        if [ "${INTEGRATION_BUILD:-0}" = "1" ]; then
            echo "  INTEGRATION_BUILD=1 → building binary via 'make dotnet-build'…"
            make dotnet-build >/dev/null 2>&1 || {
                yellow "  ⊘ skipping integration: build failed"
                SKIP=$((SKIP + 1)); return 0
            }
        fi
    fi
    if [ ! -x "$BIN" ]; then
        yellow "  ⊘ skipping integration: binary not found at $BIN"
        yellow "    run 'make dotnet-build' (or set INTEGRATION_BUILD=1)"
        SKIP=$((SKIP + 1))
        return 0
    fi

    # Hermetic HOME + strip any ambient creds so tests don't accidentally hit the API.
    local test_home; test_home=$(mktemp -d)
    local _test_env_moved=false
    if [ -f .env ]; then mv .env .env.test_bak; _test_env_moved=true; fi
    local _test_api="${AZUREOPENAIAPI:-}" _test_ep="${AZUREOPENAIENDPOINT:-}" _test_mdl="${AZUREOPENAIMODEL:-}"
    unset AZUREOPENAIAPI AZUREOPENAIENDPOINT AZUREOPENAIMODEL 2>/dev/null || true
    # Pin a price-table-known model for --estimate tests (no API call made).
    export AZUREOPENAIMODEL=gpt-4o-mini

    # Restore handler
    _restore_test_env() {
        if [ "$_test_env_moved" = true ] && [ -f .env.test_bak ]; then mv .env.test_bak .env; fi
        unset AZUREOPENAIMODEL
        [ -n "$_test_api" ] && export AZUREOPENAIAPI="$_test_api"
        [ -n "$_test_ep"  ] && export AZUREOPENAIENDPOINT="$_test_ep"
        [ -n "$_test_mdl" ] && export AZUREOPENAIMODEL="$_test_mdl"
        rm -rf "$test_home"
    }
    trap _restore_test_env RETURN

    # ── 1. --help ─────────────────────────────────────────────────────────
    echo ""
    echo "▸ Help / Version / Completions"
    assert_exit_code "--help exits 0" 0 "$BIN" --help
    local help_out; help_out=$("$BIN" --help 2>&1)
    for phrase in "Azure OpenAI" "--agent" "--raw" "--telemetry" "--estimate" "--persona"; do
        if printf '%s' "$help_out" | grep -qF -- "$phrase"; then
            pass "--help contains '$phrase'"
        else
            fail "--help contains '$phrase'" "phrase not found in --help output"
        fi
    done

    # ── 2. --version matches 2.x.y ────────────────────────────────────────
    assert_exit_code "--version exits 0" 0 "$BIN" --version
    local ver_out; ver_out=$("$BIN" --version 2>&1)
    if printf '%s' "$ver_out" | grep -qE '2\.[0-9]+\.[0-9]+'; then
        pass "--version matches 2.x.y"
    else
        fail "--version matches 2.x.y" "got: $ver_out"
    fi

    # ── 3. --version --short matches csproj <Version> (Gate 2) ───────────
    # Source of truth: azureopenai-cli/AzureOpenAI_CLI.csproj <Version>.
    # Fixed in v2.0.6 — prior revision hardcoded "2.0.2" and caused the
    # v2.0.5 release workflow to be gated on the very drift that v2.0.5
    # set out to fix (audit findings C-1 / C-2). Do not re-hardcode.
    local expected_ver csproj_path
    csproj_path="$(git rev-parse --show-toplevel 2>/dev/null || pwd)/azureopenai-cli/AzureOpenAI_CLI.csproj"
    expected_ver=$(sed -n 's|.*<Version>\(.*\)</Version>.*|\1|p' "$csproj_path" | head -1)
    if [[ -z "$expected_ver" ]]; then
        fail "--version --short matches csproj <Version> (Gate 2)" \
             "could not parse <Version> from csproj"
    else
        assert_equals "--version --short matches csproj <Version> (Gate 2)" \
            "$expected_ver" "$BIN" --version --short
    fi

    # ── 4. --completions bash ─────────────────────────────────────────────
    assert_exit_code "--completions bash exits 0" 0 "$BIN" --completions bash
    local first_line
    first_line=$("$BIN" --completions bash 2>/dev/null | head -1)
    if printf '%s' "$first_line" | grep -qE '^# bash completion for az-ai'; then
        pass "--completions bash starts with bash-completion header"
    else
        fail "--completions bash starts with bash-completion header" "got first line: $first_line"
    fi

    # ── 5. --completions zsh ──────────────────────────────────────────────
    assert_exit_code "--completions zsh exits 0" 0 "$BIN" --completions zsh
    first_line=$("$BIN" --completions zsh 2>/dev/null | head -1)
    if printf '%s' "$first_line" | grep -q '^#compdef'; then
        pass "--completions zsh starts with #compdef"
    else
        fail "--completions zsh starts with #compdef" "got first line: $first_line"
    fi

    # ── 6. --estimate prints USD figure ───────────────────────────────────
    echo ""
    echo "▸ Estimator (no API call)"
    assert_exit_code "--estimate exits 0" 0 "$BIN" --estimate "hello world"
    local est_out; est_out=$("$BIN" --estimate "hello world" 2>&1)
    if printf '%s' "$est_out" | grep -qE '\$0\.'; then
        pass "--estimate prints a \$0. USD figure"
    else
        fail "--estimate prints a \$0. USD figure" "no \$0. in output: $est_out"
    fi

    # ── 7. --estimate --json has required fields ──────────────────────────
    local est_json; est_json=$("$BIN" --estimate --json "hello" 2>&1)
    if printf '%s' "$est_json" | python3 -c "
import json, sys
d = json.loads(sys.stdin.read())
for f in ('model', 'input_tokens_est', 'total_usd_max'):
    assert f in d, f
" >/dev/null 2>&1; then
        pass "--estimate --json has model/input_tokens_est/total_usd_max"
    else
        fail "--estimate --json has model/input_tokens_est/total_usd_max" "got: $est_json"
    fi

    # ── 12. --raw --estimate → single line, no banner ─────────────────────
    local raw_out; raw_out=$("$BIN" --raw --estimate "hi" 2>/dev/null)
    local raw_lines; raw_lines=$(printf '%s\n' "$raw_out" | awk 'END{print NR}')
    if [ "$raw_lines" -eq 1 ]; then
        pass "--raw --estimate emits exactly 1 line"
    else
        fail "--raw --estimate emits exactly 1 line" "got $raw_lines lines: $raw_out"
    fi
    if printf '%s' "$raw_out" | grep -qiE 'cost estimate|input tokens|NO API CALL'; then
        fail "--raw --estimate suppresses banner text" "banner leaked: $raw_out"
    else
        pass "--raw --estimate suppresses banner text"
    fi

    # ── 8 & 9. --set-model / --current-model / --models round-trip ────────
    echo ""
    echo "▸ Model alias round-trip (hermetic HOME)"
    assert_exit_code "--set-model testAlias=testDeployment exits 0" 0 \
        env HOME="$test_home" "$BIN" --set-model testAlias=testDeployment
    assert_equals "--current-model returns testAlias" "testAlias" \
        env HOME="$test_home" "$BIN" --current-model
    assert_exit_code "--models exits 0" 0 env HOME="$test_home" "$BIN" --models
    assert_contains "--models lists seeded alias" "testAlias" \
        env HOME="$test_home" "$BIN" --models
    assert_contains "--models lists seeded deployment" "testDeployment" \
        env HOME="$test_home" "$BIN" --models

    # ── 10. Invalid flag (non-JSON) — Scope 3 rejects unknown flags ───────
    echo ""
    echo "▸ Invalid flag handling"
    # Must exit 2 and surface an [ERROR] on stderr with a 'Run --help' hint.
    local inv_stderr inv_rc
    set +e; inv_stderr=$("$BIN" --nope 2>&1 1>/dev/null); inv_rc=$?; set -e
    if [ $inv_rc -eq 2 ]; then
        pass "--nope exits 2 (unknown_flag)"
    else
        fail "--nope exits 2 (unknown_flag)" "exited $inv_rc"
    fi
    if printf '%s' "$inv_stderr" | grep -qF '[ERROR] unknown flag: --nope'; then
        pass "--nope has [ERROR] unknown flag on stderr"
    else
        fail "--nope has [ERROR] unknown flag on stderr" "stderr was: $inv_stderr"
    fi
    if printf '%s' "$inv_stderr" | grep -qF 'Run --help for usage.'; then
        pass "--nope stderr includes 'Run --help for usage.' hint"
    else
        fail "--nope stderr includes 'Run --help for usage.' hint" "stderr was: $inv_stderr"
    fi

    # ── 11. Invalid flag + --json → valid JSON envelope on STDERR ──────────
    # Scope 2 + 3 (Puddy): JSON error payloads must land on stderr so
    # consumers piping stdout to jq don't see them. Unknown-flag emits the
    # nested {"error":{"code":"unknown_flag",...}} envelope.
    local inv_json_stderr inv_json_rc
    set +e; inv_json_stderr=$("$BIN" --json --nope 2>&1 1>/dev/null); inv_json_rc=$?; set -e
    if [ $inv_json_rc -eq 2 ]; then
        pass "--json --nope exits 2 (unknown_flag)"
    else
        fail "--json --nope exits 2 (unknown_flag)" "exited $inv_json_rc"
    fi
    if printf '%s' "$inv_json_stderr" | python3 -c "
import json, sys
d = json.loads(sys.stdin.read())
assert 'error' in d, 'no error field'
assert isinstance(d['error'], dict), 'error must be nested object'
assert d['error'].get('code') == 'unknown_flag', 'code must be unknown_flag'
assert d['error'].get('flag') == '--nope', 'flag must be --nope'
" >/dev/null 2>&1; then
        pass "--json --nope emits nested unknown_flag JSON on stderr"
    else
        fail "--json --nope emits nested unknown_flag JSON on stderr" "got: $inv_json_stderr"
    fi

    # Confirm stdout is empty for the JSON error path.
    local inv_json_stdout
    set +e; inv_json_stdout=$("$BIN" --json --nope 2>/dev/null); set -e
    if [ -z "$inv_json_stdout" ]; then
        pass "--json --nope stdout empty (errors go to stderr)"
    else
        fail "--json --nope stdout empty" "stdout leaked: $inv_json_stdout"
    fi

    # ── 13. --tools datetime --help does not leak tool list to stderr ─────
    echo ""
    echo "▸ Tools help hygiene"
    local tools_stderr; tools_stderr=$("$BIN" --tools datetime --help 2>&1 1>/dev/null)
    if [ -z "$tools_stderr" ]; then
        pass "--tools datetime --help has empty stderr"
    else
        fail "--tools datetime --help has empty stderr" "stderr leaked: $tools_stderr"
    fi

    # ── 14. Cancellation: SIGINT → exit 3 ─────────────────────────────────
    echo ""
    echo "▸ Cancellation"
    if [ -z "${AZUREOPENAIENDPOINT:-}" ] || [ -z "${AZUREOPENAIAPI:-}" ]; then
        skip "SIGINT → exit 3" "requires AZUREOPENAIENDPOINT + AZUREOPENAIAPI"
    else
        "$BIN" --agent "long task that should be cancelled" >/dev/null 2>&1 &
        local pid=$!
        sleep 1
        kill -INT "$pid" 2>/dev/null || true
        set +e; wait "$pid"; local rc=$?; set -e
        if [ "$rc" -eq 3 ]; then
            pass "SIGINT → exit 3"
        else
            fail "SIGINT → exit 3" "got exit $rc"
        fi
    fi

    # ── 15. FR-021 regression — malformed persona name in .squad.json ─────
    #
    # Pre-written regression test for docs/proposals/FR-021-persona-argumentexception-ux-wrap.md.
    #
    # TODAY (2.0.0): the call site at Program.cs:321 invokes
    #   PersonaMemory.ReadHistory(activePersona.Name)
    # outside the try/catch. If .squad.json contains a persona whose `name`
    # field violates [a-z0-9_-]{1,64}, SanitizePersonaName throws an
    # unhandled ArgumentException → .NET aborts → exit 134 with a stack
    # trace on stderr. That's the bug FR-021 tracks.
    #
    # 2.0.1: Kramer adds the three-line wrap (per FR-021 §Fix). The call site
    # catches ArgumentException and returns ErrorAndExit(..., 1), producing
    # exit 1 with a single `[ERROR] ...` line on stderr. No stack trace.
    #
    # This test is SKIPPED BY DEFAULT so preflight stays green on 2.0.0.
    # Flip the sentinel to force-run:
    #   FR021_FIXED=1 bash tests/integration_tests.sh
    # On 2.0.0 the forced run FAILS (exit 134, stack trace) — proving the
    # test actually exercises the bug, not a mock of it. On 2.0.1 the forced
    # run PASSES. The 2.0.1 PR should set FR021_FIXED=1 as the default (or
    # remove the guard entirely once the wrap ships) so the test gates
    # future regressions.
    #
    # DO NOT delete this test without Lippman sign-off.
    #
    # 2.0.1 update: the wrap shipped (Program.cs — FR-021 try/catch at the
    # persona call site). Default flipped to 1 so the test runs by default
    # and gates regressions. Force-disable with FR021_FIXED=0 if needed.
    echo ""
    echo "▸ Persona error UX (FR-021 regression)"
    if [ "${FR021_FIXED:-1}" != "1" ]; then
        skip "FR-021 malformed persona → exit 1 + [ERROR]" \
             "pre-written for 2.0.1; un-skip by running with FR021_FIXED=1 once Program.cs:321 wraps ArgumentException"
    else
        local fr021_dir; fr021_dir=$(mktemp -d)
        local fr021_bin; fr021_bin=$(cd "$(dirname "$BIN")" && pwd)/$(basename "$BIN")
        cat > "$fr021_dir/.squad.json" <<'FR021_JSON'
{
  "team": {"name": "fr021-regression"},
  "personas": [
    {"name": "bad name!", "role": "adversarial", "description": "FR-021 regression fixture — name violates [a-z0-9_-]{1,64}", "system_prompt": "x"}
  ]
}
FR021_JSON
        local fr021_stderr fr021_rc
        set +e
        fr021_stderr=$(cd "$fr021_dir" && \
            env HOME="$test_home" \
                AZUREOPENAIENDPOINT="https://example.invalid/" \
                AZUREOPENAIAPI="dummy-key-not-used" \
                "$fr021_bin" --persona "bad name!" "hi" 2>&1 1>/dev/null)
        fr021_rc=$?
        set -e

        # (a) exit code is 1 (clean error), not 134 (unhandled exception abort).
        if [ "$fr021_rc" -eq 1 ]; then
            pass "FR-021 malformed persona exits 1 (not 134)"
        else
            fail "FR-021 malformed persona exits 1 (not 134)" "got exit $fr021_rc; stderr was: $fr021_stderr"
        fi

        # (b) stderr contains the [ERROR] prefix ErrorAndExit emits.
        if printf '%s' "$fr021_stderr" | grep -qF '[ERROR]'; then
            pass "FR-021 stderr contains [ERROR] prefix"
        else
            fail "FR-021 stderr contains [ERROR] prefix" "stderr: $fr021_stderr"
        fi

        # (c) stderr does NOT leak an unhandled-exception stack trace.
        if printf '%s' "$fr021_stderr" | grep -qE 'Unhandled exception|System\.ArgumentException|^   at '; then
            fail "FR-021 stderr has no stack trace / unhandled exception" "stderr leaked: $fr021_stderr"
        else
            pass "FR-021 stderr has no stack trace / unhandled exception"
        fi

        rm -rf "$fr021_dir"
    fi

    # ── API-gated smoke (skip unless creds present) ───────────────────────
    if [ -z "${AZUREOPENAIENDPOINT:-}" ] || [ -z "${AZUREOPENAIAPI:-}" ]; then
        skip "real API call" "AZUREOPENAIENDPOINT/AZUREOPENAIAPI not set"
    fi

    trap - RETURN
    _restore_test_env
}

# ═══════════════════════════════════════════════════════════════════════════
# Orchestrate
# ═══════════════════════════════════════════════════════════════════════════

run_bin_tests

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
