# Security Policy

> **Azure OpenAI CLI** — Security guidance for developers, operators, and contributors.

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
| **Credential Separation** | API keys are injected at runtime via environment variables — never baked into the image. |

### Threat Model Summary

The primary threats this project mitigates:

- **Credential leakage** — credentials are excluded from the image and from version control.
- **Container escape / privilege escalation** — non-root execution with a minimal OS layer.
- **Supply-chain compromise** — dependency pinning and vulnerability scanning via Grype.
- **Data exfiltration** — the CLI stores no prompt or response data locally.

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

#### Option A — `--env-file` (Recommended)

Pass a `.env` file at runtime without embedding it in the image:

```bash
docker run --rm \
  --env-file ./azureopenai-cli/.env \
  azureopenai-cli:gpt-5-chat "Hello"
```

#### Option B — Individual Environment Variables

```bash
docker run --rm \
  -e AZUREOPENAIENDPOINT="https://my-resource.openai.azure.com/" \
  -e AZUREOPENAIMODEL="gpt-4o" \
  -e AZUREOPENAIAPI="$(cat ~/.secrets/azure-oai-key)" \
  -e SYSTEMPROMPT="You are a helpful assistant" \
  azureopenai-cli:gpt-5-chat "Hello"
```

#### Option C — Docker Secrets (Swarm / Compose)

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

### Azure RBAC Recommendations

- Use **service principals** with the `Cognitive Services OpenAI User` role
  scoped to the specific OpenAI resource — not the entire subscription.
- Avoid using personal Azure credentials in automated or shared environments.
- Where possible, prefer **Azure Managed Identity** over API keys to eliminate
  static credentials entirely.
- Apply the principle of least privilege: grant only the permissions the CLI
  actually needs.

---

## 3. Container Security

### Alpine-Based Minimal Image

The runtime stage uses `mcr.microsoft.com/dotnet/runtime-deps:9.0-preview-alpine`,
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

The CLI is published as a **single-file, self-contained, trimmed** binary:

```
dotnet publish ... --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=true
```

This means:
- No .NET runtime is required in the container (only native OS dependencies).
- The trimmed binary excludes unused framework code, reducing surface area.
- Only `icu-libs` is installed as an additional runtime dependency.

### Image Digest Pinning (Production)

For production deployments, pin the base image by digest to prevent supply-chain
attacks via tag mutation:

```dockerfile
# Instead of:
FROM mcr.microsoft.com/dotnet/runtime-deps:9.0-preview-alpine

# Use:
FROM mcr.microsoft.com/dotnet/runtime-deps:9.0-preview-alpine@sha256:<digest>
```

Retrieve the current digest:

```bash
docker inspect --format='{{index .RepoDigests 0}}' \
  mcr.microsoft.com/dotnet/runtime-deps:9.0-preview-alpine
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
to `stderr` — no API call is attempted.

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

```
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

---

## 6. Network Security

### TLS/HTTPS Enforcement

All communication with Azure OpenAI endpoints uses HTTPS (TLS 1.2+). The Azure
SDK enforces this — plain HTTP endpoints are rejected.

```
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

```
*.openai.azure.com:443
```

---

## 7. Dependency Security

### Current Dependencies

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

Run the Grype scanner against the built image:

```bash
make scan
```

This executes:

```bash
grype azureopenai-cli:gpt-5-chat
```

Grype checks all OS packages and application dependencies against known CVE
databases.

**Install Grype** (if not already installed):

```bash
curl -sSfL https://raw.githubusercontent.com/anchore/grype/main/install.sh | sh -s -- -b /usr/local/bin
```

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

---

## 9. Exit Codes Reference

The CLI uses structured exit codes to distinguish between error categories:

| Exit Code | Meaning | Example Scenario |
|---|---|---|
| `0` | **Success** | Prompt processed and response streamed successfully. |
| `1` | **Validation / usage error** | Missing arguments, invalid model name, missing environment variables. |
| `99` | **Unhandled error** | Unexpected exception (network failure, SDK error, etc.). |

### Security Implications of Exit Codes

- **Exit 1** indicates a configuration problem. Scripts should halt and alert
  the operator — do not retry with the same configuration.
- **Exit 99** may indicate a transient error (network timeout) or a
  misconfiguration. Check `stderr` for the `[UNHANDLED ERROR]` message before
  retrying.
- In CI/CD pipelines, treat any non-zero exit as a failure and avoid logging
  the full error output to public dashboards (it may contain endpoint URLs).

---

## 10. Security Checklist for Users

Use this checklist when deploying or operating the Azure OpenAI CLI:

### Before First Use

- [ ] Created `.env` from `.env.example` — not from copy-paste in a chat or email.
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

- [ ] Passed credentials via `--env-file` or environment variables — not CLI arguments.
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

## Further Reading

- [Azure OpenAI Security Best Practices](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/managed-identity)
- [Docker Security Documentation](https://docs.docker.com/engine/security/)
- [CIS Docker Benchmark](https://www.cisecurity.org/benchmark/docker)
- [Grype Vulnerability Scanner](https://github.com/anchore/grype)
- [Azure RBAC for Cognitive Services](https://learn.microsoft.com/en-us/azure/ai-services/authentication)

---

*Last updated: 2025*
