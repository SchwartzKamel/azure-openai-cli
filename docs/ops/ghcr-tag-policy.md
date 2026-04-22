# GHCR Tag Policy -- azure-openai-cli

**Status:** Canonical
**Owner:** Jerry (ops/DevOps) + Mr. Lippman (release management)
**Last reviewed:** 2026-04-22 (jerry-medium-sweep)
**Related:**

- [`docs/security/supply-chain.md`](../security/supply-chain.md) -- pinning + provenance.
- [`docs/runbooks/release-runbook.md`](../runbooks/release-runbook.md) -- the ritual that produces these tags.
- [`.github/workflows/release.yml`](../../.github/workflows/release.yml) -- the workflow that writes the tags.

> *You ever notice how nobody writes down the tag policy until someone
> pulls `:latest` in production and gets a v2 image against a v1 brew
> formula? Yeah. Me neither.* -- Jerry

---

## 1. What tags exist

The v1 and v2 pipelines publish to **two separate GHCR image names** so
they do not displace each other. Both are gated by the `v*` tag push
trigger in `release.yml`, split by job-level `if:` guards.

### 1.1 v1 image: `ghcr.io/schwartzkamel/azure-openai-cli`

Driven by `docker/metadata-action` with:

```yaml
tags: |
  type=semver,pattern={{version}}      # e.g. 1.8.1
  type=semver,pattern={{major}}.{{minor}}  # e.g. 1.8
  type=semver,pattern={{major}}        # e.g. 1
  type=sha,prefix=                     # e.g. a1b2c3d
```

Resulting tags for a `v1.8.1` release:

| Tag | Floats? | Meaning |
|---|---|---|
| `1.8.1` | No -- immutable | Exact release. Pin this in production. |
| `1.8` | Yes -- within minor line | Latest `1.8.z` patch. |
| `1` | Yes -- within major line | Latest `1.y.z` release. |
| `<sha>` | No -- immutable | Commit digest reference (git-SHA short form). |
| `latest` | Yes -- v1 line only | The most recently pushed `v1.*` tag. **Does NOT include v2.** |

### 1.2 v2 image: `ghcr.io/schwartzkamel/azure-openai-cli/az-ai-v2`

Same metadata-action shape, different image name:

```yaml
images: ghcr.io/${{ github.repository }}/az-ai-v2
tags: |
  type=semver,pattern={{version}}
  type=semver,pattern={{major}}.{{minor}}
  type=semver,pattern={{major}}
  type=sha,prefix=
```

Resulting tags for a `v2.0.5` release:

| Tag | Floats? | Meaning |
|---|---|---|
| `2.0.5` | No -- immutable | Exact release. Pin this in production. |
| `2.0` | Yes -- within minor line | Latest `2.0.z` patch. |
| `2` | Yes -- within major line | Latest `2.y.z` release. |
| `<sha>` | No -- immutable | Commit digest reference. |
| `latest` | Yes -- v2 line only | The most recently pushed `v2.*` tag. |

### 1.3 Why two images and not one?

Consumers of v1 (Homebrew formulae, Scoop manifests, Nix derivations
pinned by Bob Sacamano) reference `ghcr.io/schwartzkamel/azure-openai-cli:<tag>`.
If v2 published to the same image under a `2.x` tag, a `docker pull
…:latest` against a downstream tap could silently swap a v1 consumer onto
the v2 binary, which has a different artifact shape and CLI surface.

Keeping the images **separate** until v1 formally EOLs means:

- v1 users pulling `:latest` always get a v1 image.
- v2 users pull the `/az-ai-v2` suffix explicitly.
- Post-v1-EOL, we can retag or alias `/az-ai-v2` back to the canonical
  name without a flag day.

This is spelled out in `release.yml:315-319`.

---

## 2. When `:latest` moves

`:latest` is **not** emitted by this pipeline's `docker/metadata-action`
config directly -- it is moved by GHCR when the action pushes a tag that
the action considers "latest-eligible."

Rules in practice:

1. **Every successful `v1.*` tag push** → `ghcr.io/…/azure-openai-cli:latest`
   points at that image.
2. **Every successful `v2.*` tag push** → `ghcr.io/…/az-ai-v2:latest`
   points at that image.
3. **Pre-release tags** (anything with a `-alpha`, `-beta`, `-rc`
   suffix -- none shipped today) would NOT move `:latest`. If you
   introduce a pre-release tag, verify `docker/metadata-action`'s
   `flavor: latest=false` behavior before pushing.
4. **`workflow_dispatch` re-runs against an existing tag** do not create
   a new tag and therefore do not move `:latest`. They re-produce the
   same image under the same tags.

Implication: **never pin production to `:latest`.** Pin to the exact
semver (`2.0.5`) or to a digest. See §5.

---

## 3. Retention policy

GHCR retains all tags indefinitely unless explicitly deleted. We **do not
delete release tags.** Deletion policy:

| Tag pattern | Retention | Who can delete |
|---|---|---|
| `v*.*.*` (exact semver) | **Forever.** | Nobody, without a CVE rationale. |
| `<major>.<minor>` (floating) | Forever; value rewritten on each patch. | Nobody. |
| `<major>` (floating) | Forever; value rewritten on each minor/patch. | Nobody. |
| `latest` | Forever; value rewritten on each release. | Nobody. |
| `<sha>` (git-SHA) | Forever. | Nobody. |
| Untagged manifests (cleanup debris) | Prune after 90 days. | Jerry, monthly sweep. |

**Never rewrite an exact semver tag.** If `2.0.5` shipped broken, fix-forward
with `2.0.6`. Never delete-and-repush `2.0.5` -- it breaks every pinned
consumer silently. This is not negotiable.

For the CVE case -- if a published image contains a secret or actively
harmful content -- deletion is authorized by Newman (security) AND Mr.
Lippman (release) jointly, and the deletion must be logged in
[`docs/security/cve-log.md`](../security/cve-log.md) with a published
advisory. A post-incident ADR is required.

---

## 4. Provenance and attestation

Every published image gets a build-provenance attestation via
`actions/attest-build-provenance` (see `release.yml:155-160` for v1,
`:344-349` for v2). Verification:

```bash
# v1
gh attestation verify oci://ghcr.io/schwartzkamel/azure-openai-cli:1.8.1 \
  --repo SchwartzKamel/azure-openai-cli

# v2
gh attestation verify oci://ghcr.io/schwartzkamel/azure-openai-cli/az-ai-v2:2.0.5 \
  --repo SchwartzKamel/azure-openai-cli
```

See [`docs/verifying-releases.md`](../verifying-releases.md) for the full
verification flow (SBOM + attestation + SLSA provenance).

---

## 5. How to pull a specific version

### 5.1 Exact semver (recommended for production)

```bash
# v1
docker pull ghcr.io/schwartzkamel/azure-openai-cli:1.8.1

# v2
docker pull ghcr.io/schwartzkamel/azure-openai-cli/az-ai-v2:2.0.5
```

### 5.2 Digest pin (maximally reproducible)

```bash
# Look up the digest for a tag:
docker buildx imagetools inspect \
  ghcr.io/schwartzkamel/azure-openai-cli/az-ai-v2:2.0.5 \
  | grep Digest

# Then pin by digest:
docker pull ghcr.io/schwartzkamel/azure-openai-cli/az-ai-v2@sha256:<digest>
```

Digest pins survive upstream retag or policy mistakes; semver tags survive
only if nobody touches them. For anything that matters, use a digest.

### 5.3 Floating tag (development / CI on `main` only)

```bash
# Track the 2.0.z line -- automatically picks up patch releases
docker pull ghcr.io/schwartzkamel/azure-openai-cli/az-ai-v2:2.0
```

This is fine for `docs/`-grade examples and local development. **Not fine
for production.**

### 5.4 `:latest` is acceptable where?

- Quickstart docs (README, getting-started) -- yes, with a warning.
- CI pipelines that need deterministic behavior -- **no.**
- Downstream distribution manifests (Homebrew / Scoop / Nix) -- **no**,
  always pin to exact semver + digest.
- Examples in `docs/runbooks/` -- no; always show the semver form so
  copy-paste into production is safe.

---

## 6. Debugging "which tag am I actually running?"

The image labels carry provenance. To interrogate a pulled image:

```bash
docker inspect ghcr.io/schwartzkamel/azure-openai-cli/az-ai-v2:latest \
  --format '{{ index .Config.Labels "org.opencontainers.image.revision" }}'
# → git SHA

docker inspect ghcr.io/schwartzkamel/azure-openai-cli/az-ai-v2:latest \
  --format '{{ index .Config.Labels "org.opencontainers.image.version" }}'
# → semver
```

If either is empty, the image pre-dates the OCI-labels policy -- pull a
newer tag.

---

## 7. Open follow-ups

- [ ] Verify `docker/metadata-action`'s default `flavor` for pre-release
      handling before we ship a `-rc` tag for the first time.
- [ ] Add a monthly "untagged manifest prune" to Jerry's modernization
      cadence -- track in `docs/audits/` when that sweep lands.
- [ ] Once v1 formally EOLs, publish an ADR for consolidating the image
      name back to `ghcr.io/schwartzkamel/azure-openai-cli` and aliasing
      the `/az-ai-v2` path for one major cycle.

---

*One policy. Two images. Five tag shapes. All documented. Don't pin to
`:latest` in production.* -- Jerry
