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
    linux-x64|linux-musl-x64|linux-arm64|osx-x64|osx-arm64|win-x64|win-arm64) ;;
    *) die "unsupported rid '$RID' (expected linux-*, osx-*, or win-*)" ;;
esac

# Single-source-of-truth: parse <Version> from AzureOpenAI_CLI_V2.csproj so that
# tarball filenames stay in lock-step with the binary's --version output and the
# csproj itself. Previously hardcoded as "2.0.2" and never rolled past v2.0.2 —
# v2.0.3 and v2.0.4 tarballs shipped with `2.0.2` embedded in the filename
# (audit finding C-1, docs/audits/docs-audit-2026-04-22-lippman.md). Pinned by
# the VersionContractTests xUnit suite. STAGE_VERSION env var overrides for
# exceptional re-staging cases (e.g. the v2.0.4 re-release that had to match
# the already-uploaded filenames).
SCRIPT_DIR_EARLY="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CSPROJ="$SCRIPT_DIR_EARLY/../../azureopenai-cli-v2/AzureOpenAI_CLI_V2.csproj"
if [[ -n "${STAGE_VERSION:-}" ]]; then
    VERSION="$STAGE_VERSION"
elif [[ -f "$CSPROJ" ]]; then
    VERSION="$(sed -n 's|.*<Version>\(.*\)</Version>.*|\1|p' "$CSPROJ" | head -n1)"
    [[ -n "$VERSION" ]] || die "could not parse <Version> from $CSPROJ"
else
    die "csproj not found at $CSPROJ — cannot derive version"
fi

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
    # windows-latest ships no Info-ZIP `zip` (and neither does the bundled
    # Git-for-Windows MSYS bash) — v2.0.0 attempt #1 hit this, see
    # docs/launch/v2-release-attempt-1-diagnostic.md §Failure #1. PowerShell's
    # Compress-Archive is always present on windows-latest. Point it at the
    # stage dir (NOT stage/*) so the archive preserves the top-level
    # `az-ai-v2-<ver>-<rid>/` directory — matches the tar.gz layout enforced
    # by `zip -r <basename>` on the unix side and by the release-v2 job body.
    if command -v cygpath >/dev/null 2>&1; then
        # Git Bash on windows-latest: translate POSIX → Windows paths so
        # powershell.exe resolves them. MSYS_NO_PATHCONV is unreliable when
        # paths sit inside single-quoted argument strings.
        STAGE_WIN="$(cygpath -w "$STAGE_DIR")"
        OUT_WIN="$(cygpath -w "$OUT")"
    else
        STAGE_WIN="$STAGE_DIR"
        OUT_WIN="$OUT"
    fi
    command -v powershell.exe >/dev/null 2>&1 \
        || die "'powershell.exe' not on PATH — required on windows-latest to stage Windows artifact"
    powershell.exe -NoProfile -NonInteractive -Command \
        "Compress-Archive -Path '$STAGE_WIN' -DestinationPath '$OUT_WIN' -Force"
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
