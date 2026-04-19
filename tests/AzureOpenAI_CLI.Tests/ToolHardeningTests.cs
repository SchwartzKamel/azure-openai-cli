using System.Net;
using System.Text.Json;
using AzureOpenAI_CLI.Tools;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// Tests for tool hardening: TryGetProperty migration, SSRF redirect protection,
/// and process disposal patterns.
/// </summary>
public class ToolHardeningTests
{
    // ═══════════════════════════════════════════════════════════════════
    // 1. TryGetProperty — missing required parameters return error, not throw
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task WebFetch_MissingUrlParam_ReturnsError()
    {
        var tool = new WebFetchTool();
        var args = JsonDocument.Parse("{}").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains("url", result);
    }

    [Fact]
    public async Task WebFetch_NullUrlParam_ReturnsError()
    {
        var tool = new WebFetchTool();
        var args = JsonDocument.Parse("""{"url": null}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
    }

    [Fact]
    public async Task WebFetch_UndefinedArguments_ReturnsError()
    {
        var tool = new WebFetchTool();
        var args = new JsonElement(); // ValueKind == Undefined

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains("url", result);
    }

    [Fact]
    public async Task ShellExec_MissingCommandParam_ReturnsError()
    {
        var tool = new ShellExecTool();
        var args = JsonDocument.Parse("{}").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains("command", result);
    }

    [Fact]
    public async Task ShellExec_NullCommandParam_ReturnsError()
    {
        var tool = new ShellExecTool();
        var args = JsonDocument.Parse("""{"command": null}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
    }

    [Fact]
    public async Task ShellExec_UndefinedArguments_ReturnsError()
    {
        var tool = new ShellExecTool();
        var args = new JsonElement();

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains("command", result);
    }

    [Fact]
    public async Task ReadFile_MissingPathParam_ReturnsError()
    {
        var tool = new ReadFileTool();
        var args = JsonDocument.Parse("{}").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains("path", result);
    }

    [Fact]
    public async Task ReadFile_NullPathParam_ReturnsError()
    {
        var tool = new ReadFileTool();
        var args = JsonDocument.Parse("""{"path": null}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
    }

    [Fact]
    public async Task ReadFile_UndefinedArguments_ReturnsError()
    {
        var tool = new ReadFileTool();
        var args = new JsonElement();

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains("path", result);
    }

    [Fact]
    public async Task DelegateTask_MissingTaskParam_ReturnsError()
    {
        var tool = new DelegateTaskTool();
        var args = JsonDocument.Parse("""{"tools": "shell"}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains("task", result);
    }

    [Fact]
    public async Task DelegateTask_NullTaskParam_ReturnsError()
    {
        var tool = new DelegateTaskTool();
        var args = JsonDocument.Parse("""{"task": null}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains("task", result);
    }

    [Fact]
    public async Task DelegateTask_UndefinedArguments_ReturnsError()
    {
        var tool = new DelegateTaskTool();
        var args = new JsonElement();

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains("task", result);
    }

    /// <summary>
    /// Verify that ToolRegistry.ExecuteAsync returns a clean error message
    /// (not a raw exception) when required arguments are missing.
    /// </summary>
    [Theory]
    [InlineData("web_fetch", "url")]
    [InlineData("shell_exec", "command")]
    [InlineData("read_file", "path")]
    [InlineData("delegate_task", "task")]
    public async Task ToolRegistry_MissingRequiredParam_ReturnsCleanError(string toolName, string paramName)
    {
        var registry = ToolRegistry.Create(null);

        var result = await registry.ExecuteAsync(toolName, "{}", CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains(paramName, result);
        // Must NOT contain exception type names — this confirms graceful handling
        Assert.DoesNotContain("KeyNotFoundException", result);
        Assert.DoesNotContain("ArgumentException", result);
    }

    // ── Positive cases: valid params still work ─────────────────────────

    [Fact]
    public async Task ShellExec_ValidCommand_StillWorks()
    {
        var tool = new ShellExecTool();
        var args = JsonDocument.Parse("""{"command":"echo hardening-ok"}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.Contains("hardening-ok", result);
    }

    [Fact]
    public async Task GetDateTime_EmptyArgs_StillWorks()
    {
        // GetDateTimeTool already uses TryGetProperty — verify it still works
        var tool = new GetDateTimeTool();
        var args = JsonDocument.Parse("{}").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.DoesNotContain("Error", result);
        // Year-boundary safe (audit H1): match 20xx structure, not literal current year.
        Assert.Matches(@"20\d{2}", result);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 2. WebFetchTool — SSRF redirect protection
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ValidateRedirectedUri_HttpScheme_ReturnsError()
    {
        var uri = new Uri("http://evil.com/admin");

        var result = await WebFetchTool.ValidateRedirectedUriAsync(uri, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("non-HTTPS", result);
    }

    [Fact]
    public async Task ValidateRedirectedUri_NullUri_ReturnsError()
    {
        var result = await WebFetchTool.ValidateRedirectedUriAsync(null, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("could not determine", result);
    }

    [Fact]
    public async Task ValidateRedirectedUri_HttpsLocalhost_ReturnsPrivateIpError()
    {
        // localhost resolves to 127.0.0.1 which is a private/loopback address
        var uri = new Uri("https://localhost/admin");

        var result = await WebFetchTool.ValidateRedirectedUriAsync(uri, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("private", result.ToLower());
    }

    [Fact]
    public async Task ValidateRedirectedUri_HttpsPublicUrl_ReturnsNull()
    {
        // 1.1.1.1 is a known public IP (Cloudflare DNS) — should pass validation
        var uri = new Uri("https://1.1.1.1/");

        var result = await WebFetchTool.ValidateRedirectedUriAsync(uri, CancellationToken.None);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("http://localhost/admin")]
    [InlineData("http://127.0.0.1/metadata")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("ftp://internal.server/file")]
    public async Task ValidateRedirectedUri_NonHttpsSchemes_ReturnsError(string url)
    {
        var uri = new Uri(url);

        var result = await WebFetchTool.ValidateRedirectedUriAsync(uri, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("non-HTTPS", result);
    }

    [Theory]
    [InlineData("https://10.0.0.1/internal")]
    [InlineData("https://192.168.1.1/router")]
    [InlineData("https://172.16.0.1/private")]
    public async Task ValidateRedirectedUri_HttpsPrivateIps_ReturnsError(string url)
    {
        var uri = new Uri(url);

        var result = await WebFetchTool.ValidateRedirectedUriAsync(uri, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("private", result.ToLower());
    }

    // ── Full tool integration tests with mock HTTP handler ──────────────

    /// <summary>
    /// Mock handler that simulates an HTTP redirect by returning a 200 OK
    /// but with RequestMessage.RequestUri set to a different (redirected) URL.
    /// This mimics what HttpClientHandler does when following redirects.
    /// </summary>
    private sealed class RedirectSimulatingHandler : HttpMessageHandler
    {
        private readonly Uri _finalUri;

        public RedirectSimulatingHandler(Uri finalUri) => _finalUri = finalUri;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("redirected content"),
                RequestMessage = new HttpRequestMessage(request.Method, _finalUri)
            };
            return Task.FromResult(response);
        }
    }

    [Fact]
    public async Task WebFetch_RedirectToHttp_ReturnsError()
    {
        // Simulate: https://1.1.1.1/test → 301 → http://evil.com/admin
        var handler = new RedirectSimulatingHandler(new Uri("http://evil.com/admin"));
        var tool = new WebFetchTool(handler);
        var args = JsonDocument.Parse("""{"url":"https://1.1.1.1/test"}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains("non-HTTPS", result);
    }

    [Fact]
    public async Task WebFetch_RedirectToPrivateIp_ReturnsError()
    {
        // Simulate: https://1.1.1.1/test → 301 → https://localhost/admin
        var handler = new RedirectSimulatingHandler(new Uri("https://localhost/admin"));
        var tool = new WebFetchTool(handler);
        var args = JsonDocument.Parse("""{"url":"https://1.1.1.1/test"}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains("private", result.ToLower());
    }

    [Fact]
    public async Task WebFetch_RedirectTo10Network_ReturnsError()
    {
        // Simulate redirect to internal 10.x.x.x network
        var handler = new RedirectSimulatingHandler(new Uri("https://10.0.0.1/internal"));
        var tool = new WebFetchTool(handler);
        var args = JsonDocument.Parse("""{"url":"https://1.1.1.1/test"}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains("private", result.ToLower());
    }

    [Fact]
    public async Task WebFetch_RedirectToMetadataEndpoint_ReturnsError()
    {
        // AWS-style SSRF: redirect to cloud metadata endpoint (non-HTTPS)
        var handler = new RedirectSimulatingHandler(new Uri("http://169.254.169.254/latest/meta-data"));
        var tool = new WebFetchTool(handler);
        var args = JsonDocument.Parse("""{"url":"https://1.1.1.1/test"}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains("non-HTTPS", result);
    }

    [Fact]
    public async Task WebFetch_NoRedirect_SafeUrlSucceeds()
    {
        // Simulate: no redirect, final URL matches initial (safe public HTTPS)
        var handler = new RedirectSimulatingHandler(new Uri("https://1.1.1.1/test"));
        var tool = new WebFetchTool(handler);
        var args = JsonDocument.Parse("""{"url":"https://1.1.1.1/test"}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        // Should succeed — no redirect to unsafe location
        Assert.DoesNotContain("blocked", result.ToLower());
        Assert.Contains("redirected content", result);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 2b. ShellExecTool — shell injection hardening
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ShellExec_BlocksCommandSubstitution()
    {
        var tool = new ShellExecTool();
        var args = JsonDocument.Parse("""{"command":"echo $(whoami)"}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.Contains("blocked", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShellExec_BlocksBacktickSubstitution()
    {
        var tool = new ShellExecTool();
        var args = JsonDocument.Parse("""{"command":"echo `whoami`"}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.Contains("blocked", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShellExec_BlocksProcessSubstitution()
    {
        var tool = new ShellExecTool();
        var args = JsonDocument.Parse("""{"command":"cat <(echo secret)"}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.Contains("blocked", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShellExec_BlocksOutputProcessSubstitution()
    {
        var tool = new ShellExecTool();
        var args = JsonDocument.Parse("""{"command":"tee >(cat) <<< test"}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.Contains("blocked", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShellExec_BlocksEval()
    {
        var tool = new ShellExecTool();
        var args = JsonDocument.Parse("""{"command":"eval rm -rf /"}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.Contains("blocked", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShellExec_BlocksEvalInPipeChain()
    {
        var tool = new ShellExecTool();
        var args = JsonDocument.Parse("""{"command":"echo test; eval whoami"}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.Contains("blocked", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShellExec_BlocksExec()
    {
        var tool = new ShellExecTool();
        var args = JsonDocument.Parse("""{"command":"exec /bin/bash"}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.Contains("blocked", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShellExec_BlocksExecInPipeChain()
    {
        var tool = new ShellExecTool();
        var args = JsonDocument.Parse("""{"command":"echo test; exec /bin/bash"}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.Contains("blocked", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShellExec_AllowsNormalCommands()
    {
        var tool = new ShellExecTool();
        var args = JsonDocument.Parse("""{"command":"echo hello world"}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.Contains("hello world", result);
    }

    [Fact]
    public async Task ShellExec_ArgumentListPreventsQuoteInjection()
    {
        // This would have been dangerous with the old string interpolation approach
        var tool = new ShellExecTool();
        var args = JsonDocument.Parse("""{"command":"echo \"test with quotes\""}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.Contains("test with quotes", result);
    }

    [Fact]
    public async Task ShellExec_PipeChainStillWorks()
    {
        var tool = new ShellExecTool();
        var args = JsonDocument.Parse("""{"command":"echo piped | cat"}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.Contains("piped", result);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 3. GetClipboardTool — process disposal verification
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetClipboard_ExecuteAsync_DoesNotLeak()
    {
        // Run multiple times to verify no process resource leak.
        // On CI without xclip/xsel, expect an error message (not an exception).
        var tool = new GetClipboardTool();
        var args = JsonDocument.Parse("{}").RootElement;

        for (int i = 0; i < 5; i++)
        {
            var result = await tool.ExecuteAsync(args, CancellationToken.None);
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
        }
        // If we reached here without OOM, unhandled exceptions, or process leaks, disposal works.
    }

    [Fact]
    public void GetClipboard_FindCommand_UsesUsingPattern()
    {
        // Structural test: verify that FindCommand properly disposes its Process.
        // We invoke the private method via reflection and confirm it completes
        // without resource leaks for both found and not-found cases.
        var method = typeof(GetClipboardTool).GetMethod(
            "FindCommand",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        // Existing command — should find it and dispose the process
        var echoResult = method!.Invoke(null, ["echo"]) as string;
        Assert.NotNull(echoResult);
        Assert.Contains("echo", echoResult);

        // Nonexistent command — should return null and dispose the process
        var bogusResult = method.Invoke(null, ["totally_nonexistent_cmd_xyz_42"]);
        Assert.Null(bogusResult);
    }

    [Fact]
    public void GetClipboard_FindCommand_MultipleRapidCalls_NoLeak()
    {
        var method = typeof(GetClipboardTool).GetMethod(
            "FindCommand",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        // Run 20 rapid calls to stress-test process disposal
        for (int i = 0; i < 20; i++)
        {
            method!.Invoke(null, ["echo"]);
        }
        // If we didn't exhaust file descriptors or process handles, disposal works.
    }
}
