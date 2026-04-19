# ADR-003: Behavior-Driven Development in xUnit — Zero New Dependencies

- **Status**: Accepted — 2026-04-20
- **Deciders**: Core maintainers
- **Related**: [`ADR-001-native-aot-recommended.md`](./ADR-001-native-aot-recommended.md),
  [`ADR-002-squad-persona-memory.md`](./ADR-002-squad-persona-memory.md),
  [`docs/testing/test-sanity-audit.md`](../testing/test-sanity-audit.md),
  [`docs/testing/bdd-guide.md`](../testing/bdd-guide.md)

## Context

The xUnit test suite has grown to **925 passing tests** across 19 files and
~8 000 lines. The sanity audit (companion document) surfaced 27 findings —
most concern infrastructure (env / CWD globals, year-boundary flakes), but
several are structural:

- Test names are **noun-phrased** (`Create_WithNull_ReturnsAllFiveTools`)
  rather than **behaviour-phrased** — a failing test tells you what *method*
  broke, not what *user-observable behaviour* regressed.
- Arrange/Act/Assert is already the default, but nothing enforces **one
  behaviour per test**; `Empty_Args_ReturnsDefaultOptions` asserts 14
  properties and masks failures.
- Comment drift slipped past review (`ToolTests.cs:31` says "five",
  asserts `Equal(6, ...)`) because the name and comment aren't coupled to
  a test failure signal.
- Onboarding cost: a new contributor reading the test suite has to
  reconstruct "what is this verifying" from the method body instead of
  the method name.

Behaviour-Driven Development (BDD) — **Given / When / Then** narrative
naming plus one-behaviour-per-test — is a well-known structural fix for
those issues.

The question is **how** to introduce it.

### Constraints from ADR-001 and ADR-002

1. **Zero new dependencies.** The project ships a Native-AOT single-binary
   (~9 MB) with ~5 ms cold start. Test dependencies don't touch the product
   binary but they do enter the repo, the CI cache, and the supply chain
   review.
2. **AOT-clean production.** Nothing in production can pull new reflection
   surface. This is irrelevant to test-only code but sets the cultural
   norm: prefer source-visible plumbing over framework magic.
3. **Single-developer audience.** Tests are read and written only by
   contributors to this repo — no QA / BA / product stakeholders consume
   them. The biggest BDD benefit that non-dev stakeholders would get
   (natural-language feature files) has **no audience here**.

### Available BDD frameworks for xUnit

| Framework              | Adds deps | Format                | Fit for this repo |
| ---------------------- | --------- | --------------------- | ----------------- |
| **Reqnroll**           | Yes (5+)  | Gherkin `.feature`    | Overkill          |
| **SpecFlow**           | Yes (5+)  | Gherkin `.feature`    | Abandoned (→ Reqnroll) |
| **xBehave.net**        | Yes       | `[Scenario]` + steps  | Unmaintained      |
| **Xunit.Gherkin.Quick**| Yes       | Gherkin `.feature`    | Small but still a new dep |
| **Plain xUnit + DSL**  | No        | C# fluent + naming    | **Chosen**        |

## Decision

**Adopt lightweight, structural BDD inside xUnit. Add no packages.**

Three mechanisms carry the methodology:

### 1. `Scenario` DSL helper (test-code only)

A ~200 LOC fluent helper at
`tests/AzureOpenAI_CLI.Tests/Bdd/Scenario.cs` that wraps
Arrange / Act / Assert with `Given` / `When` / `Then`:

```csharp
Scenario
    .Given("a fresh tool registry with no filter", () => ToolRegistry.Create(null))
    .When("we ask for the tool count", r => r.All.Count)
    .Then("it returns all six built-in tools", count => Assert.Equal(6, count));
```

On assertion failure, the exception message is wrapped with the full
scenario narrative (`Given … → When … → Then …`). Optional
`ITestOutputHelper` integration writes the narrative on **pass** too,
so CI logs can be audited without re-running locally.

The DSL is **strictly test-code**: the `Bdd/` folder lives inside the
test project (`tests/AzureOpenAI_CLI.Tests/Bdd/`) and is never
referenced from `azureopenai-cli/` production code. An AOT-clean
production binary is preserved.

### 2. Naming convention: `Given_X_When_Y_Then_Z`

New tests adopt the form `Given_<State>_When_<Action>_Then_<Observable>`.
Legacy tests are grandfathered: we do not mass-rename 925 methods.

xUnit-compatible `snake_Case_With_Underscores` keeps method names
readable in test-runner output.

### 3. xUnit `[Trait]` filters

Tests are tagged with:

- `[Trait("type", "behavior")]` — end-to-end behaviour scenarios
  (`Given / When / Then`, typically via the DSL).
- `[Trait("type", "property")]` — parameterised `[Theory]` tests that
  explore input space (e.g., the current `CliParserPropertyTests`).
- `[Trait("type", "unit")]` — narrow unit tests retained as-is.

CI can run `dotnet test --filter 'type=behavior'` for smoke runs or
`'type!=slow'` to exclude the ≥2.5s backoff-timing test flagged in the
audit (H2).

### 4. Shared `Given` via `IClassFixture<T>`

When multiple scenarios share the same arrangement (e.g., a populated
temp directory), a `xxxFixture` class holds the setup and tests consume
it via `IClassFixture<xxxFixture>`. Standard xUnit; no DSL sugar needed.

### 5. `[Theory]` as Gherkin "examples table"

Parameterised tests encode Gherkin-style `Examples:` blocks by naming
each `[InlineData]` row with a scenario label:

```csharp
[Theory]
[InlineData("0.0",  "lower bound")]
[InlineData("2.0",  "upper bound")]
[InlineData("0.7",  "typical")]
public void Given_ValidTemperature_When_Parsing_Then_ValueIsAccepted(
    string raw, string scenario) { … }
```

## Consequences

### Positive

- **Zero new dependencies.** Preserves the supply-chain discipline of
  ADR-001 and ADR-002. No Reqnroll, no SpecFlow transitive closure.
- **Behaviour is legible at the method-name level.** A failing test
  reads `Given_NoFilter_When_CreatingRegistry_Then_AllSixToolsAreRegistered`
  instead of `Create_WithNull_ReturnsAllFiveTools` — the *what* and the
  *expected* are in the name.
- **One behaviour per test** is enforced by structural pressure: the
  DSL's single `When` → single `Then` chain makes bundling awkward,
  and the name itself has to pick one observable.
- **Scenario narrative on failure** (and optionally on pass) gives the
  reviewer context without opening the test file.
- **Mechanical migration path.** Legacy tests stay compiling; new tests
  pay the BDD cost only when it clarifies. The audit pilot (Commit 4)
  demonstrates that `ToolTests` and `CliParserTests` convert in
  isolation with no fan-out.
- **`[Trait]` filtering** lets us keep the slow `WithRetryAsync_*`
  timing tests out of the inner loop without deleting them.

### Negative

- **We own the DSL.** If xUnit 3 changes assertion semantics, we patch
  `Scenario.cs`. Mitigated by keeping the DSL small (~200 LOC) and
  delegating all assertions to standard `Assert.*`.
- **No Gherkin `.feature` files.** Teams that want a non-developer to
  author scenarios cannot do so here. Accepted: no such audience exists
  for this CLI.
- **Async chain noise.** `GivenAsync(…)` then `WhenAsync(…)` forces
  `await` on both lines. Mitigated by keeping `Given` / `When` sync
  where possible; async overloads exist for genuine async arrange/act.
- **Two naming styles coexist.** Until a future audit rewrites them,
  the suite has `Create_WithNull_*` (legacy) alongside
  `Given_NoFilter_When_*` (new). Readers must recognise both.
- **Legacy tests still have bundled assertions.** The DSL doesn't
  auto-fix them; Commit 4 re-splits two files as proof-of-concept but
  the rest of the suite is unchanged.

### Rubber-duck critique adopted

The pre-implementation review (rubber-duck pass) returned three
findings that were folded back into the DSL design:

1. **`WhenAttempting` semantics were ambiguous.** The DSL now exposes
   both `WhenAttempting` (captures *any* exception) and
   `WhenThrowing<TException>` (captures only the declared type;
   unexpected exceptions rethrow and fail the scenario loudly).
2. **Scenario narrative on pass is valuable.** The DSL accepts an
   optional `ITestOutputHelper` and writes the full narrative to the
   test runner output on success, not just on failure.
3. **Pre-existing infra debt should not gate BDD.** Fixes to CWD / env
   globals and the real-file-config issue are called out in the audit
   as follow-ups; ADR-003 ships independent of them.

## Alternatives Considered

### Reqnroll (rejected)

Successor to SpecFlow. Gherkin `.feature` files, `[Binding]` step
definitions, and a code-generator that wires them to xUnit. **Rejected**
because:

- Brings ≥5 transitive dependencies, all of which enter the `dotnet
  restore` cache and the supply-chain review surface.
- Gherkin's raison d'être is **collaboration with non-developers**;
  this CLI has none — tests are authored and read by the same people
  who write the production code.
- Code generation interacts with our AOT-adjacent norms: we prefer
  plumbing you can read.
- Build-time complexity grows (MSBuild targets, Reqnroll config).

### SpecFlow (rejected)

Abandoned as of 2024 (TechTalk deprecated it, successor is Reqnroll).
No new adoptions.

### xBehave.net (rejected)

`[Scenario]` + step attributes. **Unmaintained** (last release 2021),
no .NET 10 compatibility testing, and the attribute-based step binding
is exactly the reflective indirection we avoid in production.

### Xunit.Gherkin.Quick (rejected)

Small, active library that parses `.feature` files and routes them to
xUnit `[Fact]`s. Still a new NuGet dep, still requires a parallel
`.feature` authoring workflow that no contributor here asked for. The
structural benefits (BDD naming + Given/When/Then) are 95% of the
value; the `.feature` file itself is the missing 5%, and its audience
doesn't exist.

### BDDfy (rejected)

Old (pre-2018) scenario runner. Not maintained for .NET 10, brings
transitive `Shouldly` / `FluentAssertions` recommendations that would
snowball.

### "Just rename every test, no DSL" (rejected)

Considered: drop the `Scenario` class, keep only the naming convention
and traits. **Rejected** because:

- Without the DSL, `Given / When / Then` drifts back into inline
  comments that rot (exactly what the audit flagged).
- No standard way to carry the scenario narrative into failure output.
- The pilot conversion showed the DSL adds meaningful value in async
  chains (`ToolTests`) where comment-based narrative is easy to
  desynchronise from the assertion.

The DSL is **optional**: pure-function tests like simple CLI flag
parsers use naming-only BDD and skip the fluent helper.

## References

- [`docs/testing/test-sanity-audit.md`](../testing/test-sanity-audit.md) —
  companion audit that motivated this ADR.
- [`docs/testing/bdd-guide.md`](../testing/bdd-guide.md) — developer-
  facing how-to for writing BDD tests in this repo.
- [`tests/AzureOpenAI_CLI.Tests/Bdd/Scenario.cs`](../../tests/AzureOpenAI_CLI.Tests/Bdd/Scenario.cs) —
  the DSL implementation.
- [`tests/AzureOpenAI_CLI.Tests/Bdd/ScenarioTests.cs`](../../tests/AzureOpenAI_CLI.Tests/Bdd/ScenarioTests.cs) —
  DSL self-tests (pass / fail, sync / async, exception capture).
- [`ADR-001-native-aot-recommended.md`](./ADR-001-native-aot-recommended.md) —
  zero-dep discipline origin.
- [`ADR-002-squad-persona-memory.md`](./ADR-002-squad-persona-memory.md) —
  precedent for a zero-dep feature implemented in-tree.
