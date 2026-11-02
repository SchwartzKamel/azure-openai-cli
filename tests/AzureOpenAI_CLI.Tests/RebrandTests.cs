using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using AzureOpenAI_CLI.Commander;
using AzureOpenAI_CLI.Tools;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// FR-024 / ADR-016 stage 1: the command is <c>hopper</c> and <c>az-ai</c> stays as an alias.
///
/// Two halves, and the second is the one that protects users. Everything a person reads or types
/// names the new command; nothing on disk moves, because a renamed config directory would orphan
/// every existing user's saved setup without a word of warning.
/// </summary>
[Collection("ConsoleCapture")]
public class RebrandTests
{
    private static string CaptureStdout(Action action)
    {
        var old = Console.Out;
        using var sw = new StringWriter();
        try
        {
            Console.SetOut(sw);
            action();
            return sw.ToString();
        }
        finally { Console.SetOut(old); }
    }

    private static string Help() => CaptureStdout(() =>
        typeof(Program).GetMethod("ShowHelp", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, null));

    private static string Completions(string shell) => CaptureStdout(() => Assert.Equal(0, Program.EmitCompletions(shell)));

    // ── What a person reads ─────────────────────────────────────────────────────

    [Fact]
    public void Help_LeadsWithHopper_AndNamesTheFormerCommand()
    {
        var lines = Help().Split('\n');
        // The version in the banner is the real one, not a hard-coded string that goes stale.
        Assert.StartsWith($"hopper (v{Program.VersionSemver})", lines[0]);
        Assert.Contains("formerly az-ai", lines[1]);
        Assert.Contains("az-ai command still works", lines[1]);
    }

    [Fact]
    public void Help_NeverTellsTheUserToRunTheOldCommand()
    {
        // Strip the one sentence that is supposed to name the old command, and the two on-disk
        // locations that legitimately keep it. Whatever az-ai is left is an instruction to type it.
        var text = string.Join('\n', Help().Split('\n').Skip(2));
        text = text.Replace(".az-ai/queue.json", "").Replace("~/.config/az-ai", "");
        Assert.DoesNotContain("az-ai", text);
    }

    // ── What a person types: both names complete ────────────────────────────────

    [Fact]
    public void Completions_Bash_RegistersBothNamesOnOneLine()
    {
        var script = Completions("bash");
        Assert.Contains("complete -F _hopper_completions hopper az-ai", script);
    }

    [Fact]
    public void Completions_Zsh_RegistersBothNames()
    {
        var script = Completions("zsh");
        Assert.StartsWith("#compdef hopper az-ai", script);
        Assert.Contains("compdef _hopper hopper az-ai", script);
    }

    [Fact]
    public void Completions_Fish_CompletesHopper_AndWrapsTheAlias()
    {
        var script = Completions("fish");
        Assert.Contains("complete -c hopper -l agent", script);
        Assert.Contains("complete -c az-ai -w hopper", script);
        // A command must never be told to wrap itself.
        Assert.DoesNotContain("complete -c hopper -w hopper", script);
    }

    [Theory]
    [InlineData("bash")]
    [InlineData("zsh")]
    public void Completions_NeverRegisterTheSameNameTwice(string shell)
    {
        // An earlier mechanical rename left `compdef _x az-ai az-ai`; keep that from coming back.
        foreach (var line in Completions(shell).Split('\n'))
        {
            if (!line.StartsWith("complete -F") && !line.StartsWith("compdef") && !line.StartsWith("#compdef")) continue;
            var names = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1)
                .Where(t => t is "hopper" or "az-ai").ToArray();
            Assert.Equal(names.Distinct().Count(), names.Length);
        }
    }

    // ── What must NOT move: the on-disk locations of existing users ─────────────

    [Fact]
    public void EnvFile_StaysInTheOldConfigDirectory()
    {
        var dir = Path.GetFileName(Path.GetDirectoryName(WizardSession.DefaultEnvFilePath()));
        Assert.Equal("az-ai", dir);
    }

    [Fact]
    public void PreferencesFile_StaysInTheOldConfigDirectory()
    {
        var dir = Path.GetFileName(Path.GetDirectoryName(Preferences.DefaultPath()));
        Assert.Equal("az-ai", dir);
    }

    [Fact]
    public void QueueFile_StaysInTheOldWorkingDirectoryFolder()
    {
        var dir = Path.GetFileName(Path.GetDirectoryName(WorkQueue.DefaultQueuePath));
        Assert.Equal(".az-ai", dir);
    }

    [Fact]
    public void ReadFileTool_StillBlocksTheOldConfigDirectory()
    {
        // The config directory holds API keys. Renaming it in the blocklist but not on disk
        // (or the reverse) would silently let the file tool read them.
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.True(ReadFileTool.IsBlockedPath(Path.Combine(home, ".config", "az-ai", "env")));
    }
}
