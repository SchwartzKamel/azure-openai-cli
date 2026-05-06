using System.ComponentModel;
using System.Net;
using System.Reflection;
using AzureOpenAI_CLI.Net;

namespace AzureOpenAI_CLI.Tools;

/// <summary>
/// Fetch the text content of a URL via HTTP GET. HTTPS only.
/// MAF version: uses [Description] attributes for AIFunctionFactory.Create.
/// </summary>
internal static class WebFetchTool
{
    private const int MaxResponseBytes = 131_072; // 128 KB
    private const int TimeoutSeconds = 10;
    private const int MaxRedirects = 3;

    [Description("Fetch the text content of a web URL via HTTP GET. HTTPS only. Returns the response body as text.")]
    public static async Task<string> FetchAsync(
        [Description("The HTTPS URL to fetch")] string url,
        CancellationToken ct = default)
    {
        return await FetchInternalAsync(url, handlerOverride: null, ct);
    }

    /// <summary>
    /// Internal implementation that supports injecting a custom HTTP message handler for testing.
    /// </summary>
    internal static async Task<string> FetchInternalAsync(string url, HttpMessageHandler? handlerOverride, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(url))
            return "Error: parameter 'url' must not be empty.";

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https")
            return "Error: only HTTPS URLs are allowed.";

        // S03E16: SSRF allowlist seam. WebFetchTool is a tool, never a
        // provider connection -- localProvidersOptIn is hard-coded false.
        var verdict = EndpointAllowlist.Check(uri, localProvidersOptIn: false);
        if (verdict != AllowlistVerdict.Allow)
        {
            return $"Error: URL blocked by endpoint allowlist ({EndpointAllowlist.Describe(verdict)}).";
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

        var handler = handlerOverride ?? new HttpClientHandler
        {
            MaxAutomaticRedirections = MaxRedirects,
        };
        using var http = new HttpClient(handler, disposeHandler: handlerOverride == null)
        {
            Timeout = TimeSpan.FromSeconds(TimeoutSeconds)
        };

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var versionString = version is not null ? $"{version.Major}.{version.Minor}" : "0.0";
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"AzureOpenAI-CLI-V2/{versionString}");

        var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        response.EnsureSuccessStatusCode();

        // Post-redirect SSRF protection: validate the final URL after any redirects
        var redirectError = await ValidateRedirectedUriAsync(response.RequestMessage?.RequestUri, ct);
        if (redirectError is not null)
            return redirectError;

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
    /// Validate the final URI after HTTP redirects. Returns null if safe, or an error message if blocked.
    /// </summary>
    internal static async Task<string?> ValidateRedirectedUriAsync(Uri? finalUri, CancellationToken ct)
    {
        if (finalUri is null)
            return "Error: could not determine final URL after redirects.";

        if (finalUri.Scheme != "https")
            return "Error: redirect to non-HTTPS URL is blocked for security.";

        // S03E16: post-redirect URI passes through the same allowlist.
        var verdict = EndpointAllowlist.Check(finalUri, localProvidersOptIn: false);
        if (verdict != AllowlistVerdict.Allow)
        {
            return $"Error: redirect blocked by endpoint allowlist ({EndpointAllowlist.Describe(verdict)}).";
        }

        await Task.CompletedTask;
        return null;
    }

    /// <summary>
    /// Back-compat shim for callers / tests that pre-date S03E16. Delegates
    /// to <see cref="EndpointAllowlist"/>'s address classifier so the legacy
    /// surface continues to answer "is this address blocked for the tool
    /// surface" without a second range catalog drifting alongside the new
    /// seam. Tool surface = opt-in always false.
    /// </summary>
    internal static bool IsPrivateAddress(IPAddress address)
    {
        // Build a synthetic HTTPS URI so the address-classifier path can
        // run through the public Check overload. The URI shape is irrelevant
        // here -- the literal IP host triggers the bare-IP fast path.
        if (address is null) return true;
        var host = address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            ? "[" + address.ToString() + "]"
            : address.ToString();
        if (!Uri.TryCreate("https://" + host + "/", UriKind.Absolute, out var uri))
            return true;
        return EndpointAllowlist.Check(uri, localProvidersOptIn: false) != AllowlistVerdict.Allow;
    }
}
