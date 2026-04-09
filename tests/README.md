# Tests

This directory contains **301 unit tests** and **78 integration tests** for the Azure OpenAI CLI.

---

## Test Suites at a Glance

| Suite | Type | Count | Description |
|-------|------|------:|-------------|
| [`AzureOpenAI_CLI.Tests/`](AzureOpenAI_CLI.Tests/) | xUnit | 301 | Unit tests — run via `dotnet test` |
| [`integration_tests.sh`](integration_tests.sh) | Bash | 78 | End-to-end CLI tests — no Azure credentials needed |
| [`docker-image-optimization.sh`](docker-image-optimization.sh) | Bash | — | Dockerfile validation — checks build best practices |

**Total: 379 tests**

---

## Unit Test Files

| File | Tests | Description |
|------|------:|-------------|
| [`ProgramTests.cs`](AzureOpenAI_CLI.Tests/ProgramTests.cs) | — | Core CLI argument parsing, flags, exit codes |
| [`ToolTests.cs`](AzureOpenAI_CLI.Tests/ToolTests.cs) | — | Built-in tool registration, execution, edge cases |
| [`UserConfigTests.cs`](AzureOpenAI_CLI.Tests/UserConfigTests.cs) | — | Config file loading, model persistence, file permissions |
| [`SecurityToolTests.cs`](AzureOpenAI_CLI.Tests/SecurityToolTests.cs) | 104 | Security hardening: symlink traversal, DNS rebinding, command blocklist, path blocking, input sanitization |
| [`ParallelToolExecutionTests.cs`](AzureOpenAI_CLI.Tests/ParallelToolExecutionTests.cs) | 11 | Concurrent tool call execution via `Task.WhenAll` |
| [`RetryTests.cs`](AzureOpenAI_CLI.Tests/RetryTests.cs) | 36 | Retry logic, backoff, timeout behavior |
| [`StreamingAgentLoopTests.cs`](AzureOpenAI_CLI.Tests/StreamingAgentLoopTests.cs) | 21 | Streaming agent loop lifecycle, token handling, error recovery |
| [`PublishTargetTests.cs`](AzureOpenAI_CLI.Tests/PublishTargetTests.cs) | 19 | Build output validation, single-file publish, target framework |
| [`DelegateTaskToolTests.cs`](AzureOpenAI_CLI.Tests/DelegateTaskToolTests.cs) | 16 | Subagent delegation, recursion depth, input validation |
| [`RalphModeTests.cs`](AzureOpenAI_CLI.Tests/RalphModeTests.cs) | 28 | Ralph (Wiggum) mode: validation loop, iteration limits, error feedback |
| [`SecurityDocValidationTests.cs`](AzureOpenAI_CLI.Tests/SecurityDocValidationTests.cs) | 36 | Security documentation claims validation: constants, boundaries, defense-in-depth |

---

## Running Tests

### Unit Tests

```bash
# Via dotnet
dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj --verbosity minimal

# Via Make
make test
```

### Integration Tests

```bash
# Via bash (no Azure credentials needed)
bash tests/integration_tests.sh

# Via Make
make integration-test
```

### Docker Optimization Tests

```bash
# Validates Dockerfile best practices:
# multi-stage build, Alpine runtime, non-root user, single-file publish,
# .dockerignore, no :latest tags, etc.
bash tests/docker-image-optimization.sh
```

### All Tests

```bash
# Run unit + integration tests together
make all-tests
```

---

## CI Integration

Tests run automatically on every PR and push to `main` via [GitHub Actions](../.github/workflows/ci.yml):

- ✅ Unit tests (`dotnet test`)
- ✅ Integration tests (`integration_tests.sh`)
- ✅ Code formatting check (`dotnet format --verify-no-changes`)
- ✅ NuGet vulnerability audit (`dotnet list package --vulnerable`)
- ✅ Trivy container image scan (CRITICAL/HIGH)
