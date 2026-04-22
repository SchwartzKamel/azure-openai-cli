# SBOM — Software Bill of Materials

> *Hello. Newman.* If a CVE hits tomorrow, the SBOM is the receipt that
> proves whether you were exposed. Keep it current. Keep it reachable.

**Status:** Canonical
**Last updated:** 2026-04-22
**Owner:** Newman (Security & Compliance Inspector) · coordinates with Jerry
on dependency upgrades and Mr. Lippman on release packaging.

---

## 1. What ships

The v2 release pipeline emits a **CycloneDX JSON SBOM per RID** alongside
each release artifact. Ground truth:

| Stage | Workflow step | Output |
|---|---|---|
| Tagged release, per-RID binary | `.github/workflows/release.yml:95-102` (`dotnet dotnet-CycloneDX ...`) | `az-ai-v2-<version>-<rid>.sbom.json` |
| Tagged release, per-RID artifact dist | `.github/workflows/release.yml:277-287` | same filename, uploaded to the release |
| Docker image | Trivy produces a CycloneDX report inline in CI (not yet archived) | CI log artifact only — see §5 |

Generator: [`CycloneDX .NET`](https://github.com/CycloneDX/cyclonedx-dotnet),
invoked via `dotnet tool restore` + `dotnet dotnet-CycloneDX` against the
v2 csproj (`azureopenai-cli-v2/AzureOpenAI_CLI_V2.csproj`).

Format: **CycloneDX 1.4 JSON**. Contains every transitive `PackageReference`
resolved by `dotnet restore`, with version pin, license metadata, and
package hash.

---

## 2. Where to find it

| Who | Where |
|---|---|
| Release consumers | [GitHub Releases](https://github.com/SchwartzKamel/azure-openai-cli/releases) → pick your tag → download `*.sbom.json` next to the binary |
| Auditors | `gh release download <tag> --pattern '*.sbom.json'` — or pull the release `artifacts/*.sbom.json` via the API |
| Local developers | `cd azureopenai-cli-v2 && dotnet tool restore && dotnet dotnet-CycloneDX AzureOpenAI_CLI_V2.csproj -o . -f sbom.json --output-format Json` |
| CI (non-release) | Trivy runs on every PR (`.github/workflows/ci.yml:119`) and reports CVEs at build time; it does not publish the SBOM outside the CI log |

Canonical filename pattern: `az-ai-v2-<version>-<rid>.sbom.json`
(e.g. `az-ai-v2-2.0.4-linux-musl-x64.sbom.json`).

---

## 3. Freshness policy

| Trigger | Action | SLO |
|---|---|---|
| Any `PackageReference` change in a csproj | Rebuild + republish SBOM with next release | Next tag |
| Tagged release | Fresh SBOM **must** be attached to the release | Release gate — Mr. Lippman enforces |
| Advisory against a listed package | Diff SBOM against affected versions; open advisory in [`cve-log.md`](./cve-log.md) | 48 h acknowledgment, per `SECURITY.md` §8 |
| Quarterly supply-chain review | Re-diff SBOM vs last quarter; note added/removed deps | Last week of each quarter |

A release **without** an SBOM is a release-blocker. Mr. Lippman's
pre-release checklist should fail the pipeline, not paper over it.

---

## 4. Reproducing the SBOM locally

Operators who want to validate what shipped without trusting the release
artifact:

```bash
# From a clean checkout of the tag you're auditing
git checkout v2.0.4
dotnet tool restore
dotnet dotnet-CycloneDX azureopenai-cli-v2/AzureOpenAI_CLI_V2.csproj \
    --output . \
    --filename az-ai-v2-2.0.4.sbom.json \
    --output-format Json

# Compare against the release asset
diff <(jq -S . az-ai-v2-2.0.4.sbom.json) \
     <(jq -S . <(gh release download v2.0.4 --pattern '*linux-x64.sbom.json' -O -))
```

Differences should be limited to timestamp + `serialNumber` fields. A
structural diff in the `components[]` array means the release artifact and
your reproduction disagree on dependencies — **that is an incident**, open
a private advisory.

Light-weight alternative for a name+version listing (useful in a hurry, not
a replacement for the CycloneDX doc):

```bash
cd azureopenai-cli-v2
dotnet list package --include-transitive
```

This does **not** produce a signed SBOM and is not acceptable as the
canonical release SBOM — it's a sanity check only.

---

## 5. Gaps (tracked, not closed)

- **Container image SBOM not archived.** Trivy generates one per CI run but
  it lives in the CI log. Follow-on work: upload the container SBOM as a
  release asset alongside the binary SBOMs. Tracked as future work;
  priority bumps if we distribute the image publicly on a registry.
- **No SBOM for v1.8.x.** v1 pipeline (`release.yml:95`) does emit a
  CycloneDX file (`azureopenai-cli/AzureOpenAI_CLI.csproj`), but v1 is in
  security-only maintenance; no backport of this doc is planned.
- **No signature on the SBOM itself.** The build-provenance attestation
  covers the binary; the SBOM rides alongside unsigned. Reproducibility
  (§4) is the compensating control.

---

## 6. See also

- [`docs/security/supply-chain.md`](./supply-chain.md) — NuGet pinning, feed trust, provenance.
- [`docs/security/scanners.md`](./scanners.md) — Trivy / Grype split.
- [`docs/security/cve-log.md`](./cve-log.md) — live advisory register.
- [`docs/runbooks/threat-model-v2.md`](../runbooks/threat-model-v2.md) § T-5.

---

*If the SBOM and the binary disagree, the binary is wrong. File the paperwork.* — Newman
