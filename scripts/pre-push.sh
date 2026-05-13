#!/usr/bin/env bash
# scripts/pre-push.sh
#
# Pre-push gate. Installed by `make install-hooks` as `.git/hooks/pre-push`.
# Chains three checks against the push range:
#
#   1. exec-report-check  -- every push that touches files outside
#      docs/exec-reports/ must add a new sNNeMM-*.md report.
#      Opt out per commit: `Skip-Exec-Report: <reason>` trailer.
#
#   2. docs-lint          -- markdownlint-cli2 on every *.md file in
#      the push range. Mirrors `.github/workflows/docs-lint.yml`.
#      Opt out per commit: `Skip-Docs-Lint: <reason>` trailer.
#
#   3. ascii-check        -- bans U+2018/U+2019/U+201C/U+201D/U+2013/U+2014
#      in *.md files in the push range. Mirrors the smart-quote step in
#      docs-lint.yml.
#      Opt out per commit: `Skip-Docs-Lint: <reason>` trailer.
#
# Bypass for the whole push: `git push --no-verify`. Use only in genuine
# emergencies and follow up with a fix-forward in the next push.
#
# Exit 0 = all gates green. Exit 1 = at least one gate failed.

set -euo pipefail

repo_root=$(git rev-parse --show-toplevel 2>/dev/null || pwd)
cd "$repo_root"

# ---------------------------------------------------------------------------
# Gate 1: exec-report-check (delegates to the existing script).
# ---------------------------------------------------------------------------
bash scripts/exec-report-check.sh

# ---------------------------------------------------------------------------
# Determine the push range. Mirror exec-report-check.sh's discovery logic so
# the two gates always agree on what "the push" means.
# ---------------------------------------------------------------------------
if upstream=$(git rev-parse --abbrev-ref --symbolic-full-name '@{u}' 2>/dev/null); then
    range="$upstream..HEAD"
elif git rev-parse --verify origin/main >/dev/null 2>&1; then
    range="origin/main..HEAD"
else
    echo "[pre-push] no upstream and no origin/main; skipping docs-lint / ascii-check" >&2
    exit 0
fi

# Nothing to push?
new_commits=$(git rev-list "$range" 2>/dev/null || true)
if [ -z "$new_commits" ]; then
    exit 0
fi

# ---------------------------------------------------------------------------
# Skip-Docs-Lint trailer: parallel to Skip-Exec-Report. Any commit in the
# range with `Skip-Docs-Lint: <reason>` (start-of-line, colon-separated)
# opts the whole push out of the docs-lint + ascii-check gates. Use only
# for legitimate edge cases (bulk renames where lint will be cleaned in a
# follow-up, generated-content imports, etc.).
# ---------------------------------------------------------------------------
if git log "$range" --pretty=%B | grep -qiE '^Skip-Docs-Lint:[[:space:]]'; then
    echo "[pre-push] docs-lint + ascii-check skipped via Skip-Docs-Lint: trailer" >&2
    exit 0
fi

# Touched *.md files in the push range (Added or Modified).
mapfile -t changed_md < <(git diff --name-only --diff-filter=AM "$range" -- '*.md' 2>/dev/null || true)

if [ "${#changed_md[@]}" -eq 0 ]; then
    echo "[pre-push] no *.md changes in range; docs-lint + ascii-check skipped" >&2
    exit 0
fi

# ---------------------------------------------------------------------------
# Gate 2: markdownlint-cli2 on the changed *.md files. We pass file paths
# explicitly so the gate is fast on small pushes; the global config in
# .markdownlint-cli2.jsonc / .markdownlintignore still applies.
# ---------------------------------------------------------------------------
echo "[pre-push] docs-lint on ${#changed_md[@]} changed *.md file(s) ..." >&2
if ! NODE_OPTIONS=--max-old-space-size=4096 npx --yes markdownlint-cli2 "${changed_md[@]}" >&2; then
    {
        echo ""
        echo "[pre-push] FAIL: markdownlint-cli2 reported errors above."
        echo ""
        echo "Required: fix the errors and re-attempt the push, or run"
        echo "          'make docs-lint' locally to iterate."
        echo ""
        echo "Opt out:  add 'Skip-Docs-Lint: <reason>' trailer (start of"
        echo "          line, like Co-authored-by:) to any commit body in"
        echo "          the range. Use only for bulk-renames / imports"
        echo "          that will be cleaned in a follow-up."
        echo ""
        echo "Bypass:   'git push --no-verify' (do not normalize this)."
        echo ""
    } >&2
    exit 1
fi

# ---------------------------------------------------------------------------
# Gate 3: ASCII check on the changed *.md files. Bans the six bytes the
# server-side smart-quote step bans.
# ---------------------------------------------------------------------------
echo "[pre-push] ascii-check on ${#changed_md[@]} changed *.md file(s) ..." >&2
hits=$(grep -nP '[\x{2018}\x{2019}\x{201C}\x{201D}\x{2013}\x{2014}]' "${changed_md[@]}" 2>/dev/null || true)
if [ -n "$hits" ]; then
    {
        echo ""
        echo "[pre-push] FAIL: smart quote or en/em dash detected"
        echo ""
        echo "$hits"
        echo ""
        echo "Required: replace per .github/skills/ascii-validation.md"
        echo "          U+2018/U+2019 -> '   U+201C/U+201D -> \""
        echo "          U+2013 -> -    U+2014 -> --"
        echo ""
        echo "Opt out:  add 'Skip-Docs-Lint: <reason>' trailer to any"
        echo "          commit body in the range."
        echo ""
    } >&2
    exit 1
fi

echo "[pre-push] docs-lint + ascii-check clean." >&2
exit 0
