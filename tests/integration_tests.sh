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

    # ── S03E25 -- The Rotation (Newman BYOK rotation flow) ───────────────
    echo ""
    echo "▸ S03E25 -- creds rotate (--rotate-creds)"

    # 1. Non-TTY refusal: piping stdin without a PTY must exit 3 with
    #    [ERROR], not block on a prompt.
    local rot_rc rot_out
    set +e
    rot_out=$(echo "" | "$BIN" --rotate-creds openai 2>&1)
    rot_rc=$?
    set -e
    if [ "$rot_rc" -eq 3 ] && printf '%s' "$rot_out" | grep -qF '[ERROR]'; then
        pass "S03E25 rotate: non-TTY refuses with [ERROR] (rc=3)"
    else
        fail "S03E25 rotate: non-TTY refuses with [ERROR]" "rc=$rot_rc out=$rot_out"
    fi

    # 2-5. PTY-driven happy path. Pre-seed the env file with a known old
    #     key, drive the prompts via `script`, then assert: rewritten key,
    #     backup with old key, mode 0600 on both, key not in --doctor.
    if ! command -v script >/dev/null 2>&1; then
        skip "S03E25 rotate: PTY-driven --rotate-creds" "script(1) not available"
    else
        local rot_home; rot_home=$(mktemp -d)
        mkdir -p "$rot_home/.config/az-ai"
        local rot_env="$rot_home/.config/az-ai/env"
        cat > "$rot_env" <<'ROTEOF'
# az-ai env file
export AZ_AI_COMPAT_MODELS="openai:gpt-4o-mini"

[provider:openai]
API_KEY=sk-old-rotation-test-0123456789
ROTEOF
        chmod 600 "$rot_env"

        local rot_answers; rot_answers=$(mktemp)
        # Sequence: new key (typed char-by-char into ReadKey), then "y".
        printf '%s\ny\n' 'sk-new-rotation-test-fedcba9876' > "$rot_answers"

        local rot_log; rot_log=$(mktemp)
        set +e
        env -i HOME="$rot_home" PATH="$PATH" TERM=dumb \
            DOTNET_ROOT="${DOTNET_ROOT:-}" \
            script -qec "$BIN --rotate-creds openai" "$rot_log" < "$rot_answers" >/dev/null 2>&1
        local rot_pty_rc=$?
        set -e

        if [ "$rot_pty_rc" -eq 0 ] && grep -qF 'API_KEY=sk-new-rotation-test-fedcba9876' "$rot_env"; then
            pass "S03E25 rotate: PTY-driven rotate rewrites the API key"
        else
            fail "S03E25 rotate: PTY-driven rotate rewrites the API key" \
                "rc=$rot_pty_rc contents=$(cat "$rot_env")"
        fi

        local rot_backup
        rot_backup=$(ls -1 "$rot_home/.config/az-ai/"env.bak.* 2>/dev/null | head -1 || true)
        if [ -n "$rot_backup" ] && grep -qF 'sk-old-rotation-test' "$rot_backup"; then
            pass "S03E25 rotate: backup file contains the OLD key"
        else
            fail "S03E25 rotate: backup file contains the OLD key" \
                "backup=$rot_backup"
        fi

        local rot_mode rot_bak_mode
        rot_mode=$(stat -c '%a' "$rot_env" 2>/dev/null || stat -f '%Lp' "$rot_env" 2>/dev/null)
        if [ "$rot_mode" = "600" ]; then
            pass "S03E25 rotate: rewritten env file is mode 0600"
        else
            fail "S03E25 rotate: rewritten env file is mode 0600" "actual: $rot_mode"
        fi
        if [ -n "$rot_backup" ]; then
            rot_bak_mode=$(stat -c '%a' "$rot_backup" 2>/dev/null || stat -f '%Lp' "$rot_backup" 2>/dev/null)
            if [ "$rot_bak_mode" = "600" ]; then
                pass "S03E25 rotate: backup file is mode 0600"
            else
                fail "S03E25 rotate: backup file is mode 0600" "actual: $rot_bak_mode"
            fi
        fi

        # The new key must NEVER appear in --doctor output (Newman H-2).
        local doc_out
        set +e
        doc_out=$(env -i HOME="$rot_home" PATH="$PATH" \
            DOTNET_ROOT="${DOTNET_ROOT:-}" \
            "$BIN" --doctor 2>&1 || true)
        set -e
        if printf '%s' "$doc_out" | grep -qF 'sk-new-rotation-test'; then
            fail "S03E25 rotate: --doctor does not echo the rotated key" \
                "leaked output: $doc_out"
        else
            pass "S03E25 rotate: --doctor does not echo the rotated key"
        fi

        rm -rf "$rot_home" "$rot_answers" "$rot_log"
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

    # -- S03E26 The Offline Mode: --offline forbids non-loopback ----------
    local off_home; off_home=$(mktemp -d)

    # 1. --offline --help exits 0 (parser accepts the flag).
    set +e
    env -i HOME="$off_home" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" \
        "$BIN" --offline --help >/dev/null 2>&1
    local off_help_rc=$?
    set -e
    if [ "$off_help_rc" -eq 0 ]; then
        pass "S03E26 offline: --offline --help exits 0"
    else
        fail "S03E26 offline: --offline --help" "expected exit 0, got $off_help_rc"
    fi

    # 2. --offline --doctor with NO providers configured exits 0.
    set +e
    env -i HOME="$off_home" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" \
        "$BIN" --offline --doctor >/dev/null 2>&1
    local off_empty_rc=$?
    set -e
    if [ "$off_empty_rc" -eq 0 ]; then
        pass "S03E26 offline: --offline --doctor (no providers) exits 0"
    else
        fail "S03E26 offline: --offline --doctor (no providers)" "expected exit 0, got $off_empty_rc"
    fi

    # 3. --offline --doctor with Azure provider env -> exits 1 and reports
    #    blocked-offline in the dns column.
    set +e
    local off_doctor_out
    off_doctor_out=$(env -i HOME="$off_home" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" \
        AZUREOPENAIENDPOINT="https://invalid.example.invalid/" \
        AZUREOPENAIAPI="sk-not-a-real-key-12345" \
        AZUREOPENAIMODEL="gpt-4o-mini" \
        "$BIN" --offline --doctor 2>&1)
    local off_doctor_rc=$?
    set -e
    if [ "$off_doctor_rc" -ne 0 ] && \
       printf '%s' "$off_doctor_out" | grep -q 'blocked-offline'; then
        pass "S03E26 offline: --offline --doctor reports blocked-offline (rc=$off_doctor_rc)"
    else
        fail "S03E26 offline: --offline --doctor blocked-offline" \
            "rc=$off_doctor_rc out=$(printf '%s' "$off_doctor_out" | head -c 240)"
    fi

    # 4. AZ_AI_OFFLINE=1 env (no flag) acts identically to --offline.
    set +e
    local off_env_out
    off_env_out=$(env -i HOME="$off_home" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" \
        AZ_AI_OFFLINE=1 \
        AZUREOPENAIENDPOINT="https://invalid.example.invalid/" \
        AZUREOPENAIAPI="sk-not-a-real-key-12345" \
        AZUREOPENAIMODEL="gpt-4o-mini" \
        "$BIN" --doctor 2>&1)
    local off_env_rc=$?
    set -e
    if [ "$off_env_rc" -ne 0 ] && \
       printf '%s' "$off_env_out" | grep -q 'blocked-offline'; then
        pass "S03E26 offline: AZ_AI_OFFLINE=1 env (no flag) gates same as --offline"
    else
        fail "S03E26 offline: AZ_AI_OFFLINE=1 env" \
            "rc=$off_env_rc out=$(printf '%s' "$off_env_out" | head -c 240)"
    fi

    # 5. --offline --doctor --json shows blocked-offline in the dns field.
    set +e
    local off_json
    off_json=$(env -i HOME="$off_home" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" \
        AZUREOPENAIENDPOINT="https://invalid.example.invalid/" \
        AZUREOPENAIAPI="sk-not-a-real-key-12345" \
        AZUREOPENAIMODEL="gpt-4o-mini" \
        "$BIN" --offline --doctor --json 2>/dev/null)
    set -e
    if printf '%s' "$off_json" | python3 -c \
        "import json,sys; d=json.load(sys.stdin); \
         assert any(p.get('dns')=='blocked-offline' for p in d.get('providers',[])), 'no blocked-offline row'; \
         assert d.get('all_healthy') is False" \
        >/dev/null 2>&1; then
        pass "S03E26 offline: --offline --doctor --json emits blocked-offline + all_healthy=false"
    else
        fail "S03E26 offline: --offline --doctor --json" \
            "got: $(printf '%s' "$off_json" | head -c 240)"
    fi

    # 6. AZ_AI_OFFLINE=true (non-strict) does NOT activate offline mode
    #    (strict-equality "1" only, mirrors AZ_AI_TELEMETRY / AZ_AI_LOCAL_PROVIDERS).
    set +e
    local off_lax_out
    off_lax_out=$(env -i HOME="$off_home" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" \
        AZ_AI_OFFLINE=true \
        AZUREOPENAIENDPOINT="https://invalid.example.invalid/" \
        AZUREOPENAIAPI="sk-not-a-real-key-12345" \
        AZUREOPENAIMODEL="gpt-4o-mini" \
        "$BIN" --doctor 2>&1)
    set -e
    if printf '%s' "$off_lax_out" | grep -q 'blocked-offline'; then
        fail "S03E26 offline: AZ_AI_OFFLINE=true must NOT enable offline" \
            "lax env activated offline gate (strict-equality '1' is required)"
    else
        pass "S03E26 offline: AZ_AI_OFFLINE=true does not enable (strict-equality '1' only)"
    fi

    # 7. Secret-shape leak guard: offline error path must not emit the
    #    AZUREOPENAIAPI value or any sk-... token.
    if printf '%s' "$off_doctor_out" | grep -Eq 'Bearer [A-Za-z0-9._-]+|sk-[A-Za-z0-9]{8,}'; then
        fail "S03E26 offline: secret-shape leak in offline path" \
            "found Bearer/sk- pattern in --offline --doctor output"
    else
        pass "S03E26 offline: --offline --doctor emits no Bearer/sk- secret shape"
    fi

    rm -rf "$off_home"

    # -- S03E18 The Capability Gate: refuse incompatible requests early ----
    local cg_home; cg_home=$(mktemp -d)

    # 1. Baseline: --help exits 0 even with capability env set (gate is
    #    dispatch-side, not parser-side).
    set +e
    env -i HOME="$cg_home" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" \
        AZ_AI_CAPABILITY_OVERRIDES="bogus-but-ignored" \
        "$BIN" --help >/dev/null 2>&1
    local cg_help_rc=$?
    set -e
    if [ "$cg_help_rc" -eq 0 ]; then
        pass "S03E18 capability gate: --help unaffected by AZ_AI_CAPABILITY_OVERRIDES"
    else
        fail "S03E18 capability gate: --help" "expected 0, got $cg_help_rc"
    fi

    # 2. Tool-call request to a Groq model that does NOT support tool-calls
    #    (llama-3.1-8b-instant) -> exit 2 + friendly CapabilityMismatch.
    set +e
    local cg_tool_out
    cg_tool_out=$(env -i HOME="$cg_home" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" \
        AZUREOPENAIENDPOINT="https://invalid.example.invalid/" \
        AZUREOPENAIAPI="sk-not-real-1234" \
        AZUREOPENAIMODEL="llama-3.1-8b-instant" \
        AZ_AI_COMPAT_MODELS="groq:llama-3.1-8b-instant" \
        GROQ_API_KEY="sk-not-real-groq" \
        "$BIN" --agent --raw "hello" 2>&1)
    local cg_tool_rc=$?
    set -e
    if [ "$cg_tool_rc" -eq 2 ] && \
       printf '%s' "$cg_tool_out" | grep -q 'does not support tool_calls'; then
        pass "S03E18 capability gate: tool-call gate fires on groq:llama-3.1-8b-instant (exit 2)"
    else
        fail "S03E18 capability gate: tool-call refusal" \
            "rc=$cg_tool_rc out=$(printf '%s' "$cg_tool_out" | head -c 240)"
    fi

    # 3. Override env flips the bit -> request proceeds past the gate (the
    #    subsequent network call to invalid.example.invalid will fail, but
    #    NOT with exit 2 / CapabilityMismatch).
    set +e
    local cg_ovr_out
    cg_ovr_out=$(env -i HOME="$cg_home" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" \
        AZUREOPENAIENDPOINT="https://invalid.example.invalid/" \
        AZUREOPENAIAPI="sk-not-real-1234" \
        AZUREOPENAIMODEL="llama-3.1-8b-instant" \
        AZ_AI_COMPAT_MODELS="groq:llama-3.1-8b-instant" \
        AZ_AI_CAPABILITY_OVERRIDES="groq:llama-3.1-8b-instant:tool_calls=true" \
        GROQ_API_KEY="sk-not-real-groq" \
        "$BIN" --agent --raw "hello" 2>&1)
    local cg_ovr_rc=$?
    set -e
    if [ "$cg_ovr_rc" -ne 2 ] && \
       ! printf '%s' "$cg_ovr_out" | grep -q 'does not support tool_calls'; then
        pass "S03E18 capability gate: AZ_AI_CAPABILITY_OVERRIDES flips tool_calls past gate"
    else
        fail "S03E18 capability gate: override path" \
            "rc=$cg_ovr_rc out=$(printf '%s' "$cg_ovr_out" | head -c 240)"
    fi

    # 4. Error message names the override env var so the user can self-rescue.
    if printf '%s' "$cg_tool_out" | grep -q 'AZ_AI_CAPABILITY_OVERRIDES'; then
        pass "S03E18 capability gate: error message names AZ_AI_CAPABILITY_OVERRIDES"
    else
        fail "S03E18 capability gate: actionable hint" \
            "tool-call refusal did not mention AZ_AI_CAPABILITY_OVERRIDES"
    fi

    # 5. Capability-gate refusal must NOT leak the API key.
    if printf '%s' "$cg_tool_out" | grep -Eq 'Bearer [A-Za-z0-9._-]+|sk-not-real'; then
        fail "S03E18 capability gate: secret-shape leak" "key value appears in refusal output"
    else
        pass "S03E18 capability gate: refusal emits no Bearer/sk- key value"
    fi

    rm -rf "$cg_home"

    # ── S03E20 -- The Switch (Costanza) ───────────────────────────────────
    echo ""
    echo "▸ S03E20 -- The Switch: precedence chain"

    # Build a sandbox HOME with a curated preferences.json so the resolver
    # has profiles to consult. The file lives at
    # ${XDG_CONFIG_HOME}/az-ai/preferences.json on Linux/macOS; Preferences
    # honors XDG_CONFIG_HOME first, falls back to $HOME/.config.
    local sw_home
    sw_home=$(mktemp -d "${TMPDIR:-/tmp}/az-ai-switch.XXXXXX")
    mkdir -p "$sw_home/.config/az-ai"
    cat > "$sw_home/.config/az-ai/preferences.json" <<'SWEOF'
{
  "schema": "1",
  "providers": {
    "azure": {"endpoint": "https://x.cognitiveservices.azure.com/"},
    "groq":  {}
  },
  "profiles": {
    "work": {"provider": "azure", "model": "gpt-4o-pinned"},
    "ci":   {"provider": "groq",  "model": "llama-3.1-pinned"}
  }
}
SWEOF
    chmod 600 "$sw_home/.config/az-ai/preferences.json"

    # 1. --config show prints the new "Switch resolution (S03E20)" block
    #    with a source field. Provided AZUREOPENAIENDPOINT so default
    #    heuristic resolves to azure.
    local sw_show_out sw_show_rc
    set +e
    sw_show_out=$(env -i HOME="$sw_home" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" \
        AZUREOPENAIENDPOINT="https://x.cognitiveservices.azure.com/" \
        AZUREOPENAIAPI="sk-not-real" \
        AZUREOPENAIMODEL="gpt-4o-mini" \
        "$BIN" --config show 2>&1)
    sw_show_rc=$?
    set -e
    if [ "$sw_show_rc" -eq 0 ] && \
       printf '%s' "$sw_show_out" | grep -qF 'Switch resolution (S03E20):' && \
       printf '%s' "$sw_show_out" | grep -qE '^\s*source:\s+'; then
        pass "S03E20 switch: --config show emits Switch resolution + source field"
    else
        fail "S03E20 switch: --config show source field" "rc=$sw_show_rc out=$(printf '%s' "$sw_show_out" | head -c 320)"
    fi

    # 2. --provider azure overrides AZ_PROVIDER=groq -> source 'cli'.
    local sw_cli_out
    set +e
    sw_cli_out=$(env -i HOME="$sw_home" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" \
        AZUREOPENAIENDPOINT="https://x.cognitiveservices.azure.com/" \
        AZUREOPENAIAPI="sk-not-real" \
        AZUREOPENAIMODEL="gpt-4o-mini" \
        AZ_PROVIDER="groq" \
        "$BIN" --provider azure --config show 2>&1)
    set -e
    if printf '%s' "$sw_cli_out" | grep -qE 'provider source:\s+cli'; then
        pass "S03E20 switch: --provider beats AZ_PROVIDER (provider source = cli)"
    else
        fail "S03E20 switch: --provider precedence" "out=$(printf '%s' "$sw_cli_out" | head -c 320)"
    fi

    # 3. --profile work chains to the profile's provider (azure) and model.
    local sw_prof_out
    set +e
    sw_prof_out=$(env -i HOME="$sw_home" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" \
        AZUREOPENAIENDPOINT="https://x.cognitiveservices.azure.com/" \
        AZUREOPENAIAPI="sk-not-real" \
        AZUREOPENAIMODEL="gpt-4o-mini" \
        "$BIN" --profile work --config show 2>&1)
    set -e
    if printf '%s' "$sw_prof_out" | grep -qF 'profile:work:provider' && \
       printf '%s' "$sw_prof_out" | grep -qF 'profile:work:model'; then
        pass "S03E20 switch: --profile chains to profile provider + model"
    else
        fail "S03E20 switch: --profile chain" "out=$(printf '%s' "$sw_prof_out" | head -c 320)"
    fi

    # 4. Missing profile errors with the available list (work, ci).
    local sw_miss_out sw_miss_rc
    set +e
    sw_miss_out=$(env -i HOME="$sw_home" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" \
        AZUREOPENAIENDPOINT="https://x.cognitiveservices.azure.com/" \
        AZUREOPENAIAPI="sk-not-real" \
        AZUREOPENAIMODEL="gpt-4o-mini" \
        "$BIN" --profile production --raw "hi" 2>&1)
    sw_miss_rc=$?
    set -e
    if [ "$sw_miss_rc" -ne 0 ] && \
       printf '%s' "$sw_miss_out" | grep -qF "'production'" && \
       printf '%s' "$sw_miss_out" | grep -qF 'Available profiles:' && \
       printf '%s' "$sw_miss_out" | grep -q 'work' && \
       printf '%s' "$sw_miss_out" | grep -q 'ci'; then
        pass "S03E20 switch: missing profile lists available names"
    else
        fail "S03E20 switch: missing profile message" "rc=$sw_miss_rc out=$(printf '%s' "$sw_miss_out" | head -c 320)"
    fi

    # 5. --provider, --profile, --model appear in --help.
    local sw_help_out
    sw_help_out=$("$BIN" --help 2>&1)
    if printf '%s' "$sw_help_out" | grep -qFe '--provider <name>' && \
       printf '%s' "$sw_help_out" | grep -qFe '--profile <name>'; then
        pass "S03E20 switch: --help documents --provider and --profile"
    else
        fail "S03E20 switch: --help doc" "help missing --provider/--profile"
    fi

    # 6. AZ_PROFILE env routes to the profile's provider when no --profile.
    local sw_env_out
    set +e
    sw_env_out=$(env -i HOME="$sw_home" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" \
        AZUREOPENAIENDPOINT="https://x.cognitiveservices.azure.com/" \
        AZUREOPENAIAPI="sk-not-real" \
        AZUREOPENAIMODEL="gpt-4o-mini" \
        AZ_PROFILE="ci" \
        "$BIN" --config show 2>&1)
    set -e
    if printf '%s' "$sw_env_out" | grep -qF 'profile:ci:provider' && \
       printf '%s' "$sw_env_out" | grep -qF 'env:AZ_PROFILE'; then
        pass "S03E20 switch: AZ_PROFILE env resolves to profile.provider"
    else
        fail "S03E20 switch: AZ_PROFILE env chain" "out=$(printf '%s' "$sw_env_out" | head -c 320)"
    fi

    rm -rf "$sw_home"

    # ── S03E22 -- The Default (file slot 22): heuristic ladder ─────────────
    echo ""
    echo "▸ S03E22 -- The Default: six-rung heuristic (ADR-011)"

    # All assertions in this block run with a clean HOME so no
    # preferences.json shadows the resolver. We toggle env vars to walk
    # the ladder rung-by-rung and read the source label from --config show.
    local def_home
    def_home=$(mktemp -d "${TMPDIR:-/tmp}/az-ai-default.XXXXXX")
    mkdir -p "$def_home/.config/az-ai"

    # 1. Rung 1: AZUREOPENAIENDPOINT + AZUREOPENAIAPI → default:azure
    local def1_out
    def1_out=$(env -i HOME="$def_home" PATH="$PATH" \
                XDG_CONFIG_HOME="$def_home/.config" \
                AZUREOPENAIENDPOINT="https://x.cognitiveservices.azure.com/" \
                AZUREOPENAIAPI="not-a-real-key" \
                "$BIN" --config show 2>&1 || true)
    if printf '%s' "$def1_out" | grep -qE 'provider source:[[:space:]]+default:azure(\b|$)'; then
        pass "S03E22 default: rung 1 (azure endpoint+api → default:azure)"
    else
        fail "S03E22 default: rung 1 azure" "out=$(printf '%s' "$def1_out" | head -c 320)"
    fi

    # 2. Rung 2: exactly one AZ_AI_<PRESET>_ENDPOINT → default:<preset>
    local def2_out
    def2_out=$(env -i HOME="$def_home" PATH="$PATH" \
                XDG_CONFIG_HOME="$def_home/.config" \
                AZ_AI_GROQ_ENDPOINT="https://api.groq.com/openai/v1" \
                "$BIN" --config show 2>&1 || true)
    if printf '%s' "$def2_out" | grep -qE 'provider source:[[:space:]]+default:groq(\b|$)'; then
        pass "S03E22 default: rung 2 (single preset endpoint → default:groq)"
    else
        fail "S03E22 default: rung 2 single preset" "out=$(printf '%s' "$def2_out" | head -c 320)"
    fi

    # 3. Rung 5: tie-break across multiple presets emits the warning.
    #    cloudflare < groq alphabetically → wins.
    local def5_out
    def5_out=$(env -i HOME="$def_home" PATH="$PATH" \
                XDG_CONFIG_HOME="$def_home/.config" \
                AZ_AI_GROQ_ENDPOINT="https://api.groq.com/openai/v1" \
                AZ_AI_CLOUDFLARE_ENDPOINT="https://api.cloudflare.com/client/v4/accounts/x/ai/v1" \
                "$BIN" --config show 2>&1 || true)
    if printf '%s' "$def5_out" | grep -qE 'provider source:[[:space:]]+default:cloudflare(\b|$)'; then
        pass "S03E22 default: rung 5 tie-break picks alphabetical first (cloudflare)"
    else
        fail "S03E22 default: rung 5 tie-break" "out=$(printf '%s' "$def5_out" | head -c 320)"
    fi
    if printf '%s' "$def5_out" | grep -qF 'multiple-presets-no-cli-no-profile-no-env-pin'; then
        pass "S03E22 default: rung 5 emits multi-preset warning"
    else
        fail "S03E22 default: rung 5 warning" "out=$(printf '%s' "$def5_out" | head -c 320)"
    fi

    # 4. AZ_PROVIDER pin pre-empts the heuristic entirely (env source).
    local def_pin_out
    def_pin_out=$(env -i HOME="$def_home" PATH="$PATH" \
                    XDG_CONFIG_HOME="$def_home/.config" \
                    AZ_PROVIDER="openai" \
                    AZ_AI_GROQ_ENDPOINT="https://api.groq.com/openai/v1" \
                    AZ_AI_CLOUDFLARE_ENDPOINT="https://api.cloudflare.com/client/v4/accounts/x/ai/v1" \
                    "$BIN" --config show 2>&1 || true)
    if printf '%s' "$def_pin_out" | grep -qE 'provider source:[[:space:]]+env:AZ_PROVIDER(\b|$)'; then
        pass "S03E22 default: AZ_PROVIDER pre-empts heuristic"
    else
        fail "S03E22 default: AZ_PROVIDER pre-empts" "out=$(printf '%s' "$def_pin_out" | head -c 320)"
    fi

    # 5. Rung 6: nothing set → default:azure:fallback (fails closed
    #    later, but the resolver itself stamps a deterministic label).
    local def6_out
    def6_out=$(env -i HOME="$def_home" PATH="$PATH" \
                XDG_CONFIG_HOME="$def_home/.config" \
                "$BIN" --config show 2>&1 || true)
    if printf '%s' "$def6_out" | grep -qE 'provider source:[[:space:]]+default:azure:fallback(\b|$)'; then
        pass "S03E22 default: rung 6 fallback label when no signals"
    else
        fail "S03E22 default: rung 6 fallback" "out=$(printf '%s' "$def6_out" | head -c 320)"
    fi

    rm -rf "$def_home"

    # ── S03E17 -- The Server (file slot 21): llamacpp preset ──────────────
    echo ""
    echo "▸ S03E17 -- The Server: llamacpp OpenAI-compat preset"

    # 1. --doctor probes the llamacpp preset (compat:llamacpp row) when
    #    AZ_AI_COMPAT_MODELS routes a model to it. Endpoint column shows
    #    the default localhost:8080/v1 base URL.
    local lc_doc_out
    set +e
    lc_doc_out=$(env -i HOME="${TMPDIR:-/tmp}" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" \
        AZ_AI_LOCAL_PROVIDERS=1 \
        AZ_AI_COMPAT_MODELS="llamacpp:llamacpp" \
        "$BIN" --doctor 2>&1 || true)
    set -e
    if printf '%s' "$lc_doc_out" | grep -q 'compat:llamacpp' && \
       printf '%s' "$lc_doc_out" | grep -q 'localhost:8080'; then
        pass "S03E17 server: --doctor lists compat:llamacpp probe row with default endpoint"
    else
        fail "S03E17 server: --doctor llamacpp probe" \
            "out=$(printf '%s' "$lc_doc_out" | head -c 320)"
    fi

    # 2. --doctor --json emits a structured row for compat:llamacpp.
    local lc_json_out
    set +e
    lc_json_out=$(env -i HOME="${TMPDIR:-/tmp}" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" \
        AZ_AI_LOCAL_PROVIDERS=1 \
        AZ_AI_COMPAT_MODELS="llamacpp:llamacpp" \
        "$BIN" --doctor --json 2>&1 || true)
    set -e
    if printf '%s' "$lc_json_out" | grep -q 'compat:llamacpp'; then
        pass "S03E17 server: --doctor --json emits compat:llamacpp row"
    else
        fail "S03E17 server: --doctor --json llamacpp" \
            "out=$(printf '%s' "$lc_json_out" | head -c 320)"
    fi

    # 3. Capability gate refuses --agent (tool-calls) on llamacpp by
    #    default (Conservative profile). Names AZ_AI_CAPABILITY_OVERRIDES
    #    so the operator knows the escape hatch. Azure stub creds keep
    #    the early credential check happy; compat allowlist routes the
    #    model to llamacpp before any Azure HTTP call would happen.
    local lc_gate_out lc_gate_rc
    set +e
    lc_gate_out=$(env -i HOME="${TMPDIR:-/tmp}" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" \
        AZUREOPENAIENDPOINT="https://invalid.example.invalid/" \
        AZUREOPENAIAPI="sk-not-real" \
        AZUREOPENAIMODEL="gpt-4o-mini" \
        AZ_AI_LOCAL_PROVIDERS=1 \
        AZ_AI_COMPAT_MODELS="llamacpp:llamacpp" \
        "$BIN" --agent --model llamacpp "ping" 2>&1)
    lc_gate_rc=$?
    set -e
    if [ "$lc_gate_rc" -ne 0 ] && \
       printf '%s' "$lc_gate_out" | grep -q 'AZ_AI_CAPABILITY_OVERRIDES'; then
        pass "S03E17 server: capability gate refuses tool-calls on llamacpp (rc=$lc_gate_rc)"
    else
        fail "S03E17 server: capability gate llamacpp tool-calls" \
            "rc=$lc_gate_rc out=$(printf '%s' "$lc_gate_out" | head -c 320)"
    fi

    # 4. Loopback gate enforced: with AZ_AI_LOCAL_PROVIDERS unset, dispatch
    #    to llamacpp must be refused. The error message names the opt-in
    #    knob so the operator knows how to unblock it.
    local lc_block_out lc_block_rc
    set +e
    lc_block_out=$(env -i HOME="${TMPDIR:-/tmp}" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" \
        AZUREOPENAIENDPOINT="https://invalid.example.invalid/" \
        AZUREOPENAIAPI="sk-not-real" \
        AZUREOPENAIMODEL="gpt-4o-mini" \
        AZ_AI_COMPAT_MODELS="llamacpp:llamacpp" \
        "$BIN" --model llamacpp "ping" 2>&1)
    lc_block_rc=$?
    set -e
    if [ "$lc_block_rc" -ne 0 ] && \
       printf '%s' "$lc_block_out" | grep -q 'AZ_AI_LOCAL_PROVIDERS'; then
        pass "S03E17 server: loopback gate refuses llamacpp without AZ_AI_LOCAL_PROVIDERS=1 (rc=$lc_block_rc)"
    else
        fail "S03E17 server: loopback gate llamacpp" \
            "rc=$lc_block_rc out=$(printf '%s' "$lc_block_out" | head -c 320)"
    fi

    # ── S03E22 *The Fallback* (Frank Costanza) ────────────────────────────
    echo "▸ S03E22 fallback chain (--fallback opt-in)"

    # 1. --help mentions --fallback (discoverable).
    if "$BIN" --help 2>&1 | grep -q -- "--fallback"; then
        pass "S03E22 fallback: --help mentions --fallback"
    else
        fail "S03E22 fallback: --help missing --fallback" "help text didn't mention the flag"
    fi

    # 2. unknown preset -> exit 2 + lists known presets on stderr.
    # No dummy creds needed: fallback parsing fires before the creds check.
    fb_unknown_out=$("$BIN" --fallback bogus -- "hi" 2>&1 1>/dev/null || true)
    fb_unknown_rc=$("$BIN" --fallback bogus -- "hi" >/dev/null 2>&1; echo $?)
    if [ "$fb_unknown_rc" = "2" ] && \
       printf '%s' "$fb_unknown_out" | grep -qi "unknown fallback provider" && \
       printf '%s' "$fb_unknown_out" | grep -q "openai"; then
        pass "S03E22 fallback: unknown preset exits 2 + lists known"
    else
        fail "S03E22 fallback: unknown preset" "rc=$fb_unknown_rc out=$(printf '%s' "$fb_unknown_out" | head -c 240)"
    fi

    # 3. depth >3 -> exit 2 + "exceeds max" message.
    fb_depth_out=$("$BIN" --fallback openai,groq,together,cloudflare -- "hi" 2>&1 1>/dev/null || true)
    fb_depth_rc=$("$BIN" --fallback openai,groq,together,cloudflare -- "hi" >/dev/null 2>&1; echo $?)
    if [ "$fb_depth_rc" = "2" ] && printf '%s' "$fb_depth_out" | grep -qi "exceeds the maximum"; then
        pass "S03E22 fallback: depth>3 exits 2 with max-depth message"
    else
        fail "S03E22 fallback: depth>3" "rc=$fb_depth_rc out=$(printf '%s' "$fb_depth_out" | head -c 240)"
    fi

    # 4. duplicate preset -> exit 2 + "duplicate" message.
    fb_dup_out=$("$BIN" --fallback openai,openai -- "hi" 2>&1 1>/dev/null || true)
    fb_dup_rc=$("$BIN" --fallback openai,openai -- "hi" >/dev/null 2>&1; echo $?)
    if [ "$fb_dup_rc" = "2" ] && printf '%s' "$fb_dup_out" | grep -qi "more than once"; then
        pass "S03E22 fallback: duplicate preset exits 2"
    else
        fail "S03E22 fallback: duplicate" "rc=$fb_dup_rc out=$(printf '%s' "$fb_dup_out" | head -c 240)"
    fi

    # 5. --fallback without value -> Fail() exits 2 with helpful message.
    fb_noval_out=$("$BIN" --fallback 2>&1 1>/dev/null || true)
    fb_noval_rc=$("$BIN" --fallback >/dev/null 2>&1; echo $?)
    if [ "$fb_noval_rc" != "0" ] && printf '%s' "$fb_noval_out" | grep -q "requires a comma-separated"; then
        pass "S03E22 fallback: missing value exits non-zero (rc=$fb_noval_rc)"
    else
        fail "S03E22 fallback: missing value" "rc=$fb_noval_rc out=$(printf '%s' "$fb_noval_out" | head -c 240)"
    fi

    # 6. AZ_AI_FALLBACK env with valid chain accepted by --doctor (parse-time
    #    succeeds; --doctor doesn't dispatch so production-skip never triggers).
    fb_env_rc=$(AZ_AI_FALLBACK=openai,groq "$BIN" --doctor >/dev/null 2>&1; echo $?)
    # rc 0 (no providers) or rc 1 (providers configured) both acceptable;
    # what matters is parse didn't reject (rc != 2).
    if [ "$fb_env_rc" != "2" ]; then
        pass "S03E22 fallback: AZ_AI_FALLBACK env with valid chain parses ok (rc=$fb_env_rc)"
    else
        fail "S03E22 fallback: env-valid" "rc=$fb_env_rc unexpectedly 2"
    fi

    # ── S03E22 fallback -- end ────────────────────────────────────────────

    # ── S03E23 *The Persona, Multi-Provider* (Kramer; file slot 28) ──────
    echo "▸ S03E23 persona pin (.squad.json provider/model)"

    # Resolve $BIN to an absolute path so we can cd into a tempdir without
    # losing it (mirrors the FR-021 regression block at #15).
    local pmp_bin; pmp_bin=$(cd "$(dirname "$BIN")" && pwd)/$(basename "$BIN")

    # Isolated workspace: a .squad.json with a known-good and a bad pin we
    # can target one at a time. Cleaned up at end of block (no RETURN trap
    # — the function already owns one).
    local pmp_dir; pmp_dir=$(mktemp -d -t azai-pmp.XXXXXX)
    pmp_cleanup() { rm -rf "$pmp_dir" 2>/dev/null || true; }

    # Good config -- pin coder->openai/gpt-4o, reviewer with no pins.
    cat > "$pmp_dir/.squad.json" <<'PMP_GOOD'
{
  "team": {"name": "PMP"},
  "personas": [
    {"name": "coder", "role": "engineer", "system_prompt": "Code.",
     "tools": [], "provider": "openai", "model": "gpt-4o"},
    {"name": "reviewer", "role": "reviewer", "system_prompt": "Review.",
     "tools": []}
  ],
  "routing": []
}
PMP_GOOD

    # 1. --personas lists the persona-pin-bearing config without crashing
    #    (validation passes; both personas surface).
    local pmp_list_out
    pmp_list_out=$(cd "$pmp_dir" && env HOME="$test_home" "$pmp_bin" --personas 2>&1; true)
    if printf '%s' "$pmp_list_out" | grep -q "coder" && \
       printf '%s' "$pmp_list_out" | grep -q "reviewer"; then
        pass "S03E23 persona pin: --personas lists persona with pinned provider"
    else
        fail "S03E23 persona pin: --personas list" \
             "out=$(printf '%s' "$pmp_list_out" | head -c 240)"
    fi

    # 2. Bad config -- unknown provider -> Load() rejects with actionable
    #    error referencing persona name + bad value + 'Known providers'.
    cat > "$pmp_dir/.squad.json" <<'PMP_BAD'
{
  "team": {"name": "PMP"},
  "personas": [
    {"name": "kramer", "role": "engineer", "system_prompt": "x",
     "tools": [], "provider": "anthropic"}
  ],
  "routing": []
}
PMP_BAD
    local pmp_bad_out pmp_bad_rc
    pmp_bad_out=$(cd "$pmp_dir" && env HOME="$test_home" "$pmp_bin" --personas 2>&1 1>/dev/null; true)
    set +e
    (cd "$pmp_dir" && env HOME="$test_home" "$pmp_bin" --personas >/dev/null 2>&1)
    pmp_bad_rc=$?
    set -e
    if [ "$pmp_bad_rc" != "0" ] && \
       printf '%s' "$pmp_bad_out" | grep -q "anthropic" && \
       printf '%s' "$pmp_bad_out" | grep -qi "known providers"; then
        pass "S03E23 persona pin: unknown provider rejected at load (rc=$pmp_bad_rc)"
    else
        fail "S03E23 persona pin: unknown provider" \
             "rc=$pmp_bad_rc out=$(printf '%s' "$pmp_bad_out" | head -c 320)"
    fi

    # 3. Bad config error names the offending persona so the operator can
    #    grep the file straight to it.
    if printf '%s' "$pmp_bad_out" | grep -q "kramer"; then
        pass "S03E23 persona pin: error names the offending persona"
    else
        fail "S03E23 persona pin: error missing persona name" \
             "out=$(printf '%s' "$pmp_bad_out" | head -c 240)"
    fi

    # 4. Squad-init scaffold passes validator (no persona pins by default
    #    -> Validate() is a no-op). Remove the bad config first; --squad-init
    #    refuses to overwrite.
    rm -f "$pmp_dir/.squad.json"
    set +e
    (cd "$pmp_dir" && env HOME="$test_home" "$pmp_bin" --squad-init >/dev/null 2>&1)
    local pmp_init_rc=$?
    set -e
    local pmp_personas_rc
    set +e
    (cd "$pmp_dir" && env HOME="$test_home" "$pmp_bin" --personas >/dev/null 2>&1)
    pmp_personas_rc=$?
    set -e
    if [ "$pmp_init_rc" = "0" ] && [ "$pmp_personas_rc" = "0" ]; then
        pass "S03E23 persona pin: squad-init scaffold passes validator"
    else
        fail "S03E23 persona pin: scaffold validator" \
             "init_rc=$pmp_init_rc personas_rc=$pmp_personas_rc"
    fi

    # 5. Empty `provider` / `model` strings are permissive (treated as no
    #    pin). Safety net for a half-edited .squad.json where a key was
    #    added but no value typed in yet.
    cat > "$pmp_dir/.squad.json" <<'PMP_EMPTY'
{
  "team": {"name": "PMP"},
  "personas": [
    {"name": "coder", "role": "engineer", "system_prompt": "x",
     "tools": [], "provider": "", "model": ""}
  ],
  "routing": []
}
PMP_EMPTY
    set +e
    (cd "$pmp_dir" && env HOME="$test_home" "$pmp_bin" --personas >/dev/null 2>&1)
    local pmp_empty_rc=$?
    set -e
    if [ "$pmp_empty_rc" = "0" ]; then
        pass "S03E23 persona pin: empty provider/model strings ignored"
    else
        fail "S03E23 persona pin: empty pins" "rc=$pmp_empty_rc"
    fi

    pmp_cleanup
    # ── S03E23 persona pin -- end ─────────────────────────────────────────

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
