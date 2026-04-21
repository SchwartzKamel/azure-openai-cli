#!/usr/bin/env bash
# az-ai unlock-secrets — prime the gpg-agent cache for Tier 2 users.
#
# Run this ONCE per WSL session (after `wsl --shutdown` or a host
# reboot) to unlock your encrypted ~/.config/az-ai/env.gpg. After this,
# gpg-agent holds the passphrase in memory for 12h and every subsequent
# `bash -lc "az-ai …"` / `zsh -lc "az-ai …"` invocation (including the
# ones espanso fires from Windows) loads your creds silently.
#
# No-op for Tier 1 (plaintext) users — their creds load without an
# agent.

set -euo pipefail

CONFIG_DIR="${XDG_CONFIG_HOME:-$HOME/.config}/az-ai"
GPG_FILE="$CONFIG_DIR/env.gpg"
PLAIN_FILE="$CONFIG_DIR/env"

if [[ -f "$PLAIN_FILE" && ! -f "$GPG_FILE" ]]; then
    echo "[info] Tier 1 detected ($PLAIN_FILE) — no unlock needed, creds load directly." >&2
    exit 0
fi

if [[ ! -f "$GPG_FILE" ]]; then
    echo "[ERROR] no encrypted creds file at $GPG_FILE" >&2
    echo "        run: bash scripts/setup-secrets.sh" >&2
    exit 1
fi

if ! command -v gpg >/dev/null 2>&1; then
    echo "[ERROR] gpg is not installed" >&2
    exit 1
fi

echo "[info] Priming gpg-agent cache — enter the passphrase you set during setup."
if gpg --quiet --decrypt "$GPG_FILE" >/dev/null 2>&1; then
    echo "[ok] cache primed. Creds will load into every new shell for ~12h."
else
    # If cache was already primed, the above would have exited 0 silently;
    # if it failed, pinentry was probably cancelled or gpg isn't available.
    echo "[ERROR] decrypt failed (wrong passphrase, cancelled pinentry, or corrupt file)" >&2
    exit 1
fi

# Verify it works end-to-end via bash -lc (which is what espanso uses)
if probe="$(bash -lc 'echo "${AZUREOPENAIENDPOINT:-}"')" && [[ -n "$probe" ]]; then
    echo "[ok] bash -lc sees AZUREOPENAIENDPOINT — espanso / AHK will work"
else
    echo "[warn] bash -lc still doesn't see AZUREOPENAIENDPOINT" >&2
    echo "       check that the auto-source hook is present in ~/.profile:" >&2
    echo "       grep 'az-ai creds hook' ~/.profile" >&2
    exit 1
fi
