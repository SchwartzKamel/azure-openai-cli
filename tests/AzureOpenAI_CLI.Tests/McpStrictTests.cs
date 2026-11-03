using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using AzureOpenAI_CLI.Mcp;
using AzureOpenAI_CLI.Tools;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// S04E27 -- with no <c>--mcp-allow</c> the MCP client still exposed every tool a server listed, and it ignored MCP annotations
/// entirely. <c>--mcp-strict</c> exposes only tools the server declares <c>readOnlyHint: true</c>, and makes a mistyped pattern fatal.
/// The hints are the server's own claims, so they are parsed leniently (a malformed one must not break a server that worked before)
/// and a missing or odd one means "not read-only".
/// </summary>
public class McpStrictTests
{
    // ── The policy ──────────────────────────────────────────────────────────────

    [Theory]
    //   readOnly  destructive  expected
    [InlineData(true, null, true)]    // declared read-only: exposed
    [InlineData(true, false, true)]
    [InlineData(null, null, false)]   // no hint at all (the ops toolkit): hidden
    [InlineData(false, null, false)]   // declared NOT read-only: hidden
    [InlineData(null, true, false)]
    [InlineData(true, true, false)]   // contradicts itself: the safe reading is to hide it
    public void Strict_NoAllowList_ExposesOnlyWhatTheServerDeclaresReadOnly(bool? readOnly, bool? destructive, bool expected)
        => Assert.Equal(expected, new McpToolFilter(null, null, strict: true).Allows("t", readOnly, destructive));

    [Fact]
    public void Strict_DenyStillWins_OverADeclaredReadOnlyTool()
        => Assert.False(new McpToolFilter(null, new[] { "read_file" }, strict: true).Allows("read_file", true, null));

    [Fact]
    public void Strict_AnExplicitAllowList_IsTheOptIn_HintsAreNotConsulted()
    {
        var f = new McpToolFilter(new[] { "janitor_report" }, null, strict: true);

        Assert.True(f.Allows("janitor_report"));                          // no hint, but the user named it
        Assert.True(f.Allows("janitor_report", null, true));   // even a tool that claims to be destructive: named by the user
        Assert.False(f.Allows("other", true, null));              // read-only, but not named: an allow list means only those
    }

    [Fact]
    public void WithoutStrict_HintsChangeNothing()
    {
        var f = new McpToolFilter(null, null);

        Assert.True(f.Allows("a", false, null));
        Assert.True(f.Allows("b", null, null));
        Assert.True(f.Allows("c", null, true));
    }

    [Fact]
    public void StrictAlone_IsAnActiveFilter()
    {
        Assert.True(new McpToolFilter(null, null, strict: true).IsActive);
        Assert.False(new McpToolFilter(null, null).IsActive);
        Assert.True(new McpToolFilter(new[] { "a" }, null, strict: true).HasAllowList);
        Assert.False(new McpToolFilter(null, new[] { "a" }, strict: true).HasAllowList);
    }

    // ── The flag ────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseArgs_McpStrict_IsAFlag_AndDoesNotEnableAgentModeByItself()
    {
        var on = Program.ParseArgs(new[] { "--mcp-strict", "--mcp-server", "x", "hi" });
        var off = Program.ParseArgs(new[] { "hi" });
        var alone = Program.ParseArgs(new[] { "--mcp-strict", "hi" });

        Assert.True(on.McpStrict);
        Assert.False(off.McpStrict);
        Assert.False(on.ParseError);
        Assert.False(alone.AgentMode);
    }

    // ── Annotations are untrusted input: parsed leniently ───────────────────────

    private static McpToolInfo ParseTool(string annotationsJson)
    {
        var json = "{\"tools\":[{\"name\":\"t\",\"description\":\"d\",\"inputSchema\":{}" + (annotationsJson.Length > 0 ? ",\"annotations\":" + annotationsJson : "") + "}]}";
        var parsed = JsonSerializer.Deserialize(json, McpJsonContext.Default.McpToolsListResult);
        return Assert.Single(parsed!.Tools);
    }

    [Fact]
    public void Annotations_RealBooleans_AreRead()
    {
        var a = ParseTool("{\"readOnlyHint\":true,\"destructiveHint\":false}").Annotations;

        Assert.True(a!.ReadOnlyHint);
        Assert.False(a.DestructiveHint);
    }

    [Theory]
    [InlineData("{\"readOnlyHint\":\"true\"}")]            // a string, not a boolean: the sloppy-server case
    [InlineData("{\"readOnlyHint\":1}")]
    [InlineData("{\"readOnlyHint\":null}")]
    [InlineData("{\"readOnlyHint\":{\"nested\":[1,2,{\"a\":true}]}}")]
    [InlineData("{\"readOnlyHint\":[true]}")]
    public void Annotations_AMalformedHint_IsNoHint_NotAnError_AndNeverReadOnly(string json)
    {
        var tool = ParseTool(json);   // must not throw: it would break every tool of a server that worked before

        Assert.Null(tool.Annotations!.ReadOnlyHint);
        Assert.False(new McpToolFilter(null, null, strict: true).Allows("t", tool.Annotations.ReadOnlyHint, tool.Annotations.DestructiveHint));
        Assert.True(new McpToolFilter(null, null).Allows("t", tool.Annotations.ReadOnlyHint, tool.Annotations.DestructiveHint)); // non-strict: unchanged
    }

    [Theory]
    [InlineData("\"read-only\"")]
    [InlineData("42")]
    [InlineData("[true,false]")]
    [InlineData("true")]
    [InlineData("null")]
    public void Annotations_NotAnObject_IsIgnored_NotAnError(string json)
        => Assert.Null(ParseTool(json).Annotations?.ReadOnlyHint);

    [Fact]
    public void Annotations_UnknownFields_AreIgnored_AndNeighboursStillRead()
    {
        var tool = ParseTool("{\"title\":\"x\",\"openWorldHint\":true,\"readOnlyHint\":true,\"idempotentHint\":{\"a\":[1]}}");

        Assert.True(tool.Annotations!.ReadOnlyHint);
    }

    [Fact]
    public void AToolWithNoAnnotations_HasNone_AndIsNotWrittenWithANullOne()
    {
        Assert.Null(ParseTool("").Annotations);

        var json = JsonSerializer.Serialize(new McpToolsListResult(new List<McpToolInfo>
        {
            new("a", "d", JsonDocument.Parse("{}").RootElement),
            new("b", "d", JsonDocument.Parse("{}").RootElement, new McpToolAnnotations(ReadOnlyHint: true)),
        }), McpJsonContext.Default.McpToolsListResult);

        Assert.Contains("\"annotations\":{\"readOnlyHint\":true}", json);
        Assert.Equal(1, json.Split("annotations").Length - 1);   // only the tool that has them
        Assert.DoesNotContain("null", json);
    }

    // ── Hopper's own server declares what it knows ──────────────────────────────

    private sealed class Dogfood : IDisposable
    {
        private readonly AnonymousPipeServerStream _c2sRead, _s2cRead;
        private readonly AnonymousPipeClientStream _c2sWrite, _s2cWrite;
        private readonly StreamReader _serverIn, _clientIn;
        private readonly StreamWriter _serverOut, _clientOut;
        private readonly CancellationTokenSource _cts = new(TimeSpan.FromSeconds(20));
        public McpStdioClient Client { get; }
        public Task<int> Server { get; }

        public Dogfood(bool readOnly)
        {
            _c2sRead = new AnonymousPipeServerStream(PipeDirection.In);
            _c2sWrite = new AnonymousPipeClientStream(PipeDirection.Out, _c2sRead.GetClientHandleAsString());
            _s2cRead = new AnonymousPipeServerStream(PipeDirection.In);
            _s2cWrite = new AnonymousPipeClientStream(PipeDirection.Out, _s2cRead.GetClientHandleAsString());
            _serverIn = new StreamReader(_c2sRead, new UTF8Encoding(false));
            _serverOut = new StreamWriter(_s2cWrite, new UTF8Encoding(false)) { AutoFlush = true };
            _clientIn = new StreamReader(_s2cRead, new UTF8Encoding(false));
            _clientOut = new StreamWriter(_c2sWrite, new UTF8Encoding(false)) { AutoFlush = true };
            Server = Task.Run(() => McpStdioServer.RunAsync(_serverIn, _serverOut, allowShell: false, readOnly: readOnly, explicitTools: null,
                pluginOptions: new PluginLoadOptions(NoPlugins: true), ct: _cts.Token));
            Client = McpStdioClient.ConnectStreamsAsync("strict-dogfood", _clientIn, _clientOut, _cts.Token).GetAwaiter().GetResult();
        }

        public CancellationToken Ct => _cts.Token;

        public void Dispose()
        {
            Client.Dispose();
            try { _clientOut.Close(); Server.Wait(TimeSpan.FromSeconds(5)); } catch { /* the server may already be gone */ }
            _serverIn.Dispose(); _serverOut.Dispose(); _clientIn.Dispose(); _clientOut.Dispose();
            _c2sRead.Dispose(); _c2sWrite.Dispose(); _s2cRead.Dispose(); _s2cWrite.Dispose(); _cts.Dispose();
        }
    }

    [Fact]
    public async Task TheServer_DeclaresReadOnly_ForExactlyItsReadOnlyBuiltins()
    {
        using var d = new Dogfood(readOnly: false);

        var tools = await d.Client.ListToolsAsync(d.Ct);

        var hinted = tools.Where(t => t.Annotations?.ReadOnlyHint == true).Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(McpStdioServer.ReadOnlyToolNames.OrderBy(n => n, StringComparer.Ordinal).ToArray(), hinted);
        Assert.Null(tools.Single(t => t.Name == "queue_add").Annotations);       // writes the queue
        Assert.Null(tools.Single(t => t.Name == "delegate_task").Annotations);   // runs a child agent
    }

    [Fact]
    public async Task InReadOnlyMode_EveryToolTheServerOffersIsDeclaredReadOnly()
    {
        // Pins that `--read-only` and the declared hints cannot drift apart: whatever read-only mode exposes, it declares.
        using var d = new Dogfood(readOnly: true);

        var tools = await d.Client.ListToolsAsync(d.Ct);

        Assert.NotEmpty(tools);
        Assert.All(tools, t => Assert.True(t.Annotations?.ReadOnlyHint, $"{t.Name} has no readOnlyHint"));
    }

    // ── Enforcement: the real client against the real server ────────────────────

    [Fact]
    public async Task Strict_ExposesExactlyTheDeclaredReadOnlyTools_ToTheModel()
    {
        using var d = new Dogfood(readOnly: false);

        var (exposed, offered) = await d.Client.CreateMafToolsAsync(new McpToolFilter(null, null, strict: true), d.Ct);

        Assert.Equal(McpStdioServer.ReadOnlyToolNames.OrderBy(n => n, StringComparer.Ordinal).ToArray(),
            exposed.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray());
        Assert.Contains("queue_add", offered);                                  // offered by the server...
        Assert.DoesNotContain(exposed, t => t.Name == "queue_add");             // ...never told to the model
        Assert.DoesNotContain(exposed, t => t.Name == "delegate_task");
    }

    [Fact]
    public async Task Strict_WithAnAllowList_ExposesExactlyWhatWasNamed()
    {
        using var d = new Dogfood(readOnly: false);

        var (exposed, _) = await d.Client.CreateMafToolsAsync(new McpToolFilter(new[] { "queue_add" }, null, strict: true), d.Ct);

        Assert.Equal(new[] { "queue_add" }, exposed.Select(t => t.Name).ToArray());
    }
}
