using AzureOpenAI_CLI.Tools;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// S04E17 -- the long tail. Two days after S04E15 measured "0 of 32 bypasses", an independent reviewer model
/// (gpt-5.2-chat, driven through hopper) read the scanner and claimed more. Graded mechanically against the real
/// scanner, one claim was genuinely new, and measuring the family around it found seven routes that really
/// deleted a file through the full guard: coproc, trap, taskset, chrt, strace, systemd-run and script -c.
/// These tests keep that list, and an equally long list of ordinary uses that must still work.
/// </summary>
public class ShellCommandScannerLongTailTests
{
    [Theory]
    // coproc runs a command in the background, in every spelling
    [InlineData("coproc rm /tmp/x")]
    [InlineData("bash -c 'coproc rm /tmp/x; wait'")]
    [InlineData("coproc NAME { rm /tmp/x; }")]
    [InlineData("bash -c 'coproc NAME { rm /tmp/x; }; wait'")]
    // trap runs a command STRING when the shell exits
    [InlineData("trap 'rm /tmp/x' EXIT")]
    [InlineData("trap \"sudo ls\" 0")]
    [InlineData("trap rm EXIT")]
    [InlineData("sh -c \"trap 'rm /tmp/x' EXIT\"")]
    // wrappers with argument shapes too varied to model option by option
    [InlineData("taskset -c 0 rm /tmp/x")]
    [InlineData("taskset 0x1 rm /tmp/x")]
    [InlineData("chrt -o 0 rm /tmp/x")]
    [InlineData("strace -o /dev/null rm /tmp/x")]
    [InlineData("strace -f env rm /tmp/x")]
    [InlineData("ltrace rm /tmp/x")]
    [InlineData("unshare -r rm /tmp/x")]
    [InlineData("nsenter -t 1 -m rm /tmp/x")]
    [InlineData("setpriv --reuid=0 rm /tmp/x")]
    [InlineData("systemd-run --user --wait --pipe rm /tmp/x")]
    [InlineData("systemd-run --user sh -c 'rm /tmp/x'")]
    [InlineData("script -qec 'rm /tmp/x' /dev/null")]
    [InlineData("parallel rm ::: /tmp/x")]
    // deferred execution: what arrives on stdin runs later, where nothing can read it
    [InlineData("echo 'rm /tmp/x' | at now")]
    [InlineData("batch")]
    public void ExecCapableCommand_CannotCarryABlockedCommandPastTheGuard(string command)
    {
        var refusal = ShellCommandScanner.Scan(command);

        Assert.NotNull(refusal);
        Assert.Contains("blocked for safety", refusal);
    }

    [Theory]
    [InlineData("strace -p 1234")]
    [InlineData("strace -c ls")]
    [InlineData("taskset -p 1")]
    [InlineData("taskset -c 0 ls")]
    [InlineData("chrt -p 1")]
    [InlineData("chrt -f 10 ./run.sh")]
    [InlineData("trap - INT")]
    [InlineData("trap cleanup EXIT")]
    [InlineData("trap 'echo bye' EXIT")]
    [InlineData("script -c 'ls -la' /dev/null")]
    [InlineData("systemd-run --user echo hi")]
    [InlineData("unshare --help")]
    [InlineData("nsenter --version")]
    [InlineData("parallel echo ::: a b")]
    [InlineData("ls | wc -l")]
    public void OrdinaryUseOfTheSameCommands_StillRuns(string command)
    {
        Assert.Null(ShellCommandScanner.Scan(command));
    }

    [Fact]
    public void TrapThatKillsAChild_IsRefused_AStatedFailClosedCost()
    {
        // `trap 'kill $pid' EXIT` is a common cleanup idiom. kill is on the blocklist and a trap takes a command
        // string, so it is refused. That is the price of reading the string; a legitimate need can use a function.
        Assert.NotNull(ShellCommandScanner.Scan("trap 'kill $pid' EXIT"));
    }

    [Fact]
    public void AtAndBatch_AreOnTheBlocklist()
    {
        Assert.True(ShellExecTool.IsBlockedName("at"));
        Assert.True(ShellExecTool.IsBlockedName("batch"));
    }

    // ── End to end: the victim survives. Each of these deleted the file before the change. ──

    [Theory]
    [InlineData("bash -c 'coproc rm {F}; wait'")]
    [InlineData("bash -c 'coproc NAME { rm {F}; }; wait'")]
    [InlineData("sh -c \"trap 'rm {F}' EXIT\"")]
    [InlineData("bash -c \"trap 'rm {F}' EXIT; true\"")]
    [InlineData("taskset -c 0 rm {F}")]
    [InlineData("chrt -o 0 rm {F}")]
    [InlineData("strace -o /dev/null rm {F}")]
    [InlineData("systemd-run --user --wait --pipe rm {F}")]
    [InlineData("script -qec 'rm {F}' /dev/null")]
    public async Task ExecuteAsync_RefusesTheCommand_SoTheFileSurvives(string template)
    {
        var victim = Path.Combine(Path.GetTempPath(), "longtail-victim-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(victim, "keep me");
        try
        {
            var result = await ShellExecTool.ExecuteAsync(template.Replace("{F}", victim), CancellationToken.None);

            Assert.StartsWith("Error:", result);
            Assert.True(File.Exists(victim), "the guard returned an error but the command ran anyway");
        }
        finally { if (File.Exists(victim)) File.Delete(victim); }
    }
}
