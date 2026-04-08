using System.Text.Json;

namespace AzureOpenAI_CLI.Tools;

/// <summary>
/// Get the current date, time, and timezone information.
/// </summary>
internal sealed class GetDateTimeTool : IBuiltInTool
{
    public string Name => "get_datetime";
    public string Description => "Get the current date, time, and timezone. Useful for time-aware responses.";
    public BinaryData ParametersSchema => BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "timezone": { "type": "string", "description": "Optional IANA timezone (e.g. 'America/New_York'). Defaults to local." }
            },
            "required": []
        }
        """);

    public Task<string> ExecuteAsync(JsonElement arguments, CancellationToken ct)
    {
        string? tz = null;
        if (arguments.ValueKind == JsonValueKind.Object &&
            arguments.TryGetProperty("timezone", out var tzProp))
        {
            tz = tzProp.GetString();
        }

        DateTimeOffset now;
        string tzName;

        if (!string.IsNullOrEmpty(tz))
        {
            try
            {
                var tzi = TimeZoneInfo.FindSystemTimeZoneById(tz);
                now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tzi);
                tzName = tzi.DisplayName;
            }
            catch (TimeZoneNotFoundException)
            {
                return Task.FromResult($"Error: unknown timezone '{tz}'");
            }
        }
        else
        {
            now = DateTimeOffset.Now;
            tzName = TimeZoneInfo.Local.DisplayName;
        }

        var result = $"{now:yyyy-MM-dd HH:mm:ss zzz} ({now:dddd}) — {tzName}";
        return Task.FromResult(result);
    }
}
