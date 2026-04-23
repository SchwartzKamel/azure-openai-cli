# Security Policy

> **Azure OpenAI CLI** -- Security guidance for developers, operators, and contributors.

**See also:** [`docs/security/index.md`](docs/security/index.md) -- full index
of every security audit, review, and post-release verification. Start there
before opening a security issue.

---

## Table of Contents

1. [Security Overview](#1-security-overview)
2. [Credential Management](#2-credential-management)
3. [Container Security](#3-container-security)
4. [Input Validation](#4-input-validation)
5. [Configuration Security](#5-configuration-security)
6. [Network Security](#6-network-security)
7. [Dependency Security](#7-dependency-security)
8. [Reporting Vulnerabilities](#8-reporting-vulnerabilities)
9. [Exit Codes Reference](#9-exit-codes-reference)
10. [Security Checklist for Users](#10-security-checklist-for-users)
11. [Tool Security](#11-tool-security)
12. [DelegateTaskTool Security](#12-delegatetasktool-security)
13. [Ralph Mode Security](#13-ralph-mode-security)
14. [Subagent Attack Surface](#14-subagent-attack-surface)
15. [Repository Hardening Recommendations](#15-repository-hardening-recommendations)

---

## 1. Security Overview

The Azure OpenAI CLI is a containerized command-line tool that communicates with
Azure OpenAI endpoints over HTTPS. It is designed with the following security
principles:

| Principle | Implementation |
|---|---|
| **Isolation** | Every invocation runs inside a Docker container, limiting blast radius. |
| **Least Privilege** | The container process runs as a non-root user (`appuser`). |
| **Minimal Surface** | Alpine-based runtime image with only the libraries the binary requires. |
| **No Persistence** | Containers are ephemeral (`--rm`); no data is written to disk beyond user config preferences. |
| **Credential Separation** | API keys are injected at runtime via environment variables -- never baked into the image. |

### Threat Model Summary

The primary threats this project mitigates:

- **Credential leakage** -- credentials are excluded from the image and from version control.
- **Container escape / privilege escalation** -- non-root execution with a minimal OS layer.
- **Supply-chain compromise** -- dependency pinning and vulnerability scanning via Grype.
- **Data exfiltration** -- the CLI stores no prompt or response data locally.

---

## 2. Credential Management

### Required Credentials

The CLI requires three environment variables to operate:

| Variable | Purpose | Sensitive? |
|---|---|---|
| `AZUREOPENAIENDPOINT` | Azure OpenAI resource URL | Low |
| `AZUREOPENAIMODEL` | Deployment name(s), comma-separated | Low |
| `AZUREOPENAIAPI` | API key for authentication | **High** |
| `SYSTEMPROMPT` | System-level prompt text | Low |

### Secure Configuration Methods

#### Option A -- `--env-file` (Recommended)

Pass a `.env` file at runtime without embedding it in the image:

```bash
docker run --rm \
  --env-file ./azureopenai-cli/.env \
  azureopenai-cli:gpt-5-chat "Hello"
```

#### Option B -- Individual Environment Variables

```bash
docker run --rm \
  -e AZUREOPENAIENDPOINT="https://my-resource.openai.azure.com/" \
  -e AZUREOPENAIMODEL="gpt-4o" \
  -e AZUREOPENAIAPI="$(cat ~/.secrets/azure-oai-key)" \
  -e SYSTEMPROMPT="You are a helpful assistant" \
  azureopenai-cli:gpt-5-chat "Hello"
```

#### Option C -- Docker Secrets (Swarm / Compose)

For orchestrated environments, use Docker secrets to avoid exposing keys in
process listings:

```yaml
# docker-compose.yml
services:
  cli:
    image: azureopenai-cli:gpt-5-chat
    secrets:
      - azure_oai_key
    environment:
      AZUREOPENAIAPI_FILE: /run/secrets/azure_oai_key

secrets:
  azure_oai_key:
    file: ./secrets/api-key.txt
```

### What NOT To Do

| ❌ Anti-Pattern | Why It's Dangerous |
|---|---|
| Hard-coding keys in the Dockerfile | Keys end up in every image layer and registry pull. |
| Committing `.env` to version control | Keys are exposed to anyone with repo access (the `.gitignore` already excludes `.env`). |
| Passing keys via CLI arguments | Keys appear in `docker inspect`, shell history, and `/proc`. |
| Sharing a single API key across teams | No auditability; a leaked key affects everyone. |

### API Key Rotation

1. Generate a new key in the [Azure Portal](https://portal.azure.com) under your OpenAI resource → **Keys and Endpoint**.
2. Update your local `.env` file or secret store with the new key.
3. Rebuild or restart the container to pick up the change.
4. Revoke the old key in the Azure Portal once the new key is confirmed working.

**Recommendation:** Rotate API keys at least every 90 days, or immediately if a
compromise is suspected.

### Secret Redaction (v2)

Added in v2.0.4 (commit [`4842b6a`](https://github.com/SchwartzKamel/azure-openai-cli/commit/4842b6a),
resolving FDR High-severity finding `fdr-v2-err-unwrap` from
[`docs/audits/fdr-v2-dogfood-2026-04-22.md`](docs/audits/fdr-v2-dogfood-2026-04-22.md)).

The v2 binary (`az-ai`) redacts the `AZUREOPENAIAPI` key value and the
`AZUREOPENAIENDPOINT` hostname from every user-visible error surface before
anything is written to `stdout` or `stderr`. The helper is
`UnsafeReplaceSecrets` in `azureopenai-cli/Program.cs` (line 1348) --
the `Unsafe` prefix is a caller-side warning that the *input* contains
secrets; the *output* is the safe form to emit.

| Property | Detail |
|---|---|
| **What is redacted** | The full `AZUREOPENAIAPI` value (verbatim substring match) and the `AZUREOPENAIENDPOINT` URL *plus* its parsed hostname. Replacement token: `[REDACTED]`. |
| **Where it applies** | Every catch block that surfaces an Azure SDK or inner exception to the user -- covers standard mode, `--agent`, `--ralph`, and persona code paths (`Program.cs:604`, `Program.cs:616`, and the Ralph workflow). |
| **Unwrap depth** | Up to 5 levels of `InnerException` are unwrapped before redaction, so AOT-trim error chains (`RequestFailedException` → `TypeInitializationException` → …) surface actionable detail without leaking. |
| **Verification** | `ExceptionUnwrapTests` (7 cases) + `UserConfigQuietTests` (5 cases) under `tests/AzureOpenAI_CLI.V2.Tests/`. 485/485 v2 green at `4842b6a`. |
| **Null/empty safety** | Safe on null/empty `apiKey` or `endpoint`; short (< 4 char) API keys are skipped to avoid false-positive substitution of common tokens. |

**Limitation.** Redaction protects *displayed* error messages. It does
**not** protect:

- API keys passed on the command line with `--set-api-key` -- shell history,
  `ps` output, `/proc/<pid>/cmdline`, and process-monitoring tools will
  still capture the raw value. **Prefer the `AZUREOPENAIAPI` env var or
  the UserConfig file (`~/.azureopenai-cli.json`) with `chmod 600`.**
- Logs written by an upstream proxy, sidecar, or orchestrator that sees
  the TLS-terminated traffic. Redaction is an application-level control.
- Arbitrary secrets not matching `AZUREOPENAIAPI` or the endpoint -- the
  blocklist is intentionally narrow to keep the false-positive rate low.

**Threat-model note.** Pre-v2.0.4, `ex.Message` was printed raw in the
global catch, so an operator debugging a 401 would see their API key
echoed in a `RequestFailedException` chain. Impact: key exposure in
terminal scrollback, CI job logs, and shared-screen troubleshooting
sessions. Mitigation: centralized redaction helper, unit-tested on every
error path. Residual: command-line and upstream-log exposure (above).

### Azure RBAC Recommendations

- Use **service principals** with the `Cognitive Services OpenAI User` role
  scoped to the specific OpenAI resource -- not the entire subscription.
- Avoid using personal Azure credentials in automated or shared environments.
- Where possible, prefer **Azure Managed Identity** over API keys to eliminate
  static credentials entirely.
- Apply the principle of least privilege: grant only the permissions the CLI
  actually needs.

---

## 3. Container Security

### Alpine-Based Minimal Image

The runtime stage uses `mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine`,
which provides:

- A minimal Linux user-space (~5 MB base).
- Reduced attack surface compared to Debian- or Ubuntu-based images.
- Fewer pre-installed packages that could contain vulnerabilities.

### Non-Root Execution

The Dockerfile creates a dedicated system user and group:

```dockerfile
RUN addgroup --system appgroup \
 && adduser --system --ingroup appgroup appuser
# ...
USER appuser
```

The process cannot:

- Bind to privileged ports (< 1024).
- Modify system files or install packages.
- Access other users' files.

### Self-Contained Binary

- `az-ai` is published as a **NativeAOT** single-file ELF
  (`linux-x64`, `linux-musl-x64`, `osx-arm64`, `win-x64`). Build flags:
  `--self-contained -p:PublishAot=true`
  (see [`Dockerfile:65`](Dockerfile) and the csproj).

The binary does not need the .NET runtime installed on the
host; only native OS dependencies (`icu-libs`) are required in the
container. The AOT output has no managed-fallback path -- a reflection
access that the trimmer dropped will surface as
`TypeInitializationException` at runtime rather than JIT-compiling on
demand. Trim-related findings are tracked in
[`docs/aot-trim-investigation.md`](docs/aot-trim-investigation.md) and the
unwrap-and-redact chain in [`docs/security/redaction.md`](docs/security/redaction.md)
is the compensating control against error-path credential leakage.

### Image Digest Pinning (Default)

The shipped Dockerfile **already pins the base image by digest** to defeat
tag-mutation supply-chain attacks:

```dockerfile
# Instead of:
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine
# Use:
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine@sha256:<digest>
```

Retrieve the current digest:

```bash
docker inspect --format='{{index .RepoDigests 0}}' \
  mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine
```

### Regular Base Image Updates

- Subscribe to [Microsoft .NET Docker image announcements](https://github.com/dotnet/dotnet-docker/discussions)
  for security patches.
- Rebuild images after base image updates: `make clean && make build`.
- Run `make scan` after each rebuild to verify no new vulnerabilities are
  introduced.

### Additional Container Hardening

For high-security environments, consider adding these flags at runtime:

```bash
docker run --rm \
  --read-only \
  --tmpfs /tmp:rw,noexec,nosuid,size=64m \
  --cap-drop=ALL \
  --security-opt=no-new-privileges \
  --env-file ./azureopenai-cli/.env \
  azureopenai-cli:gpt-5-chat "Hello"
```

| Flag | Purpose |
|---|---|
| `--read-only` | Makes the container filesystem immutable. |
| `--tmpfs /tmp` | Provides a writable `/tmp` for the .NET bundle extractor. |
| `--cap-drop=ALL` | Drops all Linux capabilities. |
| `--security-opt=no-new-privileges` | Prevents privilege escalation via setuid/setgid. |

---

## 4. Input Validation

### Prompt Handling

- User prompts are passed as command-line arguments and joined into a single
  string (`string.Join(' ', args)`).
- The prompt is sent directly to the Azure OpenAI API, which enforces its own
  content filtering and token limits.
- The Azure OpenAI SDK handles request serialization and escaping.

### Startup Configuration Validation

The CLI validates required configuration on startup before making any API calls:

```csharp
if (string.IsNullOrEmpty(azureOpenAiEndpoint))
    throw new ArgumentNullException(nameof(azureOpenAiEndpoint), "Azure OpenAI endpoint is not set.");
if (string.IsNullOrEmpty(azureOpenAiApiKey))
    throw new ArgumentNullException(nameof(azureOpenAiApiKey), "Azure OpenAI API key is not set.");
```

If validation fails, the CLI exits with code `1` and prints a descriptive error
to `stderr` -- no API call is attempted.

### Model Selection Validation

When setting a model via `--set-model`, the CLI verifies the requested model
exists in the configured `AvailableModels` list (case-insensitive match). Invalid
model names are rejected with an error and a list of valid options.

### Request Parameters

The CLI sends requests with bounded parameters:

| Parameter | Value | Purpose |
|---|---|---|
| `MaxOutputTokenCount` | 10,000 | Caps response length to control cost and latency. |
| `Temperature` | 0.55 | Balances creativity and determinism. |
| `TopP` | 1.0 | Nucleus sampling threshold. |
| `FrequencyPenalty` | 0.0 | No frequency penalty applied. |
| `PresencePenalty` | 0.0 | No presence penalty applied. |

---

## 5. Configuration Security

### Config File Location

User preferences are stored in a JSON file at:

```text
~/.azureopenai-cli.json
```

This path resolves to `Environment.SpecialFolder.UserProfile` on all platforms.

### What Is Stored

The config file contains **only** model preferences:

```json
{
  "ActiveModel": "gpt-4o",
  "AvailableModels": [
    "gpt-4",
    "gpt-35-turbo",
    "gpt-4o"
  ]
}
```

**No credentials, API keys, endpoints, or prompt history are stored in this file.**

### File Permissions

Set owner-only read/write permissions on the config file:

```bash
chmod 600 ~/.azureopenai-cli.json
```

Inside a container, the file is owned by `appuser` and located under the
`appuser` home directory, which is inaccessible to other users.

### `.env` File Permissions

Your `.env` file contains the API key. Protect it on the host:

```bash
chmod 600 azureopenai-cli/.env
```

Verify it is excluded from version control:

```bash
grep '.env' .gitignore
# Expected: azureopenai-cli/.env
```

### `--raw` / `--json` Silent-stderr Contract

Both `--raw` (pipeline-friendly plaintext) and `--json` (machine-readable
envelope) deliberately suppress **config-parse** warnings on stderr. The
contract, hardened in v2.0.4 (`UserConfig.Load(quiet:)`), is:

- A successful run under `--raw` / `--json` emits **nothing** on stderr.
- A failing run still emits a **redacted** error on stderr (see
  [`docs/security/redaction.md`](docs/security/redaction.md)) -- silence
  applies to advisories, not to genuine errors.

This matters for security-adjacent pipelines -- Espanso / AHK hotkeys that
tee output, CI jobs that treat any stderr byte as a warning signal. The
contract ensures `az-ai --raw` never leaks home-dir config paths into
a shared log surface when the hot path is working correctly.

---

## 6. Network Security

### TLS/HTTPS Enforcement

All communication with Azure OpenAI endpoints uses HTTPS (TLS 1.2+). The Azure
SDK enforces this -- plain HTTP endpoints are rejected.

```text
AZUREOPENAIENDPOINT=https://your-resource.openai.azure.com/
                    ^^^^^^
                    HTTPS is required
```

### Azure Private Endpoints (Enterprise)

For enterprise deployments where the Azure OpenAI resource should not be
accessible from the public internet:

1. Create an [Azure Private Endpoint](https://learn.microsoft.com/en-us/azure/ai-services/cognitive-services-virtual-networks)
   for your OpenAI resource.
2. Configure DNS resolution so the endpoint hostname resolves to the private IP.
3. Run the CLI container within the same virtual network (or a peered network).

This ensures all traffic stays within the Azure backbone and never traverses the
public internet.

### No Local Data Storage

- **Prompts** are not logged or persisted locally.
- **Responses** are streamed to `stdout` and not written to disk.
- **No telemetry** is collected by the CLI itself.
- The only file written is the model-preference config (`~/.azureopenai-cli.json`).

### Network Egress

The container only needs outbound HTTPS (port 443) to your Azure OpenAI endpoint.
In firewall-restricted environments, allowlist:

```text
*.openai.azure.com:443
```

---

## 7. Dependency Security

> **Canonical references for this section:**
>
> - [`docs/security/sbom.md`](docs/security/sbom.md) -- CycloneDX SBOM generation, storage, freshness policy.
> - [`docs/security/supply-chain.md`](docs/security/supply-chain.md) -- NuGet pinning, feed trust, SLSA provenance.
> - [`docs/security/scanners.md`](docs/security/scanners.md) -- Trivy (CI gate) vs Grype (local convenience).
> - [`docs/security/cve-log.md`](docs/security/cve-log.md) -- advisory register.
>
> The tables below are the short story for operators skimming the policy.
> For the full transitive closure that shipped with a given release,
> **download the SBOM** attached to that release -- it is the source of
> truth, not the list below.

### Current Dependencies (v2, reader convenience)

| Package | Version | Purpose |
|---|---|---|
| `Microsoft.Agents.AI` | 1.1.0 | MAF parent agent runtime |
| `Microsoft.Agents.AI.OpenAI` | 1.1.0 | OpenAI channel for MAF |
| `Azure.AI.OpenAI` | 2.1.0 (GA) | Azure OpenAI SDK client |
| `dotenv.net` | 3.1.2 | `.env` file parser |
| `OpenTelemetry` (+ `Api`, `Exporter.OpenTelemetryProtocol`) | 1.15.2 | Opt-in telemetry (OTLP) |

### Legacy Dependencies (v1, 1.8.x / 1.9.x maintenance)

| Package | Version | Purpose |
|---|---|---|
| `Azure.AI.OpenAI` | 2.3.0-beta.1 | Azure OpenAI SDK client |
| `Azure.Core` | 1.47.2 | Core Azure SDK libraries (HTTP pipeline, auth) |
| `dotenv.net` | 3.1.2 | `.env` file parser |

### Runtime Image Dependencies

| Package | Source | Purpose |
|---|---|---|
| `icu-libs` | Alpine APK | Unicode / globalization support |

### Vulnerability Scanning

Two scanners coexist deliberately -- see
[`docs/security/scanners.md`](docs/security/scanners.md) for the long form.
The short version:

- **CI gate (authoritative):** Trivy, pinned at
  `aquasecurity/trivy-action@57a97c7 # v0.35.0` in
  [`.github/workflows/ci.yml:119`](.github/workflows/ci.yml). Runs on
  every PR and push to `main`; fails on `HIGH,CRITICAL`.
- **Local developer convenience:** Grype via `make scan` (not a gate).
  Useful for a quick local check before opening a PR:

  ```bash
  make scan    # wraps: grype azureopenai-cli:gpt-5-chat
  ```

  Install Grype locally if you want it:

  ```bash
  curl -sSfL https://raw.githubusercontent.com/anchore/grype/main/install.sh | sh -s -- -b /usr/local/bin
  ```

If Trivy (CI) is clean and Grype (local) disagrees, **CI wins**; log the
delta in [`docs/security/cve-log.md`](docs/security/cve-log.md) as
`status: grype-delta-only`.

### Update Policy Recommendations

| Action | Frequency |
|---|---|
| Run `make scan` | After every build and before every release. |
| Update NuGet packages | Monthly, or immediately when a security advisory is published. |
| Rebuild base image | Weekly in CI, or whenever Microsoft publishes a patch. |
| Review Grype findings | Triage all `Critical` and `High` findings before deploying. |

### Updating Dependencies

```bash
# Update all NuGet packages to latest compatible versions
cd azureopenai-cli
dotnet list package --outdated
dotnet add package Azure.AI.OpenAI --prerelease
dotnet add package Azure.Core
dotnet add package dotenv.net
```

After updating, rebuild and scan:

```bash
make clean && make build && make scan
```

---

## 8. Reporting Vulnerabilities

We take security issues seriously. If you discover a vulnerability, please
report it responsibly.

### Supported Versions

The **v2 line** (Microsoft Agent Framework rebuild) is current and receives
full security support. The **v1 line** (handrolled loop) is in
security-only maintenance through **2026-10-22** (six months post-v2.0.4
cutover) to give operators a bounded window to migrate; no v1 feature work
will be accepted. Older lines are unsupported.

| Version | Status | Support policy |
|---|---|---|
| `2.0.x` (current -- v2.0.4, v2.0.5 rolling) | ✅ Active | Full security + feature support. All new fixes land here first. |
| `1.8.x` | ⚠️ Maintenance | Security-only patches through **2026-10-22**. No new features. Critical fixes backported on a best-effort basis. |
| `< 1.8` | ❌ Unsupported | Please upgrade. No patches will be issued. |

After 2026-10-22, v1 moves to end-of-life and will receive no further
patches, regardless of severity. Migration guide:
[`docs/migration-v1-to-v2.md`](docs/migration-v1-to-v2.md).

### How to Report

| Method | Details |
|---|---|
| **GitHub Security Advisories** (Preferred) | Go to the repository → **Security** tab → **Report a vulnerability**. This creates a private advisory visible only to maintainers. |
| **GitHub Issues** | If the issue is low-severity and does not expose exploit details, you may open a regular GitHub Issue with the `security` label. |

### What to Include

Please provide as much of the following as possible:

- **Description** of the vulnerability and its potential impact.
- **Steps to reproduce**, including CLI version, Docker version, and OS.
- **Affected component** (e.g., Dockerfile, dependency, CLI logic).
- **Suggested fix**, if you have one.

### Response Timeline

| Stage | Target |
|---|---|
| Acknowledgment | Within 48 hours |
| Initial assessment | Within 5 business days |
| Fix or mitigation | Depends on severity; critical issues are prioritized |
| Public disclosure | Coordinated with the reporter after a fix is available |

### Scope

The following are **in scope**:

- Credential leakage via container image or logs.
- Privilege escalation within the container.
- Dependency vulnerabilities with a known exploit.
- Injection or unexpected behavior via crafted prompts.

The following are **out of scope**:

- Vulnerabilities in Azure OpenAI service itself (report to [Microsoft MSRC](https://msrc.microsoft.com/)).
- Social engineering attacks.
- Denial of service via large prompts (rate-limited by Azure).

### Safe Harbor

Good-faith security research conducted in line with this policy will not
be pursued legally. Specifically, we will not initiate or support legal
action against researchers who:

- Report vulnerabilities through the channels above (GitHub Security
  Advisories preferred) before any public disclosure.
- Avoid privacy violations, destruction of data, or degradation of
  service for other users.
- Do not exfiltrate more data than is necessary to prove the issue.
- Give us a reasonable window -- matching the [Response Timeline](#response-timeline)
  above -- to investigate and ship a fix before disclosure.

This clause applies to the code and infrastructure in this repository.
It does not extend to Azure OpenAI service itself -- follow MSRC's rules
for that surface.

---

## 9. Exit Codes Reference

The CLI uses structured exit codes to distinguish between error categories:

| Exit Code | Meaning | Example Scenario |
|---|---|---|
| `0` | **Success** | Prompt processed and response streamed successfully. |
| `1` | **Validation / usage error** *or* **Ralph `--max-iterations` exhausted without a validation pass** (v2.0.4+). | Missing arguments, invalid model name, missing environment variables; Ralph ran the full iteration budget without the validator returning OK. |
| `2` | **CLI parse error** | Unknown flag, malformed subcommand (v2 convention). |
| `99` | **Unhandled error** | Unexpected exception (network failure, SDK error, etc.). |
| `130` | **SIGINT / user interrupt** (preserved end-to-end by Ralph workflow, v2.0.4+) | Operator hit Ctrl-C mid-run. Always safe to abandon; do **not** retry automatically in CI. |

### Security Implications of Exit Codes

- **Exit 1** indicates a configuration problem or an exhausted Ralph loop.
  Scripts should halt and alert the operator -- do not retry with the same
  configuration.
- **Exit 2** indicates a malformed invocation; re-check the argv before
  retry.
- **Exit 99** may indicate a transient error (network timeout) or a
  misconfiguration. Check `stderr` for the `[UNHANDLED ERROR]` message before
  retrying.
- **Exit 130** is operator intent -- **never** retry automatically on this
  code. A supervisor that re-runs on `130` will loop the operator's
  Ctrl-C against itself.
- In CI/CD pipelines, treat any non-zero exit as a failure and avoid logging
  the full error output to public dashboards (it is redacted of API key +
  endpoint per [`docs/security/redaction.md`](docs/security/redaction.md),
  but stack frames and file paths remain visible).

---

## 10. Security Checklist for Users

Use this checklist when deploying or operating the Azure OpenAI CLI:

### Before First Use

- [ ] Created `.env` from `.env.example` -- not from copy-paste in a chat or email.
- [ ] Set file permissions: `chmod 600 azureopenai-cli/.env`.
- [ ] Verified `.env` is listed in `.gitignore`.
- [ ] Used a dedicated Azure service principal with `Cognitive Services OpenAI User` role.
- [ ] Scoped the service principal to the specific OpenAI resource (not the subscription).

### At Build Time

- [ ] Built the image locally or in a trusted CI environment: `make build`.
- [ ] Ran a vulnerability scan: `make scan`.
- [ ] Reviewed and resolved all `Critical` and `High` findings.
- [ ] Did **not** copy `.env` or credentials into the Docker image.

### At Runtime

- [ ] Passed credentials via `--env-file` or environment variables -- not CLI arguments.
- [ ] Used `--rm` to remove the container after execution (default in `make run`).
- [ ] (Production) Pinned the base image by digest in the Dockerfile.
- [ ] (Production) Applied additional hardening flags (`--read-only`, `--cap-drop=ALL`).

### Ongoing Operations

- [ ] Rotating API keys at least every 90 days.
- [ ] Rebuilding images when base image updates are available.
- [ ] Running `make scan` after each rebuild.
- [ ] Monitoring Azure activity logs for unexpected API usage.
- [ ] (Enterprise) Using Azure Private Endpoints to restrict network access.

---

## 11. Tool Security

> Added in v1.3.0 -- documents the security hardening applied to all built-in
> agent tools.

### Tool Security Protections

#### ReadFileTool

| Protection | Detail |
|---|---|
| **Defense-in-depth validation pipeline** (S02E26) | NFKC-normalize → tilde-expand → `Path.GetFullPath` (canonicalize `..`, collapse `//`, strip trailing `/`) → exact-prefix blocklist match. Substring-on-raw-input is forbidden. Mirrors the E32 shell-exec structural rewrite. |
| **Evasion rejection** (S02E26) | Control bytes (including NUL), percent-encoded path segments (`%2E`, `%2F`, `%00`), and invalid Unicode are rejected up front as they indicate bypass intent rather than legitimate filenames. |
| **Symlink traversal** | Resolves symlinks via `File.ResolveLinkTarget` and re-checks the final target against the blocklist -- prevents aliasing a readable path to a sensitive one. |
| **Prefix-based path blocking** | Blocks all files *under* sensitive directories, not just exact paths (e.g., `/root/.ssh/id_rsa` is blocked, not only `/root/.ssh`). |
| **File size cap** | 256 KB maximum -- prevents memory exhaustion from large files. |

Blocked path prefixes (system-level + per-user credential stores). S02E26
*The Locked Drawer* extended the per-user list to close the 7
`e23-readfile-*` gaps logged in S02E23 *The Adversary* -- OpenSSH user
keys (`~/.ssh`), Kubernetes cluster creds (`~/.kube`), GPG keyrings
(`~/.gnupg`), machine/login/password creds (`~/.netrc`), Docker
registry auth (`~/.docker/config.json`), git credential store
(`~/.git-credentials`, `~/.config/git/credentials`), and
package-registry upload tokens (`~/.npmrc`, `~/.pypirc`). GitHub CLI
OAuth tokens (`~/.config/gh/hosts.yml`) added as same-shape bonus.

```text
# System
/etc/shadow
/etc/passwd
/etc/sudoers
/etc/hosts
/root/.ssh
/proc/self/environ
/proc/self/cmdline
/var/run/secrets
/run/secrets
/var/run/docker.sock

# Per-user credential stores
~/.aws
~/.azure
~/.config/az-ai
~/.azureopenai-cli.json
~/.ssh
~/.kube
~/.gnupg
~/.netrc
~/.docker/config.json
~/.git-credentials
~/.config/git/credentials
~/.npmrc
~/.pypirc
~/.config/gh/hosts.yml
```

Ground truth: [`azureopenai-cli/Tools/ReadFileTool.cs`](azureopenai-cli/Tools/ReadFileTool.cs).
Adversarial coverage: [`tests/AzureOpenAI_CLI.Tests/Adversary/ReadFileSensitivePathTests.cs`](tests/AzureOpenAI_CLI.Tests/Adversary/ReadFileSensitivePathTests.cs).

#### ShellExecTool

Ground truth: [`azureopenai-cli/Tools/ShellExecTool.cs`](azureopenai-cli/Tools/ShellExecTool.cs).
Proof-of-coverage: `tests/AzureOpenAI_CLI.V2.Tests/ToolHardeningTests.cs`.

| Protection | Detail |
|---|---|
| **Destructive command blocklist** | `rm`, `rmdir`, `mkfs`, `dd`, `shutdown`, `reboot`, `halt`, `poweroff`, `kill`, `killall`, `pkill`, `format`, `del`, `fdisk`, `passwd` (`ShellExecTool.cs:17-18`) |
| **Privilege / interactive blocklist** | `sudo`, `su`, `crontab`, `vi`, `vim`, `nano`, `nc`, `ncat`, `netcat`, `wget` (`ShellExecTool.cs:19-20`) |
| **Command-substitution block** | `$(...)` and backticks rejected (`ShellExecTool.cs:53`) |
| **Process-substitution block** | `<(...)` and `>(...)` rejected (`ShellExecTool.cs:57`) |
| **`eval` / `exec` prefix block** | Rejected as first token or after pipe/chain (`ShellExecTool.cs:58-59`) |
| **Tab/newline first-token rescan** | K-1 hardening (v2.0.2) -- splits on tabs+newlines, not just spaces, so `"\trm"` does not bypass the blocklist (`ShellExecTool.cs:69`) |
| **Pipe-chain analysis** | Scans through `\|`, `;`, `&` for blocked commands -- prevents bypass via chaining. |
| **HTTP-write / upload block** | `ContainsHttpWriteForms` rejects `curl`/`wget` bodies + upload verbs (`-X POST/PUT/DELETE`, `--data`, `-T`, `-F`) to defeat exfiltration (`ShellExecTool.cs:79-80, 131-186`) |
| **Env scrub** | `SensitiveEnvVars` are unset on the child before `execve` so AZUREOPENAIAPI et al. never reach the shelled command (`ShellExecTool.cs:32-42`) |
| **Output cap** | 64 KB stdout, 16 KB stderr -- prevents memory exhaustion from verbose commands. |
| **Timeout** | 10 seconds with `Process.Kill(entireProcessTree: true)` -- prevents long-running or hanging processes and their children. |
| **Stdin closed** | Child process stdin is closed immediately to prevent interactive command hanging. |

#### WebFetchTool

| Protection | Detail |
|---|---|
| **HTTPS-only** | Rejects any URL not using the `https://` scheme. |
| **DNS rebinding protection** | Resolves hostnames before connecting and blocks requests to private / reserved IP ranges. |
| **Redirect limit** | Maximum 3 automatic redirects to prevent redirect loops and open-redirect abuse. |
| **Response size cap** | 128 KB -- prevents memory exhaustion from large downloads. |
| **Timeout** | 10 seconds per request. |

Blocked IP ranges (DNS rebinding protection):

| Range | Description |
|---|---|
| `127.0.0.0/8` | IPv4 loopback |
| `10.0.0.0/8` | RFC 1918 private |
| `172.16.0.0/12` | RFC 1918 private |
| `192.168.0.0/16` | RFC 1918 private |
| `169.254.0.0/16` | Link-local |
| `::1` | IPv6 loopback |
| `fd00::/8` | IPv6 unique-local |
| `fe80::/10` | IPv6 link-local |
| IPv4-mapped IPv6 | e.g., `::ffff:127.0.0.1` -- also blocked |

#### GetClipboardTool

| Protection | Detail |
|---|---|
| **Content size cap** | 32 KB with truncation warning returned to the model. |
| **Platform-adaptive detection** | Looks up clipboard commands via `PATH` (`xclip`, `xsel`, `pbpaste`, `powershell`) -- never hard-codes absolute paths. |
| **Timeout** | 5 seconds -- prevents hanging when no clipboard provider is available. |

#### ToolRegistry

| Protection | Detail |
|---|---|
| **Exact alias matching** | Uses a dictionary of explicit aliases -- not substring search -- to prevent unintended tool activation. |
| **Short aliases** | `shell` → `shell_exec`, `file` → `read_file`, `web` → `web_fetch`, `clipboard` → `get_clipboard`, `datetime` → `get_datetime` |

### Agent Mode Safety

| Control | Detail |
|---|---|
| **Maximum tool-calling rounds** | Default: 5, configurable via `--max-rounds`. Prevents infinite loops. |
| **Operation timeout** | Bounds the entire agent session duration. |
| **Tool choice** | Set to `"auto"` only when tools are present; otherwise omitted entirely. |

---

## 12. DelegateTaskTool Security

> Added in v1.4.0 as an out-of-process design; **rewritten in v2.0.0** as
> an in-process Microsoft Agent Framework (MAF) child-agent model. This
> section describes the **v2 reality**. See the bottom of this section for
> notes on the v1 model and why it was replaced.

The `delegate_task` tool spawns a child agent to handle a sub-task. In v2
the child runs **in-process**, sharing the parent's `IChatClient`, so
hardening is enforced by language-level invariants (static config,
`AsyncLocal<int>` depth counter) rather than by OS process boundaries.
Ground truth: [`azureopenai-cli/Tools/DelegateTaskTool.cs`](azureopenai-cli/Tools/DelegateTaskTool.cs).

### Architecture (v2)

- **In-process MAF child agents.** A child agent is a new MAF agent built
  from the parent's `IChatClient` (`DelegateTaskTool.cs:10-35`). There is
  no `Process.Start`, no binary re-locate, no shell-argument quoting, no
  credential re-plumbing, no subprocess stdout/stderr capture.
- **Shared memory space.** Parent and child live in the same .NET process
  and share the managed heap. This is a **trust assumption**, not a
  weakness: a child agent already operates on behalf of the same user with
  the same credentials the parent already has. Treat it as "same blast
  radius as the parent," not as a sandbox boundary.
- **Tool allowlist, not tool inheritance.** The child receives **only** the
  tools named in the `tools` parameter (default: `shell_exec, read_file,
  web_fetch, get_datetime` -- mirrors `ToolRegistry.DefaultChildAgentTools`,
  excludes `get_clipboard` and `delegate_task` itself). The parent's full
  registry is never passed through.

### Recursion Depth Cap

Depth is tracked via an `AsyncLocal<int>` counter
(`DelegateTaskTool.cs:34`, `s_depth`), **not** an environment variable.
This replaces the v1 `RALPH_DEPTH` marshalling scheme entirely.

```csharp
internal const int MaxDepth = 3;
// ...
private static readonly AsyncLocal<int> s_depth = new();
// ...
var currentDepth = s_depth.Value;
if (currentDepth >= MaxDepth)
    return $"Error: maximum delegation depth ({MaxDepth}) reached. ...";
```

**Why `AsyncLocal<T>`?** Nested delegations in the same logical flow see a
monotonic depth, while parallel delegation roots stay isolated per-flow.
There is no environment-variable round-trip, no subprocess spawn, and no
way for a child to forge a lower depth than its parent -- the parent
increments `s_depth.Value` around the child's `RunAsync` call.

**Hard limit:** 3 levels. Enforced in code, not convention.

### No Credential Re-plumbing

Because the child runs in-process and reuses the parent's `IChatClient`,
there is nothing to marshal: the Azure SDK handle, the API key, and the
endpoint already live in process memory. There is no env-var allowlist
to maintain, no `Process.Start` with a scrubbed `Environment` dictionary,
and no risk of a stale CI token leaking from an overlooked inherited
variable.

**Security property:** child inherits exactly the parent's capability set
-- no more, no less.

### No Subprocess Boundary

There is no `/proc` entry to read, no stdin/stdout to capture, no
`entireProcessTree: true` kill to perform. Containment comes from:

1. The depth cap (above).
2. The tool allowlist (above) -- a child cannot call `delegate_task`
   unless the parent explicitly included it.
3. Each child tool enforces its **own** hardening (see §11):
   `ShellExecTool` blocklist + 10 s timeout + output cap;
   `ReadFileTool` path prefix-blocking + symlink resolution + 256 KB cap;
   `WebFetchTool` HTTPS-only + DNS-rebinding block + redirect limit.

### Configuration Lifecycle

The tool is wired exactly once at startup via
`DelegateTaskTool.Configure(chatClient, baseInstructions, model)` in
`Program.cs` after the parent `IChatClient` is built. The static fields
(`s_chatClient`, `s_baseInstructions`, `s_model`) are set once; tests may
call `ResetForTests()` to clear them. A child agent spawned before
`Configure` has run returns an error and never contacts the model.

### Threat-Model Summary (v2)

| Threat | Mitigation | Residual |
|---|---|---|
| Infinite recursion | `AsyncLocal<int>` depth ≤ 3 | Low -- enforced at call site, no env-var spoofing path |
| Tool escalation by child | Explicit tool-name allowlist per call; `delegate_task` excluded from default child toolset | Low -- parent must opt in by name |
| Credential exfiltration via child | In-process shared `IChatClient`; no env var marshalling | **Trust assumption:** child has the parent's full Azure credentials by design |
| Prompt injection in child response | Child output returned as tool result, not promoted to system prompt | Medium -- parent model may trust child text; mitigated by child tool hardening |
| Resource exhaustion via nested delegation | Depth cap × per-tool timeouts (shell 10 s, web 10 s, read-file 256 KB) | Low |

### v1 → v2 migration note

The v1.4.0 design used `Process.Start` to re-launch the CLI as a child,
passed credentials through a 5-element env-var allowlist, enforced depth
via the `RALPH_DEPTH` env var, and applied a 60 s subprocess timeout with
`process.Kill(entireProcessTree: true)`. That architecture is retired.
Threat modelers working on v2 should **ignore** references to
`Process.Start`, `RALPH_DEPTH`, `DefaultTimeoutMs = 60_000`, and env-var
marshalling -- none of those constructs exist in the v2 delegation path.
Prior Newman audits referencing them (`docs/security-review-v2.md`,
`docs/security/reaudit-v2-phase5.md`) predate this rewrite; see
[`docs/security/index.md`](docs/security/index.md) for the full report
timeline.

---

## 13. Ralph Mode Security

> Added in v1.4.0 -- documents security controls for Ralph mode, the autonomous
> iteration loop (`--ralph`).

Ralph mode runs the agent in an autonomous loop: execute a task, optionally
validate, and retry on failure. Several controls bound the resources and
attack surface of this loop.

### Iteration Limit

The `--max-iterations` flag is bounded to a range of **1-50** (default: 10).
Values outside this range are rejected at CLI argument parsing:

```csharp
int maxIterations = 10;
// ...
return (null, new CliParseError("[ERROR] --max-iterations requires a value between 1 and 50", 1));
```

### Stateless Iterations

Each iteration builds a **fresh message list** -- there is no conversation
history accumulation across iterations. This prevents prompt injection
artifacts from persisting between iterations:

```csharp
// Build fresh messages for each iteration (stateless -- the Ralph way)
var messages = new List<ChatMessage>
{
    new SystemChatMessage(effectiveSystemPrompt + ...),
    new UserChatMessage(currentPrompt),
};
```

Previous iteration context (error messages, validation output) is injected
only through the user prompt, not through accumulated assistant messages.

### Validation Sandboxing

The `--validate` command runs via `/bin/sh -c` as a separate process with the
same timeout as the main agent session. The validation process has `stdin`
closed immediately:

```csharp
FileName = "/bin/sh",
Arguments = $"-c \"{command.Replace("\"", "\\\"")}\"",
// ...
process.StandardInput.Close();
```

On timeout, the validation process tree is killed:

```csharp
if (!process.HasExited) process.Kill(entireProcessTree: true);
```

### File-Based State (Wiggum Pattern)

Ralph mode uses the filesystem for state persistence between iterations.
The agent reads and writes files via the existing `ReadFileTool` and
`ShellExecTool`, which enforce their own security controls:

- `ReadFileTool` -- path blocking, symlink resolution, 256 KB size cap.
- `ShellExecTool` -- command blocklist, output cap, 10-second timeout.

No in-memory state is shared between iterations beyond the constructed prompt.

### `.ralph-log` File

A `.ralph-log` file is written to the **current directory** on a best-effort
basis. Failures to write the log are silently caught and never cause the loop
to abort:

```csharp
static void WriteRalphLog(string content)
{
    try { File.WriteAllText(".ralph-log", ...); }
    catch { /* best-effort logging */ }
}
```

### No Credential Exposure in Logs

Agent responses are truncated in `.ralph-log` to prevent credential leakage
from tool outputs:

| Field | Truncation |
|---|---|
| Prompt | 200 characters |
| Agent response | 500 characters |
| Validation output | 2,000 characters |

---

## 14. Subagent Attack Surface

> Added in v1.4.0 -- threat model for DelegateTaskTool and Ralph mode operating
> together.

### Threat Model

| Threat | Mitigation | Residual Risk |
|---|---|---|
| Infinite recursion | `AsyncLocal<int>` depth cap (3) in `DelegateTaskTool` (v2) | Low -- hard limit enforced, no env-var spoofing path |
| Resource exhaustion | Per-tool timeouts (shell 10 s, web 10 s) + output caps | Low -- enforced at tool level |
| Credential leakage to child | In-process shared `IChatClient` (v2) -- no env-var marshalling | Trust assumption: child has parent's full capabilities by design |
| Prompt injection via child response | Child output treated as tool result, not system prompt | Medium -- model may trust child output |
| Ralph loop denial of service | Max 50 iterations, each with timeout | Low -- bounded resource use |
| Validation command injection | Command comes from CLI flags (user-controlled, not model-controlled) | Low -- user trusts their own flags |

### Defense-in-Depth Summary

```text
┌──────────────────────────────────────────────┐
│  CLI Argument Parsing                        │
│  • --max-iterations capped at 1-50           │
│  • --validate is user-supplied, not model     │
├──────────────────────────────────────────────┤
│  Ralph Loop                                  │
│  • Stateless iterations (fresh messages)     │
│  • Validation subprocess sandboxed           │
│  • .ralph-log truncated to prevent leaks     │
├──────────────────────────────────────────────┤
│  DelegateTaskTool (v2 -- in-process MAF)      │
│  • AsyncLocal<int> depth ≤ 3                 │
│  • Per-call tool allowlist (delegate excl.)  │
│  • Shared IChatClient -- no cred re-plumb     │
├──────────────────────────────────────────────┤
│  Child Agent Tools                           │
│  • ShellExecTool: command blocklist, 10 s    │
│  • ReadFileTool: path blocking, 256 KB       │
│  • WebFetchTool: HTTPS-only, DNS rebinding   │
└──────────────────────────────────────────────┘
```

---

## Further Reading

- [Azure OpenAI Security Best Practices](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/managed-identity)
- [Docker Security Documentation](https://docs.docker.com/engine/security/)
- [CIS Docker Benchmark](https://www.cisecurity.org/benchmark/docker)
- [Grype Vulnerability Scanner](https://github.com/anchore/grype)
- [Azure RBAC for Cognitive Services](https://learn.microsoft.com/en-us/azure/ai-services/authentication)

---

## 15. Repository Hardening Recommendations

> Advisory guidance for maintainers and forkers. These controls cannot be
> fully expressed in code and must be configured in the GitHub UI.

### Branch Protection (`main`)

The following rules should be enforced via **Settings → Branches → Branch
protection rules** (or the equivalent Rulesets) for the `main` branch:

| Rule | Setting |
|---|---|
| Require a pull request before merging | ✅ -- at least **1** approving review |
| Dismiss stale approvals on new commits | ✅ Yes |
| Require review from Code Owners | ✅ (when `CODEOWNERS` exists) |
| Require status checks to pass before merging | ✅ -- `build-and-test`, `integration-test`, `docker` |
| Require branches to be up to date before merging | ✅ Yes |
| Require signed commits | ✅ Yes |
| Require linear history | ✅ (recommended) |
| Include administrators | ✅ Yes |
| Restrict who can push to matching branches | ✅ -- maintainers only |
| Allow force pushes | ❌ Disabled |
| Allow deletions | ❌ Disabled |

### Tag Protection

Release tags (`v*`) should be protected to prevent retroactive tag hijacking:

- **Settings → Tags → New rule** → pattern `v*` → restrict to maintainers.

### Actions Hardening

- **Settings → Actions → General**:
  - Allow only GitHub-owned and verified actions, plus the SHAs explicitly
    pinned in this repository.
  - Workflow permissions: **Read repository contents** (default). Workflows
    opt into more via `permissions:` blocks.
  - Require approval for all outside contributor workflow runs.

### Vulnerability Reporting Infrastructure

- **Private vulnerability reporting**: enable under **Settings → Code security
  and analysis → Private vulnerability reporting**.
- **Dependabot alerts + security updates**: enable.
- **Dependency graph**: enable.
- **Code scanning**: OpenSSF Scorecards runs weekly (`.github/workflows/scorecards.yml`).

### Supply-Chain Attestations

Release artifacts (binaries and GHCR container image) are published with
[SLSA build provenance](https://slsa.dev/) via `actions/attest-build-provenance`.
Verify a downloaded binary with:

```bash
gh attestation verify <artifact> --repo SchwartzKamel/azure-openai-cli
```

SBOMs (CycloneDX JSON) are attached to each release alongside the binaries.

---

*Last updated: 2026-04-22*

*Index of all security audits and reviews: [`docs/security/index.md`](docs/security/index.md).*
