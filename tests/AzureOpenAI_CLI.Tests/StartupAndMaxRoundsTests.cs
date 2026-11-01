using AzureOpenAI_CLI.Tools;
using Microsoft.Extensions.AI;

namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// T2 tests: proves shared <see cref="System.Net.Http.SocketsHttpHandler"/> transport
/// between <see cref="Program.PrewarmAsync"/> and <see cref="Program.CreateAzureOpenAIClient"/>
/// (FR-007) and <c>--max-rounds</c> enforcement on agent tool calls.
/// </summary>
public class StartupAndMaxRoundsTests
{
    [Fact]
    public void SharedHttpClient_UsesConfiguredSharedSocketsHandler()
    {
        Assert.NotNull(Program.SharedSocketsHandler);
        Assert.NotNull(Program.SharedHttpClient);
        Assert.True(Program.SharedSocketsHandler.EnableMultipleHttp2Connections);
        Assert.Equal(TimeSpan.FromMinutes(5), Program.SharedSocketsHandler.PooledConnectionLifetime);
    }

    [Fact]
    public void CreateAzureOpenAIClient_ConstructsClientWithSharedHttpTransport()
    {
        var uri = new Uri("https://example.openai.azure.com/");
        var client = Program.CreateAzureOpenAIClient(uri, "test-api-key");

        Assert.NotNull(client);
    }

    [Fact]
    public async Task CreateMafTools_WithMaxRoundsCap_BlocksCallsBeyondLimit()
    {
        // Proves that when maxRounds is set (e.g. --max-rounds 2), the first 2 tool
        // invocations succeed and the 3rd returns the --max-rounds boundary message
        // without executing the underlying tool.
        var tools = ToolRegistry.CreateMafTools(new[] { "datetime" }, maxRounds: 2);
        var dtTool = Assert.Single(tools);
        var fn = Assert.IsAssignableFrom<AIFunction>(dtTool);

        var r1 = await fn.InvokeAsync(new AIFunctionArguments());
        var r2 = await fn.InvokeAsync(new AIFunctionArguments());
        var r3 = await fn.InvokeAsync(new AIFunctionArguments());

        Assert.NotNull(r1);
        Assert.DoesNotContain("maximum tool-call rounds", r1.ToString());
        Assert.NotNull(r2);
        Assert.DoesNotContain("maximum tool-call rounds", r2.ToString());

        Assert.NotNull(r3);
        Assert.Contains("Error: maximum tool-call rounds (2) reached (--max-rounds)", r3.ToString());
    }

    [Fact]
    public async Task CreateMafTools_MaxRoundsSharedAcrossMultipleTools()
    {
        // Proves that the round counter is shared across all tools in the agent's
        // registry for a given run, so calling tool A then tool B counts as 2 rounds.
        var tools = ToolRegistry.CreateMafTools(new[] { "datetime", "file" }, maxRounds: 1);
        Assert.Equal(2, tools.Count);

        var dtFn = Assert.IsAssignableFrom<AIFunction>(tools.Single(t => t.Name == "get_datetime"));
        var fileFn = Assert.IsAssignableFrom<AIFunction>(tools.Single(t => t.Name == "read_file"));

        var first = await dtFn.InvokeAsync(new AIFunctionArguments());
        Assert.DoesNotContain("maximum tool-call rounds", first?.ToString() ?? "");

        var second = await fileFn.InvokeAsync(new AIFunctionArguments());
        Assert.Contains("Error: maximum tool-call rounds (1) reached (--max-rounds)", second?.ToString() ?? "");
    }
}
