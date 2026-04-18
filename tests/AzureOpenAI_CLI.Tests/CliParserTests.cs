using AzureOpenAI_CLI;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// Direct unit tests for Program.ParseCliFlags. Each flag is exercised with
/// both happy-path values (positive assertions) and malformed inputs
/// (negative assertions confirming a CliParseError is returned).
///
/// Notes on flags that DO exist (parsed by ParseCliFlags):
///   --temperature/-t, --max-tokens, --system, --config show, --agent,
///   --max-rounds, --tools, --schema, --ralph, --validate, --task-file,
///   --max-iterations, --persona, --squad-init, --personas, --raw
///
/// Flags like --help, --json, --model, --timeout, --prompt, --chat are NOT
/// recognized by ParseCliFlags; they fall through to RemainingArgs as
/// positional tokens. Tests below assert that pass-through behavior.
/// </summary>
public class CliParserTests
{
    // ---------- helpers ----------
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

    // ---------- empty / no flags ----------
    [Fact]
    public void Empty_Args_ReturnsDefaultOptions()
    {
        var o = ParseOk();
        Assert.Null(o.Temperature);
        Assert.Null(o.MaxTokens);
        Assert.Null(o.SystemPrompt);
        Assert.False(o.ShowConfig);
        Assert.False(o.AgentMode);
        Assert.False(o.RalphMode);
        Assert.False(o.Raw);
        Assert.False(o.SquadInit);
        Assert.False(o.ListPersonas);
        Assert.Null(o.EnabledTools);
        Assert.Null(o.JsonSchema);
        Assert.Null(o.ValidateCommand);
        Assert.Null(o.TaskFile);
        Assert.Null(o.PersonaName);
        Assert.Empty(o.RemainingArgs);
    }

    [Fact]
    public void OnlyPositional_Args_PassedThroughAsRemaining()
    {
        var o = ParseOk("hello", "world");
        Assert.Equal(new[] { "hello", "world" }, o.RemainingArgs);
    }

    // ---------- --temperature ----------
    [Theory]
    [InlineData("0.0")]
    [InlineData("0.7")]
    [InlineData("1.5")]
    [InlineData("2.0")]
    public void Temperature_ValidValue_IsParsed(string v)
    {
        var o = ParseOk("--temperature", v);
        Assert.Equal(float.Parse(v, System.Globalization.CultureInfo.InvariantCulture), o.Temperature);
    }

    [Fact]
    public void Temperature_ShortFlag_T_Works()
    {
        var o = ParseOk("-t", "0.42");
        Assert.Equal(0.42f, o.Temperature);
    }

    [Fact]
    public void Temperature_BelowZero_Errors()
    {
        var e = ParseErr("--temperature", "-0.1");
        Assert.Contains("between 0.0 and 2.0", e.Message);
        Assert.Equal(1, e.ExitCode);
    }

    [Fact]
    public void Temperature_AboveTwo_Errors()
    {
        var e = ParseErr("--temperature", "2.5");
        Assert.Contains("between 0.0 and 2.0", e.Message);
    }

    [Fact]
    public void Temperature_NonNumeric_Errors()
    {
        var e = ParseErr("--temperature", "hot");
        Assert.Contains("numeric value", e.Message);
    }

    [Fact]
    public void Temperature_MissingValue_Errors()
    {
        var e = ParseErr("--temperature");
        Assert.Contains("numeric value", e.Message);
    }

    // ---------- --max-tokens ----------
    [Theory]
    [InlineData("1", 1)]
    [InlineData("5000", 5000)]
    [InlineData("128000", 128000)]
    public void MaxTokens_ValidValue_IsParsed(string v, int expected)
    {
        var o = ParseOk("--max-tokens", v);
        Assert.Equal(expected, o.MaxTokens);
    }

    [Fact]
    public void MaxTokens_Zero_Errors()
    {
        var e = ParseErr("--max-tokens", "0");
        Assert.Contains("between 1 and 128000", e.Message);
    }

    [Fact]
    public void MaxTokens_Negative_Errors()
    {
        Assert.Contains("between 1 and 128000", ParseErr("--max-tokens", "-5").Message);
    }

    [Fact]
    public void MaxTokens_TooLarge_Errors()
    {
        Assert.Contains("between 1 and 128000", ParseErr("--max-tokens", "200000").Message);
    }

    [Fact]
    public void MaxTokens_NonNumeric_Errors()
    {
        Assert.Contains("integer value", ParseErr("--max-tokens", "lots").Message);
    }

    [Fact]
    public void MaxTokens_MissingValue_Errors()
    {
        Assert.Contains("integer value", ParseErr("--max-tokens").Message);
    }

    // ---------- --system ----------
    [Fact]
    public void System_ValidPrompt_IsParsed()
    {
        var o = ParseOk("--system", "You are a pirate");
        Assert.Equal("You are a pirate", o.SystemPrompt);
    }

    [Fact]
    public void System_EmptyString_IsAccepted()
    {
        // Current behavior: empty string is still a value, parser accepts it.
        var o = ParseOk("--system", "");
        Assert.Equal("", o.SystemPrompt);
    }

    [Fact]
    public void System_MissingValue_Errors()
    {
        Assert.Contains("--system requires a value", ParseErr("--system").Message);
    }

    // ---------- --config ----------
    [Fact]
    public void Config_Show_SetsFlag()
    {
        Assert.True(ParseOk("--config", "show").ShowConfig);
    }

    [Fact]
    public void Config_UnknownSub_Errors()
    {
        Assert.Contains("Unknown --config subcommand", ParseErr("--config", "edit").Message);
    }

    [Fact]
    public void Config_MissingSub_Errors()
    {
        Assert.Contains("Unknown --config subcommand", ParseErr("--config").Message);
    }

    // ---------- --agent / --ralph ----------
    [Fact]
    public void Agent_Flag_SetsAgentMode()
    {
        var o = ParseOk("--agent");
        Assert.True(o.AgentMode);
        Assert.False(o.RalphMode);
    }

    [Fact]
    public void Ralph_Flag_ImpliesAgentMode()
    {
        var o = ParseOk("--ralph");
        Assert.True(o.RalphMode);
        Assert.True(o.AgentMode);
    }

    [Fact]
    public void Agent_And_Ralph_Together_BothSet_NoError()
    {
        // Documented current behavior: both flags can coexist.
        var o = ParseOk("--agent", "--ralph");
        Assert.True(o.AgentMode);
        Assert.True(o.RalphMode);
    }

    // ---------- --max-rounds ----------
    [Theory]
    [InlineData("1", 1)]
    [InlineData("20", 20)]
    public void MaxRounds_Valid_IsParsed(string v, int expected)
    {
        Assert.Equal(expected, ParseOk("--max-rounds", v).MaxAgentRounds);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("21")]
    [InlineData("nope")]
    public void MaxRounds_OutOfRangeOrNonNumeric_Errors(string v)
    {
        Assert.Contains("--max-rounds requires", ParseErr("--max-rounds", v).Message);
    }

    [Fact]
    public void MaxRounds_MissingValue_Errors()
    {
        Assert.Contains("--max-rounds requires", ParseErr("--max-rounds").Message);
    }

    // ---------- --tools ----------
    [Fact]
    public void Tools_CommaList_IsParsedCaseInsensitive()
    {
        var o = ParseOk("--tools", "shell,FILE, web");
        Assert.NotNull(o.EnabledTools);
        Assert.Contains("shell", o.EnabledTools!);
        Assert.Contains("file", o.EnabledTools!); // case-insensitive set
        Assert.Contains("WEB", o.EnabledTools!);  // case-insensitive lookup
        Assert.Equal(3, o.EnabledTools!.Count);
    }

    [Fact]
    public void Tools_MissingValue_Errors()
    {
        Assert.Contains("--tools requires", ParseErr("--tools").Message);
    }

    // ---------- --schema ----------
    [Fact]
    public void Schema_ValidJson_IsAccepted()
    {
        var json = "{\"type\":\"object\",\"properties\":{\"x\":{\"type\":\"string\"}}}";
        var o = ParseOk("--schema", json);
        Assert.Equal(json, o.JsonSchema);
    }

    [Fact]
    public void Schema_InvalidJson_Errors()
    {
        Assert.Contains("Invalid JSON schema", ParseErr("--schema", "{not json").Message);
    }

    [Fact]
    public void Schema_EmptyString_Errors()
    {
        // Empty string is not valid JSON.
        Assert.Contains("Invalid JSON schema", ParseErr("--schema", "").Message);
    }

    [Fact]
    public void Schema_MissingValue_Errors()
    {
        Assert.Contains("--schema requires", ParseErr("--schema").Message);
    }

    // ---------- --validate ----------
    [Fact]
    public void Validate_WithCommand_IsParsed()
    {
        Assert.Equal("dotnet test", ParseOk("--validate", "dotnet test").ValidateCommand);
    }

    [Fact]
    public void Validate_MissingValue_Errors()
    {
        Assert.Contains("--validate requires", ParseErr("--validate").Message);
    }

    // ---------- --task-file ----------
    [Fact]
    public void TaskFile_WithPath_IsParsed_NoFileExistenceCheck()
    {
        // ParseCliFlags does NOT check for file existence — it just records the path.
        var o = ParseOk("--task-file", "/no/such/file.txt");
        Assert.Equal("/no/such/file.txt", o.TaskFile);
    }

    [Fact]
    public void TaskFile_MissingValue_Errors()
    {
        Assert.Contains("--task-file requires", ParseErr("--task-file").Message);
    }

    // ---------- --max-iterations ----------
    [Theory]
    [InlineData("1", 1)]
    [InlineData("10", 10)]
    [InlineData("50", 50)]
    public void MaxIterations_Valid_IsParsed(string v, int expected)
    {
        Assert.Equal(expected, ParseOk("--max-iterations", v).MaxIterations);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("51")]
    [InlineData("100")]
    [InlineData("abc")]
    public void MaxIterations_OutOfRangeOrNonNumeric_Errors(string v)
    {
        Assert.Contains("--max-iterations requires", ParseErr("--max-iterations", v).Message);
    }

    [Fact]
    public void MaxIterations_MissingValue_Errors()
    {
        Assert.Contains("--max-iterations requires", ParseErr("--max-iterations").Message);
    }

    [Fact]
    public void MaxIterations_DefaultIsTen()
    {
        Assert.Equal(10, ParseOk().MaxIterations);
    }

    // ---------- --persona ----------
    [Fact]
    public void Persona_Name_IsParsedAndImpliesAgentMode()
    {
        var o = ParseOk("--persona", "coder");
        Assert.Equal("coder", o.PersonaName);
        Assert.True(o.AgentMode);
    }

    [Fact]
    public void Persona_Auto_IsAccepted()
    {
        var o = ParseOk("--persona", "auto");
        Assert.Equal("auto", o.PersonaName);
        Assert.True(o.AgentMode);
    }

    [Fact]
    public void Persona_MissingValue_Errors()
    {
        Assert.Contains("--persona requires", ParseErr("--persona").Message);
    }

    // ---------- --squad-init / --personas / --raw ----------
    [Fact]
    public void SquadInit_Flag_IsSet()
    {
        Assert.True(ParseOk("--squad-init").SquadInit);
    }

    [Fact]
    public void Personas_Flag_IsSet()
    {
        Assert.True(ParseOk("--personas").ListPersonas);
    }

    [Fact]
    public void Raw_Flag_IsSet()
    {
        Assert.True(ParseOk("--raw").Raw);
    }

    // ---------- combinations ----------
    [Fact]
    public void RawAndJsonPositional_BothPreserved_NoError()
    {
        // --json is NOT a recognized flag; it falls through as positional.
        var o = ParseOk("--raw", "--json");
        Assert.True(o.Raw);
        Assert.Contains("--json", o.RemainingArgs);
    }

    [Fact]
    public void PromptPositional_AndUnknownPromptFlag_FallThrough()
    {
        // --prompt is not parsed; both it and its value land in RemainingArgs.
        var o = ParseOk("hello", "--prompt", "world");
        Assert.Equal(new[] { "hello", "--prompt", "world" }, o.RemainingArgs);
    }

    [Fact]
    public void RepeatedFlag_LastValueWins()
    {
        var o = ParseOk("--temperature", "0.1", "--temperature", "1.9");
        Assert.Equal(1.9f, o.Temperature);
    }

    [Fact]
    public void RepeatedToolsFlag_LastListWins()
    {
        var o = ParseOk("--tools", "shell", "--tools", "file,web");
        Assert.NotNull(o.EnabledTools);
        Assert.DoesNotContain("shell", o.EnabledTools!);
        Assert.Contains("file", o.EnabledTools!);
        Assert.Contains("web", o.EnabledTools!);
    }

    [Fact]
    public void UnknownFlag_FallsThroughToRemainingArgs()
    {
        var o = ParseOk("--unknown-flag", "value");
        Assert.Equal(new[] { "--unknown-flag", "value" }, o.RemainingArgs);
    }

    [Fact]
    public void HelpFlag_FallsThroughToRemainingArgs()
    {
        // --help is not consumed by ParseCliFlags; main handles it elsewhere.
        var o = ParseOk("--help");
        Assert.Equal(new[] { "--help" }, o.RemainingArgs);
    }

    [Fact]
    public void FlagCaseInsensitive_LongForms()
    {
        // Argument names are lowercased before comparison; mixed case still works.
        var o = ParseOk("--TEMPERATURE", "0.5", "--Max-Tokens", "100", "--Agent");
        Assert.Equal(0.5f, o.Temperature);
        Assert.Equal(100, o.MaxTokens);
        Assert.True(o.AgentMode);
    }

    [Fact]
    public void MultipleFlagsAndPositionals_Combined()
    {
        var o = ParseOk("hello", "--agent", "--max-rounds", "7", "world", "--raw");
        Assert.True(o.AgentMode);
        Assert.True(o.Raw);
        Assert.Equal(7, o.MaxAgentRounds);
        Assert.Equal(new[] { "hello", "world" }, o.RemainingArgs);
    }

    [Fact]
    public void FirstError_ShortCircuits()
    {
        // When the first malformed flag is encountered, parsing stops and
        // later (also-malformed) flags are not reported.
        var e = ParseErr("--temperature", "5.0", "--max-tokens", "0");
        Assert.Contains("Temperature", e.Message);
    }

    [Fact]
    public void Default_MaxAgentRounds_IsFive()
    {
        Assert.Equal(5, ParseOk().MaxAgentRounds);
    }
}
