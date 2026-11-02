#!/usr/bin/env bash
# brand-inventory.sh -- count every place the product name appears, per name form.
#
# Purpose: make a rename MEASURABLE. Run it before the rename for the baseline and after it
# for the leftovers; "done" means the old forms reach the number you decided to keep (aliases,
# history, changelog). Read-only. Searches tracked files only.
#
# Excluded on purpose: docs/exec-reports, docs/diary, docs/img, .wgm. They are append-only
# history (episode logs, generated notes) and are never rewritten by a rename.
#
# Usage: scripts/brand-inventory.sh [--json]
set -euo pipefail

cd "$(git rev-parse --show-toplevel)"

EXCLUDES=(':!docs/exec-reports' ':!docs/diary' ':!docs/img' ':!.wgm')

# label|fixed-string. Order matters only for display.
FORMS=(
  "binary / command|az-ai"
  "repo / package|azure-openai-cli"
  "namespace / project|AzureOpenAI_CLI"
  "source dir|azureopenai-cli"
  "Azure env vars|AZUREOPENAI"
  "AZ_AI_ env vars|AZ_AI_"
)

json=0
[ "${1:-}" = "--json" ] && json=1

rows=()
for entry in "${FORMS[@]}"; do
  label=${entry%%|*}
  pat=${entry##*|}
  files=$(git grep -l -F -- "$pat" -- . "${EXCLUDES[@]}" 2>/dev/null | wc -l | tr -d ' ')
  lines=$(git grep -h -F -- "$pat" -- . "${EXCLUDES[@]}" 2>/dev/null | wc -l | tr -d ' ')
  rows+=("$label|$pat|$files|$lines")
done

if [ "$json" -eq 1 ]; then
  printf '{"commit":"%s","forms":[' "$(git rev-parse --short HEAD)"
  first=1
  for r in "${rows[@]}"; do
    IFS='|' read -r label pat files lines <<<"$r"
    [ "$first" -eq 1 ] || printf ','
    first=0
    printf '{"label":"%s","pattern":"%s","files":%s,"lines":%s}' "$label" "$pat" "$files" "$lines"
  done
  printf ']}\n'
else
  printf 'Brand inventory at %s (tracked files; exec-reports, diary, img, .wgm excluded)\n\n' "$(git rev-parse --short HEAD)"
  printf '%-22s %-18s %7s %7s\n' "form" "pattern" "files" "lines"
  printf '%-22s %-18s %7s %7s\n' "----" "-------" "-----" "-----"
  for r in "${rows[@]}"; do
    IFS='|' read -r label pat files lines <<<"$r"
    printf '%-22s %-18s %7s %7s\n' "$label" "$pat" "$files" "$lines"
  done
fi
