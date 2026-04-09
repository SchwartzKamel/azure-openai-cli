using System.Diagnostics;
using System.Text.Json;

namespace AzureOpenAI_CLI.Tools;

/// <summary>
/// Read the current system clipboard text content.
/// Cross-platform: xclip/xsel (Linux), pbpaste (macOS), PowerShell (Windows).
/// </summary>
internal sealed class GetClipboardTool : IBuiltInTool
{
    private const int MaxClipboardBytes = 32_768; // 32 KB

    public string Name => "get_clipboard";
    public string Description => "Read the current text content from the system clipboard. Useful when the user refers to 'what I copied' or 'my clipboard'.";
    public BinaryData ParametersSchema => BinaryData.FromString("""
        {
            "type": "object",
            "properties": {},
            "required": []
        }
        """);

    public async Task<string> ExecuteAsync(JsonElement arguments, CancellationToken ct)
    {
        string command;
        string args;

        if (OperatingSystem.IsLinux())
        {
            // Use 'which' to find clipboard tools instead of hardcoded paths
            var xclipPath = FindCommand("xclip");
            command = xclipPath is not null ? "xclip" : "xsel";
            args = command == "xclip" ? "-selection clipboard -o" : "--clipboard --output";
        }
        else if (OperatingSystem.IsMacOS())
        {
            command = "pbpaste";
            args = "";
        }
        else if (OperatingSystem.IsWindows())
        {
            command = "powershell";
            args = "-NoProfile -Command Get-Clipboard";
        }
        else
        {
            return "Error: clipboard access not supported on this platform.";
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(5000);

        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException($"Failed to start {command}");

            var buffer = new char[MaxClipboardBytes];
            int read = await process.StandardOutput.ReadBlockAsync(buffer.AsMemory(0, MaxClipboardBytes), cts.Token);
            await process.WaitForExitAsync(cts.Token);

            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync(cts.Token);
                return $"Error reading clipboard: {stderr.Trim()}";
            }

            var content = new string(buffer, 0, read);
            if (string.IsNullOrEmpty(content))
                return "(clipboard is empty)";

            if (read == MaxClipboardBytes)
                content += "\n... (clipboard content truncated)";

            return content;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return $"Error: '{command}' not found. Install xclip or xsel for clipboard support.";
        }
    }

    /// <summary>
    /// Use 'which' (or 'where' on Windows) to locate a command on the PATH.
    /// Returns the full path if found, null otherwise.
    /// </summary>
    private static string? FindCommand(string commandName)
    {
        try
        {
            var whichCommand = OperatingSystem.IsWindows() ? "where" : "which";
            var psi = new ProcessStartInfo
            {
                FileName = whichCommand,
                Arguments = commandName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process is null) return null;

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(2000);

            return process.ExitCode == 0 && !string.IsNullOrEmpty(output) ? output : null;
        }
        catch
        {
            return null;
        }
    }
}
