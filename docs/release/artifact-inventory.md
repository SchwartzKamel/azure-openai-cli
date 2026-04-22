# Release artifact inventory

> "If it isn't on this page, it doesn't ship. If it's on this page
> and it isn't on the Release, we haven't shipped yet." — Mr. Lippman

Audience: release author running the pre-release checklist (gate 19)
and anyone downstream (Homebrew, Nix, Scoop, Docker users) who needs
to know what to expect for a given version.

This page is the canonical inventory of what the release workflow
publishes for every v2.x release, per RID, with the filename patterns
downstream packaging consumes.

Companion docs:
- [`pre-release-checklist.md`](pre-release-checklist.md) — gate 19
  validates every row below landed.
- [`ghcr-tag-lifecycle.md`](ghcr-tag-lifecycle.md) — how the OCI
  side of the inventory behaves over time.
- [`semver-policy.md`](semver-policy.md) §2 item 9 — the filename
  pattern is a versioned contract.
- [`../runbooks/packaging-publish.md`](../runbooks/packaging-publish.md)
  — Bob's tap/bucket publish flow consumes the artifacts listed here.
- [`../verifying-releases.md`](../verifying-releases.md) — user-facing
  verification steps.

**Scope.** v2 line. The v1 line uses the same workflow with different
RIDs and filenames; see the workflow `release.yml` directly.

---

## 1. Supported RIDs (v2, as of 2.0.4)

| RID              | OS           | Arch    | libc  | Primary consumer            |
|------------------|--------------|---------|-------|-----------------------------|
| `linux-x64`      | Linux        | x86_64  | glibc | Homebrew (Linux), direct    |
| `linux-musl-x64` | Linux (musl) | x86_64  | musl  | Alpine, Nix (musl), Docker  |
| `osx-arm64`      | macOS 11+    | ARM64   | —     | Homebrew (macOS), Rosetta 2 |
| `win-x64`        | Windows 10+  | x86_64  | —     | Scoop, direct               |

**`osx-x64` was dropped in v2.0.4.** Intel Mac users fall back to
Rosetta 2 over the `osx-arm64` binary, Docker (`linux/amd64`), or
build-from-source. See CHANGELOG `[2.0.4]` banner and
[`../runbooks/macos-runner-triage.md`](../runbooks/macos-runner-triage.md).

Dropping a RID is **MAJOR** per SemVer §3, bent to PATCH at v2.0.4
with a documented migration path — do not treat it as precedent.

---

## 2. Per-RID artifact matrix

For every release at version `X.Y.Z`, the release workflow publishes
the following to the **GitHub Release** page for tag `vX.Y.Z`.

Filename template: `az-ai-v2-<VERSION>-<RID>.<EXT>`.
`<VERSION>` is `X.Y.Z` (no `v` prefix). Extension is `.tar.gz` on
Unix-family RIDs, `.zip` on Windows.

### 2.1 `linux-x64`

| File                                                  | Contents                                                     | Required |
|-------------------------------------------------------|--------------------------------------------------------------|:--------:|
| `az-ai-v2-<VERSION>-linux-x64.tar.gz`                 | AOT ELF binary `az-ai-v2`, `LICENSE`, `NOTICE`, `README.md`. | ✅       |
| `az-ai-v2-<VERSION>-linux-x64.tar.gz.sha256`          | SHA256 sidecar (single-line `<hex>  <filename>`).            | ✅       |
| `az-ai-v2-<VERSION>-linux-x64.sbom.json`              | CycloneDX 1.5 JSON SBOM.                                     | ✅       |
| `az-ai-v2-<VERSION>-linux-x64.tar.gz.sig` (or equiv.) | Sigstore attestation material (per workflow).                | ✅       |

### 2.2 `linux-musl-x64`

| File                                              | Contents                                           | Required |
|---------------------------------------------------|----------------------------------------------------|:--------:|
| `az-ai-v2-<VERSION>-linux-musl-x64.tar.gz`        | AOT ELF binary, `LICENSE`, `NOTICE`, `README.md`.  | ✅       |
| `az-ai-v2-<VERSION>-linux-musl-x64.tar.gz.sha256` | SHA256 sidecar.                                    | ✅       |
| `az-ai-v2-<VERSION>-linux-musl-x64.sbom.json`     | CycloneDX 1.5 JSON SBOM.                           | ✅       |
| `az-ai-v2-<VERSION>-linux-musl-x64.tar.gz.sig`    | Sigstore attestation.                              | ✅       |

Note: Homebrew does not model musl; this leg exists for Nix (musl)
and container consumers who want a statically-linked binary.

### 2.3 `osx-arm64`

| File                                         | Contents                                             | Required |
|----------------------------------------------|------------------------------------------------------|:--------:|
| `az-ai-v2-<VERSION>-osx-arm64.tar.gz`        | AOT Mach-O binary, `LICENSE`, `NOTICE`, `README.md`. | ✅       |
| `az-ai-v2-<VERSION>-osx-arm64.tar.gz.sha256` | SHA256 sidecar.                                      | ✅       |
| `az-ai-v2-<VERSION>-osx-arm64.sbom.json`     | CycloneDX 1.5 JSON SBOM.                             | ✅       |
| `az-ai-v2-<VERSION>-osx-arm64.tar.gz.sig`    | Sigstore attestation.                                | ✅       |

The binary is unsigned by Apple (we do not notarize). Users on
Gatekeeper-strict macs must `xattr -d com.apple.quarantine` or run
under Rosetta via Docker. Documented in the Homebrew formula caveats.

### 2.4 `win-x64`

| File                                    | Contents                                                            | Required |
|-----------------------------------------|---------------------------------------------------------------------|:--------:|
| `az-ai-v2-<VERSION>-win-x64.zip`        | AOT PE binary `az-ai-v2.exe`, `LICENSE`, `NOTICE`, `README.md`.     | ✅       |
| `az-ai-v2-<VERSION>-win-x64.zip.sha256` | SHA256 sidecar.                                                     | ✅       |
| `az-ai-v2-<VERSION>-win-x64.sbom.json`  | CycloneDX 1.5 JSON SBOM.                                            | ✅       |
| `az-ai-v2-<VERSION>-win-x64.zip.sig`    | Sigstore attestation.                                               | ✅       |

The binary is unsigned by Microsoft (we do not Authenticode-sign yet).
SmartScreen prompt on first run is expected; Scoop install path is
the documented happy path.

---

## 3. Container artifacts

Published to **GHCR** (`ghcr.io/schwartzkamel/azure-openai-cli/az-ai-v2`)
by the `docker-publish-v2` job. Tag behavior per
[`ghcr-tag-lifecycle.md`](ghcr-tag-lifecycle.md).

| Tag                 | Class   | Published by workflow | Required |
|---------------------|---------|-----------------------|:--------:|
| `<VERSION>`         | version | yes                   | ✅       |
| `<MAJOR>.<MINOR>`   | float   | yes (GA only)         | ✅       |
| `<MAJOR>`           | float   | yes (GA only)         | ✅       |
| `latest`            | float   | yes (GA only)         | ✅       |

Each pushed tag carries:

- A manifest digest (`sha256:…`) — recorded in the GitHub Release
  body per pre-release-checklist gate 19.
- A Sigstore attestation — Rekor log index recorded in the release
  body.
- An SBOM attached to the image (separate from the per-tarball
  `.sbom.json` above; both exist).

Images are published as `linux/amd64` and `linux/arm64` multi-arch
manifests where the underlying distro supports it. (Current state:
amd64 primary; arm64 parity tracked in the release workflow matrix.)

---

## 4. GitHub Release body

Every release includes a Release body (Markdown) with:

- Summary / headline — pulled from the CHANGELOG banner quote if one
  exists.
- **Artifact inventory** section — a table matching §2 above with
  filename, size, and SHA256 per file.
- **Image digests** section — one line per platform leg, per
  [`ghcr-tag-lifecycle.md`](ghcr-tag-lifecycle.md) §3.
- **Attestations** section — Rekor log indexes for the image and
  (optionally) for each binary.
- Upgrade notes and breaking-change callouts for MAJOR / notable
  MINOR releases.
- A link to the full CHANGELOG entry.

The Release body is authored by the workflow from a template plus
the CHANGELOG entry; do not hand-edit it except to fix typos after
publish. Never mutate it after announce goes out.

---

## 5. Downstream packaging (what consumes this inventory)

The in-repo manifests under `packaging/` are hash-synced against the
inventory after every release. Bob's
[`../runbooks/packaging-publish.md`](../runbooks/packaging-publish.md)
then mirrors them to the external tap/bucket repos.

| Consumer                | Path                                               | Which RIDs it ships                        | Where it pulls from        |
|-------------------------|----------------------------------------------------|--------------------------------------------|----------------------------|
| Homebrew                | `packaging/homebrew/Formula/az-ai.rb` (+ `@X.Y.Z`) | `osx-arm64`, `linux-x64`                   | GitHub Release tarballs    |
| Nix flake               | `packaging/nix/flake.nix`                          | `linux-x64`, `linux-musl-x64`, `osx-arm64` | GitHub Release tarballs    |
| Scoop                   | `packaging/scoop/az-ai.json` (+ `versions/`)       | `win-x64`                                  | GitHub Release zip         |
| Docker                  | (not in `packaging/`; driven by `release.yml`)     | amd64 (+ arm64 where avail.)               | GHCR directly              |
| Homebrew tap (external) | `SchwartzKamel/homebrew-az-ai`                     | mirrors `packaging/homebrew`               | Bob's publish flow         |
| Scoop bucket (external) | `SchwartzKamel/scoop-az-ai`                        | mirrors `packaging/scoop`                  | Bob's publish flow         |

The Homebrew tap and Scoop bucket repos **may or may not exist yet**
— see [`../runbooks/packaging-publish.md`](../runbooks/packaging-publish.md)
§0. Until they're live, user-facing install instructions point at the
in-repo manifests. That is expected and does not block a release.

**Downstream implication of filename drift** — every consumer in the
table above derives download URLs from the
`az-ai-v2-<VERSION>-<RID>.<EXT>` pattern. A filename that doesn't
match the version tag (audit C-1) breaks all of them simultaneously.
That's why pre-release-checklist gate 4 (`verify-version-strings.sh`)
is non-negotiable.

---

## 6. Validation

After the workflow completes, the release author runs gate 19:

1. `gh release view v<VERSION> --json assets -q '.assets[].name' | sort`
   — every row required in §2 appears, for every RID in §1.
2. For each tarball/zip, confirm its `.sha256` sidecar and
   `.sbom.json` are present.
3. For each tarball/zip, confirm there's an attestation asset.
4. `gh api /users/SchwartzKamel/packages/container/azure-openai-cli%2Faz-ai-v2/versions`
   — the four tags from §3 resolve; capture the digest for each.
5. Paste the digests and the SHA256 list into the Release body under
   the `Image digests` / `Artifact inventory` sections.

A missing asset is a **no-go**. Fix forward with a new version tag;
do not mutate the existing Release.

— Mr. Lippman, release management
