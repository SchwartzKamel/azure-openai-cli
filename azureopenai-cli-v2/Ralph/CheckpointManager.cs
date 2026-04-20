using System.Globalization;
using System.Text;

namespace AzureOpenAI_CLI_V2.Ralph;

/// <summary>
/// Manages .ralph-log checkpoint file. Format is v1-compatible — users can resume
/// Ralph sessions across v1/v2 boundaries without data loss.
/// </summary>
internal static class CheckpointManager
{
    private const string LogFilePath = ".ralph-log";

    /// <summary>
    /// Appends an iteration record to the .ralph-log file.
    /// Format: ISO-8601 timestamp + iteration number + summary + validation result.
    /// </summary>
    public static void WriteCheckpoint(int iteration, string prompt, int agentExitCode, string agentResponse,
        string? validationCommand, int? validationExitCode, string? validationOutput)
    {
        try
        {
            var entry = new StringBuilder();
            entry.AppendLine($"## Iteration {iteration}");
            entry.AppendLine($"**Timestamp:** {DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}");
            entry.AppendLine($"**Prompt:** {TruncateForLog(prompt, 200)}");
            entry.AppendLine($"**Agent exit:** {agentExitCode}");
            entry.AppendLine($"**Response:** {TruncateForLog(agentResponse, 500)}");

            if (validationCommand != null)
            {
                var status = validationExitCode == 0 ? "PASSED" : $"FAILED (exit {validationExitCode})";
                entry.AppendLine($"**Validation:** {status}");
                if (validationExitCode != 0 && !string.IsNullOrEmpty(validationOutput))
                {
                    entry.AppendLine("```");
                    entry.AppendLine(TruncateForLog(validationOutput, 2000));
                    entry.AppendLine("```");
                }
            }

            entry.AppendLine();

            // Append to log (create if missing)
            File.AppendAllText(LogFilePath, entry.ToString());
        }
        catch
        {
            // Best-effort logging — don't fail the workflow on I/O errors
        }
    }

    /// <summary>
    /// Writes a cancellation or exhaustion final entry to the log.
    /// </summary>
    public static void WriteFinalEntry(string message)
    {
        try
        {
            var entry = new StringBuilder();
            entry.AppendLine($"**Final status:** {message}");
            entry.AppendLine($"**Timestamp:** {DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}");
            entry.AppendLine();

            File.AppendAllText(LogFilePath, entry.ToString());
        }
        catch
        {
            // Best-effort
        }
    }

    /// <summary>
    /// Initializes the log file with a header. Call once at workflow start.
    /// </summary>
    public static void InitializeLog()
    {
        try
        {
            if (!File.Exists(LogFilePath))
            {
                File.WriteAllText(LogFilePath, "# Ralph Loop Log\n\n");
            }
        }
        catch
        {
            // Best-effort
        }
    }

    private static string TruncateForLog(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
            return "(empty)";

        if (text.Length <= maxLength)
            return text;

        return text[..maxLength] + "...";
    }
}
