using AzureOpenAI_CLI;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// Tests for slash command parsing as specified in the Interactive REPL Mode proposal.
/// These tests verify that slash commands are correctly identified, parsed, and validated.
/// </summary>
public class SlashCommandParserTests
{
    #region Positive Tests - Valid Commands

    [Theory]
    [InlineData("/help")]
    [InlineData("/HELP")]
    [InlineData("/Help")]
    public void Parse_ValidHelpCommand_ReturnsValidSlashCommand(string input)
    {
        var result = SlashCommandParser.Parse(input);

        Assert.True(result.IsSlashCommand);
        Assert.True(result.IsValid);
        Assert.Equal("help", result.CommandName);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Parse_SystemCommandWithArguments_ReturnsValidSlashCommandWithArguments()
    {
        var result = SlashCommandParser.Parse("/system You are a helpful assistant");

        Assert.True(result.IsSlashCommand);
        Assert.True(result.IsValid);
        Assert.Equal("system", result.CommandName);
        Assert.Equal("You are a helpful assistant", result.Arguments);
        Assert.Null(result.ErrorMessage);
    }

    [Theory]
    [InlineData("/history", "history")]
    [InlineData("/clear", "clear")]
    [InlineData("/quit", "quit")]
    [InlineData("/exit", "exit")]
    public void Parse_ValidCommandsWithoutArguments_ReturnsValidSlashCommand(string input, string expectedCommand)
    {
        var result = SlashCommandParser.Parse(input);

        Assert.True(result.IsSlashCommand);
        Assert.True(result.IsValid);
        Assert.Equal(expectedCommand, result.CommandName);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Parse_ExportCommandWithFilename_ReturnsValidSlashCommandWithFilename()
    {
        var result = SlashCommandParser.Parse("/export session.json");

        Assert.True(result.IsSlashCommand);
        Assert.True(result.IsValid);
        Assert.Equal("export", result.CommandName);
        Assert.Equal("session.json", result.Arguments);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Parse_ImportCommandWithFilename_ReturnsValidSlashCommandWithFilename()
    {
        var result = SlashCommandParser.Parse("/import session.json");

        Assert.True(result.IsSlashCommand);
        Assert.True(result.IsValid);
        Assert.Equal("import", result.CommandName);
        Assert.Equal("session.json", result.Arguments);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Parse_MergeCommand_ReturnsValidSlashCommand()
    {
        var result = SlashCommandParser.Parse("/merge imported_session.json");

        Assert.True(result.IsSlashCommand);
        Assert.True(result.IsValid);
        Assert.Equal("merge", result.CommandName);
        Assert.Equal("imported_session.json", result.Arguments);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Parse_CommandWithLeadingWhitespace_ReturnsValidSlashCommand()
    {
        var result = SlashCommandParser.Parse("  /help  ");

        Assert.True(result.IsSlashCommand);
        Assert.True(result.IsValid);
        Assert.Equal("help", result.CommandName);
        Assert.Null(result.ErrorMessage);
    }

    #endregion

    #region Negative Tests - Invalid Commands

    [Theory]
    [InlineData("/unknown")]
    [InlineData("/invalidcommand")]
    [InlineData("/foo")]
    public void Parse_UnknownCommand_ReturnsInvalidSlashCommand(string input)
    {
        var result = SlashCommandParser.Parse(input);

        Assert.True(result.IsSlashCommand);
        Assert.False(result.IsValid);
        Assert.Contains("Unknown command", result.ErrorMessage);
        Assert.Contains("/help", result.ErrorMessage);
    }

    [Fact]
    public void Parse_EmptySlashCommand_ReturnsInvalidSlashCommand()
    {
        var result = SlashCommandParser.Parse("/");

        Assert.True(result.IsSlashCommand);
        Assert.False(result.IsValid);
        Assert.Contains("Empty command", result.ErrorMessage);
    }

    [Fact]
    public void Parse_SlashWithOnlyWhitespace_ReturnsInvalidSlashCommand()
    {
        var result = SlashCommandParser.Parse("/   ");

        Assert.True(result.IsSlashCommand);
        Assert.False(result.IsValid);
        Assert.Contains("Empty command", result.ErrorMessage);
    }

    #endregion

    #region Non-Slash Command Tests - Regular Input

    [Fact]
    public void Parse_RegularText_ReturnsNonSlashCommand()
    {
        var result = SlashCommandParser.Parse("Hello, how are you?");

        Assert.False(result.IsSlashCommand);
    }

    [Fact]
    public void Parse_NullInput_ReturnsNonSlashCommand()
    {
        var result = SlashCommandParser.Parse(null);

        Assert.False(result.IsSlashCommand);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsNonSlashCommand()
    {
        var result = SlashCommandParser.Parse("");

        Assert.False(result.IsSlashCommand);
    }

    [Fact]
    public void Parse_WhitespaceOnly_ReturnsNonSlashCommand()
    {
        var result = SlashCommandParser.Parse("   ");

        Assert.False(result.IsSlashCommand);
    }

    [Fact]
    public void Parse_SlashInMiddleOfText_ReturnsNonSlashCommand()
    {
        var result = SlashCommandParser.Parse("This is a path: /usr/bin/local");

        Assert.False(result.IsSlashCommand);
    }

    #endregion

    #region GetValidCommands Tests

    [Fact]
    public void GetValidCommands_ReturnsExpectedCommands()
    {
        var commands = SlashCommandParser.GetValidCommands();

        Assert.Contains("help", commands, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("system", commands, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("history", commands, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("clear", commands, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("export", commands, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("import", commands, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("quit", commands, StringComparer.OrdinalIgnoreCase);
    }

    #endregion
}
