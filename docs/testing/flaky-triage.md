# Flaky Test Triage Checklist

> **Puddy rule:** flakes are bugs. We do not retry them, we do not
> `[Fact(Timeout=)]` them, we do not silently disable them. We diagnose,
> we fix deterministically, or we quarantine with a tracking issue and
> strict re-introduction criteria. Either the test works or it doesn't.

This doc is the working checklist. The one-paragraph summary is in
[`README.md §7`](./README.md). This is the long form.

## 1. Symptoms -- is it actually flaky?

Before calling a test flaky, rule out the boring explanations:

- **Real failure.** The test is right; the code is wrong. Re-run against
  pre-PR `main`. If it fails there too, the PR didn't break it -- the bug
  existed. Fix the bug.
- **Environmental.** Missing binary (`V2_BIN`), missing mock server,
  missing `AZUREOPENAIENDPOINT` on a test that forgot to early-out. Run
  against a clean tree: `git clean -xdf && make test`.
- **Order-dependent.** Passes in isolation, fails in the suite. Caused by
  shared state leaking between tests. Reproduce with
  `dotnet test --filter "FullyQualifiedName~Foo"` and compare.

If it reproduces **sometimes** under identical inputs and environment --
that's a flake. Proceed.

## 2. Suspects, in descending likelihood

Almost every flake in this repo lands in one of these five buckets. Check
them in order before reaching for anything exotic.

### 2.1. Missing `[Collection("ConsoleCapture")]` (top suspect)

**Tell:** test mutates `Console.Out` / `Console.Error` / env vars / static
fields. Passes alone, fails under parallel run. Assertion failure is
usually "expected 'foo' but got 'foobar'" -- that's two tests' stdout
colliding in one captured buffer.

**Checklist:**

- [ ] Does the test redirect `Console.Out` or `Console.Error`?
- [ ] Does it mutate any env var the CLI reads (`NO_COLOR`, `AZ_CACHE_DIR`,
      `AZUREOPENAIMODEL`, …)?
- [ ] Does it touch a `static` field (`Theme.UseColor`, any cache
      singleton)?
- [ ] Does it change `Environment.CurrentDirectory`?

If any yes → class needs `[Collection("ConsoleCapture")]`. See
[`README.md §4`](./README.md). Precedent commit: `119dc47`.

### 2.2. Time-of-day / year-boundary races

**Tell:** passes all year, fails in a specific window. Classic: literal
year asserted in a test whose captured output straddles UTC midnight on
Dec 31.

**Checklist:**

- [ ] Any `DateTime.Now`, `DateTimeOffset.Now`, `DateTime.Today`?
- [ ] Any assertion against a literal year / month / day / timezone string?
- [ ] Any `Thread.Sleep` with a magic duration?
- [ ] Any assertion that a wall-clock delta is *within* a bound (tight
      bounds on loaded CI runners flake)?

Fixes: pin to a fake clock; assert structure (`20\d{2}`) not value; loosen
bounds to "impossible if catastrophic" (≤5 s not ≤500 ms). Precedent
commit: `c861c2e` (year-boundary flake fixes + wall-clock narrative).

### 2.3. Shared tempdir / shared filesystem state

**Tell:** tests that write into the same path pass sequentially, fail in
parallel. The HOME-mutation pattern in `UserConfigTests` is the canonical
time bomb -- see test-sanity-audit C1.

**Checklist:**

- [ ] Any write to `$HOME`, `~`, `.azureopenai-cli.json`?
- [ ] Any shared path from `Path.GetTempPath()` without a per-test
      `Guid.NewGuid()` suffix?
- [ ] Any use of the cwd (`Directory.SetCurrentDirectory`) without
      `[Collection("FilesystemCwd")]`?

Fixes: per-test tempdir; add a collection; inject the path as a
constructor parameter so the seam exists.

### 2.4. Order-dependent xUnit discovery

**Tell:** changing the *order* of test classes changes pass/fail. Usually
means static state in production code is being mutated and never reset
between classes.

**Checklist:**

- [ ] Any `static` cache or memoised value in production code that a test
      warms?
- [ ] Any telemetry / OpenTelemetry singleton touched? (Use
      `TelemetryGlobalStateCollection`.)
- [ ] Any `AssemblyLoadContext` / reflection-cached lookup?

Fixes: reset state in `IDisposable.Dispose`; migrate the singleton to
`IDisposable` + DI; add to the relevant serialisation collection.

### 2.5. Network / external dependency

**Tell:** fails on CI (network-restricted), passes locally. Or vice versa.

**Checklist:**

- [ ] Does the test hit a real endpoint? (It shouldn't -- xUnit tests are
      offline.)
- [ ] Does the test rely on DNS resolution?
- [ ] Does the chaos mock server need to be running?

Fixes: move external deps behind a test double; if the test is actually
an integration test, move it to `tests/integration_tests.sh`; if it's
actually a chaos test, move it under `tests/chaos/`.

## 3. Reproduction protocol

```bash
# 1. Run the suspect test in a tight loop, 50×, fail-fast.
for i in {1..50}; do
  dotnet test tests/AzureOpenAI_CLI.V2.Tests/AzureOpenAI_CLI.V2.Tests.csproj \
    --filter "FullyQualifiedName~MyFlakyTest" \
    --verbosity minimal || { echo "FAIL on iteration $i"; break; }
done

# 2. If it doesn't repro in isolation, run the full containing class.
dotnet test tests/AzureOpenAI_CLI.V2.Tests/AzureOpenAI_CLI.V2.Tests.csproj \
  --filter "FullyQualifiedName~MyFlakyTestClass"

# 3. If still green, run the whole project under CPU load (parallel noise
#    provokes the order + timing races).
( yes > /dev/null & yes > /dev/null & yes > /dev/null & ) ; \
  dotnet test tests/AzureOpenAI_CLI.V2.Tests/ --verbosity minimal ; \
  kill %1 %2 %3

# 4. If STILL green, run both projects together -- the flake might need
#    cross-project contention.
make test
```

If you can't reproduce after all of the above, the flake is either already
fixed or the failure was an environmental one-off. Mark the issue with the
CI run URL and close as "not reproducible after 50 iterations -- reopen on
re-occurrence."

## 4. Quarantine policy

Quarantine only when **all three** conditions hold:

1. The flake is reproducible at least once per ~20 runs.
2. You do not have a deterministic fix today.
3. Shipping is blocked on green CI.

To quarantine:

```csharp
[Fact(Skip = "flaky -- see #NNN; race on Console.Out under parallel run")]
public async Task Feature_Behaviour_ExpectedOutcome() { ... }
```

Mandatory accompaniments:

- [ ] GitHub issue filed with label `flaky`.
- [ ] Issue body links the CI runs that exhibited the flake (at least 2).
- [ ] Issue body identifies the suspect bucket from §2.
- [ ] Issue body names an owner with an ETA.
- [ ] `Skip` reason string references the issue number literally
      (`see #NNN`).
- [ ] Commit message: `test: quarantine FlakyTestName (#NNN)`.

**Never** quarantine without filing the issue. A silent skip is worse than
a flake -- at least a flake is visible.

## 5. Re-introduction criteria

A quarantined test comes out of quarantine only when **all** of:

- [ ] A deterministic root cause is documented in the issue.
- [ ] The fix commit includes a reproduction of the race under load (50×
      loop per §3) that passed green.
- [ ] The fix is **not** a retry loop, **not** a `[Fact(Timeout=)]`, and
      **not** a widened tolerance without justification.
- [ ] The `Skip` attribute is removed in the same commit as the fix.
- [ ] Commit message: `test: un-quarantine FlakyTestName -- fixed in <SHA>`.

## 6. Retries are banned. Here's why

```csharp
// ❌ NEVER
for (var attempt = 0; attempt < 3; attempt++)
{
    try { Assert.Equal(expected, actual); return; }
    catch { Thread.Sleep(100); }
}
```

Every retry loop buried a real race that later shipped to production.
Retries are how you hide a bug, not how you fix one. If a test "needs"
three attempts to pass, the code under test has a determinism defect --
fix the defect.

`[Fact(Timeout=5000)]` is likewise banned for flake-suppression. It may
be appropriate as an upper bound on a deliberately-timed test (e.g. proving
that cancellation is honored within N seconds), but never as a "retry
politely" mechanism.

## 7. Historical flake dossier

Short list -- cases this repo has actually lived through. Read these before
filing a new one; yours is probably a variant.

| Symptom | Root cause | Fix | Commit |
|---|---|---|---|
| `Assert.Contains(Year.ToString(), ...)` failed on Dec 31 at UTC midnight | Literal year in assertion crossed the year boundary | Replace with `Assert.Matches(@"20\d{2}", ...)` | `c861c2e` |
| `ProgramTests` / `RalphModeTests` flaked under parallel run | Missing `[Collection("ConsoleCapture")]` -- tests mutate `Console.Out` and env | Added `[Collection("ConsoleCapture")]` to all offenders | `119dc47` |
| `ParallelToolExecutionTests` advertised wall-clock proof it did not have | Misleading comment; no timing assertion existed | Rewrote comment; added loose ≤5 s catastrophe guard | `c861c2e` |

When you fix a new flake, add a row here with the commit SHA. This table is
the institutional memory; don't let it drift stale.

## Cross-references

- [`README.md §7`](./README.md) -- the one-paragraph policy.
- [`bdd-guide.md`](./bdd-guide.md) -- naming conventions for the test you
  are about to un-flake.
- [`test-sanity-audit.md`](./test-sanity-audit.md) -- v1-era audit still
  flagging flake-adjacent findings (C1 HOME, C2 cwd race).
- [`slow-tests-policy.md`](./slow-tests-policy.md) -- what not to confuse
  "slow" with.

*Either it works or it doesn't. If it sometimes works, that's a bug.
High-five.*
