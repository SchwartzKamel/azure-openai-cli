using System.Net;
using AzureOpenAI_CLI.Tools;

namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// Boundary coverage for WebFetchTool's address-based SSRF guard. These tests
/// exercise the classifier directly so they do not depend on DNS or network
/// availability.
/// </summary>
public sealed class WebFetchAddressClassificationTests
{
    public static TheoryData<string, bool> AddressCases => new()
    {
        // IPv4 loopback, RFC 1918, and link-local ranges (including edges).
        { "127.0.0.1", true },
        { "127.255.255.255", true },
        { "126.255.255.255", false },
        { "10.0.0.0", true },
        { "10.255.255.255", true },
        { "11.0.0.0", false },
        { "172.15.255.255", false },
        { "172.16.0.0", true },
        { "172.31.255.255", true },
        { "172.32.0.0", false },
        { "192.167.255.255", false },
        { "192.168.0.0", true },
        { "192.168.255.255", true },
        { "192.169.0.0", false },
        { "169.253.255.255", false },
        { "169.254.0.0", true },
        { "169.254.255.255", true },
        { "169.255.0.0", false },

        // IPv6 loopback, unique-local (RFC 4193 fc00::/7), and link-local
        // (fe80::/10) boundaries.
        { "::1", true },
        { "fc00::", true },
        { "fdff:ffff:ffff:ffff:ffff:ffff:ffff:ffff", true },
        { "fbff:ffff:ffff:ffff:ffff:ffff:ffff:ffff", false },
        { "fe7f:ffff:ffff:ffff:ffff:ffff:ffff:ffff", false },
        { "fe80::", true },
        { "febf:ffff:ffff:ffff:ffff:ffff:ffff:ffff", true },
        { "fec0::", false },

        // IPv4-mapped IPv6 values must receive the same classification as the
        // embedded IPv4 address, not evade the IPv4 checks.
        { "::ffff:10.20.30.40", true },
        { "::ffff:169.254.1.2", true },
        { "::ffff:8.8.8.8", false },

        // Known public ranges remain fetchable.
        { "8.8.8.8", false },
        { "1.1.1.1", false },
        { "2001:4860:4860::8888", false },
    };

    [Theory]
    [MemberData(nameof(AddressCases))]
    public void IsPrivateAddress_ClassifiesBoundaryRanges(string address, bool expected)
    {
        Assert.Equal(expected, WebFetchTool.IsPrivateAddress(IPAddress.Parse(address)));
    }
}
