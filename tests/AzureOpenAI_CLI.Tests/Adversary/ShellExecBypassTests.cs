using System.Text.Json;
using AzureOpenAI_CLI.Tools;

namespace AzureOpenAI_CLI.Tests.Adversary;

/// <summary>
/// S02E23 -- The Adversary. FDR's bypass attempts against
/// <see cref="ShellExecTool"/>'s shell-substitution / blocked-command
/// blocklist.
///
/// Tests that the defense currently HOLDS are <c>[Fact]</c>. Tests
/// that surface a live blocklist bypass are <c>[Fact(Skip = "Live
/// bypass: e23-...")]</c> and reproduce the attack as documentation;
/// removing the Skip after a future hardening episode (Newman lead)
/// must turn the test green.
///
/// All probe commands are intentionally innocuous (echo, /bin/true,
/// wget --version) so even if a Skipped test is accidentally run
/// against an un-hardened build no destructive side effect occurs.
/// </summary>
public class ShellExecBypassTests
{
    private static JsonElement Args(string command)
    {
        var json = JsonSerializer.Serialize(new { command });
        return JsonDocument.Parse(json).RootElement;
    }

    // ===================================================================
    // Defenses that hold today (passing facts -- regression coverage)
    // ===================================================================

    [Fact]
    public async Task Bypass_DollarParenSubstitution_Rejected()
    {
        var tool = new ShellExecTool();
        var result = await tool.ExecuteAsync(Args("echo $(whoami)"), CancellationToken.None);
        Assert.StartsWith("Error:", result);
        Assert.Contains("substitution", result);
    }

    [Fact]
    public async Task Bypass_BacktickSubstitution_Rejected()
    {
        var tool = new ShellExecTool();
        var result = await tool.ExecuteAsync(Args("echo `whoami`"), CancellationToken.None);
        Assert.StartsWith("Error:", result);
        Assert.Contains("substitution", result);
    }

    [Fact]
    public async Task Bypass_ProcessSubstitutionInput_Rejected()
    {
        var tool = new ShellExecTool();
        var result = await tool.ExecuteAsync(Args("cat <(echo probe)"), CancellationToken.None);
        Assert.StartsWith("Error:", result);
        Assert.Contains("process substitution", result);
    }

    [Fact]
    public async Task Bypass_ProcessSubstitutionOutput_Rejected()
    {
        var tool = new ShellExecTool();
        var result = await tool.ExecuteAsync(Args("echo probe > >(cat)"), CancellationToken.None);
        Assert.StartsWith("Error:", result);
        Assert.Contains("process substitution", result);
    }

    [Fact]
    public async Task Bypass_EvalAtStart_Rejected()
    {
        var tool = new ShellExecTool();
        var result = await tool.ExecuteAsync(Args("eval echo probe"), CancellationToken.None);
        Assert.StartsWith("Error:", result);
        Assert.Contains("eval", result);
    }

    [Fact]
    public async Task Bypass_ExecAtStart_Rejected()
    {
        var tool = new ShellExecTool();
        var result = await tool.ExecuteAsync(Args("exec echo probe"), CancellationToken.None);
        Assert.StartsWith("Error:", result);
    }

    [Fact]
    public async Task Bypass_EvalAfterSemicolon_Rejected()
    {
        var tool = new ShellExecTool();
        var result = await tool.ExecuteAsync(Args("true; eval echo probe"), CancellationToken.None);
        Assert.StartsWith("Error:", result);
        Assert.Contains("eval", result);
    }

    [Fact]
    public async Task Bypass_PathPrefixedRm_Rejected()
    {
        // /usr/bin/rm collapses to "rm" via Split('/').LastOrDefault() in
        // ShellExecTool's tokenizer. Defense holds.
        var tool = new ShellExecTool();
        var result = await tool.ExecuteAsync(Args("/usr/bin/rm /tmp/probe-nonexistent-s02e23"), CancellationToken.None);
        Assert.StartsWith("Error:", result);
        Assert.Contains("blocked", result);
    }

    [Fact]
    public async Task Bypass_RmInSecondPipeSegment_Rejected()
    {
        // The blocklist scans every segment after split on |, ;, &
        var tool = new ShellExecTool();
        var result = await tool.ExecuteAsync(Args("echo probe | rm /tmp/probe-nonexistent-s02e23"), CancellationToken.None);
        Assert.StartsWith("Error:", result);
        Assert.Contains("blocked", result);
    }

    [Fact]
    public async Task Bypass_RmAfterSemicolon_Rejected()
    {
        var tool = new ShellExecTool();
        var result = await tool.ExecuteAsync(Args("echo probe ; rm /tmp/probe-nonexistent-s02e23"), CancellationToken.None);
        Assert.StartsWith("Error:", result);
        Assert.Contains("blocked", result);
    }

    // ===================================================================
    // LIVE BYPASSES surfaced by FDR -- Skipped, with finding name
    // ===================================================================

    [Fact]
    public async Task Bypass_EvalAfter_AndAnd_ShouldBeRejected()
    {
        // The substitution/eval check looks for the literal substring
        //   command.Contains("; eval ")
        // but NOT for "&& eval " or "|| eval ". A user can chain a
        // benign first command with && to smuggle eval past the gate.
        var tool = new ShellExecTool();
        var result = await tool.ExecuteAsync(Args("true && eval echo probe"), CancellationToken.None);
        Assert.StartsWith("Error:", result);
        Assert.Contains("eval", result);
    }

    [Fact]
    public async Task Bypass_IfsExpansionTokenization_ShouldBeRejected()
    {
        // ${IFS} expands to whitespace at shell time but reads as a
        // single non-space token at blocklist time. The first-token
        // check (Split(' ', 2)[0]) sees "rm${IFS}-rf" and the
        // BlockedCommands HashSet does not contain that string, so
        // the input passes the gate and reaches /bin/sh -c, which
        // re-tokenizes ${IFS} into a real space and executes "rm".
        var tool = new ShellExecTool();
        var result = await tool.ExecuteAsync(Args("rm${IFS}/tmp/probe-nonexistent-s02e23"), CancellationToken.None);
        Assert.StartsWith("Error:", result);
        Assert.Contains("blocked", result);
    }

    [Fact]
    public async Task Bypass_TabAsCommandSeparator_ShouldBeRejected()
    {
        // The tokenizer splits on a single ASCII space (' ') only.
        // /bin/sh treats tab as IFS, so "rm\t/tmp/x" runs rm but the
        // blocklist sees the first token as "rm\t/tmp/x".
        var tool = new ShellExecTool();
        var result = await tool.ExecuteAsync(Args("rm\t/tmp/probe-nonexistent-s02e23"), CancellationToken.None);
        Assert.StartsWith("Error:", result);
        Assert.Contains("blocked", result);
    }

    [Fact]
    public async Task Bypass_NewlineCommandSeparator_ShouldBeRejected()
    {
        // The pipe-segment split uses '|', ';', '&' but NOT '\n'.
        // /bin/sh interprets newline as a statement terminator, so
        // a newline-injected blocked command after a benign one
        // executes; the blocklist never sees it as its own segment.
        var tool = new ShellExecTool();
        var result = await tool.ExecuteAsync(Args("echo probe\nrm /tmp/probe-nonexistent-s02e23"), CancellationToken.None);
        Assert.StartsWith("Error:", result);
        Assert.Contains("blocked", result);
    }

    [Fact]
    public async Task Bypass_QuotedCommandName_ShouldBeRejected()
    {
        // Tokenizer takes the literal first token, including quote
        // characters. The HashSet contains "rm", not "\"rm\"". /bin/sh
        // strips the quotes before resolving the command name, so the
        // bypass executes the underlying tool.
        var tool = new ShellExecTool();
        var result = await tool.ExecuteAsync(Args("\"rm\" /tmp/probe-nonexistent-s02e23"), CancellationToken.None);
        Assert.StartsWith("Error:", result);
        Assert.Contains("blocked", result);
    }

    [Fact]
    public async Task Bypass_BackslashEscapedCommandName_ShouldBeRejected()
    {
        // /bin/sh allows a leading backslash as a no-op character
        // escape that disables alias expansion: \rm resolves to rm.
        // The blocklist tokenizer sees "\\rm" and finds no match.
        var tool = new ShellExecTool();
        var result = await tool.ExecuteAsync(Args("\\rm /tmp/probe-nonexistent-s02e23"), CancellationToken.None);
        Assert.StartsWith("Error:", result);
        Assert.Contains("blocked", result);
    }

    [Fact]
    public async Task Bypass_EnvVarCommandIndirection_ShouldBeRejected()
    {
        // ${RM:-rm} or $RM expands to "rm" at shell time but the
        // tokenizer reads "${RM:-rm}" or "$RM" as an unrelated
        // first token. SensitiveEnvVars only scrubs API keys; RM
        // is left intact and may even already be set in the parent.
        var tool = new ShellExecTool();
        var result = await tool.ExecuteAsync(Args("${RM:-rm} /tmp/probe-nonexistent-s02e23"), CancellationToken.None);
        Assert.StartsWith("Error:", result);
        Assert.Contains("blocked", result);
    }

    [Fact]
    public async Task Bypass_FullwidthUnicodeLookalike_ShouldBeRejected()
    {
        // Cosmetic-only attack: the fullwidth letters resolve to a
        // separate code point and /bin/sh will not find the command
        // either, so this is harmless in practice. Logged because the
        // blocklist's "comparison happens before normalization" stance
        // is worth pinning -- if a future change normalizes Unicode at
        // execution but not at gate time, the attack surface grows.
        var tool = new ShellExecTool();
        var result = await tool.ExecuteAsync(Args("\uFF52\uFF4D /tmp/probe-nonexistent-s02e23"), CancellationToken.None);
        Assert.StartsWith("Error:", result);
        Assert.Contains("blocked", result);
    }
}
