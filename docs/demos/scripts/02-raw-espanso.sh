#!/usr/bin/env bash
# ──────────────────────────────────────────────────────────────────────────────
# 02-raw-espanso.sh — The Clean Pipe
#
# It was Reykjavík, February, the sun barely bothering to rise. Our hero is
# editing a customer email in a cramped TextEdit window, fingers cold,
# patience thin. She types ":aifix". Somewhere — invisibly — Espanso fires a
# shell command. Somewhere — invisibly — `az-ai --raw` swallows the clipboard
# and returns *only* the corrected prose. No spinner. No banner. No trailing
# newline to upset the cursor. The word arrives in her document as if she had
# typed it herself, only better.
#
# That is what --raw is for. This demo proves it by piping input in and
# piping output onward, the way a text expander would.
#
# Runtime: ~5 seconds. Network: yes (can be stubbed — see STUB block below).
# ──────────────────────────────────────────────────────────────────────────────
set -euo pipefail

type_prompt() {
  echo -n "$ "
  sleep 0.3
  for (( i=0; i<${#1}; i++ )); do
    printf '%s' "${1:$i:1}"
    sleep 0.02
  done
  echo
  sleep 0.4
  # shellcheck disable=SC2086
  eval "$1"
}

clear
sleep 0.5

# --- Act I: the ugly draft in the "clipboard" ------------------------------
type_prompt 'echo "their going too the store later, me and him" | tee /tmp/az-ai-demo-clip.txt'
sleep 0.8

# --- Act II: one raw call, no spinner, no ceremony -------------------------
# The hex dump after is the point: --raw emits only the answer bytes,
# with no trailing newline. That is what makes it safe for Espanso replace:.
#
# STUB: if you want to record this offline, replace the `az-ai` line with:
#   printf 'They are going to the store later, he and I.'
type_prompt 'cat /tmp/az-ai-demo-clip.txt | az-ai --raw --system "Fix grammar. Output ONLY corrected text, no quotes, no preamble."'
sleep 1.0

# --- Act III: proof that there is no rogue newline -------------------------
echo
type_prompt 'cat /tmp/az-ai-demo-clip.txt | az-ai --raw --system "Fix grammar. Output ONLY corrected text." | xxd | tail -3'
sleep 1.2

# --- Curtain ---------------------------------------------------------------
echo
echo "# Clean bytes in, clean bytes out. Espanso never knew we were there."
sleep 1.2

rm -f /tmp/az-ai-demo-clip.txt
