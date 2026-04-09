using System.Net;
using System.Reflection;
using System.Text.Json;

namespace AzureOpenAI_CLI.Tools;

/// <summary>
/// Fetch the text content of a URL via HTTP GET. HTTPS only.
/// </summary>
internal sealed class WebFetchTool : IBuiltInTool
{
    private const int MaxResponseBytes = 131_072; // 128 KB
    private const int TimeoutSeconds = 10;
    private const int MaxRedirects = 3;

    public string Name => "web_fetch";
    public string Description => "Fetch the text content of a web URL via HTTP GET. HTTPS only. Returns the response body as text.";
    public BinaryData ParametersSchema => BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "url": { "type": "string", "description": "The HTTPS URL to fetch" }
            },
            "required": ["url"]
        }
        """);

    public async Task<string> ExecuteAsync(JsonElement arguments, CancellationToken ct)
    {
        var url = arguments.GetProperty("url").GetString()
            ?? throw new ArgumentException("Missing 'url' parameter");

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https")
            return "Error: only HTTPS URLs are allowed.";

        // DNS rebinding / SSRF protection: resolve hostname and block private IPs
        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(uri.Host, ct);
        }
        catch (Exception ex)
        {
            return $"Error: failed to resolve hostname '{uri.Host}': {ex.Message}";
        }

        if (addresses.Length == 0)
            return $"Error: hostname '{uri.Host}' did not resolve to any address.";

        foreach (var addr in addresses)
        {
            if (IsPrivateAddress(addr))
                return $"Error: access to private/loopback addresses is blocked for security.";
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

        var handler = new HttpClientHandler
        {
            MaxAutomaticRedirections = MaxRedirects,
        };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(TimeoutSeconds) };

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var versionString = version is not null ? $"{version.Major}.{version.Minor}" : "0.0";
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"AzureOpenAI-CLI/{versionString}");

        var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        var buffer = new byte[MaxResponseBytes];
        int totalRead = 0;
        int read;
        while (totalRead < MaxResponseBytes &&
               (read = await stream.ReadAsync(buffer.AsMemory(totalRead, MaxResponseBytes - totalRead), cts.Token)) > 0)
        {
            totalRead += read;
        }

        var text = System.Text.Encoding.UTF8.GetString(buffer, 0, totalRead);
        if (totalRead == MaxResponseBytes)
            text += "\n... (response truncated)";

        return text;
    }

    /// <summary>
    /// Returns true if the address is loopback, link-local, or in a private RFC-1918 / RFC-4193 range.
    /// </summary>
    internal static bool IsPrivateAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
            return true;

        // Map IPv4-mapped IPv6 addresses to their IPv4 equivalent
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        var bytes = address.GetAddressBytes();

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            // 10.0.0.0/8
            if (bytes[0] == 10)
                return true;
            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                return true;
            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168)
                return true;
            // 127.0.0.0/8 (additional loopback check)
            if (bytes[0] == 127)
                return true;
            // 169.254.0.0/16 (link-local)
            if (bytes[0] == 169 && bytes[1] == 254)
                return true;
        }
        else if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            // ::1 is already handled by IPAddress.IsLoopback above
            // fd00::/8 (unique local addresses)
            if (bytes[0] == 0xfd)
                return true;
            // fe80::/10 (link-local)
            if (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80)
                return true;
        }

        return false;
    }
}
