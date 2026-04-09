# Tests

This directory contains test suites for the Azure OpenAI CLI.

## Test Suites

| File | Type | Description |
|------|------|-------------|
| `AzureOpenAI_CLI.Tests/` | xUnit | Unit tests (34 tests) — run via `dotnet test` |
| `integration_tests.sh` | Bash | End-to-end CLI tests (51 tests) — no Azure creds needed |
| `docker-image-optimization.sh` | Bash | Dockerfile validation tests — checks build best practices |

## Running Tests

```bash
# Unit tests
dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj --verbosity minimal

# Integration tests
bash tests/integration_tests.sh

# Docker optimization tests
bash tests/docker-image-optimization.sh

# Or use Make
make test                # unit tests
make integration-test    # integration tests
```

## Docker Optimization Tests

Validates Dockerfile best practices (multi-stage build, Alpine runtime, non-root user, single-file publish, .dockerignore, no `:latest` tags, etc.).
