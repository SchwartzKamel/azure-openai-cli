---
title: Security Audit -- v2.1.4 (S03E26 / The Offline Mode)
auditor: Newman (Security & Compliance Inspector)
date: 2026-05-19
release_under_review: post-S03E26 (--offline gate forbids non-loopback egress)
scope:
  - azureopenai-cli/Net/EndpointAllowlist.cs (new BlockOffline verdict, OfflineMode latch, explicit-offline overloads)
  - azureopenai-cli/Program.cs (--offline flag, parser, early latch, Azure / Foundry / Prewarm gates)
  - azureopenai-cli/Observability/Telemetry.cs (OTLP exporter gate)
  - azureopenai-cli/Cli/ProviderDoctor.cs (per-provider blocked-offline reporting)
  - tests/AzureOpenAI_CLI.Tests/EndpointAllowlistTests.cs (16 facts + 6 theory rows)
  - tests/AzureOpenAI_CLI.Tests/OfflineModeTests.cs (9 facts, ConsoleCapture-serialised)
  - tests/integration_tests.sh (7 hermetic --offline assertions)
excluded_from_scope:
  - SDK-internal HTTP transport (Azure / Foundry SDKs do their own DNS;
    we gate at the construction boundary, not the transport)
  - File I/O paths (orthogonal; file:// is not a "non-loopback" call)
  - Future S03E27 demo-recording harness (separate scope)
related_audits:
  - docs/audits/security-v2.1.3-allowlist.md
  - docs/audits/security-v2.1.2-keychain.md
related_exec_reports:
  - docs/exec-reports/s03e16-the-allowlist.md
  - docs/exec-reports/s03e26-the-offline-mode.md
predecessor_audit_id: security-v2.1.3-allowlist
audit_id: security-v2.1.4-offline
severity_scale:
  CRITICAL: Code execution / metadata read possible via routine config.
  HIGH: SSRF reachable under realistic input.
  MEDIUM: Defense-in-depth gap; structural drift risk.
  LOW: Operator footgun; documented but not gated.
  INFO: Note for the next auditor.
---

# Security Audit -- 2026-05-19 -- Newman (S03E26)

> *Hello. Newman. I came to inspect a flag. I left with a paper trail.*
> *The seam holds. The latch holds. The doctor holds. The audit holds.*
> *And FDR's TOCTOU finding from v2.1.3? Still open. We do not forget.*

---

## Executive Summary

**Verdict: GREEN -- the offline gate is layered, single-pointed, and
auditable.** `--offline` (and its strict-equality env twin `AZ_AI_OFFLINE=1`)
forbid every non-loopback provider call across all six known network
seams: Azure SDK construction, Foundry SDK construction, OpenAI-compat
adapter, WebFetchTool, OTLP exporter, and the prewarm probe. Loopback
hosts continue to require `AZ_AI_LOCAL_PROVIDERS=1` -- offline does NOT
relax the existing opt-in. ProviderDoctor reflects the gate row by row
(`dns: blocked-offline`, `healthy: false`) so operators can audit a
process from the outside without invoking it.

No CRITICAL / HIGH / MEDIUM findings. Three LOW / INFO forward-hardening
notes filed as `newman-2026-05-O-1` through `newman-2026-05-O-3`.

S03E27 *The Demo* will exercise this gate end-to-end in the conference
recording harness; the integration test corpus (7 hermetic assertions
under `env -i`) is the seed.

---

## Threat Model

### Adversary capabilities considered

1. **Air-gapped reviewer.** Runs `az-ai` inside a network namespace
   with no external interface. Wants a provable guarantee that no
   syscall will reach for a non-loopback socket.
2. **Demo recorder.** Wants the talk to look the same offline as
   online -- no surprise telemetry blobs, no surprise DNS, no surprise
   Azure call when a stray env var leaks in from `~/.env`.
3. **Pipeline operator.** Runs `az-ai` in CI with `--offline` to
   prevent a misconfigured job from spending tokens or leaking
   prompts.
4. **Hostile env injector.** Slips `AZUREOPENAIENDPOINT=https://attacker.tld`
   into the process env. Wants the offline gate to fail open ("oh,
   this looks like a normal Azure URL").
5. **Loopback masquerader.** Provides a public URL that DNS-resolves
   to 127.0.0.1 from one resolver and to a public IP from another
   (TOCTOU). Wants offline to greenlight on the loopback answer.

### Asset list (what we are protecting)

- **Outbound packets.** Every byte that would otherwise leave the host
  to a non-loopback destination.
- **Telemetry payloads.** OTLP spans containing prompt metadata.
- **Prewarm side-channel.** A network probe that fires before the
  user sees output -- silent egress is doubly bad.
- **Doctor's transparency.** The output of `--doctor` must reflect
  the gate state truthfully; a row that says "ok" while offline is on
  is a lie that erodes trust.

---

## Per-Vector Coverage

Every row maps a vector class -> CWE -> the test fact that pins the
defense. If a vector regresses, the named test goes red; the
remediation is the diff that turns the test green again.

| # | Vector class | CWE | Test fact | Verdict |
|---|--------------|-----|-----------|---------|
| 1  | HTTPS public host, --offline | CWE-918 / CWE-693 | `Offline_HttpsPublic_Blocked` | Block |
| 2  | HTTPS public bare-IP, --offline | CWE-918 | `Offline_HttpsPublic_BareIp_Blocked` | Block |
| 3  | HTTP localhost, --offline, no opt-in | CWE-918 | `Offline_HttpLocalhost_NoOptIn_StillBlockLoopback` | Block (layered) |
| 4  | HTTP localhost, --offline + opt-in | -- | `Offline_HttpLocalhost_WithOptIn_Allowed` | Allow |
| 5  | 127.0.0.1, --offline + opt-in | -- | `Offline_Loopback127_WithOptIn_Allowed` | Allow |
| 6  | IPv6 ::1, --offline + opt-in | -- | `Offline_IPv6Loopback_WithOptIn_Allowed` | Allow |
| 7  | Azure-shape endpoint, --offline | CWE-918 | `Offline_AzureShape_Endpoint_Blocked` | Block |
| 8  | RFC1918 + --offline | CWE-918 | `Offline_Rfc1918_StillBlockedAsPrivate` | Block (priority verdict) |
| 9  | 169.254.169.254 + --offline | CWE-918 (IMDS) | `Offline_CloudMetadata_StillBlockedAsLinkLocal` | Block (priority verdict) |
| 10 | DNS rebinding mixed records, --offline | CWE-350 | `Offline_DnsRebinding_MixedRecords_Blocked` | Block |
| 11 | DNS resolves all-public, --offline | -- | `Offline_DnsResolves_Public_Blocked` | Block |
| 12 | DNS resolves all-loopback, --offline + opt-in | -- | `Offline_DnsResolves_AllLoopback_Allowed` | Allow |
| 13 | --offline OFF (default) regression | -- | `Offline_OffByDefault_DoesNotChangeBehavior` | No change |
| 14 | Static latch reads via 2-arg overload | CWE-1188 | `Offline_StaticLatch_PicksUpFromTwoArgOverload` | Latch read |
| 15 | Describe(BlockOffline) names the rule | CWE-209 (info exposure -- inverse) | `Offline_Describe_HasActionableText` | Friendly + redacted |
| 16 | Strict-equality env (`AZ_AI_OFFLINE=1` only) | CWE-20 | `OfflineEnv_StrictEqualityOnly` | Theory (6 cases) |
| 17 | Adapter Build refuses public preset, offline | CWE-918 | `Adapter_Build_OfflineBlocksPublicHttpsEndpoint` | Throws + key redacted |
| 18 | Adapter Build allows loopback preset, offline + opt-in | -- | `Adapter_Build_OfflinePlusOptIn_LoopbackPresetSucceeds` | Construct |
| 19 | Adapter Build still blocks loopback w/o opt-in | -- | `Adapter_Build_OfflineWithoutOptIn_LoopbackStillBlocked` | Block (layered) |
| 20 | Doctor reports blocked-offline for non-loopback | -- | `Doctor_Offline_AzureProvider_ReportsBlockedOffline` | rc != 0, redacted |
| 21 | Doctor leaves loopback rows alone | -- | `Doctor_Offline_LocalhostProvider_NotMarkedOffline` | No false offline-row |
| 22 | WebFetchTool refuses HTTPS under offline | CWE-918 | `WebFetchTool_OfflineRefusesAnyHttpsUrl` | Friendly error |
| 23 | Parser lifts --offline | -- | `ParseArgs_OfflineFlag_SetsOfflineTrue` | Offline=true |
| 24 | Parser default Offline=false | -- | `ParseArgs_OfflineFlag_DefaultsFalse` | Offline=false |
| 25 | Env fallback (strict) lifts to options | CWE-20 | `ParseArgs_OfflineEnv_StrictEqualityOnly_LiftsToOptions` | Strict only |
| 26 | Hermetic --offline --doctor (no providers) | -- | integration: `--offline --doctor (no providers) exits 0` | rc=0 |
| 27 | Hermetic --offline --doctor (Azure env) | -- | integration: `reports blocked-offline (rc=1)` | rc=1, blocked-offline visible |
| 28 | Hermetic AZ_AI_OFFLINE=1 env-only | -- | integration: `env (no flag) gates same as --offline` | Same gate |
| 29 | Hermetic --offline --doctor --json schema | -- | integration: `emits blocked-offline + all_healthy=false` | JSON correct |
| 30 | Hermetic AZ_AI_OFFLINE=true (lax) does NOT enable | CWE-20 | integration: `does not enable (strict-equality)` | Fail-closed against typos |
| 31 | Hermetic --offline --doctor secret-shape leak guard | CWE-532 | integration: `emits no Bearer/sk- secret shape` | Redacted |
| 32 | --offline --help exits 0 | -- | integration: `--offline --help exits 0` | rc=0 |

Total: 25 named facts (1 Theory-backed with 6 inline rows) + 7 hermetic
integration assertions. Net new test count is 30 unit cases + 7
integration cases over the v2.1.3 baseline.

---

## What the seam does NOT cover (yet)

These are intentional gaps, filed below as `newman-2026-05-O-*`. They
are NOT regressions; they are forward-hardening lanes.

- **Network-namespace / nftables proof.** `--offline` is a logical
  gate inside our process. A compromised dependency that opens its
  own socket bypasses the gate. The CLI cannot enforce kernel-level
  isolation by itself. Operators who need a hard guarantee should
  pair `--offline` with `unshare -n` (Linux) or a deny-egress firewall
  rule. Filed as `newman-2026-05-O-1` (LOW, doc).

- **In-process resolver poisoning.** A malicious DNS shim that
  shadows `Dns.GetHostAddressesAsync` could feed the allowlist
  loopback IPs while the actual TCP connect goes elsewhere. This
  TOCTOU lane was already filed as `fdr-2026-05-A-1` and is
  cross-referenced here -- offline does not change the picture but
  also does not close that gap. Filed as `newman-2026-05-O-2` (INFO,
  cross-ref).

- **Telemetry env-var sticky on degrade.** When `--offline` flips
  the OTLP gate closed, we silently degrade (no exception). A future
  operator might enable `AZ_AI_TELEMETRY=1` expecting traces and not
  notice they are running offline. The stderr telemetry emitter
  (which is local-only and safe) keeps working, so the operator does
  see SOMETHING -- but the OTLP-vs-stderr split is not surfaced.
  Filed as `newman-2026-05-O-3` (LOW, UX).

---

## Findings

### `newman-2026-05-O-1` -- offline is a logical gate, not a kernel boundary (LOW, doc)

**Description.** `--offline` enforces in-process. It does not stop
a transitively loaded native dependency that calls `connect()`
directly -- those would not pass through `EndpointAllowlist.Check`.
For air-gapped guarantees, the operator must pair the flag with OS
isolation (`unshare -n`, an egress firewall, or a network namespace).

**Threat impact.** A reviewer who trusts `--offline` to be the only
guarantee may miss a compromised dependency that opens its own
socket. Severity is LOW because (a) we ship a native AOT binary with
a pinned dependency tree, (b) Trivy scans every release, and (c) the
finding is a doc gap, not a code gap.

**Mitigation lane.** `README.md "Air-gapped operation"` subsection
explicitly recommends `unshare -n az-ai --offline ...` for paranoid
runs. `docs/hardening.md` should grow an "Offline depth model"
paragraph in the S03E27 doc sweep.

**Status:** open, owner Newman + Elaine, deferred to S03E27 doc sweep.

---

### `newman-2026-05-O-2` -- offline does not close the v2.1.3 TOCTOU lane (INFO, cross-ref)

**Description.** FDR's `fdr-2026-05-A-1` (MEDIUM) noted that the
allowlist resolves the hostname once and the TCP connect resolves
again, racing on a hostile resolver. `--offline` consumes the same
verdict path -- it neither widens nor narrows the TOCTOU window. The
attacker would need to flip a public IP to a loopback IP between the
two resolves AND the operator would need `AZ_AI_LOCAL_PROVIDERS=1`
already on for the second resolve to pass.

**Threat impact.** No new exposure. The mitigation lane is unchanged
(pin resolved IPs into a custom `HttpMessageHandler`).

**Mitigation lane.** Same as `fdr-2026-05-A-1`. When that lane
ships, this finding closes automatically.

**Status:** open, owner FDR + Newman, deferred to A-1 mitigation
delivery.

---

### `newman-2026-05-O-3` -- silent OTLP degrade under --offline (LOW, UX)

**Description.** When `--offline` is on, `Telemetry.Initialize` skips
the OTLP exporter pipeline construction. There is no stderr message,
no log line, no doctor row -- the exporter just never starts. An
operator who enabled `AZ_AI_TELEMETRY=1` and `--offline` together
might assume traces are flowing and only notice on the receiving end.

**Threat impact.** Confusion, not compromise. No data leaks; the
gate is fail-closed in the right direction. Severity is LOW because
the stderr `TelemetryEmitter` (local file/stderr only, no network)
keeps working, so the operator does see telemetry -- just not OTLP.

**Mitigation lane.** Add a one-line stderr note when both flags are
set ("OTLP exporter disabled by --offline; stderr telemetry still
active") behind `--verbose` only, to avoid noise on normal runs.

**Status:** open, owner Frank Costanza (observability) + Newman,
deferred to S03E27 telemetry sweep.

---

## Invariants Audited

I checked five invariants. All five hold.

1. **Single seam, layered.** Every outbound provider URL still
   passes through `EndpointAllowlist.Check`. The new `BlockOffline`
   verdict fires ONLY when the prior block verdicts (Private,
   LinkLocal, Loopback-without-opt-in, Userinfo, Multicast,
   Broadcast, AllZeros, NonHttpScheme, Userinfo, PrivilegedPort)
   would have passed. Older verdicts always win -- they carry the
   actionable env-var hint. Verified by the priority-verdict tests
   (`Offline_Rfc1918_StillBlockedAsPrivate`,
   `Offline_CloudMetadata_StillBlockedAsLinkLocal`).

2. **Offline does NOT relax the loopback opt-in.** `--offline`
   alone leaves loopback gated by `AZ_AI_LOCAL_PROVIDERS=1`. The
   layered model is verified by
   `Offline_HttpLocalhost_NoOptIn_StillBlockLoopback` and
   `Adapter_Build_OfflineWithoutOptIn_LoopbackStillBlocked`.

3. **Strict-equality env.** `AZ_AI_OFFLINE=1` activates the gate;
   "true" / "yes" / "1 " / "01" / unset / null all leave it OFF.
   Mirrors `AZ_AI_TELEMETRY` and `AZ_AI_LOCAL_PROVIDERS`. Verified
   by `OfflineEnv_StrictEqualityOnly` (Theory) and the integration
   row "AZ_AI_OFFLINE=true does not enable".

4. **No credential ever appears in an offline error path.** The
   `Describe(BlockOffline)` text mentions the rule, the env-var to
   flip, and nothing else -- not the URL, not the host, not the
   key. Verified by `Offline_Describe_HasActionableText` and the
   adapter-build tests (which assert
   `DoesNotContain("sk-stub-not-real-redacted")` on the exception
   message). Doctor output is run through `SecretRedactor`; the
   integration row "emits no Bearer/sk- secret shape" pins it.

5. **Doctor reflects the gate truthfully.** Every non-loopback
   provider row reports `dns: blocked-offline`, `healthy: false`
   when the latch is on. Loopback rows are not falsely marked
   offline. Verified by `Doctor_Offline_AzureProvider_*` and
   `Doctor_Offline_LocalhostProvider_*`.

---

## Differential Notes vs. v2.1.3 (FDR / Allowlist)

FDR's allowlist seam is the foundation. v2.1.4 extends it with a
single new verdict (`BlockOffline`) and a process-wide latch
(`OfflineMode`). The 2-arg `Check(uri, optIn)` overload reads the
latch, which means existing call sites in `WebFetchTool` and
`OpenAiCompatAdapter` pick up offline mode WITHOUT a signature
change -- that is the design point. Test code uses the explicit
3-arg overload to avoid touching process state.

Cross-references with v2.1.3 findings:

- `fdr-2026-05-A-1` (TOCTOU): unchanged; cross-referenced in
  `newman-2026-05-O-2` above.
- `fdr-2026-05-A-2` (IPv4 short-form parser drift): unchanged;
  v2.1.4 does not introduce a new parse path.
- `fdr-2026-05-A-3` (env-var name "local providers" overstates
  scope): adjacent. The new env var `AZ_AI_OFFLINE` follows the
  same convention. Lloyd Braun's S03E27 onboarding sweep should
  surface BOTH names together for the README revamp.

No regression of v2.1.3 invariants. The CheckUriShape signature
changed from `(verdict, host)` to `(verdict, host, isLoopback)`,
but the change is internal -- no public API break.

---

## Sign-off

- **Verdict:** GREEN.
- **Findings filed:** `newman-2026-05-O-1` (LOW, doc),
  `newman-2026-05-O-2` (INFO, cross-ref to A-1),
  `newman-2026-05-O-3` (LOW, UX). All open; backlog rows appended.
- **No CRITICAL / HIGH / MEDIUM.**
- **Pre-release chaos drill** still co-signed by Newman + Frank,
  deferred to the end-of-Arc-3 sweep, after S03E27 *The Demo*.

> *The postman always rings twice. The attacker only needs to ring*
> *once. Tonight, the bell is unplugged.* -- Newman
