#!/usr/bin/env bats

# Docker Image Optimization Tests
# These tests validate the Docker image build optimizations as described in
# docs/proposals/improve-docker-image-speed.md
#
# Test philosophy: "Pass the pass, fail the fail"
# - Positive tests verify that optimizations ARE present
# - Negative tests verify that anti-patterns are NOT present

DOCKERFILE="${BATS_TEST_DIRNAME}/../Dockerfile"
DOCKERIGNORE="${BATS_TEST_DIRNAME}/../.dockerignore"
MAKEFILE="${BATS_TEST_DIRNAME}/../Makefile"

# =============================================================================
# POSITIVE TESTS - These should PASS when optimizations are correctly implemented
# =============================================================================

@test "Dockerfile uses multi-stage build pattern" {
    # Multi-stage builds should have at least 2 FROM statements
    local from_count=$(grep -c "^FROM" "$DOCKERFILE")
    [ "$from_count" -ge 2 ]
}

@test "Dockerfile has a build stage" {
    # Should have a named build stage
    grep -q "AS build" "$DOCKERFILE"
}

@test "Dockerfile has a runtime stage" {
    # Should have a named runtime stage
    grep -q "AS runtime" "$DOCKERFILE"
}

@test "Final stage uses Alpine-based image for smaller size" {
    # The runtime stage should use an Alpine variant
    grep "AS runtime" -A 1 "$DOCKERFILE" | grep -q "alpine"
}

@test "Dockerfile uses runtime-deps base (not full SDK) for final stage" {
    # Runtime stage should use runtime-deps, not sdk
    grep "AS runtime" -A 1 "$DOCKERFILE" | grep -q "runtime-deps"
}

@test "Dockerfile copies only necessary artifacts from build stage" {
    # Should have COPY --from=build statements
    grep -q "COPY --from=build" "$DOCKERFILE"
}

@test "Dockerfile creates non-root user for security" {
    # Should create a non-root user
    grep -q "adduser" "$DOCKERFILE"
}

@test "Dockerfile runs as non-root user" {
    # Should have USER directive to switch away from root
    grep -q "^USER" "$DOCKERFILE"
}

@test "Dockerfile publishes as self-contained single file" {
    # Should use PublishSingleFile for smaller deployment
    grep -q "PublishSingleFile=true" "$DOCKERFILE"
}

@test "Dockerfile uses trimming to reduce size" {
    # Should use PublishTrimmed to reduce binary size
    grep -q "PublishTrimmed=true" "$DOCKERFILE"
}

@test ".dockerignore file exists" {
    [ -f "$DOCKERIGNORE" ]
}

@test ".dockerignore excludes .git directory" {
    grep -q "\.git" "$DOCKERIGNORE"
}

@test ".dockerignore excludes build artifacts (bin/obj)" {
    grep -q "bin/" "$DOCKERIGNORE" && grep -q "obj/" "$DOCKERIGNORE"
}

@test ".dockerignore excludes node_modules" {
    grep -q "node_modules" "$DOCKERIGNORE"
}

@test ".dockerignore excludes IDE settings" {
    grep -q "\.vscode" "$DOCKERIGNORE" || grep -q "\.idea" "$DOCKERIGNORE"
}

@test "Makefile uses docker buildx for advanced features" {
    grep -q "docker buildx build" "$MAKEFILE"
}

@test "Dockerfile cleans package cache in runtime stage" {
    # Should clean apk cache in the same layer as install
    grep "apk" "$DOCKERFILE" | grep -q "rm -rf /var/cache/apk"
}

@test "Dockerfile uses apk --no-cache flag" {
    # Should use --no-cache to avoid storing package cache
    grep -q "apk add --no-cache" "$DOCKERFILE" || grep -q "apk upgrade --no-cache" "$DOCKERFILE"
}

@test "Dockerfile sets appropriate workdir" {
    grep -q "^WORKDIR" "$DOCKERFILE"
}

@test "Dockerfile has entrypoint defined" {
    grep -q "^ENTRYPOINT" "$DOCKERFILE"
}

# =============================================================================
# NEGATIVE TESTS - These should FAIL if anti-patterns are present
# =============================================================================

@test "Dockerfile does NOT use latest tag in base image" {
    # Using :latest is an anti-pattern - should use specific versions
    ! grep "^FROM.*:latest" "$DOCKERFILE"
}

@test "Dockerfile does NOT include SDK in final stage" {
    # Final stage should NOT reference sdk image
    local runtime_line=$(grep -n "AS runtime" "$DOCKERFILE" | cut -d: -f1)
    if [ -n "$runtime_line" ]; then
        local after_runtime=$(tail -n "+$runtime_line" "$DOCKERFILE")
        ! echo "$after_runtime" | grep -q "sdk:"
    fi
}

@test "Dockerfile does NOT run apt-get without cleaning" {
    # If apt-get is used, it should be cleaned in same layer
    # This test passes if apt-get is not used OR if it cleans properly
    if grep -q "apt-get install" "$DOCKERFILE"; then
        grep "apt-get install" "$DOCKERFILE" | grep -q "apt-get clean\|rm -rf /var/lib/apt/lists"
    fi
}

@test ".dockerignore does NOT include essential source files" {
    # Should NOT ignore *.cs, *.csproj, *.sln files
    ! grep -q "^\*\.cs$" "$DOCKERIGNORE"
    ! grep -q "^\*\.csproj$" "$DOCKERIGNORE"
    ! grep -q "^\*\.sln$" "$DOCKERIGNORE"
}

@test "Dockerfile does NOT copy entire context before restore" {
    # Layer ordering: should copy project files first, then restore, then copy source
    # The pattern should NOT be: COPY . -> dotnet restore
    # It should be: COPY *.csproj -> dotnet restore -> COPY . (or selective copy)
    
    # Get line numbers for key operations
    local copy_all_line=$(grep -n "^COPY \. " "$DOCKERFILE" 2>/dev/null | head -1 | cut -d: -f1)
    local restore_line=$(grep -n "dotnet restore" "$DOCKERFILE" | head -1 | cut -d: -f1)
    
    # If both exist, restore should come before copying all source
    if [ -n "$copy_all_line" ] && [ -n "$restore_line" ]; then
        [ "$restore_line" -lt "$copy_all_line" ]
    fi
}

@test "Dockerfile does NOT expose unnecessary ports in runtime" {
    # For a CLI tool, there should be no EXPOSE statement
    ! grep -q "^EXPOSE" "$DOCKERFILE"
}
