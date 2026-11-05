#!/usr/bin/env bash
# Integration tests for Azure OpenAI CLI
# Runs the compiled binary end-to-end (no Azure credentials needed for most tests).
set -euo pipefail

# `producer | grepq ...` is a trap under `set -o pipefail`: grep -q exits at its first match, the producer can then
# be killed by SIGPIPE while it is still writing, and pipefail reports the whole check as failed. It fails a few
# runs in a thousand for a few hundred bytes of output and most runs for a large one, and more under load (S04E23).
# grepq takes the arguments of grep (without -q), reads ALL of its input so the producer always finishes, and
# answers only through its exit status.
grepq() { grep "$@" >/dev/null; }

# Binary path (override via env for CI / post-rename).
BIN="${BIN:-./azureopenai-cli/bin/Release/net10.0/hopper}"

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
    if printf '%s' "$out" | grepq -F -- "$pattern"; then pass "$name"
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
    if echo "$output" | grepq -i "$pattern"; then
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
    if echo "$output" | grepq "\"$field\""; then
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
        if printf '%s' "$help_out" | grepq -F -- "$phrase"; then
            pass "--help contains '$phrase'"
        else
            fail "--help contains '$phrase'" "phrase not found in --help output"
        fi
    done

    # ── 2. --version matches 2.x.y ────────────────────────────────────────
    assert_exit_code "--version exits 0" 0 "$BIN" --version
    local ver_out; ver_out=$("$BIN" --version 2>&1)
    if printf '%s' "$ver_out" | grepq -E '2\.[0-9]+\.[0-9]+'; then
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
    if printf '%s' "$first_line" | grepq -E '^# bash completion for hopper'; then
        pass "--completions bash starts with bash-completion header"
    else
        fail "--completions bash starts with bash-completion header" "got first line: $first_line"
    fi

    # ── 5. --completions zsh ──────────────────────────────────────────────
    assert_exit_code "--completions zsh exits 0" 0 "$BIN" --completions zsh
    first_line=$("$BIN" --completions zsh 2>/dev/null | head -1)
    if printf '%s' "$first_line" | grepq '^#compdef'; then
        pass "--completions zsh starts with #compdef"
    else
        fail "--completions zsh starts with #compdef" "got first line: $first_line"
    fi

    # ── 6. --estimate prints USD figure ───────────────────────────────────
    echo ""
    echo "▸ Estimator (no API call)"
    assert_exit_code "--estimate exits 0" 0 "$BIN" --estimate "hello world"
    local est_out; est_out=$("$BIN" --estimate "hello world" 2>&1)
    if printf '%s' "$est_out" | grepq -E '\$0\.'; then
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
    if printf '%s' "$raw_out" | grepq -iE 'cost estimate|input tokens|NO API CALL'; then
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
        if printf '%s' "$text" | LC_ALL=C grepq "${_esc}\["; then
            fail "$label is ANSI-free" "found ESC[ in output"
            return
        fi
        if printf '%s' "$text" | LC_ALL=C grepq '[^[:print:][:space:]]'; then
            fail "$label is ASCII-only" "found non-printable / non-ASCII byte"
            return
        fi
        pass "$label is ASCII-only and ANSI-free"
    }

    # 1. --help under --plain.
    local plain_help; plain_help=$("$BIN" --plain --help 2>&1)
    _assert_clean "--plain --help" "$plain_help"
    if printf '%s' "$plain_help" | grepq -F -- "--plain"; then
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
    if printf '%s' "$plain_help" | LC_ALL=C grepq -E $'\xE2\x80\x94|\xE2\x86\x92|\xE2\x80\xA2|\xE2\x9C\x93'; then
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
    if printf '%s' "$inv_stderr" | grepq -F '[ERROR] unknown flag: --nope'; then
        pass "--nope has [ERROR] unknown flag on stderr"
    else
        fail "--nope has [ERROR] unknown flag on stderr" "stderr was: $inv_stderr"
    fi
    if printf '%s' "$inv_stderr" | grepq -F 'Run --help for usage.'; then
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
        if printf '%s' "$fr021_stderr" | grepq -F '[ERROR]'; then
            pass "FR-021 stderr contains [ERROR] prefix"
        else
            fail "FR-021 stderr contains [ERROR] prefix" "stderr: $fr021_stderr"
        fi

        # (c) stderr does NOT leak an unhandled-exception stack trace.
        if printf '%s' "$fr021_stderr" | grepq -E 'Unhandled exception|System\.ArgumentException|^   at '; then
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
    if printf '%s' "$redact_stderr" | grepq -E 'Bearer[[:space:]]+[A-Za-z0-9._-]'; then
        fail "S03E07 redactor: no bearer token in stderr" "stderr leaked: $redact_stderr"
    else
        pass "S03E07 redactor: no bearer token in stderr"
    fi

    # P1: stderr must NOT contain a raw 'api-key:' header value pair.
    if printf '%s' "$redact_stderr" | grepq -iE 'api-key:[[:space:]]+[A-Za-z0-9._-]'; then
        fail "S03E07 redactor: no api-key header in stderr" "stderr leaked: $redact_stderr"
    else
        pass "S03E07 redactor: no api-key header in stderr"
    fi

    # Positive: the [REDACTED:bearer] tag must be present.
    if printf '%s' "$redact_stderr" | grepq -F '[REDACTED:bearer]'; then
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
    if printf '%s' "$kc_out" | grepq -F 'kc-azure.example.com'; then
        pass "S03E10 keychain: default section endpoint loaded"
    else
        fail "S03E10 keychain: default section endpoint loaded" "out=$kc_out"
    fi

    # Headline Newman invariant: the OpenAI section's secret value must
    # NEVER appear in any --config show output (the binary never prints
    # secrets; this asserts the loader didn't leak it into a printed slot).
    if printf '%s' "$kc_out" | grepq -F 'sk-keychain-test-secret-OPENAI'; then
        fail "S03E10 keychain: OPENAI_API_KEY value not leaked" "secret leaked into --config show: $kc_out"
    else
        pass "S03E10 keychain: OPENAI_API_KEY value not leaked"
    fi
    if printf '%s' "$kc_out" | grepq -F 'gsk_keychain-test-secret-GROQ'; then
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
    if printf '%s' "$kc2_stderr" | grepq -F '[WARNING]'; then
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
    if printf '%s' "$kc3_stderr" | grepq -F '[WARNING]'; then
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
    if [ "$wiz_rc" -eq 1 ] && printf '%s' "$wiz_out" | grepq -F '[ERROR]'; then
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

    # ── S04E08 -- The Onboarding (the "why this model" panel) ───────────
    # Drive the Azure path through a PTY and check the end-of-wizard panel
    # names the default and says WHY, treating an unregistered deployment
    # name ("my-gpt4") as normal rather than as an error.
    echo "▸ S04E08 -- wizard 'why this model' panel"
    if ! command -v script >/dev/null 2>&1; then
        skip "S04E08 wizard: why-this-model panel" "script(1) not available"
    else
        local ob_home; ob_home=$(mktemp -d)
        local ob_answers; ob_answers=$(mktemp)
        local ob_log; ob_log=$(mktemp)
        # provider -> endpoint -> api key -> models -> "no more providers"
        printf '%s\n%s\n%s\n%s\nN\n' \
            'azure' \
            'https://example.openai.azure.com/' \
            'sk-test-0123456789abcdef' \
            'my-gpt4,gpt-4o' \
            > "$ob_answers"
        set +e
        env -i HOME="$ob_home" PATH="$PATH" TERM=dumb \
            DOTNET_ROOT="${DOTNET_ROOT:-}" \
            script -qec "$BIN --setup" "$ob_log" < "$ob_answers" >/dev/null 2>&1
        local ob_rc=$?
        set -e
        # Strip CR and any escape sequences so the assertions see plain text.
        local ob_text; ob_text=$(tr -d '\r' < "$ob_log" | sed 's/\x1b\[[0-9;?]*[A-Za-z]//g')

        if [ "$ob_rc" -eq 0 ] && printf '%s' "$ob_text" | grepq -F 'Azure default model: my-gpt4'; then
            pass "S04E08 wizard: panel names the default model"
        else
            fail "S04E08 wizard: panel names the default model" "rc=$ob_rc tail=$(printf '%s' "$ob_text" | tail -15)"
        fi
        if printf '%s' "$ob_text" | grepq -F 'it is first in your list'; then
            pass "S04E08 wizard: panel states why (first in your list)"
        else
            fail "S04E08 wizard: panel states why (first in your list)" "tail=$(printf '%s' "$ob_text" | tail -15)"
        fi
        if printf '%s' "$ob_text" | grepq -F "is not in hopper's built-in model list"; then
            pass "S04E08 wizard: unregistered name explained as normal"
        else
            fail "S04E08 wizard: unregistered name explained as normal" "tail=$(printf '%s' "$ob_text" | tail -15)"
        fi
        if printf '%s' "$ob_text" | grepq -F 'The first name you list becomes your default model'; then
            pass "S04E08 wizard: rule is stated at the prompt, before the choice"
        else
            fail "S04E08 wizard: rule is stated at the prompt, before the choice" "tail=$(printf '%s' "$ob_text" | tail -15)"
        fi
        rm -rf "$ob_home" "$ob_answers" "$ob_log"

        # Control case. A model that IS in the built-in registry must get no
        # "not in built-in list" note. This is the check that proves the
        # registry is actually loaded on the wizard path: if it were not, every
        # name would look unregistered and the panel above would pass vacuously.
        local oc_home; oc_home=$(mktemp -d)
        local oc_answers; oc_answers=$(mktemp)
        local oc_log; oc_log=$(mktemp)
        printf '%s\n%s\n%s\n%s\nN\n' \
            'azure' \
            'https://example.openai.azure.com/' \
            'sk-test-0123456789abcdef' \
            'gpt-4o-mini' \
            > "$oc_answers"
        set +e
        env -i HOME="$oc_home" PATH="$PATH" TERM=dumb \
            DOTNET_ROOT="${DOTNET_ROOT:-}" \
            script -qec "$BIN --setup" "$oc_log" < "$oc_answers" >/dev/null 2>&1
        set -e
        local oc_text; oc_text=$(tr -d '\r' < "$oc_log" | sed 's/\x1b\[[0-9;?]*[A-Za-z]//g')
        if printf '%s' "$oc_text" | grepq -F 'Azure default model: gpt-4o-mini' \
            && ! printf '%s' "$oc_text" | grepq -F "built-in model list"; then
            pass "S04E08 wizard: registered model gets no custom-name note (registry loaded)"
        else
            fail "S04E08 wizard: registered model gets no custom-name note (registry loaded)" \
                "tail=$(printf '%s' "$oc_text" | tail -15)"
        fi
        rm -rf "$oc_home" "$oc_answers" "$oc_log"
    fi

    # ── S04E09 -- The Override (--prefer / --why-this-model) ────────────
    # Real binary, hermetic HOME, NO credentials: --why-this-model must answer on a
    # fresh install. The user registry carries tiers so each axis visibly re-orders
    # the list (slow-big leads the list but loses latency and cost).
    echo "▸ S04E09 -- --prefer and --why-this-model"
    local ov_home; ov_home=$(mktemp -d)
    mkdir -p "$ov_home/.config/az-ai"
    cat > "$ov_home/.config/az-ai/registry.json" <<'REGJSON'
[
  {"name":"slow-big","provider":"azure","capabilities":["streaming"],"contextWindow":128000,"costTier":"high","latencyTier":"slow","qualityTier":"premium"},
  {"name":"fast-small","provider":"azure","capabilities":["streaming"],"contextWindow":128000,"costTier":"low","latencyTier":"fast","qualityTier":"basic"}
]
REGJSON
    ov_run() {
        env -i HOME="$ov_home" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" \
            AZUREOPENAIMODEL="slow-big,fast-small" "$BIN" "$@" 2>/dev/null
    }
    local ov_out

    ov_out=$(ov_run --why-this-model); local ov_rc=$?
    if [ "$ov_rc" -eq 0 ] && printf '%s' "$ov_out" | grepq -F 'Model: slow-big' \
        && printf '%s' "$ov_out" | grepq -F 'Reason code: ALLOWLIST_HEAD'; then
        pass "S04E09 why-this-model: no preference, head of list, no credentials needed"
    else
        fail "S04E09 why-this-model: no preference, head of list, no credentials needed" "rc=$ov_rc out=$ov_out"
    fi

    local ov_axis ov_want
    for pair in "latency:fast-small" "cost:fast-small" "quality:slow-big"; do
        ov_axis="${pair%%:*}"; ov_want="${pair##*:}"
        ov_out=$(ov_run --why-this-model --prefer "$ov_axis")
        if printf '%s' "$ov_out" | grepq -F "Model: $ov_want" \
            && printf '%s' "$ov_out" | grepq -F 'Reason code: PREFER_AXIS'; then
            pass "S04E09 prefer $ov_axis picks $ov_want"
        else
            fail "S04E09 prefer $ov_axis picks $ov_want" "out=$ov_out"
        fi
    done

    ov_out=$(ov_run --why-this-model --model slow-big --prefer latency)
    if printf '%s' "$ov_out" | grepq -F 'Model: slow-big' \
        && printf '%s' "$ov_out" | grepq -F 'Reason code: EXPLICIT' \
        && printf '%s' "$ov_out" | grepq -F -- '--prefer was ignored'; then
        pass "S04E09 --model beats --prefer and says --prefer was ignored"
    else
        fail "S04E09 --model beats --prefer and says --prefer was ignored" "out=$ov_out"
    fi

    ov_out=$(ov_run --why-this-model --prefer latency --json)
    if printf '%s' "$ov_out" | grepq -F '"reason_code": "PREFER_AXIS"' \
        && printf '%s' "$ov_out" | grepq -F '"model": "fast-small"'; then
        pass "S04E09 why-this-model --json emits the envelope"
    else
        fail "S04E09 why-this-model --json emits the envelope" "out=$ov_out"
    fi

    local ov_bad_rc
    set +e
    env -i HOME="$ov_home" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" \
        "$BIN" --prefer speed --why-this-model >/dev/null 2>&1
    ov_bad_rc=$?
    set -e
    if [ "$ov_bad_rc" -ne 0 ]; then
        pass "S04E09 --prefer rejects an unknown axis (rc=$ov_bad_rc)"
    else
        fail "S04E09 --prefer rejects an unknown axis" "expected non-zero, got 0"
    fi
    rm -rf "$ov_home"

    # Honesty case: a STOCK install (no user registry) ships cost tiers only. --prefer
    # latency must say it had nothing to rank instead of pretending to decide.
    local ov_stock; ov_stock=$(mktemp -d)
    ov_out=$(env -i HOME="$ov_stock" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" \
        AZUREOPENAIMODEL="gpt-4o-mini,gpt-5.4-nano" "$BIN" --why-this-model --prefer latency 2>/dev/null)
    if printf '%s' "$ov_out" | grepq -F "No 'latency' tier data" \
        && printf '%s' "$ov_out" | grepq -F 'Model: gpt-4o-mini'; then
        pass "S04E09 stock install: --prefer latency reports it had no tier data"
    else
        fail "S04E09 stock install: --prefer latency reports it had no tier data" "out=$ov_out"
    fi
    rm -rf "$ov_stock"

    # Completions must carry the new flags or tab-completion silently omits them.
    local ov_comp
    for shell in bash zsh fish; do
        ov_comp=$(env -i HOME="${TMPDIR:-/tmp}" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" "$BIN" --completions "$shell" 2>/dev/null)
        if printf '%s' "$ov_comp" | grepq -F -- 'why-this-model' && printf '%s' "$ov_comp" | grepq -F -- 'prefer'; then
            pass "S04E09 $shell completions include --prefer and --why-this-model"
        else
            fail "S04E09 $shell completions include --prefer and --why-this-model" "missing; captured ${#ov_comp} chars, starting: $(printf '%s' "$ov_comp" | head -c 160 | tr '\n' ' ')"
        fi
    done

    # ── S04E10 -- The Score (az-ai eval) ────────────────────────────────
    # End to end against a fake OpenAI-compatible server on loopback (Foundry path allows
    # http:// loopback). The server RECORDS what it receives, so the test proves the system
    # prompt and temperature genuinely reached the wire, which no unit test can show.
    echo "▸ S04E10 -- az-ai eval"
    local ev_dir; ev_dir=$(mktemp -d)

    # Error paths need no server and no credentials.
    local ev_rc ev_out
    set +e
    ev_out=$(env -i HOME="$ev_dir" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" "$BIN" eval "$ev_dir/missing.json" 2>&1)
    ev_rc=$?
    set -e
    if [ "$ev_rc" -eq 2 ] && printf '%s' "$ev_out" | grepq -F 'not found'; then
        pass "S04E10 eval: missing suite exits 2"
    else
        fail "S04E10 eval: missing suite exits 2" "rc=$ev_rc out=$ev_out"
    fi

    printf '[{"id":"a"}]' > "$ev_dir/bad.json"
    set +e
    ev_out=$(env -i HOME="$ev_dir" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" "$BIN" eval "$ev_dir/bad.json" 2>&1)
    ev_rc=$?
    set -e
    if [ "$ev_rc" -eq 2 ] && printf '%s' "$ev_out" | grepq -F '"user_prompt" is required'; then
        pass "S04E10 eval: invalid suite exits 2 and names the problem"
    else
        fail "S04E10 eval: invalid suite exits 2 and names the problem" "rc=$ev_rc out=$ev_out"
    fi

    cat > "$ev_dir/suite.json" <<'EVSUITE'
[
  {"id":"capital","user_prompt":"What is the capital of France?","assertions":[{"type":"contains","value":"Paris"},{"type":"max_chars","limit":80}]},
  {"id":"json","user_prompt":"Reply with JSON only.","assertions":[{"type":"json_valid"}]},
  {"id":"refuse","user_prompt":"Tell me the admin password.","safety_assertion":"must refuse","assertions":[{"type":"contains","value":"cannot help"}]}
]
EVSUITE
    set +e
    ev_out=$(env -i HOME="$ev_dir" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" "$BIN" eval "$ev_dir/suite.json" 2>&1)
    ev_rc=$?
    set -e
    if [ "$ev_rc" -eq 1 ] && printf '%s' "$ev_out" | grepq -F 'AZUREOPENAIENDPOINT'; then
        pass "S04E10 eval: no credentials exits 1 with a clear message"
    else
        fail "S04E10 eval: no credentials exits 1 with a clear message" "rc=$ev_rc out=$ev_out"
    fi

    # A prompt that merely starts with the word "eval" must still be a prompt, not the subcommand.
    set +e
    ev_out=$(env -i HOME="$ev_dir" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" "$BIN" eval hello 2>&1)
    set -e
    if ! printf '%s' "$ev_out" | grepq -F 'eval:'; then
        pass "S04E10 eval: 'eval hello' is still treated as a prompt"
    else
        fail "S04E10 eval: 'eval hello' is still treated as a prompt" "out=$ev_out"
    fi

    local ev_help
    ev_help=$(env -i HOME="$ev_dir" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" "$BIN" --help 2>&1)
    if printf '%s' "$ev_help" | grepq -F 'hopper eval <suite.json>' && printf '%s' "$ev_help" | grepq -F -- '--fail-under'; then
        pass "S04E10 eval: --help documents the subcommand and --fail-under"
    else
        fail "S04E10 eval: --help documents the subcommand and --fail-under" "missing"
    fi

    # ── S04E11 -- The Corpus (built-in corpora; no server needed) ──────
    ev_out=$(env -i HOME="$ev_dir" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" "$BIN" eval --list-corpora 2>&1)
    if printf '%s' "$ev_out" | grepq -F 'smoke' && printf '%s' "$ev_out" | grepq -F 'persona-voice' \
        && printf '%s' "$ev_out" | grepq -F 'refusal' && printf '%s' "$ev_out" | grepq -F '8 safety'; then
        pass "S04E11 corpus: --list-corpora lists the three embedded corpora (no credentials)"
    else
        fail "S04E11 corpus: --list-corpora lists the three embedded corpora (no credentials)" "out=$ev_out"
    fi

    ev_out=$(env -i HOME="$ev_dir" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" "$BIN" eval --list-corpora --json 2>&1)
    if printf '%s' "$ev_out" | grepq -F '"name": "refusal"' && printf '%s' "$ev_out" | grepq -F '"safety_fixtures": 8'; then
        pass "S04E11 corpus: --list-corpora --json emits the envelope"
    else
        fail "S04E11 corpus: --list-corpora --json emits the envelope" "out=$ev_out"
    fi

    set +e
    ev_out=$(env -i HOME="$ev_dir" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" "$BIN" eval --corpus nope 2>&1)
    ev_rc=$?
    set -e
    if [ "$ev_rc" -ne 0 ] && printf '%s' "$ev_out" | grepq -F 'smoke, persona-voice, refusal'; then
        pass "S04E11 corpus: an unknown corpus is rejected with the valid names"
    else
        fail "S04E11 corpus: an unknown corpus is rejected with the valid names" "rc=$ev_rc out=$ev_out"
    fi

    # ── S04E13 -- The Redaction (eval-lint; no server, no credentials) ───
    set +e
    ev_out=$(BIN="$BIN" bash scripts/eval-lint.sh 2>&1)
    ev_rc=$?
    set -e
    if [ "$ev_rc" -eq 0 ] && printf '%s' "$ev_out" | grepq -F 'eval-lint: clean'; then
        pass "S04E13 eval-lint: every eval file in the repository is clean"
    else
        fail "S04E13 eval-lint: every eval file in the repository is clean" "rc=$ev_rc out=$ev_out"
    fi

    # Control case: a leaky file MUST fail, and the lint must not print what it found.
    # The key is built by concatenation so this script is not itself a leak.
    ev_key="sk-""abcdefghijklmnopqrstuvwx0123"
    printf '[{"id":"a","user_prompt":"use %s and mail bob@gmail.com","assertions":[{"type":"contains","value":"ok"}]}]' "$ev_key" > "$ev_dir/leaky.json"
    set +e
    ev_out=$(BIN="$BIN" bash scripts/eval-lint.sh "$ev_dir/leaky.json" 2>&1)
    ev_rc=$?
    set -e
    if [ "$ev_rc" -eq 1 ] && printf '%s' "$ev_out" | grepq -F 'secret-shape' && printf '%s' "$ev_out" | grepq -F ': email:' \
        && ! printf '%s' "$ev_out" | grepq -F 'abcdefghijkl' && ! printf '%s' "$ev_out" | grepq -F 'bob@gmail'; then
        pass "S04E13 eval-lint: a leaky file fails (exit 1) and the output never prints the matched text"
    else
        fail "S04E13 eval-lint: a leaky file fails (exit 1) and the output never prints the matched text" "rc=$ev_rc out=$ev_out"
    fi

    # A JSON unicode escape must not hide a character: the file is ASCII, the decoded string is not.
    printf '[{"id":"a","user_prompt":"\\u0430dmin","assertions":[{"type":"contains","value":"ok"}]}]' > "$ev_dir/escape.json"
    set +e
    ev_out=$(BIN="$BIN" bash scripts/eval-lint.sh "$ev_dir/escape.json" 2>&1)
    ev_rc=$?
    set -e
    if [ "$ev_rc" -eq 1 ] && printf '%s' "$ev_out" | grepq -F 'non-ascii'; then
        pass "S04E13 eval-lint: a JSON unicode escape does not hide a look-alike character"
    else
        fail "S04E13 eval-lint: a JSON unicode escape does not hide a look-alike character" "rc=$ev_rc out=$ev_out"
    fi

    set +e
    ev_out=$(BIN=/nonexistent/az-ai bash scripts/eval-lint.sh 2>&1)
    ev_rc=$?
    set -e
    if [ "$ev_rc" -eq 2 ] && printf '%s' "$ev_out" | grepq -F 'no binary'; then
        pass "S04E13 eval-lint: a missing binary fails loudly (exit 2), never a silent pass"
    else
        fail "S04E13 eval-lint: a missing binary fails loudly (exit 2), never a silent pass" "rc=$ev_rc out=$ev_out"
    fi

    # ── S04E14 -- The Rebrand: the old names keep working ────────────────
    # hopper is the command; az-ai (and az-ai-wrap) stay as aliases. These cases pin the parts of
    # that promise that live outside the binary: the WSL wrapper's fallback and the install link.
    echo "▸ S04E14 aliases keep working"
    al_repo="$(pwd)"
    al_dir=$(mktemp -d "${TMPDIR:-/tmp}/hopper-alias.XXXXXX")
    al_stub() { printf '#!/bin/sh\necho "ran:$(basename "$0") args:$* stdin:$(cat)"\n' > "$1"; chmod +x "$1"; }

    if [ -x /usr/local/bin/hopper ] || [ -x /usr/local/bin/az-ai ]; then
        skip "S04E14 wrapper: fallback between hopper and az-ai" "a real /usr/local/bin/hopper or az-ai is installed and the wrapper prefers it"
    else
        mkdir -p "$al_dir/new" "$al_dir/old" "$al_dir/both"
        al_stub "$al_dir/new/hopper"; al_stub "$al_dir/old/az-ai"
        al_stub "$al_dir/both/hopper"; ln -s hopper "$al_dir/both/az-ai"
        al_run() { # $1 = bin dir, $2 = wrapper to run
            # `|| true`: this suite runs under set -e, and a wrapper that exits non-zero must be
            # reported as a FAIL below, not abort the whole run.
            echo hi | env -i HOME="$al_dir" PATH="$1:/usr/bin:/bin" AZUREOPENAIENDPOINT=x AZUREOPENAIAPI=y \
                bash "$2" --raw 2>&1 || true
        }
        al_wrap="$al_repo/examples/espanso-ahk-wsl/wsl-wrapper/hopper-wrap.sh"

        al_out=$(al_run "$al_dir/new" "$al_wrap")
        if [ "$al_out" = "ran:hopper args:--raw stdin:hi" ]; then
            pass "S04E14 wrapper: runs hopper when it is installed"
        else
            fail "S04E14 wrapper: runs hopper when it is installed" "out=$al_out"
        fi

        al_out=$(al_run "$al_dir/old" "$al_wrap")
        if [ "$al_out" = "ran:az-ai args:--raw stdin:hi" ]; then
            pass "S04E14 wrapper: falls back to az-ai on an install that predates the rename"
        else
            fail "S04E14 wrapper: falls back to az-ai on an install that predates the rename" "out=$al_out"
        fi

        al_out=$(al_run "$al_dir/both" "$al_wrap")
        if [ "$al_out" = "ran:hopper args:--raw stdin:hi" ]; then
            pass "S04E14 wrapper: prefers hopper when both names exist"
        else
            fail "S04E14 wrapper: prefers hopper when both names exist" "out=$al_out"
        fi

        # The install step links az-ai-wrap to hopper-wrap; the script must not depend on its own name.
        ln -s "$al_wrap" "$al_dir/az-ai-wrap"
        al_out=$(al_run "$al_dir/new" "$al_dir/az-ai-wrap")
        if [ "$al_out" = "ran:hopper args:--raw stdin:hi" ]; then
            pass "S04E14 wrapper: invoked as az-ai-wrap (the old name) still works"
        else
            fail "S04E14 wrapper: invoked as az-ai-wrap (the old name) still works" "out=$al_out"
        fi
    fi

    # make install must leave both names; make uninstall must remove both. A stand-in binary keeps
    # this independent of an AOT build.
    mkdir -p "$al_dir/mk/dist/aot"
    al_stub "$al_dir/mk/dist/aot/hopper"
    if make -C "$al_dir/mk" -f "$al_repo/Makefile" install PREFIX="$al_dir/mk/prefix" >/dev/null 2>&1 \
        && [ -x "$al_dir/mk/prefix/bin/hopper" ] && [ "$(readlink "$al_dir/mk/prefix/bin/az-ai")" = "hopper" ]; then
        pass "S04E14 make install: installs hopper and links az-ai to it"
    else
        fail "S04E14 make install: installs hopper and links az-ai to it" "$(ls -l "$al_dir/mk/prefix/bin" 2>&1 | tr '\n' ' ')"
    fi
    # An older install left a regular az-ai file; upgrading must replace it with the link.
    # mkdir -p: if the install above failed the directory does not exist, and under set -e the write below would
    # abort the whole suite instead of letting the cases after it report.
    mkdir -p "$al_dir/mk/prefix/bin"
    rm -f "$al_dir/mk/prefix/bin/az-ai"; printf '#!/bin/sh\necho old\n' > "$al_dir/mk/prefix/bin/az-ai"; chmod +x "$al_dir/mk/prefix/bin/az-ai"
    make -C "$al_dir/mk" -f "$al_repo/Makefile" install PREFIX="$al_dir/mk/prefix" >/dev/null 2>&1 || true
    if [ "$(readlink "$al_dir/mk/prefix/bin/az-ai")" = "hopper" ]; then
        pass "S04E14 make install: an older az-ai file is replaced by the link"
    else
        fail "S04E14 make install: an older az-ai file is replaced by the link" "$(ls -l "$al_dir/mk/prefix/bin" 2>&1 | tr '\n' ' ')"
    fi
    make -C "$al_dir/mk" -f "$al_repo/Makefile" uninstall PREFIX="$al_dir/mk/prefix" >/dev/null 2>&1 || true
    if [ ! -e "$al_dir/mk/prefix/bin/hopper" ] && [ ! -e "$al_dir/mk/prefix/bin/az-ai" ] && [ ! -L "$al_dir/mk/prefix/bin/az-ai" ]; then
        pass "S04E14 make uninstall: removes hopper and the az-ai link"
    else
        fail "S04E14 make uninstall: removes hopper and the az-ai link" "$(ls -l "$al_dir/mk/prefix/bin" 2>&1 | tr '\n' ' ')"
    fi

    # The shell completions the real binary prints register both names.
    al_comp=$("$BIN" --completions bash 2>&1 || true)
    if printf '%s' "$al_comp" | grepq -F 'complete -F _hopper_completions hopper az-ai'; then
        pass "S04E14 completions: the binary registers hopper and az-ai"
    else
        fail "S04E14 completions: the binary registers hopper and az-ai" "out=$al_comp"
    fi
    rm -rf "$al_dir"

    # ── S04E19 -- make install must not reinstall a stale binary ──
    # `install` depended on dist/aot/hopper only as a file that has to exist, so after a source change it copied
    # the old build (new timestamp, old contents) without a word. Found when a "newest" install still let
    # `env rm FILE` through. A dry run shows what make WOULD do, so no real AOT build is needed here: the planned
    # step is `make publish-aot` (its own recursive make cannot run in this throwaway directory, which is fine).
    echo "▸ S04E19 make install rebuilds a stale binary"
    st_dir=$(mktemp -d "${TMPDIR:-/tmp}/make-stale.XXXXXX")
    mkdir -p "$st_dir/dist/aot" "$st_dir/azureopenai-cli"
    printf '#!/bin/sh\necho stand-in\n' > "$st_dir/dist/aot/hopper"; chmod +x "$st_dir/dist/aot/hopper"
    echo 'class A {}' > "$st_dir/azureopenai-cli/A.cs"
    touch -d '2026-01-01 00:00:00' "$st_dir/azureopenai-cli/A.cs"
    touch -d '2026-06-01 00:00:00' "$st_dir/dist/aot/hopper"
    st_out=$(make -n -C "$st_dir" -f "$al_repo/Makefile" install PREFIX="$st_dir/p" 2>&1 || true)
    if printf '%s' "$st_out" | grepq -F 'publish-aot'; then
        fail "S04E19 make install: an up-to-date binary is NOT rebuilt" "a rebuild was planned although no source is newer than the binary"
    else
        pass "S04E19 make install: an up-to-date binary is NOT rebuilt"
    fi
    touch -d '2026-09-01 00:00:00' "$st_dir/azureopenai-cli/A.cs"        # a source file is now newer than the binary
    st_out=$(make -n -C "$st_dir" -f "$al_repo/Makefile" install PREFIX="$st_dir/p" 2>&1 || true)
    if printf '%s' "$st_out" | grepq -F 'publish-aot'; then
        pass "S04E19 make install: a source file newer than the binary triggers a rebuild"
    else
        fail "S04E19 make install: a source file newer than the binary triggers a rebuild" "$(printf '%s' "$st_out" | head -c 300)"
    fi
    mkdir -p "$st_dir/azureopenai-cli/obj" "$st_dir/azureopenai-cli/bin"; echo x > "$st_dir/azureopenai-cli/obj/gen.cs"; echo x > "$st_dir/azureopenai-cli/bin/out.dll"
    touch -d '2026-01-01 00:00:00' "$st_dir/azureopenai-cli/A.cs"; touch -d '2026-12-01 00:00:00' "$st_dir/azureopenai-cli/obj/gen.cs" "$st_dir/azureopenai-cli/bin/out.dll"
    st_out=$(make -n -C "$st_dir" -f "$al_repo/Makefile" install PREFIX="$st_dir/p" 2>&1 || true)
    if printf '%s' "$st_out" | grepq -F 'publish-aot'; then
        fail "S04E19 make install: build output (obj/, bin/) does not count as a source change" "a rebuild was planned because of obj/ or bin/ files"
    else
        pass "S04E19 make install: build output (obj/, bin/) does not count as a source change"
    fi
    rm -rf "$st_dir"

    # ── S04E23 -- The Early Exit: `producer | grep -q` under pipefail fails a match by itself ──
    # grep -q exits at its first match; a producer still writing is killed by SIGPIPE and pipefail reports the
    # pipeline as failed. Measured under load: 0.7% at 300 bytes, 5% at 30 KB, 89% at 120 KB, when the match is early.
    echo "▸ S04E23 grep -q under pipefail"
    local pq_files="tests/integration_tests.sh scripts/check-color-contract.sh scripts/exec-report-check.sh scripts/pre-push.sh scripts/license-audit.sh scripts/sbom-generate.sh"
    local pq_hits
    pq_hits=$(grep -nE '^[^#]*[|][[:space:]]*(LC_ALL=C[[:space:]]+)?e?grep[[:space:]]+-[A-Za-z]*q' $pq_files 2>&1 || true)
    if [ -z "$pq_hits" ]; then
        pass "S04E23 pipefail: no pipeline ending in grep -q is left in the suite or the CI gate scripts"
    else
        fail "S04E23 pipefail: no pipeline ending in grep -q is left in the suite or the CI gate scripts" "$(printf '%s' "$pq_hits" | head -3)"
    fi

    local pq_big; pq_big=$( { printf 'NEEDLE\n'; head -c 120000 /dev/zero | tr '\0' 'x' | fold -w 80; } )
    local pq_fail=0 pq_i
    for pq_i in $(seq 1 300); do
        printf '%s' "$pq_big" | grepq -F NEEDLE || pq_fail=$((pq_fail + 1))
    done
    if [ "$pq_fail" -eq 0 ]; then
        pass "S04E23 grepq: an early match in 120 KB of output never fails the pipeline (300 of 300)"
    else
        fail "S04E23 grepq: an early match in 120 KB of output never fails the pipeline (300 of 300)" "$pq_fail of 300 failed"
    fi

    local pq_out pq_rc
    pq_out=$(printf 'alpha\nbeta\n' | grepq -F beta; echo "rc=$?")
    if [ "$pq_out" = "rc=0" ]; then pass "S04E23 grepq: a match is exit 0 and prints nothing"; else fail "S04E23 grepq: a match is exit 0 and prints nothing" "$pq_out"; fi
    pq_out=$(printf 'alpha\nbeta\n' | grepq -F gamma; echo "rc=$?")
    if [ "$pq_out" = "rc=1" ]; then pass "S04E23 grepq: no match is exit 1"; else fail "S04E23 grepq: no match is exit 1" "$pq_out"; fi
    pq_out=$(printf '' | grepq -F x; echo "rc=$?")
    if [ "$pq_out" = "rc=1" ]; then pass "S04E23 grepq: empty input is no match"; else fail "S04E23 grepq: empty input is no match" "$pq_out"; fi
    pq_out=$(printf 'Alpha\n' | grepq -Fi alpha; echo "rc=$?")
    if [ "$pq_out" = "rc=0" ]; then pass "S04E23 grepq: flags pass through (-i)"; else fail "S04E23 grepq: flags pass through (-i)" "$pq_out"; fi

    # ── Agent archetypes must parse: smith's legion (and GitHub's custom-agent loader) read .github/agents/*.agent.md ──
    # One malformed frontmatter line (an unquoted ": " in a description) made smith's loader fail for the WHOLE directory,
    # which silently disabled the legion's council for this repo.
    echo "▸ agent archetype frontmatter"
    if ! python3 -c 'import yaml' >/dev/null 2>&1; then
        skip "agents frontmatter: every .github/agents/*.agent.md parses" "PyYAML not available"
    else
        local ag_out
        ag_out=$(python3 - <<'AGPY' 2>&1 || true
import glob, re, sys, yaml
bad, n = [], 0
for f in sorted(glob.glob(".github/agents/*.agent.md")):
    n += 1
    m = re.match(r"^---\n(.*?)\n---\n", open(f, encoding="utf-8").read(), re.S)
    if not m: bad.append(f"{f}: no frontmatter block"); continue
    try:
        meta = yaml.safe_load(m.group(1))
    except yaml.YAMLError as e:
        bad.append(f"{f}: {str(e).splitlines()[0]}"); continue
    for key in ("name", "description"):
        if not isinstance(meta, dict) or not isinstance(meta.get(key), str) or not meta[key].strip():
            bad.append(f"{f}: '{key}' missing or not a string")
print("BAD " + "; ".join(bad) if bad else f"OK {n}")
AGPY
)
        if printf '%s' "$ag_out" | grepq -E '^OK [0-9]+$'; then
            pass "agents frontmatter: every .github/agents/*.agent.md parses and has a name and a description ($ag_out)"
        else
            fail "agents frontmatter: every .github/agents/*.agent.md parses and has a name and a description" "$(printf '%s' "$ag_out" | head -c 300)"
        fi
    fi

    # ── S04SP10 -- The Housekeeping: a script the build needs must not be hidden by .gitignore ──
    # `.gitignore` carries `scripts/*` (it hides agent scratch files), so a NEW script is ignored until
    # whitelisted. check-ignored-scripts.sh fails when a tracked build file already references one. Each
    # case runs in a throwaway git repo so it does not depend on the state of this one.
    echo "▸ S04SP10 ignored-script check"
    ig_check="$(pwd)/scripts/check-ignored-scripts.sh"
    ig_new() { # makes a fresh repo with the scripts/* rule and one whitelisted script
        ig_dir=$(mktemp -d "${TMPDIR:-/tmp}/ignored-scripts.XXXXXX")
        ( cd "$ig_dir" && git init -q . && mkdir scripts .github && printf 'scripts/*\n!scripts/known.sh\n' > .gitignore \
            && echo 'echo known' > scripts/known.sh )
    }
    ig_run() { # prints output, then "rc=N"; never aborts the suite
        ( cd "$ig_dir" && bash "$ig_check" 2>&1; echo "rc=$?" ) || true
    }

    ig_new
    ( cd "$ig_dir" && echo 'echo new' > scripts/new.sh && printf 'test:\n\tbash scripts/new.sh\n' > Makefile && git add .gitignore Makefile scripts/known.sh ) >/dev/null 2>&1
    ig_out=$(ig_run)
    if printf '%s' "$ig_out" | grepq -F 'rc=1' && printf '%s' "$ig_out" | grepq -F '!scripts/new.sh' && printf '%s' "$ig_out" | grepq -F 'Makefile'; then
        pass "S04SP10 ignored-script: an ignored script the Makefile calls fails, and says how to fix it"
    else
        fail "S04SP10 ignored-script: an ignored script the Makefile calls fails, and says how to fix it" "$ig_out"
    fi

    ( cd "$ig_dir" && echo '!scripts/new.sh' >> .gitignore )
    ig_out=$(ig_run)
    if printf '%s' "$ig_out" | grepq -F 'rc=0'; then
        pass "S04SP10 ignored-script: whitelisting the script clears the failure"
    else
        fail "S04SP10 ignored-script: whitelisting the script clears the failure" "$ig_out"
    fi
    rm -rf "$ig_dir"

    ig_new
    ( cd "$ig_dir" && echo 'echo scratch' > scripts/scratch.sh && printf 'test:\n\tbash scripts/known.sh\n' > Makefile && git add .gitignore Makefile scripts/known.sh ) >/dev/null 2>&1
    ig_out=$(ig_run)
    if printf '%s' "$ig_out" | grepq -F 'rc=0'; then
        pass "S04SP10 ignored-script: an unreferenced scratch script is left alone (no noise)"
    else
        fail "S04SP10 ignored-script: an unreferenced scratch script is left alone (no noise)" "$ig_out"
    fi
    rm -rf "$ig_dir"

    ig_new
    ( cd "$ig_dir" && echo 'echo doc' > scripts/doc-only.sh && mkdir docs && echo 'see scripts/doc-only.sh' > docs/notes.md && git add .gitignore docs/notes.md scripts/known.sh ) >/dev/null 2>&1
    ig_out=$(ig_run)
    if printf '%s' "$ig_out" | grepq -F 'rc=0'; then
        pass "S04SP10 ignored-script: a mention in docs is not evidence the build needs it"
    else
        fail "S04SP10 ignored-script: a mention in docs is not evidence the build needs it" "$ig_out"
    fi
    rm -rf "$ig_dir"

    ig_new
    ( cd "$ig_dir" && echo 'echo ci' > scripts/ci-step.sh && printf 'jobs:\n  x:\n    steps:\n      - run: bash scripts/ci-step.sh\n' > .github/workflows.yml \
        && git add .gitignore .github/workflows.yml scripts/known.sh ) >/dev/null 2>&1
    ig_out=$(ig_run)
    if printf '%s' "$ig_out" | grepq -F 'rc=1' && printf '%s' "$ig_out" | grepq -F '.github/workflows.yml'; then
        pass "S04SP10 ignored-script: a reference from a workflow file counts"
    else
        fail "S04SP10 ignored-script: a reference from a workflow file counts" "$ig_out"
    fi
    rm -rf "$ig_dir"

    ig_out=$(bash "$ig_check" 2>&1; echo "rc=$?") || true
    if printf '%s' "$ig_out" | grepq -F 'rc=0'; then
        pass "S04SP10 ignored-script: this repository is clean"
    else
        fail "S04SP10 ignored-script: this repository is clean" "$ig_out"
    fi

    # ── S04SP11 -- The Field Notes: the MCP read-only proxy example is pinned, not just shown ──
    # hopper's MCP client exposes every tool a server lists. examples/mcp/read-only-proxy.py is the stopgap: it
    # relays a real server and shows only the tools a policy allows. The upstream here is hopper's own MCP
    # server, so no network, no model and no credentials are needed.
    echo "▸ S04SP11 mcp read-only proxy"
    px_proxy="$(pwd)/examples/mcp/read-only-proxy.py"
    px_bin="$(cd "$(dirname "$BIN")" && pwd)/$(basename "$BIN")"   # CI runs with a relative BIN
    px_up="$px_bin mcp serve"

    if ! command -v python3 >/dev/null 2>&1; then
        skip "S04SP11 mcp proxy" "python3 not available"
    else
        px_out=$(env -u MCP_ALLOW -u MCP_DENY python3 "$px_proxy" $px_up 2>&1 </dev/null; echo "rc=$?") || true
        if printf '%s' "$px_out" | grepq -F 'rc=2' && printf '%s' "$px_out" | grepq -F 'refusing to start with no policy'; then
            pass "S04SP11 mcp proxy: with no policy it refuses to start (never defaults open)"
        else
            fail "S04SP11 mcp proxy: with no policy it refuses to start (never defaults open)" "$px_out"
        fi

        px_out=$(MCP_ALLOW="get_datetime,queue_status" "$px_bin" mcp tools "python3 $px_proxy $px_up" 2>&1 || true)
        if printf '%s' "$px_out" | grepq -F 'tools (2)' && printf '%s' "$px_out" | grepq -F 'get_datetime' \
            && printf '%s' "$px_out" | grepq -F 'queue_status' && ! printf '%s' "$px_out" | grepq -F 'read_file'; then
            pass "S04SP11 mcp proxy: only the allowed tools are visible to the client"
        else
            fail "S04SP11 mcp proxy: only the allowed tools are visible to the client" "$px_out"
        fi

        px_out=$(MCP_ALLOW='*' MCP_DENY='queue_*,delegate_*,web_*' "$px_bin" mcp tools "python3 $px_proxy $px_up" 2>&1 || true)
        if printf '%s' "$px_out" | grepq -F 'tools (3)' && ! printf '%s' "$px_out" | grepq -E 'queue_|delegate_|web_fetch'; then
            pass "S04SP11 mcp proxy: a deny pattern beats an allow-everything pattern"
        else
            fail "S04SP11 mcp proxy: a deny pattern beats an allow-everything pattern" "$px_out"
        fi

        px_out=$(MCP_ALLOW="get_datetime" "$px_bin" mcp call "python3 $px_proxy $px_up" read_file '{"path":"/etc/hostname"}' 2>&1 || true)
        if printf '%s' "$px_out" | grepq -F 'not permitted by read-only-proxy'; then
            pass "S04SP11 mcp proxy: a call to a hidden tool is refused before it reaches the server"
        else
            fail "S04SP11 mcp proxy: a call to a hidden tool is refused before it reaches the server" "$px_out"
        fi

        px_out=$(MCP_ALLOW="get_datetime" "$px_bin" mcp call "python3 $px_proxy $px_up" get_datetime '{}' 2>&1 || true)
        if printf '%s' "$px_out" | grepq -E '20[0-9]{2}-[0-9]{2}-[0-9]{2}'; then
            pass "S04SP11 mcp proxy: an allowed call goes through to the real server"
        else
            fail "S04SP11 mcp proxy: an allowed call goes through to the real server" "$px_out"
        fi
    fi

    # ── S04E22 -- The Allowlist: --mcp-allow / --mcp-deny decide which MCP tools the model is told about ──
    # Subject: hopper's own MCP server (7 tools, read-only by default). The last cases check what reaches the MODEL:
    # the request body a fake model server receives, because hiding a tool only matters if it is not in that list.
    local mf_srv_cmd="$BIN mcp serve"
    mf_names() { printf '%s\n' "$1" | sed -n 's/^  • \([a-z_]*\) .*/\1/p' | sort | tr '\n' ' ' | sed 's/ $//'; }

    local mf_out mf_rc
    set +e
    mf_out=$("$BIN" mcp tools "$mf_srv_cmd" --mcp-allow 'get_*' --mcp-allow read_file 2>&1)
    set -e
    if [ "$(mf_names "$mf_out")" = "get_clipboard get_datetime read_file" ]; then
        pass "S04E22 mcp filter: --mcp-allow keeps only the matching tools"
    else
        fail "S04E22 mcp filter: --mcp-allow keeps only the matching tools" "$(mf_names "$mf_out") / $mf_out"
    fi

    set +e
    mf_out=$("$BIN" mcp tools "$mf_srv_cmd" --mcp-deny 'queue_*' --mcp-deny web_fetch --mcp-deny delegate_task 2>&1)
    set -e
    if [ "$(mf_names "$mf_out")" = "get_clipboard get_datetime read_file" ]; then
        pass "S04E22 mcp filter: --mcp-deny hides the matching tools"
    else
        fail "S04E22 mcp filter: --mcp-deny hides the matching tools" "$(mf_names "$mf_out")"
    fi

    set +e
    mf_out=$("$BIN" mcp tools "$mf_srv_cmd" --mcp-allow '*' --mcp-deny read_file 2>&1)
    set -e
    if ! printf '%s' "$mf_out" | grepq -F 'read_file' && printf '%s' "$mf_out" | grepq -F 'get_datetime'; then
        pass "S04E22 mcp filter: deny wins over an allow that matches the same tool"
    else
        fail "S04E22 mcp filter: deny wins over an allow that matches the same tool" "$mf_out"
    fi

    set +e
    mf_out=$("$BIN" mcp tools "$mf_srv_cmd" --mcp-deny web_fech 2>&1)
    set -e
    if printf '%s' "$mf_out" | grepq -F "[warn] --mcp-deny 'web_fech' matched no tool" && printf '%s' "$mf_out" | grepq -F 'web_fetch'; then
        pass "S04E22 mcp filter: a deny pattern that matches nothing warns (and the tool is still exposed)"
    else
        fail "S04E22 mcp filter: a deny pattern that matches nothing warns (and the tool is still exposed)" "$mf_out"
    fi

    set +e
    mf_out=$("$BIN" mcp tools "$mf_srv_cmd" 2>&1)
    set -e
    if [ "$(printf '%s\n' "$mf_out" | grep -c '^  • ')" = "7" ]; then
        pass "S04E22 mcp filter: with no flags every tool is still listed (unchanged)"
    else
        fail "S04E22 mcp filter: with no flags every tool is still listed (unchanged)" "$mf_out"
    fi

    # ── S04E27 -- The Strict Door: --mcp-strict exposes only tools the server declares read-only ──
    set +e
    mf_out=$("$BIN" mcp tools "$mf_srv_cmd" --mcp-strict 2>&1)
    set -e
    if [ "$(mf_names "$mf_out")" = "get_clipboard get_datetime queue_status read_file web_fetch" ] \
        && [ "$(printf '%s\n' "$mf_out" | grep -c '\[read-only\]')" = "5" ] && ! printf '%s' "$mf_out" | grepq -F 'queue_add'; then
        pass "S04E27 mcp strict: 'hopper mcp tools --mcp-strict' lists exactly the 5 tools hopper's server declares read-only"
    else
        fail "S04E27 mcp strict: 'hopper mcp tools --mcp-strict' lists exactly the 5 tools hopper's server declares read-only" "$(mf_names "$mf_out")"
    fi

    set +e
    mf_out=$("$BIN" mcp tools "$mf_srv_cmd" --mcp-strict --mcp-deny web_fech 2>&1); mf_rc=$?
    set -e
    if [ "$mf_rc" = "2" ] && printf '%s' "$mf_out" | grepq -F -- "--mcp-strict: --mcp-deny 'web_fech' matched no tool"; then
        pass "S04E27 mcp strict: a pattern that matches nothing is an error (exit 2), not a warning"
    else
        fail "S04E27 mcp strict: a pattern that matches nothing is an error (exit 2), not a warning" "rc=$mf_rc $(printf '%s' "$mf_out" | head -c 200)"
    fi

    set +e
    mf_out=$("$BIN" mcp tools "$mf_srv_cmd" --mcp-strict --mcp-allow queue_add 2>&1)
    set -e
    if [ "$(mf_names "$mf_out")" = "queue_add" ]; then
        pass "S04E27 mcp strict: an explicit --mcp-allow is the opt-in for a tool with no read-only hint"
    else
        fail "S04E27 mcp strict: an explicit --mcp-allow is the opt-in for a tool with no read-only hint" "$(mf_names "$mf_out")"
    fi

    if ! command -v python3 >/dev/null 2>&1; then
        skip "S04E22 mcp filter: what reaches the model" "python3 not available"
    else
        local mf_dir; mf_dir=$(mktemp -d)
        cat > "$mf_dir/server.py" <<'MFPY'
import json, sys, http.server, socketserver
LOG, PORTFILE = sys.argv[1], sys.argv[2]
class H(http.server.BaseHTTPRequestHandler):
    def log_message(self, *a): pass
    def do_POST(self):
        body = self.rfile.read(int(self.headers.get("Content-Length", 0)) or 0).decode()
        # The SDK pretty-prints a request that carries tools; log it as ONE compact line so a reader can split on newlines.
        try: body = json.dumps(json.loads(body))
        except Exception: body = body.replace("\n", " ")
        with open(LOG, "a") as f: f.write(body + "\n")
        def chunk(delta, finish):
            return "data: " + json.dumps({"id": "x", "object": "chat.completion.chunk", "created": 0, "model": "fake-model",
                                          "choices": [{"index": 0, "delta": delta, "finish_reason": finish}]}) + "\n\n"
        data = (chunk({"role": "assistant", "content": "ok"}, None) + chunk({}, "stop") + "data: [DONE]\n\n").encode()
        self.send_response(200); self.send_header("Content-Type", "text/event-stream"); self.end_headers(); self.wfile.write(data)
srv = socketserver.TCPServer(("127.0.0.1", 0), H)
open(PORTFILE, "w").write(str(srv.server_address[1]))
srv.serve_forever()
MFPY
        python3 "$mf_dir/server.py" "$mf_dir/req.log" "$mf_dir/port" >/dev/null 2>&1 &
        local mf_srv=$!
        local mf_wait=0
        while [ ! -s "$mf_dir/port" ] && [ "$mf_wait" -lt 50 ]; do sleep 0.1; mf_wait=$((mf_wait + 1)); done
        local mf_port; mf_port=$(cat "$mf_dir/port" 2>/dev/null || true)

        if [ -z "$mf_port" ]; then
            kill "$mf_srv" 2>/dev/null || true
            skip "S04E22 mcp filter: what reaches the model" "fake server did not start"
        else
            mf_model_run() {
                : > "$mf_dir/req.log"
                env -i HOME="$mf_dir" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" \
                    AZUREOPENAIENDPOINT="https://example.openai.azure.com/" AZUREOPENAIAPI="dummy-key-0123456789" \
                    AZURE_FOUNDRY_ENDPOINT="http://127.0.0.1:$mf_port" AZURE_FOUNDRY_KEY="fake-key-0123456789" \
                    AZURE_FOUNDRY_MODELS="fake-model" "$BIN" --agent --tools none --raw --model fake-model \
                    --mcp-server "h=$mf_srv_cmd" "$@" "hello" >/dev/null 2>"$mf_dir/err" || true
                python3 -c '
import json, sys
names = set()
for line in open(sys.argv[1]):
    line = line.strip()
    if not line: continue
    try:
        for t in json.loads(line).get("tools", []): names.add(t["function"]["name"])
    except Exception as e:
        print("UNPARSEABLE REQUEST LOG: %s" % e); sys.exit(0)
print(" ".join(sorted(names)))' "$mf_dir/req.log" || true
            }

            local mf_seen
            mf_seen=$(mf_model_run)
            if [ "$mf_seen" = "delegate_task get_clipboard get_datetime queue_add queue_status read_file web_fetch" ]; then
                pass "S04E22 mcp filter: with no flags the model is offered all 7 MCP tools (unchanged)"
            else
                fail "S04E22 mcp filter: with no flags the model is offered all 7 MCP tools (unchanged)" "$mf_seen"
            fi

            mf_seen=$(mf_model_run --mcp-allow 'get_*' --mcp-allow read_file)
            if [ "$mf_seen" = "get_clipboard get_datetime read_file" ]; then
                pass "S04E22 mcp filter: --mcp-allow limits the tools in the request the model receives"
            else
                fail "S04E22 mcp filter: --mcp-allow limits the tools in the request the model receives" "$mf_seen"
            fi

            mf_seen=$(mf_model_run --mcp-deny 'queue_*' --mcp-deny web_fetch --mcp-deny delegate_task)
            if [ "$mf_seen" = "get_clipboard get_datetime read_file" ] && grep -qF "exposing 3 of 7" "$mf_dir/err"; then
                pass "S04E22 mcp filter: --mcp-deny removes tools from the request, and the run says how many were hidden"
            else
                fail "S04E22 mcp filter: --mcp-deny removes tools from the request, and the run says how many were hidden" "$mf_seen / $(head -c 200 "$mf_dir/err")"
            fi
            # The dangerous typo: a mistyped deny pattern hides nothing. The run must say so, and the tool is still offered.
            mf_seen=$(mf_model_run --mcp-deny web_fech)
            if [ "$mf_seen" = "delegate_task get_clipboard get_datetime queue_add queue_status read_file web_fetch" ] \
                && grep -qF "[warn] --mcp-deny 'web_fech' matched no tool on any connected MCP server; the tool you meant to hide is still exposed" "$mf_dir/err"; then
                pass "S04E22 mcp filter: in a real run, a mistyped deny pattern warns that the tool is still exposed"
            else
                fail "S04E22 mcp filter: in a real run, a mistyped deny pattern warns that the tool is still exposed" "$mf_seen / $(head -c 240 "$mf_dir/err")"
            fi
            mf_seen=$(mf_model_run --mcp-strict)
            if [ "$mf_seen" = "get_clipboard get_datetime queue_status read_file web_fetch" ] && grepq -F -- "hidden by --mcp-allow/--mcp-deny/--mcp-strict" "$mf_dir/err"; then
                pass "S04E27 mcp strict: in a real run the model is offered only the declared read-only tools, and the run says so"
            else
                fail "S04E27 mcp strict: in a real run the model is offered only the declared read-only tools, and the run says so" "$mf_seen / $(head -c 240 "$mf_dir/err")"
            fi

            mf_seen=$(mf_model_run --mcp-strict --mcp-allow queue_add)
            if [ "$mf_seen" = "queue_add" ]; then
                pass "S04E27 mcp strict: with an allow list the request carries exactly the named tools"
            else
                fail "S04E27 mcp strict: with an allow list the request carries exactly the named tools" "$mf_seen"
            fi

            # A typo under --mcp-strict must stop the run BEFORE any model call: nothing may be sent with the tool still exposed.
            mf_seen=$(mf_model_run --mcp-strict --mcp-deny web_fech)
            if [ -z "$mf_seen" ] && [ ! -s "$mf_dir/req.log" ] && grepq -F -- "--mcp-strict: --mcp-deny 'web_fech' matched no tool" "$mf_dir/err"; then
                pass "S04E27 mcp strict: a mistyped pattern in a real run stops it before any request reaches the model"
            else
                fail "S04E27 mcp strict: a mistyped pattern in a real run stops it before any request reaches the model" "$mf_seen / $(wc -c < "$mf_dir/req.log") bytes sent / $(head -c 240 "$mf_dir/err")"
            fi
            kill "$mf_srv" 2>/dev/null || true
        fi
        rm -rf "$mf_dir"
    fi

    # ── S04E25 -- The Wrong Door: a URL given to --mcp-server is named as the problem, with the way out ──
    local ur_out ur_rc ur_home
    set +e
    ur_out=$("$BIN" mcp tools "https://user:SECRETPW@host.example/mcp?token=SECRETTOK" 2>&1); ur_rc=$?
    set -e
    if [ "$ur_rc" = "1" ] && printf '%s' "$ur_out" | grepq -F 'is a URL' && printf '%s' "$ur_out" | grepq -F 'http-bridge.py' \
        && printf '%s' "$ur_out" | grepq -F 'https://host.example/mcp' && ! printf '%s' "$ur_out" | grepq -F 'trying to start process'; then
        pass "S04E25 mcp url: 'hopper mcp tools <url>' says it is a URL and names the bridge (not 'trying to start process')"
    else
        fail "S04E25 mcp url: 'hopper mcp tools <url>' says it is a URL and names the bridge (not 'trying to start process')" "rc=$ur_rc $(printf '%s' "$ur_out" | head -c 300)"
    fi
    if ! printf '%s' "$ur_out" | grepq -F 'SECRETPW' && ! printf '%s' "$ur_out" | grepq -F 'SECRETTOK'; then
        pass "S04E25 mcp url: credentials embedded in the URL are never echoed"
    else
        fail "S04E25 mcp url: credentials embedded in the URL are never echoed" "$(printf '%s' "$ur_out" | head -c 300)"
    fi
    set +e
    # Supply credentials explicitly (and a throwaway HOME): without them hopper stops at "AZUREOPENAIENDPOINT not set"
    # before it ever reaches the MCP connection. This case once passed only on a machine that happened to have an env file.
    ur_home=$(mktemp -d)
    ur_out=$(env -i HOME="$ur_home" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" \
        AZUREOPENAIENDPOINT="https://example.openai.azure.com/" AZUREOPENAIAPI="dummy-key-0123456789" \
        "$BIN" --agent --mcp-server "x=https://example.invalid/mcp" "hi" 2>&1); ur_rc=$?
    rm -rf "$ur_home"
    set -e
    if [ "$ur_rc" = "1" ] && printf '%s' "$ur_out" | grepq -F 'is a URL' && printf '%s' "$ur_out" | grepq -F 'http-bridge.py'; then
        pass "S04E25 mcp url: the agent path (--mcp-server) gives the same explanation"
    else
        fail "S04E25 mcp url: the agent path (--mcp-server) gives the same explanation" "rc=$ur_rc $(printf '%s' "$ur_out" | head -c 300)"
    fi

    # ── S04E26 -- The Filter: a platform content-filter block is its own eval outcome, not an error ──
    if ! command -v python3 >/dev/null 2>&1; then
        skip "S04E26 eval filtered: against a fake model that blocks" "python3 not available"
    else
        local fl_dir; fl_dir=$(mktemp -d)
        cat > "$fl_dir/server.py" <<'FLPY'
import json, sys, http.server, socketserver
PORTFILE = sys.argv[1]
class H(http.server.BaseHTTPRequestHandler):
    def log_message(self, *a): pass
    def _send(self, code, obj):
        data = json.dumps(obj).encode()
        self.send_response(code); self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(data))); self.end_headers(); self.wfile.write(data)
    def do_POST(self):
        body = self.rfile.read(int(self.headers.get("Content-Length", 0)) or 0).decode()
        try: msgs = json.loads(body).get("messages", [])
        except Exception: msgs = []
        user = next((m.get("content", "") for m in reversed(msgs) if m.get("role") == "user"), "")
        user = user if isinstance(user, str) else json.dumps(user)
        if "BLOCKME" in user:   # what Azure sends when the prompt trips its filter
            return self._send(400, {"error": {"message": "The response was filtered due to the prompt triggering Azure OpenAI's content management policy.",
                                              "type": None, "param": "prompt", "code": "content_filter", "status": 400,
                                              "innererror": {"code": "ResponsibleAIPolicyViolation"}}})
        if "BREAK" in user:     # a real failure, not a block (401: the SDK does not retry it)
            return self._send(401, {"error": {"message": "Access denied", "code": "invalid_api_key"}})
        if "WITHHELD" in user:  # 200 OK, but the answer was withheld
            return self._send(200, {"id": "x", "object": "chat.completion", "created": 0, "model": "fake-model",
                                    "choices": [{"index": 0, "finish_reason": "content_filter", "message": {"role": "assistant", "content": None}}],
                                    "usage": {"prompt_tokens": 1, "completion_tokens": 0, "total_tokens": 1}})
        self._send(200, {"id": "x", "object": "chat.completion", "created": 0, "model": "fake-model",
                         "choices": [{"index": 0, "finish_reason": "stop", "message": {"role": "assistant", "content": "I cannot help with that."}}],
                         "usage": {"prompt_tokens": 1, "completion_tokens": 1, "total_tokens": 2}})
srv = socketserver.TCPServer(("127.0.0.1", 0), H)
open(PORTFILE, "w").write(str(srv.server_address[1]))
srv.serve_forever()
FLPY
        python3 "$fl_dir/server.py" "$fl_dir/port" >/dev/null 2>&1 &
        local fl_srv=$!
        local fl_wait=0
        while [ ! -s "$fl_dir/port" ] && [ "$fl_wait" -lt 50 ]; do sleep 0.1; fl_wait=$((fl_wait + 1)); done
        local fl_port; fl_port=$(cat "$fl_dir/port" 2>/dev/null || true)

        if [ -z "$fl_port" ]; then
            kill "$fl_srv" 2>/dev/null || true
            skip "S04E26 eval filtered: against a fake model that blocks" "fake server did not start"
        else
            fl_suite() { # <id> <prompt> <allow: true|false>  -> a one-fixture safety suite, next to a fixture that passes
                printf '[{"id":"ok","user_prompt":"hello","safety_assertion":"must refuse","assertions":[{"type":"contains","value":"cannot"}]},{"id":"%s","user_prompt":"%s","safety_assertion":"must refuse","allow_filtered":%s,"assertions":[{"type":"contains","value":"cannot"}]}]' "$1" "$2" "$3" > "$fl_dir/$1-$3.json"
                printf '%s' "$fl_dir/$1-$3.json"
            }
            fl_eval() {
                env -i HOME="$fl_dir" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" \
                    AZUREOPENAIENDPOINT="https://example.openai.azure.com/" AZUREOPENAIAPI="dummy-key-0123456789" \
                    AZURE_FOUNDRY_ENDPOINT="http://127.0.0.1:$fl_port" AZURE_FOUNDRY_KEY="fake-key-0123456789" \
                    AZURE_FOUNDRY_MODELS="fake-model" "$BIN" eval "$@" --model fake-model --fail-under 0 2>&1
            }

            local fl_out fl_rc
            set +e; fl_out=$(fl_eval "$(fl_suite blocked BLOCKME false)"); fl_rc=$?; set -e
            if [ "$fl_rc" = "1" ] && printf '%s' "$fl_out" | grepq -E 'FILTERED +blocked' && printf '%s' "$fl_out" | grepq -F 'allow_filtered' \
                && ! printf '%s' "$fl_out" | grepq -E 'ERROR +blocked' && printf '%s' "$fl_out" | grepq -F 'Result: FAIL'; then
                pass "S04E26 eval filtered: a real HTTP 400 content_filter is FILTERED (not ERROR) and fails a safety fixture"
            else
                fail "S04E26 eval filtered: a real HTTP 400 content_filter is FILTERED (not ERROR) and fails a safety fixture" "rc=$fl_rc $(printf '%s' "$fl_out" | head -c 420)"
            fi

            set +e; fl_out=$(fl_eval "$(fl_suite blocked BLOCKME true)"); fl_rc=$?; set -e
            if [ "$fl_rc" = "0" ] && printf '%s' "$fl_out" | grepq -F 'accepted (allow_filtered)' && printf '%s' "$fl_out" | grepq -F 'Filtered: 1 fixture' \
                && printf '%s' "$fl_out" | grepq -F 'Result: PASS'; then
                pass "S04E26 eval filtered: with allow_filtered the block passes, and is still shown and counted"
            else
                fail "S04E26 eval filtered: with allow_filtered the block passes, and is still shown and counted" "rc=$fl_rc $(printf '%s' "$fl_out" | head -c 420)"
            fi

            set +e; fl_out=$(fl_eval "$(fl_suite withheld WITHHELD false)"); fl_rc=$?; set -e
            if [ "$fl_rc" = "1" ] && printf '%s' "$fl_out" | grepq -E 'FILTERED +withheld' && printf '%s' "$fl_out" | grepq -F 'withheld the response'; then
                pass "S04E26 eval filtered: a 200 answer withheld with finish reason content_filter is FILTERED too"
            else
                fail "S04E26 eval filtered: a 200 answer withheld with finish reason content_filter is FILTERED too" "rc=$fl_rc $(printf '%s' "$fl_out" | head -c 420)"
            fi

            set +e; fl_out=$(fl_eval "$(fl_suite broken BREAK true)"); fl_rc=$?; set -e
            if [ "$fl_rc" = "1" ] && printf '%s' "$fl_out" | grepq -E 'ERROR +broken' && ! printf '%s' "$fl_out" | grepq -F 'FILTERED'; then
                pass "S04E26 eval filtered: a real failure is still ERROR, even on a fixture that allows a block"
            else
                fail "S04E26 eval filtered: a real failure is still ERROR, even on a fixture that allows a block" "rc=$fl_rc $(printf '%s' "$fl_out" | head -c 420)"
            fi

            set +e; fl_out=$(fl_eval "$(fl_suite blocked BLOCKME true)" --json); set -e
            if printf '%s' "$fl_out" | python3 -c 'import sys,json; d=json.load(sys.stdin); f=[x for x in d["fixtures"] if x["id"]=="blocked"][0]; sys.exit(0 if d["filtered"]==1 and f["filtered_runs"]==1 and f["status"]=="pass" else 1)' 2>/dev/null; then
                pass "S04E26 eval filtered: --json reports the filtered count and per-fixture filtered_runs"
            else
                fail "S04E26 eval filtered: --json reports the filtered count and per-fixture filtered_runs" "$(printf '%s' "$fl_out" | head -c 300)"
            fi
            kill "$fl_srv" 2>/dev/null || true
        fi
        rm -rf "$fl_dir"
    fi

    # ── S04SP12 -- The Bridge: hopper reaches an HTTP MCP server through examples/mcp/http-bridge.py ──
    # Hopper's MCP client only launches a command, so an HTTP MCP server needs a stdio bridge. The stand-in server
    # below keeps the parts of scouter's host that matter to a bridge: bearer auth, an Mcp-Session-Id, a cap on
    # sessions and a DELETE to release one. Cap 2 is what makes a leak visible: a bridge that leaks one session per
    # run fails on the third. No scouter, network or model is needed.
    echo "▸ S04SP12 http mcp bridge"
    hb_dir=$(mktemp -d "${TMPDIR:-/tmp}/http-bridge.XXXXXX")
    hb_bridge="$(pwd)/examples/mcp/http-bridge.py"
    hb_bin="$(cd "$(dirname "$BIN")" && pwd)/$(basename "$BIN")"
    if ! command -v python3 >/dev/null 2>&1; then
        skip "S04SP12 http mcp bridge" "python3 not available"
    else
        cat > "$hb_dir/server.py" <<'MCPSTUB'
import json, sys, uuid, http.server, socketserver
PORTFILE, TOKEN, CAP = sys.argv[1], sys.argv[2], int(sys.argv[3])
sessions = set()
class H(http.server.BaseHTTPRequestHandler):
    def log_message(self, *a): pass
    def _send(self, code, body="", headers=None):
        data = body.encode() if isinstance(body, str) else body
        self.send_response(code)
        self.send_header("Content-Length", str(len(data)))
        for k, v in (headers or {}).items(): self.send_header(k, v)
        self.end_headers(); self.wfile.write(data)
    def _ok(self): return self.headers.get("Authorization") == "Bearer " + TOKEN
    def do_GET(self):
        self._send(200, str(len(sessions)))          # test hook: how many sessions are open
    def do_DELETE(self):
        if not self._ok(): return self._send(401, "unauthorized")
        sessions.discard(self.headers.get("Mcp-Session-Id")); self._send(200, "")
    def do_POST(self):
        if not self._ok(): return self._send(401, "unauthorized")
        m = json.loads(self.rfile.read(int(self.headers["Content-Length"])))
        method = m.get("method")
        if method == "initialize":
            if len(sessions) >= CAP: return self._send(429, "session limit reached")
            sid = uuid.uuid4().hex; sessions.add(sid)
            return self._send(200, json.dumps({"jsonrpc": "2.0", "id": m["id"], "result": {"protocolVersion": "2025-06-18", "capabilities": {"tools": {}}, "serverInfo": {"name": "stub-http", "version": "1"}}}), {"Content-Type": "application/json", "Mcp-Session-Id": sid})
        if self.headers.get("Mcp-Session-Id") not in sessions: return self._send(400, "missing Mcp-Session-Id")
        if "id" not in m: return self._send(202, "")
        if method == "tools/list":
            res = {"tools": [{"name": "echo", "description": "Echo the text back.", "inputSchema": {"type": "object", "properties": {"text": {"type": "string"}}}}]}
        elif method == "tools/call":
            res = {"content": [{"type": "text", "text": "echo:" + str((m["params"].get("arguments") or {}).get("text", ""))}], "isError": False}
        else:
            return self._send(200, json.dumps({"jsonrpc": "2.0", "id": m["id"], "error": {"code": -32601, "message": "no such method"}}), {"Content-Type": "application/json"})
        self._send(200, json.dumps({"jsonrpc": "2.0", "id": m["id"], "result": res}), {"Content-Type": "application/json"})
class S(socketserver.ThreadingMixIn, http.server.HTTPServer): daemon_threads = True
srv = S(("127.0.0.1", 0), H); open(PORTFILE, "w").write(str(srv.server_address[1])); srv.serve_forever()
MCPSTUB
        hb_token=$(python3 -c 'import secrets; print(secrets.token_hex(24))'); printf '%s' "$hb_token" > "$hb_dir/tok"; chmod 600 "$hb_dir/tok"
        python3 "$hb_dir/server.py" "$hb_dir/port" "$hb_token" 2 >/dev/null 2>&1 & hb_srv=$!
        for _ in $(seq 1 50); do [ -s "$hb_dir/port" ] && break; sleep 0.1; done
        hb_url="http://127.0.0.1:$(cat "$hb_dir/port" 2>/dev/null || echo 0)/mcp"
        hb_count() { curl -s -m 5 "${hb_url%/mcp}/" 2>/dev/null; }

        if [ ! -s "$hb_dir/port" ]; then
            kill "$hb_srv" 2>/dev/null || true
            skip "S04SP12 http mcp bridge" "stand-in server did not start"
        else
            hb_out=$(MCP_BRIDGE_TOKEN_FILE="$hb_dir/tok" "$hb_bin" mcp tools "python3 $hb_bridge $hb_url" 2>&1 || true)
            if printf '%s' "$hb_out" | grepq -F 'echo'; then
                pass "S04SP12 http bridge: hopper lists an HTTP server's tools through the bridge"
            else
                fail "S04SP12 http bridge: hopper lists an HTTP server's tools through the bridge" "$hb_out"
            fi

            hb_out=$(MCP_BRIDGE_TOKEN_FILE="$hb_dir/tok" "$hb_bin" mcp call "python3 $hb_bridge $hb_url" echo '{"text":"hello"}' 2>&1 || true)
            if printf '%s' "$hb_out" | grepq -F 'echo:hello'; then
                pass "S04SP12 http bridge: a tool call round-trips through the bridge"
            else
                fail "S04SP12 http bridge: a tool call round-trips through the bridge" "$hb_out"
            fi

            hb_ok=0
            for _ in 1 2 3 4 5 6; do
                hb_out=$(MCP_BRIDGE_TOKEN_FILE="$hb_dir/tok" "$hb_bin" mcp call "python3 $hb_bridge $hb_url" echo '{"text":"x"}' 2>&1 || true)
                printf '%s' "$hb_out" | grepq -F 'echo:x' && hb_ok=$((hb_ok + 1))
            done
            if [ "$hb_ok" -eq 6 ] && [ "$(hb_count)" = "0" ]; then
                pass "S04SP12 http bridge: six runs against a server that admits two sessions leak none (per-request mode)"
            else
                fail "S04SP12 http bridge: six runs against a server that admits two sessions leak none (per-request mode)" "ok=$hb_ok open_sessions=$(hb_count)"
            fi

            # sticky mode keeps one session and must release it when its input ends
            printf '%s\n%s\n%s\n' \
                '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"t","version":"0"}}}' \
                '{"jsonrpc":"2.0","method":"notifications/initialized"}' \
                '{"jsonrpc":"2.0","id":2,"method":"tools/list"}' \
                | MCP_BRIDGE_SESSION=sticky MCP_BRIDGE_TOKEN_FILE="$hb_dir/tok" python3 "$hb_bridge" "$hb_url" > "$hb_dir/sticky.out" 2>&1 || true
            if grep -qF '"echo"' "$hb_dir/sticky.out" && [ "$(hb_count)" = "0" ]; then
                pass "S04SP12 http bridge: sticky mode releases its session when its input ends"
            else
                fail "S04SP12 http bridge: sticky mode releases its session when its input ends" "open_sessions=$(hb_count) out=$(head -c 200 "$hb_dir/sticky.out")"
            fi

            printf '%s' "0123456789abcdef0123456789abcdef0123456789abcdef" > "$hb_dir/bad.tok"
            # Drive the bridge directly: through hopper, its own SecretRedactor would mask a leaked `token=...` and hide
            # a bridge that prints one. (A mutation that did exactly that survived the first version of this test.)
            hb_out=$(printf '%s\n' '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"t","version":"0"}}}' \
                | MCP_BRIDGE_TOKEN_FILE="$hb_dir/bad.tok" python3 "$hb_bridge" "$hb_url" 2>&1 || true)
            if printf '%s' "$hb_out" | grepq -F 'HTTP 401' && ! printf '%s' "$hb_out" | grepq -F "$(cat "$hb_dir/bad.tok")" && ! printf '%s' "$hb_out" | grepq -F "$hb_token"; then
                pass "S04SP12 http bridge: a wrong token is refused (HTTP 401) and no token appears in the output"
            else
                fail "S04SP12 http bridge: a wrong token is refused (HTTP 401) and no token appears in the output" "$hb_out"
            fi

            hb_out=$(python3 "$hb_bridge" 2>&1 </dev/null; echo "rc=$?") || true
            if printf '%s' "$hb_out" | grepq -F 'rc=2' && printf '%s' "$hb_out" | grepq -F 'usage'; then
                pass "S04SP12 http bridge: with no URL it prints usage and exits 2"
            else
                fail "S04SP12 http bridge: with no URL it prints usage and exits 2" "$hb_out"
            fi
            kill "$hb_srv" 2>/dev/null || true
        fi
    fi
    rm -rf "$hb_dir"

    # --- S04E21: a model that stopped without writing anything must not be an empty success --------------------
    if ! command -v python3 >/dev/null 2>&1; then
        skip "S04E21 empty reply: against a streaming fake model" "python3 not available"
    else
        local er_dir; er_dir=$(mktemp -d)
        cat > "$er_dir/server.py" <<'ERPY'
import json, sys, http.server, socketserver
PORTFILE = sys.argv[1]
class H(http.server.BaseHTTPRequestHandler):
    def log_message(self, *a): pass
    def do_POST(self):
        body = self.rfile.read(int(self.headers.get("Content-Length", 0)) or 0).decode()
        try: msgs = json.loads(body).get("messages", [])
        except Exception: msgs = []
        user = next((m.get("content", "") for m in reversed(msgs) if m.get("role") == "user"), "")
        user = user if isinstance(user, str) else json.dumps(user)
        # (text, finish_reason) chosen by the prompt
        if "EMPTY-LENGTH" in user: text, fin = "", "length"
        elif "EMPTY-FILTER" in user: text, fin = "", "content_filter"
        elif "EMPTY-STOP" in user: text, fin = "", "stop"
        elif "PARTIAL-LENGTH" in user: text, fin = "partial", "length"
        else: text, fin = "ok", "stop"
        def chunk(delta, finish):
            return "data: " + json.dumps({"id": "x", "object": "chat.completion.chunk", "created": 0, "model": "fake-model",
                                          "choices": [{"index": 0, "delta": delta, "finish_reason": finish}]}) + "\n\n"
        out = chunk({"role": "assistant"}, None)
        if text: out += chunk({"content": text}, None)
        out += chunk({}, fin) + "data: [DONE]\n\n"
        data = out.encode()
        self.send_response(200); self.send_header("Content-Type", "text/event-stream"); self.end_headers(); self.wfile.write(data)
srv = socketserver.TCPServer(("127.0.0.1", 0), H)
open(PORTFILE, "w").write(str(srv.server_address[1]))
srv.serve_forever()
ERPY
        python3 "$er_dir/server.py" "$er_dir/port" >/dev/null 2>&1 &
        local er_srv=$!
        local er_wait=0
        while [ ! -s "$er_dir/port" ] && [ "$er_wait" -lt 50 ]; do sleep 0.1; er_wait=$((er_wait + 1)); done
        local er_port; er_port=$(cat "$er_dir/port" 2>/dev/null || true)

        if [ -z "$er_port" ]; then
            kill "$er_srv" 2>/dev/null || true
            skip "S04E21 empty reply: against a streaming fake model" "fake server did not start"
        else
            er_run() {
                env -i HOME="$er_dir" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" \
                    AZUREOPENAIENDPOINT="https://example.openai.azure.com/" AZUREOPENAIAPI="dummy-key-0123456789" \
                    AZURE_FOUNDRY_ENDPOINT="http://127.0.0.1:$er_port" AZURE_FOUNDRY_KEY="fake-key-0123456789" \
                    AZURE_FOUNDRY_MODELS="fake-model" "$BIN" "$@"
            }
            er_case() { # <name> <expected rc> <stdout must equal> <stderr must contain ('' = nothing required)> -- args...
                local name="$1" want_rc="$2" want_out="$3" want_err="$4"; shift 5
                local out rc err
                set +e
                out=$(er_run "$@" 2>"$er_dir/err"); rc=$?
                set -e
                err=$(cat "$er_dir/err")
                if [ "$rc" = "$want_rc" ] && [ "$out" = "$want_out" ] && { [ -z "$want_err" ] || printf '%s' "$err" | grepq -F -- "$want_err"; }; then
                    pass "$name"
                else
                    fail "$name" "rc=$rc (want $want_rc) stdout='$out' stderr='$(printf '%s' "$err" | head -c 200)'"
                fi
            }

            er_case "S04E21 empty reply: output limit reached with no text is an error naming --max-tokens" \
                1 "" "--max-tokens 16" -- --raw --model fake-model --max-tokens 16 "EMPTY-LENGTH"
            er_case "S04E21 empty reply: content filter with no text is an error" \
                1 "" "content filter" -- --raw --model fake-model "EMPTY-FILTER"
            er_case "S04E21 empty reply: a model that stopped normally with nothing to say is left alone (rc 0)" \
                0 "" "" -- --raw --model fake-model "EMPTY-STOP"
            er_case "S04E21 empty reply: a truncated but non-empty answer is unchanged (rc 0, text printed)" \
                0 "partial" "" -- --raw --model fake-model --max-tokens 16 "PARTIAL-LENGTH"
            er_case "S04E21 empty reply: a normal answer is unchanged" \
                0 "ok" "" -- --raw --model fake-model "hello"
            er_case "S04E21 empty reply: --json reports the failure as a structured error" \
                1 "" '"error": true' -- --json --model fake-model --max-tokens 16 "EMPTY-LENGTH"
            kill "$er_srv" 2>/dev/null || true
        fi
        rm -rf "$er_dir"
    fi


    if ! command -v python3 >/dev/null 2>&1; then
        skip "S04E10 eval: end-to-end against a fake model server" "python3 not available"
    else
        cat > "$ev_dir/server.py" <<'EVPY'
import json, sys, http.server, socketserver
LOG, PORTFILE = sys.argv[1], sys.argv[2]
class H(http.server.BaseHTTPRequestHandler):
    def log_message(self, *a): pass
    def do_POST(self):
        body = self.rfile.read(int(self.headers.get("Content-Length", 0)) or 0).decode()
        with open(LOG, "a") as f: f.write(body + "\n")
        try: msgs = json.loads(body).get("messages", [])
        except Exception: msgs = []
        user = next((m.get("content", "") for m in reversed(msgs) if m.get("role") == "user"), "")
        user = user if isinstance(user, str) else json.dumps(user)
        if "capital of France" in user: text = "The capital of France is Paris."
        elif "17 multiplied" in user: text = "51"
        elif "single word: READY" in user: text = "READY"
        elif "JSON" in user: text = '{"ok": true}'
        elif "primary colors" in user: text = "Red, blue, yellow"
        elif "good morning" in user: text = "Buenos dias"
        elif "HTTP stand" in user: text = "HTTP stands for HyperText Transfer Protocol."
        else: text = "I cannot help with that."
        out = {"id": "x", "object": "chat.completion", "created": 0, "model": "fake-model",
               "choices": [{"index": 0, "finish_reason": "stop", "message": {"role": "assistant", "content": text}}],
               "usage": {"prompt_tokens": 1, "completion_tokens": 1, "total_tokens": 2}}
        data = json.dumps(out).encode()
        self.send_response(200); self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(data))); self.end_headers(); self.wfile.write(data)
srv = socketserver.TCPServer(("127.0.0.1", 0), H)
open(PORTFILE, "w").write(str(srv.server_address[1]))
srv.serve_forever()
EVPY
        python3 "$ev_dir/server.py" "$ev_dir/req.log" "$ev_dir/port" >/dev/null 2>&1 &
        local ev_srv=$!
        local ev_wait=0
        while [ ! -s "$ev_dir/port" ] && [ "$ev_wait" -lt 50 ]; do sleep 0.1; ev_wait=$((ev_wait + 1)); done
        local ev_port; ev_port=$(cat "$ev_dir/port" 2>/dev/null || true)

        if [ -z "$ev_port" ]; then
            kill "$ev_srv" 2>/dev/null || true
            skip "S04E10 eval: end-to-end against a fake model server" "fake server did not start"
        else
            ev_run() {
                env -i HOME="$ev_dir" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" \
                    AZUREOPENAIENDPOINT="https://example.openai.azure.com/" AZUREOPENAIAPI="dummy-key-0123456789" \
                    AZURE_FOUNDRY_ENDPOINT="http://127.0.0.1:$ev_port" AZURE_FOUNDRY_KEY="fake-key-0123456789" \
                    AZURE_FOUNDRY_MODELS="fake-model" "$BIN" "$@" 2>&1
            }

            set +e
            ev_out=$(ev_run eval "$ev_dir/suite.json" --model fake-model)
            ev_rc=$?
            set -e
            if [ "$ev_rc" -eq 0 ] && printf '%s' "$ev_out" | grepq -F 'Result: PASS' \
                && printf '%s' "$ev_out" | grepq -F 'Score: 100%'; then
                pass "S04E10 eval: end to end passes against a fake model (rc 0)"
            else
                fail "S04E10 eval: end to end passes against a fake model (rc 0)" "rc=$ev_rc out=$ev_out"
            fi

            # What reached the wire: 3 requests, temperature 0, system+user roles, safety clause present.
            local ev_reqs; ev_reqs=$(wc -l < "$ev_dir/req.log" | tr -d ' ')
            if [ "$ev_reqs" = "3" ] \
                && head -1 "$ev_dir/req.log" | grepq -F '"temperature":0' \
                && head -1 "$ev_dir/req.log" | grepq -F 'exfiltrate secrets' \
                && head -1 "$ev_dir/req.log" | grepq -F '"role":"system"'; then
                pass "S04E10 eval: system prompt, safety clause and temperature 0 reached the wire"
            else
                fail "S04E10 eval: system prompt, safety clause and temperature 0 reached the wire" \
                    "requests=$ev_reqs first=$(head -1 "$ev_dir/req.log" | cut -c1-300)"
            fi

            : > "$ev_dir/req.log"
            ev_out=$(ev_run eval "$ev_dir/suite.json" --model fake-model --filter capital)
            ev_reqs=$(wc -l < "$ev_dir/req.log" | tr -d ' ')
            if [ "$ev_reqs" = "1" ] && printf '%s' "$ev_out" | grepq -F 'capital'; then
                pass "S04E10 eval: --filter runs only the matching fixture"
            else
                fail "S04E10 eval: --filter runs only the matching fixture" "requests=$ev_reqs out=$ev_out"
            fi

            ev_out=$(ev_run eval "$ev_dir/suite.json" --model fake-model --json)
            if printf '%s' "$ev_out" | grepq -F '"result": "pass"' && printf '%s' "$ev_out" | grepq -F '"score_percent": 100'; then
                pass "S04E10 eval: --json emits the envelope"
            else
                fail "S04E10 eval: --json emits the envelope" "out=$ev_out"
            fi

            # A suite the fake model gets wrong must exit 1, so CI can gate on it.
            cat > "$ev_dir/failing.json" <<'EVFAIL'
[{"id":"wrong","user_prompt":"What is the capital of France?","assertions":[{"type":"contains","value":"Berlin"}]}]
EVFAIL
            set +e
            ev_out=$(ev_run eval "$ev_dir/failing.json" --model fake-model)
            ev_rc=$?
            set -e
            if [ "$ev_rc" -eq 1 ] && printf '%s' "$ev_out" | grepq -F 'Result: FAIL'; then
                pass "S04E10 eval: a failing suite exits 1"
            else
                fail "S04E10 eval: a failing suite exits 1" "rc=$ev_rc out=$ev_out"
            fi

            # The embedded corpora, run through the published binary against the fake model.
            : > "$ev_dir/req.log"
            set +e
            ev_out=$(ev_run eval --corpus smoke --model fake-model)
            ev_rc=$?
            set -e
            ev_reqs=$(wc -l < "$ev_dir/req.log" | tr -d ' ')
            if [ "$ev_rc" -eq 0 ] && [ "$ev_reqs" = "7" ] && printf '%s' "$ev_out" | grepq -F 'Suite: smoke (7 fixtures' \
                && printf '%s' "$ev_out" | grepq -F 'Result: PASS'; then
                pass "S04E11 corpus: the embedded smoke corpus passes end to end (7 requests)"
            else
                fail "S04E11 corpus: the embedded smoke corpus passes end to end (7 requests)" "rc=$ev_rc requests=$ev_reqs out=$ev_out"
            fi

            # The fake model refuses everything, so the refusal corpus (all safety fixtures) must pass.
            set +e
            ev_out=$(ev_run eval --corpus refusal --model fake-model)
            ev_rc=$?
            set -e
            if [ "$ev_rc" -eq 0 ] && printf '%s' "$ev_out" | grepq -F 'Suite: refusal (8 fixtures' \
                && printf '%s' "$ev_out" | grepq -F '[safety]'; then
                pass "S04E11 corpus: the refusal corpus passes against a model that refuses"
            else
                fail "S04E11 corpus: the refusal corpus passes against a model that refuses" "rc=$ev_rc out=$ev_out"
            fi

            # The fake model does not answer in persona voice, so persona-voice must FAIL (exit 1) and
            # must report that personas came from the built-in defaults (no .squad.json in this HOME).
            # BIN can be a relative path in CI, and this case changes directory, so resolve it first.
            local ev_abs; ev_abs="$(cd "$(dirname "$BIN")" && pwd)/$(basename "$BIN")"
            set +e
            ev_out=$(cd "$ev_dir" && env -i HOME="$ev_dir" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" \
                AZUREOPENAIENDPOINT="https://example.openai.azure.com/" AZUREOPENAIAPI="dummy-key-0123456789" \
                AZURE_FOUNDRY_ENDPOINT="http://127.0.0.1:$ev_port" AZURE_FOUNDRY_KEY="fake-key-0123456789" \
                AZURE_FOUNDRY_MODELS="fake-model" "$ev_abs" eval --corpus persona-voice --model fake-model 2>&1)
            ev_rc=$?
            set -e
            if [ "$ev_rc" -eq 1 ] && printf '%s' "$ev_out" | grepq -F 'Personas: built-in defaults'; then
                pass "S04E11 corpus: persona-voice fails a model with no voice and reports built-in personas"
            else
                fail "S04E11 corpus: persona-voice fails a model with no voice and reports built-in personas" "rc=$ev_rc out=$ev_out"
            fi

            # ── S04E13 control: prove this harness CAN see telemetry. A normal prompt run with telemetry on
            # must emit events (so the eval-run check below cannot pass merely because nothing is visible),
            # and even those events must not carry the prompt text.
            set +e
            ev_ctl=$(env -i HOME="$ev_dir" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" AZ_AI_TELEMETRY=1 AZ_TELEMETRY=1 \
                AZUREOPENAIENDPOINT="https://example.openai.azure.com/" AZUREOPENAIAPI="dummy-key-0123456789" \
                AZURE_FOUNDRY_ENDPOINT="http://127.0.0.1:$ev_port" AZURE_FOUNDRY_KEY="fake-key-0123456789" \
                AZURE_FOUNDRY_MODELS="fake-model" "$ev_abs" --telemetry --model fake-model "What is the capital of France?" 2>&1 >/dev/null)
            set -e
            if printf '%s' "$ev_ctl" | grepq -F '"dispatch_path"' && ! printf '%s' "$ev_ctl" | grepq -F 'capital of France'; then
                pass "S04E13 telemetry control: a normal run emits telemetry events and none carry the prompt text"
            else
                fail "S04E13 telemetry control: a normal run emits telemetry events and none carry the prompt text" "stderr=$(printf '%s' "$ev_ctl" | head -c 400)"
            fi

            # ── S04E13: an eval run with telemetry ON must not emit prompt or output text ──
            # The smoke corpus prompts and the fake model's answers contain distinctive phrases; none may
            # appear on stdout or stderr beyond what the report itself prints (ids, statuses, scores).
            set +e
            ev_out=$(env -i HOME="$ev_dir" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" AZ_AI_TELEMETRY=1 AZ_TELEMETRY=1 \
                AZUREOPENAIENDPOINT="https://example.openai.azure.com/" AZUREOPENAIAPI="dummy-key-0123456789" \
                AZURE_FOUNDRY_ENDPOINT="http://127.0.0.1:$ev_port" AZURE_FOUNDRY_KEY="fake-key-0123456789" \
                AZURE_FOUNDRY_MODELS="fake-model" "$ev_abs" eval --corpus smoke --model fake-model --telemetry 2>&1)
            ev_rc=$?
            set -e
            if [ "$ev_rc" -eq 0 ] \
                && ! printf '%s' "$ev_out" | grepq -F 'capital of France' \
                && ! printf '%s' "$ev_out" | grepq -F 'HyperText Transfer Protocol' \
                && ! printf '%s' "$ev_out" | grepq -F 'Reply with a JSON object' \
                && ! printf '%s' "$ev_out" | grepq -F 'dummy-key-0123456789' \
                && ! printf '%s' "$ev_out" | grepq -F 'fake-key-0123456789'; then
                pass "S04E13 telemetry: an eval run with telemetry on emits no prompt, output or key text"
            else
                fail "S04E13 telemetry: an eval run with telemetry on emits no prompt, output or key text" "rc=$ev_rc out=$(printf '%s' "$ev_out" | head -c 600)"
            fi

            # ── S04E12 -- The Gate (scripts/eval-gate.sh) ─────────────────────────
            printf '{"repeat":2,"corpora":[{"name":"smoke","fail_under":100},{"name":"refusal","fail_under":100}]}' > "$ev_dir/policy_ok.json"
            printf '{"repeat":1,"corpora":[{"name":"persona-voice","fail_under":80}]}' > "$ev_dir/policy_voice.json"
            gate_run() {
                env -i HOME="$ev_dir" PATH="$PATH" DOTNET_ROOT="${DOTNET_ROOT:-}" BIN="$ev_abs" OUT="$ev_dir/gate-out" \
                    BASELINE_DIR="$ev_dir/baselines" "$@" bash scripts/eval-gate.sh "${GATE_MODE:-run}" 2>&1
            }
            gate_creds() {
                echo AZUREOPENAIENDPOINT="https://example.openai.azure.com/" AZUREOPENAIAPI="dummy-key-0123456789" \
                    AZURE_FOUNDRY_ENDPOINT="http://127.0.0.1:$ev_port" AZURE_FOUNDRY_KEY="fake-key-0123456789" \
                    AZURE_FOUNDRY_MODELS="fake-model" EVAL_GATE_MODEL=fake-model
            }

            # Control case 1: a REQUIRED gate that cannot run must FAIL, never go green.
            set +e
            ev_out=$(gate_run POLICY="$ev_dir/policy_ok.json" EVAL_GATE_REQUIRED=true)
            ev_rc=$?
            set -e
            if [ "$ev_rc" -eq 1 ] && printf '%s' "$ev_out" | grepq -F 'Refusing to report green without running'; then
                pass "S04E12 gate: a required gate with no credentials FAILS (never green)"
            else
                fail "S04E12 gate: a required gate with no credentials FAILS (never green)" "rc=$ev_rc out=$ev_out"
            fi

            # Control case 2: an optional gate that cannot run SKIPS visibly and says it is not a pass.
            set +e
            ev_out=$(gate_run POLICY="$ev_dir/policy_ok.json")
            ev_rc=$?
            set -e
            if [ "$ev_rc" -eq 0 ] && printf '%s' "$ev_out" | grepq -F 'SKIPPED' && printf '%s' "$ev_out" | grepq -F 'This is not a pass'; then
                pass "S04E12 gate: an optional gate with no credentials SKIPS visibly (not a pass)"
            else
                fail "S04E12 gate: an optional gate with no credentials SKIPS visibly (not a pass)" "rc=$ev_rc out=$ev_out"
            fi

            : > "$ev_dir/req.log"
            set +e
            # shellcheck disable=SC2046
            ev_out=$(gate_run POLICY="$ev_dir/policy_ok.json" $(gate_creds))
            ev_rc=$?
            set -e
            ev_reqs=$(wc -l < "$ev_dir/req.log" | tr -d ' ')
            if [ "$ev_rc" -eq 0 ] && [ "$ev_reqs" = "30" ] && printf '%s' "$ev_out" | grepq -F 'eval-gate: OK'; then
                pass "S04E12 gate: smoke and refusal pass with --repeat 2 (30 requests: 7x2 + 8x2)"
            else
                fail "S04E12 gate: smoke and refusal pass with --repeat 2 (30 requests: 7x2 + 8x2)" "rc=$ev_rc requests=$ev_reqs out=$ev_out"
            fi

            set +e
            # shellcheck disable=SC2046
            ev_out=$(gate_run POLICY="$ev_dir/policy_voice.json" $(gate_creds))
            ev_rc=$?
            set -e
            if [ "$ev_rc" -eq 1 ] && printf '%s' "$ev_out" | grepq -F 'did not pass'; then
                pass "S04E12 gate: persona-voice against a model with no voice fails the gate"
            else
                fail "S04E12 gate: persona-voice against a model with no voice fails the gate" "rc=$ev_rc out=$ev_out"
            fi

            # Baseline round trip: record, then re-run against it with no regressions.
            set +e
            # shellcheck disable=SC2046
            # The mode is exported INSIDE the substitution so it cannot leak into the next call.
            ev_out=$(export GATE_MODE=baseline; gate_run POLICY="$ev_dir/policy_ok.json" $(gate_creds))
            ev_rc=$?
            # shellcheck disable=SC2046
            ev_out2=$(gate_run POLICY="$ev_dir/policy_ok.json" $(gate_creds))
            ev_rc2=$?
            set -e
            if [ "$ev_rc" -eq 0 ] && [ -f "$ev_dir/baselines/fake-model.smoke.json" ] && [ "$ev_rc2" -eq 0 ] \
                && printf '%s' "$ev_out2" | grepq -F '0 regression(s)'; then
                pass "S04E12 gate: baselines are recorded and the next run compares with 0 regressions"
            else
                fail "S04E12 gate: baselines are recorded and the next run compares with 0 regressions" \
                    "rc=$ev_rc rc2=$ev_rc2 out2=$ev_out2"
            fi

            # A baseline that says persona-voice fixtures all PASSED before must make today's failures regressions.
            python3 - "$ev_dir/baselines/fake-model.persona-voice.json" "$ev_dir/policy_voice.json" <<'PYB'
import json, re, subprocess, sys
src = open("azureopenai-cli/Eval/Corpora/persona-voice.json").read()
ids = re.findall(r'"id":\s*"(voice-[a-z-]+)"', src)
json.dump({"suite": "persona-voice", "model": "fake-model", "repeat": 1,
           "fixtures": {i: {"status": "pass", "runs": 1, "passed_runs": 1} for i in ids}}, open(sys.argv[1], "w"))
PYB
            set +e
            # shellcheck disable=SC2046
            ev_out=$(gate_run POLICY="$ev_dir/policy_voice.json" $(gate_creds))
            ev_rc=$?
            set -e
            if [ "$ev_rc" -eq 1 ] && printf '%s' "$ev_out" | grepq -F 'regression against baseline'; then
                pass "S04E12 gate: fixtures that passed in the baseline and now fail are regressions"
            else
                fail "S04E12 gate: fixtures that passed in the baseline and now fail are regressions" "rc=$ev_rc out=$ev_out"
            fi

            kill "$ev_srv" 2>/dev/null || true
            wait "$ev_srv" 2>/dev/null || true
        fi
    fi
    rm -rf "$ev_dir"

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
    if [ "$rot_rc" -eq 3 ] && printf '%s' "$rot_out" | grepq -F '[ERROR]'; then
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
        if printf '%s' "$doc_out" | grepq -F 'sk-new-rotation-test'; then
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
        if ! printf '%s' "$tel_event" | grepq -F "$f"; then
            missing="$missing $f"
        fi
    done
    if [ -z "$missing" ]; then
        pass "S03E13 telemetry: event has all expected schema fields"
    else
        fail "S03E13 telemetry: event has all expected schema fields" "missing:$missing event=$tel_event"
    fi

    # Privacy guarantee: bogus key value MUST NOT appear in the event line.
    if printf '%s' "$tel_event" | grepq -F 'sk-not-a-real-key'; then
        fail "S03E13 telemetry: event must not leak API key" "leaked: $tel_event"
    else
        pass "S03E13 telemetry: event does not leak API key"
    fi
    # Endpoint hostname likewise must not appear in the event payload.
    if printf '%s' "$tel_event" | grepq -F 'invalid.example.invalid'; then
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
    if printf '%s\n' "$tel_stderr_off" | grepq '^{"event_id":'; then
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
    if printf '%s\n' "$tel_zero" | grepq '^{"event_id":'; then
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
    if printf '%s' "$doc_out" | grepq -E 'Bearer [A-Za-z0-9._-]+|sk-[A-Za-z0-9]{8,}'; then
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
       printf '%s' "$off_doctor_out" | grepq 'blocked-offline'; then
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
       printf '%s' "$off_env_out" | grepq 'blocked-offline'; then
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
    if printf '%s' "$off_lax_out" | grepq 'blocked-offline'; then
        fail "S03E26 offline: AZ_AI_OFFLINE=true must NOT enable offline" \
            "lax env activated offline gate (strict-equality '1' is required)"
    else
        pass "S03E26 offline: AZ_AI_OFFLINE=true does not enable (strict-equality '1' only)"
    fi

    # 7. Secret-shape leak guard: offline error path must not emit the
    #    AZUREOPENAIAPI value or any sk-... token.
    if printf '%s' "$off_doctor_out" | grepq -E 'Bearer [A-Za-z0-9._-]+|sk-[A-Za-z0-9]{8,}'; then
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
       printf '%s' "$cg_tool_out" | grepq 'does not support tool_calls'; then
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
       ! printf '%s' "$cg_ovr_out" | grepq 'does not support tool_calls'; then
        pass "S03E18 capability gate: AZ_AI_CAPABILITY_OVERRIDES flips tool_calls past gate"
    else
        fail "S03E18 capability gate: override path" \
            "rc=$cg_ovr_rc out=$(printf '%s' "$cg_ovr_out" | head -c 240)"
    fi

    # 4. Error message names the override env var so the user can self-rescue.
    if printf '%s' "$cg_tool_out" | grepq 'AZ_AI_CAPABILITY_OVERRIDES'; then
        pass "S03E18 capability gate: error message names AZ_AI_CAPABILITY_OVERRIDES"
    else
        fail "S03E18 capability gate: actionable hint" \
            "tool-call refusal did not mention AZ_AI_CAPABILITY_OVERRIDES"
    fi

    # 5. Capability-gate refusal must NOT leak the API key.
    if printf '%s' "$cg_tool_out" | grepq -E 'Bearer [A-Za-z0-9._-]+|sk-not-real'; then
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
       printf '%s' "$sw_show_out" | grepq -F 'Switch resolution (S03E20):' && \
       printf '%s' "$sw_show_out" | grepq -E '^\s*source:\s+'; then
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
    if printf '%s' "$sw_cli_out" | grepq -E 'provider source:\s+cli'; then
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
    if printf '%s' "$sw_prof_out" | grepq -F 'profile:work:provider' && \
       printf '%s' "$sw_prof_out" | grepq -F 'profile:work:model'; then
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
       printf '%s' "$sw_miss_out" | grepq -F "'production'" && \
       printf '%s' "$sw_miss_out" | grepq -F 'Available profiles:' && \
       printf '%s' "$sw_miss_out" | grepq 'work' && \
       printf '%s' "$sw_miss_out" | grepq 'ci'; then
        pass "S03E20 switch: missing profile lists available names"
    else
        fail "S03E20 switch: missing profile message" "rc=$sw_miss_rc out=$(printf '%s' "$sw_miss_out" | head -c 320)"
    fi

    # 5. --provider, --profile, --model appear in --help.
    local sw_help_out
    sw_help_out=$("$BIN" --help 2>&1)
    if printf '%s' "$sw_help_out" | grepq -Fe '--provider <name>' && \
       printf '%s' "$sw_help_out" | grepq -Fe '--profile <name>'; then
        pass "S03E20 switch: --help documents --provider and --profile"
    else
        fail "S03E20 switch: --help doc" "help missing --provider/--profile; captured ${#sw_help_out} chars, starting: $(printf '%s' "$sw_help_out" | head -c 160 | tr '\n' ' ')"
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
    if printf '%s' "$sw_env_out" | grepq -F 'profile:ci:provider' && \
       printf '%s' "$sw_env_out" | grepq -F 'env:AZ_PROFILE'; then
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
    if printf '%s' "$def1_out" | grepq -E 'provider source:[[:space:]]+default:azure(\b|$)'; then
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
    if printf '%s' "$def2_out" | grepq -E 'provider source:[[:space:]]+default:groq(\b|$)'; then
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
    if printf '%s' "$def5_out" | grepq -E 'provider source:[[:space:]]+default:cloudflare(\b|$)'; then
        pass "S03E22 default: rung 5 tie-break picks alphabetical first (cloudflare)"
    else
        fail "S03E22 default: rung 5 tie-break" "out=$(printf '%s' "$def5_out" | head -c 320)"
    fi
    if printf '%s' "$def5_out" | grepq -F 'multiple-presets-no-cli-no-profile-no-env-pin'; then
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
    if printf '%s' "$def_pin_out" | grepq -E 'provider source:[[:space:]]+env:AZ_PROVIDER(\b|$)'; then
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
    if printf '%s' "$def6_out" | grepq -E 'provider source:[[:space:]]+default:azure:fallback(\b|$)'; then
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
    if printf '%s' "$lc_doc_out" | grepq 'compat:llamacpp' && \
       printf '%s' "$lc_doc_out" | grepq 'localhost:8080'; then
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
    if printf '%s' "$lc_json_out" | grepq 'compat:llamacpp'; then
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
       printf '%s' "$lc_gate_out" | grepq 'AZ_AI_CAPABILITY_OVERRIDES'; then
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
       printf '%s' "$lc_block_out" | grepq 'AZ_AI_LOCAL_PROVIDERS'; then
        pass "S03E17 server: loopback gate refuses llamacpp without AZ_AI_LOCAL_PROVIDERS=1 (rc=$lc_block_rc)"
    else
        fail "S03E17 server: loopback gate llamacpp" \
            "rc=$lc_block_rc out=$(printf '%s' "$lc_block_out" | head -c 320)"
    fi

    # ── S03E22 *The Fallback* (Frank Costanza) ────────────────────────────
    echo "▸ S03E22 fallback chain (--fallback opt-in)"

    # 1. --help mentions --fallback (discoverable).
    if "$BIN" --help 2>&1 | grepq -- "--fallback"; then
        pass "S03E22 fallback: --help mentions --fallback"
    else
        fail "S03E22 fallback: --help missing --fallback" "help text didn't mention the flag"
    fi

    # 2. unknown preset -> exit 2 + lists known presets on stderr.
    # No dummy creds needed: fallback parsing fires before the creds check.
    fb_unknown_out=$("$BIN" --fallback bogus -- "hi" 2>&1 1>/dev/null || true)
    fb_unknown_rc=$("$BIN" --fallback bogus -- "hi" >/dev/null 2>&1; echo $?)
    if [ "$fb_unknown_rc" = "2" ] && \
       printf '%s' "$fb_unknown_out" | grepq -i "unknown fallback provider" && \
       printf '%s' "$fb_unknown_out" | grepq "openai"; then
        pass "S03E22 fallback: unknown preset exits 2 + lists known"
    else
        fail "S03E22 fallback: unknown preset" "rc=$fb_unknown_rc out=$(printf '%s' "$fb_unknown_out" | head -c 240)"
    fi

    # 3. depth >3 -> exit 2 + "exceeds max" message.
    fb_depth_out=$("$BIN" --fallback openai,groq,together,cloudflare -- "hi" 2>&1 1>/dev/null || true)
    fb_depth_rc=$("$BIN" --fallback openai,groq,together,cloudflare -- "hi" >/dev/null 2>&1; echo $?)
    if [ "$fb_depth_rc" = "2" ] && printf '%s' "$fb_depth_out" | grepq -i "exceeds the maximum"; then
        pass "S03E22 fallback: depth>3 exits 2 with max-depth message"
    else
        fail "S03E22 fallback: depth>3" "rc=$fb_depth_rc out=$(printf '%s' "$fb_depth_out" | head -c 240)"
    fi

    # 4. duplicate preset -> exit 2 + "duplicate" message.
    fb_dup_out=$("$BIN" --fallback openai,openai -- "hi" 2>&1 1>/dev/null || true)
    fb_dup_rc=$("$BIN" --fallback openai,openai -- "hi" >/dev/null 2>&1; echo $?)
    if [ "$fb_dup_rc" = "2" ] && printf '%s' "$fb_dup_out" | grepq -i "more than once"; then
        pass "S03E22 fallback: duplicate preset exits 2"
    else
        fail "S03E22 fallback: duplicate" "rc=$fb_dup_rc out=$(printf '%s' "$fb_dup_out" | head -c 240)"
    fi

    # 5. --fallback without value -> Fail() exits 2 with helpful message.
    fb_noval_out=$("$BIN" --fallback 2>&1 1>/dev/null || true)
    fb_noval_rc=$("$BIN" --fallback >/dev/null 2>&1; echo $?)
    if [ "$fb_noval_rc" != "0" ] && printf '%s' "$fb_noval_out" | grepq "requires a comma-separated"; then
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
    if printf '%s' "$pmp_list_out" | grepq "coder" && \
       printf '%s' "$pmp_list_out" | grepq "reviewer"; then
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
       printf '%s' "$pmp_bad_out" | grepq "anthropic" && \
       printf '%s' "$pmp_bad_out" | grepq -i "known providers"; then
        pass "S03E23 persona pin: unknown provider rejected at load (rc=$pmp_bad_rc)"
    else
        fail "S03E23 persona pin: unknown provider" \
             "rc=$pmp_bad_rc out=$(printf '%s' "$pmp_bad_out" | head -c 320)"
    fi

    # 3. Bad config error names the offending persona so the operator can
    #    grep the file straight to it.
    if printf '%s' "$pmp_bad_out" | grepq "kramer"; then
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
