using System.Net;
using System.Text.Json;
using AzureOpenAI_CLI.Tools;

namespace AzureOpenAI_CLI.Tests.Adversary;

/// <summary>
/// S02E23 -- The Adversary. FDR's SSRF / private-IP / DNS-rebinding /
/// redirect-chain attacks against <see cref="WebFetchTool"/>.
///
/// Tests confirming the CURRENT defense are <c>[Fact]</c>. Tests that
/// surface a live gap (DNS TOCTOU, decimal IP encoding) are
/// <c>[Fact(Skip = "Live finding: e23-...")]</c>. The redirect-chain
/// path is unit-testable via the test-only HttpMessageHandler ctor on
/// WebFetchTool; the live-DNS paths use the static
/// <see cref="WebFetchTool.IsPrivateAddress"/> /
/// <see cref="WebFetchTool.ValidateRedirectedUriAsync"/> entry points.
/// </summary>
public class WebFetchSSRFTests
{
    private static JsonElement Args(string url)
    {
        var json = JsonSerializer.Serialize(new { url });
        return JsonDocument.Parse(json).RootElement;
    }

    // ===================================================================
    // Defenses that hold today
    // ===================================================================

    [Theory]
    [InlineData("http://example.com/")]
    [InlineData("ftp://example.com/")]
    [InlineData("file:///etc/passwd")]
    [InlineData("gopher://example.com/")]
    [InlineData("javascript:alert(1)")]
    public async Task NonHttpsScheme_Rejected(string url)
    {
        var tool = new WebFetchTool();
        var result = await tool.ExecuteAsync(Args(url), CancellationToken.None);
        Assert.StartsWith("Error:", result);
        Assert.Contains("HTTPS", result);
    }

    [Theory]
    [InlineData("127.0.0.1")]      // loopback
    [InlineData("127.255.255.254")] // loopback range
    [InlineData("10.0.0.1")]        // RFC1918
    [InlineData("172.16.0.1")]      // RFC1918
    [InlineData("172.31.255.254")]  // RFC1918 upper
    [InlineData("192.168.1.1")]     // RFC1918
    [InlineData("169.254.169.254")] // link-local / AWS IMDS
    public void IsPrivateAddress_IPv4_Blocked(string ip)
    {
        Assert.True(WebFetchTool.IsPrivateAddress(IPAddress.Parse(ip)));
    }

    [Theory]
    [InlineData("::1")]                 // IPv6 loopback
    [InlineData("fd00::1")]             // ULA
    [InlineData("fe80::1")]             // link-local
    [InlineData("::ffff:127.0.0.1")]    // IPv4-mapped IPv6 loopback
    [InlineData("::ffff:10.0.0.1")]     // IPv4-mapped IPv6 RFC1918
    [InlineData("::ffff:169.254.169.254")] // IPv4-mapped IPv6 IMDS
    public void IsPrivateAddress_IPv6_Blocked(string ip)
    {
        Assert.True(WebFetchTool.IsPrivateAddress(IPAddress.Parse(ip)));
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("2606:4700:4700::1111")]
    public void IsPrivateAddress_Public_Allowed(string ip)
    {
        Assert.False(WebFetchTool.IsPrivateAddress(IPAddress.Parse(ip)));
    }

    [Fact]
    public async Task ValidateRedirectedUri_AwsImdsHttp_Rejected()
    {
        var uri = new Uri("http://169.254.169.254/latest/meta-data/");
        var result = await WebFetchTool.ValidateRedirectedUriAsync(uri, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Contains("non-HTTPS", result);
    }

    [Fact]
    public async Task ValidateRedirectedUri_AwsImdsHttps_Rejected()
    {
        // Even if attacker tricks us with HTTPS to IMDS, the IP-range
        // check in ValidateRedirectedUriAsync catches 169.254.0.0/16.
        var uri = new Uri("https://169.254.169.254/latest/meta-data/");
        var result = await WebFetchTool.ValidateRedirectedUriAsync(uri, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Contains("private", result.ToLower());
    }

    [Fact]
    public async Task RedirectChain_FromPublicToLoopback_Rejected()
    {
        // Mock handler simulates a 302 redirect chain landing on
        // https://localhost (which DNS-resolves to 127.0.0.1).
        var handler = new RedirectStub(new Uri("https://localhost/secret"));
        var tool = new WebFetchTool(handler);
        var result = await tool.ExecuteAsync(Args("https://1.1.1.1/start"), CancellationToken.None);
        Assert.StartsWith("Error:", result);
        Assert.Contains("private", result.ToLower());
    }

    [Fact]
    public async Task RedirectChain_FromHttpsToHttp_Rejected()
    {
        var handler = new RedirectStub(new Uri("http://example.com/insecure"));
        var tool = new WebFetchTool(handler);
        var result = await tool.ExecuteAsync(Args("https://1.1.1.1/start"), CancellationToken.None);
        Assert.StartsWith("Error:", result);
        Assert.Contains("non-HTTPS", result);
    }

    [Fact]
    public async Task PreflightDns_BogusHost_RejectsCleanly()
    {
        // Unresolvable hostname returns an error string, not an exception.
        var tool = new WebFetchTool();
        var result = await tool.ExecuteAsync(
            Args("https://does-not-exist-s02e23-adversary-probe.invalid/"),
            CancellationToken.None);
        Assert.StartsWith("Error:", result);
    }

    // ===================================================================
    // LIVE FINDINGS surfaced by FDR -- Skipped, with finding name
    // ===================================================================

    [Fact(Skip = "Live finding: e23-webfetch-dns-rebinding-toctou")]
    public void DnsRebinding_TimeOfCheckTimeOfUse_ShouldBeMitigated()
    {
        // The pre-flight Dns.GetHostAddressesAsync resolves the
        // hostname BEFORE HttpClient does its own resolution. A
        // hostile DNS server can answer the first lookup with a
        // public IP and the second lookup (microseconds later) with
        // 169.254.169.254 / 127.0.0.1 / 10.x. The post-redirect
        // re-validation only runs after a redirect, not on the
        // initial request.
        //
        // Mitigation is structural (resolve once, then connect to the
        // resolved IP with Host header preserved). Pinned here as a
        // placeholder; reproducing it requires a controlled DNS
        // server, which lives outside the test project.
        Assert.Fail("structural fix required -- see finding e23-webfetch-dns-rebinding-toctou");
    }

    [Fact(Skip = "Live finding: e23-webfetch-decimal-ip-encoding-untested")]
    public async Task DecimalIpEncoding_LoopbackBlocked()
    {
        // 2130706433 == 127.0.0.1. Some resolvers (and Uri parsers)
        // accept the decimal form. WebFetchTool relies on
        // Dns.GetHostAddressesAsync which on glibc commonly resolves
        // "2130706433" to 127.0.0.1 -- which IsPrivateAddress would
        // catch -- but on alternate stdlib implementations the parser
        // may accept it as a literal int and skip DNS altogether. No
        // test pins the behavior either way today.
        var tool = new WebFetchTool();
        var result = await tool.ExecuteAsync(Args("https://2130706433/"), CancellationToken.None);
        Assert.StartsWith("Error:", result);
        Assert.Contains("private", result.ToLower());
    }

    [Fact(Skip = "Live finding: e23-webfetch-multicast-broadcast-not-blocked")]
    public void IsPrivateAddress_MulticastAndBroadcast_ShouldBeBlocked()
    {
        // 224.0.0.0/4 (multicast) and 255.255.255.255 (broadcast)
        // are not RFC1918 but are not safe SSRF targets either --
        // multicast can hit on-LAN services (mDNS, SSDP) and reveal
        // network topology.
        Assert.True(WebFetchTool.IsPrivateAddress(IPAddress.Parse("224.0.0.1")));
        Assert.True(WebFetchTool.IsPrivateAddress(IPAddress.Parse("239.255.255.250"))); // SSDP
        Assert.True(WebFetchTool.IsPrivateAddress(IPAddress.Parse("255.255.255.255")));
    }

    [Fact(Skip = "Live finding: e23-webfetch-cgnat-100_64-not-blocked")]
    public void IsPrivateAddress_Cgnat100_64_ShouldBeBlocked()
    {
        // 100.64.0.0/10 is RFC6598 carrier-grade NAT space. Not
        // RFC1918 but also not routable on the public internet --
        // SSRF target on customer networks behind CGNAT.
        Assert.True(WebFetchTool.IsPrivateAddress(IPAddress.Parse("100.64.0.1")));
        Assert.True(WebFetchTool.IsPrivateAddress(IPAddress.Parse("100.127.255.254")));
    }

    // ===================================================================
    // Test infrastructure
    // ===================================================================

    /// <summary>
    /// HttpMessageHandler that simulates a single hop ending at
    /// <paramref name="finalUri"/> -- mirrors how HttpClientHandler
    /// reports the post-redirect URL on RequestMessage.RequestUri.
    /// </summary>
    private sealed class RedirectStub : HttpMessageHandler
    {
        private readonly Uri _finalUri;
        public RedirectStub(Uri finalUri) => _finalUri = finalUri;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("redirected"),
                RequestMessage = new HttpRequestMessage(request.Method, _finalUri),
            });
    }
}
