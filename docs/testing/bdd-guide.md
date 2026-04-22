# BDD Developer Guide

How to write behaviour-driven tests in this repo. See
[ADR-003](../adr/ADR-003-behavior-driven-development.md) for the
*why*; this file is the *how*.

## TL;DR

1. Name tests `Given_<State>_When_<Action>_Then_<Observable>()`.
2. Assert **one observable behaviour** per test.
3. Use the `Scenario` DSL for async chains or where narrative clarifies;
   skip it for one-liners.
4. Tag the class or method with a `[Trait]`: `behavior`, `property`, or `unit`.
5. Every test asserts a positive AND a negative condition when feasible.
6. Zero new NuGet deps. Ever.

## 1. Naming

### The rule

```csharp
public void Given_<InitialState>_When_<Action>_Then_<ExpectedObservation>()
```

### Examples

| Bad (noun-focused)                     | Good (behaviour-focused)                                          |
| -------------------------------------- | ----------------------------------------------------------------- |
| `Create_WithNull_ReturnsAllFiveTools`  | `Given_NoFilter_When_CreatingRegistry_Then_AllSixToolsAreRegistered` |
| `ParseTemperature_OutOfRange`          | `Given_TemperatureAboveTwo_When_Parsing_Then_RangeErrorIsReturned`   |
| `Main_NoArgs_ReturnsExitCode1`         | `Given_NoArguments_When_RunningMain_Then_ExitsWith1AndShowsUsage`    |

### Why it matters

A failing test in CI shows only the method name. Readers shouldn't have
to open the file to learn *what regressed* -- the name carries the
expected behaviour.

## 2. One behaviour per test

### Good

```csharp
[Fact]
public void Given_NoArgs_When_Parsing_Then_TemperatureIsNull() =>
    Assert.Null(ParseOk().Temperature);

[Fact]
public void Given_NoArgs_When_Parsing_Then_MaxTokensIsNull() =>
    Assert.Null(ParseOk().MaxTokens);
```

### Bad

```csharp
[Fact]
public void Empty_Args_ReturnsDefaultOptions()
{
    var o = ParseOk();
    Assert.Null(o.Temperature);
    Assert.Null(o.MaxTokens);
    Assert.Null(o.SystemPrompt);
    Assert.False(o.ShowConfig);
    // … 10 more assertions
}
```

When `SystemPrompt` regresses to empty-string, the test should say
`Given_NoArgs_When_Parsing_Then_SystemPromptIsNull` failed -- not
"Empty_Args_ReturnsDefaultOptions failed on assertion #3".

### Acceptable bundling

Closely-related invariants that form a single *semantic* observation
may stay in one test:

```csharp
[Fact]
public void Given_UnknownToolName_When_Executing_Then_ErrorMessageNamesTheTool()
{
    var registry = ToolRegistry.Create(null);
    var result = await registry.ExecuteAsync("nonexistent_tool", "{}", CancellationToken.None);

    Assert.Contains("unknown tool", result);     // kind of error
    Assert.Contains("nonexistent_tool", result); // which tool
}
```

Both assertions verify the same observable ("the error message names the
offending tool"). This is a judgement call; err on the side of splitting.

## 3. The Scenario DSL

The DSL wraps Arrange/Act/Assert with narrative descriptions. On
failure, the exception message includes the full scenario; on pass
(with `ITestOutputHelper`) it writes the narrative to test output.

### Sync

```csharp
using AzureOpenAI_CLI.Tests.Bdd;

[Fact]
public void Given_CommaSeparatedTools_When_Parsing_Then_AllAreStoredCaseInsensitive()
{
    Scenario
        .Given("the --tools list 'shell,FILE, web'",
            () => new[] { "--tools", "shell,FILE, web" })
        .When("parsing flags", args => ParseOk(args).EnabledTools!)
        .Then("three entries are present", t => Assert.Equal(3, t.Count))
        .And("'shell' is present",         t => Assert.Contains("shell", t))
        .And("'file' matches case-insensitively", t => Assert.Contains("file", t));
}
```

### Async

```csharp
[Fact]
public async Task Given_NoTimezone_When_CallingGetDateTime_Then_ACurrentYearStringIsReturned()
{
    var ctx = await Scenario
        .Given("a fresh datetime tool", () => new GetDateTimeTool())
        .WhenAsync("executing it with empty args",
            tool => tool.ExecuteAsync(
                JsonDocument.Parse("{}").RootElement, CancellationToken.None));

    ctx.Then("the result contains a 20xx year",
            r => Assert.Matches(YearPattern, r))
       .And("the result does NOT start with 'Error:'",
            r => Assert.DoesNotContain("Error", r));
}
```

### Expected exceptions

Prefer `WhenThrowing<T>` (typed, rethrows wrong types loudly) over
`WhenAttempting` (catches anything):

```csharp
Scenario
    .Given("null input", () => (string?)null)
    .WhenThrowing<ArgumentNullException>(
        "calling ArgumentNullException.ThrowIfNull",
        s => ArgumentNullException.ThrowIfNull(s))
    .Then("the typed exception was captured", ex => Assert.NotNull(ex));
```

If the act throws `InvalidOperationException` instead of
`ArgumentNullException`, `WhenThrowing<ArgumentNullException>` fails
the scenario with the full narrative -- no silent pass.

### When NOT to use the DSL

For one-liner tests, the DSL adds ceremony without clarity:

```csharp
// Good -- no DSL needed
[Fact]
public void Given_RawFlag_When_Parsing_Then_RawIsTrue() =>
    Assert.True(ParseOk("--raw").Raw);
```

The Given/When/Then is visible in the **method name**; the body is so
short that the narrative wrapper would obscure rather than reveal.

Use the DSL when:

- The chain is async (narrative failure messages are especially
  valuable when await stack traces are noisy).
- The test has 3+ assertions that form a coherent scenario.
- You want to narrate intermediate states (`.And("…")`).

## 4. `[Trait]` filtering

Tag each class or test method:

```csharp
[Trait("type", "behavior")]  // end-to-end scenarios via the DSL
[Trait("type", "property")]  // parameterised [Theory] tests
[Trait("type", "unit")]      // narrow / legacy / reflection tests
[Trait("type", "slow")]      // >500ms -- see audit H2
```

CI usage:

```bash
dotnet test --filter 'type=behavior'        # fast feedback
dotnet test --filter 'type!=slow'           # inner loop
dotnet test --filter 'type=property'        # parameterised only
```

## 5. `[Theory]` as a Gherkin examples table

For property-style tests, add a `scenario` label to each `[InlineData]`
so failures name the case rather than the raw value:

```csharp
[Theory]
[InlineData("0.0",  "lower bound")]
[InlineData("2.0",  "upper bound")]
[InlineData("0.7",  "typical value")]
[Trait("type", "property")]
public void Given_ValidTemperature_When_Parsing_Then_ValueIsStored(
    string raw, string scenario)
{
    _ = scenario; // narrative only; xUnit test name includes it
    Assert.Equal(
        float.Parse(raw, CultureInfo.InvariantCulture),
        ParseOk("--temperature", raw).Temperature);
}
```

The scenario string is unused inside the body; its job is to label
test-runner output (`…Then_ValueIsStored(raw: "0.0", scenario: "lower bound")`).

## 6. Shared `Given` via `IClassFixture<T>`

When multiple scenarios in one class share arrange cost (temp dir,
parsed fixture, loaded config), wrap it in an `IClassFixture`:

```csharp
public sealed class PopulatedTempDirFixture : IDisposable
{
    public string Path { get; }
    public PopulatedTempDirFixture()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"shared-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
        File.WriteAllText(System.IO.Path.Combine(Path, "seed.txt"), "seed");
    }
    public void Dispose()
        => Directory.Delete(Path, recursive: true);
}

[Trait("type", "behavior")]
public class MyTests : IClassFixture<PopulatedTempDirFixture>
{
    private readonly PopulatedTempDirFixture _given;
    public MyTests(PopulatedTempDirFixture given) => _given = given;

    [Fact]
    public void Given_SeededDirectory_When_Reading_Then_ContentIsAvailable() =>
        Assert.Equal("seed",
            File.ReadAllText(System.IO.Path.Combine(_given.Path, "seed.txt")));
}
```

The fixture is created once per class and reused -- **treat it as
read-only** or tests will race each other.

## 7. Anti-patterns

### ❌ Bundled assertions

```csharp
// Don't -- one regression masks the others
[Fact]
public void Flag_Parsing_Works()
{
    Assert.True(ParseOk("--agent").AgentMode);
    Assert.True(ParseOk("--raw").Raw);
    Assert.True(ParseOk("--ralph").RalphMode);
}
```

### ❌ Positive-only tests

Every test asserts at least one **negative** where feasible -- "pass the
pass, fail the fail":

```csharp
// Weak
Assert.Equal("Hello from tool tests!", result);

// Strong
Assert.Equal("Hello from tool tests!", result);
Assert.False(result.StartsWith("Error:"));  // would have caught M7
```

### ❌ Year-boundary flakes

```csharp
// Bad -- fails on Dec 31 23:59:59.999
Assert.Contains(DateTime.Now.Year.ToString(), result);

// Good -- structure-only assertion
Assert.Matches(@"20\d{2}", result);
```

### ❌ Silently swallowed cleanup exceptions

```csharp
// Bad -- hides leaked-fd / permission issues in CI
try { Directory.Delete(_tempDir, recursive: true); } catch { }

// Good
try { Directory.Delete(_tempDir, recursive: true); }
catch (Exception ex) { _output?.WriteLine($"cleanup failed: {ex}"); }
```

### ❌ Process-global mutation without a `[Collection]`

`Directory.SetCurrentDirectory`, `Environment.SetEnvironmentVariable`,
and any static field on `Program` are **process-global**. xUnit parallelises
test **classes** by default. Mutating globals without opting into a
shared collection causes non-deterministic races.

See audit findings C2, C3 for the list of classes that violate this rule
today. New tests **must** include:

```csharp
[Collection("EnvironmentVariables")]  // or "FilesystemCwd", etc.
public class MyNewTests { … }
```

### ❌ Overspecified error-message assertions

```csharp
// Bad -- breaks when wording is edited
Assert.Contains("Temperature must be between 0.0 and 2.0", e.Message);

// Better -- structural
Assert.Equal(1, e.ExitCode);
Assert.Equal("temperature", e.FailedFlag);  // if available

// Acceptable -- partial wording as a smoke check
Assert.Contains("between 0.0 and 2.0", e.Message);
```

### ❌ Reintroducing dependencies

No Reqnroll. No SpecFlow. No FluentAssertions. No Moq. If you find
yourself reaching for a package, write the 20 lines of helper code
first and show it doesn't work before opening a package-addition PR.

## 8. Migration checklist for a legacy test file

1. Read the file end-to-end. Flag bundled assertions and flaky patterns
   in a scratchpad.
2. Rename methods to `Given_X_When_Y_Then_Z`. Keep behaviour identical.
3. Split each bundled test into one-behaviour-per-method.
4. Add missing negatives (audit your positive assertions -- what's the
   corresponding "does NOT" clause?).
5. Replace `DateTime.Now.Year.ToString()` with `Assert.Matches(@"20\d{2}", …)`.
6. Replace silent `catch { }` in cleanup with logged catches.
7. Add `[Trait("type", …)]` to the class.
8. Pick 1-2 async tests that benefit from narrative and port them to
   the Scenario DSL. Leave the rest naming-only.
9. Run `dotnet test`. Green? Commit with the `Co-authored-by: Copilot
   <223556219+Copilot@users.noreply.github.com>` trailer.

## 9. References

- [ADR-003: Behaviour-Driven Development in xUnit](../adr/ADR-003-behavior-driven-development.md)
- [Test sanity audit](./test-sanity-audit.md) -- the findings that
  motivated this guide
- [`tests/AzureOpenAI_CLI.Tests/Bdd/Scenario.cs`](../../tests/AzureOpenAI_CLI.Tests/Bdd/Scenario.cs)
  -- the DSL implementation (~200 LOC)
- [`tests/AzureOpenAI_CLI.Tests/CliParserTests.cs`](../../tests/AzureOpenAI_CLI.Tests/CliParserTests.cs)
  -- pilot: pure-function BDD
- [`tests/AzureOpenAI_CLI.Tests/ToolTests.cs`](../../tests/AzureOpenAI_CLI.Tests/ToolTests.cs)
  -- pilot: async BDD with DSL

---

*Either it works or it doesn't. Test it. High-five.*
