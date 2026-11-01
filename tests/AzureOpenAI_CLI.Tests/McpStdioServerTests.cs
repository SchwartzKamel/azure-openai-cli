using System.Text.Json;
using AzureOpenAI_CLI.Mcp;
using AzureOpenAI_CLI.Tools;

namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// T4 tests: proves MCP stdio JSON-RPC 2.0 server (<c>az-ai mcp serve</c>)
/// protocol compliance (<c>initialize</c>, <c>notifications/initialized</c>,
/// <c>ping</c>, <c>tools/list</c>, <c>tools/call</c>), safe default tool
/// exposure (blocking <c>shell_exec</c> unless <c>--allow-shell</c> is passed),
/// custom manifest plugin execution over MCP, and recovery from malformed JSON
/// or unknown JSON-RPC methods.
/// </summary>
public class McpStdioServerTests
{
    [Fact]
    public void ParseArgs_McpServeSubcommand_AndFlags_ParsedCorrectly()
    {
        var opts = Program.ParseArgs(["mcp", "serve", "--allow-shell", "--read-only"]);
        Assert.False(opts.ParseError);
        Assert.True(opts.McpServe);
        Assert.True(opts.AllowShell);
        Assert.True(opts.ReadOnly);
    }

    [Fact]
    public void ParseArgs_McpWithoutServe_ReturnsParseError()
    {
        var origErr = Console.Error;
        using var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            var opts = Program.ParseArgs(["mcp", "unknown"]);
            Assert.True(opts.ParseError);
            Assert.Contains("'mcp' subcommand requires 'serve'", sw.ToString());
        }
        finally
        {
            Console.SetError(origErr);
        }
    }

    [Fact]
    public async Task McpStdioServer_InitializeAndToolsList_ExposesSafeToolsAndPlugins_OmitShellByDefault()
    {
        var pluginDir = Path.Combine(Path.GetTempPath(), $"mcp-plugins-{Guid.NewGuid():N}");
        Directory.CreateDirectory(pluginDir);
        try
        {
            File.WriteAllText(Path.Combine(pluginDir, "echo_plugin.json"), """
                {
                  "name": "echo_plugin",
                  "description": "Echoes a greeting.",
                  "command": ["/bin/echo", "Hello {who}"],
                  "parameters": {
                    "type": "object",
                    "properties": {
                      "who": { "type": "string" }
                    }
                  }
                }
                """);

            var inputLines = string.Join("\n", new[]
            {
                """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05"}}""",
                """{"jsonrpc":"2.0","method":"notifications/initialized"}""",
                """{"jsonrpc":"2.0","id":2,"method":"tools/list"}""",
            });

            using var reader = new StringReader(inputLines);
            using var writer = new StringWriter();

            int rc = await McpStdioServer.RunAsync(
                reader,
                writer,
                allowShell: false,
                readOnly: false,
                explicitTools: null,
                pluginOptions: new PluginLoadOptions(CustomPluginDir: pluginDir));

            Assert.Equal(0, rc);

            var responses = writer.ToString()
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            // Notification has no id and must not emit a response line -> exactly 2 response lines.
            Assert.Equal(2, responses.Length);

            // Check initialize response
            using var initDoc = JsonDocument.Parse(responses[0]);
            Assert.Equal(1, initDoc.RootElement.GetProperty("id").GetInt32());
            var initResult = initDoc.RootElement.GetProperty("result");
            Assert.Equal("2024-11-05", initResult.GetProperty("protocolVersion").GetString());
            Assert.Equal("az-ai", initResult.GetProperty("serverInfo").GetProperty("name").GetString());

            // Check tools/list response
            using var listDoc = JsonDocument.Parse(responses[1]);
            Assert.Equal(2, listDoc.RootElement.GetProperty("id").GetInt32());
            var toolsArray = listDoc.RootElement.GetProperty("result").GetProperty("tools");
            var names = toolsArray.EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToList();

            Assert.Contains("read_file", names);
            Assert.Contains("get_datetime", names);
            Assert.Contains("echo_plugin", names);
            // shell_exec must NOT be exposed unless --allow-shell is true
            Assert.DoesNotContain("shell_exec", names);
        }
        finally
        {
            try { Directory.Delete(pluginDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task McpStdioServer_ToolsCall_ExecutesBuiltInAndManifestTools_AndBlocksShellWithoutAllowShell()
    {
        var pluginDir = Path.Combine(Path.GetTempPath(), $"mcp-call-{Guid.NewGuid():N}");
        Directory.CreateDirectory(pluginDir);
        try
        {
            File.WriteAllText(Path.Combine(pluginDir, "echo_plugin.json"), """
                {
                  "name": "echo_plugin",
                  "description": "Echoes a greeting.",
                  "command": ["/bin/echo", "Hello {who}!"]
                }
                """);

            var inputLines = string.Join("\n", new[]
            {
                """{"jsonrpc":"2.0","id":10,"method":"tools/call","params":{"name":"get_datetime","arguments":{"timezone":"UTC"}}}""",
                """{"jsonrpc":"2.0","id":11,"method":"tools/call","params":{"name":"echo_plugin","arguments":{"who":"Agent"}}}""",
                """{"jsonrpc":"2.0","id":12,"method":"tools/call","params":{"name":"shell_exec","arguments":{"command":"echo hi"}}}""",
            });

            using var reader = new StringReader(inputLines);
            using var writer = new StringWriter();

            await McpStdioServer.RunAsync(
                reader,
                writer,
                allowShell: false,
                readOnly: false,
                explicitTools: null,
                pluginOptions: new PluginLoadOptions(CustomPluginDir: pluginDir));

            var responses = writer.ToString()
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Assert.Equal(3, responses.Length);

            // 1. get_datetime succeeds
            using var dtDoc = JsonDocument.Parse(responses[0]);
            var dtRes = dtDoc.RootElement.GetProperty("result");
            Assert.False(dtRes.GetProperty("isError").GetBoolean());
            var dtText = dtRes.GetProperty("content")[0].GetProperty("text").GetString();
            Assert.Contains("+00:00", dtText);

            // 2. echo_plugin succeeds
            using var plugDoc = JsonDocument.Parse(responses[1]);
            var plugRes = plugDoc.RootElement.GetProperty("result");
            Assert.False(plugRes.GetProperty("isError").GetBoolean());
            var plugText = plugRes.GetProperty("content")[0].GetProperty("text").GetString();
            Assert.Equal("Hello Agent!", plugText);

            // 3. shell_exec blocked with isError: true and guidance to pass --allow-shell
            using var shellDoc = JsonDocument.Parse(responses[2]);
            var shellRes = shellDoc.RootElement.GetProperty("result");
            Assert.True(shellRes.GetProperty("isError").GetBoolean());
            var shellText = shellRes.GetProperty("content")[0].GetProperty("text").GetString();
            Assert.Contains("--allow-shell", shellText);
        }
        finally
        {
            try { Directory.Delete(pluginDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task McpStdioServer_MalformedJsonAndUnknownMethod_ReturnsJsonRpcErrors_AndContinuesToAnswerPing()
    {
        var inputLines = string.Join("\n", new[]
        {
            """{not valid json""",
            """{"jsonrpc":"2.0","id":99,"method":"nonexistent/method"}""",
            """{"jsonrpc":"2.0","id":100,"method":"ping"}""",
        });

        using var reader = new StringReader(inputLines);
        using var writer = new StringWriter();

        await McpStdioServer.RunAsync(reader, writer, pluginOptions: new PluginLoadOptions(NoPlugins: true));

        var responses = writer.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal(3, responses.Length);

        // 1. Parse error (-32700)
        using var err1 = JsonDocument.Parse(responses[0]);
        Assert.Equal(-32700, err1.RootElement.GetProperty("error").GetProperty("code").GetInt32());

        // 2. Method not found (-32601)
        using var err2 = JsonDocument.Parse(responses[1]);
        Assert.Equal(99, err2.RootElement.GetProperty("id").GetInt32());
        Assert.Equal(-32601, err2.RootElement.GetProperty("error").GetProperty("code").GetInt32());

        // 3. Subsequent ping succeeds
        using var pingDoc = JsonDocument.Parse(responses[2]);
        Assert.Equal(100, pingDoc.RootElement.GetProperty("id").GetInt32());
        Assert.Equal(JsonValueKind.Object, pingDoc.RootElement.GetProperty("result").ValueKind);
    }
}
