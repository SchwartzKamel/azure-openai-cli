# Supply-Chain Disclosure

> *Hello. Newman.* The supply chain is where trust decisions happen before
> you ever read a line of our code. This is where we write down the trust
> assumptions so they can be audited.

**Status:** Canonical
**Last updated:** 2026-04-22
**Owner:** Newman (security) + Jerry (dependency upgrades) + Mr. Lippman
(release mechanics).

---

## 1. Overview

| Layer | Source | Pinning | Verification |
|---|---|---|---|
| Base container image | `mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine` | **Digest** (`sha256:...` in `Dockerfile.v2:80`) | Tag-mutation resistant |
| .NET SDK (build) | `mcr.microsoft.com/dotnet/sdk:10.0-alpine` | Digest-pinned in `Dockerfile.v2` build stage | Re-pinned on SDK upgrades |
| NuGet packages | `nuget.org` (default feed) | `Version="x.y.z"` in csproj (exact, not range) | Restore-time hash check |
| GitHub Actions | `@<commit-sha>` comment-annotated | **SHA-pinned**, not `@v4` float | Dependabot + manual review |
| SBOM generator | `CycloneDX.NET` via `dotnet tool restore` | `.config/dotnet-tools.json` lock | Reproducible from tag |

---

## 2. NuGet — transitive pinning

`dotnet restore` reads `PackageReference` entries in
`azureopenai-cli-v2/AzureOpenAI_CLI_V2.csproj`. We commit **exact versions**
(e.g. `Version="2.1.0"`, not `Version="2.*"`). This gives:

- **Deterministic top-level resolution.** Same csproj → same top-level
  packages, always.
- **Transitives resolved at restore time.** The restore picks the highest
  compatible version the direct deps need. Transitive drift is possible
  between restores **if an upstream dep publishes a new compatible
  patch**.

To eliminate transitive drift entirely, enable the lockfile:

```xml
<!-- Not currently enabled; tracked as follow-on work -->
<PropertyGroup>
  <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  <RestoreLockedMode>true</RestoreLockedMode>
</PropertyGroup>
```

When enabled, `packages.lock.json` pins every transitive dep by exact
version + content hash. Until then, the **SBOM emitted at release time**
(see [`docs/security/sbom.md`](./sbom.md)) is the canonical record of the
exact transitive closure that shipped. Reconstruction from the SBOM is
supported.

### Direct NuGet dependencies (v2, as of 2026-04-22)

Live truth is the SBOM; the snapshot below is a *reader convenience*:

| Package | Version | Role |
|---|---|---|
| `Microsoft.Agents.AI` | `1.1.0` | MAF parent agent runtime |
| `Microsoft.Agents.AI.OpenAI` | `1.1.0` | OpenAI channel for MAF |
| `Azure.AI.OpenAI` | `2.1.0` (GA) | Azure OpenAI client SDK |
| `dotenv.net` | `3.1.2` | `.env` parsing |
| `OpenTelemetry` + `.Api` + `.Exporter.OpenTelemetryProtocol` | `1.15.2` | Opt-in telemetry (OTLP) |

For the full transitive closure, pull the SBOM from the release.

---

## 3. Feed trust

- **Default feed:** `https://api.nuget.org/v3/index.json`. No private
  mirror, no pull-through cache.
- **Alternate feeds:** **none configured** in the repo. Any `nuget.config`
  addition must land via PR and inherits branch-protection review —
  treat new feeds like new production dependencies.
- **Prerelease policy:** v2 deliberately dropped the `Azure.AI.OpenAI
  2.3.0-beta.1` prerelease in favor of the `2.1.0` GA (see
  `docs/security/reaudit-v2-phase5.md`). New prereleases require
  sign-off from Newman + Jerry.

---

## 4. Build provenance

Each tagged release emits a **SLSA v1 build-provenance attestation** via
GitHub's `actions/attest-build-provenance@e8998f9` (pinned by SHA in
`.github/workflows/release.yml:103, 287`). The attestation binds:

- Subject: the release artifact (`*.tar.gz`, `*.zip`).
- Builder: `github-hosted-runner` executing `release.yml`.
- Source: the git SHA of the tagged commit.

Verify with:

```bash
gh attestation verify az-ai-v2-2.0.4-linux-musl-x64.tar.gz \
    --repo SchwartzKamel/azure-openai-cli
```

This is the primary integrity guarantee for release artifacts. The
attestation does **not** cover the SBOM file itself — see
[`docs/security/sbom.md`](./sbom.md) §5.

No Sigstore `cosign` keyless signing is wired in yet; the GitHub-native
attestation (backed by Sigstore under the hood) is the only provenance
story today. Adding a `cosign sign` step is tracked as follow-on work.

---

## 5. Base-image hygiene

- **Pinned by digest,** not by tag. See `Dockerfile.v2:80`.
- **Rebuild cadence:** weekly in CI (not release-tied), plus immediately
  after an Alpine or .NET security advisory.
- **Update flow:**
  ```bash
  # 1. resolve the new digest for the intended tag
  docker buildx imagetools inspect mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine
  # 2. update Dockerfile.v2:80 with the new @sha256:...
  # 3. rebuild, rescan (Trivy), open PR
  ```

`mcr.microsoft.com` is owned by Microsoft and served via Azure Front Door;
tag mutation would require control of that infrastructure. Pinning by
digest is belt + suspenders.

---

## 6. GitHub Actions posture

- **All third-party actions pinned by commit SHA** with a `# v<tag>`
  comment. See `.github/workflows/ci.yml` and `release.yml` — every `uses:`
  line is pinned.
- **Workflow permissions:** least-privilege at the job level (`contents:
  read` by default; `contents: write` + `id-token: write` only on the
  release job for attestation signing).
- **Dependabot:** enabled for `github-actions` ecosystem to flag action
  version drift.

---

## 7. Disclosure + response

If a supply-chain compromise is suspected (upstream package takeover,
registry outage with substitutions, malicious workflow run):

1. **Treat it as a security incident.** Open a private advisory per
   [`SECURITY.md` § 8](../../SECURITY.md#8-reporting-vulnerabilities).
2. **Capture the SBOM** of the last-known-good release and the suspect
   release. Diff them. That diff **is** the exposure.
3. **Rotate release tokens** (`GITHUB_TOKEN` scope review; NuGet.org API
   keys if we ever publish packages).
4. **Document in [`cve-log.md`](./cve-log.md).** Even a false alarm
   earns a row so future responders know it was investigated.

---

## 8. Residual risk

| ID | Risk | Compensating control | Owner |
|---|---|---|---|
| S-1 | NuGet lockfile not enforced; transitive drift between restores | SBOM at release is canonical | Jerry |
| S-2 | No Sigstore `cosign` signature on binaries | `attest-build-provenance` covers binaries | Newman |
| S-3 | Container SBOM not published | Binary SBOMs + Trivy report in CI | Newman |
| S-4 | No private NuGet mirror | Pinned exact versions + CI Trivy gate | Jerry + Newman |
| S-5 | No nuget.org-upstream takeover detection between Trivy runs | Incident-response flow (§7) | Newman |

---

## 9. See also

- [`docs/security/sbom.md`](./sbom.md) — CycloneDX generation + freshness.
- [`docs/security/scanners.md`](./scanners.md) — Trivy / Grype.
- [`docs/security/cve-log.md`](./cve-log.md) — advisory register.
- [`docs/runbooks/threat-model-v2.md`](../runbooks/threat-model-v2.md) § T-5.
- `SECURITY.md` § 7 — high-level policy.

---

*Trust is a chain. We're writing down every link.* — Newman
