# Verifying Releases

> How to cryptographically verify that an `azure-openai-cli` binary, container
> image, or SBOM you downloaded was built by this repository's CI and has not
> been tampered with.

This guide uses **v1.8.1** as the working example. The same workflow applies to
every release tag from v1.8.0 onward.

---

## 1. Why verify?

Supply-chain attacks work by substituting a malicious artifact for a legitimate
one, somewhere between the build machine and your laptop:

- A compromised mirror or CDN serving a patched binary.
- A typosquatted container image on a public registry.
- A man-in-the-middle on your download.
- A release page defaced by a stolen maintainer token.

Verification is the end-user's last line of defence. It answers one question:
**"Is the bytes-on-disk version I hold right now the exact bytes GitHub Actions
produced from tag `vX.Y.Z` of this repository?"**

If the answer is not a hard "yes", do not run the binary.

---

## 2. What we sign and attest

Every GitHub Release from v1.8.0 onward publishes:

| Artifact                                    | Format                | What proves it authentic                              |
|---------------------------------------------|-----------------------|-------------------------------------------------------|
| `azure-openai-cli-<rid>.tar.gz` / `.zip`    | Platform binary       | SLSA v1 build-provenance attestation (keyless Sigstore) |
| `azure-openai-cli-<rid>.sbom.json`          | CycloneDX 1.7 JSON    | Uploaded alongside signed binary                      |
| `ghcr.io/schwartzkamel/azure-openai-cli:<tag>` | OCI container image | SLSA v1 build-provenance attestation pushed to registry |

Attestations are produced by
[`actions/attest-build-provenance`](https://github.com/actions/attest-build-provenance)
using GitHub's OIDC token -- there are **no long-lived signing keys** to steal.
The signer identity is the workflow file itself, pinned to the release tag:
`.github/workflows/release.yml@refs/tags/vX.Y.Z`.

> We do **not** publish a separate `checksums.txt`. The attestation already
> binds a SHA-256 digest to the artifact; recording the digest elsewhere adds
> no security. You can still compute `sha256sum` locally for an integrity
> record -- see §4.

---

## 3. Prerequisites

You need two tools. Everything below is copy-pasteable on Linux and macOS.

```bash
# GitHub CLI -- handles attestation download + verification
# https://cli.github.com/
gh --version            # need >= 2.49 for `gh attestation verify`

# (Optional) cosign -- only if you want to inspect raw bundles yourself
# https://docs.sigstore.dev/cosign/system_config/installation/
cosign version

# Standard UNIX tooling
sha256sum --version     # coreutils on Linux; `shasum -a 256` on macOS
jq --version            # for SBOM inspection
```

If `gh` is not authenticated, run `gh auth login` once. Verification itself
hits the public Sigstore transparency log and works for anonymous users, but
`gh` needs a token to fetch attestations from the GitHub API.

---

## 4. Step-by-step: verify the Linux x64 binary of v1.8.1

### 4.1 Download the binary and its SBOM

```bash
mkdir -p ~/verify-azure-openai-cli && cd ~/verify-azure-openai-cli

gh release download v1.8.1 \
  --repo SchwartzKamel/azure-openai-cli \
  --pattern 'azure-openai-cli-linux-x64.tar.gz' \
  --pattern 'azure-openai-cli-linux-x64.sbom.json'
```

### 4.2 Record the SHA-256 (optional but useful for audit logs)

```bash
sha256sum azure-openai-cli-linux-x64.tar.gz
```

Expected output for v1.8.1:

```text
93c28f53d98c0fa699512d515f26522981ba32a24d668593e0830ca30520f03d  azure-openai-cli-linux-x64.tar.gz
```

> If your digest does not match this value, **stop**. Either the asset was
> re-uploaded (see the release page edit history) or you have a corrupt/tampered
> download. Proceed to §6.

### 4.3 Verify the build-provenance attestation

```bash
gh attestation verify azure-openai-cli-linux-x64.tar.gz \
  --repo SchwartzKamel/azure-openai-cli
```

Expected output (abbreviated):

```text
Loaded digest sha256:93c28f53d98c0fa699512d515f26522981ba32a24d668593e0830ca30520f03d for file://azure-openai-cli-linux-x64.tar.gz
Loaded 1 attestation from GitHub API

The following policy criteria will be enforced:
- Predicate type must match:................ https://slsa.dev/provenance/v1
- Source Repository Owner URI must match:... https://github.com/SchwartzKamel
- Source Repository URI must match:......... https://github.com/SchwartzKamel/azure-openai-cli
- Subject Alternative Name must match regex: (?i)^https://github.com/SchwartzKamel/azure-openai-cli/
- OIDC Issuer must match:................... https://token.actions.githubusercontent.com

✓ Verification succeeded!

- Attestation #1
  - Build repo:..... SchwartzKamel/azure-openai-cli
  - Build workflow:. .github/workflows/release.yml@refs/tags/v1.8.1
  - Signer repo:.... SchwartzKamel/azure-openai-cli
  - Signer workflow: .github/workflows/release.yml@refs/tags/v1.8.1
```

The two lines that matter are `✓ Verification succeeded!` **and** that the
signer workflow reference ends in `@refs/tags/v1.8.1`. A valid attestation from
a different tag proves authenticity but not the version you expected.

### 4.4 Verify the container image

> Note the tag format: GHCR repository names must be lowercase, and the image
> tag is the bare version **without** a leading `v` (e.g. `1.8.1`). The Git tag
> is `v1.8.1`; the OCI tag is `1.8.1`. The release notes show both forms.

```bash
gh attestation verify \
  oci://ghcr.io/schwartzkamel/azure-openai-cli:1.8.1 \
  --owner SchwartzKamel
```

Expected output (abbreviated):

```text
Loaded digest sha256:e050992d42e0338d892f7fe778aecf9e2cf512406aa126e700007b198b7a9286 for oci://ghcr.io/schwartzkamel/azure-openai-cli:1.8.1
Loaded 1 attestation from GitHub API
...
✓ Verification succeeded!

- Attestation #1
  - Build repo:..... SchwartzKamel/azure-openai-cli
  - Build workflow:. .github/workflows/release.yml@refs/tags/v1.8.1
```

Pin by digest in production deployments so the tag cannot be silently moved:

```bash
docker pull ghcr.io/schwartzkamel/azure-openai-cli@sha256:e050992d42e0338d892f7fe778aecf9e2cf512406aa126e700007b198b7a9286
```

### 4.5 Inspect the SBOM

Each binary ships with a CycloneDX 1.7 JSON SBOM. Confirm the format and list
the .NET dependencies that were bundled:

```bash
jq '{format: .bomFormat, version: .specVersion, count: (.components | length)}' \
  azure-openai-cli-linux-x64.sbom.json

jq -r '.components[].name' azure-openai-cli-linux-x64.sbom.json
```

Expected output for v1.8.1:

```text
{
  "format": "CycloneDX",
  "version": "1.7",
  "count": 15
}
Azure.AI.OpenAI
Azure.Core
dotenv.net
Microsoft.Bcl.AsyncInterfaces
Microsoft.Extensions.Configuration.Abstractions
... (10 more)
```

Feed the SBOM into your vulnerability scanner of choice (Grype, Trivy, OSV
Scanner) to track CVEs in the bundled libraries.

---

## 5. Verify every platform in one go

```bash
for rid in linux-x64 linux-musl-x64 osx-x64 osx-arm64; do
  gh release download v1.8.1 \
    --repo SchwartzKamel/azure-openai-cli \
    --pattern "azure-openai-cli-${rid}.tar.gz" --clobber
  gh attestation verify "azure-openai-cli-${rid}.tar.gz" \
    --repo SchwartzKamel/azure-openai-cli \
    || { echo "FAIL: ${rid}"; exit 1; }
done

gh release download v1.8.1 \
  --repo SchwartzKamel/azure-openai-cli \
  --pattern 'azure-openai-cli-win-x64.zip' --clobber
gh attestation verify azure-openai-cli-win-x64.zip \
  --repo SchwartzKamel/azure-openai-cli
```

---

## 6. Troubleshooting

| Symptom                                                        | Meaning / Fix                                                                                  |
|----------------------------------------------------------------|------------------------------------------------------------------------------------------------|
| `✗ verification failed: no matching attestations found`        | The file digest does not match any signed artifact. Likely tampering or wrong file. **Do not run.** |
| `Error: HTTP 404: Not Found (…attestations)`                   | Version predates attestations (before v1.8.0) or the repo slug is wrong. Re-check `--repo`.    |
| `repository name must be lowercase`                            | GHCR requires lowercase owner/name. Use `ghcr.io/schwartzkamel/...`, not `SchwartzKamel`.      |
| `MANIFEST_UNKNOWN` on `oci://…:v1.8.1`                         | The OCI tag omits the `v` prefix. Use `:1.8.1`.                                                 |
| `Signer workflow: …@refs/tags/vX.Y.Z` differs from expected    | The binary was built from a different release. Refuse to run it.                                |
| `gh: could not authenticate`                                   | Run `gh auth login`. Verification still calls the GitHub API to fetch the bundle.              |
| SHA-256 differs from §4.2 but verification passes              | Trust the attestation over any static digest list -- the asset was re-uploaded post-release.    |
| SHA-256 matches but verification fails                         | Clock skew, revoked Sigstore root, or offline environment. Retry with fresh `gh` / `cosign`.   |

A **failed verification is not a warning** -- it is a security event. Treat the
artifact as hostile, delete it, and report it (§7).

---

## 7. Reporting tampering or signing anomalies

If you find:

- An artifact on the release page that fails `gh attestation verify`,
- A container image tag whose provenance points at an unexpected workflow or
  tag,
- Or an SBOM whose digest is not covered by any attestation,

follow the coordinated-disclosure process in
[`SECURITY.md`](../SECURITY.md). Do **not** open a public GitHub issue; use the
private security advisory channel described there.

---

## 8. Further reading

- SLSA v1 specification -- <https://slsa.dev/spec/v1.0/>
- `actions/attest-build-provenance` -- <https://github.com/actions/attest-build-provenance>
- Sigstore & keyless signing -- <https://docs.sigstore.dev/>
- CycloneDX SBOM format -- <https://cyclonedx.org/docs/1.7/json/>
- GitHub CLI `attestation` reference -- <https://cli.github.com/manual/gh_attestation_verify>
