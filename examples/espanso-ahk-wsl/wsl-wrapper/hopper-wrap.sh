#!/usr/bin/env bash
# /usr/local/bin/hopper-wrap   (installed with a compatibility link: az-ai-wrap)
#
# Thin wrapper around `hopper` for Windows-side callers (Espanso-on-Windows,
# AutoHotkey, anything that invokes `wsl.exe -e ...`). Those callers spawn a
# *non-login, non-interactive* shell, so ~/.bashrc is skipped and the
# AZUREOPENAI* env vars are missing.
#
# This wrapper sources ~/.bashrc if the vars aren't already set, then execs
# hopper with all arguments forwarded. Stdin/stdout/stderr pass through
# unchanged -- the caller is still responsible for `--raw` and stderr
# redirection.
#
# The command used to be called `az-ai` and this wrapper `az-ai-wrap`. Both old
# names keep working: this script falls back to `az-ai` when `hopper` is not
# installed (an install that predates the rename), and the install step below
# links `az-ai-wrap` to it so existing AutoHotkey and Espanso configs still run.
#
# NOTE (v2.1.1+): hopper auto-loads ~/.config/az-ai/env at startup. If you
# store credentials there instead of ~/.bashrc, this wrapper is unnecessary --
# call hopper directly from your Espanso/AHK configs. (The directory keeps its
# old name `az-ai` on purpose, so existing settings are found.)
#
# Install:
#   sudo install -m 0755 hopper-wrap.sh /usr/local/bin/hopper-wrap
#   sudo ln -sf hopper-wrap /usr/local/bin/az-ai-wrap     # keeps old configs working
#
# Usage (from Windows):
#   wsl.exe -e /usr/local/bin/hopper-wrap --raw "hello"
#   Get-Clipboard | wsl.exe -e /usr/local/bin/hopper-wrap --raw --system '...'

set -euo pipefail

# Ensure UTF-8 locale for correct handling of non-ASCII text (CJK, Arabic,
# accented characters). WSL non-login shells may inherit a bare POSIX locale.
export LANG="${LANG:-C.UTF-8}"

# Only source if creds are missing -- avoids re-running .bashrc on every call
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

# Prefer /usr/local/bin/hopper, then whatever `hopper` is on PATH, then the old
# name (the same binary on a current install; the only one on an older install).
if [[ -x /usr/local/bin/hopper ]]; then
    exec /usr/local/bin/hopper "$@"
elif command -v hopper >/dev/null 2>&1; then
    exec hopper "$@"
elif [[ -x /usr/local/bin/az-ai ]]; then
    exec /usr/local/bin/az-ai "$@"
else
    exec az-ai "$@"
fi
