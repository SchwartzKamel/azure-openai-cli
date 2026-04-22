# Vulnerability Scanners -- Trivy (CI) + Grype (local)

> *Hello. Newman.* Two scanners in the repo. One gates the pipeline; the
> other sits in the Makefile for people who like their CVEs at the
> terminal. We document both, honestly, so nobody thinks the other one is
> the source of truth.

**Status:** Canonical
**Last updated:** 2026-04-22
**Owner:** Newman + Jerry.
**Related audit finding:** [`docs/audits/docs-audit-2026-04-22-newman.md`](../audits/docs-audit-2026-04-22-newman.md) §F-3 (the prior `SECURITY.md` claimed Grype was authoritative; it isn't).

---

## 1. The honest story

- **CI gate (authoritative):** Trivy
  (`.github/workflows/ci.yml:119`, pinned at
  `aquasecurity/trivy-action@57a97c7e7821a5776cebc9bb87c984fa69cba8f1 # v0.35.0`).
  Runs on every PR and every push to `main`. Failing severity thresholds
  block merges. This is the scanner whose verdict matters for a release.
- **Local developer convenience:** Grype (`Makefile:148-151`,
  `make scan`). Anchore Grype, runs against the locally built image.
  Useful for a quick pass on a work-in-progress build without waiting for
  CI. **Not authoritative, not wired into any gate.**

The two tools use **different CVE databases** (Trivy → Aqua's DB; Grype →
Anchore's Vulners-derived DB). They will sometimes disagree at the edges.
**If Trivy is clean and Grype complains, CI wins.** File a note in
[`cve-log.md`](./cve-log.md) noting the Grype delta for tracking, but the
merge is not blocked.

---

## 2. Trivy -- what it covers

| Target | Run | Severity threshold |
|---|---|---|
| Built Docker image (v2) | `ci.yml:119` | `HIGH,CRITICAL` → fail job |
| Filesystem / config misconfigs | Same action, default Trivy scan types | Advisory only |

Trivy fetches its DB at action start and pins to the action SHA above.
Output is surfaced in the CI log; SARIF upload into the GitHub Security
tab is **not yet wired** and is tracked as follow-on work.

---

## 3. Grype -- what it covers

```bash
make scan
# equivalent to:
grype azureopenai-cli:gpt-5-chat
```

Requires a local Grype install:

```bash
curl -sSfL https://raw.githubusercontent.com/anchore/grype/main/install.sh \
    | sh -s -- -b /usr/local/bin
```

Scans the locally tagged image, reports OS + application CVEs, exits
non-zero on findings. Because it is not a CI gate, operators can ignore
it at their own risk -- but if Grype is clean, it is a useful sanity check
before opening a PR.

---

## 4. When they disagree

Symptom: Trivy reports `GHSA-xxxx-yyyy-zzzz: HIGH` on package `P@v1.2.3`,
Grype says the same package is clean.

Likely causes:

1. **Database lag.** One DB ingested the advisory later. Check advisory
   publication date vs both DB update timestamps.
2. **Different matching.** Trivy does SBOM-based matching; Grype does
   binary + metadata matching. Pre-release tags and vendored deps can
   match differently.
3. **Scope.** Trivy scans the whole image layer; Grype matches against
   discovered packages only.

**Policy:** Trivy wins. Note the delta in [`cve-log.md`](./cve-log.md) with
`status: grype-delta-only` so future responders don't re-investigate.

---

## 5. Why keep Grype at all

- Fast local iteration without pushing a branch.
- Useful when CI is slow or GitHub Actions is down.
- Historical reason: it was the original v1 scanner, still serves as a
  second opinion.
- Jackie (licensing) uses Grype's SBOM output for cross-checks.

If Grype ever disappears from the Makefile, it's because Jerry replaced
it with `trivy image` local invocation (equivalent capability, single
vendor). Not a priority.

---

## 6. See also

- [`docs/security/sbom.md`](./sbom.md) -- SBOM as the scanner's input.
- [`docs/security/supply-chain.md`](./supply-chain.md) -- pinning + feeds.
- [`docs/security/cve-log.md`](./cve-log.md) -- where scanner findings get
  tracked.
- `SECURITY.md` § 7 -- short user-facing summary pointing here.

---

*One scanner in front of the gate. One scanner on the bench. Both
documented. No ambiguity.* -- Newman
