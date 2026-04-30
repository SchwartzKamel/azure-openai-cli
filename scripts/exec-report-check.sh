#!/usr/bin/env bash
# scripts/exec-report-check.sh
#
# Mechanical enforcement for the exec-report convention. Fails when a
# push range touches files outside docs/exec-reports/ but adds no new
# `docs/exec-reports/sNNeMM-*.md` file.
#
# Used by:
#   * `make exec-report-check` (wired into `make preflight`)
#   * `.git/hooks/pre-push` (installed via `make install-hooks`)
#
# Opt-out: include `Skip-Exec-Report: <reason>` as a git trailer
# (start-of-line, colon-separated) in any commit body in the range
# under inspection. Use sparingly -- doc-typo fixes, dependency
# bumps, and hotfix rollbacks are the common legitimate cases.
#
# Exit 0 = green (or nothing to check). Exit 1 = missing exec-report.
# Exit 0 = also returned when the repo is in a state we cannot reason
# about (no upstream, detached HEAD, initial commit) -- we never block
# the user on infrastructure ambiguity.

set -euo pipefail

# Locate range. Prefer @{u} (tracked upstream); fall back to origin/main.
if upstream=$(git rev-parse --abbrev-ref --symbolic-full-name '@{u}' 2>/dev/null); then
    range="$upstream..HEAD"
elif git rev-parse --verify origin/main >/dev/null 2>&1; then
    range="origin/main..HEAD"
else
    echo "[exec-report-check] no upstream and no origin/main; skipping" >&2
    exit 0
fi

# Anything to push?
new_commits=$(git rev-list "$range" 2>/dev/null || true)
if [ -z "$new_commits" ]; then
    exit 0
fi

# Opt-out: any commit in the range with a `Skip-Exec-Report:` trailer
# (start-of-line, colon-separated, like git's standard trailer convention).
# Using a trailer rather than a free-text tag avoids false positives when
# a commit message *describes* the opt-out mechanism.
if git log "$range" --pretty=%B | grep -qiE '^Skip-Exec-Report:[[:space:]]'; then
    echo "[exec-report-check] opted out via Skip-Exec-Report: trailer in commit body" >&2
    exit 0
fi

# Did this range add a new exec-report?
added_reports=$(git diff --name-only --diff-filter=A "$range" -- 'docs/exec-reports/s[0-9]*.md' 2>/dev/null || true)
if [ -n "$added_reports" ]; then
    echo "[exec-report-check] OK: $(echo "$added_reports" | wc -l) new exec-report(s) in range" >&2
    exit 0
fi

# Are the changes confined to docs/exec-reports/ already? Then no new
# report needed (you're editing prior reports, not adding work that
# warrants one).
changed_outside=$(git diff --name-only "$range" | grep -vE '^docs/exec-reports/' || true)
if [ -z "$changed_outside" ]; then
    echo "[exec-report-check] OK: range edits only existing exec-reports" >&2
    exit 0
fi

# Fail.
commit_count=$(echo "$new_commits" | wc -l | tr -d ' ')
{
    echo ""
    echo "[exec-report-check] FAIL"
    echo ""
    echo "Pushing $commit_count commit(s) without a new exec-report:"
    echo ""
    git log "$range" --pretty='  %h %s' | sed 's/^/  /'
    echo ""
    echo "These commits touched files outside docs/exec-reports/:"
    echo ""
    echo "$changed_outside" | head -20 | sed 's/^/  /'
    if [ "$(echo "$changed_outside" | wc -l)" -gt 20 ]; then
        echo "  ... and $(($(echo "$changed_outside" | wc -l) - 20)) more"
    fi
    echo ""
    echo "Required: add docs/exec-reports/sNNeMM-kebab-title.md per"
    echo "          .github/skills/exec-report-format.md"
    echo ""
    echo "Opt out:  add a 'Skip-Exec-Report: <reason>' trailer (start"
    echo "          of line, like Co-authored-by:) to any commit body"
    echo "          in the range. Use only for trivial changes that"
    echo "          do not warrant an episode write-up (typo fixes,"
    echo "          dependency bumps, hotfix rollbacks)."
    echo ""
} >&2

exit 1
