# v1.8.0 Post-Release Security Audit

**Auditor:** Newman (Security Inspector)
**Date:** post-release, tag `v1.8.0` (commit `6f71a75`)
**Scope:** release pipeline output, supply-chain posture, workflow permissions.

---

## Executive Summary

**Verdict: 🔴 RED — release pipeline failed; no GitHub Release, binaries, or binary attestations were published for v1.8.0.**

The v1.8.0 release workflow (run `24637009891`) failed in all five
`build-binaries` matrix jobs at the **Generate SBOM (CycloneDX)** step because
the pinned tool version does not exist on NuGet:

> `Version 4.0.2 of package cyclonedx is not found in NuGet feeds https://api.nuget.org/v3/index.json.`

Because `release` depends on `build-binaries`, the release job never executed
and **no `v1.8.0` GitHub Release exists** (`gh release view v1.8.0` → `release
not found`). No signed binaries, no SBOM assets, no build-provenance
attestations for the binary artifacts.

The container half of the pipeline *did* succeed: the
`docker-publish` job pushed `ghcr.io/schwartzkamel/azure-openai-cli:v1.8.0`
and generated a keyless Sigstore build-provenance attestation for the image
digest. OpenSSF Scorecard ran cleanly on `main` post-tag. Workflow
permissions and action pinning remain solid.

End-user impact: anyone trying to `curl` a binary from the v1.8.0 release
page gets a 404. The container image is the only shippable artifact today.

---

## Control Checklist

| # | Control | Status | Evidence |
|---|---------|--------|----------|
| 1 | `release.yml` run succeeded for `v1.8.0` | ❌ | Run `24637009891` — 5/5 `build-binaries` matrix jobs failed; `release` job skipped. |
| 2 | GitHub Release `v1.8.0` exists with binaries | ❌ | `gh release view v1.8.0` → `release not found`. |
| 3 | SBOM (CycloneDX JSON) attached per artifact | ❌ | Step failed: CycloneDX `4.0.2` not published to NuGet. No SBOM generated. |
| 4 | `actions/attest-build-provenance` attestation per binary | ❌ | Step skipped — SBOM step failure short-circuited the job before attestation. |
| 5 | Container image `ghcr.io/schwartzkamel/azure-openai-cli:v1.8.0` pushed | ✅ | `docker-publish` job green (`72034373849`); tag produced by `docker/metadata-action` semver pattern. |
| 6 | Container image build-provenance attestation published | ✅ | `actions/attest-build-provenance@e8998f94…` with `push-to-registry: true` ran to completion in the same job. |
| 7 | OpenSSF Scorecards ran on latest `main` | ✅ | Runs `24636916226` and `24637109051` both green; SARIF uploaded to code-scanning. |
| 8 | Workflow permissions least-privilege (no `write-all`) | ✅ | `ci.yml`/`release.yml` default `contents: read`; `scorecards.yml` top-level `read-all` with narrow job-level writes for `security-events`/`id-token` only. Grepped `.github/workflows/*.yml` — no `permissions: write-all` or unscoped PATs. |
| 9 | All third-party Actions pinned by commit SHA | ✅ | Every `uses:` in `ci.yml`, `release.yml`, `scorecards.yml` references a 40-char SHA with a `# vX.Y.Z` comment. |
|10 | Secrets hygiene in workflows | ✅ | Only `secrets.GITHUB_TOKEN` used (docker login to GHCR). No long-lived PATs, no echoed secrets. |
|11 | Dependabot / automated dep updates configured | ⚠️ | Not yet in repo — Jerry is landing `.github/dependabot.yml` today per the v1.9 plan. Tracked, not whitewashed. |

Legend: ✅ pass · ⚠️ in-flight / acceptable gap · ❌ broken, needs fix.

---

## Evidence Links

- Failed release run: https://github.com/SchwartzKamel/azure-openai-cli/actions/runs/24637009891
- Scorecard runs: https://github.com/SchwartzKamel/azure-openai-cli/actions/runs/24636916226 · https://github.com/SchwartzKamel/azure-openai-cli/actions/runs/24637109051
- Tag commit: `6f71a75` (`release: v1.8.0`)
- Container ref (expected): `ghcr.io/schwartzkamel/azure-openai-cli:v1.8.0` — pushed by `docker-publish` job `72034373849`, attestation recorded to the registry (requires `gh auth refresh -s read:packages` to enumerate digests locally; not available to the audit shell).

Failing step log excerpt (one of five identical failures):

```
build-binaries (linux-x64) › Generate SBOM (CycloneDX)
  dotnet tool install --global CycloneDX --version 4.0.2
  Version 4.0.2 of package cyclonedx is not found in NuGet feeds
    https://api.nuget.org/v3/index.json.
  ##[error]Process completed with exit code 1.
```

---

## Follow-up Items (Maintainer Action Required)

1. **Fix the SBOM tool pin** — `CycloneDX` tool's latest published version on
   NuGet is in the `3.x` line (e.g. `3.0.8`) under the package id
   `CycloneDX`, case-sensitive. Recommend either:
   - Pin to a known-good version, e.g. `dotnet tool install --global CycloneDX --version 3.0.8`, OR
   - Switch to `CycloneDX/gh-dotnet-generate-sbom-action` pinned by SHA, which handles tool resolution upstream.
   Verify by running the tool locally before re-tagging.
2. **Re-cut v1.8.0** after the SBOM fix: either delete+retag `v1.8.0` or roll
   forward to `v1.8.1`. Prefer `v1.8.1` — rewriting a signed tag invalidates
   any consumer who already pulled the container digest.
3. **Backfill binary attestations** for whatever tag is shipped. Until then,
   the README's "download a signed binary" claim is aspirational.
4. **Confirm container attestation is verifiable** end-to-end with
   `gh attestation verify oci://ghcr.io/schwartzkamel/azure-openai-cli:v1.8.0 --owner SchwartzKamel`
   from a host with `read:packages` scope — this audit shell couldn't reach
   the package API.
5. **Dependabot** — land `.github/dependabot.yml` (Jerry, today) covering
   `nuget`, `github-actions`, and `docker` ecosystems, weekly cadence,
   grouped minor/patch.

---

## Recommendations for v1.9

- **Gate the release job on an explicit "SBOM smoke test" job** that runs in
  CI on every PR touching `release.yml`, so a broken tool pin fails fast
  instead of at tag-push time.
- **Add a pre-release dry-run workflow** (`workflow_dispatch` on a throwaway
  tag) that executes the full matrix into a staging release, so we catch
  SBOM/attestation regressions before cutting a real tag.
- **Pin `CycloneDX` via a lockfile** (dotnet tool manifest,
  `.config/dotnet-tools.json`) committed to the repo, instead of a
  command-line `--version` flag. Same for any other tool installed at
  release time.
- **Upgrade Node.js-20-based actions** (`actions/checkout`,
  `actions/setup-dotnet`, `actions/cache`, the `docker/*` family,
  `actions/attest*`). GitHub will force Node.js 24 on 2026-06-02 and remove
  Node.js 20 on 2026-09-16; bump pins well before then.
- **Turn on CodeQL** (`github/codeql-action/init` + `analyze`) for `csharp`
  to complement Scorecards with actual SAST.
- **Add `cosign verify-attestation` regression test** in CI that pulls the
  latest published image and asserts the provenance predicate parses and
  matches `github.repository`.
- **Require signed commits / `verified` status on release tags** via a
  branch/tag protection rule, and document the signing key in
  `docs/security/release-signing.md`.
- **Enforce `permissions:` at the top of every workflow** (already the case;
  add a Scorecards "Token-Permissions" badge to README to keep it honest).
- **Publish SBOMs to the Sigstore transparency log** in addition to
  attaching them as release assets — the `attest-sbom` action does this in
  one step.

---

## Final Word

The hardening *code* landed correctly — pinned SHAs, scoped permissions,
Scorecards wired up, container attestation working. But a single bad
version string in the SBOM step turned the v1.8.0 binary release into a
no-op. Ship a `v1.8.1` with a working SBOM tool and the checklist above
flips to all-green.
