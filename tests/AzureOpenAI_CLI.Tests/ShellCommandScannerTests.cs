using AzureOpenAI_CLI.Tools;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// S04E15 -- the shell_exec guard looked only at the first token of each segment. Measured against that
/// check, 30 of 32 trivial variations of a blocked command ran. These tests keep the measured list, and
/// keep an equally long list of ordinary commands that must still run, so the guard cannot get "safe" by
/// blocking everything.
/// </summary>
public class ShellCommandScannerTests
{
    // ── Every one of these ran before the scanner existed ───────────────────────

    [Theory]
    // an interpreter's -c payload
    [InlineData("bash -c 'rm /tmp/x'")]
    [InlineData("sh -c \"sudo ls\"")]
    [InlineData("bash -lc 'rm /tmp/x'")]
    [InlineData("bash -o pipefail -c 'rm /tmp/x'")]
    [InlineData("/bin/bash -c 'kill 1'")]
    [InlineData("bash -c \"bash -c 'rm /tmp/x'\"")]
    [InlineData("sh -c 'rm \"$@\"' _ /tmp/x")]
    // grouping and reserved words
    [InlineData("(rm /tmp/x)")]
    [InlineData("{ rm /tmp/x; }")]
    [InlineData("if true; then rm /tmp/x; fi")]
    [InlineData("true && (rm /tmp/x)")]
    [InlineData("! rm /tmp/x")]
    // wrappers
    [InlineData("env rm /tmp/x")]
    [InlineData("env -u HOME rm /tmp/x")]
    [InlineData("env FOO=1 rm /tmp/x")]
    [InlineData("env bash -c 'rm /tmp/x'")]
    [InlineData("env -S 'rm /tmp/x'")]
    [InlineData("nohup rm /tmp/x")]
    [InlineData("nice -n 5 rm /tmp/x")]
    [InlineData("timeout 5 rm /tmp/x")]
    [InlineData("timeout -s KILL 5 rm /tmp/x")]
    [InlineData("timeout 5 sh -c 'sudo x'")]
    [InlineData("command rm /tmp/x")]
    [InlineData("time rm /tmp/x")]
    [InlineData("setsid rm /tmp/x")]
    [InlineData("busybox rm /tmp/x")]
    [InlineData("stdbuf -o0 rm /tmp/x")]
    [InlineData("flock /tmp/lock rm /tmp/x")]
    [InlineData("echo /tmp/x | xargs rm")]
    [InlineData("echo /tmp/x | xargs -n1 rm")]
    [InlineData("echo /tmp/x | xargs -I {} rm {}")]
    [InlineData("xargs sh -c 'rm \"$@\"' _")]
    // an interpreter reading commands from stdin
    [InlineData("echo 'rm /tmp/x' | sh")]
    [InlineData("echo x | bash -s")]
    [InlineData("echo x | sh -")]
    [InlineData("sh <<< 'rm /tmp/x'")]
    [InlineData("sh < script.txt")]
    // find
    [InlineData("find /tmp/x -delete")]
    [InlineData("find /tmp -name x -exec rm {} \\;")]
    [InlineData("find /tmp -execdir rm {} +")]
    // the name, disguised
    [InlineData("\\rm /tmp/x")]
    [InlineData("r'm' /tmp/x")]
    [InlineData("\"rm\" /tmp/x")]
    [InlineData("rm${IFS}/tmp/x")]
    [InlineData("a=rm; $a /tmp/x")]
    [InlineData("/bin/r? /tmp/x")]
    [InlineData("/bin/[r]m /tmp/x")]
    [InlineData("$'rm' /tmp/x")]
    [InlineData("{rm,/tmp/x}")]
    // eval and exec in any command position, not just the first
    [InlineData("true && exec rm /tmp/x")]
    [InlineData("true | eval 'rm /tmp/x'")]
    [InlineData("(eval x)")]
    // names that were missing from the list
    [InlineData("unlink /tmp/x")]
    [InlineData("shred -u /tmp/x")]
    [InlineData("doas ls")]
    [InlineData("pkexec ls")]
    [InlineData("mkfs.ext4 /dev/x")]
    public void BlockedCommand_IsFound_HoweverItIsWritten(string command)
    {
        var refusal = ShellCommandScanner.Scan(command);

        Assert.NotNull(refusal);
        Assert.Contains("blocked for safety", refusal);
    }

    // ── What must keep working ──────────────────────────────────────────────────

    [Theory]
    [InlineData("ls -la")]
    [InlineData("git status")]
    [InlineData("git log --oneline -5")]
    [InlineData("cat file.txt | grep foo | head -5")]
    [InlineData("bash -c 'ls -la'")]
    [InlineData("sh -c 'echo hello'")]
    [InlineData("bash -c \"cd src && ls\"")]
    [InlineData("bash -o pipefail -c 'ls | head'")]
    [InlineData("bash script.sh")]
    [InlineData("bash -n script.sh")]
    [InlineData("sh ./build.sh --flag")]
    [InlineData("bash --version")]
    [InlineData("env | sort")]
    [InlineData("env FOO=1 ls")]
    [InlineData("env -i PATH=/usr/bin ls")]
    [InlineData("timeout 5 git status")]
    [InlineData("timeout -s KILL 5 ls")]
    [InlineData("time ls")]
    [InlineData("nice ls")]
    [InlineData("nice -n 5 ls")]
    [InlineData("nohup ls")]
    [InlineData("command -v rm")]
    [InlineData("command ls")]
    [InlineData("echo hi | xargs echo")]
    [InlineData("xargs -n1 echo")]
    [InlineData("echo x | xargs -I {} echo {}")]
    [InlineData("find . -name '*.cs' -print")]
    [InlineData("find . -type f | wc -l")]
    [InlineData("find . -name x -exec grep -l foo {} \\;")]
    [InlineData("grep -r kill src")]
    [InlineData("grep -rn \"rm -rf\" docs")]
    [InlineData("git commit -m 'remove rm'")]
    [InlineData("echo \"rm -rf is dangerous\"")]
    [InlineData("awk '{print $1}' file.txt")]
    [InlineData("[ -f x ] && echo yes")]
    [InlineData("if [ -f x ]; then echo yes; fi")]
    [InlineData("for f in a b; do echo $f; done")]
    [InlineData("FOO=bar ls")]
    [InlineData("ls 2>&1 | head")]
    [InlineData("echo hi > /dev/null 2>&1")]
    [InlineData("cat <<< hello")]
    [InlineData("echo hi # rm x")]
    [InlineData("./scripts/test.sh")]
    [InlineData("~/bin/tool")]
    [InlineData("wc -l *.md")]
    [InlineData("echo $HOME")]
    [InlineData("head -n 5 README.md")]
    public void OrdinaryCommand_IsNotBlocked(string command)
    {
        Assert.Null(ShellCommandScanner.Scan(command));
    }

    // ── Fail-closed limits, stated so nobody mistakes them for bugs ─────────────

    [Fact]
    public void CommandWhoseNameIsAVariable_IsRefused_EvenForAnInnocentPath()
    {
        // An unquoted expansion can word-split (X='rm '; $X/tmp/y), so $HOME/bin/tool cannot be vouched for.
        var refusal = ShellCommandScanner.Scan("$HOME/bin/tool");

        Assert.NotNull(refusal);
        Assert.Contains("variable, glob", refusal);
    }

    [Fact]
    public void UnbalancedQuote_IsRefused_NotGuessedAt()
    {
        var refusal = ShellCommandScanner.Scan("echo 'unterminated");

        Assert.NotNull(refusal);
        Assert.Contains("could not be parsed", refusal);
    }

    [Fact]
    public void DeepNesting_IsRefused()
    {
        var nested = "rm /tmp/x";
        for (var i = 0; i < 8; i++) nested = "sh -c \"" + nested.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        var refusal = ShellCommandScanner.Scan(nested);

        Assert.NotNull(refusal);
        Assert.Contains("blocked for safety", refusal);
    }

    // ── The messages say what was found ─────────────────────────────────────────

    [Fact]
    public void Refusal_NamesTheCommandInsideThePayload()
    {
        Assert.Equal("Error: command 'rm' is blocked for safety.", ShellCommandScanner.Scan("bash -c 'rm /tmp/x'"));
        Assert.Equal("Error: command 'sudo' is blocked for safety.", ShellCommandScanner.Scan("timeout 5 env sh -c 'sudo x'"));
    }

    [Fact]
    public void Refusal_ForAnInterpreterOnStdin_SaysSo()
    {
        Assert.Contains("reading commands from stdin", ShellCommandScanner.Scan("echo x | sh"));
    }

    [Fact]
    public void Refusal_ForFindDelete_SaysSo()
    {
        Assert.Contains("find -delete", ShellCommandScanner.Scan("find . -delete"));
    }

    [Fact]
    public void Refusal_ForEvalAnywhere_UsesTheExistingMessage()
    {
        Assert.Equal("Error: process substitution and eval/exec are blocked for safety.", ShellCommandScanner.Scan("true && eval x"));
    }

    // ── The tokenizer reads the line the way the shell does ─────────────────────

    private static List<string> Words(string line, int command = 0)
    {
        Assert.True(ShellCommandScanner.TryParse(line, out var commands));
        return commands[command].Select(w => w.Text).ToList();
    }

    [Theory]
    [InlineData("\\rm x", "rm")]
    [InlineData("r'm' x", "rm")]
    [InlineData("\"rm\" x", "rm")]
    [InlineData("'r''m' x", "rm")]
    public void Quoting_IsRemovedTheWayTheShellRemovesIt(string line, string expectedFirstWord)
    {
        Assert.Equal(expectedFirstWord, Words(line)[0]);
    }

    [Fact]
    public void QuotedSpaces_StayInOneWord()
    {
        Assert.Equal(new[] { "echo", "a b  c" }, Words("echo \"a b  c\""));
    }

    [Fact]
    public void Separators_StartNewCommands()
    {
        Assert.True(ShellCommandScanner.TryParse("a; b && c | d\ne", out var commands));
        Assert.Equal(5, commands.Count);
    }

    [Fact]
    public void FileDescriptorAndRedirectTarget_AreNotWords()
    {
        Assert.Equal(new[] { "ls", "-l" }, Words("ls -l 2>&1 > out.txt"));
    }

    [Fact]
    public void Comment_IsDropped_ButAHashInsideAWordIsKept()
    {
        Assert.Equal(new[] { "echo", "a#b" }, Words("echo a#b # rm x"));
    }

    [Fact]
    public void Variable_MarksTheWordDynamic()
    {
        Assert.True(ShellCommandScanner.TryParse("$a x", out var commands));
        Assert.True(commands[0][0].Dynamic);
        Assert.False(commands[0][1].Dynamic);
    }

    // ── End to end: the victim survives ─────────────────────────────────────────

    [Theory]
    [InlineData("bash -c 'rm {F}'")]
    [InlineData("sh -c \"rm {F}\"")]
    [InlineData("env rm {F}")]
    [InlineData("timeout 5 rm {F}")]
    [InlineData("echo {F} | xargs rm")]
    [InlineData("find {F} -delete")]
    [InlineData("\\rm {F}")]
    [InlineData("(rm {F})")]
    public async Task ExecuteAsync_DoesNotRunTheBlockedCommand_SoTheFileSurvives(string template)
    {
        var victim = Path.Combine(Path.GetTempPath(), "scanner-victim-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(victim, "keep me");
        try
        {
            var result = await ShellExecTool.ExecuteAsync(template.Replace("{F}", victim), CancellationToken.None);

            Assert.StartsWith("Error:", result);
            Assert.True(File.Exists(victim), "the guard returned an error but the command ran anyway");
        }
        finally { if (File.Exists(victim)) File.Delete(victim); }
    }

    [Fact]
    public async Task ExecuteAsync_StillRunsAnOrdinaryShellWrapper()
    {
        var result = await ShellExecTool.ExecuteAsync("bash -c 'echo scanner-ok'", CancellationToken.None);

        Assert.Contains("scanner-ok", result);
    }
}
