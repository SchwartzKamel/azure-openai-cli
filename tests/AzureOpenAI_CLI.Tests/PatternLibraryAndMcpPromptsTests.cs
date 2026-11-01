using System.Text.Json;
using AzureOpenAI_CLI.Mcp;
using AzureOpenAI_CLI.Patterns;
using AzureOpenAI_CLI.Squad;
using AzureOpenAI_CLI.Tools;

namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// T6 tests: proves FR-015 curated pattern library (<c>--patterns [--json]</c>,
/// <c>--pattern &lt;name&gt;</c> / <c>-p</c>, user custom patterns in
/// <c>~/.config/azureopenai-cli/patterns/*.json</c>) and MCP server
/// <c>prompts/list</c>, <c>prompts/get</c>, <c>resources/list</c>, and
/// <c>resources/read</c>.
/// </summary>
[Collection("ConsoleCapture")]
public class PatternLibraryAndMcpPromptsTests
{
    [Fact]
    public void ListPatternsCommand_JsonMode_EmitsBuiltInAndUserPatterns()
    {
        using var home = new TempHome();
        var userPatternsDir = Path.Combine(
            Environment.GetEnvironmentVariable("HOME")!,
            ".config",
            "azureopenai-cli",
            "patterns");
        Directory.CreateDirectory(userPatternsDir);

        File.WriteAllText(Path.Combine(userPatternsDir, "pr-description.json"), """
            {
              "name": "pr-description",
              "description": "Generate a concise pull request description.",
              "system_prompt": "Write a crisp PR summary and test plan.",
              "temperature": 0.25
            }
            """);

        var origOut = Console.Out;
        var outWriter = new StringWriter();
        try
        {
            Console.SetOut(outWriter);
            var opts = Program.ParseArgs(["--patterns", "--json"]);
            int rc = Program.ListPatternsCommand(opts);

            Assert.Equal(0, rc);
            using var doc = JsonDocument.Parse(outWriter.ToString());
            var names = doc.RootElement.EnumerateArray().Select(e => e.GetProperty("name").GetString()).ToList();

            Assert.Contains("summarize", names);
            Assert.Contains("commit-message", names);
            Assert.Contains("code-review", names);
            Assert.Contains("pr-description", names);

            var custom = doc.RootElement.EnumerateArray().Single(e => e.GetProperty("name").GetString() == "pr-description");
            Assert.Equal("user-pattern", custom.GetProperty("source").GetString());
        }
        finally
        {
            Console.SetOut(origOut);
        }
    }

    [Fact]
    public void ParseArgs_PatternFlag_CapturedAndResolvesTemplate()
    {
        var opts = Program.ParseArgs(["-p", "commit-message", "diff text"]);
        Assert.Equal("commit-message", opts.Pattern);
        var resolved = PatternRegistry.Resolve(opts.Pattern);
        Assert.NotNull(resolved);
        Assert.Contains("Conventional Commits", resolved!.SystemPrompt);
        Assert.Equal(0.2f, resolved.Temperature);
    }

    [Fact]
    public async Task McpStdioServer_PromptsListAndGet_ExposesPatternsAndPersonas()
    {
        using var cwd = new TempCwd();
        SquadInitializer.Initialize();

        var inputLines = string.Join("\n", new[]
        {
            """{"jsonrpc":"2.0","id":1,"method":"prompts/list"}""",
            """{"jsonrpc":"2.0","id":2,"method":"prompts/get","params":{"name":"commit-message","arguments":{"input":"feat: add mcp"}}}""",
            """{"jsonrpc":"2.0","id":3,"method":"prompts/get","params":{"name":"persona:coder","arguments":{"input":"review this"}}}""",
        });

        using var reader = new StringReader(inputLines);
        using var writer = new StringWriter();

        await McpStdioServer.RunAsync(reader, writer, pluginOptions: new PluginLoadOptions(NoPlugins: true));

        var responses = writer.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal(3, responses.Length);

        // 1. prompts/list contains both built-in patterns and squad personas
        using var listDoc = JsonDocument.Parse(responses[0]);
        var promptNames = listDoc.RootElement
            .GetProperty("result")
            .GetProperty("prompts")
            .EnumerateArray()
            .Select(p => p.GetProperty("name").GetString())
            .ToList();
        Assert.Contains("commit-message", promptNames);
        Assert.Contains("persona:coder", promptNames);

        // 2. prompts/get for commit-message
        using var getPatDoc = JsonDocument.Parse(responses[1]);
        var patText = getPatDoc.RootElement
            .GetProperty("result")
            .GetProperty("messages")[0]
            .GetProperty("content")
            .GetProperty("text")
            .GetString();
        Assert.Contains("Conventional Commits", patText);
        Assert.Contains("feat: add mcp", patText);

        // 3. prompts/get for persona:coder
        using var getPerDoc = JsonDocument.Parse(responses[2]);
        var perText = getPerDoc.RootElement
            .GetProperty("result")
            .GetProperty("messages")[0]
            .GetProperty("content")
            .GetProperty("text")
            .GetString();
        Assert.Contains("review this", perText);
    }

    [Fact]
    public async Task McpStdioServer_ResourcesListAndRead_ExposesSquadConfigAndMemory()
    {
        using var cwd = new TempCwd();
        SquadInitializer.Initialize();
        var mem = new PersonaMemory();
        mem.AppendHistory("coder", "fix tests", "all green");

        var inputLines = string.Join("\n", new[]
        {
            """{"jsonrpc":"2.0","id":10,"method":"resources/list"}""",
            """{"jsonrpc":"2.0","id":11,"method":"resources/read","params":{"uri":"squad://config"}}""",
            """{"jsonrpc":"2.0","id":12,"method":"resources/read","params":{"uri":"squad://memory/coder"}}""",
            """{"jsonrpc":"2.0","id":13,"method":"resources/read","params":{"uri":"squad://memory/../../etc/passwd"}}""",
        });

        using var reader = new StringReader(inputLines);
        using var writer = new StringWriter();

        await McpStdioServer.RunAsync(reader, writer, pluginOptions: new PluginLoadOptions(NoPlugins: true));

        var responses = writer.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal(4, responses.Length);

        // 1. resources/list includes squad://config and squad://memory/coder
        using var listDoc = JsonDocument.Parse(responses[0]);
        var uris = listDoc.RootElement
            .GetProperty("result")
            .GetProperty("resources")
            .EnumerateArray()
            .Select(r => r.GetProperty("uri").GetString())
            .ToList();
        Assert.Contains("squad://config", uris);
        Assert.Contains("squad://memory/coder", uris);

        // 2. resources/read squad://config returns .squad.json
        using var cfgDoc = JsonDocument.Parse(responses[1]);
        var cfgText = cfgDoc.RootElement.GetProperty("result").GetProperty("contents")[0].GetProperty("text").GetString();
        Assert.Contains("\"personas\"", cfgText);

        // 3. resources/read squad://memory/coder returns session history
        using var memDoc = JsonDocument.Parse(responses[2]);
        var memText = memDoc.RootElement.GetProperty("result").GetProperty("contents")[0].GetProperty("text").GetString();
        Assert.Contains("all green", memText);

        // 4. Path traversal attempt on squad://memory/ is rejected with -32602
        using var errDoc = JsonDocument.Parse(responses[3]);
        Assert.Equal(-32602, errDoc.RootElement.GetProperty("error").GetProperty("code").GetInt32());
    }
}
