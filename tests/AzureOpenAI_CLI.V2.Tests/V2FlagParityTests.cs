using System.IO;
using System.Linq;

namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// Flag-parity tests for v2.0.0 cutover: Newman's 9 v1→v2 flag audit + FR-003/009/010
/// (UserConfig, model aliases, config CRUD). Covers positive AND negative paths for every
/// flag — Kramer's rule: pass the pass, fail the fail.
/// </summary>
[Collection("ConsoleCapture")]
public class V2FlagParityTests
{
    // ── --json: flips error output to structured JSON at every ErrorAndExit ─────

    [Fact]
    public void Json_Flag_IsParsed()
    {
        var opts = Program.ParseArgs(["--json", "hello"]);
        Assert.True(opts.Json);
        Assert.False(opts.ParseError);
    }

    [Theory]
    [InlineData(true, "\"error\":")]   // JSON mode: emit structured error on stdout
    [InlineData(false, null)]          // Non-JSON: stderr [ERROR] prefix
    public void ErrorAndExit_HonorsJsonMode(bool jsonMode, string? stdoutSubstring)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var oldOut = Console.Out;
        var oldErr = Console.Error;
        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            int rc = Program.ErrorAndExit("boom", 7, jsonMode);
            Assert.Equal(7, rc);
            if (jsonMode)
            {
                Assert.Contains(stdoutSubstring!, stdout.ToString());
                Assert.Contains("\"message\":", stdout.ToString());
                Assert.Contains("\"exit_code\":", stdout.ToString());
                Assert.Equal(string.Empty, stderr.ToString());
            }
            else
            {
                Assert.Equal(string.Empty, stdout.ToString());
                Assert.Contains("[ERROR] boom", stderr.ToString());
            }
        }
        finally
        {
            Console.SetOut(oldOut);
            Console.SetError(oldErr);
        }
    }

    // ── --version --short: bare semver (cutover Gate 2 depends on this) ────────

    [Fact]
    public void VersionShort_IsParsed_AlongWithVersion()
    {
        var opts = Program.ParseArgs(["--version", "--short"]);
        Assert.True(opts.ShowVersion);
        Assert.True(opts.VersionShort);
    }

    [Fact]
    public void VersionShort_WithoutVersion_IsStillCaptured()
    {
        // --short without --version still parses, but has no effect until --version fires.
        var opts = Program.ParseArgs(["--short"]);
        Assert.True(opts.VersionShort);
        Assert.False(opts.ShowVersion);
    }

    // ── --schema <json>: structured-output constraint ──────────────────────────

    [Fact]
    public void Schema_ValidJson_IsParsed()
    {
        var schema = "{\"type\":\"object\",\"properties\":{\"x\":{\"type\":\"number\"}}}";
        var opts = Program.ParseArgs(["--schema", schema, "prompt"]);
        Assert.Equal(schema, opts.Schema);
        Assert.False(opts.ParseError);
    }

    [Fact]
    public void Schema_InvalidJson_ReturnsParseError()
    {
        using var err = new StringWriter();
        var oldErr = Console.Error;
        try
        {
            Console.SetError(err);
            var opts = Program.ParseArgs(["--schema", "{not valid json"]);
            Assert.True(opts.ParseError);
            Assert.True(opts.ShowHelp);
            Assert.Contains("Invalid JSON schema", err.ToString());
        }
        finally { Console.SetError(oldErr); }
    }

    [Fact]
    public void Schema_Missing_Argument_ReturnsParseError()
    {
        using var err = new StringWriter();
        var oldErr = Console.Error;
        try
        {
            Console.SetError(err);
            var opts = Program.ParseArgs(["--schema"]);
            Assert.True(opts.ParseError);
        }
        finally { Console.SetError(oldErr); }
    }

    // ── --max-rounds: agent round cap, 1-20 ────────────────────────────────────

    [Theory]
    [InlineData("1", 1)]
    [InlineData("10", 10)]
    [InlineData("20", 20)]
    public void MaxRounds_ValidRange_IsParsed(string raw, int expected)
    {
        var opts = Program.ParseArgs(["--max-rounds", raw, "prompt"]);
        Assert.Equal(expected, opts.MaxRounds);
        Assert.False(opts.ParseError);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("21")]
    [InlineData("not-a-number")]
    public void MaxRounds_OutOfRangeOrInvalid_ReturnsParseError(string raw)
    {
        using var err = new StringWriter();
        var oldErr = Console.Error;
        try
        {
            Console.SetError(err);
            var opts = Program.ParseArgs(["--max-rounds", raw]);
            Assert.True(opts.ParseError);
            Assert.Contains("--max-rounds", err.ToString());
        }
        finally { Console.SetError(oldErr); }
    }

    [Fact]
    public void MaxRounds_Default_Is5()
    {
        var opts = Program.ParseArgs(["prompt"]);
        Assert.Equal(5, opts.MaxRounds);
    }

    // ── --config <path>: alt config file ───────────────────────────────────────

    [Fact]
    public void ConfigPath_AltPath_IsParsed()
    {
        var opts = Program.ParseArgs(["--config", "/tmp/my-config.json", "prompt"]);
        Assert.Equal("/tmp/my-config.json", opts.ConfigPath);
        Assert.Null(opts.ConfigSubcommand);
    }

    [Fact]
    public void ConfigPath_NotASubcommand_TreatedAsPath()
    {
        var opts = Program.ParseArgs(["--config", "some/path/not-a-subcmd"]);
        Assert.Equal("some/path/not-a-subcmd", opts.ConfigPath);
    }

    // ── --config set/get/list/reset (FR-009) ───────────────────────────────────

    [Fact]
    public void ConfigSet_ValidKeyValue_IsParsed()
    {
        var opts = Program.ParseArgs(["--config", "set", "default_model=fast"]);
        Assert.Equal("set", opts.ConfigSubcommand);
        Assert.Equal("default_model", opts.ConfigKey);
        Assert.Equal("fast", opts.ConfigValue);
    }

    [Fact]
    public void ConfigSet_MissingEquals_ReturnsParseError()
    {
        using var err = new StringWriter();
        var oldErr = Console.Error;
        try
        {
            Console.SetError(err);
            var opts = Program.ParseArgs(["--config", "set", "default_model"]);
            Assert.True(opts.ParseError);
        }
        finally { Console.SetError(oldErr); }
    }

    [Fact]
    public void ConfigGet_WithKey_IsParsed()
    {
        var opts = Program.ParseArgs(["--config", "get", "default_model"]);
        Assert.Equal("get", opts.ConfigSubcommand);
        Assert.Equal("default_model", opts.ConfigKey);
    }

    [Fact]
    public void ConfigList_IsParsed()
    {
        var opts = Program.ParseArgs(["--config", "list"]);
        Assert.Equal("list", opts.ConfigSubcommand);
    }

    [Fact]
    public void ConfigReset_IsParsed()
    {
        var opts = Program.ParseArgs(["--config", "reset"]);
        Assert.Equal("reset", opts.ConfigSubcommand);
    }

    [Fact]
    public void ConfigShow_IsParsed()
    {
        var opts = Program.ParseArgs(["--config", "show"]);
        Assert.Equal("show", opts.ConfigSubcommand);
    }

    // ── --completions <shell>: emit shell completion script ────────────────────

    [Theory]
    [InlineData("bash")]
    [InlineData("zsh")]
    [InlineData("fish")]
    public void Completions_ValidShell_EmitsScript(string shell)
    {
        using var stdout = new StringWriter();
        var oldOut = Console.Out;
        try
        {
            Console.SetOut(stdout);
            int rc = Program.EmitCompletions(shell);
            Assert.Equal(0, rc);
            var output = stdout.ToString();
            Assert.NotEmpty(output);
            // Each script mentions az-ai-v2.
            Assert.Contains("az-ai", output);
        }
        finally { Console.SetOut(oldOut); }
    }

    [Fact]
    public void Completions_UnknownShell_ReturnsExit2()
    {
        using var err = new StringWriter();
        var oldErr = Console.Error;
        try
        {
            Console.SetError(err);
            int rc = Program.EmitCompletions("powershell");
            Assert.Equal(2, rc);
            Assert.Contains("Unsupported shell", err.ToString());
        }
        finally { Console.SetError(oldErr); }
    }

    [Fact]
    public void Completions_MissingShellArg_ReturnsParseError()
    {
        using var err = new StringWriter();
        var oldErr = Console.Error;
        try
        {
            Console.SetError(err);
            var opts = Program.ParseArgs(["--completions"]);
            Assert.True(opts.ParseError);
        }
        finally { Console.SetError(oldErr); }
    }

    // ── --models / --list-models / --current-model / --set-model (FR-010) ──────

    [Theory]
    [InlineData("--models")]
    [InlineData("--list-models")]
    public void ListModelsFlags_AreParsed(string flag)
    {
        var opts = Program.ParseArgs([flag]);
        Assert.True(opts.ListModels);
    }

    [Fact]
    public void CurrentModel_IsParsed()
    {
        var opts = Program.ParseArgs(["--current-model"]);
        Assert.True(opts.CurrentModel);
    }

    [Fact]
    public void SetModel_WithSpec_IsParsed()
    {
        var opts = Program.ParseArgs(["--set-model", "fast=gpt-4o-mini"]);
        Assert.Equal("fast=gpt-4o-mini", opts.SetModelSpec);
    }

    [Fact]
    public void SetModel_Missing_Argument_ReturnsParseError()
    {
        using var err = new StringWriter();
        var oldErr = Console.Error;
        try
        {
            Console.SetError(err);
            var opts = Program.ParseArgs(["--set-model"]);
            Assert.True(opts.ParseError);
        }
        finally { Console.SetError(oldErr); }
    }

    [Fact]
    public void SetModelCommand_PersistsAndSetsAsDefault()
    {
        using var tmpDir = new TempHome();
        var config = UserConfig.Load();
        int rc = Program.SetModelCommand("fast=gpt-4o-mini", config, jsonMode: false);
        Assert.Equal(0, rc);
        Assert.Equal("gpt-4o-mini", config.Models["fast"]);
        Assert.Equal("fast", config.DefaultModel);

        // Round-trip: reload from disk
        var loaded = UserConfig.Load();
        Assert.Equal("gpt-4o-mini", loaded.Models["fast"]);
        Assert.Equal("fast", loaded.DefaultModel);
    }

    [Fact]
    public void SetModelCommand_InvalidSpec_ReturnsExit1()
    {
        using var tmpDir = new TempHome();
        using var err = new StringWriter();
        var oldErr = Console.Error;
        try
        {
            Console.SetError(err);
            var config = new UserConfig();
            int rc = Program.SetModelCommand("no-equals-sign", config, jsonMode: false);
            Assert.Equal(1, rc);
        }
        finally { Console.SetError(oldErr); }
    }

    // ── UserConfig.ResolveModel (FR-010) ───────────────────────────────────────

    [Fact]
    public void UserConfig_ResolveModel_MapsAliasToDeployment()
    {
        var cfg = new UserConfig();
        cfg.Models["fast"] = "gpt-4o-mini";
        Assert.Equal("gpt-4o-mini", cfg.ResolveModel("fast"));
    }

    [Fact]
    public void UserConfig_ResolveModel_LiteralFallback()
    {
        var cfg = new UserConfig();
        Assert.Equal("literal-deployment", cfg.ResolveModel("literal-deployment"));
    }

    [Fact]
    public void UserConfig_ResolveModel_NullInput_ReturnsNull()
    {
        Assert.Null(new UserConfig().ResolveModel(null));
    }

    [Fact]
    public void UserConfig_SmartDefault_ResolvesDefaultModelAlias()
    {
        var cfg = new UserConfig();
        cfg.Models["smart"] = "gpt-4o";
        cfg.DefaultModel = "smart";
        Assert.Equal("gpt-4o", cfg.ResolveSmartDefault());
    }

    [Fact]
    public void UserConfig_SmartDefault_Unset_ReturnsNull()
    {
        Assert.Null(new UserConfig().ResolveSmartDefault());
    }

    // ── UserConfig CRUD (FR-009) ───────────────────────────────────────────────

    [Fact]
    public void UserConfig_SetKey_DottedDefaults()
    {
        var cfg = new UserConfig();
        Assert.True(cfg.SetKey("defaults.temperature", "0.7"));
        Assert.Equal(0.7f, cfg.Defaults.Temperature);

        Assert.True(cfg.SetKey("defaults.max_tokens", "5000"));
        Assert.Equal(5000, cfg.Defaults.MaxTokens);

        Assert.True(cfg.SetKey("default_model", "fast"));
        Assert.Equal("fast", cfg.DefaultModel);

        Assert.True(cfg.SetKey("models.fast", "gpt-4o-mini"));
        Assert.Equal("gpt-4o-mini", cfg.Models["fast"]);
    }

    [Fact]
    public void UserConfig_SetKey_InvalidKey_ReturnsFalse()
    {
        var cfg = new UserConfig();
        Assert.False(cfg.SetKey("unknown.key", "value"));
        Assert.False(cfg.SetKey("defaults.nonsense", "value"));
        Assert.False(cfg.SetKey("defaults.temperature", "not-a-float"));
    }

    [Fact]
    public void UserConfig_GetKey_RoundTrip()
    {
        var cfg = new UserConfig();
        cfg.SetKey("defaults.temperature", "0.7");
        cfg.SetKey("default_model", "fast");
        cfg.SetKey("models.fast", "gpt-4o-mini");

        Assert.Equal("0.7", cfg.GetKey("defaults.temperature"));
        Assert.Equal("fast", cfg.GetKey("default_model"));
        Assert.Equal("gpt-4o-mini", cfg.GetKey("models.fast"));
        Assert.Null(cfg.GetKey("defaults.nonexistent"));
    }

    [Fact]
    public void UserConfig_ListKeys_StableOutput()
    {
        var cfg = new UserConfig();
        cfg.SetKey("default_model", "fast");
        cfg.SetKey("models.fast", "gpt-4o-mini");
        cfg.SetKey("defaults.temperature", "0.3");
        var lines = cfg.ListKeys().ToList();
        Assert.Contains("default_model=fast", lines);
        Assert.Contains("models.fast=gpt-4o-mini", lines);
        Assert.Contains("defaults.temperature=0.3", lines);
    }

    [Fact]
    public void UserConfig_SaveLoad_RoundTrip()
    {
        using var tmpDir = new TempHome();
        var cfg = new UserConfig();
        cfg.Models["fast"] = "gpt-4o-mini";
        cfg.DefaultModel = "fast";
        cfg.Defaults.Temperature = 0.3f;
        cfg.Save();

        var loaded = UserConfig.Load();
        Assert.Equal("gpt-4o-mini", loaded.Models["fast"]);
        Assert.Equal("fast", loaded.DefaultModel);
        Assert.Equal(0.3f, loaded.Defaults.Temperature);
    }

    [Fact]
    public void UserConfig_LoadExplicitPath_ReturnsExplicitConfig()
    {
        using var tmpDir = new TempHome();
        var explicitPath = Path.Combine(Path.GetTempPath(), $"az-cfg-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(explicitPath,
                "{\"models\":{\"special\":\"gpt-5\"},\"default_model\":\"special\"}");
            var cfg = UserConfig.Load(explicitPath);
            Assert.Equal("gpt-5", cfg.Models["special"]);
            Assert.Equal("special", cfg.DefaultModel);
            Assert.Equal(explicitPath, cfg.LoadedFrom);
        }
        finally
        {
            if (File.Exists(explicitPath)) File.Delete(explicitPath);
        }
    }

    [Fact]
    public void UserConfig_Load_MissingFile_ReturnsEmptyDefaults()
    {
        using var tmpDir = new TempHome();
        var cfg = UserConfig.Load();
        Assert.Empty(cfg.Models);
        Assert.Null(cfg.DefaultModel);
        Assert.Null(cfg.LoadedFrom);
    }

    // ── --config CRUD dispatch (end-to-end through HandleConfigSubcommand) ─────

    [Fact]
    public void HandleConfigSubcommand_Set_PersistsKey()
    {
        using var tmpDir = new TempHome();
        using var stdout = new StringWriter();
        var oldOut = Console.Out;
        try
        {
            Console.SetOut(stdout);
            var opts = Program.ParseArgs(["--config", "set", "default_model=fast"]);
            var cfg = UserConfig.Load();
            int rc = Program.HandleConfigSubcommand(opts, cfg);
            Assert.Equal(0, rc);
            Assert.Equal("fast", UserConfig.Load().DefaultModel);
        }
        finally { Console.SetOut(oldOut); }
    }

    [Fact]
    public void HandleConfigSubcommand_Get_EmitsValue()
    {
        using var tmpDir = new TempHome();
        var cfg = new UserConfig { DefaultModel = "fast" };
        cfg.Save();

        using var stdout = new StringWriter();
        var oldOut = Console.Out;
        try
        {
            Console.SetOut(stdout);
            var opts = Program.ParseArgs(["--config", "get", "default_model"]);
            int rc = Program.HandleConfigSubcommand(opts, UserConfig.Load());
            Assert.Equal(0, rc);
            Assert.Contains("fast", stdout.ToString());
        }
        finally { Console.SetOut(oldOut); }
    }

    [Fact]
    public void HandleConfigSubcommand_Get_MissingKey_ReturnsExit1()
    {
        using var tmpDir = new TempHome();
        using var err = new StringWriter();
        var oldErr = Console.Error;
        try
        {
            Console.SetError(err);
            var opts = Program.ParseArgs(["--config", "get", "default_model"]);
            int rc = Program.HandleConfigSubcommand(opts, new UserConfig());
            Assert.Equal(1, rc);
        }
        finally { Console.SetError(oldErr); }
    }

    [Fact]
    public void HandleConfigSubcommand_Reset_DeletesFile()
    {
        using var tmpDir = new TempHome();
        var cfg = new UserConfig { DefaultModel = "fast" };
        cfg.Save();
        Assert.True(File.Exists(UserConfig.DefaultPath));

        var opts = Program.ParseArgs(["--config", "reset"]);
        var loaded = UserConfig.Load();
        int rc = Program.HandleConfigSubcommand(opts, loaded);
        Assert.Equal(0, rc);
        Assert.False(File.Exists(UserConfig.DefaultPath));
    }

    [Fact]
    public void HandleConfigSubcommand_List_EmitsAllKeys()
    {
        using var tmpDir = new TempHome();
        var cfg = new UserConfig { DefaultModel = "fast" };
        cfg.Models["fast"] = "gpt-4o-mini";
        cfg.Save();

        using var stdout = new StringWriter();
        var oldOut = Console.Out;
        try
        {
            Console.SetOut(stdout);
            var opts = Program.ParseArgs(["--config", "list"]);
            int rc = Program.HandleConfigSubcommand(opts, UserConfig.Load());
            Assert.Equal(0, rc);
            var output = stdout.ToString();
            Assert.Contains("default_model=fast", output);
            Assert.Contains("models.fast=gpt-4o-mini", output);
        }
        finally { Console.SetOut(oldOut); }
    }

    // ── Persona wiring (H3+H4): --persona picks config, persists memory ────────

    [Fact]
    public void Persona_Flag_IsParsed()
    {
        var opts = Program.ParseArgs(["--persona", "coder", "prompt"]);
        Assert.Equal("coder", opts.Persona);
    }

    [Fact]
    public void Persona_Auto_IsParsed()
    {
        var opts = Program.ParseArgs(["--persona", "auto", "prompt"]);
        Assert.Equal("auto", opts.Persona);
    }

    [Fact]
    public void SquadCoordinator_Route_PicksHighestScore()
    {
        // Uses v2 SquadCoordinator directly — verifies routing logic works end-to-end
        var cfg = new AzureOpenAI_CLI_V2.Squad.SquadConfig();
        cfg.Personas.Add(new AzureOpenAI_CLI_V2.Squad.PersonaConfig
        {
            Name = "coder",
            Role = "Coder",
            SystemPrompt = "You write code.",
        });
        cfg.Personas.Add(new AzureOpenAI_CLI_V2.Squad.PersonaConfig
        {
            Name = "reviewer",
            Role = "Reviewer",
            SystemPrompt = "You review code.",
        });
        cfg.Routing.Add(new AzureOpenAI_CLI_V2.Squad.RoutingRule
        {
            Pattern = "code,implement,function",
            Persona = "coder",
        });
        cfg.Routing.Add(new AzureOpenAI_CLI_V2.Squad.RoutingRule
        {
            Pattern = "review,audit",
            Persona = "reviewer",
        });

        var coord = new AzureOpenAI_CLI_V2.Squad.SquadCoordinator(cfg);
        Assert.Equal("coder", coord.Route("please implement this function")?.Name);
        Assert.Equal("reviewer", coord.Route("review my pull request")?.Name);
    }

    [Fact]
    public void PersonaMemory_ReadHistory_EmptyWhenMissing()
    {
        using var tmpDir = new TempCwd();
        var mem = new AzureOpenAI_CLI_V2.Squad.PersonaMemory();
        Assert.Equal(string.Empty, mem.ReadHistory("nonexistent"));
    }

    [Fact]
    public void PersonaMemory_AppendReadRoundTrip()
    {
        using var tmpDir = new TempCwd();
        var mem = new AzureOpenAI_CLI_V2.Squad.PersonaMemory();
        mem.AppendHistory("coder", "task 1", "wrote a for loop");
        mem.AppendHistory("coder", "task 2", "added unit test");
        var history = mem.ReadHistory("coder");
        Assert.Contains("wrote a for loop", history);
        Assert.Contains("added unit test", history);
    }

    // ── Kramer H1 dedupe: DefaultOptions round-trips ───────────────────────────

    [Fact]
    public void ParseArgs_NoArgs_AllNewFieldsAtDefault()
    {
        var opts = Program.ParseArgs([]);
        Assert.False(opts.VersionShort);
        Assert.Null(opts.Schema);
        Assert.Equal(5, opts.MaxRounds);
        Assert.Null(opts.ConfigPath);
        Assert.Null(opts.CompletionsShell);
        Assert.False(opts.ListModels);
        Assert.False(opts.CurrentModel);
        Assert.Null(opts.SetModelSpec);
        Assert.Null(opts.ConfigSubcommand);
        Assert.Null(opts.ConfigKey);
        Assert.Null(opts.ConfigValue);
    }

    [Fact]
    public void ParseArgs_PreservesExistingFrankMortyFields()
    {
        // Regression: make sure the Kramer H1 rewrite didn't drop the recently
        // added EnableTelemetry / Json / Estimate / EstimateOutputMax fields.
        var opts = Program.ParseArgs(["--telemetry", "--json", "--estimate-with-output", "1000", "prompt"]);
        Assert.True(opts.EnableTelemetry);
        Assert.True(opts.Json);
        Assert.True(opts.Estimate);
        Assert.Equal(1000, opts.EstimateOutputMax);
    }
}

/// <summary>
/// Redirect HOME (and USERPROFILE on Windows) to a scratch dir so UserConfig tests
/// don't touch the real user config. Disposes on exit.
/// </summary>
internal sealed class TempHome : IDisposable
{
    private readonly string _homeBackup;
    private readonly string _userProfileBackup;
    private readonly string _dir;

    public TempHome()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"az-home-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        _homeBackup = Environment.GetEnvironmentVariable("HOME") ?? "";
        _userProfileBackup = Environment.GetEnvironmentVariable("USERPROFILE") ?? "";
        Environment.SetEnvironmentVariable("HOME", _dir);
        Environment.SetEnvironmentVariable("USERPROFILE", _dir);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("HOME", _homeBackup);
        Environment.SetEnvironmentVariable("USERPROFILE", _userProfileBackup);
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }
}

/// <summary>Redirects cwd so PersonaMemory tests don't pollute the repo.</summary>
internal sealed class TempCwd : IDisposable
{
    private readonly string _backup;
    private readonly string _dir;

    public TempCwd()
    {
        _backup = Directory.GetCurrentDirectory();
        _dir = Path.Combine(Path.GetTempPath(), $"az-cwd-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        Directory.SetCurrentDirectory(_dir);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_backup);
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }
}
