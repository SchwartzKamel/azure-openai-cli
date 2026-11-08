#!/usr/bin/env bash
# mirror-image.sh -- copy a published container image to the repository's current registry path, BY DIGEST.
#
#   scripts/mirror-image.sh <version> [source-image] [target-image]
#
# Why this exists: the image path comes from the repository name at publish time, so after the repository was renamed to
# SchwartzKamel/hopper the new path ghcr.io/schwartzkamel/hopper had no images (the pre-rename releases live under
# ghcr.io/schwartzkamel/azure-openai-cli). Re-running the release workflow would REBUILD the archives and break every
# checksum and attestation, so this copies the image that is already published instead:
#
#   * the copy is by digest (docker buildx imagetools create), so the mirrored image IS the published one, byte for byte,
#     attestation manifest included; nothing is built and nothing is pushed except the copy;
#   * the destination digest is read back and must equal the source digest, or the script fails;
#   * a tag is mirrored only when the SOURCE carries it at the same digest (2.4.0 always; 2.4, 2 and latest only if the source
#     has them pointing at this version), so mirroring an old version can never move latest backwards.
#
# No new provenance is created: this repository did not build the image, and an attestation would claim it had. The original
# build's attestation travels with the image, and `gh attestation verify oci://... --owner <owner>` finds it by digest.
#
# Defaults: source ghcr.io/schwartzkamel/azure-openai-cli, target ghcr.io/<lowercase $GITHUB_REPOSITORY>.
set -euo pipefail

die() { echo "mirror-image.sh: error: $*" >&2; exit 1; }

VERSION="${1:-}"
[[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || die "usage: mirror-image.sh <X.Y.Z> [source-image] [target-image]"
SOURCE="${2:-ghcr.io/schwartzkamel/azure-openai-cli}"
if [[ -n "${3:-}" ]]; then
    TARGET="$3"
elif [[ -n "${GITHUB_REPOSITORY:-}" ]]; then
    TARGET="ghcr.io/${GITHUB_REPOSITORY,,}"
else
    die "no target image: pass one, or set GITHUB_REPOSITORY"
fi
[[ "$SOURCE" != "$TARGET" ]] || die "source and target are the same image ($SOURCE)"

MINOR="${VERSION%.*}"
MAJOR="${VERSION%%.*}"

digest_of() { # <image:tag> -> sha256:... (empty when the tag does not exist)
    docker buildx imagetools inspect "$1" --format '{{.Manifest.Digest}}' 2>/dev/null || true
}

SRC_DIGEST="$(digest_of "$SOURCE:$VERSION")"
[[ "$SRC_DIGEST" =~ ^sha256:[0-9a-f]{64}$ ]] || die "$SOURCE:$VERSION does not exist (or has no readable digest)"

TAGS=("$VERSION")
for t in "$MINOR" "$MAJOR" latest; do
    [[ "$(digest_of "$SOURCE:$t")" == "$SRC_DIGEST" ]] && TAGS+=("$t")
done

ARGS=()
for t in "${TAGS[@]}"; do ARGS+=(--tag "$TARGET:$t"); done

echo ">> Mirroring $SOURCE@$SRC_DIGEST to $TARGET with tags: ${TAGS[*]}"
docker buildx imagetools create "${ARGS[@]}" "$SOURCE@$SRC_DIGEST"

for t in "${TAGS[@]}"; do
    DST_DIGEST="$(digest_of "$TARGET:$t")"
    [[ "$DST_DIGEST" == "$SRC_DIGEST" ]] || die "$TARGET:$t is $DST_DIGEST after the copy, not $SRC_DIGEST"
    echo ">> verified $TARGET:$t = $SRC_DIGEST"
done
echo ">> Done: $TARGET now carries ${TAGS[*]} at $SRC_DIGEST."
