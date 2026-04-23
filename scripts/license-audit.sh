#!/usr/bin/env bash
# scripts/license-audit.sh
# -----------------------------------------------------------------------------
# OSS license audit for NuGet dependency graph.
#
# Owner:   Jackie Chiles (OSS compliance, legal posture)
# Baseline: commit 81a1e3a — v2.0.0 manual audit (see docs/licensing-audit.md).
# Purpose: reproduce that audit automatically so CI can gate every dependency
#          change and surface regressions before they reach Mr. Lippman's
#          release cut.
#
# Method  (matches docs/licensing-audit.md):
#   1. `dotnet list <project> package --include-transitive --format json`
#   2. For each resolved package@version, resolve its license:
#        (a) ~/.nuget/packages/<pkg>/<ver>/<pkg>.nuspec
#              - <license type="expression">SPDX</license>      (modern)
#              - <licenseUrl>...</licenseUrl>                   (legacy)
#        (b) https://api.nuget.org/v3/registration5-gz-semver2/<pkg>/<ver>.json
#              (skipped when --offline)
#        (c) Manual-override table for packages the auditor verified against
#            upstream LICENSE files (see OVERRIDES below).
#   3. Cross-reference against scripts/license-allowlist.txt (SPDX IDs).
#   4. Hard-fail on copyleft (GPL*, LGPL*, AGPL*, MPL*, SSPL*, EPL*, CDDL*).
#   5. Warn (but do not fail) on MS-EULA — Microsoft historically shipped
#      MIT code under that legacy licenseUrl; requires manual review.
#   6. Exit 0 clean / 1 on any violation or unknown.
#
# Dependencies: bash, jq, curl, unzip (only curl is optional via --offline).
# -----------------------------------------------------------------------------
set -euo pipefail

# ---------- defaults ---------------------------------------------------------
PROJECT="azureopenai-cli"
FORMAT="text"
OFFLINE=0
VERBOSE=0

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ALLOWLIST_FILE="${SCRIPT_DIR}/license-allowlist.txt"
NUGET_CACHE="${NUGET_PACKAGES:-$HOME/.nuget/packages}"

# Hard-fail license families. Matched as a case-insensitive prefix against
# any SPDX identifier component in the discovered expression.
HARDFAIL_PATTERNS='^(GPL|LGPL|AGPL|MPL|SSPL|EPL|CDDL|CC-BY-NC)'

# Warn-only licenses (proprietary Microsoft EULA + similar).
WARN_PATTERNS='^(MS-EULA|Ms-PL|Ms-RL|PROPRIETARY)$'

# ---------- colors -----------------------------------------------------------
if [[ -t 1 ]] && [[ -z "${NO_COLOR:-}" ]]; then
  C_RED=$'\033[31m'; C_GRN=$'\033[32m'; C_YEL=$'\033[33m'
  C_CYA=$'\033[36m'; C_DIM=$'\033[2m';  C_BLD=$'\033[1m'; C_OFF=$'\033[0m'
else
  C_RED=; C_GRN=; C_YEL=; C_CYA=; C_DIM=; C_BLD=; C_OFF=
fi

# ---------- usage ------------------------------------------------------------
usage() {
  cat <<EOF
Usage: $(basename "$0") [options] [project-path]

Audit NuGet dependency licenses against the project allowlist.

Options:
  --project PATH      Project path (default: azureopenai-cli)
  --format=FMT        Output format: text|json (default: text)
  --offline           Do not query api.nuget.org; fail on missing nuspec.
  -v, --verbose       Print per-package resolution steps to stderr.
  -h, --help          Show this help.

Exit codes:
  0  All packages licensed under allowlisted SPDX identifiers.
  1  One or more packages disallowed, unknown, or copyleft.
  2  Usage / environment error.
EOF
}

# ---------- arg parsing ------------------------------------------------------
while [[ $# -gt 0 ]]; do
  case "$1" in
    --project)      PROJECT="$2"; shift 2 ;;
    --project=*)    PROJECT="${1#*=}"; shift ;;
    --format)       FORMAT="$2"; shift 2 ;;
    --format=*)     FORMAT="${1#*=}"; shift ;;
    --offline)      OFFLINE=1; shift ;;
    -v|--verbose)   VERBOSE=1; shift ;;
    -h|--help)      usage; exit 0 ;;
    --)             shift; break ;;
    -*)             echo "unknown flag: $1" >&2; usage >&2; exit 2 ;;
    *)              PROJECT="$1"; shift ;;
  esac
done

case "$FORMAT" in text|json) ;; *) echo "bad --format: $FORMAT" >&2; exit 2 ;; esac

for bin in jq dotnet; do
  command -v "$bin" >/dev/null || { echo "missing required binary: $bin" >&2; exit 2; }
done
if (( OFFLINE == 0 )); then
  command -v curl >/dev/null || { echo "curl required unless --offline" >&2; exit 2; }
fi

vlog() { (( VERBOSE )) && echo "${C_DIM}[audit]${C_OFF} $*" >&2 || true; }

# ---------- known licenseUrl → SPDX mapping ---------------------------------
# These are legacy-URL conventions documented in docs/licensing-audit.md.
# Any match here short-circuits resolution.
map_license_url() {
  local url="$1"
  # Strip common prefixes that carry the SPDX ID directly.
  case "$url" in
    https://licenses.nuget.org/*)
        echo "${url#https://licenses.nuget.org/}"; return 0 ;;
    https://raw.githubusercontent.com/dotnet/corefx/master/LICENSE.TXT)
        echo "MIT"; return 0 ;;
    https://opensource.org/licenses/MIT|http://opensource.org/licenses/MIT)
        echo "MIT"; return 0 ;;
    https://www.apache.org/licenses/LICENSE-2.0|http://www.apache.org/licenses/LICENSE-2.0|*LICENSE-2.0.txt|*LICENSE-2.0.html)
        echo "Apache-2.0"; return 0 ;;
    http://go.microsoft.com/fwlink/?LinkId=329770|https://go.microsoft.com/fwlink/?LinkId=329770)
        # Microsoft proprietary EULA stub — historically also used for MIT
        # Microsoft code. Flag for manual review; do not hard-fail.
        echo "MS-EULA"; return 0 ;;
  esac
  return 1
}

# ---------- manual overrides -------------------------------------------------
# Packages where nuspec + catalog both lack a licenseExpression and the
# licenseUrl only points at an upstream LICENSE file. Entries must cite the
# auditor who verified the upstream LICENSE. Format: "<name>@<version>=SPDX".
override_for() {
  local key="$1"  # e.g. "dotenv.net@3.1.2"
  case "$key" in
    # dotenv.net: licenseUrl points at upstream repo LICENSE (MIT).
    # Verified 2026-04-10 by Jackie Chiles; see docs/licensing-audit.md.
    dotenv.net@*) echo "MIT"; return 0 ;;
    # xunit.abstractions: legacy licenseUrl points at a dead GitHub path
    # (https://raw.githubusercontent.com/xunit/xunit/master/license.txt → 404).
    # xunit and all its sibling packages declare Apache-2.0 on nuget.org;
    # abstractions is shipped from the same repo under the same LICENSE.
    # Verified by Jackie Chiles against https://github.com/xunit/xunit (Apache-2.0).
    xunit.abstractions@*) echo "Apache-2.0"; return 0 ;;
  esac
  return 1
}

# ---------- nuspec parser ----------------------------------------------------
# Extracts a license expression or licenseUrl from a nuspec file. Writes
# "<spdx>|<source>" to stdout. Sources: expr, file, url, unknown.
parse_nuspec() {
  local nuspec="$1"
  [[ -f "$nuspec" ]] || { echo "|missing"; return; }
  # Strip XML namespaces/whitespace-naively with grep; nuspec is small.
  local expr url ltype
  expr=$(sed -n 's|.*<license type="expression">\([^<]*\)</license>.*|\1|p'  "$nuspec" | head -n1)
  if [[ -n "$expr" ]]; then
    echo "${expr}|expr"; return
  fi
  ltype=$(sed -n 's|.*<license type="\([^"]*\)".*|\1|p' "$nuspec" | head -n1)
  url=$(sed -n 's|.*<licenseUrl>\([^<]*\)</licenseUrl>.*|\1|p' "$nuspec" | head -n1)
  if [[ -n "$url" ]]; then
    local mapped
    if mapped=$(map_license_url "$url"); then
      echo "${mapped}|url"; return
    fi
    echo "licenseUrl:${url}|url"; return
  fi
  if [[ -n "$ltype" ]]; then
    echo "license-type:${ltype}|file"; return
  fi
  echo "|none"
}

# ---------- api.nuget.org fallback ------------------------------------------
fetch_api_license() {
  local name_lower="$1" ver="$2"
  local url="https://api.nuget.org/v3/registration5-gz-semver2/${name_lower}/${ver}.json"
  vlog "  fetch ${url}"
  local reg catalog entry_url expr lurl
  reg=$(curl -sS --compressed --max-time 15 "$url" 2>/dev/null) || return 1
  entry_url=$(echo "$reg" | jq -r '.catalogEntry // empty')
  [[ -z "$entry_url" || "$entry_url" == "null" ]] && return 1
  catalog=$(curl -sS --max-time 15 "$entry_url" 2>/dev/null) || return 1
  expr=$(echo "$catalog" | jq -r '.licenseExpression // empty')
  if [[ -n "$expr" && "$expr" != "null" ]]; then
    echo "${expr}|api"; return 0
  fi
  lurl=$(echo "$catalog" | jq -r '.licenseUrl // empty')
  if [[ -n "$lurl" && "$lurl" != "null" ]]; then
    local mapped
    if mapped=$(map_license_url "$lurl"); then
      echo "${mapped}|api"; return 0
    fi
    echo "licenseUrl:${lurl}|api"; return 0
  fi
  return 1
}

# ---------- classify a resolved license -------------------------------------
# stdin: SPDX expression (may be compound: "MIT AND Apache-2.0" etc.)
# stdout: status code: allowed | warn | fail | unknown
classify() {
  local lic="$1"
  [[ -z "$lic" ]] && { echo unknown; return; }
  case "$lic" in
    licenseUrl:*|license-type:*) echo unknown; return ;;
  esac
  # Split compound expressions on " AND "/" OR "/parens; each component must
  # be individually allowed. Conservative: if any component is hard-fail, fail.
  local normalized; normalized=$(echo "$lic" | tr -d '()' | sed -E 's/ (AND|OR|WITH) /\n/g')
  local any_unknown=0 any_fail=0 any_warn=0 all_allowed=1
  while IFS= read -r comp; do
    comp="${comp## }"; comp="${comp%% }"
    [[ -z "$comp" ]] && continue
    if echo "$comp" | grep -qEi "$HARDFAIL_PATTERNS"; then
      any_fail=1; all_allowed=0; continue
    fi
    if echo "$comp" | grep -qEi "$WARN_PATTERNS"; then
      any_warn=1; all_allowed=0; continue
    fi
    if ! grep -Fxq "$comp" "$ALLOWLIST_FILE" 2>/dev/null; then
      any_unknown=1; all_allowed=0
    fi
  done <<< "$normalized"
  if (( any_fail )); then echo fail
  elif (( any_unknown )); then echo unknown
  elif (( any_warn )); then echo warn
  elif (( all_allowed )); then echo allowed
  else echo unknown
  fi
}

# ---------- main -------------------------------------------------------------
[[ -d "$PROJECT" || -f "$PROJECT" ]] || { echo "project not found: $PROJECT" >&2; exit 2; }

# Strip allowlist file of comments / blanks in-memory.
if [[ ! -f "$ALLOWLIST_FILE" ]]; then
  echo "allowlist not found: $ALLOWLIST_FILE" >&2; exit 2
fi

vlog "running dotnet list on ${PROJECT}"
LIST_JSON=$(dotnet list "$PROJECT" package --include-transitive --format json 2>/dev/null) \
  || { echo "dotnet list failed" >&2; exit 2; }

# Extract: "name<TAB>version" per package across all frameworks.
PKGS=$(echo "$LIST_JSON" | jq -r '
  .projects[].frameworks[]
  | (.topLevelPackages // []) + (.transitivePackages // [])
  | .[]
  | [.id, .resolvedVersion] | @tsv
' | sort -u)

[[ -z "$PKGS" ]] && { echo "no packages resolved from ${PROJECT}" >&2; exit 2; }

TOTAL=0; ALLOWED=0; WARN=0; FAIL=0; UNKNOWN=0
# Row format emitted per package: name<TAB>ver<TAB>license<TAB>source<TAB>status
ROWS=""

while IFS=$'\t' read -r name ver; do
  [[ -z "$name" ]] && continue
  TOTAL=$((TOTAL+1))
  name_lower=$(echo "$name" | tr '[:upper:]' '[:lower:]')
  key="${name}@${ver}"
  vlog "resolve ${key}"

  # (c) manual override first — auditor-verified trumps whatever lives in the wild.
  if override_lic=$(override_for "${name_lower}@${ver}"); then
    license="$override_lic"; source="override"
  else
    license=""; source=""
    # (a) local nuspec
    nuspec="${NUGET_CACHE}/${name_lower}/${ver}/${name_lower}.nuspec"
    if [[ -f "$nuspec" ]]; then
      parsed=$(parse_nuspec "$nuspec")
      pl="${parsed%|*}"; ps="${parsed##*|}"
      if [[ -n "$pl" && "$ps" == "expr" ]]; then
        license="$pl"; source="nuspec"
      elif [[ -n "$pl" && "$ps" == "url" ]]; then
        # url mapper returned something (may be SPDX or licenseUrl:...)
        license="$pl"; source="nuspec-url"
      elif [[ "$ps" == "file" ]]; then
        license="$pl"; source="nuspec-file"
      fi
    fi
    # (b) api.nuget.org
    if [[ -z "$license" || "$license" == licenseUrl:* || "$license" == license-type:* ]]; then
      if (( OFFLINE == 0 )); then
        if api=$(fetch_api_license "$name_lower" "$ver"); then
          license="${api%|*}"; source="api"
        fi
      fi
    fi
  fi

  status=$(classify "$license")
  case "$status" in
    allowed) ALLOWED=$((ALLOWED+1)) ;;
    warn)    WARN=$((WARN+1)) ;;
    fail)    FAIL=$((FAIL+1)) ;;
    unknown) UNKNOWN=$((UNKNOWN+1)) ;;
  esac
  # Safe default for display.
  [[ -z "$license" ]] && license="(unresolved)"
  [[ -z "$source"  ]] && source="none"
  ROWS+="${name}"$'\t'"${ver}"$'\t'"${license}"$'\t'"${source}"$'\t'"${status}"$'\n'
done <<< "$PKGS"

# ---------- report -----------------------------------------------------------
if [[ "$FORMAT" == "json" ]]; then
  status="pass"; (( FAIL + UNKNOWN > 0 )) && status="fail"
  echo "$ROWS" | jq -Rn --arg status "$status" \
      --argjson total "$TOTAL" --argjson allowed "$ALLOWED" \
      --argjson warn "$WARN" --argjson fail "$FAIL" --argjson unknown "$UNKNOWN" \
      --arg project "$PROJECT" '
    [ inputs | select(length>0) | split("\t")
      | {name:.[0], version:.[1], license:.[2], source:.[3], classification:.[4]} ] as $rows
    | {
        status: $status,
        project: $project,
        summary: {total:$total, allowed:$allowed, warn:$warn, fail:$fail, unknown:$unknown},
        packages: $rows,
        violations: ($rows | map(select(.classification=="fail" or .classification=="unknown"))),
        warnings:   ($rows | map(select(.classification=="warn")))
      }'
  (( FAIL + UNKNOWN > 0 )) && exit 1 || exit 0
fi

# ---- text format ----
printf "%b\n" "${C_BLD}License audit — ${PROJECT}${C_OFF}"
printf "%b\n" "${C_DIM}allowlist: ${ALLOWLIST_FILE}${C_OFF}"
printf "\n"
printf "%-55s %-12s %-18s %-12s %s\n" "PACKAGE" "VERSION" "LICENSE" "SOURCE" "STATUS"
printf -- '%.0s-' {1..115}; printf '\n'

# Print fails first, then warns, then unknown, then allowed.
print_rows_by_status() {
  local want="$1" color="$2" label="$3"
  local shown=0
  while IFS=$'\t' read -r name ver license source status; do
    [[ -z "$name" ]] && continue
    [[ "$status" == "$want" ]] || continue
    printf "%b%-55s %-12s %-18s %-12s %-8s%b\n" \
      "$color" "$name" "$ver" "$license" "$source" "$label" "$C_OFF"
    shown=$((shown+1))
  done <<< "$ROWS"
}
print_rows_by_status fail    "$C_RED" "FAIL"
print_rows_by_status unknown "$C_RED" "UNKNOWN"
print_rows_by_status warn    "$C_YEL" "WARN"
print_rows_by_status allowed "$C_GRN" "OK"

printf -- '%.0s-' {1..115}; printf '\n'
printf "Total: ${C_BLD}%d${C_OFF}   OK: ${C_GRN}%d${C_OFF}   Warn: ${C_YEL}%d${C_OFF}   Unknown: ${C_RED}%d${C_OFF}   Fail: ${C_RED}%d${C_OFF}\n" \
  "$TOTAL" "$ALLOWED" "$WARN" "$UNKNOWN" "$FAIL"

# License-family breakdown (just the allowed bucket — matches manual summary).
printf "\n%bLicense-family breakdown (allowed):%b\n" "$C_CYA" "$C_OFF"
echo "$ROWS" | awk -F'\t' '$5=="allowed"{print $3}' | sort | uniq -c | sort -rn \
  | awk '{printf "  %3d  %s\n", $1, $2}'

if (( FAIL + UNKNOWN > 0 )); then
  printf "\n%b✗ LICENSE AUDIT FAILED%b — %d violations require remediation.\n" \
    "$C_RED$C_BLD" "$C_OFF" "$((FAIL + UNKNOWN))" >&2
  printf "  ${C_DIM}See docs/licensing-audit.md for the remediation protocol.${C_OFF}\n" >&2
  exit 1
fi

if (( WARN > 0 )); then
  printf "\n%b⚠ LICENSE AUDIT PASSED WITH WARNINGS%b — %d package(s) need manual review.\n" \
    "$C_YEL$C_BLD" "$C_OFF" "$WARN"
  exit 0
fi

printf "\n%b✓ LICENSE AUDIT CLEAR%b — all %d packages allowlisted.\n" \
  "$C_GRN$C_BLD" "$C_OFF" "$TOTAL"
exit 0
