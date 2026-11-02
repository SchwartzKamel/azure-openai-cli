#!/usr/bin/env bash
# eval-lint.sh -- refuse to commit an eval file that leaks something.
#
# Lints eval corpora, fixtures and baselines with `az-ai --lint-suite`: keys and tokens, emails and
# hosts on real domains, non-reserved IP addresses, home-directory paths, PII shapes, high-entropy
# tokens, non-ASCII characters (homoglyphs, bidi and zero-width controls). Findings are masked and
# never printed in full. Needs no credentials, no model and no network.
#
# Usage:
#   scripts/eval-lint.sh                 lint every eval file in the repository
#   scripts/eval-lint.sh <file>...       lint only these files (what the pre-push hook does)
#
# Files under azureopenai-cli/Eval/Corpora/ are compiled into the binary, so they must also carry an
# "Origin: synthetic" provenance header; the others do not.
#
# Environment:  BIN  the hopper binary (default: azureopenai-cli/bin/Release/net10.0/hopper)
# Exit codes:   0 clean | 1 findings | 2 the binary is missing or a file is missing
#
# There is deliberately NO opt-out trailer: a check that guards against publishing secrets should not
# have a waiver. `git push --no-verify` is the only bypass, and CI still runs this check.
set -euo pipefail

cd "$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
BIN="${BIN:-azureopenai-cli/bin/Release/net10.0/hopper}"

if [ ! -x "$BIN" ] && ! command -v "$BIN" >/dev/null 2>&1; then
  echo "eval-lint: no binary at $BIN. Build first: make dotnet-build" >&2
  exit 2
fi

corpora=()
others=()
classify() {
  case "$1" in
    azureopenai-cli/Eval/Corpora/*.json) corpora+=("$1") ;;
    *) others+=("$1") ;;
  esac
}

if [ "$#" -gt 0 ]; then
  for f in "$@"; do classify "$f"; done
else
  for f in azureopenai-cli/Eval/Corpora/*.json examples/eval/*.json docs/prompts/fixtures/*.json eval/baselines/*.json; do
    [ -e "$f" ] && classify "$f"
  done
fi

if [ "${#corpora[@]}" -eq 0 ] && [ "${#others[@]}" -eq 0 ]; then
  echo "eval-lint: no eval files to check"
  exit 0
fi

lint() {
  local require="$1"; shift
  local args=()
  local f
  for f in "$@"; do args+=(--lint-suite "$f"); done
  [ "$require" = "yes" ] && args+=(--require-provenance)
  "$BIN" "${args[@]}"
}

rc=0
if [ "${#corpora[@]}" -gt 0 ]; then lint yes "${corpora[@]}" || rc=$?; fi
if [ "${#others[@]}" -gt 0 ]; then
  lint no "${others[@]}" || { r=$?; [ "$r" -gt "$rc" ] && rc=$r; }
fi
exit "$rc"
