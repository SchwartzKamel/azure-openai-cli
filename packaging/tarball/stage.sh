#!/usr/bin/env bash
# stage.sh — build and stage az-ai-v2 release tarballs.
#
# Usage:
#   packaging/tarball/stage.sh <rid>
#
# Where <rid> is a .NET runtime identifier, e.g.:
#   linux-x64, linux-arm64, osx-x64, osx-arm64, win-x64
#
# Produces dist/az-ai-v2-<version>-<rid>.tar.gz (or .zip on win-*)
# containing the AOT-published binary alongside LICENSE, NOTICE,
# THIRD_PARTY_NOTICES.md, and README.md so every distributed artifact
# carries the full legal/attribution bundle.

set -euo pipefail

die() {
    echo "stage.sh: error: $*" >&2
    exit 1
}

RID="${1:-}"
[[ -n "$RID" ]] || die "missing runtime identifier. Usage: stage.sh <rid>  (e.g. linux-x64, osx-arm64, win-x64)"

case "$RID" in
    linux-x64|linux-arm64|osx-x64|osx-arm64|win-x64|win-arm64) ;;
    *) die "unsupported rid '$RID' (expected linux-*, osx-*, or win-*)" ;;
esac

VERSION="2.0.0"

# Resolve repo root relative to this script.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$REPO_ROOT"

PROJECT_DIR="azureopenai-cli-v2"
[[ -d "$PROJECT_DIR" ]] || die "project directory '$PROJECT_DIR' not found at repo root (cwd=$PWD)"

for doc in LICENSE NOTICE THIRD_PARTY_NOTICES.md README.md; do
    [[ -f "$doc" ]] || die "missing '$doc' at repo root — every distributed artifact must include it"
done

BIN_NAME="az-ai-v2"
[[ "$RID" == win-* ]] && BIN_EXT=".exe" || BIN_EXT=""

PUBLISH_DIR="$REPO_ROOT/artifacts/publish/$RID"
STAGE_DIR="$REPO_ROOT/artifacts/stage/az-ai-v2-$VERSION-$RID"
DIST_DIR="$REPO_ROOT/dist"

mkdir -p "$PUBLISH_DIR" "$DIST_DIR"
rm -rf "$STAGE_DIR"
mkdir -p "$STAGE_DIR"

echo ">> Publishing $PROJECT_DIR for $RID (AOT, Release)..."
dotnet publish "$PROJECT_DIR" \
    -c Release \
    -r "$RID" \
    -p:PublishAot=true \
    -o "$PUBLISH_DIR"

PUBLISHED_BIN="$PUBLISH_DIR/${BIN_NAME}${BIN_EXT}"
[[ -f "$PUBLISHED_BIN" ]] || die "expected '${BIN_NAME}${BIN_EXT}' in $PUBLISH_DIR after publish (check AssemblyName / OutputType in $PROJECT_DIR)"

echo ">> Staging artifact tree at $STAGE_DIR"
cp "$PUBLISHED_BIN" "$STAGE_DIR/"
cp LICENSE NOTICE THIRD_PARTY_NOTICES.md README.md "$STAGE_DIR/"

if [[ "$RID" == win-* ]]; then
    OUT="$DIST_DIR/az-ai-v2-$VERSION-$RID.zip"
    echo ">> Creating $OUT"
    rm -f "$OUT"
    command -v zip >/dev/null 2>&1 || die "'zip' not on PATH — required to stage Windows artifact"
    (cd "$(dirname "$STAGE_DIR")" && zip -r "$OUT" "$(basename "$STAGE_DIR")" >/dev/null)
else
    OUT="$DIST_DIR/az-ai-v2-$VERSION-$RID.tar.gz"
    echo ">> Creating $OUT"
    rm -f "$OUT"
    tar -C "$(dirname "$STAGE_DIR")" -czf "$OUT" "$(basename "$STAGE_DIR")"
fi

echo ">> Done: $OUT"
if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$OUT"
elif command -v shasum >/dev/null 2>&1; then
    shasum -a 256 "$OUT"
fi
