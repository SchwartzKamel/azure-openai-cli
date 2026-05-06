#!/usr/bin/env bash
# scripts/sbom-generate.sh
#
# Lightweight SBOM emitter used by the per-PR sbom.yml workflow and by the
# `make sbom` Make target. NOT a replacement for the canonical CycloneDX
# SBOM emitted by .github/workflows/release.yml at tag time -- that one is
# the auditor-grade artifact (signed alongside the release attestation).
#
# This script produces a JSON shape suitable for joining against Trivy
# output in scripts/provider-cve-report.sh:
#
#   {
#     "schema": "az-ai/sbom@1",
#     "generated_at": "<ISO-8601 UTC>",
#     "csproj": "azureopenai-cli/AzureOpenAI_CLI.csproj",
#     "packages": [
#       { "name": "Azure.AI.OpenAI", "version": "2.1.0", "transitive": false },
#       ...
#     ]
#   }
#
# Output path: $1 (default: dist/sbom.json).
#
# Tooling only. Not referenced by Program.cs.

set -euo pipefail

OUT="${1:-dist/sbom.json}"
CSPROJ="${CSPROJ:-azureopenai-cli/AzureOpenAI_CLI.csproj}"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "[sbom-generate] error: dotnet not on PATH" >&2
  exit 1
fi
if ! command -v jq >/dev/null 2>&1; then
  echo "[sbom-generate] error: jq not on PATH (apt install jq / brew install jq)" >&2
  exit 1
fi

mkdir -p "$(dirname "$OUT")"

# `dotnet list package --include-transitive` is text-only; we parse it.
# We deliberately avoid taking a hard dependency on the CycloneDX dotnet
# tool here -- that lives in release.yml and is quarantined behind a
# `dotnet tool restore`. This script is the cheap CI feedback loop.
RAW="$(mktemp)"
trap 'rm -f "$RAW"' EXIT

dotnet list "$CSPROJ" package --include-transitive --format json >"$RAW" 2>/dev/null || {
  # Fallback for older SDKs that don't support --format json.
  dotnet list "$CSPROJ" package --include-transitive >"$RAW"
}

GENERATED_AT="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

if head -c 1 "$RAW" | grep -q '{'; then
  # JSON shape (modern SDK).
  jq --arg generated_at "$GENERATED_AT" --arg csproj "$CSPROJ" '
    {
      schema: "az-ai/sbom@1",
      generated_at: $generated_at,
      csproj: $csproj,
      packages: (
        [ .projects[]?.frameworks[]?.topLevelPackages[]?
          | { name: .id, version: .resolvedVersion, transitive: false } ]
        +
        [ .projects[]?.frameworks[]?.transitivePackages[]?
          | { name: .id, version: .resolvedVersion, transitive: true } ]
      ) | unique_by("\(.name)@\(.version)")
    }
  ' "$RAW" >"$OUT"
else
  # Text fallback. Parse lines like:
  #   > Azure.AI.OpenAI    2.1.0    2.1.0
  awk '
    /^[[:space:]]*>[[:space:]]+[A-Za-z0-9._-]+/ {
      name=$2; version=$NF;
      printf "%s|%s\n", name, version;
    }
  ' "$RAW" | sort -u | jq -R -s --arg generated_at "$GENERATED_AT" --arg csproj "$CSPROJ" '
    split("\n") | map(select(length>0)) | map(split("|")) |
    {
      schema: "az-ai/sbom@1",
      generated_at: $generated_at,
      csproj: $csproj,
      packages: map({ name: .[0], version: .[1], transitive: null })
    }
  ' >"$OUT"
fi

PKG_COUNT="$(jq '.packages | length' "$OUT")"
echo "[sbom-generate] wrote $OUT ($PKG_COUNT packages)"
