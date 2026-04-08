using System.Text.Json;
using AzureOpenAI_CLI.Tools;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// Unit tests for the built-in tool system (agentic mode).
/// Covers ToolRegistry, GetDateTimeTool, ReadFileTool, and ShellExecTool.
/// </summary>
public class ToolTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _tempFile;

    public ToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"tool-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _tempFile = Path.Combine(_tempDir, "test-read.txt");
        File.WriteAllText(_tempFile, "Hello from tool tests!");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }

    // ── ToolRegistry ────────────────────────────────────────────────────

    [Fact]
    public void Create_WithNull_ReturnsAllFiveTools()
    {
        var registry = ToolRegistry.Create(null);

        Assert.Equal(5, registry.All.Count);
    }

    [Fact]
    public void Create_WithFilterDatetime_ReturnsOnlyMatchingTool()
    {
        var registry = ToolRegistry.Create(["datetime"]);

        Assert.Single(registry.All);
        Assert.Equal("get_datetime", registry.All.First().Name);
    }

    [Fact]
    public void Create_WithFilterShell_MatchesShellExecTool()
    {
        var registry = ToolRegistry.Create(["shell"]);

        Assert.Single(registry.All);
        Assert.Equal("shell_exec", registry.All.First().Name);
    }

    [Fact]
    public void ToChatTools_ReturnsCorrectCount()
    {
        var registry = ToolRegistry.Create(null);
        var chatTools = registry.ToChatTools();

        Assert.Equal(5, chatTools.Count);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownTool_ReturnsErrorMessage()
    {
        var registry = ToolRegistry.Create(null);

        var result = await registry.ExecuteAsync("nonexistent_tool", "{}", CancellationToken.None);

        Assert.Contains("unknown tool", result);
        Assert.Contains("nonexistent_tool", result);
    }

    // ── GetDateTimeTool ─────────────────────────────────────────────────

    [Fact]
    public async Task GetDateTime_ReturnsCurrentYear()
    {
        var tool = new GetDateTimeTool();
        var args = JsonDocument.Parse("{}").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.Contains(DateTime.Now.Year.ToString(), result);
    }

    [Fact]
    public async Task GetDateTime_InvalidTimezone_ReturnsError()
    {
        var tool = new GetDateTimeTool();
        var args = JsonDocument.Parse("""{"timezone":"Mars/Olympus_Mons"}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains("Mars/Olympus_Mons", result);
    }

    [Fact]
    public async Task GetDateTime_EmptyArguments_Works()
    {
        var tool = new GetDateTimeTool();
        // default JsonElement (ValueKind == Undefined) simulates no arguments
        var args = new JsonElement();

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        // Should return a valid date string, not an error
        Assert.DoesNotContain("Error", result);
        Assert.Contains(DateTime.Now.Year.ToString(), result);
    }

    // ── ReadFileTool ────────────────────────────────────────────────────

    [Fact]
    public async Task ReadFile_ExistingFile_ReturnsContent()
    {
        var tool = new ReadFileTool();
        var json = $$"""{"path":"{{_tempFile.Replace("\\", "\\\\")}}" }""";
        var args = JsonDocument.Parse(json).RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.Equal("Hello from tool tests!", result);
    }

    [Fact]
    public async Task ReadFile_NonexistentFile_ReturnsError()
    {
        var tool = new ReadFileTool();
        var bogus = Path.Combine(_tempDir, "does-not-exist.txt");
        var json = $$"""{"path":"{{bogus.Replace("\\", "\\\\")}}" }""";
        var args = JsonDocument.Parse(json).RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains("not found", result);
    }

    [Fact]
    public async Task ReadFile_BlockedPath_ReturnsError()
    {
        var tool = new ReadFileTool();
        var args = JsonDocument.Parse("""{"path":"/etc/shadow"}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains("blocked", result);
    }

    // ── ShellExecTool ───────────────────────────────────────────────────

    [Fact]
    public async Task ShellExec_EchoHello_ReturnsHello()
    {
        var tool = new ShellExecTool();
        var args = JsonDocument.Parse("""{"command":"echo hello"}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.Contains("hello", result);
    }

    [Fact]
    public async Task ShellExec_BlockedCommand_ReturnsError()
    {
        var tool = new ShellExecTool();
        var args = JsonDocument.Parse("""{"command":"rm -rf /"}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains("blocked", result);
    }

    [Fact]
    public async Task ShellExec_PipeWithBlockedCommand_ReturnsError()
    {
        var tool = new ShellExecTool();
        var args = JsonDocument.Parse("""{"command":"cat /etc/hostname | rm"}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains("blocked", result);
    }
}
