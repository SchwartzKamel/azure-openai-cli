#!/usr/bin/env bash
# uninstall-nim-gemma-2b.sh — rollback of install-nim-gemma-2b.sh.
# Stops/disables the user unit, removes the container; leaves Docker and
# NVIDIA Container Toolkit in place (user may want them).
#
# Flags:
#   --yes                 Non-interactive; keep image + ack file.
#   --purge-image         Also remove the pulled NIM image.
#   --purge-ack           Also remove the Gemma ToU acknowledgement.

set -euo pipefail

UNIT_NAME="az-ai-nim.service"
UNIT_FILE="${HOME}/.config/systemd/user/${UNIT_NAME}"
ACK_FILE="${HOME}/.config/az-ai/providers/nvidia.ack.json"
ENV_FILE="${HOME}/.config/az-ai/providers/nvidia.env"
NGC_KEY_FILE="${HOME}/.config/az-ai/providers/ngc_api_key"

YES=0; PURGE_IMAGE=0; PURGE_ACK=0
while [[ $# -gt 0 ]]; do
  case "$1" in
    --yes)          YES=1; shift ;;
    --purge-image)  PURGE_IMAGE=1; shift ;;
    --purge-ack)    PURGE_ACK=1; shift ;;
    -h|--help)      sed -n '2,10p' "$0"; exit 0 ;;
    *) echo "Unknown flag: $1" >&2; exit 2 ;;
  esac
done

if [[ -n "${NO_COLOR:-}" || ! -t 1 ]]; then
  C_GRN=""; C_YLW=""; C_DIM=""; C_RST=""
else
  C_GRN=$'\033[32m'; C_YLW=$'\033[33m'; C_DIM=$'\033[2m'; C_RST=$'\033[0m'
fi
ok()   { echo "  ${C_GRN}✓${C_RST} $*"; }
info() { echo "  ${C_DIM}$*${C_RST}"; }
warn() { echo "  ${C_YLW}!${C_RST} $*" >&2; }

confirm() {
  local prompt="$1"
  [[ "${YES}" -eq 1 ]] && return 0
  read -r -p "  ${prompt} [y/N] " reply
  [[ "${reply}" =~ ^[Yy]$ ]]
}

echo "[install-nim-gemma-2b] UNINSTALL"

# 1. Stop + disable systemd --user unit
if systemctl --user list-unit-files 2>/dev/null | grep -q "${UNIT_NAME}"; then
  systemctl --user disable --now "${UNIT_NAME}" >/dev/null 2>&1 || true
  ok "${UNIT_NAME} stopped + disabled"
else
  info "${UNIT_NAME} not installed"
fi
if [[ -f "${UNIT_FILE}" ]]; then
  rm -f "${UNIT_FILE}"
  systemctl --user daemon-reload 2>/dev/null || true
  ok "removed ${UNIT_FILE}"
fi

# 2. Remove container (belt-and-braces; --rm should have cleaned it already)
DOCKER="docker"
if ! docker info >/dev/null 2>&1 && sudo docker info >/dev/null 2>&1; then
  DOCKER="sudo docker"
fi
if ${DOCKER} ps -a --format '{{.Names}}' 2>/dev/null | grep -qx 'az-ai-nim'; then
  ${DOCKER} rm -f az-ai-nim >/dev/null 2>&1 || true
  ok "removed container az-ai-nim"
fi

# 3. Optional image purge
if [[ "${PURGE_IMAGE}" -eq 1 ]] || { [[ "${YES}" -eq 0 ]] && confirm "Also delete the NIM Docker image?"; }; then
  # Best-effort: match any tag for the gemma-4-2b-it-nvfp4 repository.
  mapfile -t IMGS < <(${DOCKER} images --format '{{.Repository}}:{{.Tag}}@{{.Digest}}' 2>/dev/null | grep 'gemma-4-2b-it-nvfp4' || true)
  if [[ "${#IMGS[@]}" -gt 0 ]]; then
    for i in "${IMGS[@]}"; do
      ref="${i%@*}"
      ${DOCKER} rmi -f "${ref}" >/dev/null 2>&1 || true
    done
    ok "NIM image(s) removed"
  else
    info "no matching NIM images to remove"
  fi
fi

# 4. Optional ack + creds purge
if [[ "${PURGE_ACK}" -eq 1 ]] || { [[ "${YES}" -eq 0 ]] && confirm "Also delete Gemma ToU ack + NGC credentials?"; }; then
  rm -f "${ACK_FILE}" "${ENV_FILE}" "${NGC_KEY_FILE}" 2>/dev/null || true
  ok "ack + NGC credentials removed"
else
  info "keeping ack + NGC credentials at ~/.config/az-ai/providers/"
fi

echo
echo "${C_GRN}✓ Uninstall complete.${C_RST} Docker + NVIDIA Container Toolkit left intact."
