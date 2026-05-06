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

    # ── S03E14 -- The Screen Reader (Mickey Abbott) ───────────────────────
    # Accessibility / CLI ergonomics: --plain, NO_COLOR, TERM=dumb, AZ_AI_PLAIN
    # all yield ASCII-only and ANSI-free output across the headline surfaces.
    echo ""
    echo "▸ Accessibility (S03E14)"

    # The literal control byte is awkward in shell single-quotes; build it once.
    local _esc; _esc=$(printf '\033')

    _assert_clean() {
        # _assert_clean <label> <captured-text>
        local label="$1" text="$2"
        if printf '%s' "$text" | LC_ALL=C grep -q "${_esc}\["; then
            fail "$label is ANSI-free" "found ESC[ in output"
            return
        fi
        if printf '%s' "$text" | LC_ALL=C grep -q '[^[:print:][:space:]]'; then
            fail "$label is ASCII-only" "found non-printable / non-ASCII byte"
            return
        fi
        pass "$label is ASCII-only and ANSI-free"
    }

    # 1. --help under --plain.
    local plain_help; plain_help=$("$BIN" --plain --help 2>&1)
    _assert_clean "--plain --help" "$plain_help"
    if printf '%s' "$plain_help" | grep -qF -- "--plain"; then
        pass "--help advertises --plain"
    else
        fail "--help advertises --plain" "missing --plain in help output"
    fi

    # 2. --version under NO_COLOR=1.
    local no_color_ver; no_color_ver=$(NO_COLOR=1 "$BIN" --version 2>&1)
    _assert_clean "NO_COLOR=1 --version" "$no_color_ver"

    # 3. --help under TERM=dumb.
    local dumb_help; dumb_help=$(TERM=dumb "$BIN" --help 2>&1)
    _assert_clean "TERM=dumb --help" "$dumb_help"

    # 4. --help under AZ_AI_PLAIN=1.
    local plain_env_help; plain_env_help=$(AZ_AI_PLAIN=1 "$BIN" --help 2>&1)
    _assert_clean "AZ_AI_PLAIN=1 --help" "$plain_env_help"

    # 5. Banner has no em-dash / arrow / mask glyph anywhere.
    if printf '%s' "$plain_help" | LC_ALL=C grep -qE $'\xE2\x80\x94|\xE2\x86\x92|\xE2\x80\xA2|\xE2\x9C\x93'; then
        fail "--help contains no unicode glyphs" "found em-dash / arrow / bullet / check"
    else
        pass "--help contains no unicode glyphs"
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

    # ── S03E07 The Redactor (Newman): bearer tokens / api-keys must not
    #    leak through error stderr. Smoke-test the centralised
    #    SecretRedactor by triggering a "task file not found" error whose
    #    path contains an Authorization: Bearer header. The path appears
    #    in the [ERROR] line, so the redactor MUST scrub it.
    echo ""
    echo "▸ S03E07 Redactor smoke (ADR-007 section 2)"
    local redact_bin; redact_bin=$(cd "$(dirname "$BIN")" && pwd)/$(basename "$BIN")
    local redact_path="/tmp/Authorization: Bearer SECRET-NEWMAN-9999"
    local redact_stderr redact_rc
    set +e
    redact_stderr=$(env HOME="$test_home" \
        AZUREOPENAIENDPOINT="https://example.invalid/" \
        AZUREOPENAIAPI="dummy-key-not-used" \
        AZUREOPENAIMODEL="gpt-4" \
        "$redact_bin" --task-file "$redact_path" "hi" 2>&1 1>/dev/null)
    redact_rc=$?
    set -e

    if [ "$redact_rc" -eq 1 ]; then
        pass "S03E07 redactor: error path exits 1"
    else
        fail "S03E07 redactor: error path exits 1" "got exit $redact_rc; stderr: $redact_stderr"
    fi

    # P1: stderr must NOT contain a literal bearer token.
    if printf '%s' "$redact_stderr" | grep -qE 'Bearer[[:space:]]+[A-Za-z0-9._-]'; then
        fail "S03E07 redactor: no bearer token in stderr" "stderr leaked: $redact_stderr"
    else
        pass "S03E07 redactor: no bearer token in stderr"
    fi

    # P1: stderr must NOT contain a raw 'api-key:' header value pair.
    if printf '%s' "$redact_stderr" | grep -qiE 'api-key:[[:space:]]+[A-Za-z0-9._-]'; then
        fail "S03E07 redactor: no api-key header in stderr" "stderr leaked: $redact_stderr"
    else
        pass "S03E07 redactor: no api-key header in stderr"
    fi

    # Positive: the [REDACTED:bearer] tag must be present.
    if printf '%s' "$redact_stderr" | grep -qF '[REDACTED:bearer]'; then
        pass "S03E07 redactor: [REDACTED:bearer] tag present"
    else
        fail "S03E07 redactor: [REDACTED:bearer] tag present" "stderr: $redact_stderr"
    fi

    # ── S03E10 -- The Keychain (per-provider env sections) ───────────────
    echo ""
    echo "▸ S03E10 -- per-provider credential sections"
    local keychain_home; keychain_home=$(mktemp -d)
    mkdir -p "$keychain_home/.config/az-ai"
    cat > "$keychain_home/.config/az-ai/env" <<'KCEOF'
# Default section -- back-compat shell-export form.
export AZUREOPENAIENDPOINT="https://kc-azure.example.com/"
export AZUREOPENAIMODEL="gpt-4o-mini"

[provider:openai]
API_KEY=sk-keychain-test-secret-OPENAI

[provider:groq]
API_KEY=gsk_keychain-test-secret-GROQ
KCEOF
    chmod 600 "$keychain_home/.config/az-ai/env"

    # `--config show` is the only mode that exercises the loader without
    # needing real credentials. It must succeed and the resolved Azure
    # endpoint must come through from the default section.
    local kc_out kc_rc
    set +e
    kc_out=$(env -i HOME="$keychain_home" PATH="$PATH" \
        AZUREOPENAIAPI="dummy-shell-key" \
        "$BIN" --config show 2>&1)
    kc_rc=$?
    set -e

    if [ "$kc_rc" -eq 0 ]; then
        pass "S03E10 keychain: --config show exits 0 with [provider:*] sections"
    else
        fail "S03E10 keychain: --config show exits 0" "rc=$kc_rc out=$kc_out"
    fi

    # Default-section endpoint must be picked up.
    if printf '%s' "$kc_out" | grep -qF 'kc-azure.example.com'; then
        pass "S03E10 keychain: default section endpoint loaded"
    else
        fail "S03E10 keychain: default section endpoint loaded" "out=$kc_out"
    fi

    # Headline Newman invariant: the OpenAI section's secret value must
    # NEVER appear in any --config show output (the binary never prints
    # secrets; this asserts the loader didn't leak it into a printed slot).
    if printf '%s' "$kc_out" | grep -qF 'sk-keychain-test-secret-OPENAI'; then
        fail "S03E10 keychain: OPENAI_API_KEY value not leaked" "secret leaked into --config show: $kc_out"
    else
        pass "S03E10 keychain: OPENAI_API_KEY value not leaked"
    fi
    if printf '%s' "$kc_out" | grep -qF 'gsk_keychain-test-secret-GROQ'; then
        fail "S03E10 keychain: GROQ_API_KEY value not leaked" "secret leaked into --config show: $kc_out"
    else
        pass "S03E10 keychain: GROQ_API_KEY value not leaked"
    fi

    # Unknown provider section warns to stderr but does not abort.
    cat > "$keychain_home/.config/az-ai/env" <<'KCEOF2'
[provider:bogus-not-a-provider]
API_KEY=should-be-skipped
KCEOF2
    chmod 600 "$keychain_home/.config/az-ai/env"
    # Use --config show (not --version) because --help/--version short-circuit
    # before LoadConfigEnv runs.
    local kc2_stderr kc2_rc
    set +e
    kc2_stderr=$(env -i HOME="$keychain_home" PATH="$PATH" \
        DOTNET_ROOT="${DOTNET_ROOT:-}" "$BIN" --config show 2>&1 1>/dev/null)
    kc2_rc=$?
    set -e
    if [ "$kc2_rc" -eq 0 ]; then
        pass "S03E10 keychain: unknown section does not abort startup"
    else
        fail "S03E10 keychain: unknown section does not abort" "rc=$kc2_rc stderr=$kc2_stderr"
    fi
    if printf '%s' "$kc2_stderr" | grep -qF '[WARNING]'; then
        pass "S03E10 keychain: unknown section warns to stderr"
    else
        fail "S03E10 keychain: unknown section warns to stderr" "stderr=$kc2_stderr"
    fi

    # --raw must silence the warning.
    local kc3_stderr
    set +e
    kc3_stderr=$(env -i HOME="$keychain_home" PATH="$PATH" \
        DOTNET_ROOT="${DOTNET_ROOT:-}" "$BIN" --raw --config show 2>&1 1>/dev/null)
    set -e
    if printf '%s' "$kc3_stderr" | grep -qF '[WARNING]'; then
        fail "S03E10 keychain: --raw silences unknown-section warning" "stderr leaked under --raw: $kc3_stderr"
    else
        pass "S03E10 keychain: --raw silences unknown-section warning"
    fi

    rm -rf "$keychain_home"

    # ── S03E11 -- The Wizard, Reprise (provider-aware setup) ─────────────
    echo ""
    echo "▸ S03E11 -- provider-aware setup wizard"

    # Non-TTY refusal: piping answers without a PTY must fail loudly,
    # not loop on closed stdin. The wizard prints an [ERROR] and exits 1.
    local wiz_rc wiz_out
    set +e
    wiz_out=$(echo "" | "$BIN" --setup 2>&1)
    wiz_rc=$?
    set -e
    if [ "$wiz_rc" -eq 1 ] && printf '%s' "$wiz_out" | grep -qF '[ERROR]'; then
        pass "S03E11 wizard: non-TTY refuses with [ERROR] (rc=1)"
    else
        fail "S03E11 wizard: non-TTY refuses with [ERROR]" "rc=$wiz_rc out=$wiz_out"
    fi

    # PTY-driven happy path: feed answers via `script` (util-linux). Skip
    # gracefully if `script` isn't on the box -- macOS runners can have a
    # different flavour.
    if ! command -v script >/dev/null 2>&1; then
        skip "S03E11 wizard: PTY-driven setup" "script(1) not available"
    else
        local wiz_home; wiz_home=$(mktemp -d)
        local answers_file; answers_file=$(mktemp)
        # Answer sequence: provider menu (Enter = openai default) -> api key
        # (typed char-by-char into ReadKey) -> model list (Enter = default)
        # -> "no more providers" (Enter = N).
        printf '%s\n%s\n%s\nN\n' \
            '' \
            'sk-test-0123456789abcdef' \
            'gpt-4o-mini' \
            > "$answers_file"

        local script_log; script_log=$(mktemp)
        set +e
        env -i HOME="$wiz_home" PATH="$PATH" TERM=dumb \
            DOTNET_ROOT="${DOTNET_ROOT:-}" \
            script -qec "$BIN --setup" "$script_log" < "$answers_file" >/dev/null 2>&1
        local script_rc=$?
        set -e

        local env_out="$wiz_home/.config/az-ai/env"
        if [ "$script_rc" -eq 0 ] && [ -f "$env_out" ]; then
            pass "S03E11 wizard: PTY-driven --setup writes env file"
        else
            fail "S03E11 wizard: PTY-driven --setup writes env file" \
                "rc=$script_rc env_out_present=$([ -f "$env_out" ] && echo y || echo n) log=$(cat "$script_log" 2>/dev/null | tr -d '\r' | tail -20)"
        fi

        if [ -f "$env_out" ]; then
            if grep -qF '[provider:openai]' "$env_out"; then
                pass "S03E11 wizard: env file has [provider:openai] section"
            else
                fail "S03E11 wizard: env file has [provider:openai] section" \
                    "contents: $(cat "$env_out")"
            fi
            if grep -qF 'AZ_AI_COMPAT_MODELS' "$env_out"; then
                pass "S03E11 wizard: env file exports AZ_AI_COMPAT_MODELS"
            else
                fail "S03E11 wizard: env file exports AZ_AI_COMPAT_MODELS" \
                    "contents: $(cat "$env_out")"
            fi
            # chmod 600 -- the headline security invariant.
            local perms; perms=$(stat -c '%a' "$env_out" 2>/dev/null || stat -f '%Lp' "$env_out" 2>/dev/null)
            if [ "$perms" = "600" ]; then
                pass "S03E11 wizard: env file is mode 0600"
            else
                fail "S03E11 wizard: env file is mode 0600" "actual: $perms"
            fi
        fi

        rm -rf "$wiz_home" "$answers_file" "$script_log"
    fi

    # ── S03E13 -- The Telemetry (Frank Costanza opt-in observability) ─────
    echo ""
    echo "▸ S03E13 -- opt-in telemetry (AZ_AI_TELEMETRY=1)"

    # Drive the dispatch path with bogus credentials so it fails predictably
    # (no real API call). The dispatch try/catch lands in catch (Exception),
    # sets outcome=unknown_error, and the finally emits the structured event
    # to stderr. Stdout is irrelevant for these assertions.
    local tel_home; tel_home=$(mktemp -d)
    local tel_stderr_on tel_stderr_off

    set +e
    tel_stderr_on=$(env -i HOME="$tel_home" PATH="$PATH" \
        DOTNET_ROOT="${DOTNET_ROOT:-}" \
        AZ_AI_TELEMETRY=1 \
        AZUREOPENAIENDPOINT="https://invalid.example.invalid/" \
        AZUREOPENAIAPI="sk-not-a-real-key" \
        AZUREOPENAIMODEL="gpt-4o-mini" \
        "$BIN" --raw "S03E13 telemetry probe" 2>&1 1>/dev/null)
    set -e

    # Extract the single line that looks like a telemetry event (starts with
    # '{"event_id":'). There must be exactly one. Other stderr lines (errors,
    # warnings) are tolerated -- the assertion is on the telemetry line.
    local tel_lines
    tel_lines=$(printf '%s\n' "$tel_stderr_on" | grep -c '^{"event_id":' || true)
    if [ "$tel_lines" = "1" ]; then
        pass "S03E13 telemetry: emits exactly one event line on stderr when AZ_AI_TELEMETRY=1"
    else
        fail "S03E13 telemetry: emits exactly one event line on stderr when AZ_AI_TELEMETRY=1" \
            "expected 1 event line, got $tel_lines. stderr: $tel_stderr_on"
    fi

    local tel_event
    tel_event=$(printf '%s\n' "$tel_stderr_on" | grep '^{"event_id":' | head -n1)
    local f
    local missing=""
    for f in '"event_id"' '"ts"' '"model"' '"provider"' '"dispatch_path"' '"latency_ms_bucket"' '"outcome"' '"error_class"'; do
        if ! printf '%s' "$tel_event" | grep -qF "$f"; then
            missing="$missing $f"
        fi
    done
    if [ -z "$missing" ]; then
        pass "S03E13 telemetry: event has all expected schema fields"
    else
        fail "S03E13 telemetry: event has all expected schema fields" "missing:$missing event=$tel_event"
    fi

    # Privacy guarantee: bogus key value MUST NOT appear in the event line.
    if printf '%s' "$tel_event" | grep -qF 'sk-not-a-real-key'; then
        fail "S03E13 telemetry: event must not leak API key" "leaked: $tel_event"
    else
        pass "S03E13 telemetry: event does not leak API key"
    fi
    # Endpoint hostname likewise must not appear in the event payload.
    if printf '%s' "$tel_event" | grep -qF 'invalid.example.invalid'; then
        fail "S03E13 telemetry: event must not leak endpoint" "leaked: $tel_event"
    else
        pass "S03E13 telemetry: event does not leak endpoint"
    fi

    # Negative: without AZ_AI_TELEMETRY=1, no telemetry line on stderr.
    set +e
    tel_stderr_off=$(env -i HOME="$tel_home" PATH="$PATH" \
        DOTNET_ROOT="${DOTNET_ROOT:-}" \
        AZUREOPENAIENDPOINT="https://invalid.example.invalid/" \
        AZUREOPENAIAPI="sk-not-a-real-key" \
        AZUREOPENAIMODEL="gpt-4o-mini" \
        "$BIN" --raw "S03E13 telemetry probe" 2>&1 1>/dev/null)
    set -e
    if printf '%s\n' "$tel_stderr_off" | grep -q '^{"event_id":'; then
        fail "S03E13 telemetry: default off (env unset) emits no event" "leaked: $tel_stderr_off"
    else
        pass "S03E13 telemetry: default off (env unset) emits no event"
    fi

    # Negative: AZ_AI_TELEMETRY=0 does not enable.
    set +e
    local tel_zero
    tel_zero=$(env -i HOME="$tel_home" PATH="$PATH" \
        DOTNET_ROOT="${DOTNET_ROOT:-}" \
        AZ_AI_TELEMETRY=0 \
        AZUREOPENAIENDPOINT="https://invalid.example.invalid/" \
        AZUREOPENAIAPI="sk-not-a-real-key" \
        AZUREOPENAIMODEL="gpt-4o-mini" \
        "$BIN" --raw "S03E13 telemetry probe" 2>&1 1>/dev/null)
    set -e
    if printf '%s\n' "$tel_zero" | grep -q '^{"event_id":'; then
        fail "S03E13 telemetry: AZ_AI_TELEMETRY=0 must not enable" "leaked: $tel_zero"
    else
        pass "S03E13 telemetry: AZ_AI_TELEMETRY=0 does not enable (strict-equality '1')"
    fi

    rm -rf "$tel_home"

    # -- S03E15 The Probe: az-ai --doctor ----------------------------------
    local doc_home; doc_home=$(mktemp -d)

    # 1. With NO providers configured (hermetic empty HOME, no env), --doctor exits 0.
    set +e
    env -i HOME="$doc_home" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" \
        "$BIN" --doctor >/dev/null 2>&1
    local doc_rc=$?
    set -e
    if [ "$doc_rc" -eq 0 ]; then
        pass "S03E15 doctor: no providers configured exits 0"
    else
        fail "S03E15 doctor: no providers configured" "expected exit 0, got $doc_rc"
    fi

    # 2. --doctor --json emits valid JSON with required keys.
    set +e
    local doc_json
    doc_json=$(env -i HOME="$doc_home" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" \
        "$BIN" --doctor --json 2>/dev/null)
    set -e
    if printf '%s' "$doc_json" | python3 -c \
        "import json,sys; d=json.load(sys.stdin); assert 'providers' in d and 'all_healthy' in d" \
        >/dev/null 2>&1; then
        pass "S03E15 doctor: --json emits valid schema"
    else
        fail "S03E15 doctor: --json schema" "got: $(printf '%s' "$doc_json" | head -c 200)"
    fi

    # 3. --doctor output never contains obvious-secret-shape (Bearer / sk-...).
    set +e
    local doc_out
    doc_out=$(env -i HOME="$doc_home" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" \
        AZUREOPENAIENDPOINT="https://invalid.example.invalid/" \
        AZUREOPENAIAPI="sk-not-a-real-key-12345" \
        AZUREOPENAIMODEL="gpt-4o-mini" \
        "$BIN" --doctor 2>&1 || true)
    set -e
    if printf '%s' "$doc_out" | grep -Eq 'Bearer [A-Za-z0-9._-]+|sk-[A-Za-z0-9]{8,}'; then
        fail "S03E15 doctor: secret-shape leak" "found Bearer/sk- pattern in output"
    else
        pass "S03E15 doctor: never emits Bearer/sk- secret shape"
    fi

    rm -rf "$doc_home"

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
