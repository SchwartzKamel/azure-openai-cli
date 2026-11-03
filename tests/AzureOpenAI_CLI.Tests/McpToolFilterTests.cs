using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using AzureOpenAI_CLI.Mcp;
using AzureOpenAI_CLI.Tools;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// S04E22 -- the MCP client used to hand the model every tool a server listed, including ones that delete or deploy.
/// <c>--mcp-allow</c> and <c>--mcp-deny</c> decide what the model is told about; a tool it is not told about cannot be called.
/// </summary>
public class McpToolFilterTests
{
    // ── The glob ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("read_file", "read_file", true)]
    [InlineData("read_file", "read_files", false)]
    [InlineData("read_*", "read_file", true)]
    [InlineData("read_*", "read_", true)]          // * matches the empty run
    [InlineData("read_*", "reader", false)]
    [InlineData("*_run", "janitor_run", true)]
    [InlineData("*_run", "janitor_runner", false)]
    [InlineData("*", "anything", true)]
    [InlineData("*", "", true)]
    [InlineData("a?c", "abc", true)]
    [InlineData("a?c", "ac", false)]               // ? is exactly one character
    [InlineData("a?c", "abbc", false)]
    [InlineData("*a*b*", "xxaxxbxx", true)]
    [InlineData("*a*b*", "xxbxxaxx", false)]       // order matters
    [InlineData("Read_File", "read_file", false)]  // tool names are case-sensitive
    [InlineData("", "x", false)]
    [InlineData("", "", true)]
    public void GlobMatch_Table(string pattern, string text, bool expected)
        => Assert.Equal(expected, McpToolFilter.GlobMatch(pattern, text));

    [Fact]
    public void GlobMatch_PathologicalPattern_FinishesFast()
    {
        // The classic catastrophic-backtracking shape. A regex translation of this takes effectively forever.
        var text = new string('a', 20000);
        var sw = Stopwatch.StartNew();
        var matched = McpToolFilter.GlobMatch("*a*a*a*a*a*a*a*a*b", text);
        sw.Stop();

        Assert.False(matched);
        Assert.True(sw.ElapsedMilliseconds < 2000, $"took {sw.ElapsedMilliseconds} ms");
    }

    // ── The policy ──────────────────────────────────────────────────────────────

    private static readonly string[] Seven = { "delegate_task", "get_clipboard", "get_datetime", "queue_add", "queue_status", "read_file", "web_fetch" };

    private static string[] Kept(McpToolFilter f) => Seven.Where(f.Allows).ToArray();

    [Fact]
    public void NoPatterns_ExposesEverything_TheHistoricalBehaviour()
    {
        var f = new McpToolFilter(null, null);

        Assert.False(f.IsActive);
        Assert.Equal(Seven, Kept(f));
    }

    [Fact]
    public void AllowList_KeepsOnlyWhatMatches()
    {
        var f = new McpToolFilter(new[] { "get_*", "read_file" }, null);

        Assert.True(f.IsActive);
        Assert.Equal(new[] { "get_clipboard", "get_datetime", "read_file" }, Kept(f));
    }

    [Fact]
    public void DenyList_HidesWhatMatches_AndKeepsTheRest()
    {
        var f = new McpToolFilter(null, new[] { "queue_*", "web_fetch" });

        Assert.Equal(new[] { "delegate_task", "get_clipboard", "get_datetime", "read_file" }, Kept(f));
    }

    [Fact]
    public void DenyAlwaysWins_EvenOverAnAllowThatMatchesTheSameTool()
    {
        var f = new McpToolFilter(new[] { "*" }, new[] { "read_file" });

        Assert.DoesNotContain("read_file", Kept(f));
        Assert.Equal(6, Kept(f).Length);
    }

    [Fact]
    public void AnAllowListThatMatchesNothing_ExposesNothing_NotEverything()
    {
        // The unsafe failure would be falling back to "no filter" when the allow list is empty of matches.
        var f = new McpToolFilter(new[] { "nonexistent_*" }, null);

        Assert.Empty(Kept(f));
    }

    [Fact]
    public void BlankAndDuplicatePatterns_AreIgnored_NotTreatedAsMatchAll()
    {
        // A blank --mcp-allow must not become an active filter that allows nothing, and a blank deny must not hide everything.
        var f = new McpToolFilter(new[] { "  ", "" }, new[] { " ", "" });

        Assert.False(f.IsActive);
        Assert.Equal(Seven, Kept(f));
        Assert.Single(new McpToolFilter(new[] { "a", "a", " a " }, null).AllowPatterns);
    }

    [Fact]
    public void UnmatchedPatterns_ReportsTheTypo_ForDenyAndAllow()
    {
        var f = new McpToolFilter(new[] { "read_file", "reed_*" }, new[] { "web_fech", "queue_*" });

        var unmatched = f.UnmatchedPatterns(Seven);

        Assert.Equal(2, unmatched.Count);
        Assert.Contains(("--mcp-deny", "web_fech"), unmatched);
        Assert.Contains(("--mcp-allow", "reed_*"), unmatched);
    }

    [Fact]
    public void UnmatchedPatterns_AMatchOnAnyServerCounts()
    {
        var f = new McpToolFilter(null, new[] { "janitor_run" });

        Assert.Single(f.UnmatchedPatterns(Seven));                                   // not on this server...
        Assert.Empty(f.UnmatchedPatterns(Seven.Concat(new[] { "janitor_run" })));    // ...but on another one
    }

    // ── The flags ───────────────────────────────────────────────────────────────

    [Fact]
    public void ParseArgs_AllowAndDeny_AreRepeatable()
    {
        var o = Program.ParseArgs(new[] { "--mcp-server", "x", "--mcp-allow", "get_*", "--mcp-allow", "read_file", "--mcp-deny", "*_run", "hi" });

        Assert.False(o.ParseError);
        Assert.Equal(new[] { "get_*", "read_file" }, o.McpAllow);
        Assert.Equal(new[] { "*_run" }, o.McpDeny);
    }

    [Fact]
    public void ParseArgs_NoFlags_LeavesBothNull()
    {
        var o = Program.ParseArgs(new[] { "hi" });

        Assert.Null(o.McpAllow);
        Assert.Null(o.McpDeny);
    }

    [Theory]
    [InlineData("--mcp-allow")]
    [InlineData("--mcp-deny")]
    public void ParseArgs_AFlagWithNoValue_IsAnError(string flag)
        => Assert.True(Program.ParseArgs(new[] { flag }).ParseError);

    // ── Enforcement: the real client against the real server ────────────────────

    [Fact]
    public async Task TheClient_ExposesOnlyAllowedTools_ToTheModel_AndStillReportsWhatTheServerOffered()
    {
        using var c2sRead = new AnonymousPipeServerStream(PipeDirection.In);
        using var c2sWrite = new AnonymousPipeClientStream(PipeDirection.Out, c2sRead.GetClientHandleAsString());
        using var s2cRead = new AnonymousPipeServerStream(PipeDirection.In);
        using var s2cWrite = new AnonymousPipeClientStream(PipeDirection.Out, s2cRead.GetClientHandleAsString());
        using var serverIn = new StreamReader(c2sRead, new UTF8Encoding(false));
        using var serverOut = new StreamWriter(s2cWrite, new UTF8Encoding(false)) { AutoFlush = true };
        using var clientIn = new StreamReader(s2cRead, new UTF8Encoding(false));
        using var clientOut = new StreamWriter(c2sWrite, new UTF8Encoding(false)) { AutoFlush = true };
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var serverTask = Task.Run(() => McpStdioServer.RunAsync(serverIn, serverOut, allowShell: false, readOnly: false,
            explicitTools: null, pluginOptions: new PluginLoadOptions(NoPlugins: true), ct: cts.Token));
        using var client = await McpStdioClient.ConnectStreamsAsync("filter-dogfood", clientIn, clientOut, cts.Token);

        var filter = new McpToolFilter(new[] { "get_datetime" }, new[] { "get_clipboard" });
        var (exposed, offered) = await client.CreateMafToolsAsync(filter, cts.Token);

        Assert.Equal(new[] { "get_datetime" }, exposed.Select(t => t.Name).ToArray());
        Assert.Contains("read_file", offered);          // the server offered it...
        Assert.DoesNotContain(exposed, t => t.Name == "read_file"); // ...but the model is never told
        Assert.True(offered.Count > exposed.Count);

        // Without a filter, the same call exposes everything, as before.
        var (all, _) = await client.CreateMafToolsAsync((McpToolFilter?)null, cts.Token);
        Assert.Equal(offered.Count, all.Count);

        clientOut.Close();
        Assert.Equal(0, await serverTask);
    }
}
