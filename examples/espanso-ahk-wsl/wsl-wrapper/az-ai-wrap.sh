#!/usr/bin/env bash
# /usr/local/bin/az-ai-wrap
#
# Thin wrapper around `az-ai` for Windows-side callers (Espanso-on-Windows,
# AutoHotkey, anything that invokes `wsl.exe -e ...`). Those callers spawn a
# *non-login, non-interactive* shell, so ~/.bashrc is skipped and the
# AZUREOPENAI* env vars are missing.
#
# This wrapper sources ~/.bashrc if the vars aren't already set, then execs
# az-ai with all arguments forwarded. Stdin/stdout/stderr pass through
# unchanged -- the caller is still responsible for `--raw` and stderr
# redirection.
#
# NOTE (v2.1.1+): az-ai now auto-loads ~/.config/az-ai/env at startup.
# If you store credentials there instead of ~/.bashrc, this wrapper is
# unnecessary -- call az-ai directly from your Espanso/AHK configs.
#
# Install:
#   sudo install -m 0755 az-ai-wrap.sh /usr/local/bin/az-ai-wrap
#
# Usage (from Windows):
#   wsl.exe -e /usr/local/bin/az-ai-wrap --raw "hello"
#   Get-Clipboard | wsl.exe -e /usr/local/bin/az-ai-wrap --raw --system '...'

set -euo pipefail

# Only source if creds are missing — avoids re-running .bashrc on every call
# from inside an already-configured shell.
if [[ -z "${AZUREOPENAIENDPOINT:-}" || -z "${AZUREOPENAIAPI:-}" ]]; then
    if [[ -f "$HOME/.bashrc" ]]; then
        # .bashrc often bails early on non-interactive shells; source it
        # with a flag that some distros check, and tolerate `set -u` strictness.
        set +u
        # shellcheck disable=SC1091
        source "$HOME/.bashrc" >/dev/null 2>&1 || true
        set -u
    fi
fi

# Prefer /usr/local/bin/az-ai; fall back to whatever is on PATH.
if [[ -x /usr/local/bin/az-ai ]]; then
    exec /usr/local/bin/az-ai "$@"
else
    exec az-ai "$@"
fi
