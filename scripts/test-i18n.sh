#!/usr/bin/env bash
# test-i18n.sh — byte-identity smoke test for the i18n fixture corpus.
# Verifies non-ASCII strings survive shell quoting and file I/O round-trip.
# Manual-run half of docs/i18n/test-corpus.md. Network-free, idempotent.
# Exit: 0 all pass · 1 one or more mismatched · 2 misuse.
# Owner: Babu Bhatt (i18n). See docs/i18n/test-corpus.md.

set -euo pipefail
TMPDIR="$(mktemp -d)"
trap 'rm -rf "$TMPDIR"' EXIT

# Fixtures: label|string (see docs/i18n/test-corpus.md for provenance).
FIXTURES=(
  "latin-umlaut|Prüfung"
  "latin-sharp-s|Straße"
  "nfc-cafe|café"
  "cjk-japanese|日本語"
  "cjk-korean|한국어"
  "rtl-arabic|مرحبا"
  "rtl-hebrew|שלום"
  "emoji-zwj|👨‍👩‍👧‍👦"
  "emoji-flag|🇯🇵"
  "variation-selector|1️⃣"
  "mixed-bidi|az-ai is כלי CLI"
)

fail=0
for row in "${FIXTURES[@]}"; do
  label="${row%%|*}"; str="${row#*|}"
  printf '%s' "$str" >"$TMPDIR/$label.in"
  cp "$TMPDIR/$label.in" "$TMPDIR/$label.out"
  if ! cmp -s "$TMPDIR/$label.in" "$TMPDIR/$label.out"; then
    echo "[FAIL] $label: byte-identity round-trip failed" >&2
    fail=1
  fi
done

[ "$fail" -eq 0 ] && echo "[OK] i18n corpus byte-identity: $(printf '%s\n' "${FIXTURES[@]}" | wc -l) fixtures passed"
exit "$fail"
