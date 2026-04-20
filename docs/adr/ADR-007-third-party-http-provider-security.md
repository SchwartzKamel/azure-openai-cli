# ADR-007 — Security Guardrails for Third-Party HTTP Inference Providers

- **Status**: Proposed — 2026-04-23
- **Deciders**: Newman (sec), Kramer (eng), Jerry (DevOps), Jackie (legal)
- **Related**:
  - [ADR-006 — NVIDIA NIM / NVFP4 Provider Integration](./ADR-006-nvfp4-nim-integration.md) — originating decision
  - [FR-018 — Local-Model Provider](../proposals/FR-018-local-model-provider-llamacpp.md)
  - Prospective FR-020 — NVIDIA NIM Provider

## Context

FR-018 introduces az-ai's provider abstraction: "OpenAI-compatible HTTP + configurable base URL + bearer token." FR-020 (NVIDIA NIM) and every future third-party HTTP inference provider (Ollama, `trtllm-serve`, on-prem vLLM, remote community endpoints) will ride on that same abstraction.

Each of these providers is, from az-ai's perspective, an arbitrary HTTP endpoint running on the user's machine, their LAN, or the public internet. That opens five distinct risk classes:

1. **Supply chain.** Unpinned container tags (e.g. `:latest`) permit silent upstream swap; the model image runs a full HTTP server, a model runtime, often Triton and Python — a lot of sockets for "just a model."
2. **Auth posture.** Several runtimes (notably NIM) default to **no authentication on `localhost`**. Any local process can issue inference requests and read the prompt stream.
3. **SSRF / metadata exfiltration.** A provider configured to point at `169.254.169.254` (AWS IMDS), `metadata.google.internal`, `metadata.azure.com`, or `100.100.100.200` will happily exfiltrate cloud-instance credentials on the user's behalf. Deny-lists miss variants; allow-lists do not.
4. **Credential leakage.** Bearer tokens end up in logs, exception messages, structured traces, and shell history if the code path is not disciplined from day one.
5. **Third-party terms of use.** Gemma, Llama, and similar gated models carry acceptable-use policies. az-ai does not distribute them but *does* invoke them on the user's behalf; a first-run acknowledgment protects both the user and the project.

ADR-006 rolled these concerns into its decision section. Every future provider will re-litigate them unless they are promoted to policy. This ADR does the promotion.

## Decision

**The six guardrails below are required for every third-party HTTP inference provider az-ai adds.** A provider FR that does not satisfy all six does not land.

### 1. Digest-pinned container references

Every `docker run`, `docker-compose.yml`, documentation snippet, and CI fixture references a third-party image by SHA-256 digest:

```
nvcr.io/nim/nvidia/gemma-4-9b-it-nvfp4@sha256:<64-hex>
```

Tag-pinning (`:1.2.0`) is the pragmatic floor only when registry digest discovery is blocked (e.g. NVCR requires auth to resolve digests). `:latest` is never used.

Rationale: Dependabot does not cover NVCR or most non-OSS registries. A monthly manual sweep updates the digest; any Trivy HIGH finding blocks the sweep. (See ADR-008 for how bench fixtures are pinned to match.)

### 2. Bearer-token auth on every provider endpoint

az-ai generates a random 256-bit bearer token on first provider run, starts the backing process with the token injected into its environment (e.g. `NIM_API_KEY` for NIM), and sends `Authorization: Bearer …` on every request. The token is stored in the OS keychain (macOS Keychain, Windows Credential Manager, libsecret on Linux) — **never** in a dotfile, **never** echoed to stdout, **never** logged.

Implementation rule: any log path, exception message, or structured trace that could carry an auth header must route through a redactor. Auth header appearing in an exception message is a **P1 bug**.

### 3. SSRF: allow-list, not deny-list

Default allow set for any provider base URL:

- `127.0.0.1/32`
- `::1/128`
- Resolved `localhost`

The `--allow-lan` flag opts in to RFC1918 (`10/8`, `172.16/12`, `192.168/16`) and link-local IPv4 (but **not** the metadata blocks below), with a yellow banner on every invocation.

**Always blocked, even under `--allow-lan`:**

| Endpoint | Scope |
|---|---|
| `169.254.169.254` | AWS / OpenStack / Azure IMDS |
| `fd00:ec2::254` | AWS IPv6 IMDS |
| `100.100.100.200` | Alibaba / some appliances |
| `metadata.google.internal` | GCP |
| `metadata.azure.com` | Azure IMDS (name form) |
| full `169.254/16` | link-local catch-all |

Post-redirect IP re-validation is required: a 3xx to `http://169.254.169.254` from an allowed host is rejected. DNS results are pinned per request to defeat DNS-rebinding.

### 4. TLS required on non-loopback

Plain HTTP to any non-loopback host is refused unless `--insecure-remote` is passed explicitly on the command line. That flag prints a loud red banner on every invocation. It is **never** set via env var.

First use of any non-loopback endpoint triggers an interactive confirmation, cached per-host in a trust file (`~/.config/az-ai/providers/trust.json`). Structured logs record `host:port` and auth scheme only — never the prompt, never the response, never the token.

### 5. First-run acknowledgment file for gated models

First invocation of a provider + model combination subject to third-party ToU (Gemma, Llama, etc.) writes an acknowledgment record at `~/.config/az-ai/providers/<provider>.ack.json`:

```json
{
  "model_id": "nvidia/Gemma-4-9B-IT-NVFP4",
  "tou_url_sha256": "…",
  "accepted_at": "2026-04-23T10:04:17Z",
  "accepted_by": "user@host"
}
```

Phrasing is a **notice** ("By proceeding you acknowledge the Gemma Terms of Use and the NVIDIA Software License…"), never "You agree to…" — az-ai is not the user's agent and not a party to the contract (Jackie §3).

Non-interactive callers (Espanso, AHK, CI) use either `--yes` or `AZ_AI_ACCEPT_THIRD_PARTY_TERMS=1`. With neither flag set and no TTY, az-ai fails closed with a clear stderr pointer to the doc page — never hangs on a prompt.

### 6. Hardened container-run documentation

Every provider that runs in a container gets a `docs/providers/<provider>.md` with a hardened reference `docker run` recipe: `--user <non-root>`, `--read-only`, `--cap-drop=ALL`, `--security-opt=no-new-privileges`, `--network` bound to a dedicated bridge on `127.0.0.1`, **never** `--privileged`. GPU passthrough (`--gpus`) is permitted; `--privileged` is not.

## Required test coverage

The following cases are added to `ToolHardeningTests` alongside any PR that introduces a third-party provider:

1. Provider base URL on an SSRF-blocked address (e.g. `169.254.169.254`) — request rejected before socket open.
2. Metadata endpoint under `--allow-lan` — still rejected.
3. Provider endpoint that 3xx-redirects to a blocked IP — rejected post-redirect.
4. Exception path emitted with bearer header present — redactor verified.
5. Non-loopback HTTP endpoint without `--insecure-remote` — rejected.
6. First-run acknowledgment missing under non-interactive invocation — fails closed, clear stderr message, non-zero exit.

## Consequences

### Positive

- Every future third-party provider inherits a fixed, reviewable security posture. No re-litigation per FR.
- Metadata-endpoint SSRF, the highest-severity cloud-local exposure, is structurally impossible through the provider abstraction.
- Credential handling is centralised in one redactor + one keychain accessor; future auditors have one place to look.

### Negative

- Extra scaffolding per provider (keychain integration, trust file, ack file, redactor). Amortised across providers, but real up-front cost.
- `--allow-lan` banner + non-interactive ack env var add CLI surface that must be documented and tested.
- The allow-list will, over time, reject legitimate exotic deployments (custom DNS, multi-homed hosts). Those users take the `--insecure-remote` + explicit-IP escape hatch and accept the banner.

## Alternatives Considered

- **Per-provider ad-hoc hardening.** Rejected: seven providers later, seven subtly different threat models. Centralising is cheaper and more auditable.
- **Deny-list for metadata endpoints.** Rejected: there are too many variants (IPv4, IPv6, name forms, cloud-specific), and new ones appear with each cloud release. Allow-list wins by construction.
- **Store bearer tokens in `~/.config/az-ai/providers/<provider>.json`.** Rejected: file-on-disk tokens end up in backups, Dropbox, git-tracked dotfile repos. Keychain is the policy.

## References

- ADR-006 §Decision (original Newman roundtable memo, items 1–8) — the unabridged version lives in [ADR-006 appendix §A.3](./ADR-006-appendix-roundtable.md#a3--newman-security).
- OWASP SSRF Prevention Cheat Sheet
- Cloud metadata endpoint index: AWS IMDSv2, GCP metadata server, Azure IMDS
