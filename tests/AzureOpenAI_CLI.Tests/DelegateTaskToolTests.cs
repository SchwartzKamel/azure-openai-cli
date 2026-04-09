using System.Text.Json;
using AzureOpenAI_CLI.Tools;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// Unit tests for DelegateTaskTool — hierarchical task delegation.
/// Validates tool metadata, recursion depth guards, registry integration,
/// and alias resolution.
/// </summary>
public class DelegateTaskToolTests
{
    // ═══════════════════════════════════════════════════════════════════
    // 1. Tool metadata
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Name_ReturnsDelegateTask()
    {
        var tool = new DelegateTaskTool();
        Assert.Equal("delegate_task", tool.Name);
    }

    [Fact]
    public void Description_IsNotEmpty()
    {
        var tool = new DelegateTaskTool();
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
    }

    [Fact]
    public void ParametersSchema_IsValidJsonWithTaskRequired()
    {
        var tool = new DelegateTaskTool();
        var json = tool.ParametersSchema.ToString();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Must be an object type
        Assert.Equal("object", root.GetProperty("type").GetString());

        // Must have a "properties" object containing "task"
        var props = root.GetProperty("properties");
        Assert.True(props.TryGetProperty("task", out var taskProp));
        Assert.Equal("string", taskProp.GetProperty("type").GetString());

        // "task" must be in the required array
        var required = root.GetProperty("required");
        var requiredNames = new List<string>();
        foreach (var item in required.EnumerateArray())
            requiredNames.Add(item.GetString()!);
        Assert.Contains("task", requiredNames);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 2. Missing 'task' parameter
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ExecuteAsync_MissingTaskProperty_ReturnsErrorMessage()
    {
        var tool = new DelegateTaskTool();
        var args = JsonDocument.Parse("""{"tools": "shell"}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains("task", result);
    }

    [Fact]
    public async Task ExecuteAsync_NullTaskValue_ReturnsErrorMessage()
    {
        var tool = new DelegateTaskTool();
        // JSON null for the task value
        var args = JsonDocument.Parse("""{"task": null}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains("task", result);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 3. Recursion depth enforcement
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ExecuteAsync_MaxDepthReached_ReturnsError()
    {
        Environment.SetEnvironmentVariable("RALPH_DEPTH", "3");
        try
        {
            var tool = new DelegateTaskTool();
            var args = JsonDocument.Parse("""{"task": "test subtask"}""").RootElement;

            var result = await tool.ExecuteAsync(args, CancellationToken.None);

            Assert.Contains("maximum delegation depth", result);
            Assert.Contains("3", result); // references MaxDepth
        }
        finally
        {
            Environment.SetEnvironmentVariable("RALPH_DEPTH", null);
        }
    }

    [Fact]
    public async Task ExecuteAsync_DepthExceedsMax_ReturnsError()
    {
        Environment.SetEnvironmentVariable("RALPH_DEPTH", "5");
        try
        {
            var tool = new DelegateTaskTool();
            var args = JsonDocument.Parse("""{"task": "deeply nested"}""").RootElement;

            var result = await tool.ExecuteAsync(args, CancellationToken.None);

            Assert.Contains("maximum delegation depth", result);
            Assert.StartsWith("Error:", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("RALPH_DEPTH", null);
        }
    }

    [Fact]
    public async Task ExecuteAsync_InvalidDepthString_TreatsAsZero()
    {
        // Non-numeric RALPH_DEPTH should default to 0 (not block)
        Environment.SetEnvironmentVariable("RALPH_DEPTH", "not-a-number");
        try
        {
            var tool = new DelegateTaskTool();
            var args = JsonDocument.Parse("""{"task": "test"}""").RootElement;

            var result = await tool.ExecuteAsync(args, CancellationToken.None);

            // Depth 0 < MaxDepth, so it should NOT return the depth error.
            // It will fail for other reasons (can't find CLI binary in test env)
            // but the depth check passes.
            Assert.DoesNotContain("maximum delegation depth", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("RALPH_DEPTH", null);
        }
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2")]
    public async Task ExecuteAsync_DepthBelowMax_PassesDepthCheck(string depth)
    {
        Environment.SetEnvironmentVariable("RALPH_DEPTH", depth);
        try
        {
            var tool = new DelegateTaskTool();
            var args = JsonDocument.Parse("""{"task": "allowed depth"}""").RootElement;

            var result = await tool.ExecuteAsync(args, CancellationToken.None);

            // The depth check passes — the tool proceeds to try to start a process.
            // It may fail for other reasons (no credentials, can't find binary in CI)
            // but the critical assertion is that the depth guard did NOT fire.
            Assert.DoesNotContain("maximum delegation depth", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("RALPH_DEPTH", null);
        }
    }

    [Fact]
    public async Task ExecuteAsync_NoDepthEnvVar_DefaultsToZero()
    {
        Environment.SetEnvironmentVariable("RALPH_DEPTH", null);
        try
        {
            var tool = new DelegateTaskTool();
            var args = JsonDocument.Parse("""{"task": "no depth set"}""").RootElement;

            var result = await tool.ExecuteAsync(args, CancellationToken.None);

            // Depth 0 passes the check — will fail later for other reasons
            Assert.DoesNotContain("maximum delegation depth", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("RALPH_DEPTH", null);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // 4. ToolRegistry integration
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void ToolRegistry_AllToolsEnabled_IncludesDelegateTask()
    {
        var registry = ToolRegistry.Create(null);

        Assert.Contains(registry.All, t => t.Name == "delegate_task");
    }

    [Fact]
    public void ToolRegistry_DelegateAlias_RegistersTool()
    {
        var registry = ToolRegistry.Create(["delegate"]);

        Assert.Single(registry.All);
        Assert.Equal("delegate_task", registry.All.First().Name);
    }

    [Fact]
    public void ToolRegistry_FullName_RegistersTool()
    {
        var registry = ToolRegistry.Create(["delegate_task"]);

        Assert.Single(registry.All);
        Assert.Equal("delegate_task", registry.All.First().Name);
    }

    [Fact]
    public void ToolRegistry_ShellOnly_DoesNotIncludeDelegate()
    {
        var registry = ToolRegistry.Create(["shell"]);

        Assert.Single(registry.All);
        Assert.Equal("shell_exec", registry.All.First().Name);
        Assert.DoesNotContain(registry.All, t => t.Name == "delegate_task");
    }

    [Fact]
    public void ToolRegistry_ChatToolsIncludeDelegate_WhenAllEnabled()
    {
        var registry = ToolRegistry.Create(null);
        var chatTools = registry.ToChatTools();

        Assert.Contains(chatTools, ct => ct.FunctionName == "delegate_task");
    }

    [Fact]
    public void ToolRegistry_GetByName_ReturnsDelegateTask()
    {
        var registry = ToolRegistry.Create(null);

        var tool = registry.Get("delegate_task");

        Assert.NotNull(tool);
        Assert.IsType<DelegateTaskTool>(tool);
    }

    [Fact]
    public void ToolRegistry_DelegateNotInFileOrWebOnlyFilter()
    {
        var registry = ToolRegistry.Create(["file", "web"]);

        Assert.Equal(2, registry.All.Count);
        Assert.DoesNotContain(registry.All, t => t.Name == "delegate_task");
    }
}
