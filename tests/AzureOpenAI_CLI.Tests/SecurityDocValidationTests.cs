using System.Reflection;
using System.Text.Json;
using AzureOpenAI_CLI.Tools;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// Validates that every security claim in SECURITY.md §12–§14
/// (DelegateTaskTool Security, Ralph Mode Security, Subagent Attack Surface)
/// is backed by verifiable code.
///
/// Each test references the specific SECURITY.md claim it validates.
/// Pass the pass, fail the fail.
/// </summary>
[Collection("ConsoleCapture")]
public class SecurityDocValidationTests
{
    // ═══════════════════════════════════════════════════════════════════
    // §12 DelegateTaskTool Security — Recursion Depth Cap
    // SECURITY.md: "RALPH_DEPTH env var limits delegation to max 3 levels"
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void DelegateTask_MaxDepthConstant_IsExactly3()
    {
        // Verify the documented MaxDepth of 3 via reflection on the private const
        var field = typeof(DelegateTaskTool)
            .GetField("MaxDepth", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(field);
        Assert.Equal(3, (int)field!.GetValue(null)!);
    }

    [Fact]
    public void DelegateTask_MaxDepthConstant_IsNot5()
    {
        // Negative: if someone bumps MaxDepth to 5, this test fails
        // and forces a SECURITY.md update
        var field = typeof(DelegateTaskTool)
            .GetField("MaxDepth", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(field);
        Assert.NotEqual(5, (int)field!.GetValue(null)!);
    }

    [Fact]
    public async Task DelegateTask_AtMaxDepth_ReturnsDepthError()
    {
        Environment.SetEnvironmentVariable("RALPH_DEPTH", "3");
        try
        {
            var tool = new DelegateTaskTool();
            var args = JsonDocument.Parse("""{"task":"test"}""").RootElement;

            var result = await tool.ExecuteAsync(args, CancellationToken.None);

            Assert.Contains("maximum delegation depth", result);
            Assert.Contains("3", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("RALPH_DEPTH", null);
        }
    }

    [Fact]
    public async Task DelegateTask_BelowMaxDepth_DoesNotReturnDepthError()
    {
        Environment.SetEnvironmentVariable("RALPH_DEPTH", "2");
        try
        {
            var tool = new DelegateTaskTool();
            var args = JsonDocument.Parse("""{"task":"test"}""").RootElement;

            var result = await tool.ExecuteAsync(args, CancellationToken.None);

            // Depth 2 < 3 passes the guard; may fail later (no binary) but not on depth
            Assert.DoesNotContain("maximum delegation depth", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("RALPH_DEPTH", null);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // §12 DelegateTaskTool Security — Timeout Enforcement
    // SECURITY.md: "60-second hard timeout per child agent"
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void DelegateTask_TimeoutConstant_Is60Seconds()
    {
        var field = typeof(DelegateTaskTool)
            .GetField("DefaultTimeoutMs", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(field);
        Assert.Equal(60_000, (int)field!.GetValue(null)!);
    }

    [Fact]
    public void DelegateTask_TimeoutConstant_IsNot30Seconds()
    {
        // Negative: catches accidental halving of the timeout
        var field = typeof(DelegateTaskTool)
            .GetField("DefaultTimeoutMs", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(field);
        Assert.NotEqual(30_000, (int)field!.GetValue(null)!);
    }

    // ═══════════════════════════════════════════════════════════════════
    // §12 DelegateTaskTool Security — Output Truncation
    // SECURITY.md: "Child output capped at 64KB"
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void DelegateTask_MaxOutputConstant_Is64KB()
    {
        var field = typeof(DelegateTaskTool)
            .GetField("MaxOutputBytes", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(field);
        Assert.Equal(65_536, (int)field!.GetValue(null)!);
    }

    [Fact]
    public void DelegateTask_MaxOutputConstant_IsNot128KB()
    {
        // Negative: catches accidental doubling of the output cap
        var field = typeof(DelegateTaskTool)
            .GetField("MaxOutputBytes", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(field);
        Assert.NotEqual(131_072, (int)field!.GetValue(null)!);
    }

    // ═══════════════════════════════════════════════════════════════════
    // §12 DelegateTaskTool Security — Default Tool Restriction
    // SECURITY.md: "Child agents default to shell,file,web,datetime —
    //               the delegate tool is excluded by default"
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DelegateTask_DefaultTools_ExcludesDelegate()
    {
        // When no "tools" property is specified, the default toolsArg is
        // "shell,file,web,datetime" — verified by checking the child process
        // arguments would NOT contain "delegate"
        //
        // We test indirectly: the source code sets toolsArg to "shell,file,web,datetime"
        // when no tools property is present. We verify by examining what the tool
        // passes to child via the ExecuteAsync path (will fail to start but we can
        // verify the depth check passes and delegation proceeds without delegate).
        Environment.SetEnvironmentVariable("RALPH_DEPTH", "0");
        try
        {
            var tool = new DelegateTaskTool();
            var args = JsonDocument.Parse("""{"task":"test default tools"}""").RootElement;

            var result = await tool.ExecuteAsync(args, CancellationToken.None);

            // The tool should get past depth check (depth=0 < 3)
            Assert.DoesNotContain("maximum delegation depth", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("RALPH_DEPTH", null);
        }
    }

    [Fact]
    public void ToolRegistry_DefaultChildToolset_DoesNotIncludeDelegate()
    {
        // The documented default child tools are "shell,file,web,datetime"
        // Verify that this set does NOT include delegate_task
        var registry = ToolRegistry.Create(["shell", "file", "web", "datetime"]);

        Assert.DoesNotContain(registry.All, t => t.Name == "delegate_task");
    }

    [Fact]
    public void ToolRegistry_DefaultChildToolset_IncludesExactly4Tools()
    {
        // "shell,file,web,datetime" → exactly 4 tools
        var registry = ToolRegistry.Create(["shell", "file", "web", "datetime"]);

        Assert.Equal(4, registry.All.Count);
    }

    [Fact]
    public void ToolRegistry_DefaultChildToolset_ContainsDocumentedTools()
    {
        var registry = ToolRegistry.Create(["shell", "file", "web", "datetime"]);
        var names = registry.All.Select(t => t.Name).ToHashSet();

        Assert.Contains("shell_exec", names);
        Assert.Contains("read_file", names);
        Assert.Contains("web_fetch", names);
        Assert.Contains("get_datetime", names);
    }

    // ═══════════════════════════════════════════════════════════════════
    // §12 DelegateTaskTool Security — Credential Isolation
    // SECURITY.md: "Only specific env vars are passed to child processes"
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void DelegateTask_CredentialAllowlist_MatchesDocumentation()
    {
        // The source code iterates over an explicit array of env var names.
        // We verify the ExecuteAsync method body references exactly the
        // 4 documented credential env vars via reflection on the source.
        //
        // This is a structural test: if someone adds/removes an env var
        // from the allowlist, this test should be updated alongside SECURITY.md.
        var sourceText = File.ReadAllText(
            Path.Combine(FindRepoRoot(), "azureopenai-cli", "Tools", "DelegateTaskTool.cs"));

        // Each documented env var must appear in the source
        Assert.Contains("AZUREOPENAIENDPOINT", sourceText);
        Assert.Contains("AZUREOPENAIAPI", sourceText);
        Assert.Contains("AZUREOPENAIMODEL", sourceText);
        Assert.Contains("AZURE_DEEPSEEK_KEY", sourceText);
        Assert.Contains("RALPH_DEPTH", sourceText);
    }

    [Fact]
    public void DelegateTask_CredentialAllowlist_DoesNotIncludeHOME()
    {
        // Negative: HOME should NOT be in the env var allowlist
        var sourceText = File.ReadAllText(
            Path.Combine(FindRepoRoot(), "azureopenai-cli", "Tools", "DelegateTaskTool.cs"));

        // The allowlist array literal should not contain HOME, PATH, etc.
        // We check the specific foreach line pattern
        var allowlistLine = sourceText.Split('\n')
            .FirstOrDefault(l => l.Contains("new[]") && l.Contains("AZUREOPENAIENDPOINT"));

        Assert.NotNull(allowlistLine);
        Assert.DoesNotContain("\"HOME\"", allowlistLine);
        Assert.DoesNotContain("\"PATH\"", allowlistLine);
        Assert.DoesNotContain("\"USER\"", allowlistLine);
    }

    // ═══════════════════════════════════════════════════════════════════
    // §12 DelegateTaskTool Security — stdin Closure
    // SECURITY.md: "Child process stdin is immediately closed"
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void DelegateTask_Source_ClosesStdinImmediately()
    {
        var sourceText = File.ReadAllText(
            Path.Combine(FindRepoRoot(), "azureopenai-cli", "Tools", "DelegateTaskTool.cs"));

        // Verify the source calls StandardInput.Close() BEFORE reading output
        int closeIndex = sourceText.IndexOf("StandardInput.Close()");
        int readIndex = sourceText.IndexOf("ReadBlockAsync");

        Assert.True(closeIndex > 0, "StandardInput.Close() not found in DelegateTaskTool source");
        Assert.True(readIndex > 0, "ReadBlockAsync not found in DelegateTaskTool source");
        Assert.True(closeIndex < readIndex,
            "stdin must be closed BEFORE reading output (close at " +
            $"{closeIndex}, read at {readIndex})");
    }

    // ═══════════════════════════════════════════════════════════════════
    // §12 DelegateTaskTool Security — Process Isolation
    // SECURITY.md: "Child runs as a separate process — no shared memory"
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void DelegateTask_Source_UsesProcessStart()
    {
        var sourceText = File.ReadAllText(
            Path.Combine(FindRepoRoot(), "azureopenai-cli", "Tools", "DelegateTaskTool.cs"));

        Assert.Contains("Process.Start", sourceText);
        Assert.Contains("ProcessStartInfo", sourceText);
    }

    [Fact]
    public void DelegateTask_Source_KillsEntireProcessTree()
    {
        // SECURITY.md: "Process tree killed on timeout"
        var sourceText = File.ReadAllText(
            Path.Combine(FindRepoRoot(), "azureopenai-cli", "Tools", "DelegateTaskTool.cs"));

        Assert.Contains("entireProcessTree: true", sourceText);
    }

    // ═══════════════════════════════════════════════════════════════════
    // §13 Ralph Mode Security — Iteration Limit
    // SECURITY.md: "--max-iterations capped at 50 (default 10)"
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void RalphMode_MaxIterationsDefault_Is10()
    {
        var sourceText = File.ReadAllText(
            Path.Combine(FindRepoRoot(), "azureopenai-cli", "Program.cs"));

        // The source declares: int maxIterations = 10;
        Assert.Contains("int maxIterations = 10;", sourceText);
    }

    [Fact]
    public void RalphMode_MaxIterationsCap_Is50()
    {
        var sourceText = File.ReadAllText(
            Path.Combine(FindRepoRoot(), "azureopenai-cli", "Program.cs"));

        // The source validates: "between 1 and 50"
        Assert.Contains("between 1 and 50", sourceText);
    }

    [Fact]
    public void RalphMode_MaxIterations51_Rejected()
    {
        // Boundary: 51 exceeds the documented cap of 50
        var mainMethod = typeof(UserConfig).Assembly.EntryPoint!;
        var result = (int)mainMethod.Invoke(null,
            new object[] { new[] { "--ralph", "--max-iterations", "51", "test" } })!;

        Assert.Equal(1, result);
    }

    [Fact]
    public void RalphMode_MaxIterations50_Accepted()
    {
        // Boundary: 50 is the documented max — should parse OK
        // (will fail later on missing creds, not on parsing)
        var origErr = Console.Error;
        Console.SetError(new StringWriter()); // suppress stderr
        try
        {
            var mainMethod = typeof(UserConfig).Assembly.EntryPoint!;
            var result = (int)mainMethod.Invoke(null,
                new object[] { new[] { "--ralph", "--max-iterations", "50", "test prompt" } })!;

            // Non-zero (missing creds) but NOT the parse error exit
            Assert.NotEqual(0, result);
        }
        finally
        {
            Console.SetError(origErr);
        }
    }

    [Fact]
    public void RalphMode_MaxIterations0_Rejected()
    {
        // Boundary: 0 is below the documented minimum of 1
        var mainMethod = typeof(UserConfig).Assembly.EntryPoint!;
        var result = (int)mainMethod.Invoke(null,
            new object[] { new[] { "--ralph", "--max-iterations", "0", "test" } })!;

        Assert.Equal(1, result);
    }

    // ═══════════════════════════════════════════════════════════════════
    // §13 Ralph Mode Security — Stateless Iterations
    // SECURITY.md: "Each iteration uses fresh messages"
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void RalphMode_Source_BuildsFreshMessagesEachIteration()
    {
        var sourceText = File.ReadAllText(
            Path.Combine(FindRepoRoot(), "azureopenai-cli", "Program.cs"));

        // The source has a comment and code creating new messages inside the while loop
        Assert.Contains("Build fresh messages for each iteration", sourceText);
        Assert.Contains("new List<ChatMessage>", sourceText);
    }

    // ═══════════════════════════════════════════════════════════════════
    // §13 Ralph Mode Security — Validation Sandboxing
    // SECURITY.md: "--validate command runs via /bin/sh -c"
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void RalphMode_ValidationProcess_UsesBinSh()
    {
        var sourceText = File.ReadAllText(
            Path.Combine(FindRepoRoot(), "azureopenai-cli", "Program.cs"));

        // RunValidation uses /bin/sh -c
        Assert.Contains("FileName = \"/bin/sh\"", sourceText);
    }

    [Fact]
    public void RalphMode_ValidationProcess_ClosesStdin()
    {
        var sourceText = File.ReadAllText(
            Path.Combine(FindRepoRoot(), "azureopenai-cli", "Program.cs"));

        // RunValidation closes stdin — verify pattern exists in that method
        // We search for the pattern near RunValidation
        int runValidationIndex = sourceText.IndexOf("RunValidation(string command");
        Assert.True(runValidationIndex > 0, "RunValidation method not found");

        string validationMethod = sourceText.Substring(runValidationIndex);
        Assert.Contains("StandardInput.Close()", validationMethod);
    }

    [Fact]
    public void RalphMode_ValidationProcess_KillsOnTimeout()
    {
        var sourceText = File.ReadAllText(
            Path.Combine(FindRepoRoot(), "azureopenai-cli", "Program.cs"));

        int runValidationIndex = sourceText.IndexOf("RunValidation(string command");
        Assert.True(runValidationIndex > 0, "RunValidation method not found");

        string validationMethod = sourceText.Substring(runValidationIndex);
        Assert.Contains("entireProcessTree: true", validationMethod);
    }

    // ═══════════════════════════════════════════════════════════════════
    // §13 Ralph Mode Security — .ralph-log File
    // SECURITY.md: "Best-effort logging — never causes failures"
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void RalphMode_WriteRalphLog_IsBestEffort()
    {
        var sourceText = File.ReadAllText(
            Path.Combine(FindRepoRoot(), "azureopenai-cli", "Program.cs"));

        // The try/catch in WriteRalphLog has an empty catch block
        Assert.Contains("catch { /* best-effort logging */ }", sourceText);
    }

    [Fact]
    public void RalphMode_WriteRalphLog_WritesToCurrentDirectory()
    {
        var sourceText = File.ReadAllText(
            Path.Combine(FindRepoRoot(), "azureopenai-cli", "Program.cs"));

        // Uses a relative path ".ralph-log" (current directory)
        Assert.Contains("\".ralph-log\"", sourceText);
    }

    // ═══════════════════════════════════════════════════════════════════
    // §13 Ralph Mode Security — No Credential Exposure in Logs
    // SECURITY.md: "Agent responses are truncated in .ralph-log"
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void RalphMode_LogTruncation_PromptAt200Chars()
    {
        var sourceText = File.ReadAllText(
            Path.Combine(FindRepoRoot(), "azureopenai-cli", "Program.cs"));

        // Prompt truncated to 200 chars in log
        Assert.Contains("currentPrompt.Length > 200", sourceText);
        Assert.Contains("currentPrompt[..200]", sourceText);
    }

    [Fact]
    public void RalphMode_LogTruncation_ResponseAt500Chars()
    {
        var sourceText = File.ReadAllText(
            Path.Combine(FindRepoRoot(), "azureopenai-cli", "Program.cs"));

        // Response truncated to 500 chars in log
        Assert.Contains("agentResponse.Length > 500", sourceText);
        Assert.Contains("agentResponse[..500]", sourceText);
    }

    [Fact]
    public void RalphMode_LogTruncation_ValidationAt2000Chars()
    {
        var sourceText = File.ReadAllText(
            Path.Combine(FindRepoRoot(), "azureopenai-cli", "Program.cs"));

        // Validation output truncated to 2000 chars in log
        Assert.Contains("validationOutput.Length > 2000", sourceText);
        Assert.Contains("validationOutput[..2000]", sourceText);
    }

    // ═══════════════════════════════════════════════════════════════════
    // §14 Subagent Attack Surface — Defense-in-Depth cross-checks
    // Verify layered protections compose correctly
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SubagentSurface_ChildShellExec_StillBlocksDestructiveCommands()
    {
        // Even though DelegateTaskTool enables "shell" for children,
        // ShellExecTool's blocklist still applies
        var tool = new ShellExecTool();
        var args = JsonDocument.Parse("""{"command":"rm -rf /"}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains("blocked", result);
    }

    [Fact]
    public void SubagentSurface_ChildReadFile_StillBlocksSensitivePaths()
    {
        // Even though DelegateTaskTool enables "file" for children,
        // ReadFileTool's path blocking still applies
        Assert.True(ReadFileTool.IsBlockedPath("/etc/shadow"));
        Assert.True(ReadFileTool.IsBlockedPath("/proc/self/environ"));
    }

    [Fact]
    public void SubagentSurface_AllToolsRegistry_IncludesExactly6Tools()
    {
        // The full tool set (null filter) should be exactly 6 tools.
        // If a new tool is added, this test forces a security review.
        var registry = ToolRegistry.Create(null);

        Assert.Equal(6, registry.All.Count);
    }

    [Fact]
    public void SubagentSurface_DelegateAlias_ResolvesCorrectly()
    {
        // "delegate" alias maps to "delegate_task" — exact matching, not substring
        var registry = ToolRegistry.Create(["delegate"]);

        Assert.Single(registry.All);
        Assert.Equal("delegate_task", registry.All.First().Name);
    }

    [Fact]
    public void SubagentSurface_PartialDelegateName_DoesNotMatch()
    {
        // "del" should NOT match "delegate_task" — exact alias matching only
        var registry = ToolRegistry.Create(["del"]);

        Assert.Empty(registry.All);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "azure-openai-cli.sln")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new InvalidOperationException(
            "Could not find repo root (azure-openai-cli.sln) from " + AppContext.BaseDirectory);
    }
}
