using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using AzureOpenAI_CLI.Mcp;
using AzureOpenAI_CLI.Tools;
using Microsoft.Extensions.AI;

namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// T7 tests: proves outbound MCP stdio client (<see cref="McpStdioClient"/>),
/// <c>--mcp-server</c> / <c>az-ai mcp tools</c> / <c>az-ai mcp call</c> parsing,
/// <c>Content-Length</c> header framing support in <see cref="McpStdioServer"/>,
/// and full dogfooding of <see cref="McpStdioClient"/> against <see cref="McpStdioServer"/>
/// over bidirectional pipes.
/// </summary>
public class McpStdioClientTests
{
    [Fact]
    public void ParseArgs_McpServerFlag_AndMcpToolsAndCallSubcommands_ParsedCorrectly()
    {
        var opts1 = Program.ParseArgs(["--mcp-server", "fs=npx -y @modelcontextprotocol/server-filesystem .", "list files"]);
        Assert.False(opts1.ParseError);
        Assert.True(opts1.AgentMode); // --mcp-server implies --agent
        Assert.NotNull(opts1.McpServers);
        Assert.Equal("fs=npx -y @modelcontextprotocol/server-filesystem .", Assert.Single(opts1.McpServers!));

        var opts2 = Program.ParseArgs(["mcp", "tools", "my-mcp-server --stdio", "--json"]);
        Assert.False(opts2.ParseError);
        Assert.Equal("tools", opts2.McpSubcommand);
        Assert.Equal("my-mcp-server --stdio", opts2.McpTargetServer);
        Assert.True(opts2.Json);

        var opts3 = Program.ParseArgs(["mcp", "call", "my-mcp-server", "get_datetime", """{"timezone":"UTC"}"""]);
        Assert.False(opts3.ParseError);
        Assert.Equal("call", opts3.McpSubcommand);
        Assert.Equal("my-mcp-server", opts3.McpTargetServer);
        Assert.Equal("get_datetime", opts3.McpTargetTool);
        Assert.Equal("""{"timezone":"UTC"}""", opts3.McpTargetArgsJson);
    }

    [Fact]
    public void ParseServerSpec_HandlesNamedAndBareCommandSpecsWithQuotes()
    {
        var (name1, argv1) = McpStdioClient.ParseServerSpec("github=docker run -i --rm \"my image:latest\"");
        Assert.Equal("github", name1);
        Assert.Equal(new[] { "docker", "run", "-i", "--rm", "my image:latest" }, argv1);

        var (name2, argv2) = McpStdioClient.ParseServerSpec("/usr/local/bin/az-ai mcp serve");
        Assert.Equal("az-ai", name2);
        Assert.Equal(new[] { "/usr/local/bin/az-ai", "mcp", "serve" }, argv2);
    }

    [Fact]
    public async Task McpStdioServer_SupportsContentLengthHeaderFraming()
    {
        var requestBody = """{"jsonrpc":"2.0","id":42,"method":"ping"}""";
        var byteLen = Encoding.UTF8.GetByteCount(requestBody);
        var framedInput = $"Content-Length: {byteLen}\r\nContent-Type: application/vscode-jsonrpc; charset=utf-8\r\n\r\n{requestBody}";

        using var reader = new StringReader(framedInput);
        using var writer = new StringWriter();

        int rc = await McpStdioServer.RunAsync(reader, writer, pluginOptions: new PluginLoadOptions(NoPlugins: true));
        Assert.Equal(0, rc);

        var rawOut = writer.ToString();
        Assert.StartsWith("Content-Length: ", rawOut);
        Assert.Contains("\r\n\r\n", rawOut);

        var splitIdx = rawOut.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        var body = rawOut[(splitIdx + 4)..];
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(42, doc.RootElement.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task McpStdioClient_DogfoodAgainstMcpStdioServer_InitializesListsAndCallsTools()
    {
        // Connects McpStdioClient to McpStdioServer over in-process AnonymousPipeStreams
        // and verifies initialize -> tools/list -> CreateMafToolsAsync -> AIFunction.InvokeAsync.
        using var clientToServerRead = new AnonymousPipeServerStream(PipeDirection.In);
        using var clientToServerWrite = new AnonymousPipeClientStream(PipeDirection.Out, clientToServerRead.GetClientHandleAsString());

        using var serverToClientRead = new AnonymousPipeServerStream(PipeDirection.In);
        using var serverToClientWrite = new AnonymousPipeClientStream(PipeDirection.Out, serverToClientRead.GetClientHandleAsString());

        using var serverIn = new StreamReader(clientToServerRead, new UTF8Encoding(false));
        using var serverOut = new StreamWriter(serverToClientWrite, new UTF8Encoding(false)) { AutoFlush = true };

        using var clientIn = new StreamReader(serverToClientRead, new UTF8Encoding(false));
        using var clientOut = new StreamWriter(clientToServerWrite, new UTF8Encoding(false)) { AutoFlush = true };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var serverTask = Task.Run(() => McpStdioServer.RunAsync(
            serverIn,
            serverOut,
            allowShell: false,
            readOnly: false,
            explicitTools: null,
            pluginOptions: new PluginLoadOptions(NoPlugins: true),
            ct: cts.Token));

        using var client = await McpStdioClient.ConnectStreamsAsync("az-ai-dogfood", clientIn, clientOut, cts.Token);

        // 1. List tools via MCP client
        var tools = await client.ListToolsAsync(cts.Token);
        Assert.Contains(tools, t => t.Name == "get_datetime");
        Assert.Contains(tools, t => t.Name == "read_file");
        Assert.DoesNotContain(tools, t => t.Name == "shell_exec");

        // 2. Wrap remote MCP tools as MAF AITools and invoke get_datetime
        var mafTools = await client.CreateMafToolsAsync(cts.Token);
        var dtFn = Assert.IsAssignableFrom<AIFunction>(mafTools.Single(t => t.Name == "get_datetime"));

        var invokeResult = await dtFn.InvokeAsync(new AIFunctionArguments
        {
            ["timezone"] = "UTC",
        }, cts.Token);

        Assert.NotNull(invokeResult);
        Assert.Contains("+00:00", invokeResult.ToString());

        // Close client writer so server sees EOF and exits cleanly
        clientOut.Close();
        int serverExit = await serverTask;
        Assert.Equal(0, serverExit);
    }
}
