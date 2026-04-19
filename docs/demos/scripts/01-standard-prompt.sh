#!/usr/bin/env bash
# ──────────────────────────────────────────────────────────────────────────────
# 01-standard-prompt.sh — The Hero Shot
#
# Picture it: Marrakech, 1998. A traveling correspondent, deadline at dawn,
# a battered laptop balanced on a tea crate. He needs one sentence — just one —
# that captures the smell of cardamom and the sound of a muezzin at first
# light. He types a question. The cursor blinks. And then the words arrive,
# one token at a time, like camels cresting a dune.
#
# That is the feeling we are selling here. Default mode. Spinner. Streaming
# tokens. No flags, no ceremony, no JSON. Just: ask and receive.
#
# Runtime: ~6 seconds. Network: yes. Secrets on screen: none.
# ──────────────────────────────────────────────────────────────────────────────
set -euo pipefail

# Visual pacing helpers — makes the GIF watchable without feeling staged.
type_prompt() {
  # $1 = command to display and execute
  echo -n "$ "
  sleep 0.3
  # Print the command character-by-character for that carriage-return warmth.
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

# --- The setup -------------------------------------------------------------
type_prompt 'az-ai --version --short'
sleep 0.8

# --- The question ----------------------------------------------------------
# One prompt. No system message. No tools. The bare metal of the tool.
type_prompt 'az-ai "In one sentence, explain why a 5ms cold start matters for a text expander."'
sleep 1.0

# --- The tag ---------------------------------------------------------------
echo
echo "# End scene. Fade to black."
sleep 1.2
