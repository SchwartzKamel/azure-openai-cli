# Testing Playbook

> **Either it works or it doesn't.** This is the live, executable reference
> for how we test `azure-openai-cli`. If something here disagrees with the
> code, the code is right and this doc is wrong — file an issue.

Companion docs:

- [`bdd-guide.md`](./bdd-guide.md) — naming, Given/When/Then structure, one
  behaviour per test.
- [`test-sanity-audit.md`](./test-sanity-audit.md) — v1-era audit findings
  (scope frozen at 925 tests, still useful for the files it covers).
- [`../adr/ADR-003-behavior-driven-development.md`](../adr/ADR-003-behavior-driven-development.md)
  — the BDD decision record.
- [`../../tests/README.md`](../../tests/README.md) — directory-level index
  of the test trees.
- [`../../tests/chaos/README.md`](../../tests/chaos/README.md) — chaos /
  failure-injection harness.

---

## 1. Test inventory

Numbers measured at tip of `main`. `+` absorbs PRs in flight.

| Tree | Location | Type | Count |
|---|---|---|---:|
| v1 xUnit | `tests/AzureOpenAI_CLI.Tests/` (28 `*.cs`) | unit + contract + property + security | **1,025** |
| v2 xUnit | `tests/AzureOpenAI_CLI.V2.Tests/` (38+ `*.cs`) | unit + contract + regression | **490+** |
| Integration | `tests/integration_tests.sh` | bash, end-to-end binary exec | ~174 assertions |
| Docker | `tests/docker-image-optimization.sh` | bash, Dockerfile lint | — |
| Chaos | `tests/chaos/*.sh` | bash, failure injection | 11 scenarios |
| **xUnit total** | | | **1,510+** |

Rough category breakdown (xUnit, both trees combined):

| Category | Examples | Rationale |
|---|---|---|
| Unit | `ToolTests.cs`, `UserConfigTests.cs`, `CostEstimatorTests.cs` | Pure logic, no I/O beyond tempdirs. |
| Contract | `RalphExitCodeTests.cs`, `ToolHardeningTests.cs`, `RawModeTests.cs`, `VersionContractTests.cs` | Gate user-visible promises — exit codes, `--raw` stderr discipline, `--version` strings. |
| Regression | `SecurityRegressionTests.cs`, `ErrorAndExitTests.cs` | Pin past bugs so they stay dead. |
| Property | `CliParserPropertyTests.cs` | Hand-rolled property-style coverage (no FsCheck — see §10). |
| Security | `SecurityToolTests.cs`, `SecurityDocValidationTests.cs` | SSRF, symlink traversal, DNS rebinding, doc-claim truth-ness. |
| Observability | `ObservabilityTests.cs`, `PrewarmTests.cs` | Telemetry, cache warm-up. |

---

## 2. Running tests

Four supported entry points — pick the one that matches your loop:

```bash
# 1. Canonical: runs BOTH v1 and v2 projects via the solution file.
#    This is what CI runs. `make test` and the solution form are equivalent.
make test
dotnet test azure-openai-cli.sln --verbosity minimal

# 2. v1 only — faster loop when working inside tests/AzureOpenAI_CLI.Tests/.
#    NEVER ship on v1-only green; CI runs both.
make test-v1
dotnet test tests/AzureOpenAI_CLI.Tests/AzureOpenAI_CLI.Tests.csproj --verbosity minimal

# 3. v2 only — same deal, for tests/AzureOpenAI_CLI.V2.Tests/.
make test-v2
dotnet test tests/AzureOpenAI_CLI.V2.Tests/AzureOpenAI_CLI.V2.Tests.csproj --verbosity minimal

# 4. Single test or filter — for really tight debug loops.
dotnet test tests/AzureOpenAI_CLI.V2.Tests/AzureOpenAI_CLI.V2.Tests.csproj \
  --filter "FullyQualifiedName~RalphExitCode"
```

Integration + docker + chaos have their own entry points:

```bash
make integration-test      # bash tests/integration_tests.sh
make docker-test           # bash tests/docker-image-optimization.sh
bash tests/chaos/run_all.sh
```

Full preflight gate (format + color-contract + build + unit + integration):

```bash
make preflight
```

---

## 3. TDD workflow

The loop for any behavioural change:

```bash
# 1. Write the failing test first (v2 example).
$EDITOR tests/AzureOpenAI_CLI.V2.Tests/MyFeatureTests.cs

# 2. Confirm RED — test exists and fails for the right reason.
make test-v2
# Expect: Failed: 1, Passed: ...

# 3. Implement in azureopenai-cli-v2/.
$EDITOR azureopenai-cli-v2/MyFeature.cs

# 4. Confirm GREEN on the tight loop.
make test-v2

# 5. Confirm GREEN on the full surface BEFORE commit.
make test
# Expect: Failed: 0 on BOTH projects.

# 6. Commit with Conventional Commits prefix.
git commit -m "test: cover MyFeature edge case X"    # test-only
git commit -m "feat: implement MyFeature"            # behaviour-changing
git commit -m "fix: MyFeature regression on empty input"
```

**Bug fix rule (non-negotiable):** every bug fix ships with the regression
test that would have caught it. The test lands in the same commit as the
fix, or in the immediately preceding commit.

### 3.1. Worked example — RED → GREEN → REFACTOR

A real case from this repo: the **year-boundary flake** (commit `c861c2e`,
2026-04-19; audit H1 follow-up).

**Context.** Several tests asserted captured CLI output contained
`DateTime.Now.Year.ToString()`. On a test machine running straddling UTC
midnight on Dec 31, the `get_datetime` tool wrote the old year into
stdout and the assertion computed the new year — guaranteed failure once
a year, impossible to reproduce the other 364.

**RED — the failing test.** Reproducible with a clock stub fixed to
`2025-12-31T23:59:59.900Z` (the pre-fix test, paraphrased):

```csharp
// tests/AzureOpenAI_CLI.Tests/ToolHardeningTests.cs:203 (pre-fix)
var output = await CaptureGetDateTimeOutput();
Assert.Contains(DateTime.Now.Year.ToString(), output);
// ❌ output contains "2025" (tool was called at 23:59:59.900Z)
// ❌ DateTime.Now.Year is 2026 by the time the assertion runs
// Failed: expected "2026" in "...2025-12-31T23:59:59Z...".
```

The test proves the race exists — `Assert.Equal(expected, actual)` is the
right shape, the literal being asserted is wrong.

**GREEN — the simplest code that makes the test pass.** Replace the
year-literal coupling with a structural match that cannot straddle a
boundary:

```csharp
// tests/AzureOpenAI_CLI.Tests/ToolHardeningTests.cs:203 (post-fix)
var output = await CaptureGetDateTimeOutput();
Assert.Matches(@"20\d{2}", output);
// ✅ matches any four-digit year in the 2000s range.
// The test now asserts STRUCTURE (ISO year field present), not VALUE.
```

That's it. No clock abstraction, no `IDateTimeProvider` introduced, no
production code touched. The simplest thing that turns the test green.

**REFACTOR — tighten the narrative, not the assertion.** With both passing,
look for duplicated flakes and for drift in surrounding comments:

- Three more sites in `ParallelToolExecutionTests.cs` had the same literal
  coupling → apply the same `20\d{2}` regex.
- `ParallelToolExecutionTests:37` advertised a wall-clock timing claim
  ("wall-clock should be close to 200 ms") that no assertion enforced →
  rewrote the comment to state the real invariant (`Task.WhenAll` returns
  all results intact) and added a loose ≤5 s catastrophe guard.
- The commit message calls out the category (year-boundary flake) so the
  next auditor finds the pattern by `git log --grep`.

Net diff: four assertion replacements + one loosened bound + one rewritten
comment. No retries. No `[Fact(Timeout=)]`. No new test infrastructure.
The race is gone because the assertion no longer cares about the
transient value that caused it.

**Takeaways for future TDD loops on this codebase:**

1. RED first. A test that never failed isn't evidence of anything.
2. GREEN minimum. The smallest change that moves the bar from red to
   green. No refactor yet.
3. REFACTOR with the safety net. Once green, grep for the same bug
   pattern, fix the sisters, and update surrounding narratives to match.
4. One commit per phase is ideal; bundling is fine if the diff stays
   readable and the commit message names all three phases.

For flaky-adjacent cases, also walk the
[`flaky-triage.md`](./flaky-triage.md) checklist before shipping the fix
— the same race may hide in three more files you haven't looked at yet.

---

## 4. `ConsoleCapture` collection

Some xUnit tests swap `Console.Out` / `Console.Error` / environment
variables to assert on output. Those mutations are process-global — if two
such tests run in parallel, their stdout streams collide and assertions
flake.

Invariant (enforced by `tests/AzureOpenAI_CLI.V2.Tests/ConsoleCaptureCollection.cs`):

```csharp
[CollectionDefinition("ConsoleCapture", DisableParallelization = true)]
public class ConsoleCaptureCollection { }
```

Any test class that does **any** of the following MUST carry the
`[Collection("ConsoleCapture")]` attribute:

- Redirects `Console.Out` or `Console.Error`.
- Mutates environment variables the CLI reads (e.g. `NO_COLOR`,
  `AZ_CACHE_DIR`, `AZUREOPENAIMODEL`).
- Changes `Environment.CurrentDirectory` or reads/writes a shared tempdir
  path baked into a global.
- Touches a `static` field mutated at runtime (e.g. `Theme.UseColor`).

Existing membership (non-exhaustive) — treat as the reference set:

```
tests/AzureOpenAI_CLI.V2.Tests/
  CostEstimatorTests.cs
  ErrorAndExitTests.cs
  PrewarmTests.cs
  PromptCacheTests.cs
  RalphExitCodeTests.cs
  RalphModeTests.cs
  UserConfigQuietTests.cs
  V2FlagParityTests.cs
```

Parallel-safe test classes (no global mutation) must NOT join this
collection — that needlessly serialises the suite and makes CI slower.

A separate collection, `TelemetryGlobalStateCollection`, exists for the
same reason but scoped to `OpenTelemetry` singleton state. Don't cross the
streams — a class joins exactly one serialization collection.

---

## 5. Contract tests

Contract tests gate **user-visible promises**. They fail loudly if a
public interface drifts. Reviewers: if a PR touches any of these, the
commit message must call out whether the drift is intentional.

| Contract | File | Promise |
|---|---|---|
| Ralph exit codes | `tests/AzureOpenAI_CLI.V2.Tests/RalphExitCodeTests.cs` | `az-ai-v2 --ralph` exit codes stay stable per spec (0/1/2/…); Espanso / CI scripts depend on these. |
| `--raw` stderr discipline | `tests/AzureOpenAI_CLI.V2.Tests/RawModeTests.cs` | `--raw` writes ONLY model content to stdout; banners, prompts, and warnings go to stderr (or are suppressed). Espanso / AHK pipes would break otherwise. |
| Tool hardening | `tests/AzureOpenAI_CLI.Tests/ToolHardeningTests.cs` | Built-in tools: `TryGetProperty` is the parse pattern (no throws on missing fields); SSRF redirects are blocked; missing params yield a structured error, not a crash. |
| `--version` strings | `tests/AzureOpenAI_CLI.V2.Tests/VersionContractTests.cs` | `Program.VersionSemver` / `Program.VersionFull` / `Telemetry.ServiceVersion` all agree with the csproj `<Version>`. Catches the v2.0.2→v2.0.4 hardcoded-version-drift class of bug. |
| Security doc claims | `tests/AzureOpenAI_CLI.Tests/SecurityDocValidationTests.cs` | Constants in `SECURITY.md` match constants in code (blocklist size, bounded recursion depth, etc.). The doc can't lie. |

Adding a contract test is the right move whenever a PR introduces a
user-facing promise (flag, exit code, output format, env-var semantics).

---

## 6. Integration tests

Script: `tests/integration_tests.sh`. Bash, `set -euo pipefail`, exercises
the compiled binary end-to-end.

### Prerequisites

```bash
# Build the binaries first — the script runs the artifacts, not `dotnet run`
# (except for the v1 legacy path which still uses `dotnet run`).
make dotnet-build          # or: dotnet build azure-openai-cli.sln -c Release
```

### Environment variables

Most tests need no credentials. A handful exercise the real Azure code
path and are skipped if creds are missing:

| Variable | Required for | Default |
|---|---|---|
| `V1_BIN` | locate legacy binary | `./azureopenai-cli/bin/Release/net10.0/AzureOpenAI_CLI` |
| `V2_BIN` | locate v2 binary | `./azureopenai-cli-v2/bin/Release/net10.0/az-ai-v2` |
| `AZUREOPENAIENDPOINT` | live-endpoint smoke tests | — (skipped if unset) |
| `AZUREOPENAIAPIKEY` | live-endpoint smoke tests | — (skipped if unset) |

### Running

```bash
make integration-test
# or
bash tests/integration_tests.sh
```

Output is `✓ PASS`, `✗ FAIL`, `⊘ SKIP`. Exit code is non-zero iff any
assertion failed — skips are not failures.

### When to add an integration test

Add one when:

- You're wiring up a new CLI flag or exit-code path that a script will
  depend on (e.g. Espanso).
- You've fixed a bug whose root cause was outside the xUnit process model
  (process exit, signal handling, argv parsing quirks, env-var
  inheritance).
- An xUnit test would need too much mocking to be honest.

Do **not** add an integration test when a fast xUnit test can cover the
same path — integration is slower, less debuggable, and more
environment-dependent.

### What `integration_tests.sh` covers (and doesn't)

Live snapshot; refresh after any integration-facing PR.

| Covered | Not covered (by design) | Not covered (gap) |
|---|---|---|
| `--version` / `--version --short` / `--help` output shape | Live Azure endpoint calls (skipped without creds) | `ShellExec` adversarial exit-code matrix (only echo-hello happy path) |
| Exit-code contract for common flags (`--raw`, `--ralph`, unknown flag) | Windows path semantics (CI is Linux-only) | Ralph validator JSON tool-call shape |
| `--raw` stdout cleanliness (no banners leak to stdout) | xUnit-unit-testable pure logic (argv parse, config merge) | 429 `Retry-After` end-to-end replay |
| env-var precedence (`AZUREOPENAIMODEL`, `NO_COLOR`) | Multi-process concurrency | stdin multi-MB streaming end-to-end |
| v1 vs v2 binary parity where promised (`V2FlagParity` mirror) | Anything needing a running mock server (chaos territory) | `UserConfig` rewrite atomicity under crash |
| Cache-dir and `.squad/` file layout basics | AOT-specific failure modes (covered by `PublishTargetTests`) | Locale / LC_ALL-driven output drift |

### When to add a row to `integration_tests.sh` vs a new xUnit test

Add to `integration_tests.sh` when **any** of:

- The promise is about the **process boundary** — exit code, signal,
  real argv, real env-var inheritance, stdin/stdout bytes verbatim.
- An xUnit test would need so much mocking that it stops proving the
  real behaviour (e.g. faking `Environment.Exit`).
- A downstream script (Espanso, shell wrapper, Homebrew formula test)
  will exec the binary the same way — the integration test mirrors that
  usage.

Add an **xUnit** test instead when:

- The logic is a pure function or a class in isolation.
- You need to assert internal state, thrown exceptions, or multiple
  behaviours cheaply per case.
- You want sub-second feedback on a tight loop.

Add **both** when a user-visible promise has both a process-level
manifestation and an internal invariant — the xUnit test pins the
invariant, the integration test pins the wire format. `--version` is the
canonical example: `VersionContractTests` asserts the internal strings
match the csproj; `integration_tests.sh` asserts `az-ai-v2 --version`
exits 0 and prints the expected shape.

---

## 7. Flaky test policy

Flakes are bugs. We do not paper over them with retries.

**Triage order (strict):**

1. **Diagnose.** Re-run the failing test 10× in isolation. If it flakes,
   reproduce the race. Common causes here: missing `ConsoleCapture`
   membership (§4), shared tempdir, year-boundary math, unstable stdout
   ordering under `WriteLineAsync`.
2. **Fix deterministically.** Pin the clock, isolate the tempdir, add to
   `ConsoleCapture`, remove the race. A deterministic fix always beats a
   retry loop.
3. **If you cannot fix it today:** quarantine. File an issue labelled
   `flaky` linking the failing CI runs, and mark the test with a `Skip`
   reason that references the issue number:

   ```csharp
   [Fact(Skip = "flaky — see #NNN; race on Console.Out under parallel run")]
   public async Task Feature_Behaviour_ExpectedOutcome() { ... }
   ```

4. **Never** wrap the test body in a retry loop. Never `[Fact(Timeout=)]`
   your way out of a race. Never disable the test silently.

### `[Trait]` status (honesty note)

`docs/testing/bdd-guide.md` references `[Trait("type", "slow")]` and a
`[Trait("flaky", "true")]` quarantine lane. Both are **aspirational** —
no tests currently carry either trait. Treat these as a design target,
not a live filter. If you add a trait, also add the CI wiring to honour
it; otherwise it's decorative.

---

## 8. Coverage

We do not currently gate on coverage numbers. `dotnet test` can emit
Cobertura if you want a one-off measurement:

```bash
dotnet test azure-openai-cli.sln \
  --collect:"XPlat Code Coverage" \
  --results-directory ./coverage
```

Coverage is a signal, not a target. Coverage of the **risky** paths
(security tools, Ralph loop, retry backoff, config merging, exit-code
contracts) matters. Coverage of trivial getters does not.

If you're adding a test purely to lift a coverage percentage, stop and
ask whether the test would catch a real regression. If not, don't write
it.

---

## 9. Chaos tests

Location: `tests/chaos/`. Bash scenarios that inject failures into the
built binary and assert it degrades cleanly rather than crashing.

Scenarios (see `tests/chaos/README.md` for the full catalog):

```
01_argv_injection.sh   05_squad_chaos.sh     09_network_chaos.sh
02_stdin_evil.sh       06_persona_memory.sh  10_ralph_depth.sh
03_env_chaos.sh        07_tool_chaos.sh      11_persona_live.sh
04_config_chaos.sh     08_signal_chaos.sh
```

Run all:

```bash
bash tests/chaos/run_all.sh
```

Chaos tests are not part of `make preflight` — they're slower and some
need a running mock server (`tests/chaos/mock_server.py`). Run them
before cutting a release and whenever you touch process-level concerns
(signals, subprocess, network, env parsing).

---

## 10. Property tests

We have one hand-rolled property-style test file:
`tests/AzureOpenAI_CLI.Tests/CliParserPropertyTests.cs`. It uses a
seeded `Random` and a generator loop — **not** FsCheck. This is
deliberate: ADR-003 bans new test dependencies, and the argv parser's
state space is small enough that a seeded generator is enough.

Good candidates for property-style coverage:

- Input parsing (argv, config keys, env-var names).
- Commutative / idempotent operations (config merge, theme resolution).
- Invariants (output never exceeds N chars, exit code always in a known
  set, `--raw` stdout never contains ANSI).

If you're reaching for FsCheck or similar, open an ADR first — the
dependency cost must be justified against the AOT / supply-chain budget
(ADR-001).

---

## 11. PR review checklist (for anyone reviewing a test change)

- [ ] Does the test name describe a **behaviour**, not a method?
      (`Ralph_ExitsNonZero_OnValidationFailure`, not `TestRalph1`.)
- [ ] One behaviour per `[Fact]`? No bundled assertions.
- [ ] If the test mutates `Console.*` or env vars, is it in the
      `ConsoleCapture` collection?
- [ ] If this is a bug fix, is there a regression test in the same
      commit, and does it fail against the pre-fix code?
- [ ] If this adds a user-visible promise, is there a contract test
      under §5?
- [ ] Does the test run deterministically? (No `Thread.Sleep`, no
      unseeded randomness, no wall-clock dependencies without a clock
      abstraction.)
- [ ] Does `make test` still pass on both v1 and v2?

High-five.
