using System.Text.Json;
using AzureOpenAI_CLI.Tools;

namespace AzureOpenAI_CLI.Tests.Adversary;

/// <summary>
/// S02E23 -- The Adversary. Fault injection against the agent loop's
/// stream-accumulation and tool-dispatch surface: malformed tool-call
/// fragments, truncated JSON arguments, recursion depth, and the
/// ToolRegistry's exception envelope.
///
/// These tests probe defensive-depth: the agent loop and ToolRegistry
/// both wrap tool execution in try/catch (Program.cs around the
/// streaming dispatch, ToolRegistry.ExecuteAsync at line 48). The
/// individual tool ExecuteAsync methods, however, do not consistently
/// validate value kinds before calling GetString() -- a non-string
/// value for a required parameter throws InvalidOperationException
/// that is only swallowed at the outer envelope. Pinned as a
/// defense-in-depth gap, not a CVE-shape.
/// </summary>
public class PartialStreamChaosTests
{
    // ===================================================================
    // ToolRegistry envelope -- catches malformed input from the model
    // ===================================================================

    [Fact]
    public async Task Registry_MalformedJsonArgs_ReturnsErrorString_NoThrow()
    {
        var registry = ToolRegistry.Create(new[] { "shell" });
        var result = await registry.ExecuteAsync("shell_exec", "{not valid json", CancellationToken.None);
        Assert.StartsWith("Error", result);
    }

    [Fact]
    public async Task Registry_TruncatedJsonArgs_ReturnsErrorString_NoThrow()
    {
        // Streaming truncation: the model sent {"command":"echo hi but the
        // closing brace never arrived. The registry envelope must catch.
        var registry = ToolRegistry.Create(new[] { "shell" });
        var result = await registry.ExecuteAsync("shell_exec", "{\"command\":\"echo hi", CancellationToken.None);
        Assert.StartsWith("Error", result);
    }

    [Fact]
    public async Task Registry_EmptyArgsString_HandledGracefully()
    {
        // ToolRegistry passes empty argumentsJson as new JsonElement()
        // (ValueKind == Undefined). Each tool's TryGetProperty must
        // refuse cleanly rather than throw.
        var registry = ToolRegistry.Create(new[] { "shell" });
        var result = await registry.ExecuteAsync("shell_exec", "", CancellationToken.None);
        Assert.StartsWith("Error", result);
    }

    [Fact]
    public async Task Registry_UnknownToolName_ReturnsError_NoThrow()
    {
        // Hallucinated tool name from the model -- registry must not throw.
        var registry = ToolRegistry.Create();
        var result = await registry.ExecuteAsync("definitely_not_a_real_tool", "{}", CancellationToken.None);
        Assert.StartsWith("Error", result);
        Assert.Contains("unknown", result.ToLower());
    }

    [Fact]
    public async Task Registry_NestedToolCallSpamArgs_NoStackBlowup()
    {
        // Model generates pathological nesting in the arguments JSON.
        // Must complete without a stack overflow in the registry path.
        var deepNested = new System.Text.StringBuilder();
        deepNested.Append("{\"command\":\"echo probe\"");
        for (int i = 0; i < 200; i++) deepNested.Append(",\"x\":{");
        for (int i = 0; i < 200; i++) deepNested.Append("}");
        deepNested.Append("}");

        var registry = ToolRegistry.Create(new[] { "shell" });
        // Either parses (and runs the echo) or returns a parse error -- both
        // outcomes are acceptable; the test asserts no unhandled exception.
        var result = await registry.ExecuteAsync("shell_exec", deepNested.ToString(), CancellationToken.None);
        Assert.NotNull(result);
    }

    // ===================================================================
    // DelegateTaskTool -- recursion depth (max-rounds escape attempt)
    // ===================================================================

    [Fact]
    public async Task DelegateTask_AtMaxDepth_ReturnsError()
    {
        var prior = Environment.GetEnvironmentVariable("RALPH_DEPTH");
        try
        {
            Environment.SetEnvironmentVariable("RALPH_DEPTH", "3");
            var tool = new DelegateTaskTool();
            var args = JsonDocument.Parse("""{"task":"do something"}""").RootElement;
            var result = await tool.ExecuteAsync(args, CancellationToken.None);
            Assert.StartsWith("Error:", result);
            Assert.Contains("depth", result.ToLower());
        }
        finally
        {
            Environment.SetEnvironmentVariable("RALPH_DEPTH", prior);
        }
    }

    [Fact]
    public async Task DelegateTask_AboveMaxDepth_ReturnsError()
    {
        // Attacker-controlled RALPH_DEPTH already past the cap must
        // still block, not underflow into negative-distance territory.
        var prior = Environment.GetEnvironmentVariable("RALPH_DEPTH");
        try
        {
            Environment.SetEnvironmentVariable("RALPH_DEPTH", "99");
            var tool = new DelegateTaskTool();
            var args = JsonDocument.Parse("""{"task":"do something"}""").RootElement;
            var result = await tool.ExecuteAsync(args, CancellationToken.None);
            Assert.StartsWith("Error:", result);
            Assert.Contains("depth", result.ToLower());
        }
        finally
        {
            Environment.SetEnvironmentVariable("RALPH_DEPTH", prior);
        }
    }

    [Fact]
    public async Task DelegateTask_NonNumericDepth_DoesNotBypass()
    {
        // RALPH_DEPTH = "garbage" -- int.TryParse fails, currentDepth
        // defaults to 0, delegation is permitted (depth 0 < 3). The
        // bypass attack would be RALPH_DEPTH = "-1" hoping the tool
        // permits negative-relative recursion. int.TryParse accepts
        // "-1" as a valid integer; -1 < 3 so the gate passes. This
        // test pins the current behavior so the next reader knows
        // the gate is "less-than", not "in-range".
        var prior = Environment.GetEnvironmentVariable("RALPH_DEPTH");
        try
        {
            Environment.SetEnvironmentVariable("RALPH_DEPTH", "garbage");
            var tool = new DelegateTaskTool();
            var args = JsonDocument.Parse("""{"task":""}""").RootElement;
            var result = await tool.ExecuteAsync(args, CancellationToken.None);
            // Empty task -> error from validation, not from depth.
            Assert.StartsWith("Error:", result);
            Assert.Contains("empty", result.ToLower());
        }
        finally
        {
            Environment.SetEnvironmentVariable("RALPH_DEPTH", prior);
        }
    }

    // ===================================================================
    // LIVE FINDINGS
    // ===================================================================

    [Fact(Skip = "Live finding: e23-tool-non-string-param-throws")]
    public async Task ShellExec_NonStringCommandParam_ReturnsError_NoThrow()
    {
        // ShellExecTool calls commandProp.GetString() unconditionally
        // after TryGetProperty. If the model emits {"command": 123}
        // (number, not string) GetString() throws
        // InvalidOperationException. The throw is swallowed by the
        // ToolRegistry envelope so a hostile model cannot crash the
        // agent loop -- but the tool itself violates its "graceful
        // degradation" contract. Defense-in-depth gap: if the tool
        // is ever called outside the registry envelope (a future
        // direct-dispatch path), the throw resurfaces.
        var tool = new ShellExecTool();
        var args = JsonDocument.Parse("""{"command": 123}""").RootElement;
        var result = await tool.ExecuteAsync(args, CancellationToken.None);
        Assert.StartsWith("Error:", result);
    }

    [Fact(Skip = "Live finding: e23-delegate-negative-depth-bypass")]
    public async Task DelegateTask_NegativeDepth_ShouldBeRejected()
    {
        // RALPH_DEPTH = "-1" parses as a valid int, -1 < MaxDepth (3),
        // so delegation proceeds. A hostile parent could set
        // RALPH_DEPTH = "-99" and effectively get 102 levels of
        // recursion before the cap engages (3 - (-99) = 102). Defense
        // should clamp depth to >= 0 before comparing to MaxDepth.
        var prior = Environment.GetEnvironmentVariable("RALPH_DEPTH");
        try
        {
            Environment.SetEnvironmentVariable("RALPH_DEPTH", "-1");
            var tool = new DelegateTaskTool();
            var args = JsonDocument.Parse("""{"task":"recurse"}""").RootElement;
            var result = await tool.ExecuteAsync(args, CancellationToken.None);
            // Expected: rejected because depth normalization should
            // treat negatives as 0 and then enforce a cap that
            // doesn't permit deeper-than-MaxDepth recursion.
            Assert.StartsWith("Error:", result);
            Assert.Contains("depth", result.ToLower());
        }
        finally
        {
            Environment.SetEnvironmentVariable("RALPH_DEPTH", prior);
        }
    }
}
