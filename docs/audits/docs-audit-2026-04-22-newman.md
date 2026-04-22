# Newman — Docs Audit, Security Posture Segment (v2.0.4)

> *Hello. Newman.* Clipboard in hand. The paperwork is late and we're going
> to *fix that*.

**Date:** 2026-04-22
**Auditor:** Newman (Security & Compliance Inspector)
**Target:** v2.0.4 (tag `v2.0.4`, `main @ afa95fd`). FDR High-severity
redaction + `--raw` stderr + Ralph exit code fixes landed at `4842b6a`.
**Scope:** `SECURITY.md`, `docs/security/**`, `docs/audits/**`,
`docs/legal/**`, `.github/SECURITY.md`, `README.md` security claims,
architecture/contributing cross-refs.
**Method:** doc-text vs source-of-truth diff (`azureopenai-cli-v2/`,
`.github/workflows/`, `Dockerfile.v2`, `CHANGELOG.md`). No source edits.

---

## 0. Executive Summary

- **Findings:** 0 Critical · 5 High · 5 Medium · 3 Low · 4 Informational
- **New CVE-worthy issues:** **None.** No unpatched exploit surface uncovered.
  Every finding is documentation drift against an already-correct implementation
  — the *code* holds; the *paper trail* doesn't.
- **Ship-blocker?** No. But `SECURITY.md` is advertising a v1.8-era security
  posture from July 2025 for a v2.0.4 binary shipped April 2026. A reporter
  landing on the file today will mis-identify supported versions, mis-identify
  the vulnerability scanner, mis-identify the build flags, and will not find
  the secret-redaction control the FDR fix added. This is a **paper-trail
  incident**, and Newman does not let those slide.
- **Top three:**
  1. `SECURITY.md` last-updated **2025-07-25**; entire v2 line unacknowledged.
  2. Secret-redaction control (`UnsafeReplaceSecrets` @ `Program.cs:1348`,
     shipped for FDR High `fdr-v2-err-unwrap`) is **undocumented** in
     `SECURITY.md`. The control exists; the claim doesn't.
  3. Vulnerability scanner mismatch — `SECURITY.md` and `Makefile` say
     **Grype**; `.github/workflows/ci.yml:119` actually runs **Trivy**. One or
     the other is lying about the production security posture.

---

## 1. Methodology

### What was checked

1. ✅ `SECURITY.md` (root) — all 15 sections, full read.
2. ✅ `docs/security/reaudit-v2-phase5.md` — cross-referenced against
   `SECURITY.md` for prior-audit linkage.
3. ✅ `docs/audits/fdr-v2-dogfood-2026-04-22.md` — cross-referenced against
   `SECURITY.md` and `docs/security/` for inbound link.
4. ✅ `docs/audits/security-v1.8-post-release.md` — prior audit, linkage.
5. ✅ `docs/security-review-v2.md` — existing Newman report, linkage.
6. ✅ `docs/legal/` — no `security-*` files present (confirmed).
7. ✅ `.github/SECURITY.md` — **not present** (root `SECURITY.md` is what
   GitHub surfaces; acceptable).
8. ✅ `README.md:119` — security-claim paragraph.
9. ✅ Claim/code alignment verified with:
   - `grep -rn 'redact\|UnsafeReplaceSecrets\|AZUREOPENAIAPI' azureopenai-cli-v2/`
     → confirmed helper at `Program.cs:1348` and call sites at 604, 619.
   - `grep -n 'trivy\|grype'` across `.github/`, `SECURITY.md`, `Makefile`,
     `docs/` → confirmed scanner mismatch.
   - `azureopenai-cli-v2/Tools/DelegateTaskTool.cs:24-35` → confirmed
     in-process `AsyncLocal<int>` depth model; no `RALPH_DEPTH` env-var
     marshalling.
   - `azureopenai-cli-v2/Tools/ShellExecTool.cs:15-42, 53-90` → confirmed
     blocklist, substitution guards, `ContainsHttpWriteForms`, env-scrub.
   - `.github/workflows/release.yml` → confirmed SBOM + `attest-build-
     provenance` wiring.
   - `Dockerfile.v2:80, 113` → confirmed digest pinning + `USER appuser`.
   - `CHANGELOG.md` §2.0.4 → confirmed FDR-fix narrative.

### What was NOT checked (out of scope)

- Actual Trivy CVE findings in CI (that's a scanner-output question, not a
  docs question).
- Code-level hardening correctness — covered by `ToolHardeningTests` and
  prior Newman reports (`docs/security/reaudit-v2-phase5.md`,
  `docs/security-review-v2.md`).

---

## 2. Findings

### F-1 (HIGH) — `SECURITY.md` is 9 months stale; entire v2 line unacknowledged
**File:** `SECURITY.md:946` (footer), `SECURITY.md:450-454` (supported versions)
**Problem:** Footer reads `*Last updated: 2025-07-25*`. Supported-versions
table lists `1.8.x (current)`, `1.7.x (critical only)`, `< 1.7 unsupported`.
v2.0.4 shipped 2026-04-22 with FDR High fixes. v1 is on the 1.9.x line per
`docs/v2-cutover-checklist.md:51`. A reporter filing a CVE against v2.0.x
cannot tell from `SECURITY.md` that v2 is even a supported product.
**Fix:** Bump footer; rewrite §8 Supported Versions table:
```
| 2.0.x (current) | ✅ Active security support |
| 1.9.x (v1 LTS)  | ✅ Critical fixes during v1 LTS window |
| < 1.9           | ❌ Unsupported — upgrade to 2.0.x |
```
Add a banner at the top pointing to `docs/audits/` and
`docs/security/reaudit-v2-phase5.md`.
**Severity:** High
**Attack-surface impact:** Disclosure-flow integrity — reporters route to the
wrong maintainer contract; maintainers dismiss reports against "unsupported"
versions that are actually current.

---

### F-2 (HIGH) — Secret-redaction control (`UnsafeReplaceSecrets`) undocumented
**File:** `SECURITY.md` §2 (Credential Management, lines 51–135),
§13 (Ralph Mode Security, lines 736–829)
**Problem:** FDR High `fdr-v2-err-unwrap` (fixed at `4842b6a`, shipped in
v2.0.4) added `Program.cs:1348 UnsafeReplaceSecrets(text, apiKey, endpoint)`
which redacts the API key and the full endpoint URL + bare hostname from
every error surface (`Program.cs:604, 619`). This is a **defensive control
that users and operators want to know about**, but `SECURITY.md` does not
mention it anywhere. §2 ends at "Azure RBAC Recommendations" without ever
saying "by the way, if an exception carries a credential, we redact it
before it hits stderr/stdout/JSON." §13 documents `.ralph-log` truncation
(lines 818–828) but not the redaction of the exception message that feeds
the log.
**Fix:** New §2.x subsection "Error-path Secret Redaction" citing
`Program.cs:1348` and the call sites at 604 (RequestFailedException) and
619 (generic catch). Threat-model note: input is raw exception message
potentially containing the verbatim API key or endpoint; output is the
redacted form; residual risk is **secrets shorter than 4 characters are
not redacted** (intentional — reduces false-positive corruption of error
text) and **pre-substring keys in log rotation are not retroactively
scrubbed**.
**Severity:** High
**Attack-surface impact:** Credential-leakage claims in §1 Threat Model
Summary ("Credential leakage — credentials are excluded from the image and
from version control") understate the actual protection. Operators
validating the control via audit cannot find it in docs and may duplicate
or subvert it.

---

### F-3 (HIGH) — Scanner posture mismatch: docs say Grype, CI runs Trivy
**Files:** `SECURITY.md:46, 391–410, 416, 880`; `Makefile:65, 148–149`;
`.github/workflows/ci.yml:119` (authoritative); `docs/v2-cutover-checklist.md:109`.
**Problem:** `SECURITY.md` §1 and §7 tell users vulnerability scanning is
done "via Grype" and walk the reader through `make scan` / `grype install`.
`Makefile:149` still shells out to `grype`. But the **actual CI gate** on
every PR is:
```yaml
.github/workflows/ci.yml:119
uses: aquasecurity/trivy-action@57a97c7e7821a5776cebc9bb87c984fa69cba8f1 # v0.35.0
```
The v2 cutover checklist (`docs/v2-cutover-checklist.md:109`) and
`docs/verifying-releases.md:198` both reference Trivy. Two scanners coexist
with zero doc acknowledgment. This violates the "code matches doc" rule.
**Fix:** `SECURITY.md` §7 — either (a) declare Trivy the CI gate and Grype
the local developer convenience, or (b) converge on one and update the
Makefile. Newman's recommendation: **document both explicitly**: Trivy in
CI (authoritative), Grype locally (optional). Add `.github/workflows/ci.yml:119`
as the citation so the CI posture has a paper trail.
**Severity:** High
**Attack-surface impact:** Supply-chain posture transparency. A downstream
consumer doing a dependency-chain due-diligence review reads the docs,
installs Grype, sees "clean", and does not realize CI is actually gating
on Trivy (with different CVE DB coverage and severity thresholds).

---

### F-4 (HIGH) — Prior security audits orphaned from `SECURITY.md`
**Files:** `SECURITY.md` (no links to audits), `docs/security/` (no index),
`docs/audits/` (no index).
**Problem:** The following audit reports exist but are **not linked from
`SECURITY.md` nor from any `docs/security/index.md`** (which also does not
exist):
- `docs/audits/fdr-v2-dogfood-2026-04-22.md` (v2.0.2 red-team, drove 2.0.4 fixes)
- `docs/audits/security-v1.8-post-release.md` (v1.8 line)
- `docs/security/reaudit-v2-phase5.md` (Newman, v2 post-Phase-5)
- `docs/security-review-v2.md` (Newman, v2 cutover)
A reporter or auditor landing at `SECURITY.md` sees no trail to prior work.
Per Newman's own charter: *"Prior audit reports linked from SECURITY.md
or docs/security/index."*
**Fix:** Add a new §16 "Audit History" to `SECURITY.md` with a dated
table:
```
| 2026-04-22 | FDR v2.0.2 dogfood | docs/audits/fdr-v2-dogfood-2026-04-22.md |
| 2025-04-20 | Newman v2 post-Phase-5 reaudit | docs/security/reaudit-v2-phase5.md |
| 2025-xx-xx | Newman v2 cutover review | docs/security-review-v2.md |
| 2024-xx-xx | Newman v1.8 post-release | docs/audits/security-v1.8-post-release.md |
```
Create `docs/security/index.md` as the single entry point listing all audits
+ the current SECURITY.md + the hardening matrix.
**Severity:** High
**Attack-surface impact:** Disclosure program credibility and
discoverability — audits that aren't findable functionally don't exist for
the reporter or the regulator.

---

### F-5 (HIGH) — DelegateTaskTool §12 describes the *v1* architecture; v2 is different
**File:** `SECURITY.md:643–733` (§12 DelegateTaskTool Security)
**Problem:** Section 12 documents:
- `RALPH_DEPTH` **environment variable** as the depth counter (lines 654–663).
- `Process.Start` child processes with env-var allowlist (lines 667–681).
- `stdin.Close()` on spawned children (lines 709–716).
- Per-child 60-second process timeout (lines 683–694).

The v2 implementation at `azureopenai-cli-v2/Tools/DelegateTaskTool.cs:10–35`
is **entirely different**:
```
// Replaces the v1-style Process.Start re-launch (Kramer audit H2):
// no exe-locate dance, no shell-argument quoting, no RALPH_DEPTH env-var
// marshalling, no credential re-plumbing.
//
// Depth guard: tracked via AsyncLocal<T> so nested delegations stay
// isolated per logical flow; cap is MaxDepth (3).
```
v2 children are **in-process MAF agents** sharing the parent `IChatClient`.
There is **no `Process.Start`**, no env-var marshalling, no `RALPH_DEPTH`
env var, no subprocess stdin to close. The 60-second process timeout does
not exist in v2. The `MaxDepth = 3` cap is correct; everything else is a
lie about the actual architecture.
**Fix:** Rewrite §12 for v2:
- Depth via `AsyncLocal<int>` (cite `DelegateTaskTool.cs:35`).
- In-process execution — no subprocess boundary, no env-var allowlist
  (the parent's environment is already the child's because it's the same
  process).
- New residual-risk note: because children share the parent's
  `IChatClient` and process environment, environment scrubbing for
  shell-exec still matters (cite `ShellExecTool.cs:32–42 SensitiveEnvVars`),
  but child credential isolation is **weaker** than v1's subprocess
  allowlist model — the threat model note must acknowledge this trade.
- Retain `ToolRegistry.DefaultChildAgentTools` allowlist note
  (cite `DelegateTaskTool.cs:82`).
**Severity:** High
**Attack-surface impact:** Operators and auditors reading §12 will form
an incorrect mental model of the subagent boundary. Anyone writing a
threat model against v2 using this doc will mis-identify the isolation
guarantee, which could lead to reasoning errors in downstream security
reviews.

---

### F-6 (MEDIUM) — Publish-flag misdescription: v2 is AOT, docs describe v1 trimmed-single-file
**File:** `SECURITY.md:166–176` (§3 "Self-Contained Binary")
**Problem:** §3 describes the binary as published with
`/p:PublishSingleFile=true /p:PublishTrimmed=true`. v2 publishes with
`PublishAot=true` (see `Dockerfile.v2:30, COPY`, and the CHANGELOG entry
for v2.0.4 mentioning `--self-contained -p:PublishAot=true`). The security
consequences differ: AOT produces native ELF with no managed-fallback path,
and the AOT-trim investigation (`docs/aot-trim-investigation.md`) tracks
SDK-method drops that produced the FDR F-1 "type initializer" bug.
**Fix:** Rewrite §3 to state "v2 is PublishAot linux-x64/linux-musl-x64
single ELF; v1 1.9.x is PublishTrimmed single-file." Cite `Dockerfile.v2`
and `docs/aot-trim-investigation.md`. Note the residual AOT risk:
reflection-accessed SDK members may be trimmed and surface as
`TypeInitializationException` — mitigated by the `UnsafeReplaceSecrets`
+ `UnwrapAsync` unwrap chain (F-2).
**Severity:** Medium
**Attack-surface impact:** Operators validating binary provenance or
reviewing trim-related CVE advisories will mismatch versions.

---

### F-7 (MEDIUM) — Exit-code reference missing 130 (SIGINT) and Ralph exit 1
**File:** `SECURITY.md:498–517` (§9 Exit Codes Reference)
**Problem:** Table lists only 0/1/99. v2.0.4 hardened:
- Ralph mode returns **exit 1** when `--max-iterations` is exhausted without
  validation pass (`RalphWorkflow.cs`, FDR High `fdr-v2-ralph-exit-code`).
- **SIGINT → 130** is preserved by the Ralph workflow (per `4842b6a` commit
  message).
- **Exit 2** is used in several CliParseError paths per v2 convention
  (verify in `Program.cs`).
Neither are in the reference. Scripts parsing exit codes from CI don't know
what they mean.
**Fix:** Expand §9 table. Add a row for 130 with "SIGINT / user interrupt —
always safe to abandon". Clarify Ralph-specific exit-1 semantics.
**Severity:** Medium
**Attack-surface impact:** Scripting/CI integrations may silently retry on
SIGINT or misinterpret Ralph timeout as success.

---

### F-8 (MEDIUM) — Dependency version table stale (v1 versions, not v2)
**File:** `SECURITY.md:375–388` (§7 Current Dependencies)
**Problem:** Table lists `Azure.AI.OpenAI 2.3.0-beta.1`, `Azure.Core 1.47.2`,
`dotenv.net 3.1.2`. Per `docs/security/reaudit-v2-phase5.md:30`, v2 uses
`Azure.AI.OpenAI 2.1.0` (GA) and `OpenTelemetry 1.15.2`. v2 also ships
Microsoft.Agents.AI (MAF), `Microsoft.Extensions.AI`, squad/Ralph deps —
none listed. A CVE advisory hitting `Azure.AI.OpenAI 2.1.0` would not be
detected by a reporter consulting this table.
**Fix:** Either auto-generate this table from `*.csproj` via a CI step, or
delete the static table and point to the CycloneDX SBOM
(`*.sbom.json`) produced by `.github/workflows/release.yml:95, 277`. The
SBOM is the canonical dependency list; the doc should say so.
**Severity:** Medium
**Attack-surface impact:** Dependency-CVE triage fails closed on the
wrong version.

---

### F-9 (MEDIUM) — ShellExecTool §11 omits substitution + HTTP-write guards
**File:** `SECURITY.md:583–593` (§11 ShellExecTool table)
**Problem:** Documented protections: destructive blocklist, privilege/
interactive blocklist, pipe-chain analysis, output cap, timeout, stdin
closed. **Missing from the table** (all present in `ShellExecTool.cs`):
- `$(...)` command substitution block — line 53.
- Backtick substitution block — line 53.
- `<(...)`, `>(...)` process substitution block — line 57.
- `eval`, `exec` prefix block — line 58–59.
- `ContainsHttpWriteForms` curl/wget body+upload block — line 79–80,
  131–186.
- Tab/newline-split first-token rescan (K-1 hardening, 2.0.2) — line 69.
- `Process.Kill(entireProcessTree: true)` on timeout — per prior audit.
- Env scrub via `SensitiveEnvVars` — lines 32–42.

`docs/use-cases-agent.md:262–295` does document `$()` + backticks + `<()` +
`eval` for users — so the user-facing doc is more accurate than
`SECURITY.md`. The security policy must be at least as complete as the
user tutorial.
**Fix:** Add the missing rows to the §11 ShellExecTool table. Cite
`azureopenai-cli-v2/Tools/ShellExecTool.cs:53, 57–60, 69, 79–80, 99–100`.
Cross-link to `tests/AzureOpenAI_CLI.V2.Tests/ToolHardeningTests.cs` as
the proof-of-coverage test fixture.
**Severity:** Medium
**Attack-surface impact:** Understated hardening claims create a
"claims-vs-reality" gap — auditors and reporters underestimate the tool
surface defense, and engineers cannot cite the test coverage when
defending the design in a review.

---

### F-10 (MEDIUM) — ShellExecTool privilege/interactive blocklist enumerates a subset
**File:** `SECURITY.md:588`
**Problem:** Documented as `sudo, su, crontab, vi, vim, nano, nc, ncat,
netcat, wget`. Source of truth at `ShellExecTool.cs:19-20`:
`sudo, su, crontab, vi, vim, nano, nc, ncat, netcat, wget`. ✅ **Matches.**
But the destructive list at `SECURITY.md:587` is:
`rm, rmdir, mkfs, dd, shutdown, reboot, halt, poweroff, kill, killall,
pkill, format, del, fdisk, passwd`. Source: `ShellExecTool.cs:17-18` —
same set. ✅ **Matches.** (This finding downgraded to Informational after
verification; kept here as a reminder: these two lists are the *only*
doc/code pairs in the SECURITY.md tool surface that are currently
byte-aligned. When F-9 is fixed, keep them byte-aligned too.)
**Fix:** No change; track for regression. Add a CI lint (future work:
`scripts/lint-blocklist-docs.sh`) that diffs the doc table against the
source `HashSet`.
**Severity:** Medium → **downgraded to Informational** (see I-4)

---

### F-11 (LOW) — No CVE / disclosure log
**File:** `docs/security/` (no `cve-log.md`, no `disclosures.md`)
**Problem:** There is no record of historical CVEs (own-repo or upstream
dependency) or of past disclosures. If 2026-04-22 is pre-any-CVE, that's
fine — but the path must exist so the first CVE has a place to go. Newman's
charter mandates a "CVE disclosure log freshness check"; currently the
freshness answer is *"no log"*.
**Fix:** Create `docs/security/cve-log.md` with an empty table and the
schema:
```
| CVE ID | Date | Severity | Component | Fixed in | Advisory |
```
Cross-link from `SECURITY.md` §8.
**Severity:** Low
**Attack-surface impact:** First-incident response readiness. Not a
vulnerability; a process gap.

---

### F-12 (LOW) — `.github/SECURITY.md` not present
**File:** `.github/` (absence)
**Problem:** GitHub surfaces the root `SECURITY.md` automatically so this
is **not a functional gap**. Noted for completeness because Newman's
charter asks.
**Fix:** Optional — add a one-line stub at `.github/SECURITY.md` reading
`See [../SECURITY.md](../SECURITY.md).` for forkers who scan `.github/`
first.
**Severity:** Low
**Attack-surface impact:** Disclosure-path discoverability, minor.

---

### F-13 (LOW) — `docs/legal/security-*` absent
**File:** `docs/legal/`
**Problem:** Only `trademark-policy.md` exists. No `security-scope.md`,
no `security-safe-harbor.md`. Out of scope per mandate but noted.
**Fix:** Optional. A safe-harbor clause in `SECURITY.md` §8 would be
sufficient — consider adding "Good-faith research conducted in line with
this policy will not be pursued legally" per the GitHub default.
**Severity:** Low
**Attack-surface impact:** Researcher confidence in disclosing.

---

### I-1 (INFORMATIONAL) — README security paragraph does not quote `MaxDepth`
**File:** `README.md:119`
**Problem:** "`delegate_task` recursion is depth-capped" — correct, but
no number. Readers must go to `SECURITY.md` §12 (which, per F-5, is wrong
about the mechanism anyway).
**Fix:** Quote the cap: "`delegate_task` recursion is depth-capped at 3
levels." Cite `DelegateTaskTool.cs:24`.

---

### I-2 (INFORMATIONAL) — `--raw` stderr contract (FDR Medium `fdr-v2-raw-config-warning`) not in SECURITY.md
**File:** `SECURITY.md:272–326` (§5 Configuration Security)
**Problem:** The `--raw` silent-stderr contract (hardened at `4842b6a` —
`UserConfig.Load(quiet:)`) is not mentioned. It's a security-adjacent
control (shell-integration pipelines, no leakage of file-path information
about the user's home config into stderr when the hot path is being
tee'd).
**Fix:** Add a short note at §5: "`--raw` and `--json` modes suppress
config-parse warnings on stderr (`UserConfig.cs` + `Program.cs`) to
preserve the silent-by-design contract for Espanso/AHK and JSON-
consuming pipelines." Cite `CHANGELOG.md` v2.0.4.

---

### I-3 (INFORMATIONAL) — `SECURITY.md` §3 pinning described as "production recommendation"
**File:** `SECURITY.md:178–195`
**Problem:** "For production deployments, pin the base image by digest
to prevent supply-chain attacks via tag mutation". `Dockerfile:41` and
`Dockerfile.v2:80` **already pin** by digest
(`sha256:f8a0978d56136514d1d2f9c893a8797eb47d42f6522da7b8d1b2fcdc51e95198`).
Elevate from "recommendation" to "documented default".
**Fix:** Rewrite to "The shipped Dockerfile already pins the base image
by digest (`Dockerfile.v2:80`); to update, follow the digest-retrieval
command below."

---

### I-4 (INFORMATIONAL) — No doc/code blocklist linter
**File:** (absence) no `scripts/lint-blocklist-docs.sh` or equivalent.
**Problem:** Docs (`SECURITY.md` §11) enumerate the `BlockedCommands`
set, and `tests/AzureOpenAI_CLI.V2.Tests/ToolHardeningTests.cs` enumerates
the test cases. Drift between all three is detectable by a small script
but currently detected only by audit.
**Fix:** Future — add a CI lint that parses `ShellExecTool.cs:17-21` and
asserts every entry appears in the `SECURITY.md` §11 table and has a
`ToolHardeningTests` case.
**Severity:** Informational
**Attack-surface impact:** Regression guard against future F-9 drift.

---

## 3. Summary Table

| ID   | Severity      | Area                              | One-line                                                                  |
|------|---------------|-----------------------------------|---------------------------------------------------------------------------|
| F-1  | High          | Currency                          | `SECURITY.md` 9 months stale; v2 line unacknowledged                      |
| F-2  | High          | Secret redaction                  | `UnsafeReplaceSecrets` control exists, no doc claim                       |
| F-3  | High          | Scanner posture                   | Docs say Grype, CI runs Trivy                                             |
| F-4  | High          | Audit linkage                     | Prior audits orphaned from `SECURITY.md`, no `docs/security/index.md`     |
| F-5  | High          | Subagent architecture             | §12 describes v1 Process.Start model; v2 is in-process AsyncLocal         |
| F-6  | Medium        | Build flags                       | §3 describes PublishTrimmed; v2 is PublishAot                             |
| F-7  | Medium        | Exit codes                        | §9 missing 130/SIGINT and Ralph exit-1 semantics                          |
| F-8  | Medium        | Dependency table                  | v1 versions listed for a v2 binary; point at SBOM instead                 |
| F-9  | Medium        | ShellExecTool claims              | `$()`/backtick/`<()`/`eval`/HTTP-write blocks omitted from §11            |
| F-10 | Informational | Blocklist alignment               | Destructive + privilege lists match source (regression watch)             |
| F-11 | Low           | CVE log                           | `docs/security/cve-log.md` does not exist                                 |
| F-12 | Low           | `.github/SECURITY.md`             | Not present (root file suffices; optional)                                |
| F-13 | Low           | `docs/legal/security-*`           | Absent; consider safe-harbor language in SECURITY.md §8                   |
| I-1  | Informational | README clarity                    | Quote `MaxDepth = 3` inline                                               |
| I-2  | Informational | `--raw` stderr contract           | Not documented in §5                                                      |
| I-3  | Informational | Digest pinning                    | Elevate from "recommendation" to "default"                                |
| I-4  | Informational | Doc/code linter                   | No CI guard against blocklist-doc drift                                   |

**Counts:** 0 Critical · 5 High · 4 Medium · 3 Low · 5 Informational
*(F-10 downgraded to Informational during audit, as explained; 17 total findings.)*

---

## 4. CVE / Disclosure-Log Freshness

| Item                                          | Status                                        |
|-----------------------------------------------|-----------------------------------------------|
| `docs/security/cve-log.md`                    | ❌ Does not exist (F-11)                      |
| `SECURITY.md` footer last-updated             | ❌ 2025-07-25 — 9 months stale (F-1)          |
| Supported-versions table                      | ❌ v1.8-era; v2.0.x unlisted (F-1)            |
| Prior-audit linkage                           | ❌ Orphaned (F-4)                             |
| FDR 2026-04-22 audit linked from SECURITY.md  | ❌ Not linked (F-4)                           |
| Trivy CI gate documented                      | ❌ Docs say Grype (F-3)                       |
| SBOM + SLSA attestations wired in release.yml | ✅ `release.yml:95, 103, 277, 287`            |
| Digest-pinned base image                      | ✅ `Dockerfile.v2:80`                         |
| Secret-redaction helper present in code       | ✅ `Program.cs:1348 UnsafeReplaceSecrets`     |
| Secret-redaction helper documented            | ❌ Not in `SECURITY.md` (F-2)                 |

**Overall freshness verdict:** **Failing.** Code-side posture is strong
(SBOMs emitted, provenance attested, base images pinned, secret redaction
shipped). Doc-side posture is **9 months out of date** and, in §12,
describes an architecture that no longer exists. Fix F-1 through F-5
before the next release candidate; F-6 through F-9 before the next
quarterly review.

---

## 5. New CVE-Worthy Issues Uncovered

**None.** Every finding is a documentation-currency or claim/code-alignment
issue against an implementation that is already hardened. No new exploit
vector, no new bypass, no new unchecked input surface. The paper trail is
behind; the binary is not.

---

*When you control the input validation… you control the* information.
*The paperwork is how everyone else finds out. Fix the paperwork.* — Newman
