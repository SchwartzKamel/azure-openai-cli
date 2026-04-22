# Test Sanity Audit -- `tests/AzureOpenAI_CLI.Tests/`

- **Status**: Accepted -- 2026-04-20 *(see "Later revision" note below)*
- **Scope**: All 19 `*.cs` files under `tests/AzureOpenAI_CLI.Tests/` (925 tests, ~8k LOC)
- **Reviewer**: Puddy (via Kramer)
- **Companion**: [`ADR-003-behavior-driven-development.md`](../adr/ADR-003-behavior-driven-development.md)

> **Later revision (2026-04-22, Puddy).** The audit scope above is frozen at
> v1.x era. At the tip of `main` the xUnit surface is **1,510 tests across two
> projects**: 1,025 tests in `tests/AzureOpenAI_CLI.Tests/` (28 `*.cs`) plus
> 485 tests in `tests/AzureOpenAI_CLI.V2.Tests/` (38 `*.cs`). The findings and
> scoring below still stand for the files they reference -- they have not been
> re-audited against the expanded surface. See
> [`docs/testing/README.md`](./README.md) for the live inventory.

## Method

Every file was read and graded against six lenses:

1. **Flaky** -- races, time-dependencies, shared global state, process-global mutation
2. **Brittle** -- overspecified, hardcoded POSIX paths, wording-coupled error assertions
3. **Redundant** -- duplicate coverage across files
4. **Gaps** -- missing negative cases, missing boundaries
5. **Comment drift** -- comments / test names that lie about the code
6. **Bundled assertions** -- one test verifies multiple independent behaviors

Either it works or it doesn't. Findings cite `file:line` with a fix recommendation.

## Summary by severity

| Severity | Count |
| -------- | ----- |
| CRITICAL |   3   |
| HIGH     |   7   |
| MEDIUM   |  10   |
| LOW      |   7   |

Total: **27 findings**.

---

## CRITICAL

### C1 -- `UserConfigTests` mutates the real user config on disk
**File**: `UserConfigTests.cs:19-55`
**Symptom**: Every test deletes `~/.azureopenai-cli.json` in the ctor, backs it up to `*.test-backup`, then restores in `Dispose`. If the process is killed between delete and restore (OOM, CTRL+C, CI step timeout), the user's real config is destroyed.
**Fix**: Add a `UserConfig.GetConfigPath(string? overrideRoot)` seam; redirect the path to a per-test temp dir. Do not mutate `$HOME`.

### C2 -- `Directory.SetCurrentDirectory` races across test classes without shared `[Collection]`
**Files**: `ProgramTests.cs:466,493,521,546,584,610`; `CancellationTests.cs:113,143`
**Symptom**: `ProgramTests` is in `[Collection("ConsoleCapture")]`; `CancellationTests` declares the same collection -- good. But CWD is a **process-global** resource and xUnit runs classes from different collections **in parallel by default**. Any new test class that calls `SetCurrentDirectory` without joining the collection will race.
**Fix**: Move the lock from implicit-by-convention to explicit. Introduce `Collection("FilesystemCwd")` and mark every test that mutates CWD. Lint-guard in CI with a grep for `SetCurrentDirectory` outside the collection.

### C3 -- Environment variable mutation without collection isolation
**Files**: `DelegateTaskToolTests.cs:91,104,111,124,132,147,157,172,179,192`; `SecurityDocValidationTests.cs:49,62,69,82,155,168`; `ProgramTests.cs:1036-1060`
**Symptom**: `RALPH_DEPTH`, `AZUREOPENAIAPI`, `AZUREOPENAIENDPOINT`, `AZUREOPENAIMODEL` are process-globals. `DelegateTaskToolTests` has no `[Collection]` attribute; `SecurityDocValidationTests` has none either. Two classes running in parallel can observe each other's values.
**Fix**: `[Collection("EnvironmentVariables")]` on every class mutating env vars. Consider a test helper `using var _ = EnvVar.Set("RALPH_DEPTH", "3")` that restores in `Dispose`.

---

## HIGH

### H1 -- `DateTime.Now.Year` year-boundary flake
**Files**: `ToolTests.cs:86,112`; `ParallelToolExecutionTests.cs:63,95,205`; `ToolHardeningTests.cs:203`
**Symptom**: A test that calls the tool at 23:59:59.999 on Dec 31 reads `Year = 2025`; the tool body (running a few ms later) returns `"2026-01-01 ..."`. `Assert.Contains("2025", result)` fails.
**Fix**: Assert `Regex.IsMatch(result, @"20\d{2}")` -- the year format is what matters, not the value.

### H2 -- `RetryTests.WithRetryAsync_BackoffTimingIsExponential` sleeps ≥2.5s and asserts wall-clock
**File**: `RetryTests.cs:313-342`
**Symptom**: Timing assertion `sw.Elapsed.TotalSeconds >= 2.5` is load-bearing on a slow CI runner plus `Task.Delay` coalescing. Also a single-test 3s hit on the whole suite.
**Fix**: Inject a clock abstraction into `WithRetryAsync`; test with a virtual `TimeProvider.System` → fake provider. If that's too invasive, mark `[Trait("category","slow")]` and shrink to `>= 0.9s` with `maxRetries:1`.

### H3 -- `CancellationTests.RegisterCancelKeyPress_IsIdempotent` depends on sibling-test ordering
**File**: `CancellationTests.cs:55-72`
**Symptom**: The test asserts `Assert.Same(after1, after2)` -- which only holds if no other test in the class (or in the collection) has already registered. Relies on xUnit-per-class ordering. If a future test runs first, `after1 == the-other-test's-CTS`, not `first`.
**Fix**: Reset the static in a ctor/Dispose, or stop testing static mutation via reflection and refactor `RegisterCancelKeyPress` to accept a CTS holder.

### H4 -- 26+ hardcoded POSIX paths locked the test suite to linux-latest (ref: commit fa93cc6)
**Files**: `SecurityToolTests.cs:32-35,69,87,93-95,101-105,195,216,237,537,549-550,557`; `ToolTests.cs:147,184`; `ToolHardeningTests.cs:452,463`; `SecurityDocValidationTests.cs:380,389-390,508`; `StreamingAgentLoopTests.cs:101,153,428`
**Symptom**: `/etc/shadow`, `/bin/sh`, `/tmp/test.txt` are Linux-only. The product itself *is* POSIX-centric for these paths (see `ReadFileTool.BlockedPaths`), so the tests correctly mirror production -- **but** they cannot run on windows-latest, which is why the CI matrix dropped it.
**Fix**: Two paths forward, pick one:
  (a) **Accept the constraint** in an ADR (these tests exercise Linux-blocked-path logic; skip on Windows via `[SkippableFact]` / `[Trait("os","linux")]`). Reinstate windows-latest CI with the trait filter.
  (b) **Generalize production** to accept a runtime OS switch (Windows-blocked-paths list) and parameterize tests by `RuntimeInformation.IsOSPlatform`.
Recommendation: (a). The blocked-path list is Linux security policy, not a portable feature.

### H5 -- `ParallelToolExecutionTests_MultipleToolCalls_RunsConcurrently` claims wall-clock but never asserts it
**File**: `ParallelToolExecutionTests.cs:37-65`
**Symptom**: Comment says *"If parallel, wall-clock should be close to 200 ms"*; `sw = Stopwatch.StartNew()` is captured; `sw.Stop()` runs -- but no `Assert.True(sw.Elapsed < X)`. The test passes even if the agent loop becomes sequential.
**Fix**: Add `Assert.True(sw.Elapsed.TotalMilliseconds < 600, $"wall-clock {sw.Elapsed}")` -- needs a deterministic concurrent workload (e.g., 3 tools that each `Task.Delay(200)`). Or delete the timing narrative from the comment and leave it as a correctness-only test.

### H6 -- `PersonaUnknownName_WithSquadJson_ReturnsNonZero` has permissive assertion
**File**: `ProgramTests.cs:600-624`
**Symptom**: Comment admits *"exits with non-zero (may be 99 if creds missing, or 1 if persona check runs first)"*. Assertion is `Assert.NotEqual(0, exitCode)`. This passes even if the test regresses from "unknown-persona error" to "Azure auth failure on unrelated code path".
**Fix**: Stub Azure credentials (fake `AZUREOPENAIAPI` + endpoint) so the persona check is the only thing that can fail, then assert the specific exit code.

### H7 -- `CliParserTests.FirstError_ShortCircuits` couples to error message wording
**File**: `CliParserTests.cs:469-475`
**Symptom**: Asserts `Contains("Temperature", e.Message)`. Any wording tweak ("temperature must be...") downcases the T and breaks the test.
**Fix**: Assert the *structure* (exit code + failing flag) rather than the prose. Add a `CliParseError.Flag` property if one doesn't exist, and assert on that.

---

## MEDIUM

### M1 -- Comment + name drift: "Five" tools vs. 6 actual
**File**: `ToolTests.cs:31` (method `Create_WithNull_ReturnsAllFiveTools`) + `:35` asserts `Equal(6, ...)`
**Fix**: Rename to `Create_WithNull_ReturnsAllSixTools`. Addressed in Commit 6 (surgical fix).

### M2 -- Bundled assertions: `Empty_Args_ReturnsDefaultOptions` asserts 14 properties
**File**: `CliParserTests.cs:40-58`
**Symptom**: One behavior-under-test ("defaults when no flags given") but 14 assertions; a single failure masks the others. BDD conversion splits this up.
**Fix**: Split into per-property tests OR consolidate with `Assert.Equivalent`-style single assertion on a canonical `CliOptions` struct. Addressed in Commit 4 (pilot conversion).

### M3 -- Bundled assertions: `MultipleFlagsAndPositionals_Combined`
**File**: `CliParserTests.cs:459-466`
**Symptom**: Tests "agent mode + raw + max-rounds + positional pass-through" in one test.
**Fix**: Split into four `Given_X_When_Y_Then_Z` tests.

### M4 -- Redundant: `Schema_InvalidJson_Errors` + `Schema_EmptyString_Errors`
**File**: `CliParserTests.cs:276-287`
**Symptom**: Same error path ("Invalid JSON schema"), two tests. Both valuable as *examples* but redundant as *behaviors*. Fold into `[Theory]` with `[InlineData("{not json")]`, `[InlineData("")]`.
**Fix**: Parameterize. Addressed in Commit 4.

### M5 -- Redundant: generic-type coverage tests
**File**: `RetryTests.cs:393-425`
**Symptom**: `ReturnsCorrectGenericType_Int` / `_Bool` / `_ComplexObject` all verify the same behavior (generics pass through). Compiler verifies this at build time.
**Fix**: Keep one `ReturnsCorrectGenericType` test with a `[Theory]`, or drop entirely. Low-value coverage.

### M6 -- `CliParserPropertyTests.cs` uses hand-rolled pseudo-property-testing
**File**: `CliParserPropertyTests.cs:1-289`
**Symptom**: The name promises property-based testing, but it's 8 `[Theory]` tests with `[InlineData]` examples. Real property tests (FsCheck, Hedgehog) are excluded by zero-new-deps. That's fine -- but rename to `CliParserParameterizedTests.cs` to avoid misrepresenting the methodology.
**Fix**: Rename file, update class, add comment explaining why FsCheck isn't used.

### M7 -- Missing negative: `ReadFile_ExistingFile_ReturnsContent`
**File**: `ToolTests.cs:118-127`
**Symptom**: Asserts happy path only. Puddy rule: pass the pass, **fail the fail**. Negative pair (`does not return "Error:"`) is implicit but absent.
**Fix**: Add `Assert.DoesNotContain("Error", result)`. Addressed in Commit 4 pilot.

### M8 -- Missing coverage: `ShellExec` success path only tests `echo hello`
**File**: `ToolTests.cs:158-166`
**Symptom**: One happy path. No test for multi-word output, stderr, non-zero exit code, large output truncation, binary output.
**Fix**: Add boundary tests (noted here; not in scope for BDD commits).

### M9 -- Missing coverage: `--config` has a `show` subcommand test but no "unknown subcommand lists available subcommands"
**File**: `CliParserTests.cs:181-196`
**Symptom**: Tests that unknown sub errors, but doesn't test that the error message enumerates valid subcommands. If UX regresses silently, the test won't notice.
**Fix**: Assert `Contains("show", e.Message)`. Low urgency.

### M10 -- `UserConfigTests` uses a 100ms `Thread.Sleep` for retry
**File**: `UserConfigTests.cs:52`
**Symptom**: Retry-with-sleep in `Dispose` to dodge transient file locks. Symptom of C1. If the sleep is wrong, tests leak state between runs.
**Fix**: Eliminate with C1 fix (isolated path → no contention).

---

## LOW

### L1 -- Test names are noun-phrased, not behavior-phrased
**All files except `CliParserPropertyTests.cs`**
**Symptom**: `Create_WithNull_ReturnsAllSixTools` reads as "what the method does"; BDD form reads as "what the user observes": `Given_NoFilter_When_CreatingRegistry_Then_AllSixToolsAreRegistered`.
**Fix**: Gradual -- Commit 4 (pilot) demonstrates; full conversion is a non-goal.

### L2 -- Ambiguous test method name `Main_NoArgs_ReturnsExitCode1`
**File**: `ProgramTests.cs:34-45`
**Symptom**: The *why* ("no prompt given → usage printed") is hidden. Name restates the API signature.
**Fix**: `Given_NoArguments_When_RunningMain_Then_ExitsWith1AndShowsUsage`.

### L3 -- `Ralph_Flag_ImpliesAgentMode` bundles two assertions
**File**: `CliParserTests.cs:207-213`
**Symptom**: Verifies `RalphMode == true` **and** `AgentMode == true`. Technically two behaviors (flag-set, flag-implies-other-flag). Defensible: "Ralph implies Agent" is one behavior.
**Fix**: Keep as-is but rename to `Given_RalphFlag_When_Parsing_Then_BothAgentAndRalphAreTrue`.

### L4 -- `[Theory]` with `[InlineData]` arrays missing Gherkin-style scenario labels
**Files**: Throughout
**Symptom**: `[InlineData("0.0")]` gives xUnit only the value, not the scenario name ("lower bound"). Failure output is less helpful.
**Fix**: Use `MemberData` with named tuples or add a `scenario` parameter: `[InlineData("0.0", "lower bound")]`.

### L5 -- `try { Directory.Delete(...); } catch { }` everywhere
**Files**: `ToolTests.cs:25`, `SquadTests.cs:21`, `ParallelToolExecutionTests.cs:31`, more
**Symptom**: Silent swallow hides leaked-file situations in CI.
**Fix**: Log to `ITestOutputHelper` on catch.

### L6 -- `JsonSourceGeneratorTests` tests the generator, not the codebase's use of it
**File**: `JsonSourceGeneratorTests.cs` (507 lines, 23 tests)
**Symptom**: Many tests round-trip canonical JSON through `AppJsonContext`. Valuable regression coverage, but several tests are effectively testing `System.Text.Json` itself (e.g., "serializes int fields"). Grandfather-in.
**Fix**: None. Documented for posterity.

### L7 -- `SecurityDocValidationTests` validates `SECURITY.md` prose by `Assert.Contains("/bin/sh")`
**File**: `SecurityDocValidationTests.cs:380-390`
**Symptom**: Couples doc text to test assertions. Rewording the doc breaks tests. Mitigation: the doc is also security-sensitive, so the coupling is intentional.
**Fix**: Keep; note the trade-off in the test file's summary comment.

---

## What this audit does **not** fix

Commit 6 only lands the surgical CRITICAL/HIGH drifts that don't require an architectural refactor:
- M1 comment-drift (`ToolTests.cs:31` Five→Six)
- H5 missing timing assertion in `ParallelToolExecutionTests.cs:37` (or narrative strip)
- H1 year-boundary regex hardening in the six sites listed

C1, C2, C3, H2, H3, H6, H7 require larger refactors (config-path injection, clock abstraction, CTS-holder refactor, collection-attribute rollout) and are tracked as follow-ups. BDD adoption (ADR-003) does **not** depend on those fixes -- but the BDD guide will flag each anti-pattern so new tests don't reproduce them.

## Follow-ups (issues to file)

1. `[infra] Isolate UserConfigTests from ~/.azureopenai-cli.json` (C1)
2. `[infra] Mark all env/CWD-mutating test classes with shared [Collection]` (C2, C3)
3. `[infra] Inject clock into WithRetryAsync` (H2)
4. `[infra] Restore windows-latest CI matrix with [SkippableFact] on POSIX-only tests` (H4)
5. `[infra] Stub Azure credentials in ProgramTests for deterministic exit codes` (H6)

---

*Either it works or it doesn't. High-five.*
