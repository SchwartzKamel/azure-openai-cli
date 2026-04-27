namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// Tests for UX improvements: bare subcommands (help, setup), --setup flag,
/// and improved error messages with guidance text.
/// </summary>
public class SetupAndBareSubcommandTests
{
    // ── Bare "help" subcommand ────────────────────────────────────────────

    [Fact]
    public void ParseArgs_BareHelp_SetsShowHelp()
    {
        var opts = Program.ParseArgs(["help"]);
        Assert.True(opts.ShowHelp);
        Assert.Null(opts.Prompt); // "help" should NOT become the prompt
    }

    [Fact]
    public void ParseArgs_BareHelpCaseInsensitive_SetsShowHelp()
    {
        var opts = Program.ParseArgs(["Help"]);
        Assert.True(opts.ShowHelp);
    }

    [Fact]
    public void ParseArgs_BareHelpAfterDoubleDash_IsPrompt()
    {
        // After --, "help" is literal prompt text, not a subcommand
        var opts = Program.ParseArgs(["--", "help"]);
        Assert.False(opts.ShowHelp);
        Assert.Equal("help", opts.Prompt);
    }

    // ── Bare "setup" subcommand ───────────────────────────────────────────

    [Fact]
    public void ParseArgs_BareSetup_SetsSetup()
    {
        var opts = Program.ParseArgs(["setup"]);
        Assert.True(opts.Setup);
        Assert.Null(opts.Prompt);
    }

    [Fact]
    public void ParseArgs_BareSetupCaseInsensitive_SetsSetup()
    {
        var opts = Program.ParseArgs(["Setup"]);
        Assert.True(opts.Setup);
    }

    [Fact]
    public void ParseArgs_BareSetupAfterDoubleDash_IsPrompt()
    {
        var opts = Program.ParseArgs(["--", "setup"]);
        Assert.False(opts.Setup);
        Assert.Equal("setup", opts.Prompt);
    }

    // ── --setup flag ──────────────────────────────────────────────────────

    [Fact]
    public void ParseArgs_SetupFlag_SetsSetup()
    {
        var opts = Program.ParseArgs(["--setup"]);
        Assert.True(opts.Setup);
    }

    [Fact]
    public void ParseArgs_SetupDefaultsFalse()
    {
        var opts = Program.ParseArgs([]);
        Assert.False(opts.Setup);
    }

    // ── "help" is NOT a prompt ────────────────────────────────────────────

    [Fact]
    public void ParseArgs_HelpWithOtherFlags_StillHelp()
    {
        var opts = Program.ParseArgs(["--raw", "help"]);
        Assert.True(opts.ShowHelp);
        Assert.True(opts.Raw);
    }

    // ── Help text mentions --setup ────────────────────────────────────────

    [Fact]
    public void HelpText_MentionsSetup()
    {
        var sw = new System.IO.StringWriter();
        var original = Console.Out;
        Console.SetOut(sw);
        try
        {
            // Invoke ParseArgs with --help, which sets ShowHelp.
            // ShowHelp() is a static method we can call directly via RunAsync
            // but easier to just check the output capture from a direct call.
            var method = typeof(Program).GetMethod("ShowHelp",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            method?.Invoke(null, null);
        }
        finally
        {
            Console.SetOut(original);
        }

        var output = sw.ToString();
        Assert.Contains("--setup", output);
        Assert.Contains("az-ai help", output);
        Assert.Contains("az-ai setup", output);
    }
}
