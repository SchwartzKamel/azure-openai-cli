#!/usr/bin/env bash
# check-ignored-scripts.sh -- fail when a script is ignored by .gitignore yet a tracked build file needs it.
#
# Why: .gitignore carries `scripts/*` (it hides scratch files that agent tools drop there) and then
# whitelists each real script by name. A NEW script is therefore ignored until someone adds its
# `!scripts/<name>` line, and nothing says so: `git add -A` skips it silently, the commit goes green
# locally, and a clean checkout is missing a file the Makefile or a workflow calls. Two scripts nearly
# shipped that way (soup-nazi-2026-11-SN-1, found in S04E12).
#
# What it flags: an untracked file under scripts/ that .gitignore ignores AND that a tracked build file
# (the Makefile, a workflow, a test, another script) mentions by path. That combination is a real
# break, not a scratch file, so it is a hard failure. An ignored scratch script nobody references is
# left alone; warning on those would teach everyone to ignore this check.
#
# Usage:  scripts/check-ignored-scripts.sh
# Exit:   0 clean, 1 an ignored script is referenced by tracked files.
set -euo pipefail

cd "$(git rev-parse --show-toplevel)"

# Where a reference counts as evidence. Docs and history are excluded on purpose: they mention
# scripts that were never meant to be committed.
REFERENCE_PATHSPEC=(Makefile .github tests scripts)

status=0
while IFS= read -r file; do
  [ -n "$file" ] || continue
  # Search tracked content only; the ignored file itself is untracked, so it cannot match itself.
  refs=$(git grep -l -F -- "$file" -- "${REFERENCE_PATHSPEC[@]}" 2>/dev/null || true)
  [ -n "$refs" ] || continue
  status=1
  {
    echo "check-ignored-scripts: $file is ignored by .gitignore but tracked files reference it:"
    printf '  referenced by: %s\n' $refs
    echo "  A clean checkout will not have it. If it is meant to be committed, add this line to .gitignore:"
    echo "    !$file"
    echo "  (The \`scripts/*\` rule exists to hide scratch files; it does not apply to a script the build needs.)"
  } >&2
done < <(git ls-files --others --ignored --exclude-standard -- scripts)

if [ "$status" -eq 0 ]; then
  echo "check-ignored-scripts: clean (no ignored script is referenced by tracked build files)"
fi
exit "$status"
