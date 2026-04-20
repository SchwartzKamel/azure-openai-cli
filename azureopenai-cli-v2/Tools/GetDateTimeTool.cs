using System.ComponentModel;

namespace AzureOpenAI_CLI_V2.Tools;

/// <summary>
/// Get the current date, time, and timezone information.
/// MAF version: uses [Description] attributes for AIFunctionFactory.Create.
/// </summary>
internal static class GetDateTimeTool
{
    [Description("Get the current date, time, and timezone. Useful for time-aware responses.")]
    public static Task<string> GetAsync(
        [Description("Optional IANA timezone (e.g. 'America/New_York'). Defaults to local.")] string? timezone = null,
        CancellationToken ct = default)
    {
        DateTimeOffset now;
        string tzName;

        if (!string.IsNullOrEmpty(timezone))
        {
            try
            {
                var tzi = TimeZoneInfo.FindSystemTimeZoneById(timezone);
                now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tzi);
                tzName = tzi.DisplayName;
            }
            catch (TimeZoneNotFoundException)
            {
                return Task.FromResult($"Error: unknown timezone '{timezone}'");
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
