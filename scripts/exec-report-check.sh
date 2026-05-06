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

# ---------------------------------------------------------------------------
# Phase 1: findings-backlog audit gate (W-01 closure).
#
# For every audit report under docs/audits/ (excluding _template.md and any
# file carrying a `Findings-Backlog-Exempt: true` front-matter line), every
# gate-tier finding -- CRITICAL / HIGH / MAJOR / RED -- must have a matching
# row in docs/findings-backlog.md. MEDIUM / LOW / MINOR / NIT / INFO are
# exempt (still encouraged, not enforced).
#
# Matching rule: a backlog row for a finding must contain BOTH the audit's
# filename AND the finding ID as a whole-word token, on the same line.
#
# Severity is determined per finding by, in order:
#   1. An inline gate-tier word (CRITICAL / HIGH / MAJOR / RED) in the
#      `### <ID> -- ...` heading line.
#   2. The most recent section heading of the form `## CRITICAL`,
#      `## HIGH`, `## MAJOR`, or `## RED` (Elaine-style audits).
# Findings whose severity cannot be resolved to a gate-tier are skipped --
# unindexed-non-gate findings are not gate failures.
# ---------------------------------------------------------------------------

repo_root=$(git rev-parse --show-toplevel 2>/dev/null || pwd)
backlog_file="$repo_root/docs/findings-backlog.md"
audits_dir="$repo_root/docs/audits"

if [ -d "$audits_dir" ] && [ -f "$backlog_file" ]; then
    missing_rows=""
    missing_count=0

    # Iterate audits. Skip the template and explicit opt-outs.
    while IFS= read -r -d '' audit; do
        base=$(basename "$audit")
        case "$base" in
            _template.md) continue ;;
        esac
        if grep -qE '^Findings-Backlog-Exempt:[[:space:]]*true[[:space:]]*$' "$audit"; then
            continue
        fi

        # Extract gate-tier finding IDs from this audit. awk tracks current
        # section severity (## CRITICAL / ## HIGH / ## MAJOR / ## RED) and
        # emits any `### <ID> -- ...` heading whose severity is gate-tier.
        ids=$(awk '
            BEGIN { sect = "" }
            /^## / {
                line = $0
                sub(/^## +/, "", line)
                # First word, uppercased.
                first = line
                sub(/[[:space:]].*$/, "", first)
                up = toupper(first)
                if (up == "CRITICAL" || up == "HIGH" || up == "MAJOR" || up == "RED") {
                    sect = up
                } else {
                    sect = ""
                }
                next
            }
            /^### / {
                # Match a finding heading: "### <ID> -- ..." or "### <ID>:".
                # ID = leading run of letters/digits/dash up to first space.
                line = $0
                sub(/^### +/, "", line)
                # Extract ID = first token before " --" or ":" or whitespace.
                id = line
                sub(/[[:space:]]+--.*$/, "", id)
                sub(/:.*$/, "", id)
                sub(/[[:space:]].*$/, "", id)
                if (id == "" || id !~ /^[A-Za-z]+-?[0-9]+$/) next

                up_line = toupper(line)
                # Pad with spaces so first/last word is matchable with the
                # "non-letter on each side" idiom -- portable across awks
                # (POSIX awk has no \b).
                pad = " " up_line " "
                gsub(/[^A-Z0-9]/, " ", pad)
                sev = ""
                if (pad ~ / CRITICAL /) sev = "CRITICAL"
                else if (pad ~ / HIGH /) sev = "HIGH"
                else if (pad ~ / MAJOR /) sev = "MAJOR"
                else if (pad ~ / RED /) sev = "RED"
                else if (sect != "") sev = sect
                if (sev != "") print id
            }
        ' "$audit")

        [ -z "$ids" ] && continue

        while IFS= read -r id; do
            [ -z "$id" ] && continue
            # A backlog row must contain both the audit basename AND the ID
            # as a whole-word token on the same line. Use awk for whole-word
            # matching against the literal ID (avoids regex meta in IDs).
            if ! awk -v base="$base" -v id="$id" '
                index($0, base) == 0 { next }
                {
                    # Substring check: id appears in the line and is NOT
                    # immediately followed by a digit (so "M1" does not
                    # falsely match "M11"). Left boundary is unconstrained
                    # because backlog rows prefix IDs with the auditor key
                    # (e.g., "elaine-2026-05-M1"), so a dash is expected
                    # on the left.
                    pos = 1
                    while ((p = index(substr($0, pos), id)) > 0) {
                        idx = pos + p - 1
                        after = substr($0, idx + length(id), 1)
                        if (after !~ /[0-9]/) { found = 1; exit }
                        pos = idx + length(id)
                    }
                }
                END { exit (found ? 0 : 1) }
            ' "$backlog_file"; then
                missing_rows="$missing_rows  $base :: $id"$'\n'
                missing_count=$((missing_count + 1))
            fi
        done <<< "$ids"
    done < <(find "$audits_dir" -maxdepth 1 -type f -name '*.md' -print0)

    if [ "$missing_count" -gt 0 ]; then
        {
            echo ""
            echo "[exec-report-check] FAIL (findings-backlog gate)"
            echo ""
            echo "$missing_count gate-tier finding(s) (CRITICAL/HIGH/MAJOR/RED) are not"
            echo "indexed in docs/findings-backlog.md:"
            echo ""
            printf '%s' "$missing_rows"
            echo ""
            echo "Required: add a row to docs/findings-backlog.md whose ID column"
            echo "          references the finding ID and whose Source column links"
            echo "          to the audit file. The row must contain both the audit"
            echo "          filename and the finding ID on the same line."
            echo ""
            echo "Opt out:  add 'Findings-Backlog-Exempt: true' as a front-matter"
            echo "          line in the audit (start of line, colon-separated)."
            echo "          Reserve for meta-process reports that index findings"
            echo "          elsewhere by design."
            echo ""
            echo "Skill:    .github/skills/findings-backlog.md"
            echo ""
        } >&2
        exit 1
    fi
fi

# ---------------------------------------------------------------------------
# Phase 2: original exec-report-per-push gate.
# ---------------------------------------------------------------------------

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
