#!/usr/bin/env bash
# publish-taps.sh -- mirror the packaging manifests into the Homebrew tap and the Scoop bucket.
#
#   scripts/publish-taps.sh [--dry-run]
#
# Run it after a release is published and the manifests have been hash-synced to it. It
#   1. runs scripts/check-manifests.py on the current manifests and STOPS if any pin is not the published one;
#   2. clones the tap and the bucket, replaces their managed directories with the manifests from packaging/, and
#      commits "hopper <version>" only when something changed (running it twice is a no-op);
#   3. pushes to main (with --dry-run it shows what would change and pushes nothing).
#
# The tap and bucket are mirrors: a managed directory is replaced wholesale, so a file removed from packaging/ is removed there.
# Repository URLs and the packaging directory come from the environment so the script can be tested against local repositories:
#   TAP_REPO     (default git@github.com:SchwartzKamel/homebrew-tap.git)
#   BUCKET_REPO  (default git@github.com:SchwartzKamel/scoop-bucket.git)
#   PACKAGING_DIR (default <repo>/packaging)    CHECK_ARGS (extra arguments for check-manifests.py, e.g. --digests FILE)
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PACKAGING_DIR="${PACKAGING_DIR:-$ROOT/packaging}"
TAP_REPO="${TAP_REPO:-git@github.com:SchwartzKamel/homebrew-tap.git}"
BUCKET_REPO="${BUCKET_REPO:-git@github.com:SchwartzKamel/scoop-bucket.git}"
DRY=0
case "${1:-}" in --dry-run) DRY=1 ;; "") ;; *) echo "publish-taps.sh: usage: publish-taps.sh [--dry-run]" >&2; exit 2 ;; esac

die() { echo "publish-taps.sh: error: $*" >&2; exit 1; }
PY="$(command -v python3 || command -v python)" || die "python is required"

FORMULA="$PACKAGING_DIR/homebrew/Formula/hopper.rb"
SCOOP="$PACKAGING_DIR/scoop/hopper.json"
FLAKE="$PACKAGING_DIR/nix/flake.nix"
for f in "$FORMULA" "$SCOOP" "$FLAKE"; do [[ -f "$f" ]] || die "missing $f"; done

echo ">> Checking the manifests against the published release"
# shellcheck disable=SC2086  # CHECK_ARGS is deliberately word-split
"$PY" "$ROOT/scripts/check-manifests.py" ${CHECK_ARGS:-} "$FORMULA" "$SCOOP" "$FLAKE" || die "a manifest does not match the published release; nothing was published"

VERSION="$("$PY" -c 'import json,sys; print(json.load(open(sys.argv[1]))["version"])' "$SCOOP")"
[[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+ ]] || die "cannot read a version from $SCOOP"
# Commit as whoever commits to this repository: git's own identity if set, else the author of the latest commit here.
NAME="$(git config user.name 2>/dev/null || git -C "$ROOT" log -1 --format=%an 2>/dev/null || true)"
EMAIL="$(git config user.email 2>/dev/null || git -C "$ROOT" log -1 --format=%ae 2>/dev/null || true)"
[[ -n "$NAME" && -n "$EMAIL" ]] || die "no git identity: set user.name and user.email"

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# Two phases, so a failure while preparing the second repository cannot leave the first one published alone:
#   prepare: clone, replace the managed directories, and commit locally (nothing leaves this machine);
#   push:    push every repository that has a new commit.
prepare() { # <label> <url> <populate function>
    local label="$1" url="$2" populate="$3" dir="$WORK/$1"
    echo ">> $label: cloning $url"
    git clone --quiet "$url" "$dir" 2>/dev/null || die "cannot clone $url"
    ( cd "$dir"
      git checkout --quiet -B main
      "$populate" "$dir"
      git add -A
      if git diff --cached --quiet; then
          echo ">> $label: already up to date, nothing to publish"
      elif [[ "$DRY" == "1" ]]; then
          echo ">> $label: dry run, would commit 'hopper $VERSION':"
          git diff --cached --stat | sed 's/^/     /'
      else
          git -c user.name="$NAME" -c user.email="$EMAIL" commit --quiet -m "hopper $VERSION

Mirrored from packaging/ in SchwartzKamel/hopper by scripts/publish-taps.sh.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
          touch "$WORK/$label.commit"
          echo ">> $label: prepared hopper $VERSION ($(git rev-parse --short HEAD))"
      fi )
}

populate_tap() {
    local d="$1"
    rm -rf "$d/Formula" "$d/Aliases"
    mkdir -p "$d/Formula"
    cp -a "$PACKAGING_DIR/homebrew/Formula/." "$d/Formula/"
    [[ -d "$PACKAGING_DIR/homebrew/Aliases" ]] && cp -a "$PACKAGING_DIR/homebrew/Aliases" "$d/Aliases"
    cp "$PACKAGING_DIR/homebrew/tap-README.md" "$d/README.md"
    cp "$ROOT/LICENSE" "$d/LICENSE"
}

populate_bucket() {
    local d="$1" f
    rm -rf "$d/bucket"
    mkdir -p "$d/bucket"
    cp "$PACKAGING_DIR/scoop/hopper.json" "$d/bucket/"
    # no pins left is a valid state, so an empty directory must not be an error
    shopt -s nullglob
    for f in "$PACKAGING_DIR/scoop/versions/"*.json; do cp "$f" "$d/bucket/"; done
    shopt -u nullglob
    cp "$PACKAGING_DIR/scoop/bucket-README.md" "$d/README.md"
    cp "$ROOT/LICENSE" "$d/LICENSE"
}

prepare tap "$TAP_REPO" populate_tap
prepare bucket "$BUCKET_REPO" populate_bucket

for label in tap bucket; do
    if [[ -f "$WORK/$label.commit" ]]; then
        git -C "$WORK/$label" push --quiet origin main || die "cannot push the $label (the other repository may already be published: re-run to finish)"
        echo ">> $label: published hopper $VERSION"
    fi
done
echo ">> Done."
