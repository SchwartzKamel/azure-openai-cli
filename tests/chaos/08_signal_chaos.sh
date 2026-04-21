#!/usr/bin/env bash
# 08 — signal chaos. Invokes the binary via a helper to keep quoting clean.
source "$(dirname "$0")/_lib.sh"

# SIGINT arriving during estimate (benign, local) — must exit cleanly.
run_attack 08a "SIGINT during --estimate" -- \
  bash -c '
    export AZUREOPENAIENDPOINT=https://example.invalid AZUREOPENAIAPI=x
    "$1" --estimate "hi" &
    pid=$!
    sleep 0.05
    kill -INT "$pid" 2>/dev/null
    wait "$pid"
    echo "exit=$?"
  ' _ "$BIN"

run_attack 08b "SIGTERM during --estimate" -- \
  bash -c '
    export AZUREOPENAIENDPOINT=https://example.invalid AZUREOPENAIAPI=x
    "$1" --estimate "hi" &
    pid=$!
    sleep 0.05
    kill -TERM "$pid" 2>/dev/null
    wait "$pid"
    echo "exit=$?"
  ' _ "$BIN"

run_attack 08c "SIGPIPE via closed downstream" -- \
  bash -c '
    export AZUREOPENAIENDPOINT=https://example.invalid AZUREOPENAIAPI=x
    "$1" --estimate "hi" | head -c 0
    echo "pipestatus=${PIPESTATUS[*]}"
  ' _ "$BIN"

run_attack 08d "double SIGINT burst" -- \
  bash -c '
    export AZUREOPENAIENDPOINT=https://example.invalid AZUREOPENAIAPI=x
    "$1" --estimate "hi" &
    pid=$!
    sleep 0.02
    kill -INT "$pid" 2>/dev/null
    kill -INT "$pid" 2>/dev/null
    wait "$pid"
    echo "exit=$?"
  ' _ "$BIN"

