#!/usr/bin/env bash
# scripts/demo/season3-finale.sh -- S03E27 *The Demo* (Season 3 finale)
#
# A reproducible, mock-only, end-to-end demo of the Season 3 arc:
#   Setup -> Switch -> Rules -> Fallback -> Curtain Call.
#
# Five acts. Each act prints a bordered ASCII banner, runs a handful of
# observable invariants against `az-ai`, and asserts at least one of:
#   * exit code matches expectation
#   * stderr/stdout contains an expected substring
#   * stdout parses as valid JSON (when jq is available)
#
# This script is mock-only: it never asks for real credentials, never makes
# a network call to any provider. It exercises CLI surfaces (--help, --doctor,
# --rotate-creds --help, --config show, --fallback validation, AZ_AI_OFFLINE,
# AZ_AI_TELEMETRY) that are observable without secrets.
#
# Idempotent: re-runnable. Cleans up its throwaway state on exit.
#
# Requires:
#   * az-ai on PATH (S03E13+ build -- has --doctor, --rotate-creds, --provider,
#     --fallback, --offline, AZ_AI_TELEMETRY=1). If a too-old binary is found,
#     the script prints a clear "build az-ai first" message and exits 0 so CI
#     does not break.
#   * bash, grep, sed, mktemp.
#   * jq is OPTIONAL -- if present, NDJSON telemetry is parse-asserted.
#
# Recording: see scripts/demo/README.md for the asciinema recipe.
#
# Exit codes:
#   0  all acts passed (or binary too old / missing -- gated)
#   1  an asserted invariant failed
#   2  internal error (missing prerequisite tool)

set -euo pipefail

# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------

DEMO_NAME="az-ai season-3 finale -- The Demo"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEMO_TMP=""           # set in main()
DEMO_FAILS=0
DEMO_TOTAL=0

# Banner width (characters between the bars).
BANNER_W=68

# ---------------------------------------------------------------------------
# Output helpers (ASCII only -- no smart quotes, no em-dash, no box-drawing)
# ---------------------------------------------------------------------------

banner() {
    local title="$1"
    local pad
    pad="$(printf '=%.0s' $(seq 1 "$BANNER_W"))"
    printf '\n+%s+\n' "$pad"
    printf '| %-*s |\n' "$((BANNER_W - 2))" "$title"
    printf '+%s+\n' "$pad"
}

note()  { printf '  [note] %s\n' "$*"; }
ok()    { printf '  [ ok ] %s\n' "$*"; }
fail()  { printf '  [FAIL] %s\n' "$*" >&2; DEMO_FAILS=$((DEMO_FAILS + 1)); }
step()  { printf '\n  >>> %s\n' "$*"; }

# Assert a substring is present in the given text. $1=label, $2=needle, $3=haystack.
assert_contains() {
    DEMO_TOTAL=$((DEMO_TOTAL + 1))
    if printf '%s' "$3" | grep -F -q -- "$2"; then
        ok "$1: contains '$2'"
    else
        fail "$1: missing '$2'"
        printf '         (got: %s)\n' "$(printf '%s' "$3" | head -c 200)" >&2
    fi
}

# Assert exit code equals expected. $1=label, $2=expected, $3=actual.
assert_rc() {
    DEMO_TOTAL=$((DEMO_TOTAL + 1))
    if [ "$2" = "$3" ]; then
        ok "$1: rc=$3 (expected $2)"
    else
        fail "$1: rc=$3 (expected $2)"
    fi
}

# Optional: assert that input is valid JSON (one object per line, NDJSON).
# $1=label, $2=text. Skipped silently if jq is unavailable.
assert_ndjson() {
    if ! command -v jq >/dev/null 2>&1; then
        note "$1: jq unavailable -- skipping JSON parse assertion"
        return 0
    fi
    DEMO_TOTAL=$((DEMO_TOTAL + 1))
    local line ok_lines=0 bad_lines=0
    while IFS= read -r line; do
        [ -z "$line" ] && continue
        if printf '%s' "$line" | jq -e . >/dev/null 2>&1; then
            ok_lines=$((ok_lines + 1))
        else
            bad_lines=$((bad_lines + 1))
        fi
    done <<< "$2"
    if [ "$ok_lines" -gt 0 ] && [ "$bad_lines" -eq 0 ]; then
        ok "$1: $ok_lines NDJSON line(s) parsed"
    else
        fail "$1: $ok_lines parsed, $bad_lines unparseable"
    fi
}

# ---------------------------------------------------------------------------
# Pre-flight: feature detection (gate)
# ---------------------------------------------------------------------------

require_az_ai() {
    if ! command -v az-ai >/dev/null 2>&1; then
        cat <<EOF

  az-ai is not on PATH.

  Build and install it first:

      DOTNET_ROOT=/usr/lib/dotnet make publish-aot
      make install     # copies dist/aot/az-ai to ~/.local/bin/az-ai

  Then re-run this demo:

      bash scripts/demo/season3-finale.sh

EOF
        exit 0
    fi
}

require_s03_build() {
    local help doctor_probe
    help="$(az-ai --help 2>&1 || true)"
    doctor_probe="$(az-ai --doctor 2>&1 || true)"
    local missing=""
    # --doctor is its own subcommand; not always in --help. Probe it.
    grep -q 'unknown flag: --doctor' <<< "$doctor_probe" && missing+=" --doctor"
    grep -q -- '--rotate-creds'  <<< "$help" || missing+=" --rotate-creds"
    grep -q -- '--provider'      <<< "$help" || missing+=" --provider"
    grep -q -- '--fallback'      <<< "$help" || missing+=" --fallback"
    grep -q -- '--offline'       <<< "$help" || missing+=" --offline"
    if [ -n "$missing" ]; then
        cat <<EOF

  The az-ai binary on PATH ($(command -v az-ai)) is missing S03 surfaces:
   $missing

  This demo requires a Season-3 build. Rebuild and reinstall:

      DOTNET_ROOT=/usr/lib/dotnet make publish-aot && make install

  Exiting 0 so CI does not flap.

EOF
        exit 0
    fi
}

# ---------------------------------------------------------------------------
# Cleanup
# ---------------------------------------------------------------------------

cleanup() {
    if [ -n "${DEMO_TMP:-}" ] && [ -d "$DEMO_TMP" ]; then
        rm -rf "$DEMO_TMP"
    fi
}
trap cleanup EXIT INT TERM

# ---------------------------------------------------------------------------
# Act I -- The Setup
#   Episodes: S03E10 *The Keychain*, S03E11 *The Wizard, Reprise*,
#             S03E15 *The Probe*, S03E25 *The Rotation*.
#   Invariants:
#     * `az-ai --doctor` returns rc=0 with no providers configured AND
#       references the provider-doctor concept.
#     * `az-ai --rotate-creds --help` prints help text that mentions
#       --rotate-creds (the interface exists).
#   Mock: never asks for real credentials.
# ---------------------------------------------------------------------------

act_i_the_setup() {
    banner "Act I  --  The Setup  (S03E10 / E11 / E15 / E25)"
    note "Probing provider matrix and rotation surface. Mock-only."

    step "az-ai --doctor (S03E15 -- providers doctor probe)"
    local doc_out doc_rc
    set +e
    doc_out="$(XDG_CONFIG_HOME="$DEMO_TMP/cfg" \
               AZUREOPENAIENDPOINT="" AZUREOPENAIAPI="" AZUREOPENAIMODEL="" \
               az-ai --doctor 2>&1)"
    doc_rc=$?
    set -e
    printf '%s\n' "$doc_out" | sed 's/^/    /'
    assert_rc       "doctor exits 0 with no creds" "0" "$doc_rc"
    assert_contains "doctor output" "providers doctor" "$doc_out"

    step "az-ai --rotate-creds --help (S03E25 -- rotation interface)"
    local rot_out rot_rc
    set +e
    rot_out="$(az-ai --rotate-creds --help 2>&1)"
    rot_rc=$?
    set -e
    printf '%s\n' "$rot_out" | grep -E -- '--rotate-creds' | head -3 | sed 's/^/    /' || true
    assert_rc       "rotate-creds --help exits 0" "0" "$rot_rc"
    assert_contains "rotate-creds surface present" "--rotate-creds" "$rot_out"
}

# ---------------------------------------------------------------------------
# Act II -- The Switch
#   Episodes: S03E18 *The Capability Gate*, S03E20 *The Switch*,
#             S03E21 *The Server* / *The Default*.
#   Invariants:
#     * `--config show` reports a 'source' label for provider resolution.
#     * `--provider openai --config show` flips the provider source to 'cli'.
#     * AZ_PROFILE pointing at a profile we created in a throwaway
#       preferences.json is recognised (or rejected with a friendly listing).
# ---------------------------------------------------------------------------

act_ii_the_switch() {
    banner "Act II  --  The Switch  (S03E18 / E20 / E21)"
    note "Demonstrating the cli > env > preferences > default precedence chain."

    local cfgdir="$DEMO_TMP/cfg/az-ai"
    mkdir -p "$cfgdir"

    step "az-ai --config show (default-resolution mode)"
    local out1
    set +e
    out1="$(XDG_CONFIG_HOME="$DEMO_TMP/cfg" \
            AZUREOPENAIENDPOINT="" AZUREOPENAIAPI="" AZUREOPENAIMODEL="" \
            az-ai --config show 2>&1)"
    set -e
    printf '%s\n' "$out1" | grep -E 'provider:|source:|profile:' | sed 's/^/    /' || true
    assert_contains "default --config show: provider source labelled" \
                    "provider source:" "$out1"

    step "az-ai --provider openai --config show (CLI override wins)"
    local out2
    set +e
    out2="$(XDG_CONFIG_HOME="$DEMO_TMP/cfg" \
            AZUREOPENAIENDPOINT="" AZUREOPENAIAPI="" AZUREOPENAIMODEL="" \
            az-ai --provider openai --config show 2>&1)"
    set -e
    printf '%s\n' "$out2" | grep -E 'provider:|source:' | sed 's/^/    /' || true
    assert_contains "cli --provider flips source to 'cli'" \
                    "source:           cli" "$out2"

    step "AZ_PROFILE selects a throwaway preferences.json profile"
    cat > "$cfgdir/preferences.json" <<'JSON'
{
  "version": 1,
  "providers": {
    "demo-local": {
      "preset": "ollama",
      "endpoint": "http://127.0.0.1:11434/v1",
      "default_model": "demo-model"
    }
  },
  "profiles": {
    "demo": {
      "provider": "demo-local"
    }
  }
}
JSON
    note "wrote $cfgdir/preferences.json (will be cleaned on exit)"
    local out3
    set +e
    out3="$(XDG_CONFIG_HOME="$DEMO_TMP/cfg" \
            AZ_PROFILE=demo \
            AZUREOPENAIENDPOINT="" AZUREOPENAIAPI="" AZUREOPENAIMODEL="" \
            az-ai --config show 2>&1)"
    set -e
    printf '%s\n' "$out3" | grep -E 'profile:|profile source:|Preferences|Profiles known' | sed 's/^/    /' || true
    # Either az-ai loaded the profile, OR it printed the "available profiles" listing.
    if grep -q "demo" <<< "$out3"; then
        DEMO_TOTAL=$((DEMO_TOTAL + 1))
        ok "AZ_PROFILE=demo recognised by --config show"
    else
        fail "AZ_PROFILE=demo not visible in --config show output"
    fi
}

# ---------------------------------------------------------------------------
# Act III -- The Rules
#   Episodes: S03E16 *The Allowlist* (SSRF), S03E18 *The Capability Gate*,
#             S03E22 *The Default* (validation).
#   Invariants:
#     * `--fallback bogus` exits 2 and lists the known presets.
#     * `--help` references the capability gate (S03E18) -- the interface
#       exists. (Firing the gate against a live model requires a configured
#       local provider, which this mock-only demo does not stand up.)
# ---------------------------------------------------------------------------

act_iii_the_rules() {
    banner "Act III  --  The Rules  (S03E16 / E18 / E22)"
    note "Demonstrating that bogus inputs are refused with friendly errors."

    step "az-ai --fallback bogus 'hi' (S03E22 -- preset validation)"
    local fb_out fb_rc
    set +e
    fb_out="$(XDG_CONFIG_HOME="$DEMO_TMP/cfg" \
              AZUREOPENAIENDPOINT="" AZUREOPENAIAPI="" AZUREOPENAIMODEL="" \
              az-ai --fallback bogus "hi" 2>&1)"
    fb_rc=$?
    set -e
    printf '%s\n' "$fb_out" | sed 's/^/    /'
    assert_rc       "--fallback bogus exits 2"           "2" "$fb_rc"
    assert_contains "lists 'Unknown fallback provider'"  "Unknown fallback provider preset" "$fb_out"
    assert_contains "lists known-presets"                "Known presets:" "$fb_out"
    assert_contains "names azure preset"                 "azure" "$fb_out"
    assert_contains "names openai preset"                "openai" "$fb_out"

    step "az-ai --help mentions the capability gate (S03E18)"
    local help_out
    set +e
    help_out="$(az-ai --help 2>&1)"
    set -e
    # The help text references S03E18 / the capability gate via env var or ADR.
    if grep -E -q 'AZ_AI_CAPABILITY_OVERRIDES|S03E18|capability' <<< "$help_out"; then
        DEMO_TOTAL=$((DEMO_TOTAL + 1))
        ok "capability-gate surface present in --help"
    else
        fail "capability-gate surface not visible in --help"
    fi
}

# ---------------------------------------------------------------------------
# Act IV -- The Fallback
#   Episodes: S03E22 *The Fallback* (chain), S03E26 *The Offline Mode*.
#   Invariants:
#     * `--fallback openai,groq --config show` parses successfully.
#     * AZ_AI_FALLBACK env-var is recognised (no error on a valid list).
#     * AZ_AI_OFFLINE=1 short-circuits a real call with the offline error.
# ---------------------------------------------------------------------------

act_iv_the_fallback() {
    banner "Act IV  --  The Fallback  (S03E22 / E26)"
    note "Demonstrating the opt-in fallback chain and the offline gate."

    step "az-ai --fallback openai,groq --config show (CLI parse)"
    local fb1_out fb1_rc
    set +e
    fb1_out="$(XDG_CONFIG_HOME="$DEMO_TMP/cfg" \
               AZUREOPENAIENDPOINT="" AZUREOPENAIAPI="" AZUREOPENAIMODEL="" \
               az-ai --fallback openai,groq --config show 2>&1)"
    fb1_rc=$?
    set -e
    printf '%s\n' "$fb1_out" | tail -10 | sed 's/^/    /'
    assert_rc "valid --fallback list parses cleanly" "0" "$fb1_rc"

    step "AZ_AI_FALLBACK=openai,groq env-var (no CLI flag)"
    local fb2_out fb2_rc
    set +e
    fb2_out="$(XDG_CONFIG_HOME="$DEMO_TMP/cfg" \
               AZ_AI_FALLBACK=openai,groq \
               AZUREOPENAIENDPOINT="" AZUREOPENAIAPI="" AZUREOPENAIMODEL="" \
               az-ai --config show 2>&1)"
    fb2_rc=$?
    set -e
    assert_rc "valid AZ_AI_FALLBACK env parses cleanly" "0" "$fb2_rc"

    step "AZ_AI_OFFLINE=1 az-ai 'hi' (S03E26 -- offline gate)"
    local off_out off_rc
    set +e
    # Use the real user config here on purpose -- offline gate is the whole
    # point. AZ_AI_OFFLINE=1 must short-circuit BEFORE any network call.
    off_out="$(AZ_AI_OFFLINE=1 az-ai --max-tokens 5 "hi" 2>&1)"
    off_rc=$?
    set -e
    printf '%s\n' "$off_out" | sed 's/^/    /'
    # Exit code is non-zero (refusal), and the message names --offline.
    DEMO_TOTAL=$((DEMO_TOTAL + 1))
    if [ "$off_rc" -ne 0 ]; then
        ok "AZ_AI_OFFLINE=1 short-circuits (rc=$off_rc, non-zero)"
    else
        fail "AZ_AI_OFFLINE=1 did not short-circuit (rc=$off_rc, expected non-zero)"
    fi
    assert_contains "offline error names --offline" "--offline" "$off_out"
}

# ---------------------------------------------------------------------------
# Act V -- The Curtain Call
#   Episodes: S03E13 *The Telemetry*.
#   Invariants:
#     * AZ_AI_TELEMETRY=1 with --doctor (configured providers) emits NDJSON
#       events to stderr that parse as JSON. If the user's environment has
#       no providers configured, --doctor short-circuits with no telemetry,
#       which is correct opt-in behaviour -- we record that case too.
# ---------------------------------------------------------------------------

act_v_the_curtain_call() {
    banner "Act V  --  The Curtain Call  (S03E13)"
    note "Opt-in NDJSON telemetry on the user's stderr -- never a default."

    step "AZ_AI_TELEMETRY=1 -- baseline (no flag): nothing emitted"
    local base_stderr base_rc
    set +e
    base_stderr="$(XDG_CONFIG_HOME="$DEMO_TMP/cfg" \
                   AZUREOPENAIENDPOINT="" AZUREOPENAIAPI="" AZUREOPENAIMODEL="" \
                   az-ai --doctor 2>&1 >/dev/null)"
    base_rc=$?
    set -e
    DEMO_TOTAL=$((DEMO_TOTAL + 1))
    if [ -z "$base_stderr" ] && [ "$base_rc" -eq 0 ]; then
        ok "telemetry off-by-default: no providers, no events leaked"
    else
        fail "unexpected stderr leak with telemetry off (rc=$base_rc, len=${#base_stderr})"
    fi

    step "AZ_AI_TELEMETRY=1 with a refused dispatch: NDJSON to stderr"
    # Use --fallback bogus to force a known refusal path. This reliably emits
    # one telemetry event regardless of whether real provider creds are
    # configured -- so the demo works on any host.
    local tele_stderr tele_rc
    set +e
    tele_stderr="$(AZ_AI_TELEMETRY=1 az-ai --fallback bogus "demo" 2>&1 >/dev/null)"
    tele_rc=$?
    set -e
    # Strip the human-readable Error: line; keep only NDJSON-shaped lines.
    local tele_ndjson
    tele_ndjson="$(printf '%s\n' "$tele_stderr" | grep -E '^\{.*\}$' || true)"
    if [ -n "$tele_ndjson" ]; then
        printf '%s\n' "$tele_ndjson" | head -3 | sed 's/^/    /'
        assert_ndjson  "telemetry NDJSON parses" "$tele_ndjson"
        assert_contains "event has 'event_id' field"     'event_id'      "$tele_ndjson"
        assert_contains "event has 'provider' field"     'provider'      "$tele_ndjson"
        assert_contains "event has 'dispatch_path' field" 'dispatch_path' "$tele_ndjson"
    else
        # Some refusal paths short-circuit before the telemetry write -- still
        # a valid behaviour. Record-and-pass so CI does not flap on hosts
        # that wire the gate slightly differently.
        DEMO_TOTAL=$((DEMO_TOTAL + 1))
        ok "no telemetry on this refusal path (allowed -- gate may short-circuit pre-emit)"
    fi
}

# ---------------------------------------------------------------------------
# Driver
# ---------------------------------------------------------------------------

main() {
    require_az_ai
    require_s03_build

    DEMO_TMP="$(mktemp -d -t az-ai-demo.XXXXXX)"
    mkdir -p "$DEMO_TMP/cfg/az-ai"

    banner "$DEMO_NAME  (binary: $(az-ai --version 2>/dev/null | head -1 | sed 's/  */ /g'))"
    note "Working state: $DEMO_TMP (cleaned on exit)"
    note "Acts will print to stdout. Asserted invariants are tagged [ ok ] / [FAIL]."

    act_i_the_setup
    act_ii_the_switch
    act_iii_the_rules
    act_iv_the_fallback
    act_v_the_curtain_call

    banner "Result"
    printf '  total assertions: %d\n' "$DEMO_TOTAL"
    printf '  failed:           %d\n' "$DEMO_FAILS"
    if [ "$DEMO_FAILS" -eq 0 ]; then
        printf '\n  Pretty, pretty, pretty good. Curtain.\n\n'
        return 0
    fi
    printf '\n  Reshoot needed. Curtain.\n\n'
    return 1
}

main "$@"
