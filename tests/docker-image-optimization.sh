#!/bin/bash

# Docker Image Optimization Tests - Shell Script Version
# This script validates Docker image optimizations without requiring bats
# See docs/proposals/improve-docker-image-speed.md for optimization guidelines
#
# Exit codes:
#   0 = All tests passed
#   1 = One or more tests failed

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
DOCKERFILE="$REPO_ROOT/Dockerfile"
DOCKERIGNORE="$REPO_ROOT/.dockerignore"
MAKEFILE="$REPO_ROOT/Makefile"

PASSED=0
FAILED=0

# ANSI colors
GREEN='\033[0;32m'
RED='\033[0;31m'
NC='\033[0m' # No Color

pass() {
    echo -e "${GREEN}✓${NC} $1"
    ((PASSED++))
}

fail() {
    echo -e "${RED}✗${NC} $1"
    ((FAILED++))
}

echo "========================================"
echo "Docker Image Optimization Tests"
echo "========================================"
echo ""

# =============================================================================
# POSITIVE TESTS - These should PASS when optimizations are correctly implemented
# =============================================================================

echo "Positive Tests (optimizations that SHOULD be present):"
echo ""

# Test: Multi-stage build
if [ "$(grep -c '^FROM' "$DOCKERFILE")" -ge 2 ]; then
    pass "Dockerfile uses multi-stage build pattern"
else
    fail "Dockerfile uses multi-stage build pattern"
fi

# Test: Build stage
if grep -q "AS build" "$DOCKERFILE"; then
    pass "Dockerfile has a build stage"
else
    fail "Dockerfile has a build stage"
fi

# Test: Runtime stage
if grep -q "AS runtime" "$DOCKERFILE"; then
    pass "Dockerfile has a runtime stage"
else
    fail "Dockerfile has a runtime stage"
fi

# Test: Alpine-based image
if grep "AS runtime" -A 1 "$DOCKERFILE" | grep -q "alpine"; then
    pass "Final stage uses Alpine-based image for smaller size"
else
    fail "Final stage uses Alpine-based image for smaller size"
fi

# Test: runtime-deps base
if grep "AS runtime" -A 1 "$DOCKERFILE" | grep -q "runtime-deps"; then
    pass "Dockerfile uses runtime-deps base (not full SDK) for final stage"
else
    fail "Dockerfile uses runtime-deps base (not full SDK) for final stage"
fi

# Test: COPY --from=build
if grep -q "COPY --from=build" "$DOCKERFILE"; then
    pass "Dockerfile copies only necessary artifacts from build stage"
else
    fail "Dockerfile copies only necessary artifacts from build stage"
fi

# Test: Non-root user
if grep -q "adduser" "$DOCKERFILE"; then
    pass "Dockerfile creates non-root user for security"
else
    fail "Dockerfile creates non-root user for security"
fi

# Test: USER directive
if grep -q "^USER" "$DOCKERFILE"; then
    pass "Dockerfile runs as non-root user"
else
    fail "Dockerfile runs as non-root user"
fi

# Test: Self-contained single file
if grep -q "PublishSingleFile=true" "$DOCKERFILE"; then
    pass "Dockerfile publishes as self-contained single file"
else
    fail "Dockerfile publishes as self-contained single file"
fi

# Test: Trimmed publish
if grep -q "PublishTrimmed=true" "$DOCKERFILE"; then
    pass "Dockerfile uses trimming to reduce size"
else
    fail "Dockerfile uses trimming to reduce size"
fi

# Test: .dockerignore exists
if [ -f "$DOCKERIGNORE" ]; then
    pass ".dockerignore file exists"
else
    fail ".dockerignore file exists"
fi

# Test: .git excluded
if grep -q "\.git" "$DOCKERIGNORE"; then
    pass ".dockerignore excludes .git directory"
else
    fail ".dockerignore excludes .git directory"
fi

# Test: build artifacts excluded
if grep -q "bin/" "$DOCKERIGNORE" && grep -q "obj/" "$DOCKERIGNORE"; then
    pass ".dockerignore excludes build artifacts (bin/obj)"
else
    fail ".dockerignore excludes build artifacts (bin/obj)"
fi

# Test: node_modules excluded
if grep -q "node_modules" "$DOCKERIGNORE"; then
    pass ".dockerignore excludes node_modules"
else
    fail ".dockerignore excludes node_modules"
fi

# Test: IDE settings excluded
if grep -q "\.vscode" "$DOCKERIGNORE" || grep -q "\.idea" "$DOCKERIGNORE"; then
    pass ".dockerignore excludes IDE settings"
else
    fail ".dockerignore excludes IDE settings"
fi

# Test: docker buildx
if grep -q "docker buildx build" "$MAKEFILE"; then
    pass "Makefile uses docker buildx for advanced features"
else
    fail "Makefile uses docker buildx for advanced features"
fi

# Test: apk cache cleaned
if grep -q "rm -rf /var/cache/apk" "$DOCKERFILE"; then
    pass "Dockerfile cleans package cache in runtime stage"
else
    fail "Dockerfile cleans package cache in runtime stage"
fi

# Test: apk --no-cache
if grep -q "apk add --no-cache" "$DOCKERFILE" || grep -q "apk upgrade --no-cache" "$DOCKERFILE"; then
    pass "Dockerfile uses apk --no-cache flag"
else
    fail "Dockerfile uses apk --no-cache flag"
fi

# Test: WORKDIR
if grep -q "^WORKDIR" "$DOCKERFILE"; then
    pass "Dockerfile sets appropriate workdir"
else
    fail "Dockerfile sets appropriate workdir"
fi

# Test: ENTRYPOINT
if grep -q "^ENTRYPOINT" "$DOCKERFILE"; then
    pass "Dockerfile has entrypoint defined"
else
    fail "Dockerfile has entrypoint defined"
fi

echo ""
echo "Negative Tests (anti-patterns that should NOT be present):"
echo ""

# =============================================================================
# NEGATIVE TESTS - These should FAIL if anti-patterns are present
# =============================================================================

# Test: No :latest tag
if ! grep "^FROM.*:latest" "$DOCKERFILE"; then
    pass "Dockerfile does NOT use latest tag in base image"
else
    fail "Dockerfile does NOT use latest tag in base image"
fi

# Test: No SDK in final stage
runtime_line=$(grep -n "AS runtime" "$DOCKERFILE" | cut -d: -f1)
if [ -n "$runtime_line" ]; then
    after_runtime=$(tail -n "+$runtime_line" "$DOCKERFILE")
    if ! echo "$after_runtime" | grep -q "sdk:"; then
        pass "Dockerfile does NOT include SDK in final stage"
    else
        fail "Dockerfile does NOT include SDK in final stage"
    fi
else
    fail "Dockerfile does NOT include SDK in final stage (no runtime stage found)"
fi

# Test: No uncleaned apt-get
if grep -q "apt-get install" "$DOCKERFILE"; then
    # Check that apt-get clean or rm -rf /var/lib/apt/lists exists somewhere in the Dockerfile
    if grep -q "apt-get clean\|rm -rf /var/lib/apt/lists" "$DOCKERFILE"; then
        pass "Dockerfile does NOT run apt-get without cleaning"
    else
        fail "Dockerfile does NOT run apt-get without cleaning"
    fi
else
    pass "Dockerfile does NOT run apt-get without cleaning"
fi

# Test: Essential source files not ignored
if ! grep -q "^\*\.cs$" "$DOCKERIGNORE" && ! grep -q "^\*\.csproj$" "$DOCKERIGNORE" && ! grep -q "^\*\.sln$" "$DOCKERIGNORE"; then
    pass ".dockerignore does NOT include essential source files"
else
    fail ".dockerignore does NOT include essential source files"
fi

# Test: Proper layer ordering (restore before full copy)
copy_all_line=$(grep -n "^COPY \. " "$DOCKERFILE" 2>/dev/null | head -1 | cut -d: -f1 || echo "")
restore_line=$(grep -n "dotnet restore" "$DOCKERFILE" | head -1 | cut -d: -f1)
if [ -n "$copy_all_line" ] && [ -n "$restore_line" ]; then
    if [ "$restore_line" -lt "$copy_all_line" ]; then
        pass "Dockerfile does NOT copy entire context before restore"
    else
        fail "Dockerfile does NOT copy entire context before restore"
    fi
else
    pass "Dockerfile does NOT copy entire context before restore"
fi

# Test: No unnecessary EXPOSE
if ! grep -q "^EXPOSE" "$DOCKERFILE"; then
    pass "Dockerfile does NOT expose unnecessary ports in runtime"
else
    fail "Dockerfile does NOT expose unnecessary ports in runtime"
fi

echo ""
echo "========================================"
echo "Results: $PASSED passed, $FAILED failed"
echo "========================================"

if [ "$FAILED" -gt 0 ]; then
    exit 1
else
    exit 0
fi
