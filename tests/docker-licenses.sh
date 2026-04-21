#!/usr/bin/env bash
# tests/docker-licenses.sh
#
# Verifies that the Docker image bundles license + attribution files under
# /licenses/ and carries the expected OCI metadata labels.
#
# Exit codes:
#   0  — all checks passed
#   77 — skipped (docker not on PATH; conventional "skip" code)
#   1  — a verification failed

set -euo pipefail

IMAGE_TAG="${IMAGE_TAG:-az-ai-v2:licensecheck}"
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"

if ! command -v docker >/dev/null 2>&1; then
    echo "[SKIP] docker not on PATH — skipping license bundling checks"
    exit 77
fi

echo "[1/3] Building image ${IMAGE_TAG} from ${REPO_ROOT}..."
docker build -t "${IMAGE_TAG}" "${REPO_ROOT}" >/dev/null

echo "[2/3] Verifying /licenses/ contents..."
listing="$(docker run --rm --entrypoint ls "${IMAGE_TAG}" /licenses/)"
echo "${listing}"

fail=0
for f in LICENSE NOTICE THIRD_PARTY_NOTICES.md; do
    if ! grep -qxF "${f}" <<<"${listing}"; then
        echo "[FAIL] /licenses/${f} missing from image"
        fail=1
    fi
done

echo "[3/3] Verifying OCI labels..."
labels="$(docker inspect "${IMAGE_TAG}" --format '{{json .Config.Labels}}')"
echo "${labels}"

for kv in \
    '"org.opencontainers.image.licenses":"MIT"' \
    '"org.opencontainers.image.source":"https://github.com/SchwartzKamel/azure-openai-cli"' \
    '"org.opencontainers.image.documentation":"https://github.com/SchwartzKamel/azure-openai-cli/blob/main/docs/licensing-audit.md"'
do
    if ! grep -qF "${kv}" <<<"${labels}"; then
        echo "[FAIL] expected label fragment not present: ${kv}"
        fail=1
    fi
done

if [[ "${fail}" -ne 0 ]]; then
    echo "[FAIL] docker-licenses.sh: one or more verifications failed"
    exit 1
fi

echo "[OK] docker-licenses.sh: license files + OCI labels verified"
exit 0
