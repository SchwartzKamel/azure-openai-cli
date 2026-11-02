using System.ComponentModel;
using System.Reflection;
using AzureOpenAI_CLI.Tools;
using Xunit;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// Destructive remote git operations are refused by the shell tool. Each test states what
/// it proves so a later cleanup does not mistake it for an orphan.
/// </summary>
public class ShellExecGitSafetyTests
{
    // Proves: every direct way to force/delete/mirror-push is refused, including global
    // options before the subcommand and an absolute path to git.
    [Theory]
    [InlineData("git push --force origin main")]
    [InlineData("git push -f")]
    [InlineData("git push -fu origin topic")]
    [InlineData("git --no-pager push -f")]
    [InlineData("git -C /repo push --force-with-lease")]
    [InlineData("git -c user.name=x push -f")]
    [InlineData("/usr/bin/git push --force-with-lease=main")]
    [InlineData("git push --force-if-includes")]
    [InlineData("git push --mirror")]
    [InlineData("git push --delete origin topic")]
    [InlineData("git push -d origin topic")]
    public void Blocks_DirectDestructiveForms(string command)
    {
        Assert.True(ShellExecTool.ContainsDestructiveGitOperation(command), command);
    }

    // Proves: forms a flag-only check misses -- a '+' refspec forces the update, and a
    // leading ':' deletes the remote ref -- are refused too.
    [Theory]
    [InlineData("git push origin +main")]
    [InlineData("git push origin +refs/heads/main:refs/heads/main")]
    [InlineData("git push origin :main")]
    public void Blocks_RefspecForceAndDelete(string command)
    {
        Assert.True(ShellExecTool.ContainsDestructiveGitOperation(command), command);
    }

    // Proves: wrapping the command does not bypass the guard. sh/bash are not in
    // BlockedCommands, so a per-segment first-token check alone would let these through.
    [Theory]
    [InlineData("sh -c 'git push -f'")]
    [InlineData("bash -c \"git push --force\"")]
    [InlineData("env GIT_TERMINAL_PROMPT=0 git push -f")]
    [InlineData("ls && git push --force")]
    [InlineData("true; git push origin +main")]
    [InlineData("cat x | git push -f")]
    public void Blocks_WrappedAndChained(string command)
    {
        Assert.True(ShellExecTool.ContainsDestructiveGitOperation(command), command);
    }

    // Proves: ordinary pushes and non-push git commands are NOT refused (no false positives
    // on look-alike tokens such as a branch named feature-f or --follow-tags).
    [Theory]
    [InlineData("git push")]
    [InlineData("git push -u origin my-feature")]
    [InlineData("git push origin feature-f")]
    [InlineData("git push --follow-tags")]
    [InlineData("git push origin HEAD:refs/heads/topic")]
    [InlineData("git push --set-upstream origin topic")]
    [InlineData("git status")]
    [InlineData("git log --oneline -n 5")]
    [InlineData("git fetch --force")]
    [InlineData("git checkout -f main")]
    public void Allows_OrdinaryGit(string command)
    {
        Assert.False(ShellExecTool.ContainsDestructiveGitOperation(command), command);
    }

    // Proves: push must be the git SUBCOMMAND. A commit message that merely mentions the
    // words is not refused, so the guard does not get in the way of normal documentation.
    [Theory]
    [InlineData("git commit -m 'revert: never run git push -f here'")]
    [InlineData("git log --grep='git push --force'")]
    public void Allows_MentionsInsideOtherGitCommands(string command)
    {
        Assert.False(ShellExecTool.ContainsDestructiveGitOperation(command), command);
    }

    // Proves: the documented limit. A literal mention outside a git command (echo) is
    // refused because it cannot be told apart from `env git push --force` without a full
    // shell parser. Failing closed here costs one refused command; the reverse costs history.
    [Fact]
    public void FailsClosed_OnLiteralMentionOutsideGit()
    {
        Assert.True(ShellExecTool.ContainsDestructiveGitOperation("echo git push --force is dangerous"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Handles_EmptyInput(string? command)
    {
        Assert.False(ShellExecTool.ContainsDestructiveGitOperation(command));
    }

    // Proves: the guard is actually wired into ExecuteAsync and refuses BEFORE anything runs.
    [Fact]
    public async Task ExecuteAsync_RefusesForcePush_WithoutRunningGit()
    {
        var result = await ShellExecTool.ExecuteAsync("git push --force origin main");

        Assert.Contains("blocked", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("destructive git", result, StringComparison.OrdinalIgnoreCase);
    }

    // Proves: the tool's [Description] attribute is still attached to ExecuteAsync. This is
    // the text the model sees in the tool schema. An edit that inserts a member between the
    // attribute and the method silently moves it and nothing else fails, so pin it.
    [Fact]
    public void ExecuteAsync_KeepsItsToolDescription()
    {
        var method = typeof(ShellExecTool).GetMethod("ExecuteAsync", BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(method);
        var attr = method!.GetCustomAttribute<DescriptionAttribute>();
        Assert.NotNull(attr);
        Assert.Contains("Execute a shell command", attr!.Description);
    }
}
