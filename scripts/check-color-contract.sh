#!/usr/bin/env bash
# check-color-contract.sh — lint gate for the color contract.
#
# Enforces `.github/contracts/color-contract.md`: no raw ANSI escapes,
# no Console.ForegroundColor / ConsoleColor.* call sites outside the
# single chokepoint (`azureopenai-cli/Theme.cs`). All color decisions
# must flow through `Theme.UseColor()`.
#
# Owner: Mickey Abbott (accessibility / CLI ergonomics).
# Refs:
#   - .github/contracts/color-contract.md  (the spec)
#   - azureopenai-cli/Theme.cs          (the chokepoint)
#
# Exit codes:
#   0 — clean (no violations)
#   1 — one or more violations found
#   2 — script misuse (e.g. run from wrong directory)
#
# Honors NO_COLOR (https://no-color.org/). Errors are prefixed `[ERROR]`
# (Rule 7) so screen readers announce them correctly.

set -euo pipefail

# ---------------------------------------------------------------------------
# Output helpers — monochrome unless stdout is a TTY AND NO_COLOR is unset.
# ---------------------------------------------------------------------------
if [ -t 1 ] && [ -z "${NO_COLOR:-}" ]; then
    _red=$'\033[31m'
    _bold=$'\033[1m'
    _reset=$'\033[0m'
else
    _red=""
    _bold=""
    _reset=""
fi

err() {
    # Errors go to stderr with the `[ERROR]` prefix per contract Rule 7.
    printf '%s[ERROR]%s %s\n' "${_red}${_bold}" "${_reset}" "$*" >&2
}

info() {
    printf '%s\n' "$*"
}

# ---------------------------------------------------------------------------
# Locate repo root (script may be invoked from anywhere).
# ---------------------------------------------------------------------------
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
SRC_DIR="${REPO_ROOT}/azureopenai-cli"
CONTRACT_PATH=".github/contracts/color-contract.md"
THEME_PATH="azureopenai-cli/Theme.cs"

if [ ! -d "${SRC_DIR}" ]; then
    err "azureopenai-cli/ not found at ${SRC_DIR}"
    exit 2
fi

cd "${REPO_ROOT}"

# ---------------------------------------------------------------------------
# Forbidden patterns (extended regex, single alternation).
#
#   1. ConsoleColor.           — any property access / assignment
#   2. Console.ForegroundColor — direct BCL color mutation
#   3. Console.BackgroundColor
#   4. [Console]::ForegroundColor / ::BackgroundColor — PowerShell-style
#   5. ANSI escapes in string literals:
#        \u001b[   \u001B[   \x1b[   \x1B[   \e[   \033[
#      Matched as LITERAL backslash-sequences (what the source file shows).
# ---------------------------------------------------------------------------
FORBIDDEN_REGEX='ConsoleColor\.|Console\.(Foreground|Background)Color|\[Console\]::(Foreground|Background)Color|\\u001[bB]\[|\\x1[bB]\[|\\e\[|\\033\['

# Approval marker — call sites that must keep raw ANSI (e.g. the stderr
# braille spinner) may tag the line with this trailing comment. The marker
# is itself documented in the color contract and is grep-discoverable.
APPROVED_MARKER='// color-contract: approved-spinner'

# ---------------------------------------------------------------------------
# Collect candidate files: azureopenai-cli/**/*.cs, minus allowlist.
#
# Allowlist:
#   - Theme.cs itself (the chokepoint — it is allowed to contain ANSI)
#   - obj/ and bin/ build artifacts
#   - *.Tests/* — tests can use raw escapes to verify contract behavior
#     (we're not scanning tests/ here, but belt-and-braces)
# ---------------------------------------------------------------------------
mapfile -t candidate_files < <(
    find "${SRC_DIR}" -type f -name '*.cs' \
        -not -path "*/obj/*" \
        -not -path "*/bin/*" \
        -not -name 'Theme.cs' \
        -not -path '*/*.Tests/*' \
        | sort
)

violations=0
violation_report=""

for file in "${candidate_files[@]}"; do
    # grep -nE:    line number + extended regex
    # We deliberately do NOT use -H; we prefix the file path ourselves so
    # output is stable regardless of grep flavor.
    while IFS=: read -r lineno content; do
        # Skip blank matches (shouldn't happen, but guard).
        [ -z "${lineno}" ] && continue

        # Skip pure-comment lines: ^\s*// or ^\s**
        # (doc block continuation `* ...` or ordinary `// ...`).
        if printf '%s' "${content}" | grep -Eq '^[[:space:]]*(//|\*)'; then
            continue
        fi

        # Skip lines explicitly approved for raw ANSI (e.g. spinner).
        if printf '%s' "${content}" | grep -qF "${APPROVED_MARKER}"; then
            continue
        fi

        # Identify which forbidden symbol triggered so the error is useful.
        symbol="$(printf '%s' "${content}" | grep -oE "${FORBIDDEN_REGEX}" | head -n1 || true)"
        [ -z "${symbol}" ] && symbol="<unknown>"

        rel_path="${file#${REPO_ROOT}/}"
        violation_report+=$'\n  '"${rel_path}:${lineno}: forbidden symbol \`${symbol}\`"
        violations=$((violations + 1))
    done < <(grep -nE "${FORBIDDEN_REGEX}" "${file}" 2>/dev/null || true)
done

# ---------------------------------------------------------------------------
# Report.
# ---------------------------------------------------------------------------
if [ "${violations}" -gt 0 ]; then
    err "color-contract violations: ${violations}"
    # Body goes to stderr too so CI logs capture it alongside the [ERROR].
    {
        printf '\n  The following lines bypass the color-contract chokepoint:\n'
        printf '%s\n' "${violation_report}"
        printf '\n  Every ANSI SGR escape, ConsoleColor mutation, and\n'
        printf '  Console.Foreground/BackgroundColor assignment must route through\n'
        printf '  %s — see %s for the 7-rule precedence contract.\n' \
            "${THEME_PATH}" "${CONTRACT_PATH}"
        printf '\n  Fixes:\n'
        printf '    1. Replace the call site with Theme.WriteColored(...) /\n'
        printf '       Theme.WriteLineColored(...) — these already consult\n'
        printf '       Theme.UseColor() (NO_COLOR, FORCE_COLOR, TERM=dumb,\n'
        printf '       CLICOLOR, --raw, TTY auto-detect).\n'
        printf '    2. If the call site is an intentional carve-out (e.g. a\n'
        printf '       stderr spinner whose ANSI semantics are load-bearing),\n'
        printf '       tag the line with the trailing marker comment:\n'
        printf '         // color-contract: approved-spinner\n'
        printf '       and record the carve-out in %s §References.\n' \
            "${CONTRACT_PATH}"
        printf '\n'
    } >&2
    exit 1
fi

info "[color-contract] clean — 0 violations across ${#candidate_files[@]} file(s) under azureopenai-cli/"
exit 0
