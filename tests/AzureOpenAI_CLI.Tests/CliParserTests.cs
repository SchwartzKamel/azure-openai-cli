// CliParserTests.cs — BDD pilot conversion (ADR-003).
//
// Each test observes ONE behaviour. Names follow
// Given_<State>_When_<Action>_Then_<Observable>. The Scenario DSL is
// used on the split-out cases to demonstrate the narrative wrapper;
// simpler one-line tests use naming-only BDD. Both forms coexist on
// purpose — not every test benefits from the fluent chain.
//
// Coverage delta vs. pre-BDD version (56 tests):
//   + Empty_Args_ReturnsDefaultOptions (14 bundled asserts) split into
//     14 single-behaviour tests, 1 per default property.
//   + MultipleFlagsAndPositionals_Combined split into 4 tests.
//   + Schema_InvalidJson_Errors and Schema_EmptyString_Errors merged
//     into one Theory with named scenarios (audit finding M4).
//   All behaviours preserved; net test count increases.

using AzureOpenAI_CLI;
using AzureOpenAI_CLI.Tests.Bdd;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// Behavioural tests for Program.ParseCliFlags — one behaviour per test,
/// Given / When / Then naming. See ADR-003.
/// </summary>
[Trait("type", "behavior")]
public class CliParserTests
{
    // ── helpers ──────────────────────────────────────────────────────

    private static Program.CliOptions ParseOk(params string[] args)
    {
        var (opts, err) = Program.ParseCliFlags(args);
        Assert.Null(err);
        Assert.NotNull(opts);
        return opts!;
    }

    private static Program.CliParseError ParseErr(params string[] args)
    {
        var (opts, err) = Program.ParseCliFlags(args);
        Assert.Null(opts);
        Assert.NotNull(err);
        return err!;
    }

    // ─────────────────────────────────────────────────────────────────
    // Defaults (was Empty_Args_ReturnsDefaultOptions — 14 bundled
    // assertions split into per-behaviour tests)
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Given_NoArgs_When_Parsing_Then_TemperatureIsNull() =>
        Assert.Null(ParseOk().Temperature);

    [Fact]
    public void Given_NoArgs_When_Parsing_Then_MaxTokensIsNull() =>
        Assert.Null(ParseOk().MaxTokens);

    [Fact]
    public void Given_NoArgs_When_Parsing_Then_SystemPromptIsNull() =>
        Assert.Null(ParseOk().SystemPrompt);

    [Fact]
    public void Given_NoArgs_When_Parsing_Then_ShowConfigIsFalse() =>
        Assert.False(ParseOk().ShowConfig);

    [Fact]
    public void Given_NoArgs_When_Parsing_Then_AgentModeIsFalse() =>
        Assert.False(ParseOk().AgentMode);

    [Fact]
    public void Given_NoArgs_When_Parsing_Then_RalphModeIsFalse() =>
        Assert.False(ParseOk().RalphMode);

    [Fact]
    public void Given_NoArgs_When_Parsing_Then_RawIsFalse() =>
        Assert.False(ParseOk().Raw);

    [Fact]
    public void Given_NoArgs_When_Parsing_Then_SquadInitIsFalse() =>
        Assert.False(ParseOk().SquadInit);

    [Fact]
    public void Given_NoArgs_When_Parsing_Then_ListPersonasIsFalse() =>
        Assert.False(ParseOk().ListPersonas);

    [Fact]
    public void Given_NoArgs_When_Parsing_Then_EnabledToolsIsNull() =>
        Assert.Null(ParseOk().EnabledTools);

    [Fact]
    public void Given_NoArgs_When_Parsing_Then_JsonSchemaIsNull() =>
        Assert.Null(ParseOk().JsonSchema);

    [Fact]
    public void Given_NoArgs_When_Parsing_Then_ValidateCommandIsNull() =>
        Assert.Null(ParseOk().ValidateCommand);

    [Fact]
    public void Given_NoArgs_When_Parsing_Then_TaskFileIsNull() =>
        Assert.Null(ParseOk().TaskFile);

    [Fact]
    public void Given_NoArgs_When_Parsing_Then_PersonaNameIsNull() =>
        Assert.Null(ParseOk().PersonaName);

    [Fact]
    public void Given_NoArgs_When_Parsing_Then_RemainingArgsIsEmpty() =>
        Assert.Empty(ParseOk().RemainingArgs);

    [Fact]
    public void Given_NoArgs_When_Parsing_Then_MaxIterationsDefaultsToTen() =>
        Assert.Equal(10, ParseOk().MaxIterations);

    [Fact]
    public void Given_NoArgs_When_Parsing_Then_MaxAgentRoundsDefaultsToFive() =>
        Assert.Equal(5, ParseOk().MaxAgentRounds);

    [Fact]
    public void Given_OnlyPositionals_When_Parsing_Then_TheyPassThroughAsRemainingArgs()
    {
        var o = ParseOk("hello", "world");
        Assert.Equal(new[] { "hello", "world" }, o.RemainingArgs);
    }

    // ─────────────────────────────────────────────────────────────────
    // --temperature
    // ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("0.0", "lower bound")]
    [InlineData("0.7", "typical")]
    [InlineData("1.5", "mid range")]
    [InlineData("2.0", "upper bound")]
    [Trait("type", "property")]
    public void Given_ValidTemperature_When_Parsing_Then_ValueIsStored(string raw, string scenario)
    {
        _ = scenario; // narrative only; xUnit test name includes it
        Scenario
            .Given($"--temperature {raw} ({scenario})", () => new[] { "--temperature", raw })
            .When("parsing flags", args => ParseOk(args))
            .Then("the float is stored on CliOptions",
                o => Assert.Equal(
                    float.Parse(raw, System.Globalization.CultureInfo.InvariantCulture),
                    o.Temperature));
    }

    [Fact]
    public void Given_ShortTemperatureFlag_When_Parsing_Then_ValueIsStored() =>
        Assert.Equal(0.42f, ParseOk("-t", "0.42").Temperature);

    [Fact]
    public void Given_TemperatureBelowZero_When_Parsing_Then_RangeErrorIsReturned()
    {
        var e = ParseErr("--temperature", "-0.1");
        Assert.Contains("between 0.0 and 2.0", e.Message);
        Assert.Equal(1, e.ExitCode);
    }

    [Fact]
    public void Given_TemperatureAboveTwo_When_Parsing_Then_RangeErrorIsReturned() =>
        Assert.Contains("between 0.0 and 2.0", ParseErr("--temperature", "2.5").Message);

    [Fact]
    public void Given_NonNumericTemperature_When_Parsing_Then_NumericErrorIsReturned() =>
        Assert.Contains("numeric value", ParseErr("--temperature", "hot").Message);

    [Fact]
    public void Given_TemperatureFlagWithoutValue_When_Parsing_Then_NumericErrorIsReturned() =>
        Assert.Contains("numeric value", ParseErr("--temperature").Message);

    // ─────────────────────────────────────────────────────────────────
    // --max-tokens
    // ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("1", 1, "lower bound")]
    [InlineData("5000", 5000, "typical")]
    [InlineData("128000", 128000, "upper bound")]
    [Trait("type", "property")]
    public void Given_ValidMaxTokens_When_Parsing_Then_IntIsStored(
        string raw, int expected, string scenario)
    {
        _ = scenario;
        Assert.Equal(expected, ParseOk("--max-tokens", raw).MaxTokens);
    }

    [Fact]
    public void Given_MaxTokensZero_When_Parsing_Then_RangeErrorIsReturned() =>
        Assert.Contains("between 1 and 128000", ParseErr("--max-tokens", "0").Message);

    [Fact]
    public void Given_NegativeMaxTokens_When_Parsing_Then_RangeErrorIsReturned() =>
        Assert.Contains("between 1 and 128000", ParseErr("--max-tokens", "-5").Message);

    [Fact]
    public void Given_MaxTokensAbove128000_When_Parsing_Then_RangeErrorIsReturned() =>
        Assert.Contains("between 1 and 128000", ParseErr("--max-tokens", "200000").Message);

    [Fact]
    public void Given_NonNumericMaxTokens_When_Parsing_Then_IntegerErrorIsReturned() =>
        Assert.Contains("integer value", ParseErr("--max-tokens", "lots").Message);

    [Fact]
    public void Given_MaxTokensFlagWithoutValue_When_Parsing_Then_IntegerErrorIsReturned() =>
        Assert.Contains("integer value", ParseErr("--max-tokens").Message);

    // ─────────────────────────────────────────────────────────────────
    // --system
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Given_SystemPrompt_When_Parsing_Then_PromptIsStored() =>
        Assert.Equal("You are a pirate", ParseOk("--system", "You are a pirate").SystemPrompt);

    [Fact]
    public void Given_EmptySystemPrompt_When_Parsing_Then_EmptyStringIsStored() =>
        Assert.Equal("", ParseOk("--system", "").SystemPrompt);

    [Fact]
    public void Given_SystemFlagWithoutValue_When_Parsing_Then_RequiresValueErrorIsReturned() =>
        Assert.Contains("--system requires a value", ParseErr("--system").Message);

    // ─────────────────────────────────────────────────────────────────
    // --config
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Given_ConfigShow_When_Parsing_Then_ShowConfigFlagIsTrue() =>
        Assert.True(ParseOk("--config", "show").ShowConfig);

    [Fact]
    public void Given_UnknownConfigSubcommand_When_Parsing_Then_ErrorIsReturned() =>
        Assert.Contains("Unknown --config subcommand", ParseErr("--config", "edit").Message);

    [Fact]
    public void Given_ConfigFlagWithoutSub_When_Parsing_Then_ErrorIsReturned() =>
        Assert.Contains("Unknown --config subcommand", ParseErr("--config").Message);

    // ─────────────────────────────────────────────────────────────────
    // --agent / --ralph
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Given_AgentFlag_When_Parsing_Then_AgentModeIsTrue()
    {
        var o = ParseOk("--agent");
        Assert.True(o.AgentMode);
    }

    [Fact]
    public void Given_AgentFlag_When_Parsing_Then_RalphModeIsFalse() =>
        Assert.False(ParseOk("--agent").RalphMode);

    [Fact]
    public void Given_RalphFlag_When_Parsing_Then_RalphModeIsTrue() =>
        Assert.True(ParseOk("--ralph").RalphMode);

    [Fact]
    public void Given_RalphFlag_When_Parsing_Then_AgentModeIsImpliedTrue() =>
        Assert.True(ParseOk("--ralph").AgentMode);

    [Fact]
    public void Given_AgentAndRalphTogether_When_Parsing_Then_BothAreAcceptedWithoutError()
    {
        var o = ParseOk("--agent", "--ralph");
        Assert.True(o.AgentMode);
        Assert.True(o.RalphMode);
    }

    // ─────────────────────────────────────────────────────────────────
    // --max-rounds
    // ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("1", 1, "lower bound")]
    [InlineData("20", 20, "upper bound")]
    [Trait("type", "property")]
    public void Given_ValidMaxRounds_When_Parsing_Then_IntIsStored(
        string raw, int expected, string scenario)
    {
        _ = scenario;
        Assert.Equal(expected, ParseOk("--max-rounds", raw).MaxAgentRounds);
    }

    [Theory]
    [InlineData("0", "below range")]
    [InlineData("-1", "negative")]
    [InlineData("21", "above range")]
    [InlineData("nope", "non-numeric")]
    [Trait("type", "property")]
    public void Given_InvalidMaxRounds_When_Parsing_Then_ErrorIsReturned(string raw, string scenario)
    {
        _ = scenario;
        Assert.Contains("--max-rounds requires", ParseErr("--max-rounds", raw).Message);
    }

    [Fact]
    public void Given_MaxRoundsFlagWithoutValue_When_Parsing_Then_ErrorIsReturned() =>
        Assert.Contains("--max-rounds requires", ParseErr("--max-rounds").Message);

    // ─────────────────────────────────────────────────────────────────
    // --tools
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Given_CommaSeparatedTools_When_Parsing_Then_AllAreStoredCaseInsensitive()
    {
        Scenario
            .Given("the --tools list 'shell,FILE, web'",
                () => new[] { "--tools", "shell,FILE, web" })
            .When("parsing flags", args => ParseOk(args).EnabledTools!)
            .Then("three entries are present",
                t => Assert.Equal(3, t.Count))
            .And("'shell' is present (original case)", t => Assert.Contains("shell", t))
            .And("'file' matches case-insensitively", t => Assert.Contains("file", t))
            .And("'WEB' matches case-insensitively", t => Assert.Contains("WEB", t));
    }

    [Fact]
    public void Given_ToolsFlagWithoutValue_When_Parsing_Then_ErrorIsReturned() =>
        Assert.Contains("--tools requires", ParseErr("--tools").Message);

    // ─────────────────────────────────────────────────────────────────
    // --schema
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Given_ValidJsonSchema_When_Parsing_Then_SchemaIsStoredVerbatim()
    {
        var json = "{\"type\":\"object\",\"properties\":{\"x\":{\"type\":\"string\"}}}";
        Assert.Equal(json, ParseOk("--schema", json).JsonSchema);
    }

    // Merged M4: Schema_InvalidJson_Errors + Schema_EmptyString_Errors.
    [Theory]
    [InlineData("{not json", "truncated object")]
    [InlineData("", "empty string")]
    [InlineData("not-json-at-all", "plain text")]
    [Trait("type", "property")]
    public void Given_InvalidJsonSchema_When_Parsing_Then_SchemaErrorIsReturned(
        string raw, string scenario)
    {
        _ = scenario;
        Assert.Contains("Invalid JSON schema", ParseErr("--schema", raw).Message);
    }

    [Fact]
    public void Given_SchemaFlagWithoutValue_When_Parsing_Then_ErrorIsReturned() =>
        Assert.Contains("--schema requires", ParseErr("--schema").Message);

    // ─────────────────────────────────────────────────────────────────
    // --validate
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Given_ValidateWithCommand_When_Parsing_Then_CommandIsStored() =>
        Assert.Equal("dotnet test", ParseOk("--validate", "dotnet test").ValidateCommand);

    [Fact]
    public void Given_ValidateFlagWithoutValue_When_Parsing_Then_ErrorIsReturned() =>
        Assert.Contains("--validate requires", ParseErr("--validate").Message);

    // ─────────────────────────────────────────────────────────────────
    // --task-file
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Given_TaskFilePath_When_Parsing_Then_PathIsStored_NoExistenceCheck() =>
        Assert.Equal("/no/such/file.txt", ParseOk("--task-file", "/no/such/file.txt").TaskFile);

    [Fact]
    public void Given_TaskFileFlagWithoutValue_When_Parsing_Then_ErrorIsReturned() =>
        Assert.Contains("--task-file requires", ParseErr("--task-file").Message);

    // ─────────────────────────────────────────────────────────────────
    // --max-iterations
    // ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("1", 1, "lower bound")]
    [InlineData("10", 10, "default")]
    [InlineData("50", 50, "upper bound")]
    [Trait("type", "property")]
    public void Given_ValidMaxIterations_When_Parsing_Then_IntIsStored(
        string raw, int expected, string scenario)
    {
        _ = scenario;
        Assert.Equal(expected, ParseOk("--max-iterations", raw).MaxIterations);
    }

    [Theory]
    [InlineData("0", "below range")]
    [InlineData("-1", "negative")]
    [InlineData("51", "just above range")]
    [InlineData("100", "far above range")]
    [InlineData("abc", "non-numeric")]
    [Trait("type", "property")]
    public void Given_InvalidMaxIterations_When_Parsing_Then_ErrorIsReturned(
        string raw, string scenario)
    {
        _ = scenario;
        Assert.Contains("--max-iterations requires", ParseErr("--max-iterations", raw).Message);
    }

    [Fact]
    public void Given_MaxIterationsFlagWithoutValue_When_Parsing_Then_ErrorIsReturned() =>
        Assert.Contains("--max-iterations requires", ParseErr("--max-iterations").Message);

    // ─────────────────────────────────────────────────────────────────
    // --persona
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Given_PersonaName_When_Parsing_Then_PersonaIsStored() =>
        Assert.Equal("coder", ParseOk("--persona", "coder").PersonaName);

    [Fact]
    public void Given_PersonaName_When_Parsing_Then_AgentModeIsImpliedTrue() =>
        Assert.True(ParseOk("--persona", "coder").AgentMode);

    [Fact]
    public void Given_PersonaAuto_When_Parsing_Then_AutoIsAcceptedAsValidName() =>
        Assert.Equal("auto", ParseOk("--persona", "auto").PersonaName);

    [Fact]
    public void Given_PersonaFlagWithoutValue_When_Parsing_Then_ErrorIsReturned() =>
        Assert.Contains("--persona requires", ParseErr("--persona").Message);

    // ─────────────────────────────────────────────────────────────────
    // --squad-init / --personas / --raw
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Given_SquadInitFlag_When_Parsing_Then_SquadInitIsTrue() =>
        Assert.True(ParseOk("--squad-init").SquadInit);

    [Fact]
    public void Given_PersonasFlag_When_Parsing_Then_ListPersonasIsTrue() =>
        Assert.True(ParseOk("--personas").ListPersonas);

    [Fact]
    public void Given_RawFlag_When_Parsing_Then_RawIsTrue() =>
        Assert.True(ParseOk("--raw").Raw);

    // ─────────────────────────────────────────────────────────────────
    // combinations and fall-through
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Given_RawPlusJsonPositional_When_Parsing_Then_RawIsTrue()
    {
        // --json is NOT recognised; it is preserved as a positional token.
        Assert.True(ParseOk("--raw", "--json").Raw);
    }

    [Fact]
    public void Given_RawPlusJsonPositional_When_Parsing_Then_JsonFallsThroughToRemainingArgs() =>
        Assert.Contains("--json", ParseOk("--raw", "--json").RemainingArgs);

    [Fact]
    public void Given_UnknownPromptFlag_When_Parsing_Then_FlagAndValueFallThrough()
    {
        var o = ParseOk("hello", "--prompt", "world");
        Assert.Equal(new[] { "hello", "--prompt", "world" }, o.RemainingArgs);
    }

    [Fact]
    public void Given_RepeatedTemperature_When_Parsing_Then_LastValueWins() =>
        Assert.Equal(1.9f, ParseOk("--temperature", "0.1", "--temperature", "1.9").Temperature);

    [Fact]
    public void Given_RepeatedTools_When_Parsing_Then_LastListWins()
    {
        var enabled = ParseOk("--tools", "shell", "--tools", "file,web").EnabledTools!;

        Assert.DoesNotContain("shell", enabled);
        Assert.Contains("file", enabled);
        Assert.Contains("web", enabled);
    }

    [Fact]
    public void Given_UnknownFlag_When_Parsing_Then_FlagAndValueFallThrough()
    {
        var o = ParseOk("--unknown-flag", "value");
        Assert.Equal(new[] { "--unknown-flag", "value" }, o.RemainingArgs);
    }

    [Fact]
    public void Given_HelpFlag_When_Parsing_Then_FallsThroughToRemainingArgs() =>
        Assert.Equal(new[] { "--help" }, ParseOk("--help").RemainingArgs);

    [Fact]
    public void Given_MixedCaseLongFlags_When_Parsing_Then_AllAreRecognised()
    {
        var o = ParseOk("--TEMPERATURE", "0.5", "--Max-Tokens", "100", "--Agent");

        Assert.Equal(0.5f, o.Temperature);
        Assert.Equal(100, o.MaxTokens);
        Assert.True(o.AgentMode);
    }

    // was MultipleFlagsAndPositionals_Combined — 1 test → 4
    [Fact]
    public void Given_MultipleFlagsAndPositionals_When_Parsing_Then_AgentModeIsTrue() =>
        Assert.True(ParseOk("hello", "--agent", "--max-rounds", "7", "world", "--raw").AgentMode);

    [Fact]
    public void Given_MultipleFlagsAndPositionals_When_Parsing_Then_RawIsTrue() =>
        Assert.True(ParseOk("hello", "--agent", "--max-rounds", "7", "world", "--raw").Raw);

    [Fact]
    public void Given_MultipleFlagsAndPositionals_When_Parsing_Then_MaxRoundsIsSeven() =>
        Assert.Equal(7, ParseOk("hello", "--agent", "--max-rounds", "7", "world", "--raw").MaxAgentRounds);

    [Fact]
    public void Given_MultipleFlagsAndPositionals_When_Parsing_Then_PositionalsAreOrderPreserved() =>
        Assert.Equal(new[] { "hello", "world" },
            ParseOk("hello", "--agent", "--max-rounds", "7", "world", "--raw").RemainingArgs);

    [Fact]
    public void Given_FirstErrorEncountered_When_Parsing_Then_ParsingShortCircuits()
    {
        // When the first malformed flag is encountered, parsing stops and later
        // malformed flags are not reported. Audit H7 notes this asserts prose
        // wording; kept here until the parser exposes a structured flag field.
        var e = ParseErr("--temperature", "5.0", "--max-tokens", "0");
        Assert.Contains("Temperature", e.Message);
    }
}
