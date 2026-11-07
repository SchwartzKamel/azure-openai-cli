#!/usr/bin/env bash
# release-body.sh -- print the GitHub release body for a tag.
#
#   scripts/release-body.sh <tag> <owner/repo> <owner> <digests-file>
#
# Notes come from scripts/release-notes.sh (the CHANGELOG section for exactly this version; it fails if there is none).
# Every file name printed here is the name stage.sh gives the archive: hopper-<version>-<rid>.<ext>, where <version>
# is the tag WITHOUT its leading 'v' (the archives carry 2.4.0, not v2.4.0). Kept as a script, not inline YAML, so a
# test can hold the table to the release matrix.
set -euo pipefail

TAG="${1:-}"; REPO="${2:-}"; OWNER="${3:-}"; DIGESTS="${4:-}"
[[ -n "$TAG" && -n "$REPO" && -n "$OWNER" && -n "$DIGESTS" ]] \
    || { echo "release-body.sh: usage: release-body.sh <tag> <owner/repo> <owner> <digests-file>" >&2; exit 2; }
[[ "$TAG" =~ ^v[0-9]+\.[0-9]+\.[0-9]+([-.][0-9A-Za-z.]+)?$ ]] \
    || { echo "release-body.sh: '$TAG' is not a release tag (vX.Y.Z)" >&2; exit 2; }
[[ -f "$DIGESTS" ]] || { echo "release-body.sh: no digests file at $DIGESTS" >&2; exit 1; }

VERSION="${TAG#v}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
NOTES="$(bash "$ROOT/scripts/release-notes.sh" "$VERSION")"
IMAGE="ghcr.io/${REPO,,}"   # image references must be lowercase; GitHub's repository name keeps its case

cat <<BODY
${NOTES}

## Docker

\`\`\`bash
docker pull ${IMAGE}:${VERSION}
\`\`\`

## Binaries (\`hopper\`)

Every archive extracts to \`hopper-${VERSION}-<rid>/\` and holds the NativeAOT \`hopper\` binary, the \`az-ai\`
compatibility alias (a hard link on Linux and macOS, a small \`az-ai.cmd\` shim on Windows), and \`LICENSE\`,
\`NOTICE\`, \`THIRD_PARTY_NOTICES.md\` and \`README.md\`, so every distributed artifact carries the full
legal and attribution bundle.

| Platform | File |
|----------|------|
| Linux x64 (glibc) | \`hopper-${VERSION}-linux-x64.tar.gz\` |
| Linux x64 (musl/Alpine) | \`hopper-${VERSION}-linux-musl-x64.tar.gz\` |
| Linux ARM64 | \`hopper-${VERSION}-linux-arm64.tar.gz\` |
| Windows x64 | \`hopper-${VERSION}-win-x64.zip\` |
| Windows ARM64 | \`hopper-${VERSION}-win-arm64.zip\` |
| macOS Apple Silicon | \`hopper-${VERSION}-osx-arm64.tar.gz\` |

## Provenance

Every artifact is attested via Sigstore keyless signing (\`actions/attest-build-provenance\`). Verify with:

\`\`\`bash
gh attestation verify hopper-${VERSION}-linux-x64.tar.gz --owner ${OWNER}
\`\`\`

SBOMs (CycloneDX JSON) are attached alongside each archive. Each archive was checked before upload
(\`scripts/verify-archive.py\`): name, layout, contents, and, where the runner could run it, that
\`hopper --version\` and \`az-ai --version\` both report ${VERSION}.

## Package managers

- Nix: \`nix run github:${REPO}?dir=packaging/nix\`
- The Homebrew formula and the Scoop manifest are in \`packaging/\`. A published Homebrew tap and Scoop bucket
  do not exist yet, so there is no package-manager install command to give for them.

## SHA-256 Checksums

\`\`\`
$(cat "$DIGESTS")
\`\`\`
BODY
