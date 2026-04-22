# Security Index

> *Hello. Newman.* Clipboard in hand. This page is the index of every
> security audit, review, and post-release verification we have shipped —
> indexed so no report ends up orphaned again.

**Last updated:** 2026-04-22
**Maintainer:** Newman (Security & Compliance Inspector)
**Scope:** every security-labeled document under `docs/`, plus root-level
`SECURITY.md` cross-refs.

---

## 1. Purpose

This is the index of every security audit, review, and post-release
verification we have shipped. **Start here before opening a security issue.**
If your concern is already covered in a report below, link that report in
your advisory and save everyone a round trip.

The index captures:

- Every auditor-produced report, past and present.
- The commit (or release) that resolved the findings, when one exists.
- The current status of each finding family (open / closed / residual).
- Where to find the disclosure flow.

If a report is not listed here, **it is not considered part of the
historical record** — open a PR to add it (see §6).

---

## 2. Disclosure

All suspected vulnerabilities follow the flow in
[`SECURITY.md § Reporting Vulnerabilities`](../../SECURITY.md#8-reporting-vulnerabilities).

- **Preferred channel:** GitHub Security Advisories (private).
- **Acknowledgment SLO:** within 48 hours.
- **Triage SLO:** within 5 business days.
- **Disclosure:** coordinated with reporter after fix lands.

**Do not** publish proof-of-concept or exploit details in a public issue
before a fix has shipped. The postman always rings twice. The attacker only
needs to ring *once*.

---

## 3. Audit reports (newest first)

Independent audits, red-team exercises, and adversarial dogfood reports.
Ordered by report date, newest first. Severity headline is the highest-
severity finding in that report (Informational means "no issues worth
raising").

| Date       | Scope                                                                 | Severity headline                                                       | Report                                                                          | Resolution commit |
|------------|-----------------------------------------------------------------------|-------------------------------------------------------------------------|---------------------------------------------------------------------------------|-------------------|
| 2026-04-22 | v2.0.4 docs + security posture (SECURITY.md, `docs/security/**`, `docs/audits/**`) | 5 High (indexing, version drift, secret-redaction doc gap, v2 DelegateTaskTool drift, HTTPS-only claim) | [`docs/audits/docs-audit-2026-04-22-newman.md`](../audits/docs-audit-2026-04-22-newman.md) | *this change + follow-ups (see §5)* |
| 2026-04-22 | v2.0.2 AOT binary, live-endpoint red team (errors, `--raw`, Ralph)    | 3 High (error-surface leak, `--raw` stderr noise, Ralph exit code)      | [`docs/audits/fdr-v2-dogfood-2026-04-22.md`](../audits/fdr-v2-dogfood-2026-04-22.md) | [`4842b6a`](https://github.com/SchwartzKamel/azure-openai-cli/commit/4842b6a) |
| 2025-07-25 | v1.8.0 release pipeline, supply-chain posture, workflow permissions   | 🔴 RED — release pipeline failed, no binaries/attestations published    | [`docs/audits/security-v1.8-post-release.md`](../audits/security-v1.8-post-release.md) | [`e188837`](https://github.com/SchwartzKamel/azure-openai-cli/commit/e188837) (audit) → fix landed in v1.8.1 re-release |

### Historical v1 Newman audits

Older v1 hardening commits (e.g. `b704e9d` ReadFileTool blocklist, `ecbcc15`
env-var scrub, `45b78ee` shell upload blocks, `d8e49a4` refusal clause) were
not accompanied by a standalone report — they ship as commit-message audits
and are referenced from [`SECURITY.md § 11 Tool Security`](../../SECURITY.md#11-tool-security).

---

## 4. Security reviews (per-release)

Release-gated reviews with a go/no-go recommendation. Each row is scoped to
a specific release tag.

| Release tag | Date       | Auditor | Verdict                                     | Report                                                                    | Sign-off commit |
|-------------|------------|---------|---------------------------------------------|---------------------------------------------------------------------------|-----------------|
| v2.0.0      | 2025-04-20 | Newman  | CLEAR for cutover (0 🔴, 8 🟡 non-blocking) | [`docs/security-review-v2.md`](../security-review-v2.md)                  | [`3c35ecf`](https://github.com/SchwartzKamel/azure-openai-cli/commit/3c35ecf) |
| v2.0.0 (post-Phase-5) | 2025-04-20 | Newman | Cleared with 1 follow-up (refusal clause port) | [`docs/security/reaudit-v2-phase5.md`](./reaudit-v2-phase5.md)        | [`6830c2e`](https://github.com/SchwartzKamel/azure-openai-cli/commit/6830c2e) (refusal clause), [`7675284`](https://github.com/SchwartzKamel/azure-openai-cli/commit/7675284) (report) |
| v1.8.0      | 2025-07-25 | Newman  | 🔴 RED (pipeline failure, no artifacts)     | [`docs/audits/security-v1.8-post-release.md`](../audits/security-v1.8-post-release.md) | v1.8.1 re-release (pipeline fix) |

**Methodology baseline.** Newman reviews check:

1. All tool hardening (blocklists, caps, timeouts, symlink resolution, SSRF).
2. Secret handling (redaction, env-var allowlists, error surfaces).
3. Supply chain (base-image digests, NuGet pins, SBOM, attestations).
4. Subagent containment (depth cap, tool allowlist, credential inheritance).
5. CI posture (workflow permissions, branch protection, private vuln
   reporting).

Any reaudit must re-run this matrix — partial reviews are not sufficient
for release sign-off.

---

## 5. Known-issue log (CVE-or-not)

Open security issues, tracked here until a fix ships or a decision to
accept residual risk is recorded.

| Identifier | Date opened | Severity | Summary | Status | Link |
|------------|-------------|----------|---------|--------|------|
| — | — | — | No open security issues as of 2026-04-22. | — | — |

If a security issue is accepted, add a row above with:

- A short stable identifier (e.g. `N-2026-001`).
- Date opened.
- Severity (Critical / High / Medium / Low / Informational).
- One-sentence summary **with no exploit detail**.
- Status (`open` / `mitigated` / `accepted-residual` / `closed`).
- Link to the private advisory or closed advisory/commit.

Closed issues roll into the relevant row in §3 or §4 and are removed from
this table.

---

## 6. How to add to this index

Future auditors and maintainers:

1. **Put the report in the right place.** Red-team reports, dogfood
   reports, and one-off audits go under [`docs/audits/`](../audits/). Per-
   release Newman reviews and reaudits go under [`docs/security/`](.) or
   at the root of `docs/` if they predate this convention.
2. **Name it predictably.** `docs/audits/<topic>-<YYYY-MM-DD>-<auditor>.md`
   or `docs/security/<topic>-<release>.md`.
3. **Add a row to §3 or §4 of this file.** Newest first. Include:
   date, scope, severity headline, link to report, resolution commit SHA
   (short, hyperlinked).
4. **Cross-link from `SECURITY.md`** only if the report drives a policy
   change — otherwise this index is the canonical entry point.
5. **Never leave a report orphaned.** If the report is published without an
   index entry, Newman will find it. Newman will file the PR. Newman will
   *also* file the finding against whoever forgot.

Open issues go in §5 until they close. Closed issues roll up into the
report row that closed them.

---

## 7. Canonical security docs

The operator-facing [`SECURITY.md`](../../SECURITY.md) is the disclosure
policy. The following documents under `docs/security/` and
`docs/runbooks/` are the long-form, canonical references. Each is linked
from the relevant `SECURITY.md` section; this list is the one-stop index.

| Topic | Doc | Closes audit finding |
|---|---|---|
| v2 threat model (STRIDE, residual-risk register) | [`../runbooks/threat-model-v2.md`](../runbooks/threat-model-v2.md) | F-5 follow-on |
| SBOM generation + freshness policy | [`./sbom.md`](./sbom.md) | F-8 |
| Supply-chain disclosure (NuGet pinning, feeds, provenance) | [`./supply-chain.md`](./supply-chain.md) | F-8 |
| Scanner reconciliation (Trivy CI / Grype local) | [`./scanners.md`](./scanners.md) | F-3 (doc half) |
| `UnsafeReplaceSecrets` coverage + edge cases | [`./redaction.md`](./redaction.md) | F-2 follow-on |
| One-page hardening checklist (PR-ready) | [`./hardening-checklist.md`](./hardening-checklist.md) | I-2/I-3/I-4 rollup |
| CVE / advisory log | [`./cve-log.md`](./cve-log.md) | F-11 |

If you add a new canonical doc here, also link it from the relevant
`SECURITY.md` section so the operator entry point stays complete.

---

*Clipboard stored. Paperwork complete. Moving on.*
