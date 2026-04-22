#!/usr/bin/env bash
# ──────────────────────────────────────────────────────────────────────────────
# 03-agent-tool-calling.sh — The Model Reaches for a Shell
#
# [hook, 0:00–0:10] A strange directory. One natural-language question. The
# model decides — on its own — to run a shell command, reads the output, and
# writes back a summary. That's agent mode in ten seconds.
#
# Buenos Aires, autumn. A developer at a café, laptop open to a directory
# she has never seen — a freelance job, a zip file, a ticking clock. She
# doesn't want to grep. She doesn't want to `ls -R`. She wants to *ask the
# codebase a question* and have something competent poke around on her
# behalf. She types one line. The model decides, on its own, that the
# answer lives behind a `shell_exec` call. It runs it. It reads the output.
# It composes a reply. She takes another sip of mate.
#
# That is agent mode. This demo is deliberately boring — a question whose
# answer is knowable with one safe `ls`/`wc` invocation — because boring is
# reproducible and reproducible is what a recording needs.
#
# Runtime: ~10–15 seconds. Network: yes. Tool calls: 1–2 (shell_exec).
# Safety: the prompt asks only to count files under the current directory.
# No destructive operations. No network tools. No secrets in argv.
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

# --- Preflight: verify the v2 binary is on PATH ----------------------------
# [narrator] "Confirm the binary before we hand the model a shell."
type_prompt 'az-ai-v2 --version --short'
sleep 0.6

# --- Stage the sandbox -----------------------------------------------------
# Create a tiny, self-contained directory tree so the demo is identical on
# every machine — no surprises from the user's $PWD.
DEMO_DIR="$(mktemp -d -t az-ai-agent-demo.XXXXXX)"
trap 'rm -rf "$DEMO_DIR"' EXIT
mkdir -p "$DEMO_DIR"/src/{api,web,db} "$DEMO_DIR"/tests
: > "$DEMO_DIR/src/api/routes.py"
: > "$DEMO_DIR/src/api/auth.py"
: > "$DEMO_DIR/src/web/index.html"
: > "$DEMO_DIR/src/db/schema.sql"
: > "$DEMO_DIR/tests/test_api.py"
: > "$DEMO_DIR/README.md"
cd "$DEMO_DIR"

# --- Show the stage --------------------------------------------------------
# [narrator] "Fresh directory. Unfamiliar tree. We won't grep. We'll ask."
type_prompt 'pwd'
sleep 0.4
type_prompt 'ls'
sleep 1.0

# --- The ask ---------------------------------------------------------------
# A question the model cannot answer without actually looking. That forces
# it to reach for shell_exec. The phrasing is kept tight on purpose — agent
# prompts reward specificity.
# [narrator] "One prompt. The model picks the tool. Watch for shell_exec."
type_prompt 'az-ai-v2 --agent "Count the number of regular files under the current directory, grouped by top-level folder. Use shell_exec. Report the totals as a short table."'
sleep 1.5

# --- Tag -------------------------------------------------------------------
echo
echo "# One prompt. One tool. One table. No one wrote a for-loop."
sleep 1.2
