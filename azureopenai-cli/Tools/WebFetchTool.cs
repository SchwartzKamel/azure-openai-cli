using System.Text.Json;

namespace AzureOpenAI_CLI.Tools;

/// <summary>
/// Fetch the text content of a URL via HTTP GET. HTTPS only.
/// </summary>
internal sealed class WebFetchTool : IBuiltInTool
{
    private const int MaxResponseBytes = 131_072; // 128 KB
    private const int TimeoutSeconds = 10;

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

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(TimeoutSeconds) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("AzureOpenAI-CLI/1.1");

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
}
