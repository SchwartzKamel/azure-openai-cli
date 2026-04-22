# NIM Setup -- Gemma-4-2B-NVFP4 (WSL2 + Blackwell)

This guide gets you from a fresh WSL2 Ubuntu 24.04 on a Blackwell laptop to
`:aifix` working locally against NVIDIA NIM in under 10 minutes.

- Design rationale: [ADR-006 -- NVIDIA NIM / NVFP4 integration](adr/ADR-006-nvfp4-nim-integration.md)
- Provider spec: [FR-020 -- NVIDIA NIM provider + per-trigger routing](proposals/FR-020-nvidia-nim-provider-per-trigger-routing.md)
- Companion integration: [Espanso / AHK kit](../examples/espanso-ahk-wsl/README.md)

> **You probably do not need this page.** If you don't have a Blackwell /
> Ada / Hopper GPU with ≥ 8 GB VRAM, or you just want the CLI working
> today, **skip this entirely**. Install the AOT binary, export your Azure
> creds, and drop in the [Espanso/AHK kit](../examples/espanso-ahk-wsl/README.md)
> -- every trigger (`:aifix`, `:airw`, `:aitldr`, `:aiexp`, `:aic`, `:ai`)
> routes to Azure out of the box with a 2-3 s budget. This page is the
> **opt-in upgrade** for users who want sub-second `:aifix` and `:airw`
> against a local NVFP4 model. See FR-020 §4.8 for the cloud-only guarantee.

---

## Prerequisites

| Requirement | How to check |
|---|---|
| WSL2 Ubuntu 24.04 | `grep -i microsoft /proc/version` and `lsb_release -rs` |
| NVIDIA driver with WSL GPU passthrough | `nvidia-smi` inside WSL prints a GPU table |
| CUDA driver ≥ 12.0 | Top-right of `nvidia-smi` output |
| GPU with NVFP4 support (native or TRT-LLM emulation) | Blackwell sm_10x/sm_11x (native), Ada sm_89, or Hopper sm_9x |
| ≥ 8 GB VRAM (12 GB recommended) | `nvidia-smi --query-gpu=memory.total --format=csv` |
| `curl`, `sudo`, and an internet connection | -- |
| An NGC API key (free): https://ngc.nvidia.com/setup | -- |

**Ampere (sm_80/sm_86) is not supported.** NVFP4 requires Ada or newer.

> ⚠️ **Docker Desktop interference.** If Docker Desktop for Windows is running
> with WSL integration enabled, uninstall or disable it for this distro. This
> installer uses the **WSL-native Docker Engine** from docker.com apt repos. Do
> not run both.

---

## One-shot install

```bash
make install-nim-gemma-2b
```

The script runs 9 banner-labeled steps:

1. Preflight (WSL2, driver, CUDA, VRAM, GPU architecture)
2. Gemma Terms of Use acknowledgement
3. Docker Engine install (WSL-native, from docker.com)
4. NVIDIA Container Toolkit install + `nvidia-ctk runtime configure --runtime=docker`
5. NGC login + NIM image pull (digest-verified)
6. systemd `--user` unit install (`~/.config/systemd/user/az-ai-nim.service`)
7. Warm-up: poll `http://localhost:8000/v1/health/ready` (120 s timeout)
8. Smoke test: `POST /v1/chat/completions`
9. Success banner with next steps

Every step is idempotent -- re-running the installer skips work already done.

### Unattended (CI / advanced users)

```bash
# ToU pre-accepted, NGC key from a 0600 file
make install-nim-gemma-2b ARGS="--accept-gemma-tou --ngc-key /path/to/ngc.key"

# Pin the image by digest (strongly recommended for production)
NIM_IMAGE='nvcr.io/nim/nvidia/gemma-4-2b-it-nvfp4@sha256:<digest>' \
  make install-nim-gemma-2b ARGS="--accept-gemma-tou --ngc-key /path/to/ngc.key"
```

---

## Manual install (walkthrough)

For users who want to understand every moving part, the equivalent manual steps
are:

```bash
# 1. Docker Engine
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg \
     -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc
CODENAME=$(. /etc/os-release && echo "$VERSION_CODENAME")
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] \
    https://download.docker.com/linux/ubuntu $CODENAME stable" \
  | sudo tee /etc/apt/sources.list.d/docker.list
sudo apt-get update && sudo apt-get install -y \
  docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
sudo usermod -aG docker "$USER"
sudo service docker start

# 2. NVIDIA Container Toolkit
curl -fsSL https://nvidia.github.io/libnvidia-container/gpgkey \
  | sudo gpg --dearmor -o /usr/share/keyrings/nvidia-container-toolkit-keyring.gpg
curl -fsSL https://nvidia.github.io/libnvidia-container/stable/deb/nvidia-container-toolkit.list \
  | sed 's#deb https://#deb [signed-by=/usr/share/keyrings/nvidia-container-toolkit-keyring.gpg] https://#g' \
  | sudo tee /etc/apt/sources.list.d/nvidia-container-toolkit.list
sudo apt-get update && sudo apt-get install -y nvidia-container-toolkit
sudo nvidia-ctk runtime configure --runtime=docker
sudo service docker restart
docker run --rm --gpus all ubuntu:24.04 nvidia-smi   # smoke test

# 3. NGC login + pull NIM
echo "$NGC_API_KEY" | docker login nvcr.io -u '$oauthtoken' --password-stdin
docker pull nvcr.io/nim/nvidia/gemma-4-2b-it-nvfp4:latest   # or @sha256:...

# 4. Systemd --user unit
sudo loginctl enable-linger "$USER"
install -d ~/.config/systemd/user ~/.cache/nim
# copy scripts/systemd/az-ai-nim.service into ~/.config/systemd/user/
# and replace __NIM_IMAGE__ with your pinned reference
systemctl --user daemon-reload
systemctl --user enable --now az-ai-nim.service

# 5. Warm-up + smoke
until curl -sf http://localhost:8000/v1/health/ready; do sleep 2; done
curl -s http://localhost:8000/v1/chat/completions -H 'Content-Type: application/json' \
  -d '{"model":"nvidia/gemma-4-2b-it-nvfp4","messages":[{"role":"user","content":"ping"}],"max_tokens":8}'
```

---

## Post-install verification

```bash
make nim-status        # systemctl status + /v1/health/ready
make nim-warmup        # block until ready (120s timeout)
```

Inspect container logs if anything looks off:

```bash
docker logs -f az-ai-nim
systemctl --user status az-ai-nim.service
```

---

## Integrating with az-ai

Add a local provider to `~/.config/az-ai/preferences.json`:

```jsonc
{
  "providers": {
    "nvidia-local": {
      "endpoint": "http://localhost:8000/v1",
      "model": "nvidia/gemma-4-2b-it-nvfp4",
      "auth": "none"
    }
  },
  "default_provider": "nvidia-local"
}
```

Then:

```bash
az-ai --trigger grammar "their going home"
# → "They're going home."
```

Because the endpoint is bound to `127.0.0.1`, the NIM server is not reachable
from the LAN or from Windows -- Newman's default-deny posture. If you need
cross-host access, put a reverse proxy in front; do not expose port 8000
directly.

---

## Espanso / AHK integration

Once the provider works from `az-ai` on the command line, wire it to your
text-expansion tool. The canonical `:aifix` recipe lives in
[docs/espanso-ahk-integration.md](espanso-ahk-integration.md). The relevant
Espanso match:

```yaml
- trigger: ":aifix"
  replace: "{{out}}"
  vars:
    - name: out
      type: shell
      params:
        cmd: 'az-ai --raw --trigger grammar "{{clipboard}}"'
```

Latency target (per ADR-006): **≤ 300 ms p95 end-to-end** for short grammar
fixes on a warm NIM. If your first-token latency exceeds that, see
*Troubleshooting → OOM / cold starts*.

---

## Troubleshooting

### NIM fails to load / health check never passes

1. `docker logs az-ai-nim` -- look for CUDA errors, OOM, or missing NGC auth.
2. `nvidia-smi` -- confirm the GPU is idle (another process may hold VRAM).
3. `systemctl --user status az-ai-nim.service` -- confirm the unit is active.
4. First run downloads weights into `~/.cache/nim`. Expect a multi-minute delay
   on a cold cache; warm restarts should be under 10 s.

### OOM (out of memory)

- Stop Windows-side GPU hogs (Chrome hardware accel, games, Stable Diffusion).
- Confirm no other containers hold the GPU: `docker ps --filter ancestor=...`.
- Gemma-4-2B-NVFP4 needs ~2-3 GB VRAM at idle; KV cache grows with context.
  Reduce `max_tokens` and context length if you see OOM under load.

### Wrong architecture ("NVFP4 not supported")

- Ampere (sm_80 / sm_86) cannot run NVFP4 even under TRT-LLM emulation.
  Switch to FR-018 (llama.cpp) with a GGUF quant of Gemma instead.

### NGC auth fails (401 from `docker pull`)

- Key must be NGC API **key**, not a personal access token.
- Username is literally `$oauthtoken` (dollar sign included, quoted to prevent
  shell expansion).
- `cat ~/.config/az-ai/providers/ngc_api_key | wc -c` -- should be > 40 chars.

### Docker Desktop interference

- `docker context ls` -- if the current context points at `desktop-linux`, that's
  Docker Desktop, not the WSL-native engine. Switch: `docker context use default`.
- Or uninstall Docker Desktop WSL integration for this distro in Docker Desktop
  settings → Resources → WSL Integration.

### "systemctl --user" errors out

- WSL2 needs user-systemd. Ensure `/etc/wsl.conf` contains:
  ```ini
  [boot]
  systemd=true
  ```
  Then `wsl --shutdown` from PowerShell and relaunch the distro.

### Port 8000 already in use

- `sudo ss -lntp | grep :8000` -- find the other process.
- Edit `~/.config/systemd/user/az-ai-nim.service` to use a different host port
  (keep `127.0.0.1:` prefix).

---

## Uninstall

```bash
make uninstall-nim-gemma-2b                           # interactive
make uninstall-nim-gemma-2b ARGS="--yes"              # keep image + ack
make uninstall-nim-gemma-2b ARGS="--yes --purge-image --purge-ack"
```

Docker and the NVIDIA Container Toolkit are intentionally left in place -- you
may want them for other workloads. Remove them manually with `apt-get purge` if
desired.

---

## References

- [ADR-006 -- NVIDIA NIM / NVFP4 integration](adr/ADR-006-nvfp4-nim-integration.md) -- decision history, risk register, security posture
- FR-020 -- NVIDIA NIM provider spec (pending)
- [FR-018 -- Local-model provider (llama.cpp / Ollama)](proposals/FR-018-local-model-provider-llamacpp.md) -- dependency: provider abstraction
- [Espanso / AHK integration](espanso-ahk-integration.md) -- downstream consumer
- NGC setup: https://ngc.nvidia.com/setup
- Gemma Terms of Use: https://ai.google.dev/gemma/terms
