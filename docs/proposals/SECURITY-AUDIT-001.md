# Security Audit Report — AUDIT-001

**Date:** 2026-04-08
**Auditor:** Newman (Security Inspector)
**Scope:** All code changes in v1.1.0

## Executive Summary

The Azure OpenAI CLI project demonstrates a security-conscious design overall. Credentials are not baked into images, the Dockerfile uses multi-stage builds with a non-root Alpine runtime, JSON serialization uses `System.Text.Json` (which auto-escapes output), and environment variable parsing degrades gracefully with safe defaults. Error messages are carefully worded to avoid leaking API keys.

However, the audit identified **10 findings** across the codebase. The most notable are: unbounded stdin reads enabling memory exhaustion before prompt-length validation kicks in, acceptance of plaintext HTTP endpoints (risking credential interception), and CI/Docker supply chain risks from unpinned action and image tags. No critical vulnerabilities were found — all findings are Medium or Low severity, and several have partial mitigations already in place.

## Findings

### MEDIUM-001: Unbounded Stdin Read Enables Memory Exhaustion

- **Severity:** Medium
- **File:** `azureopenai-cli/Program.cs:63`
- **Description:** `Console.In.ReadToEnd()` reads the entirety of piped stdin into memory *before* the `MAX_PROMPT_LENGTH` (32 KB) validation on line 93. An attacker (or accidental misuse) piping gigabytes of data can exhaust process memory and crash the application with an `OutOfMemoryException`.
- **Impact:** Denial of service against the local process. No remote attack vector, but relevant when the CLI is invoked in automated pipelines where stdin sources may be untrusted.
- **Remediation:** Read stdin in chunks with a hard cap. For example, read up to `MAX_PROMPT_LENGTH + 1` characters and reject early if exceeded:
  ```csharp
  char[] buffer = new char[MAX_PROMPT_LENGTH + 1];
  int charsRead = Console.In.ReadBlock(buffer, 0, buffer.Length);
  if (charsRead > MAX_PROMPT_LENGTH)
  {
      // reject immediately without allocating the full stream
  }
  stdinContent = new string(buffer, 0, charsRead);
  ```
- **Status:** Open

### MEDIUM-002: HTTP Endpoints Accepted — API Key Sent in Plaintext

- **Severity:** Medium
- **File:** `azureopenai-cli/Program.cs:113`
- **Description:** Endpoint validation accepts both `https://` and `http://` schemes. When an `http://` endpoint is used, the Azure API key is transmitted unencrypted, exposing it to network-level interception (e.g., ARP spoofing, rogue Wi-Fi, compromised proxy).
- **Impact:** Credential theft via man-in-the-middle attack if a user mistakenly configures an HTTP endpoint.
- **Remediation:** Restrict to HTTPS only, or at minimum emit a prominent warning:
  ```csharp
  if (endpoint.Scheme != "https")
  {
      Console.Error.WriteLine("[SECURITY WARNING] Using non-HTTPS endpoint. API key will be sent in plaintext.");
  }
  ```
  Preferred: reject `http://` entirely unless an explicit `--allow-insecure` flag is passed.
- **Status:** Open

### MEDIUM-003: CI GitHub Actions Not Pinned to SHA

- **Severity:** Medium
- **File:** `.github/workflows/ci.yml:13,16`
- **Description:** Third-party actions use mutable version tags (`actions/checkout@v4`, `actions/setup-dotnet@v4`) rather than immutable commit SHA digests. A compromised tag or supply chain attack against the `actions` org could inject malicious code into CI runs.
- **Impact:** Arbitrary code execution in CI with access to repo secrets and `GITHUB_TOKEN`.
- **Remediation:** Pin to full SHA digests:
  ```yaml
  - uses: actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683 # v4.2.2
  - uses: actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9 # v4.3.1
  ```
  Use Dependabot or Renovate to keep SHAs updated.
- **Status:** Open

### MEDIUM-004: Docker Base Images Not Pinned to SHA Digest

- **Severity:** Medium
- **File:** `Dockerfile:4,27`
- **Description:** Both `FROM` instructions use mutable tags (`mcr.microsoft.com/dotnet/sdk:10.0`, `mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine`). The Dockerfile itself contains comments acknowledging this should be done for production but hasn't been implemented.
- **Impact:** A compromised or silently-updated base image could inject malicious code into the built artifact.
- **Remediation:** Pin to digest:
  ```dockerfile
  FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:<digest> AS build
  FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine@sha256:<digest> AS runtime
  ```
- **Status:** Open

### LOW-005: CI Workflow Missing Explicit Permissions Scope

- **Severity:** Low
- **File:** `.github/workflows/ci.yml`
- **Description:** No top-level `permissions` block is defined. On `push` events to `main`, the `GITHUB_TOKEN` receives broad default permissions (`contents: write`, `packages: write`, etc.), violating the principle of least privilege.
- **Impact:** If a step is compromised, the token has more access than necessary.
- **Remediation:** Add a top-level permissions block:
  ```yaml
  permissions:
    contents: read
  ```
- **Status:** Open

### LOW-006: `.env` Files Not Excluded from Docker Build Context

- **Severity:** Low
- **File:** `.dockerignore`
- **Description:** The `.dockerignore` does not exclude `.env` files. If a developer has `azureopenai-cli/.env` with real credentials, `COPY azureopenai-cli/ ./` on Dockerfile line 9 copies it into the build stage. While the multi-stage build only carries the binary to runtime (line 53), the credential-containing layer persists in the Docker build cache and could be exposed if the build image is pushed to a registry.
- **Impact:** Credential leakage via Docker layer history or build cache.
- **Remediation:** Add to `.dockerignore`:
  ```
  **/.env
  **/.env.*
  !**/.env.example
  ```
- **Status:** Open

### LOW-007: Config File TOCTOU Permission Gap on Unix

- **Severity:** Low
- **File:** `azureopenai-cli/UserConfig.cs:58-59`
- **Description:** `File.WriteAllText()` creates the config file with default permissions (typically `644` — world-readable). `SetRestrictivePermissions()` is called immediately after to set `600`. In the brief window between creation and chmod, another user on a shared system could read the file contents.
- **Impact:** Minimal — the config file only stores model preferences (no credentials). But if the config schema ever expands to include sensitive data, this becomes relevant.
- **Remediation:** On Unix, create the file with restrictive permissions atomically, e.g., by writing to a temp file with mode `600` first, then renaming it into place. Alternatively, pre-create the file with correct permissions before writing.
- **Status:** Open

### LOW-008: No File Permission Enforcement on Windows

- **Severity:** Low
- **File:** `azureopenai-cli/UserConfig.cs:76-77`
- **Description:** `SetRestrictivePermissions` is a no-op on Windows. The config file is created with default ACLs, potentially readable by other users in a multi-user Windows environment.
- **Impact:** Same as LOW-007 — low risk since the config contains only model preferences.
- **Remediation:** On Windows, use `System.IO.FileInfo.SetAccessControl()` to restrict the ACL to the current user. Alternatively, document this limitation.
- **Status:** Open

### LOW-009: Generic Exception Handler May Leak Internal Details

- **Severity:** Low
- **File:** `azureopenai-cli/Program.cs:294`
- **Description:** The catch-all exception handler outputs `ex.Message` directly to stderr. Azure SDK exceptions may contain internal URLs, deployment names, or partial request details. While API keys are not typically included in exception messages, the verbosity is higher than necessary.
- **Impact:** Information disclosure of internal infrastructure details (endpoint paths, deployment names) to a user who may not be the operator.
- **Remediation:** In non-debug mode, show a generic error message and suggest using `--verbose` or checking logs for details:
  ```csharp
  Console.Error.WriteLine("[ERROR] An unexpected error occurred. Set AZURE_DEBUG=1 for details.");
  ```
- **Status:** Open

### LOW-010: Makefile ARGS Interpolation Without Quoting

- **Severity:** Low
- **File:** `Makefile:33`
- **Description:** `$(ARGS)` is interpolated directly into a shell command without quoting: `$(FULL_IMAGE) $(ARGS)`. If ARGS contains shell metacharacters (`;`, `|`, `$()`, backticks), they are interpreted by the shell.
- **Impact:** This is developer tooling, and the user controls ARGS, so exploitation requires self-inflicted harm. However, in scripted/automated environments, unsanitized ARGS could cause unexpected command execution.
- **Remediation:** This is inherently difficult to fix in Make (quoting ARGS breaks multi-word prompts). Document the risk and recommend using the Docker command directly for untrusted inputs.
- **Status:** Open (Accepted Risk)

## Security Posture Assessment

**Overall Score: B+ (Good)**

| Category | Rating | Notes |
|---|---|---|
| Credential Handling | ✅ Strong | Keys stay in env vars, never logged or baked into images |
| Input Validation | ⚠️ Adequate | Prompt length checked but stdin unbounded; endpoint scheme too permissive |
| Output Encoding | ✅ Strong | `System.Text.Json` handles escaping correctly |
| Error Handling | ✅ Good | Structured error codes, no credential leaks in known error paths |
| Supply Chain | ⚠️ Needs Work | Neither CI actions nor Docker images are SHA-pinned |
| Least Privilege | ✅ Good | Non-root Docker user, but CI permissions unscoped |
| File Permissions | ⚠️ Adequate | Unix permissions set but with TOCTOU gap; Windows unhandled |
| Test Coverage (Security) | ⚠️ Adequate | Core paths tested; no explicit tests for malformed config, HTTP endpoint, or oversized stdin |

## Recommendations

**Priority 1 — Address before next release:**
1. **Cap stdin reads** (MEDIUM-001) — Straightforward fix, prevents DoS in pipeline contexts
2. **Enforce HTTPS-only** or warn on HTTP endpoints (MEDIUM-002) — Prevents accidental credential exposure

**Priority 2 — Address in upcoming sprint:**
3. **Pin CI actions to SHA** (MEDIUM-003) — Low effort, high supply-chain protection
4. **Pin Docker base images to digest** (MEDIUM-004) — Pair with automated digest update tooling
5. **Add CI permissions block** (LOW-005) — One-line change, significant risk reduction
6. **Exclude `.env` from Docker build context** (LOW-006) — One-line `.dockerignore` addition

**Priority 3 — Track for future improvement:**
7. Add test cases for: malformed JSON config resilience, HTTP endpoint rejection, oversized stdin handling, and boundary values for `AZURE_MAX_TOKENS`/`AZURE_TIMEOUT`
8. Consider atomic file creation for config to eliminate TOCTOU gap (LOW-007)
9. Evaluate Windows ACL support if multi-user deployments are planned (LOW-008)
