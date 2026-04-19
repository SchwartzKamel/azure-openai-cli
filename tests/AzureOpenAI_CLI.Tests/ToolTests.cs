// ToolTests.cs — BDD pilot conversion (ADR-003).
//
// Covers ToolRegistry, GetDateTimeTool, ReadFileTool, and ShellExecTool
// with one behaviour per test and Given/When/Then naming. Async tests
// use the Scenario DSL to demonstrate the narrative wrapper on the
// async chain.
//
// Changes relative to the pre-BDD version (14 tests):
//   + Comment/name drift fix (was "Five"/asserts 6): now consistently 6.
//   + Year-boundary flake (audit H1): regex match on 20\d{2} rather
//     than literal DateTime.Now.Year.ToString() — no flake across the
//     New Year boundary.
//   + ReadFile_ExistingFile: adds the missing negative assertion
//     (audit M7 — result must not start with "Error:").
//   + ShellExec success path: additional no-error assertion.
//   + One behaviour per test — renames to Given_X_When_Y_Then_Z.

using System.Text.Json;
using System.Text.RegularExpressions;
using AzureOpenAI_CLI.Tests.Bdd;
using AzureOpenAI_CLI.Tools;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// Behavioural tests for the built-in tool system. See ADR-003.
/// </summary>
[Trait("type", "behavior")]
public class ToolTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _tempFile;

    private static readonly Regex YearPattern =
        new(@"20\d{2}", RegexOptions.Compiled);

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
    public void Given_NoFilter_When_CreatingRegistry_Then_AllSixBuiltInToolsAreRegistered()
    {
        Scenario
            .Given("a null tools filter", () => (IReadOnlyCollection<string>?)null)
            .When("creating the tool registry",
                filter => ToolRegistry.Create(filter).All.Count)
            .Then("six built-in tools are registered",
                count => Assert.Equal(6, count))
            .And("the count is NOT the legacy five",
                count => Assert.NotEqual(5, count));
    }

    [Fact]
    public void Given_DatetimeFilter_When_CreatingRegistry_Then_OnlyDatetimeToolIsReturned()
    {
        var registry = ToolRegistry.Create(["datetime"]);

        Assert.Single(registry.All);
        Assert.Equal("get_datetime", registry.All.First().Name);
    }

    [Fact]
    public void Given_ShellFilter_When_CreatingRegistry_Then_OnlyShellExecToolIsReturned()
    {
        var registry = ToolRegistry.Create(["shell"]);

        Assert.Single(registry.All);
        Assert.Equal("shell_exec", registry.All.First().Name);
    }

    [Fact]
    public void Given_NoFilter_When_ConvertingToChatTools_Then_SixChatToolsAreEmitted()
    {
        var registry = ToolRegistry.Create(null);
        Assert.Equal(6, registry.ToChatTools().Count);
    }

    [Fact]
    public async Task Given_UnknownToolName_When_Executing_Then_ErrorMessageNamesTheTool()
    {
        await (await Scenario
            .Given("a registry with every built-in tool", () => ToolRegistry.Create(null))
            .WhenAsync("executing an unregistered tool name",
                r => r.ExecuteAsync("nonexistent_tool", "{}", CancellationToken.None)))
            .ThenAsync("the result mentions 'unknown tool'",
                result => { Assert.Contains("unknown tool", result); return Task.CompletedTask; });
    }

    [Fact]
    public async Task Given_UnknownToolName_When_Executing_Then_ErrorMessageContainsTheOffendingName()
    {
        var registry = ToolRegistry.Create(null);
        var result = await registry.ExecuteAsync("nonexistent_tool", "{}", CancellationToken.None);
        Assert.Contains("nonexistent_tool", result);
    }

    // ── GetDateTimeTool ─────────────────────────────────────────────────

    [Fact]
    public async Task Given_NoTimezone_When_CallingGetDateTime_Then_ACurrentYearStringIsReturned()
    {
        // Year-boundary safe: match 20\d{2} rather than literal current year
        // (audit finding H1).
        var ctx = await Scenario
            .Given("a fresh datetime tool", () => new GetDateTimeTool())
            .WhenAsync("executing it with empty args",
                tool => tool.ExecuteAsync(JsonDocument.Parse("{}").RootElement, CancellationToken.None));

        ctx.Then("the result contains a 4-digit 20xx year",
                r => Assert.Matches(YearPattern, r))
           .And("the result does NOT start with 'Error:'",
                r => Assert.DoesNotContain("Error", r));
    }

    [Fact]
    public async Task Given_InvalidTimezone_When_CallingGetDateTime_Then_ErrorNamesTheTimezone()
    {
        var tool = new GetDateTimeTool();
        var args = JsonDocument.Parse("""{"timezone":"Mars/Olympus_Mons"}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains("Mars/Olympus_Mons", result);
    }

    [Fact]
    public async Task Given_UndefinedArgs_When_CallingGetDateTime_Then_DefaultSucceedsWithoutError()
    {
        var tool = new GetDateTimeTool();
        var args = new JsonElement(); // ValueKind == Undefined simulates no arguments

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.DoesNotContain("Error", result);
        Assert.Matches(YearPattern, result);
    }

    // ── ReadFileTool ────────────────────────────────────────────────────

    [Fact]
    public async Task Given_ExistingFile_When_Reading_Then_ContentIsReturnedVerbatim()
    {
        var tool = new ReadFileTool();
        var json = $$"""{"path":"{{_tempFile.Replace("\\", "\\\\")}}" }""";
        var args = JsonDocument.Parse(json).RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.Equal("Hello from tool tests!", result);
    }

    [Fact]
    public async Task Given_ExistingFile_When_Reading_Then_NoErrorPrefixIsReturned()
    {
        // Audit M7: the previous version asserted only the positive. Pass the
        // pass, fail the fail.
        var tool = new ReadFileTool();
        var json = $$"""{"path":"{{_tempFile.Replace("\\", "\\\\")}}" }""";
        var args = JsonDocument.Parse(json).RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.False(result.StartsWith("Error:", StringComparison.Ordinal),
            $"Reading an existing file returned an error: {result}");
    }

    [Fact]
    public async Task Given_NonexistentFile_When_Reading_Then_NotFoundErrorIsReturned()
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
    public async Task Given_BlockedPath_When_Reading_Then_BlockedErrorIsReturned()
    {
        var tool = new ReadFileTool();
        var args = JsonDocument.Parse("""{"path":"/etc/shadow"}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains("blocked", result);
    }

    // ── ShellExecTool ───────────────────────────────────────────────────

    [Fact]
    public async Task Given_EchoCommand_When_ExecutingShell_Then_ExpectedStdoutIsReturned()
    {
        var ctx = await Scenario
            .Given("the shell-exec tool", () => new ShellExecTool())
            .WhenAsync("executing `echo hello`",
                t => t.ExecuteAsync(
                    JsonDocument.Parse("""{"command":"echo hello"}""").RootElement,
                    CancellationToken.None));

        ctx.Then("the output contains 'hello'", r => Assert.Contains("hello", r))
           .And("no error prefix is present", r => Assert.DoesNotContain("Error", r));
    }

    [Fact]
    public async Task Given_DestructiveCommand_When_ExecutingShell_Then_BlockedErrorIsReturned()
    {
        var tool = new ShellExecTool();
        var args = JsonDocument.Parse("""{"command":"rm -rf /"}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains("blocked", result);
    }

    [Fact]
    public async Task Given_PipeChainContainingBlockedCommand_When_ExecutingShell_Then_BlockedErrorIsReturned()
    {
        var tool = new ShellExecTool();
        var args = JsonDocument.Parse("""{"command":"cat /etc/hostname | rm"}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains("blocked", result);
    }
}
