using AzureOpenAI_CLI.Mcp;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// S04E25 -- giving <c>--mcp-server</c> a URL used to fail with "error trying to start process 'http://...'", which says nothing
/// about the cause (hopper's MCP client launches a command; it has no HTTP transport). A URL is now named as the problem, the
/// bridge is named as the way out, and nothing secret in the URL is ever echoed.
/// </summary>
public class McpUrlSpecTests
{
    [Theory]
    [InlineData("http://127.0.0.1:13340/mcp", "http://127.0.0.1:13340/mcp")]
    [InlineData("https://example.com", "https://example.com")]
    [InlineData("https://example.com/", "https://example.com")]
    [InlineData("scouter=http://127.0.0.1:13340/mcp", "http://127.0.0.1:13340/mcp")]   // with a name=
    [InlineData("  HTTPS://Example.COM/mcp  ", "https://example.com/mcp")]               // case and whitespace
    [InlineData("wss://example.com/socket", "wss://example.com/socket")]
    [InlineData("ws://localhost:8080", "ws://localhost:8080")]
    [InlineData("https://[::1]:8443/mcp", "https://[::1]:8443/mcp")]
    [InlineData("\"https://example.com/mcp\"", "https://example.com/mcp")]               // quoted
    public void AUrlSpec_IsRecognised_AndReturnedInSafeForm(string spec, string expected)
        => Assert.Equal(expected, McpStdioClient.DescribeUrlSpec(spec));

    [Theory]
    [InlineData("https://user:SECRETPW@host.example/mcp?token=SECRETTOK#frag")]
    [InlineData("name=https://user:SECRETPW@host.example/mcp?token=SECRETTOK")]
    [InlineData("https://host.example/mcp?access_token=SECRETTOK&x=1")]                 // '=' in a query must not become name=
    public void CredentialsInTheUrl_NeverAppearInTheSafeForm(string spec)
    {
        var safe = McpStdioClient.DescribeUrlSpec(spec);

        Assert.NotNull(safe);
        Assert.DoesNotContain("SECRETPW", safe);
        Assert.DoesNotContain("SECRETTOK", safe);
        Assert.DoesNotContain("?", safe);
        Assert.DoesNotContain("@", safe);
        Assert.Equal("https://host.example/mcp", safe);
    }

    [Theory]
    [InlineData("python3 examples/mcp/http-bridge.py https://host.example/mcp")]        // the intended use: a URL as an ARGUMENT
    [InlineData("scouter=python3 examples/mcp/http-bridge.py http://127.0.0.1:13340/mcp")]
    [InlineData("hopper mcp serve")]
    [InlineData("fs=npx -y @modelcontextprotocol/server-filesystem .")]
    [InlineData("agent-toolkit-mcp")]
    [InlineData("/usr/local/bin/my-server --port=8080")]                                // '=' in an argument
    [InlineData("name=/opt/server --url=https://host/mcp")]
    [InlineData("file:///opt/server")]                                                  // not a network scheme
    [InlineData("C:\\tools\\server.exe")]
    [InlineData("")]
    [InlineData("   ")]
    public void ACommand_IsNeverFlagged(string spec)
        => Assert.Null(McpStdioClient.DescribeUrlSpec(spec));

    [Fact]
    public async Task Connect_WithAUrl_ThrowsTheExplanation_NamingTheBridge_AndStartsNoProcess()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => McpStdioClient.ConnectSubprocessAsync("https://user:SECRETPW@host.example/mcp?token=SECRETTOK", timeoutSeconds: 1));

        Assert.Contains("is a URL", ex.Message);
        Assert.Contains("no HTTP transport", ex.Message);
        Assert.Contains("http-bridge.py", ex.Message);
        Assert.Contains("MCP_BRIDGE_TOKEN_FILE", ex.Message);
        Assert.Contains("https://host.example/mcp", ex.Message);
        Assert.DoesNotContain("SECRETPW", ex.Message);
        Assert.DoesNotContain("SECRETTOK", ex.Message);
        Assert.DoesNotContain("trying to start process", ex.Message); // the old, misleading message
    }

    [Fact]
    public async Task Connect_WithACommandThatDoesNotExist_StillFailsAsBefore()
    {
        // The check must not swallow ordinary start-up failures.
        var ex = await Record.ExceptionAsync(() => McpStdioClient.ConnectSubprocessAsync("definitely-not-a-real-command-xyz", timeoutSeconds: 1));

        Assert.NotNull(ex);
        Assert.DoesNotContain("is a URL", ex!.Message);
    }
}
