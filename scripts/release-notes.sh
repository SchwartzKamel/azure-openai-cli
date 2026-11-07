#!/usr/bin/env bash
# release-notes.sh -- print the CHANGELOG notes for ONE version, for the release body.
#
#   scripts/release-notes.sh 2.4.0          (a leading 'v' is accepted: v2.4.0)
#
# Prints the body of the "## [2.4.0] ..." section (everything up to the next "## [" heading), trimmed.
# Fails (exit 1) when there is no such section or it is empty, instead of falling back to [Unreleased]:
# publishing a release whose notes are somebody else's, or empty, is a mistake to stop, not to paper over.
set -euo pipefail

VERSION="${1:-}"
[[ -n "$VERSION" ]] || { echo "release-notes.sh: usage: release-notes.sh <version>" >&2; exit 2; }
VERSION="${VERSION#v}"
[[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+([-.][0-9A-Za-z.]+)?$ ]] || { echo "release-notes.sh: '$VERSION' is not a version" >&2; exit 2; }

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CHANGELOG="${CHANGELOG:-$ROOT/CHANGELOG.md}"
[[ -f "$CHANGELOG" ]] || { echo "release-notes.sh: no CHANGELOG at $CHANGELOG" >&2; exit 1; }

NOTES="$(awk -v v="$VERSION" '
    index($0, "## [" v "]") == 1 { found = 1; next }
    found && /^## \[/ { exit }
    found { print }
' "$CHANGELOG")"

# trim leading and trailing blank lines
NOTES="$(printf '%s\n' "$NOTES" | sed -e :a -e '/^\n*$/{$d;N;ba' -e '}' | sed '/./,$!d')"

if ! grep -qF "## [$VERSION]" "$CHANGELOG"; then
    echo "release-notes.sh: CHANGELOG has no '## [$VERSION]' section. Cut the release (rename [Unreleased]) before tagging." >&2
    exit 1
fi
[[ -n "${NOTES//[[:space:]]/}" ]] || { echo "release-notes.sh: the '## [$VERSION]' section is empty" >&2; exit 1; }

printf '%s\n' "$NOTES"
