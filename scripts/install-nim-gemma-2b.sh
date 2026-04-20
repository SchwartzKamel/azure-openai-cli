#!/usr/bin/env bash
# install-nim-gemma-2b.sh — one-shot installer for NVIDIA NIM (Gemma-4-2B-NVFP4)
# on WSL2 Ubuntu 24.04 with a Blackwell/Ada/Hopper GPU.
#
# Usage:
#   scripts/install-nim-gemma-2b.sh                       # interactive
#   scripts/install-nim-gemma-2b.sh --accept-gemma-tou \
#       --ngc-key /path/to/ngckey.txt                     # unattended
#   scripts/install-nim-gemma-2b.sh --uninstall           # rollback

set -euo pipefail

# ─── NIM image (digest-pinned) ───────────────────────────────────────────────
# PLACEHOLDER: replace with the real NGC-published digest once published.
# Override via:  NIM_IMAGE=nvcr.io/nim/nvidia/gemma-4-2b-it-nvfp4@sha256:...  ./install-nim-gemma-2b.sh
: "${NIM_IMAGE:=nvcr.io/nim/nvidia/gemma-4-2b-it-nvfp4:latest}"

# Gemma Terms of Use (canonical source)
GEMMA_TOU_URL="https://ai.google.dev/gemma/terms"
# Text we hash as a local acknowledgement receipt (not the ToU itself).
GEMMA_TOU_RECEIPT_TEXT="I accept the Gemma Terms of Use at ${GEMMA_TOU_URL}"

# ─── Paths ───────────────────────────────────────────────────────────────────
CFG_DIR="${HOME}/.config/az-ai"
PROV_DIR="${CFG_DIR}/providers"
ACK_FILE="${PROV_DIR}/nvidia.ack.json"
ENV_FILE="${PROV_DIR}/nvidia.env"
NGC_KEY_FILE="${PROV_DIR}/ngc_api_key"
UNIT_DIR="${HOME}/.config/systemd/user"
UNIT_NAME="az-ai-nim.service"
UNIT_FILE="${UNIT_DIR}/${UNIT_NAME}"
TEMPLATE_FILE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/systemd/az-ai-nim.service"

# ─── Flags ───────────────────────────────────────────────────────────────────
ACCEPT_TOU=0
NGC_KEY_INPUT=""
DO_UNINSTALL=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --accept-gemma-tou) ACCEPT_TOU=1; shift ;;
    --ngc-key)          NGC_KEY_INPUT="$2"; shift 2 ;;
    --uninstall)        DO_UNINSTALL=1; shift ;;
    -h|--help)
      sed -n '2,12p' "$0"; exit 0 ;;
    *) echo "Unknown flag: $1" >&2; exit 2 ;;
  esac
done

# ─── Colors (respect NO_COLOR) ───────────────────────────────────────────────
if [[ -n "${NO_COLOR:-}" || ! -t 1 ]]; then
  C_RED=""; C_GRN=""; C_YLW=""; C_BLU=""; C_DIM=""; C_RST=""
else
  C_RED=$'\033[31m'; C_GRN=$'\033[32m'; C_YLW=$'\033[33m'
  C_BLU=$'\033[34m'; C_DIM=$'\033[2m';  C_RST=$'\033[0m'
fi

TOTAL_STEPS=9
step() {
  local n="$1"; shift
  echo "${C_BLU}[install-nim-gemma-2b] STEP ${n}/${TOTAL_STEPS}: $*${C_RST}"
}
info() { echo "  ${C_DIM}$*${C_RST}"; }
ok()   { echo "  ${C_GRN}✓${C_RST} $*"; }
warn() { echo "  ${C_YLW}!${C_RST} $*" >&2; }
die()  { echo "${C_RED}[install-nim-gemma-2b] FATAL:${C_RST} $*" >&2; exit 1; }

# ─────────────────────────────────────────────────────────────────────────────
# UNINSTALL PATH
# ─────────────────────────────────────────────────────────────────────────────
if [[ "${DO_UNINSTALL}" -eq 1 ]]; then
  exec "$(dirname "${BASH_SOURCE[0]}")/uninstall-nim-gemma-2b.sh"
fi

# ─────────────────────────────────────────────────────────────────────────────
# STEP 1 / 9 — Preflight checks
# ─────────────────────────────────────────────────────────────────────────────
step 1 "Preflight checks (WSL, GPU, CUDA, VRAM, arch)"

# WSL2
if ! grep -qiE 'microsoft|wsl' /proc/version 2>/dev/null; then
  die "This script targets WSL2 Ubuntu. /proc/version does not mention Microsoft/WSL."
fi
if [[ ! -r /proc/sys/kernel/osrelease ]] || ! grep -qi 'wsl2\|microsoft' /proc/sys/kernel/osrelease; then
  warn "Could not confirm WSL2 (vs WSL1). Continuing — but GPU passthrough requires WSL2."
fi
ok "WSL2 detected"

# nvidia-smi
if ! command -v nvidia-smi >/dev/null 2>&1; then
  die "nvidia-smi not found in WSL. Install the Windows-side NVIDIA driver (with WSL support) first."
fi
if ! nvidia-smi >/dev/null 2>&1; then
  die "nvidia-smi is present but fails to run. Check Windows driver + WSL GPU passthrough."
fi
ok "nvidia-smi works"

# CUDA driver >= 12.0
CUDA_VER="$(nvidia-smi --query-gpu=driver_version --format=csv,noheader | head -n1 | tr -d ' ')"
# Use CUDA version reported in top banner instead — more reliable
CUDA_RUNTIME_VER="$(nvidia-smi | awk -F 'CUDA Version: ' '/CUDA Version/ {print $2}' | awk '{print $1}' | head -n1)"
if [[ -z "${CUDA_RUNTIME_VER}" ]]; then
  warn "Could not parse CUDA version from nvidia-smi. Continuing."
else
  CUDA_MAJOR="${CUDA_RUNTIME_VER%%.*}"
  if [[ "${CUDA_MAJOR}" -lt 12 ]]; then
    die "CUDA driver ${CUDA_RUNTIME_VER} < 12.0. Upgrade the Windows-side NVIDIA driver."
  fi
  ok "CUDA driver ${CUDA_RUNTIME_VER} (>= 12.0)"
fi

# VRAM
VRAM_MB="$(nvidia-smi --query-gpu=memory.total --format=csv,noheader,nounits | head -n1 | tr -d ' ')"
if [[ -n "${VRAM_MB}" ]]; then
  if [[ "${VRAM_MB}" -lt 8192 ]]; then
    warn "VRAM is ${VRAM_MB} MiB (< 8 GB). Gemma-4-2B-NVFP4 may not fit under load."
  else
    ok "VRAM ${VRAM_MB} MiB"
  fi
fi

# Architecture — NVFP4 requires Blackwell; TRT-LLM emulates on Ada (sm_89) / Hopper (sm_90).
GPU_NAME="$(nvidia-smi --query-gpu=name --format=csv,noheader | head -n1)"
CC="$(nvidia-smi --query-gpu=compute_cap --format=csv,noheader | head -n1 | tr -d ' ')"
CC_MAJOR="${CC%%.*}"
case "${CC_MAJOR}" in
  8)  # Ada = 8.9; Ampere = 8.0/8.6 (no NVFP4 support even emulated)
      if [[ "${CC}" == "8.9" ]]; then
        ok "GPU: ${GPU_NAME} (Ada sm_89 — NVFP4 via TRT-LLM emulation)"
      else
        die "GPU ${GPU_NAME} (compute cap ${CC}) is Ampere-class. NVFP4 requires Ada/Hopper/Blackwell."
      fi ;;
  9)  ok "GPU: ${GPU_NAME} (Hopper sm_9x — NVFP4 via TRT-LLM emulation)" ;;
  10|11|12) ok "GPU: ${GPU_NAME} (Blackwell sm_${CC_MAJOR}x — native NVFP4)" ;;
  *)  die "GPU ${GPU_NAME} (compute cap ${CC}) does not support NVFP4 or TRT-LLM emulation." ;;
esac

# ─────────────────────────────────────────────────────────────────────────────
# STEP 2 — Gemma ToU acknowledgement
# ─────────────────────────────────────────────────────────────────────────────
step 2 "Gemma Terms of Use acknowledgement"
mkdir -p "${PROV_DIR}"
if [[ -f "${ACK_FILE}" ]]; then
  ok "Existing ack found: ${ACK_FILE}"
else
  echo
  echo "  Google's Gemma models are governed by the Gemma Terms of Use."
  echo "  Key points: (1) prohibited-use policy applies, (2) you are responsible"
  echo "  for outputs, (3) redistribution of weights is restricted."
  echo "  Read the full terms at: ${GEMMA_TOU_URL}"
  echo
  if [[ "${ACCEPT_TOU}" -ne 1 ]]; then
    read -r -p "  Type 'I ACCEPT' to proceed: " reply
    [[ "${reply}" == "I ACCEPT" ]] || die "Gemma ToU not accepted. Aborting."
  else
    info "--accept-gemma-tou passed; recording acknowledgement non-interactively."
  fi
  TOU_HASH="$(printf '%s' "${GEMMA_TOU_RECEIPT_TEXT}" | sha256sum | awk '{print $1}')"
  TS="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  cat > "${ACK_FILE}" <<JSON
{
  "provider": "nvidia-nim",
  "model_family": "gemma-4",
  "tou_url": "${GEMMA_TOU_URL}",
  "tou_receipt_sha256": "${TOU_HASH}",
  "accepted_at": "${TS}",
  "accepted_by": "${USER}"
}
JSON
  chmod 0644 "${ACK_FILE}"
  ok "Ack written to ${ACK_FILE}"
fi

# ─────────────────────────────────────────────────────────────────────────────
# STEP 3 — Docker install
# ─────────────────────────────────────────────────────────────────────────────
step 3 "Docker Engine (WSL-native)"
if command -v docker >/dev/null 2>&1 && docker --version >/dev/null 2>&1; then
  ok "docker present: $(docker --version)"
else
  info "Installing Docker Engine from docker.com apt repos..."
  sudo install -m 0755 -d /etc/apt/keyrings
  if [[ ! -f /etc/apt/keyrings/docker.asc ]]; then
    sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
    sudo chmod a+r /etc/apt/keyrings/docker.asc
  fi
  CODENAME="$(. /etc/os-release && echo "${VERSION_CODENAME}")"
  echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu ${CODENAME} stable" \
    | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
  sudo apt-get update -qq
  sudo DEBIAN_FRONTEND=noninteractive apt-get install -y -qq \
    docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
  ok "docker installed"
fi

# docker group
if ! id -nG "${USER}" | tr ' ' '\n' | grep -qx docker; then
  sudo usermod -aG docker "${USER}"
  warn "Added ${USER} to 'docker' group. You may need to log out/in (or run 'newgrp docker') before Docker works without sudo."
else
  ok "${USER} already in docker group"
fi

# Start docker service (WSL: service, not systemctl)
if ! sudo service docker status >/dev/null 2>&1; then
  sudo service docker start >/dev/null 2>&1 || true
fi
# Verify daemon reachable (allow group change race)
if ! docker info >/dev/null 2>&1; then
  if sudo docker info >/dev/null 2>&1; then
    warn "docker reachable via sudo only (group change not yet active in this shell). Using sudo for remaining docker calls."
    DOCKER="sudo docker"
  else
    die "docker daemon not reachable. Try: sudo service docker start"
  fi
else
  DOCKER="docker"
  ok "docker daemon reachable"
fi

# Enable user-level systemd linger (so --user units survive logout / SSH disconnect)
if command -v loginctl >/dev/null 2>&1; then
  if ! loginctl show-user "${USER}" 2>/dev/null | grep -q 'Linger=yes'; then
    sudo loginctl enable-linger "${USER}" 2>/dev/null || warn "Could not enable linger (non-systemd WSL?). systemd-user units may not survive disconnect."
  fi
fi

# ─────────────────────────────────────────────────────────────────────────────
# STEP 4 — NVIDIA Container Toolkit
# ─────────────────────────────────────────────────────────────────────────────
step 4 "NVIDIA Container Toolkit"
if ${DOCKER} info 2>/dev/null | grep -q 'Runtimes:.*nvidia'; then
  ok "nvidia container runtime already configured"
else
  info "Adding NVIDIA Container Toolkit apt repository..."
  curl -fsSL https://nvidia.github.io/libnvidia-container/gpgkey \
    | sudo gpg --dearmor -o /usr/share/keyrings/nvidia-container-toolkit-keyring.gpg
  curl -fsSL https://nvidia.github.io/libnvidia-container/stable/deb/nvidia-container-toolkit.list \
    | sed 's#deb https://#deb [signed-by=/usr/share/keyrings/nvidia-container-toolkit-keyring.gpg] https://#g' \
    | sudo tee /etc/apt/sources.list.d/nvidia-container-toolkit.list > /dev/null
  sudo apt-get update -qq
  sudo DEBIAN_FRONTEND=noninteractive apt-get install -y -qq nvidia-container-toolkit
  sudo nvidia-ctk runtime configure --runtime=docker
  sudo service docker restart >/dev/null 2>&1 || true
  sleep 2
  ok "nvidia-container-toolkit installed + configured"
fi

# Smoke test
info "Smoke-testing GPU access inside a container..."
if ${DOCKER} run --rm --gpus all ubuntu:24.04 nvidia-smi >/dev/null 2>&1; then
  ok "container nvidia-smi works"
else
  die "container nvidia-smi failed. Inspect: ${DOCKER} run --rm --gpus all ubuntu:24.04 nvidia-smi"
fi

# ─────────────────────────────────────────────────────────────────────────────
# STEP 5 — NGC credentials + pull NIM image
# ─────────────────────────────────────────────────────────────────────────────
step 5 "NGC login + pull NIM image"

get_ngc_key() {
  local key=""
  if [[ -n "${NGC_KEY_INPUT}" ]]; then
    [[ -f "${NGC_KEY_INPUT}" ]] || die "NGC key file not found: ${NGC_KEY_INPUT}"
    key="$(tr -d '\r\n' < "${NGC_KEY_INPUT}")"
  elif [[ -f "${NGC_KEY_FILE}" ]]; then
    key="$(tr -d '\r\n' < "${NGC_KEY_FILE}")"
  else
    echo "  NIM images require an NGC API key (free: https://ngc.nvidia.com/setup)."
    read -r -s -p "  Paste NGC API key (input hidden): " key
    echo
  fi
  [[ -n "${key}" ]] || die "NGC API key is empty."
  umask 0077
  printf '%s' "${key}" > "${NGC_KEY_FILE}"
  chmod 0600 "${NGC_KEY_FILE}"
  printf 'NGC_API_KEY=%s\n' "${key}" > "${ENV_FILE}"
  chmod 0600 "${ENV_FILE}"
  echo "${key}"
}

# Try an unauthenticated pull first; if 401, authenticate then retry.
NEED_LOGIN=0
if ! ${DOCKER} pull "${NIM_IMAGE}" 2>&1 | tee /dev/null | grep -qiE 'unauthorized|authentication required|denied'; then
  if ${DOCKER} image inspect "${NIM_IMAGE}" >/dev/null 2>&1; then
    ok "Image pulled: ${NIM_IMAGE}"
  else
    NEED_LOGIN=1
  fi
else
  NEED_LOGIN=1
fi

if [[ "${NEED_LOGIN}" -eq 1 ]]; then
  info "Authenticating to nvcr.io..."
  NGC_KEY="$(get_ngc_key)"
  echo "${NGC_KEY}" | ${DOCKER} login nvcr.io -u '$oauthtoken' --password-stdin >/dev/null \
    || die "docker login nvcr.io failed."
  ${DOCKER} pull "${NIM_IMAGE}" || die "docker pull ${NIM_IMAGE} failed after login."
  ok "Image pulled: ${NIM_IMAGE}"
else
  # Still need NGC key at runtime — env file required by the service unit.
  if [[ ! -f "${ENV_FILE}" ]]; then
    NGC_KEY="$(get_ngc_key)"
  fi
fi

# Digest verification (when pinned by digest)
if [[ "${NIM_IMAGE}" == *"@sha256:"* ]]; then
  EXPECTED_DIGEST="${NIM_IMAGE##*@}"
  ACTUAL_DIGEST="$(${DOCKER} image inspect --format='{{index .RepoDigests 0}}' "${NIM_IMAGE}" | awk -F@ '{print $2}')"
  [[ "${ACTUAL_DIGEST}" == "${EXPECTED_DIGEST}" ]] \
    || die "Image digest mismatch. expected=${EXPECTED_DIGEST} actual=${ACTUAL_DIGEST}"
  ok "Image digest verified"
else
  warn "NIM_IMAGE is not digest-pinned. For production set NIM_IMAGE=...@sha256:..."
fi

# ─────────────────────────────────────────────────────────────────────────────
# STEP 6 — systemd --user unit
# ─────────────────────────────────────────────────────────────────────────────
step 6 "Install systemd --user unit"
mkdir -p "${UNIT_DIR}" "${HOME}/.cache/nim"

[[ -f "${TEMPLATE_FILE}" ]] || die "Unit template missing: ${TEMPLATE_FILE}"

# Substitute image placeholder
sed "s|__NIM_IMAGE__|${NIM_IMAGE}|g" "${TEMPLATE_FILE}" > "${UNIT_FILE}"
chmod 0644 "${UNIT_FILE}"
ok "Unit written: ${UNIT_FILE}"

# Reload user daemon
if systemctl --user daemon-reload 2>/dev/null; then
  ok "user daemon reloaded"
else
  die "systemctl --user failed. Your WSL distro must have user-systemd (Ubuntu 24.04 does when systemd=true in /etc/wsl.conf)."
fi

systemctl --user enable --now "${UNIT_NAME}" >/dev/null 2>&1 \
  || die "Failed to enable+start ${UNIT_NAME}. Inspect: systemctl --user status ${UNIT_NAME}"
ok "${UNIT_NAME} enabled + started"

# ─────────────────────────────────────────────────────────────────────────────
# STEP 7 — Warm-up (health check)
# ─────────────────────────────────────────────────────────────────────────────
step 7 "Warm-up (wait for /v1/health/ready)"
HEALTH_URL="http://localhost:8000/v1/health/ready"
DEADLINE=$(( $(date +%s) + 120 ))
READY=0
while [[ "$(date +%s)" -lt "${DEADLINE}" ]]; do
  CODE="$(curl -s -o /dev/null -w '%{http_code}' "${HEALTH_URL}" || true)"
  if [[ "${CODE}" == "200" ]]; then
    READY=1; break
  fi
  printf '  %s[warmup]%s %s waiting... (code=%s)\r' "${C_DIM}" "${C_RST}" "$(date +%H:%M:%S)" "${CODE}"
  sleep 2
done
echo
[[ "${READY}" -eq 1 ]] || die "NIM did not become ready within 120s. Logs: systemctl --user status ${UNIT_NAME} ; docker logs az-ai-nim"
ok "NIM is ready at ${HEALTH_URL}"

# ─────────────────────────────────────────────────────────────────────────────
# STEP 8 — Smoke test inference
# ─────────────────────────────────────────────────────────────────────────────
step 8 "Smoke-test /v1/chat/completions"
SMOKE_RESP="$(curl -s -w '\n%{http_code}' \
  http://localhost:8000/v1/chat/completions \
  -H 'Content-Type: application/json' \
  -d '{"model":"nvidia/gemma-4-2b-it-nvfp4","messages":[{"role":"user","content":"Fix: their going home"}],"max_tokens":20}')"
SMOKE_CODE="$(printf '%s' "${SMOKE_RESP}" | tail -n1)"
SMOKE_BODY="$(printf '%s' "${SMOKE_RESP}" | sed '$d')"
if [[ "${SMOKE_CODE}" =~ ^2 ]] && [[ -n "${SMOKE_BODY}" ]]; then
  ok "Smoke test passed (HTTP ${SMOKE_CODE})"
  echo "  ${C_DIM}${SMOKE_BODY:0:200}...${C_RST}"
else
  die "Smoke test failed (HTTP ${SMOKE_CODE}). Body: ${SMOKE_BODY}"
fi

# ─────────────────────────────────────────────────────────────────────────────
# STEP 9 — Success banner
# ─────────────────────────────────────────────────────────────────────────────
step 9 "Done"
cat <<EOF

${C_GRN}✓ NVIDIA NIM (Gemma-4-2B-NVFP4) is up and serving on http://localhost:8000${C_RST}

Next steps:
  1. Point az-ai at it. In ~/.config/az-ai/preferences.json add a provider:
       {
         "providers": {
           "nvidia-local": {
             "endpoint": "http://localhost:8000/v1",
             "model": "nvidia/gemma-4-2b-it-nvfp4"
           }
         }
       }
  2. Try the trigger:  az-ai --trigger grammar "their going home"
  3. Wire Espanso — see docs/espanso-ahk-integration.md and
     docs/nim-setup.md for the :aifix match.
  4. Status:    make nim-status
     Stop:      systemctl --user stop ${UNIT_NAME}
     Uninstall: make uninstall-nim-gemma-2b
EOF
