using AzureOpenAI_CLI;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// Tests for REPL session lifecycle behaviors as specified in the Interactive REPL Mode proposal.
/// These tests verify session start, message handling, history management, and clean exit behaviors.
/// </summary>
public class ReplSessionTests
{
    #region Positive Tests - Session Lifecycle

    [Fact]
    public void Start_WithSystemPrompt_SessionIsActive()
    {
        var session = new ReplSession();
        session.Start("You are a helpful assistant");

        Assert.True(session.IsActive);
        Assert.Equal("You are a helpful assistant", session.SystemPrompt);
    }

    [Fact]
    public void Start_WithoutSystemPrompt_SessionIsActiveWithNoSystemPrompt()
    {
        var session = new ReplSession();
        session.Start();

        Assert.True(session.IsActive);
        Assert.Null(session.SystemPrompt);
    }

    [Fact]
    public void AddUserMessage_WhenActive_MessageIsAdded()
    {
        var session = new ReplSession();
        session.Start();

        session.AddUserMessage("Hello");

        Assert.Single(session.Messages);
        Assert.Equal("user", session.Messages[0].Role);
        Assert.Equal("Hello", session.Messages[0].Content);
    }

    [Fact]
    public void AddAssistantMessage_WhenActive_MessageIsAdded()
    {
        var session = new ReplSession();
        session.Start();

        session.AddAssistantMessage("Hello! How can I help you?");

        Assert.Single(session.Messages);
        Assert.Equal("assistant", session.Messages[0].Role);
        Assert.Equal("Hello! How can I help you?", session.Messages[0].Content);
    }

    [Fact]
    public void SessionPreservesConversationContext()
    {
        var session = new ReplSession();
        session.Start("You are helpful");

        session.AddUserMessage("What is 2+2?");
        session.AddAssistantMessage("4");
        session.AddUserMessage("And plus 3?");
        session.AddAssistantMessage("7");

        // System + 4 messages = 5 total
        Assert.Equal(5, session.Messages.Count);
        Assert.Equal("system", session.Messages[0].Role);
        Assert.Equal("user", session.Messages[1].Role);
        Assert.Equal("assistant", session.Messages[2].Role);
    }

    [Fact]
    public void End_ReturnsCleanExitWithZeroCode()
    {
        var session = new ReplSession();
        session.Start();

        var result = session.End("quit");

        Assert.Equal(0, result.ExitCode);
        Assert.True(result.WasClean);
        Assert.Equal("quit", result.ExitReason);
        Assert.False(session.IsActive);
    }

    [Fact]
    public void End_WithCtrlD_ReturnsCleanExit()
    {
        var session = new ReplSession();
        session.Start();

        var result = session.End("Ctrl+D");

        Assert.Equal(0, result.ExitCode);
        Assert.True(result.WasClean);
    }

    #endregion

    #region Negative Tests - Error Conditions

    [Fact]
    public void AddUserMessage_WhenNotActive_ThrowsInvalidOperationException()
    {
        var session = new ReplSession();

        Assert.Throws<InvalidOperationException>(() => session.AddUserMessage("Hello"));
    }

    [Fact]
    public void AddAssistantMessage_WhenNotActive_ThrowsInvalidOperationException()
    {
        var session = new ReplSession();

        Assert.Throws<InvalidOperationException>(() => session.AddAssistantMessage("Hello"));
    }

    [Fact]
    public void SetSystemPrompt_WhenNotActive_ThrowsInvalidOperationException()
    {
        var session = new ReplSession();

        Assert.Throws<InvalidOperationException>(() => session.SetSystemPrompt("New prompt"));
    }

    [Fact]
    public void ClearHistory_WhenNotActive_ThrowsInvalidOperationException()
    {
        var session = new ReplSession();

        Assert.Throws<InvalidOperationException>(() => session.ClearHistory());
    }

    [Fact]
    public void EndWithError_ReturnsNonZeroExitCode()
    {
        var session = new ReplSession();
        session.Start();

        var result = session.EndWithError("API connection failed");

        Assert.Equal(1, result.ExitCode);
        Assert.False(result.WasClean);
        Assert.Equal("API connection failed", result.ExitReason);
        Assert.False(session.IsActive);
    }

    #endregion

    #region System Prompt Tests

    [Fact]
    public void SetSystemPrompt_DuringSession_UpdatesSystemPrompt()
    {
        var session = new ReplSession();
        session.Start("Initial prompt");

        session.SetSystemPrompt("Updated prompt");

        Assert.Equal("Updated prompt", session.SystemPrompt);
        Assert.Equal("Updated prompt", session.Messages.First(m => m.Role == "system").Content);
    }

    [Fact]
    public void SetSystemPrompt_WhenNoInitialPrompt_AddsSystemMessage()
    {
        var session = new ReplSession();
        session.Start();

        session.SetSystemPrompt("New system prompt");

        Assert.Equal("New system prompt", session.SystemPrompt);
        Assert.Single(session.Messages, m => m.Role == "system");
    }

    #endregion

    #region History Management Tests

    [Fact]
    public void GetHistory_ReturnsRecentTurns()
    {
        var session = new ReplSession();
        session.Start("System");

        for (int i = 0; i < 5; i++)
        {
            session.AddUserMessage($"User message {i}");
            session.AddAssistantMessage($"Assistant message {i}");
        }

        var history = session.GetHistory(3);

        // Should return last 3 turns (6 messages)
        Assert.Equal(6, history.Count);
        Assert.Contains(history, m => m.Content == "User message 4");
        Assert.Contains(history, m => m.Content == "Assistant message 4");
    }

    [Fact]
    public void ClearHistory_KeepsSystemPrompt()
    {
        var session = new ReplSession();
        session.Start("System prompt");
        session.AddUserMessage("User message");
        session.AddAssistantMessage("Assistant message");

        session.ClearHistory();

        Assert.Single(session.Messages);
        Assert.Equal("system", session.Messages[0].Role);
        Assert.Equal("System prompt", session.Messages[0].Content);
    }

    [Fact]
    public void ClearHistory_WithNoSystemPrompt_ClearsAllMessages()
    {
        var session = new ReplSession();
        session.Start();
        session.AddUserMessage("User message");
        session.AddAssistantMessage("Assistant message");

        session.ClearHistory();

        Assert.Empty(session.Messages);
    }

    #endregion

    #region Exit Command Tests

    [Theory]
    [InlineData("exit", true)]
    [InlineData("EXIT", true)]
    [InlineData("/quit", true)]
    [InlineData("/QUIT", true)]
    [InlineData("/exit", true)]
    public void IsExitCommand_WithExitCommands_ReturnsTrue(string input, bool expected)
    {
        var result = ReplSession.IsExitCommand(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("hello", false)]
    [InlineData("quit", false)]  // quit without slash is not an exit command
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("/help", false)]
    public void IsExitCommand_WithNonExitInput_ReturnsFalse(string? input, bool expected)
    {
        var result = ReplSession.IsExitCommand(input);
        Assert.Equal(expected, result);
    }

    #endregion

    #region Turn Count and Limit Tests

    [Fact]
    public void GetTurnCount_CountsUserMessages()
    {
        var session = new ReplSession();
        session.Start();

        session.AddUserMessage("Message 1");
        session.AddAssistantMessage("Response 1");
        session.AddUserMessage("Message 2");
        session.AddAssistantMessage("Response 2");

        Assert.Equal(2, session.GetTurnCount());
    }

    [Fact]
    public void IsNearLimit_WhenFarFromLimit_ReturnsFalse()
    {
        var session = new ReplSession();
        session.Start();

        for (int i = 0; i < 10; i++)
        {
            session.AddUserMessage($"Message {i}");
        }

        Assert.False(session.IsNearLimit());
    }

    #endregion
}
