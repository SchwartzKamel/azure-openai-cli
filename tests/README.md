# Docker Image Optimization Tests

This directory contains tests that validate Docker image build optimizations as described in [docs/proposals/improve-docker-image-speed.md](/docs/proposals/improve-docker-image-speed.md).

## Test Philosophy: "Pass the pass, fail the fail"

The tests are organized into two categories:

### Positive Tests
These validate that optimizations **ARE** correctly implemented:
- Multi-stage build pattern
- Alpine-based runtime image
- Non-root user security
- Single file publishing with trimming
- Proper .dockerignore configuration
- BuildKit usage in Makefile
- Package cache cleanup

### Negative Tests
These validate that anti-patterns are **NOT** present:
- No `:latest` tags in base images
- No SDK in final stage
- No uncleaned package manager runs
- Essential source files not ignored
- Proper layer ordering for cache efficiency
- No unnecessary port exposure

## Running Tests

### Using Make (Recommended)
```bash
make test-docker-optimization
```

### Using Bats directly
```bash
bats tests/docker-image-optimization.bats
```

### Using Shell script (if bats is not available)
```bash
./tests/docker-image-optimization.sh
```

## Test Files

- `docker-image-optimization.bats` - Bats test file (requires bats)
- `docker-image-optimization.sh` - Shell script version (no dependencies)

## Expected Output

All 26 tests should pass when Docker image optimizations are correctly implemented:

```
ok 1 Dockerfile uses multi-stage build pattern
ok 2 Dockerfile has a build stage
ok 3 Dockerfile has a runtime stage
...
ok 26 Dockerfile does NOT expose unnecessary ports in runtime
```
