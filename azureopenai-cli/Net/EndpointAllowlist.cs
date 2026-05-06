using System.Net;
using System.Net.Sockets;

namespace AzureOpenAI_CLI.Net;

// S03E16 -- The Allowlist. SSRF posture for the compat dispatch path.
//
// Single seam every outbound provider URL passes through before a network
// call. WebFetchTool delegates here too (replacing its earlier ad-hoc check)
// so the same private-range catalog gates both the agent tool surface and
// the provider-build surface. Localhost is opt-in only via
// AZ_AI_LOCAL_PROVIDERS=1 -- same convention as AZ_AI_TELEMETRY (strict
// equality, no truthy-string coercion).
//
// Threat model (short form): an attacker who can influence a base-URL
// string (preset table corruption, malicious env, malicious config, IDN
// homoglyph in a docs example, or a DNS-rebinding hostname) MUST NOT be
// able to coerce the binary into hitting RFC-1918 / loopback / link-local
// space -- in particular 169.254.169.254 (cloud metadata) -- without an
// explicit opt-in. The opt-in unlocks legitimate localhost runtimes
// (Ollama upcoming in S03E14, llama-server in S03E17) without opening the
// rest of private space at the same time.
//
// AOT: pure System.Net + System.Net.Sockets. No reflection, no JSON.

/// <summary>
/// Verdict from <see cref="EndpointAllowlist.Check"/>. <see cref="Allow"/>
/// is the only success state; every other value names the specific block
/// reason so callers can produce actionable error text without re-deriving
/// the rule that fired.
/// </summary>
internal enum AllowlistVerdict
{
    Allow,
    BlockPrivate,
    BlockLoopback,
    BlockLinkLocal,
    BlockUla,
    BlockMulticast,
    BlockMalformed,
    BlockBroadcast,
}

/// <summary>
/// SSRF endpoint allowlist. Single static entry point per dispatch:
/// <see cref="Check"/> for the simple env-driven case, and the
/// pre-resolved overload for tests that need to inject a stub DNS
/// resolver (DNS-rebinding regression coverage).
/// </summary>
internal static class EndpointAllowlist
{
    /// <summary>Env var that opts in to non-public endpoints (loopback /
    /// RFC1918 / link-local / ULA). Strict equality with "1" -- same
    /// convention as AZ_AI_TELEMETRY. Any other value (including "true",
    /// "yes", "1 " with trailing space, or unset) leaves the opt-in OFF.</summary>
    internal const string OptInEnvVar = "AZ_AI_LOCAL_PROVIDERS";

    /// <summary>DNS resolution timeout. Cap is 3 seconds; well under the
    /// HttpClient default and short enough that a hostile/slow resolver
    /// cannot park a build call.</summary>
    private const int DnsTimeoutSeconds = 3;

    /// <summary>True when the environment opts in to local providers via
    /// strict-equality "1".</summary>
    internal static bool LocalProvidersOptInFromEnv()
    {
        var raw = Environment.GetEnvironmentVariable(OptInEnvVar);
        return string.Equals(raw, "1", StringComparison.Ordinal);
    }

    /// <summary>
    /// Inspect <paramref name="uri"/> and return a verdict. Resolves the
    /// hostname via DNS and verifies every returned address against the
    /// blocked-range catalog (DNS-rebinding defense). Blocking I/O bounded
    /// by <see cref="DnsTimeoutSeconds"/>.
    /// </summary>
    internal static AllowlistVerdict Check(Uri? uri, bool localProvidersOptIn)
    {
        var (verdict, host) = CheckUriShape(uri, localProvidersOptIn);
        if (verdict != AllowlistVerdict.Allow || host is null)
        {
            return verdict;
        }

        IPAddress[] addresses;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(DnsTimeoutSeconds));
            addresses = Dns.GetHostAddressesAsync(host, cts.Token).GetAwaiter().GetResult();
        }
        catch
        {
            return AllowlistVerdict.BlockMalformed;
        }

        return CheckAddresses(addresses, localProvidersOptIn);
    }

    /// <summary>
    /// Test/injection seam. Skips DNS and uses the supplied addresses
    /// directly. Used by the DNS-rebinding adversarial test to assert the
    /// allowlist rejects a hostname whose A-records mix public and
    /// loopback addresses.
    /// </summary>
    internal static AllowlistVerdict Check(Uri? uri, bool localProvidersOptIn, IPAddress[] preResolved)
    {
        var (verdict, _) = CheckUriShape(uri, localProvidersOptIn);
        if (verdict != AllowlistVerdict.Allow)
        {
            return verdict;
        }
        return CheckAddresses(preResolved, localProvidersOptIn);
    }

    /// <summary>
    /// URL-shape checks. Returns Allow (and the punycode host to resolve)
    /// when the shape is acceptable; otherwise the failing verdict.
    /// </summary>
    private static (AllowlistVerdict verdict, string? host) CheckUriShape(Uri? uri, bool localProvidersOptIn)
    {
        if (uri is null || !uri.IsAbsoluteUri)
        {
            return (AllowlistVerdict.BlockMalformed, null);
        }

        // Userinfo (user:pass@host) is never legitimate for a provider
        // endpoint and is a classic SSRF obfuscation vector. Block always.
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return (AllowlistVerdict.BlockMalformed, null);
        }

        // Scheme: HTTPS for public, HTTP only when loopback AND opt-in is on.
        // (HTTPS to private space is still allowed only when opt-in is on --
        // that gate is enforced by the address check below.)
        var scheme = uri.Scheme;
        var isHttps = string.Equals(scheme, "https", StringComparison.Ordinal);
        var isHttp = string.Equals(scheme, "http", StringComparison.Ordinal);
        if (!isHttps && !isHttp)
        {
            return (AllowlistVerdict.BlockMalformed, null);
        }

        // Port shape: only 80, 443, or non-privileged (>=1024) ports.
        // Privileged ports below 1024 except 80/443 are protocol-mismatch
        // attempts (e.g. http://target:22) and are always rejected.
        var port = uri.Port;
        if (port == 0)
        {
            return (AllowlistVerdict.BlockMalformed, null);
        }
        if (port < 1024 && port != 80 && port != 443)
        {
            return (AllowlistVerdict.BlockMalformed, null);
        }

        // HTTP requires opt-in. Without opt-in, only HTTPS public is allowed.
        // (The address check still has to run; an HTTPS bare-IP into private
        // space is rejected unless opt-in is on.)
        if (isHttp && !localProvidersOptIn)
        {
            return (AllowlistVerdict.BlockLoopback, null);
        }

        // Normalize host: punycode (defeats Unicode/IDN homoglyph), lower
        // (defeats mixed case), strip trailing dot (defeats FQDN-with-dot
        // bypass).
        var host = uri.IdnHost;
        if (string.IsNullOrEmpty(host))
        {
            return (AllowlistVerdict.BlockMalformed, null);
        }
        host = host.ToLowerInvariant();
        if (host.EndsWith(".", StringComparison.Ordinal))
        {
            host = host.Substring(0, host.Length - 1);
        }
        if (host.Length == 0)
        {
            return (AllowlistVerdict.BlockMalformed, null);
        }

        // Bare-IP literal in the URI -- check immediately, without DNS.
        // Defeats octal / decimal / IPv6-mapped-IPv4 obfuscation because
        // IPAddress.TryParse normalizes those forms.
        if (IPAddress.TryParse(host, out var literal))
        {
            var addrVerdict = ClassifyAddress(literal, localProvidersOptIn);
            if (addrVerdict != AllowlistVerdict.Allow)
            {
                return (addrVerdict, null);
            }
            // Public bare IP -- allow without DNS lookup.
            return (AllowlistVerdict.Allow, null);
        }

        // The "localhost" hostname is special -- some resolvers return ::1,
        // some 127.0.0.1. Treat as loopback by name.
        if (string.Equals(host, "localhost", StringComparison.Ordinal))
        {
            return localProvidersOptIn
                ? (AllowlistVerdict.Allow, null)
                : (AllowlistVerdict.BlockLoopback, null);
        }

        return (AllowlistVerdict.Allow, host);
    }

    /// <summary>
    /// Run every supplied address through the classifier. If any address
    /// is in a blocked range and opt-in is off, the whole hostname is
    /// rejected -- this is the DNS-rebinding defense (a hostname that
    /// resolves to a mix of public and loopback addresses must lose).
    /// </summary>
    private static AllowlistVerdict CheckAddresses(IPAddress[] addresses, bool localProvidersOptIn)
    {
        if (addresses is null || addresses.Length == 0)
        {
            return AllowlistVerdict.BlockMalformed;
        }
        foreach (var addr in addresses)
        {
            var v = ClassifyAddress(addr, localProvidersOptIn);
            if (v != AllowlistVerdict.Allow)
            {
                return v;
            }
        }
        return AllowlistVerdict.Allow;
    }

    /// <summary>
    /// Classify a single resolved address. Always-blocked categories
    /// (multicast, broadcast, the all-zeros sentinel) are rejected
    /// regardless of opt-in -- those are never legitimate provider
    /// endpoints. Loopback / RFC1918 / link-local / ULA are rejected
    /// only when opt-in is OFF.
    /// </summary>
    private static AllowlistVerdict ClassifyAddress(IPAddress address, bool localProvidersOptIn)
    {
        if (address is null)
        {
            return AllowlistVerdict.BlockMalformed;
        }

        // Map IPv4-mapped-IPv6 (::ffff:a.b.c.d) down to IPv4 so the v4
        // classifier sees the real octets.
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        // Categorical always-block first.
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();

            // 0.0.0.0 -- "this host on this network", SSRF wildcard.
            if (b[0] == 0 && b[1] == 0 && b[2] == 0 && b[3] == 0)
            {
                return AllowlistVerdict.BlockMalformed;
            }
            // 255.255.255.255 limited broadcast.
            if (b[0] == 255 && b[1] == 255 && b[2] == 255 && b[3] == 255)
            {
                return AllowlistVerdict.BlockBroadcast;
            }
            // 224.0.0.0/4 multicast.
            if (b[0] >= 224 && b[0] <= 239)
            {
                return AllowlistVerdict.BlockMulticast;
            }

            // Conditional (gated by opt-in).
            // 127.0.0.0/8 loopback.
            if (b[0] == 127)
            {
                return localProvidersOptIn ? AllowlistVerdict.Allow : AllowlistVerdict.BlockLoopback;
            }
            // 169.254.0.0/16 link-local (incl. 169.254.169.254 cloud metadata).
            if (b[0] == 169 && b[1] == 254)
            {
                return localProvidersOptIn ? AllowlistVerdict.Allow : AllowlistVerdict.BlockLinkLocal;
            }
            // 10.0.0.0/8.
            if (b[0] == 10)
            {
                return localProvidersOptIn ? AllowlistVerdict.Allow : AllowlistVerdict.BlockPrivate;
            }
            // 172.16.0.0/12.
            if (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            {
                return localProvidersOptIn ? AllowlistVerdict.Allow : AllowlistVerdict.BlockPrivate;
            }
            // 192.168.0.0/16.
            if (b[0] == 192 && b[1] == 168)
            {
                return localProvidersOptIn ? AllowlistVerdict.Allow : AllowlistVerdict.BlockPrivate;
            }

            return AllowlistVerdict.Allow;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var b = address.GetAddressBytes();

            // ff00::/8 multicast (always block).
            if (b[0] == 0xff)
            {
                return AllowlistVerdict.BlockMulticast;
            }

            // ::1 loopback. IsLoopback handles this on the IPAddress level
            // but we still need the verdict mapping.
            if (IPAddress.IsLoopback(address))
            {
                return localProvidersOptIn ? AllowlistVerdict.Allow : AllowlistVerdict.BlockLoopback;
            }

            // :: (all-zeros v6) -- the v6 equivalent of 0.0.0.0.
            var allZero = true;
            for (int i = 0; i < b.Length; i++)
            {
                if (b[i] != 0) { allZero = false; break; }
            }
            if (allZero)
            {
                return AllowlistVerdict.BlockMalformed;
            }

            // fe80::/10 link-local.
            if (b[0] == 0xfe && (b[1] & 0xc0) == 0x80)
            {
                return localProvidersOptIn ? AllowlistVerdict.Allow : AllowlistVerdict.BlockLinkLocal;
            }

            // fc00::/7 ULA.
            if ((b[0] & 0xfe) == 0xfc)
            {
                return localProvidersOptIn ? AllowlistVerdict.Allow : AllowlistVerdict.BlockUla;
            }

            return AllowlistVerdict.Allow;
        }

        // Unknown address family.
        return AllowlistVerdict.BlockMalformed;
    }

    /// <summary>
    /// One-line operator-friendly explanation for a non-Allow verdict.
    /// Used by callers (OpenAiCompatAdapter, WebFetchTool) to produce
    /// error messages that name the rule that fired and the env-var to
    /// flip if the user is intentionally pointing at a local runtime.
    /// </summary>
    internal static string Describe(AllowlistVerdict verdict)
    {
        return verdict switch
        {
            AllowlistVerdict.Allow => "allowed",
            AllowlistVerdict.BlockLoopback => "private loopback address (set AZ_AI_LOCAL_PROVIDERS=1 to allow local providers)",
            AllowlistVerdict.BlockPrivate => "private RFC-1918 address (set AZ_AI_LOCAL_PROVIDERS=1 to allow local providers)",
            AllowlistVerdict.BlockLinkLocal => "link-local address (169.254/16 or fe80::/10) -- cloud metadata service is in this range; set AZ_AI_LOCAL_PROVIDERS=1 only if intentional",
            AllowlistVerdict.BlockUla => "IPv6 unique-local address (fc00::/7); set AZ_AI_LOCAL_PROVIDERS=1 to allow local providers",
            AllowlistVerdict.BlockMulticast => "multicast address (never a valid provider endpoint)",
            AllowlistVerdict.BlockBroadcast => "broadcast address (never a valid provider endpoint)",
            AllowlistVerdict.BlockMalformed => "malformed or unsupported URL (scheme, port, userinfo, or unresolvable host)",
            _ => "blocked",
        };
    }
}
