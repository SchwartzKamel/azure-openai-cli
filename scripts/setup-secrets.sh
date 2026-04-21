#!/usr/bin/env bash
# az-ai setup-secrets — interactive walkthrough for storing Azure OpenAI
# credentials on Linux/WSL in a way that (a) your interactive shell sees,
# (b) `bash -lc`/`zsh -lc` from espanso or AHK sees, and (c) survives reboot.
#
# Supports two storage tiers:
#   - Tier 1: chmod 600 plaintext at ~/.config/az-ai/env
#   - Tier 2: GPG symmetric-encrypted at ~/.config/az-ai/env.gpg
#            (gpg-agent caches the passphrase for 12h so espanso doesn't
#            re-prompt on every trigger)
#
# Auto-sources from ~/.profile (bash login, bash -lc) AND ~/.zshenv (all
# zsh invocations including zsh -lc). Idempotent — safe to re-run to
# rotate keys or switch tiers.
#
# This is a bootstrap script. The 2.1 roadmap ships `az-ai setup` as a
# first-class subcommand (see docs/proposals/FR-022-native-setup-wizard.md).

set -euo pipefail

readonly SCRIPT_VERSION="1.0.0"
readonly CONFIG_DIR="${XDG_CONFIG_HOME:-$HOME/.config}/az-ai"
readonly PLAIN_FILE="$CONFIG_DIR/env"
readonly GPG_FILE="$CONFIG_DIR/env.gpg"
readonly HOOK_MARK_BEGIN="# >>> az-ai creds hook (managed by setup-secrets.sh) >>>"
readonly HOOK_MARK_END="# <<< az-ai creds hook <<<"

# ─── colors (respect NO_COLOR per project color-contract) ─────────────────
if [[ -n "${NO_COLOR-}" || ! -t 1 ]]; then
    C_RED='' C_GRN='' C_YLW='' C_CYN='' C_DIM='' C_RST=''
else
    C_RED=$'\033[31m' C_GRN=$'\033[32m' C_YLW=$'\033[33m' C_CYN=$'\033[36m' C_DIM=$'\033[2m' C_RST=$'\033[0m'
fi

info()  { printf '%s[info]%s  %s\n' "$C_CYN" "$C_RST" "$*"; }
ok()    { printf '%s[ok]%s    %s\n' "$C_GRN" "$C_RST" "$*"; }
warn()  { printf '%s[warn]%s  %s\n' "$C_YLW" "$C_RST" "$*" >&2; }
err()   { printf '%s[ERROR]%s %s\n' "$C_RED" "$C_RST" "$*" >&2; }
die()   { err "$*"; exit 1; }

prompt() {
    local var="$1" msg="$2" secret="${3:-}" default="${4:-}"
    local ans
    if [[ -n "$default" ]]; then
        msg="$msg ${C_DIM}[$default]${C_RST}"
    fi
    if [[ "$secret" == "secret" ]]; then
        printf '%s: ' "$msg" >&2
        IFS= read -rs ans
        printf '\n' >&2
    else
        printf '%s: ' "$msg" >&2
        IFS= read -r ans
    fi
    if [[ -z "$ans" && -n "$default" ]]; then
        ans="$default"
    fi
    printf -v "$var" '%s' "$ans"
}

banner() {
    cat >&2 <<EOF
${C_CYN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${C_RST}
${C_CYN}  az-ai setup-secrets v${SCRIPT_VERSION}${C_RST}
  Interactive walkthrough for Azure OpenAI CLI credentials.
  Supports zsh, bash, and non-login shell invocations.
${C_CYN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${C_RST}

EOF
}

detect_os() {
    if grep -qi microsoft /proc/version 2>/dev/null; then
        echo "wsl"
    elif [[ "$(uname -s)" == "Linux" ]]; then
        echo "linux"
    elif [[ "$(uname -s)" == "Darwin" ]]; then
        echo "macos"
    else
        echo "unknown"
    fi
}

detect_shells() {
    # Return the list of rc files we should inject the auto-source hook
    # into. We inject into BOTH .profile (bash login / bash -lc) AND
    # .zshenv (all zsh invocations) when those shells are present, so the
    # config works regardless of which shell the user is in and which
    # shell the automation caller (espanso/AHK) invokes.
    local files=()
    # .profile → read by bash login shells incl. `bash -lc "..."`
    if command -v bash >/dev/null 2>&1; then
        files+=("$HOME/.profile")
    fi
    # .zshenv → read by ALL zsh invocations (login, non-login, interactive,
    # non-interactive). Correct place for env-var exports in zsh.
    if command -v zsh >/dev/null 2>&1; then
        files+=("$HOME/.zshenv")
    fi
    printf '%s\n' "${files[@]}"
}

# ─── Tier 1: plaintext chmod 600 ──────────────────────────────────────────
write_plain() {
    local endpoint="$1" api_key="$2" model="$3"
    mkdir -p "$CONFIG_DIR"
    chmod 700 "$CONFIG_DIR"
    # Remove any existing encrypted form — tiers are mutually exclusive.
    rm -f "$GPG_FILE"
    umask 077
    cat >"$PLAIN_FILE" <<EOF
# Azure OpenAI CLI credentials (managed by setup-secrets.sh)
# This file is chmod 600 — readable only by $USER.
export AZUREOPENAIENDPOINT="$endpoint"
export AZUREOPENAIAPI="$api_key"
export AZUREOPENAIMODEL="$model"
EOF
    chmod 600 "$PLAIN_FILE"
    ok "wrote $PLAIN_FILE (chmod 600, plaintext)"
}

# ─── Tier 2: GPG symmetric ────────────────────────────────────────────────
ensure_gpg_agent_cache() {
    # 12h cache so espanso triggers through the day don't re-prompt.
    local agent_dir="${GNUPGHOME:-$HOME/.gnupg}"
    local conf="$agent_dir/gpg-agent.conf"
    mkdir -p "$agent_dir"
    chmod 700 "$agent_dir"
    touch "$conf"
    chmod 600 "$conf"
    if ! grep -q '^default-cache-ttl' "$conf" 2>/dev/null; then
        printf 'default-cache-ttl 43200\n' >>"$conf"
    fi
    if ! grep -q '^max-cache-ttl' "$conf" 2>/dev/null; then
        printf 'max-cache-ttl 43200\n'     >>"$conf"
    fi
    # Reload agent so new TTLs take effect.
    gpg-connect-agent reloadagent /bye >/dev/null 2>&1 || true
}

write_gpg() {
    local endpoint="$1" api_key="$2" model="$3"
    command -v gpg >/dev/null 2>&1 || die "gpg is not installed. Install it (\`sudo apt install gnupg\`) or choose Tier 1."
    mkdir -p "$CONFIG_DIR"
    chmod 700 "$CONFIG_DIR"
    ensure_gpg_agent_cache
    rm -f "$PLAIN_FILE"
    info "You'll be prompted for a passphrase. Choose a strong one — it's the only thing protecting your API key at rest."
    # --symmetric = passphrase-only, no key management needed.
    # --pinentry-mode loopback works whether or not a graphical pinentry exists.
    local tmp
    tmp="$(mktemp)"
    trap 'rm -f "$tmp"' RETURN
    cat >"$tmp" <<EOF
export AZUREOPENAIENDPOINT="$endpoint"
export AZUREOPENAIAPI="$api_key"
export AZUREOPENAIMODEL="$model"
EOF
    if ! gpg --symmetric --cipher-algo AES256 --output "$GPG_FILE.tmp" "$tmp" 2>/dev/null; then
        rm -f "$GPG_FILE.tmp"
        die "GPG encryption failed. Is pinentry available? Try Tier 1 or \`sudo apt install pinentry-tty\`."
    fi
    mv "$GPG_FILE.tmp" "$GPG_FILE"
    chmod 600 "$GPG_FILE"
    ok "wrote $GPG_FILE (AES256 symmetric, chmod 600)"
    info "gpg-agent will cache the passphrase for 12h so espanso triggers don't re-prompt."
    info "after each \`wsl --shutdown\` / reboot, re-prime the cache with:"
    info "    $C_GRN bash scripts/unlock-secrets.sh $C_RST"
}

# ─── shell-hook injection ─────────────────────────────────────────────────
hook_block() {
    cat <<'EOF'
# This block auto-sources Azure OpenAI CLI credentials at shell startup.
# It works for interactive shells AND non-interactive shells like
# `bash -lc` / `zsh -lc` used by espanso/AHK text expansion. Safe to
# remove — just delete from HOOK_MARK_BEGIN to HOOK_MARK_END.
_az_ai_creds_file="${XDG_CONFIG_HOME:-$HOME/.config}/az-ai"
if [ -f "$_az_ai_creds_file/env.gpg" ]; then
    # Tier 2: GPG-encrypted. Always --batch (never prompt from a shell
    # startup file — too disruptive). Relies on gpg-agent's cached
    # passphrase. If cache is cold, this is silent no-op; the user
    # primes the cache once per session via `scripts/unlock-secrets.sh`
    # (or any `gpg --decrypt` of this file). az-ai will then emit its
    # own "[ERROR] AZUREOPENAIENDPOINT is not set" on next invocation,
    # which is the expected signal to run unlock.
    eval "$(gpg --quiet --batch --decrypt "$_az_ai_creds_file/env.gpg" 2>/dev/null || true)"
elif [ -f "$_az_ai_creds_file/env" ]; then
    # Tier 1: plaintext chmod 600. `set -a` so `. file` exports all vars
    # without needing `export` prefixes in the file itself (our file has
    # them, but this is belt-and-suspenders).
    set -a; . "$_az_ai_creds_file/env"; set +a
fi
unset _az_ai_creds_file
EOF
}

inject_hook() {
    local target="$1"
    touch "$target"
    if grep -qF "$HOOK_MARK_BEGIN" "$target"; then
        info "hook already present in $target — updating in place"
        local tmp
        tmp="$(mktemp)"
        awk -v b="$HOOK_MARK_BEGIN" -v e="$HOOK_MARK_END" '
            $0 == b { skip=1; next }
            $0 == e { skip=0; next }
            !skip
        ' "$target" >"$tmp"
        mv "$tmp" "$target"
    fi
    {
        printf '\n%s\n' "$HOOK_MARK_BEGIN"
        hook_block
        printf '%s\n' "$HOOK_MARK_END"
    } >>"$target"
    ok "hook installed → $target"
}

# ─── verification probes ──────────────────────────────────────────────────
verify() {
    info "running verification probes..."
    local fail=0

    # Probe 1: hook file readable from a fresh bash -lc
    local probe_out
    probe_out="$(bash -lc 'echo "${AZUREOPENAIENDPOINT:-UNSET}|${AZUREOPENAIAPI:+SET}"' 2>/dev/null || true)"
    local endpoint_seen="${probe_out%%|*}"
    local key_seen="${probe_out#*|}"
    if [[ "$endpoint_seen" != "UNSET" && -n "$endpoint_seen" ]]; then
        ok "bash -lc sees AZUREOPENAIENDPOINT"
    else
        err "bash -lc does NOT see AZUREOPENAIENDPOINT"
        fail=1
    fi
    if [[ "$key_seen" == "SET" ]]; then
        ok "bash -lc sees AZUREOPENAIAPI"
    else
        err "bash -lc does NOT see AZUREOPENAIAPI (this is what espanso uses)"
        fail=1
    fi

    # Probe 2: zsh -lc if zsh is installed
    if command -v zsh >/dev/null 2>&1; then
        probe_out="$(zsh -lc 'echo "${AZUREOPENAIENDPOINT:-UNSET}|${AZUREOPENAIAPI:+SET}"' 2>/dev/null || true)"
        if [[ -n "${probe_out%%|*}" && "${probe_out%%|*}" != "UNSET" ]]; then
            ok "zsh -lc sees AZUREOPENAIENDPOINT"
        else
            warn "zsh -lc does NOT see AZUREOPENAIENDPOINT (fine if you use bash)"
        fi
    fi

    # Probe 3: az-ai binary resolution in login shell
    if bash -lc 'command -v az-ai' >/dev/null 2>&1; then
        ok "az-ai binary resolves on PATH in bash -lc"
    else
        warn "az-ai binary is NOT on PATH in bash -lc"
        warn "  install with: sudo install -m 0755 dist/aot/AzureOpenAI_CLI /usr/local/bin/az-ai"
    fi

    if (( fail )); then
        err "one or more probes failed — open a new shell and re-run with VERIFY_ONLY=1 to recheck"
        return 1
    fi
    ok "all probes passed — espanso / AHK / interactive shells should work"
    return 0
}

# ─── main ─────────────────────────────────────────────────────────────────
main() {
    banner
    local os; os="$(detect_os)"
    info "detected OS: $os"
    info "detected shell (\$SHELL): ${SHELL:-unknown}"

    if [[ "${VERIFY_ONLY:-0}" == "1" ]]; then
        verify
        return $?
    fi

    local endpoint api_key model tier
    local default_endpoint="${AZUREOPENAIENDPOINT:-}"
    local default_model="${AZUREOPENAIMODEL:-gpt-4o-mini}"

    printf '\n'
    prompt endpoint "Azure OpenAI endpoint URL (e.g. https://my-res.openai.azure.com/)" "" "$default_endpoint"
    [[ -n "$endpoint" ]] || die "endpoint is required"
    [[ "$endpoint" =~ ^https://.*\.openai\.azure\.com/?$ ]] || warn "endpoint doesn't look like a standard Azure OpenAI URL — continuing anyway"

    prompt api_key "Azure OpenAI API key (input hidden)" secret
    [[ -n "$api_key" ]] || die "api key is required"
    (( ${#api_key} >= 20 )) || warn "api key is shorter than 20 chars — are you sure?"

    prompt model "Default model deployment name" "" "$default_model"
    [[ -n "$model" ]] || die "model is required"

    printf '\n%sChoose storage tier:%s\n' "$C_CYN" "$C_RST"
    printf '  1) Plaintext + chmod 600  %s(fast, fine on a trusted personal box)%s\n' "$C_DIM" "$C_RST"
    printf '  2) GPG symmetric-encrypted %s(passphrase + 12h agent cache; recommended for shared/corp machines)%s\n' "$C_DIM" "$C_RST"
    while true; do
        prompt tier "Tier [1/2]" "" "1"
        case "$tier" in
            1) write_plain "$endpoint" "$api_key" "$model"; break ;;
            2) write_gpg   "$endpoint" "$api_key" "$model"; break ;;
            *) warn "pick 1 or 2" ;;
        esac
    done

    printf '\n'
    info "installing auto-source hook into your shell rc files..."
    while IFS= read -r rc; do
        inject_hook "$rc"
    done < <(detect_shells)

    printf '\n'
    info "to activate in your CURRENT shell, run ONE of:"
    case "${SHELL##*/}" in
        zsh)  printf '  %ssource ~/.zshenv%s\n' "$C_GRN" "$C_RST" ;;
        bash) printf '  %ssource ~/.profile%s\n' "$C_GRN" "$C_RST" ;;
        *)    printf '  %ssource ~/.profile%s  (or your shell'"'"'s env file)\n' "$C_GRN" "$C_RST" ;;
    esac
    printf '  %s(new shells will load it automatically)%s\n\n' "$C_DIM" "$C_RST"

    if [[ "$os" == "wsl" ]]; then
        info "WSL note: for espanso running on Windows to pick this up, you may need to"
        info "run \`wsl --shutdown\` from PowerShell once so new \`wsl.exe bash -lc\` invocations"
        info "spawn fresh processes that source the updated ~/.profile."
        printf '\n'
    fi

    verify || exit 1

    cat <<EOF

${C_GRN}━━━ done ━━━${C_RST}
Next steps:
  ${C_CYN}1.${C_RST} Open a new terminal (or source your rc file as shown above).
  ${C_CYN}2.${C_RST} Test: ${C_DIM}az-ai --raw --system 'Say hi.' <<< 'hi'${C_RST}
  ${C_CYN}3.${C_RST} Test from espanso simulation: ${C_DIM}echo hi | bash -lc 'az-ai --raw --system "Say hi."'${C_RST}
  ${C_CYN}4.${C_RST} Rotate creds? Just re-run: ${C_DIM}bash scripts/setup-secrets.sh${C_RST} — it's idempotent.
  ${C_CYN}5.${C_RST} Recheck without re-prompting: ${C_DIM}VERIFY_ONLY=1 bash scripts/setup-secrets.sh${C_RST}

EOF
}

main "$@"
