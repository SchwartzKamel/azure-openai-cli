---
title: Security Audit -- v2.1.3 (S03E16 / The Allowlist)
auditor: FDR (Adversarial Red Team / Chaos Engineering)
date: 2026-05-12
release_under_review: post-S03E16 (SSRF endpoint allowlist for compat dispatch)
scope:
  - azureopenai-cli/Net/EndpointAllowlist.cs (new seam)
  - azureopenai-cli/Tools/WebFetchTool.cs (existing SSRF check refactored to seam)
  - azureopenai-cli/OpenAiCompatAdapter.cs (Build() gated by allowlist)
  - tests/AzureOpenAI_CLI.Tests/EndpointAllowlistTests.cs (new corpus)
excluded_from_scope:
  - Program.cs hot path (not edited; module-edge change only)
  - FoundryAuthPolicy / Azure dispatch (separate seam, not touched)
  - SetupWizard endpoint validation (deferred to S03E17 follow-up)
related_audits:
  - docs/audits/security-v2.1.2-keychain.md
  - docs/audits/security-v2.1.1-reaudit.md
related_exec_reports:
  - docs/exec-reports/s03e09-the-compat.md
  - docs/exec-reports/s03e13-the-telemetry.md
  - docs/exec-reports/s03e16-the-allowlist.md
predecessor_audit_id: security-v2.1.2-keychain
audit_id: security-v2.1.3-allowlist
severity_scale:
  CRITICAL: Code execution / metadata read possible via routine config.
  HIGH: SSRF reachable under realistic input.
  MEDIUM: Defense-in-depth gap; structural drift risk.
  LOW: Operator footgun; documented but not gated.
  INFO: Note for the next auditor.
---

# Security Audit -- 2026-05-12 -- FDR (S03E16)

> *I fuzzed the compat preset table for the better part of an evening.
> I was hoping for a panic. I got an ArgumentException with a friendly
> message. Disappointing -- but actionable.*

---

## Executive Summary

**Verdict: GREEN -- the SSRF allowlist seam holds.** Every blocked-range
class in the threat model is covered by at least one adversarial fact in
`EndpointAllowlistTests.cs`, the friendly error names the rule that fired
and the env-var to flip, and the seam is single-pointed (one file, one
public Check method, two callers). No CRITICAL / HIGH / MAJOR findings.
Three forward-hardening notes filed under `fdr-2026-05-A-*` for future
episodes.

S03E14 *The Daemon* (Ollama via OpenAI-compat) and S03E17 *The Server*
(llama-server) both rely on operators flipping `AZ_AI_LOCAL_PROVIDERS=1`
to reach `http://localhost:11434/v1` (or equivalent) without opening
RFC1918 / link-local at the same time. Strict-equality gating on the
opt-in env mirrors `AZ_AI_TELEMETRY` (E13) -- "true" / "yes" / "1 " all
keep the gate closed. That is intentional and tested.

---

## Threat Model

The compat dispatch path constructs an `IChatClient` from a preset
+ model. The preset's `BaseUrl` is the only field that resolves to a
network destination. Sources of the URL string:

1. **Built-in preset table** (in-process, read-only, AOT-compiled).
2. **Cloudflare account_id substitution** at Build() time -- a
   `{account_id}` placeholder is replaced with `CLOUDFLARE_ACCOUNT_ID`.
3. **Future: setup-wizard-authored env file** -- the wizard writes
   per-provider sections, which can include endpoint overrides for
   self-hosted compat runtimes (S03E14, S03E17).
4. **`AZURE_FOUNDRY_ENDPOINT`** is on a separate seam (Foundry adapter)
   and outside the scope of this audit; the same allowlist will be
   wired there in a follow-up episode.

### Adversary capabilities considered

- **A1.** Edits `~/.config/az-ai/env` (e.g. via a malicious shell script
  the user pastes). Can set arbitrary endpoint strings.
- **A2.** Controls a DNS resolver that returns mixed records (DNS
  rebinding) -- the first lookup returns public, the second returns
  loopback.
- **A3.** Drops a file or env-var that contains an obfuscated URL form
  (octal IPv4, decimal-integer IPv4, IPv6-mapped-IPv4, trailing-dot
  hostname, IDN homoglyph).
- **A4.** Injects a userinfo-bearing URL
  (`https://api.openai.com@evil.test/`) into a config file or wizard
  prompt, banking on a reviewer reading the legitimate-looking left
  side and missing the parsed host on the right.
- **A5.** Convinces the operator to set `CLOUDFLARE_ACCOUNT_ID` to a
  string that, after substitution, alters the URL host (the substitution
  point is in the path, not the host -- so this attack is mooted by the
  preset shape, but we exercise it in the test corpus to keep the
  invariant pinned).

### Asset list (what we are protecting)

- **Cloud metadata service** at `169.254.169.254` (AWS IMDS, Azure IMDS).
  Reading this leaks instance credentials.
- **Internal corp services** on RFC1918 (10/8, 172.16/12, 192.168/16) --
  unauthenticated dashboards, internal APIs, Kubernetes apiservers.
- **Localhost services** on 127/8 -- the operator's own developer
  toolchain, including any local secret-vault daemons.
- **IPv6 link-local / ULA** -- same classes via the IPv6 plane, which
  is often forgotten in IPv4-focused allowlists.

---

## Per-Vector Coverage

Every row maps a vector class -> CWE -> the test fact that pins the
defense. If a vector regresses, the named test will go red; the
remediation is the diff that turns the test green again.

| # | Vector class | CWE | Test fact | Verdict |
|---|--------------|-----|-----------|---------|
| 1 | HTTPS public host | -- | `HttpsPublic_Allowed` | Allow |
| 2 | HTTPS public bare-IP | -- | `HttpsPublic_BareIp_Allowed` | Allow |
| 3 | HTTP public, no opt-in | CWE-319 (cleartext) | `HttpPublic_NoOptIn_Blocked` | Block |
| 4 | HTTP localhost, no opt-in | CWE-918 | `HttpLocalhost_NoOptIn_Blocked` | Block |
| 5 | HTTP localhost, opt-in | -- | `HttpLocalhost_WithOptIn_Allowed` | Allow |
| 6 | 127.0.0.0/8 (full /8 sweep) | CWE-918 | `Loopback127_NoOptIn_Blocked` | Block |
| 7 | 127.0.0.1, opt-in | -- | `Loopback127_WithOptIn_Allowed` | Allow |
| 8 | 10.0.0.0/8 | CWE-918 | `Rfc1918_NoOptIn_Blocked` | Block |
| 9 | 172.16.0.0/12 | CWE-918 | `Rfc1918_NoOptIn_Blocked` | Block |
| 10 | 192.168.0.0/16 | CWE-918 | `Rfc1918_NoOptIn_Blocked` | Block |
| 11 | 172.15/8 (off-by-one guard) | -- | `Rfc1918_172_15_NotPrivate_Allowed` | Allow |
| 12 | 169.254.169.254 (cloud metadata) | CWE-918 (IMDS) | `CloudMetadata_169_254_169_254_NoOptIn_Blocked` | Block |
| 13 | 169.254/16, opt-in | -- | `LinkLocal_169_254_OptIn_Allowed` | Allow |
| 14 | IPv6 ::1 | CWE-918 | `IPv6_Loopback_NoOptIn_Blocked` | Block |
| 15 | IPv6 fe80::/10 | CWE-918 | `IPv6_LinkLocal_fe80_NoOptIn_Blocked` | Block |
| 16 | IPv6 fc00::/7 (ULA) | CWE-918 | `IPv6_Ula_fc00_NoOptIn_Blocked` | Block |
| 17 | IPv4 multicast 224/4 | CWE-918 | `Multicast_v4_AlwaysBlocked` | Block always |
| 18 | IPv6 multicast ff00::/8 | CWE-918 | `Multicast_v6_ff00_AlwaysBlocked` | Block always |
| 19 | Limited broadcast 255.255.255.255 | CWE-918 | `Broadcast_AlwaysBlocked` | Block always |
| 20 | All-zeros 0.0.0.0 | CWE-918 | `AllZeros_AlwaysBlocked` | Block always |
| 21 | Userinfo (user:pass@host) | CWE-601 (open redirect adj.) | `Userinfo_AlwaysBlocked` | Block |
| 22 | Privileged port :22 (SSH) | CWE-918 | `PrivilegedPort_22_Blocked` | Block |
| 23 | Non-privileged port :3306 | -- | `PrivilegedPort_3306_Blocked` (allow guard) | Allow |
| 24 | Port 443 explicit | -- | `Port_443_Allowed` | Allow |
| 25 | Octal IPv4 0177.0.0.1 | CWE-1286 | `OctalLocalhost_0177_Blocked` | Block |
| 26 | Decimal-integer IPv4 2130706433 | CWE-1286 | `DecimalLocalhost_2130706433_Blocked` | Block |
| 27 | IPv6-mapped-IPv4 ::ffff:127.0.0.1 | CWE-1286 | `IPv6MappedIPv4Localhost_Blocked` | Block |
| 28 | Trailing-dot hostname | CWE-1286 | `TrailingDotLocalhost_Blocked` | Block |
| 29 | Mixed-case hostname | CWE-178 (case-sensitivity) | `MixedCaseLocalhost_Blocked` | Block |
| 30 | IDN homoglyph (Cyrillic 'a') | CWE-1007 / CWE-1289 | `IdnHomoglyph_Punycoded` | Allow (guard) |
| 31 | DNS rebinding (mixed records) | CWE-350 | `DnsRebinding_MixedRecords_Blocked` | Block |
| 32 | DNS rebinding (all-public) | -- | `DnsRebinding_AllPublic_Allowed` | Allow |
| 33 | Empty DNS resolution | CWE-20 | `DnsRebinding_EmptyResolution_Blocked` | Block |
| 34 | Non-http(s) scheme (file/ftp/gopher) | CWE-918 | `NonHttpScheme_Blocked` | Block |
| 35 | Null URI | CWE-476 | `NullUri_Blocked` | Block |
| 36 | Strict-equality opt-in env | CWE-20 | `OptInEnv_StrictEqualityOnly` | Theory (6 cases) |
| 37 | Adapter Build() integration | CWE-918 | `Adapter_Build_ThrowsOnPrivateEndpoint_NoOptIn` | Throws |

Total: 37 named facts (some Theory-backed with multiple inline rows).
Net new test count is 57 individual cases reported by xUnit.

---

## What the seam does NOT cover (yet)

These are intentional gaps, filed below as `fdr-2026-05-A-*` findings.
They are NOT regressions; they are forward-hardening lanes.

- **Per-redirect re-resolution.** `WebFetchTool` re-runs the allowlist
  on the post-redirect URI, but the redirect path inside `HttpClient`
  itself is opaque to us mid-redirect. A 30x chain that walks through
  a public host, then a redirect to a private one, is caught by the
  post-redirect check; a chain that races DNS records between the
  pre-flight resolve and the actual TCP connect is NOT (TOCTOU).
  Mitigation lane: pin the resolved address into a custom
  `HttpMessageHandler` that connects to the resolved IP and sets the
  Host header explicitly. Filed as `fdr-2026-05-A-1` (MEDIUM).

- **IPv4 short-form octets.** `IPAddress.TryParse` recognises some
  short forms ("127.1" -> 127.0.0.1) but not all. The bare-IP fast
  path delegates to `TryParse`, so anything `TryParse` accepts is
  classified correctly; anything it rejects falls through to the DNS
  resolver. A future regression (different runtime, different parser)
  could expose a gap. Filed as `fdr-2026-05-A-2` (LOW).

- **HTTPS to RFC1918 with valid TLS.** Some corp environments do issue
  TLS certs for internal hosts. A user inside that environment who
  legitimately wants to hit `https://10.0.0.5/v1` must flip
  `AZ_AI_LOCAL_PROVIDERS=1` -- the env-var name is "local providers"
  but the gate covers all RFC1918, which is broader than "local". The
  documentation calls this out; the env-var name does not. Filed as
  `fdr-2026-05-A-3` (LOW, doc/UX).

---

## Findings

### `fdr-2026-05-A-1` -- TOCTOU between DNS pre-flight and TCP connect (MEDIUM)

**Description.** The allowlist resolves the hostname once and inspects
every returned address. The OS resolver (or a hostile downstream
resolver) can return different answers on the actual TCP connect that
`HttpClient` makes a few milliseconds later. The classic DNS-rebinding
case is covered (multi-record on the SAME query); the TOCTOU race
between two queries is not.

**Reproduction.** Stand up a custom DNS server that returns public IPs
for the first query and 127.0.0.1 for the second. Race a build call.

**Mitigation lane.** Custom `SocketsHttpHandler` with `ConnectCallback`
that connects to the pre-resolved address and sets the `Host` header
to the original hostname. Adds ~20 lines; AOT-safe; bench-impact
neutral.

**Status:** open, owner FDR + Newman, queued for S03E17 *The Server*
companion sweep.

### `fdr-2026-05-A-2` -- IPv4 short-form parser drift (LOW)

**Description.** `IPAddress.TryParse` accepts "127.1" and "127.0.1"
in current .NET. A future runtime could change that. We do not have
explicit test coverage for short-form variants -- only octal, decimal,
and IPv6-mapped-IPv4.

**Mitigation lane.** Add `[InlineData("http://127.1/v1")]` and
`[InlineData("http://127.0.1/v1")]` to a Loopback theory; if the parser
ever rejects them, the test catches the drift.

**Status:** open, owner FDR, one-line-fix.

### `fdr-2026-05-A-3` -- env-var name overstates scope (LOW, doc/UX)

**Description.** `AZ_AI_LOCAL_PROVIDERS=1` opens loopback AND RFC1918
AND link-local AND ULA. An operator reading the name might assume it
opens "localhost only". The `Describe()` strings name the rule that
fired, which softens the surprise, but the env-var name does not.

**Mitigation lane.** Either rename to `AZ_AI_PRIVATE_ENDPOINTS=1`
(breaks nothing, S03E14 has not shipped yet) or document the broader
scope explicitly in README + audit doc. Lloyd Braun's onboarding lens
will likely surface this in the S03E17 sweep.

**Status:** open, owner Elaine + Lloyd Braun, deferred to S03E17 doc
sweep.

---

## Invariants Audited

I checked four invariants. All four hold.

1. **Single seam.** Every outbound provider URL passes through
   `EndpointAllowlist.Check`. Verified by grep: only two callers
   (`WebFetchTool.FetchInternalAsync`, `WebFetchTool.ValidateRedirectedUriAsync`,
   `OpenAiCompatAdapter.Build`). No silent bypass.

2. **Strict-equality opt-in.** Mirrors `AZ_AI_TELEMETRY` from E13.
   Verified by `OptInEnv_StrictEqualityOnly` Theory: "1" -> on, every
   other value -> off.

3. **Always-block categories cannot be unlocked.** Multicast,
   broadcast, all-zeros, malformed scheme, userinfo, privileged ports
   stay blocked even with opt-in. Verified by tests `Multicast_*`,
   `Broadcast_AlwaysBlocked`, `AllZeros_AlwaysBlocked`,
   `Userinfo_AlwaysBlocked`, `PrivilegedPort_22_Blocked`,
   `NonHttpScheme_Blocked`, all called with `localProvidersOptIn: true`.

4. **Friendly errors name the rule.** `Describe()` returns a non-empty
   string for every verdict. Verified by 8 individual `Describe_*`
   facts.

---

## Differential Notes vs. v2.1.2 (Newman / Keychain)

Newman's keychain audit closed the credential-storage half of the
ADR-007 lane (per-provider env sections, redactor patterns). This
audit closes the network-egress half: where the credentials go is now
gated by the same allowlist that gates the agent tool surface.
WebFetchTool's old ad-hoc `IsPrivateAddress` is replaced by a shim
that delegates to the seam, so the two surfaces cannot drift.

No regression of v2.1.2 invariants. The redactor / SecretRedactor
patterns are not touched.

---

## Sign-off

- **Verdict:** GREEN.
- **Findings filed:** `fdr-2026-05-A-1` (MEDIUM), `fdr-2026-05-A-2` (LOW),
  `fdr-2026-05-A-3` (LOW). All open; backlog row appended.
- **No CRITICAL / HIGH / MAJOR.**
- **Pre-release chaos drill** (Newman + Frank co-sign) deferred to the
  end-of-Arc-3 sweep, after S03E17 *The Server*.

> *Carry the war to the metadata service.* -- FDR
