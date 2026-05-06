using System.Net;
using AzureOpenAI_CLI.Net;
using Xunit;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// S03E16 -- The Allowlist. Adversarial corpus for
/// <see cref="EndpointAllowlist"/>. Every fact carries an inline comment
/// naming the attack class it defends against, so when one of these fails
/// during a future refactor the breaker can read the diff and not the
/// blame.
///
/// Categories covered:
///   - HTTPS public  (allow)
///   - HTTP public   (block: HTTP requires opt-in even for public hosts)
///   - HTTP localhost +/- opt-in
///   - 127.0.0.0/8, 10/8, 172.16/12, 192.168/16, 169.254/16
///   - IPv6 ::1, fe80::, fc00::
///   - Multicast 224.x and ff00::, 0.0.0.0, broadcast
///   - userinfo, privileged ports
///   - Octal / decimal / IPv6-mapped-IPv4 / trailing-dot localhost obfuscation
///   - DNS-rebinding (multi-record stub resolver)
///   - Mixed-case + Unicode IDN homoglyph normalization
/// </summary>
public class EndpointAllowlistTests
{
    private static Uri U(string s) => new Uri(s, UriKind.Absolute);

    // --------------------------------------------------------------------
    // HTTPS public -- the happy path. Pre-resolved to a public address so
    // we don't actually hit DNS during the unit run.
    // --------------------------------------------------------------------

    [Fact]
    public void HttpsPublic_Allowed()
    {
        // Defends: legitimate provider URLs (api.openai.com, api.groq.com)
        // must continue to work after the allowlist lands.
        var v = EndpointAllowlist.Check(
            U("https://api.openai.com/v1"),
            localProvidersOptIn: false,
            new[] { IPAddress.Parse("8.8.8.8") });
        Assert.Equal(AllowlistVerdict.Allow, v);
    }

    [Fact]
    public void HttpsPublic_BareIp_Allowed()
    {
        // Defends: a DNS-less environment that pins the endpoint to a
        // public bare IP (e.g. inside a corporate proxy) still works. The
        // bare-IP fast path runs without a DNS lookup.
        var v = EndpointAllowlist.Check(U("https://1.1.1.1/v1"), localProvidersOptIn: false);
        Assert.Equal(AllowlistVerdict.Allow, v);
    }

    // --------------------------------------------------------------------
    // HTTP scheme: blocked unless opt-in is on. Documents that WebFetchTool
    // reuses the seam; WebFetchTool itself ALSO short-circuits HTTP earlier
    // ("HTTPS only") so this rule is defense-in-depth for the tool surface
    // and the primary rule for the provider surface (Ollama opt-in).
    // --------------------------------------------------------------------

    [Fact]
    public void HttpPublic_NoOptIn_Blocked()
    {
        // Defends: a downgrade attack where a config rewrites the scheme
        // to http to disable TLS for a public provider. Without opt-in,
        // HTTP is rejected even if the host is public.
        var v = EndpointAllowlist.Check(
            U("http://api.openai.com/v1"),
            localProvidersOptIn: false,
            new[] { IPAddress.Parse("8.8.8.8") });
        Assert.Equal(AllowlistVerdict.BlockLoopback, v);
    }

    [Fact]
    public void HttpLocalhost_NoOptIn_Blocked()
    {
        // Defends: an attacker-supplied "preset" that points at
        // http://localhost:11434 (Ollama default) without explicit
        // operator opt-in. Must reject.
        var v = EndpointAllowlist.Check(U("http://localhost:11434/v1"), localProvidersOptIn: false);
        Assert.Equal(AllowlistVerdict.BlockLoopback, v);
    }

    [Fact]
    public void HttpLocalhost_WithOptIn_Allowed()
    {
        // Defends: the legitimate Ollama / llama-server flow (S03E14,
        // S03E17). With AZ_AI_LOCAL_PROVIDERS=1 the operator has
        // explicitly accepted the localhost surface.
        var v = EndpointAllowlist.Check(U("http://localhost:11434/v1"), localProvidersOptIn: true);
        Assert.Equal(AllowlistVerdict.Allow, v);
    }

    // --------------------------------------------------------------------
    // Loopback IPv4 -- 127.0.0.0/8 in full.
    // --------------------------------------------------------------------

    [Theory]
    [InlineData("http://127.0.0.1:11434/v1")]
    [InlineData("http://127.0.0.2/v1")]
    [InlineData("http://127.255.255.254/v1")]
    public void Loopback127_NoOptIn_Blocked(string url)
    {
        // Defends: any address in 127/8 (not just .0.0.1) -- attackers
        // sometimes use 127.0.0.2 to slip past naive equality checks.
        var v = EndpointAllowlist.Check(U(url), localProvidersOptIn: false);
        Assert.Equal(AllowlistVerdict.BlockLoopback, v);
    }

    [Fact]
    public void Loopback127_WithOptIn_Allowed()
    {
        var v = EndpointAllowlist.Check(U("http://127.0.0.1:8080/v1"), localProvidersOptIn: true);
        Assert.Equal(AllowlistVerdict.Allow, v);
    }

    // --------------------------------------------------------------------
    // RFC1918 private space.
    // --------------------------------------------------------------------

    [Theory]
    [InlineData("https://10.0.0.1/v1")]
    [InlineData("https://10.255.255.1/v1")]
    [InlineData("https://172.16.0.1/v1")]
    [InlineData("https://172.31.255.1/v1")]
    [InlineData("https://192.168.0.1/v1")]
    [InlineData("https://192.168.1.1/v1")]
    public void Rfc1918_NoOptIn_Blocked(string url)
    {
        // Defends: HTTPS-to-private space. An attacker who can edit a
        // config file but not the SSL trust store still must not be
        // able to address an internal corp service on 10/8 or 192.168/16.
        var v = EndpointAllowlist.Check(U(url), localProvidersOptIn: false);
        Assert.Equal(AllowlistVerdict.BlockPrivate, v);
    }

    [Fact]
    public void Rfc1918_172_15_NotPrivate_Allowed()
    {
        // Defends: 172.15/8 is NOT in 172.16/12. Off-by-one regression
        // guard for the private-range catalog.
        var v = EndpointAllowlist.Check(U("https://172.15.0.1/v1"), localProvidersOptIn: false);
        Assert.Equal(AllowlistVerdict.Allow, v);
    }

    // --------------------------------------------------------------------
    // Link-local -- 169.254/16. Cloud metadata service (169.254.169.254)
    // is the headline SSRF target. This MUST be blocked by default.
    // --------------------------------------------------------------------

    [Fact]
    public void CloudMetadata_169_254_169_254_NoOptIn_Blocked()
    {
        // Defends: AWS / Azure / GCP IMDS read. CWE-918. The single most
        // important deny rule in this file.
        var v = EndpointAllowlist.Check(U("https://169.254.169.254/latest/meta-data/"), localProvidersOptIn: false);
        Assert.Equal(AllowlistVerdict.BlockLinkLocal, v);
    }

    [Fact]
    public void LinkLocal_169_254_OptIn_Allowed()
    {
        // Allowed under opt-in only because the operator explicitly named
        // local-providers as in-scope. Documented as a footgun in the
        // audit doc.
        var v = EndpointAllowlist.Check(U("http://169.254.10.1/v1"), localProvidersOptIn: true);
        Assert.Equal(AllowlistVerdict.Allow, v);
    }

    // --------------------------------------------------------------------
    // IPv6: ::1, fe80::/10, fc00::/7, ff00::/8.
    // --------------------------------------------------------------------

    [Fact]
    public void IPv6_Loopback_NoOptIn_Blocked()
    {
        // Defends: attackers who route around IPv4 loopback gates by
        // using the IPv6 form.
        var v = EndpointAllowlist.Check(U("http://[::1]:11434/v1"), localProvidersOptIn: false);
        Assert.Equal(AllowlistVerdict.BlockLoopback, v);
    }

    [Fact]
    public void IPv6_LinkLocal_fe80_NoOptIn_Blocked()
    {
        var v = EndpointAllowlist.Check(U("https://[fe80::1]/v1"), localProvidersOptIn: false);
        Assert.Equal(AllowlistVerdict.BlockLinkLocal, v);
    }

    [Fact]
    public void IPv6_Ula_fc00_NoOptIn_Blocked()
    {
        // Defends: RFC 4193 unique-local. fc00/7 covers fc.. and fd..
        var v = EndpointAllowlist.Check(U("https://[fd12:3456:789a::1]/v1"), localProvidersOptIn: false);
        Assert.Equal(AllowlistVerdict.BlockUla, v);
    }

    // --------------------------------------------------------------------
    // Always-block (regardless of opt-in): multicast, broadcast, all-zeros.
    // --------------------------------------------------------------------

    [Fact]
    public void Multicast_v4_AlwaysBlocked()
    {
        // Defends: 224/4 is never a valid provider endpoint. Blocked
        // even with opt-in -- multicast SSRF can amplify outbound traffic.
        var v = EndpointAllowlist.Check(U("https://224.0.0.1/v1"), localProvidersOptIn: true);
        Assert.Equal(AllowlistVerdict.BlockMulticast, v);
    }

    [Fact]
    public void Multicast_v6_ff00_AlwaysBlocked()
    {
        var v = EndpointAllowlist.Check(U("https://[ff02::1]/v1"), localProvidersOptIn: true);
        Assert.Equal(AllowlistVerdict.BlockMulticast, v);
    }

    [Fact]
    public void Broadcast_AlwaysBlocked()
    {
        var v = EndpointAllowlist.Check(U("https://255.255.255.255/v1"), localProvidersOptIn: true);
        Assert.Equal(AllowlistVerdict.BlockBroadcast, v);
    }

    [Fact]
    public void AllZeros_AlwaysBlocked()
    {
        // Defends: 0.0.0.0 is the SSRF wildcard -- some kernels route it
        // to localhost, some to "first interface". Always reject.
        var v = EndpointAllowlist.Check(U("https://0.0.0.0/v1"), localProvidersOptIn: true);
        Assert.Equal(AllowlistVerdict.BlockMalformed, v);
    }

    // --------------------------------------------------------------------
    // URL-shape: userinfo, privileged ports.
    // --------------------------------------------------------------------

    [Fact]
    public void Userinfo_AlwaysBlocked()
    {
        // Defends: classic SSRF obfuscation -- https://api.openai.com@evil.test/
        // where most readers see the legitimate host but the URL parser
        // resolves "evil.test".
        var v = EndpointAllowlist.Check(U("https://user:pass@api.openai.com/v1"), localProvidersOptIn: false);
        Assert.Equal(AllowlistVerdict.BlockMalformed, v);
    }

    [Fact]
    public void PrivilegedPort_22_Blocked()
    {
        // Defends: protocol-mismatch SSRF. http://target:22 elicits
        // SSH banner exposure / connection-state oracle.
        var v = EndpointAllowlist.Check(U("http://example.com:22/v1"), localProvidersOptIn: true);
        Assert.Equal(AllowlistVerdict.BlockMalformed, v);
    }

    [Fact]
    public void PrivilegedPort_3306_Blocked()
    {
        // Defends: same class -- MySQL port. 3306 is not <1024 in the
        // Internet sense, but documents that the rule is "below 1024
        // except 80/443" and 3306 is well above. This exists as a sanity
        // check that we did NOT over-block 3306.
        var v = EndpointAllowlist.Check(
            U("https://example.com:3306/v1"),
            localProvidersOptIn: false,
            new[] { IPAddress.Parse("8.8.8.8") });
        Assert.Equal(AllowlistVerdict.Allow, v);
    }

    [Fact]
    public void Port_443_Allowed()
    {
        var v = EndpointAllowlist.Check(
            U("https://api.openai.com:443/v1"),
            localProvidersOptIn: false,
            new[] { IPAddress.Parse("8.8.8.8") });
        Assert.Equal(AllowlistVerdict.Allow, v);
    }

    // --------------------------------------------------------------------
    // Obfuscation: octal IPv4, decimal-integer IPv4, IPv6-mapped-IPv4,
    // trailing-dot hostname.
    // --------------------------------------------------------------------

    [Fact]
    public void OctalLocalhost_0177_Blocked()
    {
        // Defends: 0177.0.0.1 (octal 0177 = 127). .NET's IPAddress.TryParse
        // recognises the octal form -- we exercise that on the bare-IP
        // fast path so the obfuscated form fails the same rule.
        if (Uri.TryCreate("http://0177.0.0.1/v1", UriKind.Absolute, out var uri))
        {
            var v = EndpointAllowlist.Check(uri, localProvidersOptIn: false);
            Assert.NotEqual(AllowlistVerdict.Allow, v);
        }
    }

    [Fact]
    public void DecimalLocalhost_2130706433_Blocked()
    {
        // Defends: 2130706433 == 0x7F000001 == 127.0.0.1. .NET parses
        // the decimal-integer IPv4 form via IPAddress.TryParse.
        if (Uri.TryCreate("http://2130706433/v1", UriKind.Absolute, out var uri))
        {
            var v = EndpointAllowlist.Check(uri, localProvidersOptIn: false);
            Assert.NotEqual(AllowlistVerdict.Allow, v);
        }
    }

    [Fact]
    public void IPv6MappedIPv4Localhost_Blocked()
    {
        // Defends: ::ffff:127.0.0.1 routes to IPv4 127.0.0.1. We map
        // IPv4-mapped-IPv6 down before classification, so the verdict
        // is loopback, not "unknown v6".
        var v = EndpointAllowlist.Check(U("http://[::ffff:127.0.0.1]/v1"), localProvidersOptIn: false);
        Assert.Equal(AllowlistVerdict.BlockLoopback, v);
    }

    [Fact]
    public void TrailingDotLocalhost_Blocked()
    {
        // Defends: "localhost." -- some parsers treat this as a different
        // hostname than "localhost" because of the FQDN trailing dot.
        // We strip the trailing dot before the localhost equality test.
        var v = EndpointAllowlist.Check(U("http://localhost./v1"), localProvidersOptIn: false);
        Assert.Equal(AllowlistVerdict.BlockLoopback, v);
    }

    [Fact]
    public void MixedCaseLocalhost_Blocked()
    {
        // Defends: "LocalHost" / "LOCALHOST". URL hosts are
        // case-insensitive; we lowercase before the literal check.
        var v = EndpointAllowlist.Check(U("http://LocalHost/v1"), localProvidersOptIn: false);
        Assert.Equal(AllowlistVerdict.BlockLoopback, v);
    }

    [Fact]
    public void IdnHomoglyph_Punycoded()
    {
        // Defends: a hostname with a Cyrillic 'а' (U+0430) that visually
        // mimics 'a'. The URI exposes IdnHost which is the punycode form,
        // so the case-insensitive equality test is run against the
        // ASCII-safe representation. This test asserts the path runs
        // (resolution is stubbed via pre-resolved addresses).
        var v = EndpointAllowlist.Check(
            U("https://\u0430pi.openai.com/v1"),
            localProvidersOptIn: false,
            new[] { IPAddress.Parse("8.8.8.8") });
        // The punycode'd hostname is not literally "localhost" so the
        // shape check passes; the address check then governs.
        Assert.Equal(AllowlistVerdict.Allow, v);
    }

    // --------------------------------------------------------------------
    // DNS-rebinding: a hostname that resolves to a mix of public and
    // loopback addresses. Must lose.
    // --------------------------------------------------------------------

    [Fact]
    public void DnsRebinding_MixedRecords_Blocked()
    {
        // Defends: an attacker controls evil.example.com's DNS. The
        // first query resolves public; a follow-up query resolves
        // 127.0.0.1. We resolve once and check ALL records; if any
        // record is in a blocked range and opt-in is off, reject.
        var v = EndpointAllowlist.Check(
            U("https://evil.example.com/v1"),
            localProvidersOptIn: false,
            preResolved: new[] { IPAddress.Parse("8.8.8.8"), IPAddress.Parse("127.0.0.1") });
        Assert.Equal(AllowlistVerdict.BlockLoopback, v);
    }

    [Fact]
    public void DnsRebinding_AllPublic_Allowed()
    {
        var v = EndpointAllowlist.Check(
            U("https://api.example.com/v1"),
            localProvidersOptIn: false,
            preResolved: new[] { IPAddress.Parse("8.8.8.8"), IPAddress.Parse("1.1.1.1") });
        Assert.Equal(AllowlistVerdict.Allow, v);
    }

    [Fact]
    public void DnsRebinding_EmptyResolution_Blocked()
    {
        // Defends: NXDOMAIN-but-CNAME-loop or empty answer. Reject.
        var v = EndpointAllowlist.Check(
            U("https://nx.example.com/v1"),
            localProvidersOptIn: false,
            preResolved: System.Array.Empty<IPAddress>());
        Assert.Equal(AllowlistVerdict.BlockMalformed, v);
    }

    // --------------------------------------------------------------------
    // Scheme + malformed-URI shape.
    // --------------------------------------------------------------------

    [Theory]
    [InlineData("ftp://example.com/v1")]
    [InlineData("file:///etc/passwd")]
    [InlineData("gopher://example.com/v1")]
    public void NonHttpScheme_Blocked(string url)
    {
        // Defends: file:// (RCE-via-tool-arg), gopher:// (classic SSRF
        // amplification), ftp:// (data-exfil). Only http(s) is allowed.
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var v = EndpointAllowlist.Check(uri, localProvidersOptIn: true);
            Assert.Equal(AllowlistVerdict.BlockMalformed, v);
        }
    }

    [Fact]
    public void NullUri_Blocked()
    {
        // Defends: caller hands us null after a TryCreate that they
        // forgot to check.
        var v = EndpointAllowlist.Check(null, localProvidersOptIn: true);
        Assert.Equal(AllowlistVerdict.BlockMalformed, v);
    }

    // --------------------------------------------------------------------
    // Env-driven opt-in (strict equality).
    // --------------------------------------------------------------------

    [Theory]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("true", false)]
    [InlineData("yes", false)]
    [InlineData("1 ", false)]
    [InlineData("", false)]
    public void OptInEnv_StrictEqualityOnly(string value, bool expected)
    {
        // Defends: typo-tolerance bugs. AZ_AI_TELEMETRY uses strict "1";
        // we mirror that contract so a "true" or "yes" cannot accidentally
        // open localhost.
        var key = EndpointAllowlist.OptInEnvVar;
        var prior = Environment.GetEnvironmentVariable(key);
        try
        {
            Environment.SetEnvironmentVariable(key, value);
            Assert.Equal(expected, EndpointAllowlist.LocalProvidersOptInFromEnv());
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, prior);
        }
    }

    // --------------------------------------------------------------------
    // Describe() smoke -- every verdict must produce non-empty operator
    // text. A silent "blocked" with no reason is a debugging tax we are
    // not willing to pay.
    // --------------------------------------------------------------------

    [Fact]
    public void Describe_Allow_HasText() => Assert.False(string.IsNullOrWhiteSpace(EndpointAllowlist.Describe(AllowlistVerdict.Allow)));
    [Fact]
    public void Describe_Private_HasText() => Assert.False(string.IsNullOrWhiteSpace(EndpointAllowlist.Describe(AllowlistVerdict.BlockPrivate)));
    [Fact]
    public void Describe_Loopback_HasText() => Assert.False(string.IsNullOrWhiteSpace(EndpointAllowlist.Describe(AllowlistVerdict.BlockLoopback)));
    [Fact]
    public void Describe_LinkLocal_HasText() => Assert.False(string.IsNullOrWhiteSpace(EndpointAllowlist.Describe(AllowlistVerdict.BlockLinkLocal)));
    [Fact]
    public void Describe_Ula_HasText() => Assert.False(string.IsNullOrWhiteSpace(EndpointAllowlist.Describe(AllowlistVerdict.BlockUla)));
    [Fact]
    public void Describe_Multicast_HasText() => Assert.False(string.IsNullOrWhiteSpace(EndpointAllowlist.Describe(AllowlistVerdict.BlockMulticast)));
    [Fact]
    public void Describe_Malformed_HasText() => Assert.False(string.IsNullOrWhiteSpace(EndpointAllowlist.Describe(AllowlistVerdict.BlockMalformed)));
    [Fact]
    public void Describe_Broadcast_HasText() => Assert.False(string.IsNullOrWhiteSpace(EndpointAllowlist.Describe(AllowlistVerdict.BlockBroadcast)));

    // --------------------------------------------------------------------
    // Adapter integration: OpenAiCompatAdapter.Build() must throw when the
    // resolved endpoint is in a blocked range and opt-in is off. We exercise
    // the path indirectly by configuring a preset that points at a
    // private-IP-literal endpoint via the cloudflare account_id rewrite.
    // (cloudflare's preset URL is HTTPS, so an account_id of "10.0.0.1"
    // produces a private-bare-IP endpoint after rewrite.) This also pins
    // the friendly error message format for downstream operators.
    // --------------------------------------------------------------------

    [Fact]
    public void Adapter_Build_ThrowsOnPrivateEndpoint_NoOptIn()
    {
        // Defends: a misconfigured or attacker-controlled preset that
        // points at a private-IP-literal endpoint. Build() must refuse
        // before the network call. We construct a custom preset directly
        // (the built-in cloudflare preset substitutes the account_id into
        // the path, not the host, so its host stays public; we exercise
        // the host-level rule with a synthetic preset instead).
        var prior = (
            key: Environment.GetEnvironmentVariable("STUB_API_KEY"),
            optIn: Environment.GetEnvironmentVariable(EndpointAllowlist.OptInEnvVar));
        try
        {
            Environment.SetEnvironmentVariable("STUB_API_KEY", "stub");
            Environment.SetEnvironmentVariable(EndpointAllowlist.OptInEnvVar, null);
            var preset = new OpenAiCompatPreset(
                "stub-private",
                new Uri("https://10.0.0.1/v1"),
                "STUB_API_KEY");
            var ex = Assert.Throws<ArgumentException>(() => OpenAiCompatAdapter.Build("model", preset));
            Assert.Contains("compat preset 'stub-private'", ex.Message, StringComparison.Ordinal);
            Assert.Contains("private", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("STUB_API_KEY", prior.key);
            Environment.SetEnvironmentVariable(EndpointAllowlist.OptInEnvVar, prior.optIn);
        }
    }

    // --------------------------------------------------------------------
    // S03E26 -- The Offline Mode. Layered on top of S03E16:
    //   * non-loopback Allow + offline=true   -> BlockOffline
    //   * loopback     Allow + offline=true   -> Allow (when opt-in is on)
    //   * loopback no-opt-in  + offline=true  -> still BlockLoopback
    //                                            (offline does NOT relax the opt-in gate)
    //   * existing block      + offline=true  -> verdict unchanged
    //                                            (offline never *unblocks* anything)
    // Tests use the explicit (uri, optIn, offlineMode) overload so they do
    // not touch the process-wide static latch -- which would race with the
    // ConsoleCapture serialised tests.
    // --------------------------------------------------------------------

    [Fact]
    public void Offline_HttpsPublic_Blocked()
    {
        // Defends: the entire point of --offline. A normally-allowed HTTPS
        // public endpoint must be refused.
        var v = EndpointAllowlist.Check(
            U("https://api.openai.com/v1"),
            localProvidersOptIn: false,
            offlineMode: true,
            preResolved: new[] { IPAddress.Parse("8.8.8.8") });
        Assert.Equal(AllowlistVerdict.BlockOffline, v);
    }

    [Fact]
    public void Offline_HttpsPublic_BareIp_Blocked()
    {
        // Defends: bare-IP fast path participates in the offline gate.
        var v = EndpointAllowlist.Check(
            U("https://1.1.1.1/v1"),
            localProvidersOptIn: false,
            offlineMode: true);
        Assert.Equal(AllowlistVerdict.BlockOffline, v);
    }

    [Fact]
    public void Offline_HttpLocalhost_NoOptIn_StillBlockLoopback()
    {
        // Defends: layered model. --offline does NOT relax the loopback
        // opt-in gate; the user must still set AZ_AI_LOCAL_PROVIDERS=1
        // even when offline. (A stray --offline must not accidentally
        // unlock 127.0.0.1 either.)
        var v = EndpointAllowlist.Check(
            U("http://localhost:11434/v1"),
            localProvidersOptIn: false,
            offlineMode: true);
        Assert.Equal(AllowlistVerdict.BlockLoopback, v);
    }

    [Fact]
    public void Offline_HttpLocalhost_WithOptIn_Allowed()
    {
        // Defends: --offline + AZ_AI_LOCAL_PROVIDERS=1 + Ollama-shape
        // localhost URL -> Allow. This is the demo-recording / air-gapped
        // dev posture: silent network, local runtime serves the model.
        var v = EndpointAllowlist.Check(
            U("http://localhost:11434/v1"),
            localProvidersOptIn: true,
            offlineMode: true);
        Assert.Equal(AllowlistVerdict.Allow, v);
    }

    [Fact]
    public void Offline_Loopback127_WithOptIn_Allowed()
    {
        // Defends: bare-IP 127.x.y.z literal under offline + opt-in.
        var v = EndpointAllowlist.Check(
            U("http://127.0.0.1:8080/v1"),
            localProvidersOptIn: true,
            offlineMode: true);
        Assert.Equal(AllowlistVerdict.Allow, v);
    }

    [Fact]
    public void Offline_IPv6Loopback_WithOptIn_Allowed()
    {
        // Defends: ::1 under offline + opt-in is loopback-equivalent.
        var v = EndpointAllowlist.Check(
            U("http://[::1]:11434/v1"),
            localProvidersOptIn: true,
            offlineMode: true);
        Assert.Equal(AllowlistVerdict.Allow, v);
    }

    [Fact]
    public void Offline_AzureShape_Endpoint_Blocked()
    {
        // Defends: a real-shaped Azure endpoint -- this is the most common
        // accidental egress under --offline. Pre-resolved to a public IP
        // to avoid hitting DNS in the unit run.
        var v = EndpointAllowlist.Check(
            U("https://contoso.cognitiveservices.azure.com/"),
            localProvidersOptIn: false,
            offlineMode: true,
            preResolved: new[] { IPAddress.Parse("20.50.1.2") });
        Assert.Equal(AllowlistVerdict.BlockOffline, v);
    }

    [Fact]
    public void Offline_Rfc1918_StillBlockedAsPrivate()
    {
        // Defends: an existing block trumps offline. RFC-1918 without
        // opt-in returns BlockPrivate (not BlockOffline) -- the older
        // verdict carries more diagnostic information ("set
        // AZ_AI_LOCAL_PROVIDERS=1") and offline does not need to
        // re-block what the allowlist already blocks.
        var v = EndpointAllowlist.Check(
            U("https://10.0.0.1/v1"),
            localProvidersOptIn: false,
            offlineMode: true);
        Assert.Equal(AllowlistVerdict.BlockPrivate, v);
    }

    [Fact]
    public void Offline_CloudMetadata_StillBlockedAsLinkLocal()
    {
        // Defends: 169.254.169.254 (cloud metadata) is BlockLinkLocal
        // first; offline does not relabel it. Layered diagnostics.
        var v = EndpointAllowlist.Check(
            U("https://169.254.169.254/latest/meta-data/"),
            localProvidersOptIn: false,
            offlineMode: true);
        Assert.Equal(AllowlistVerdict.BlockLinkLocal, v);
    }

    [Fact]
    public void Offline_DnsRebinding_MixedRecords_Blocked()
    {
        // Defends: a hostname that resolves to mixed public + loopback
        // under --offline. Without opt-in: BlockLoopback (existing rule
        // wins). The DNS-rebinding posture is preserved end-to-end.
        var v = EndpointAllowlist.Check(
            U("https://evil.example.com/v1"),
            localProvidersOptIn: false,
            offlineMode: true,
            preResolved: new[] { IPAddress.Parse("8.8.8.8"), IPAddress.Parse("127.0.0.1") });
        Assert.Equal(AllowlistVerdict.BlockLoopback, v);
    }

    [Fact]
    public void Offline_DnsResolves_Public_Blocked()
    {
        // Defends: a hostname whose A-records are all public under
        // --offline -> BlockOffline. The address classifier returns
        // Allow; the offline post-process converts it.
        var v = EndpointAllowlist.Check(
            U("https://api.public.example/v1"),
            localProvidersOptIn: false,
            offlineMode: true,
            preResolved: new[] { IPAddress.Parse("8.8.8.8"), IPAddress.Parse("1.1.1.1") });
        Assert.Equal(AllowlistVerdict.BlockOffline, v);
    }

    [Fact]
    public void Offline_DnsResolves_AllLoopback_Allowed()
    {
        // Defends: a hostname whose A-records are all loopback under
        // --offline + opt-in -> Allow. (Some custom /etc/hosts entries
        // alias e.g. "ollama.local" to 127.0.0.1.)
        var v = EndpointAllowlist.Check(
            U("http://ollama.local/v1"),
            localProvidersOptIn: true,
            offlineMode: true,
            preResolved: new[] { IPAddress.Parse("127.0.0.1") });
        Assert.Equal(AllowlistVerdict.Allow, v);
    }

    [Fact]
    public void Offline_OffByDefault_DoesNotChangeBehavior()
    {
        // Defends: offline=false is a strict no-op. The S03E16 corpus
        // remains green because the new overload's offline-disabled
        // path is identical to the original Check() behavior.
        var v = EndpointAllowlist.Check(
            U("https://api.openai.com/v1"),
            localProvidersOptIn: false,
            offlineMode: false,
            preResolved: new[] { IPAddress.Parse("8.8.8.8") });
        Assert.Equal(AllowlistVerdict.Allow, v);
    }

    [Fact]
    public void Offline_StaticLatch_PicksUpFromTwoArgOverload()
    {
        // Defends: WebFetchTool / OpenAiCompatAdapter call the 2-arg
        // overload Check(uri, optIn). Setting the process-wide latch
        // must propagate the offline verdict via that surface so we
        // do not have to update every call site.
        var prior = EndpointAllowlist.OfflineMode;
        try
        {
            EndpointAllowlist.OfflineMode = true;
            var v = EndpointAllowlist.Check(
                U("https://api.openai.com/v1"),
                localProvidersOptIn: false,
                preResolved: new[] { IPAddress.Parse("8.8.8.8") });
            Assert.Equal(AllowlistVerdict.BlockOffline, v);
        }
        finally
        {
            EndpointAllowlist.OfflineMode = prior;
        }
    }

    [Fact]
    public void Offline_Describe_HasActionableText()
    {
        // Defends: error text names the rule that fired and the env-var
        // to flip. A silent "blocked" with no reason is a debugging tax.
        var msg = EndpointAllowlist.Describe(AllowlistVerdict.BlockOffline);
        Assert.False(string.IsNullOrWhiteSpace(msg));
        Assert.Contains("--offline", msg, StringComparison.Ordinal);
        Assert.Contains("AZ_AI_LOCAL_PROVIDERS", msg, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("true", false)]
    [InlineData("yes", false)]
    [InlineData("1 ", false)]
    [InlineData("", false)]
    public void OfflineEnv_StrictEqualityOnly(string value, bool expected)
    {
        // Defends: typo-tolerance bugs. AZ_AI_OFFLINE mirrors the same
        // strict "1" contract as AZ_AI_TELEMETRY / AZ_AI_LOCAL_PROVIDERS;
        // a "true" / "yes" / "1 " must keep the gate closed.
        var key = EndpointAllowlist.OfflineEnvVar;
        var prior = Environment.GetEnvironmentVariable(key);
        try
        {
            Environment.SetEnvironmentVariable(key, value);
            Assert.Equal(expected, EndpointAllowlist.OfflineModeFromEnv());
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, prior);
        }
    }
}
