using System.Diagnostics;
using System.Text.Json;

namespace AzureOpenAI_CLI.Tools;

/// <summary>
/// Execute a shell command and return stdout. Sandboxed with timeout and output cap.
/// </summary>
internal sealed class ShellExecTool : IBuiltInTool
{
    private const int MaxOutputBytes = 65_536; // 64 KB
    private const int DefaultTimeoutMs = 10_000; // 10s

    private static readonly HashSet<string> BlockedCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "rm", "rmdir", "mkfs", "dd", "shutdown", "reboot", "halt", "poweroff",
        "kill", "killall", "pkill", "format", "del", "fdisk", "passwd",
        "sudo", "su", "crontab", "vi", "vim", "nano",
        "nc", "ncat", "netcat", "wget",
    };

    public string Name => "shell_exec";
    public string Description => "Execute a shell command and return its stdout. Use for running git, ls, cat, grep, curl, etc. Commands that delete or modify system state are blocked.";
    public BinaryData ParametersSchema => BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "command": { "type": "string", "description": "The shell command to execute" }
            },
            "required": ["command"]
        }
        """);

    public async Task<string> ExecuteAsync(JsonElement arguments, CancellationToken ct)
    {
        var command = arguments.GetProperty("command").GetString()
            ?? throw new ArgumentException("Missing 'command' parameter");

        // Block dangerous commands
        var firstToken = command.TrimStart().Split(' ', 2)[0].Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "";
        if (BlockedCommands.Contains(firstToken))
            return $"Error: command '{firstToken}' is blocked for safety.";

        // Block pipe chains to dangerous commands
        foreach (var segment in command.Split('|', ';', '&'))
        {
            var token = segment.Trim().Split(' ', 2)[0].Split('/').LastOrDefault() ?? "";
            if (BlockedCommands.Contains(token))
                return $"Error: command '{token}' is blocked for safety.";
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(DefaultTimeoutMs);

        var psi = new ProcessStartInfo
        {
            FileName = "/bin/sh",
            Arguments = $"-c \"{command.Replace("\"", "\\\"")}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start shell process");

        // Close stdin immediately to prevent interactive commands from hanging
        process.StandardInput.Close();

        var stdout = await ReadCappedAsync(process.StandardOutput, MaxOutputBytes, cts.Token);
        var stderr = await ReadCappedAsync(process.StandardError, MaxOutputBytes / 4, cts.Token);

        try { await process.WaitForExitAsync(cts.Token); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            throw;
        }

        var result = stdout;
        if (process.ExitCode != 0 && !string.IsNullOrEmpty(stderr))
            result += $"\n[exit code {process.ExitCode}]\n[stderr] {stderr}";

        return string.IsNullOrEmpty(result) ? "(no output)" : result;
    }

    private static async Task<string> ReadCappedAsync(StreamReader reader, int maxBytes, CancellationToken ct)
    {
        var buffer = new char[maxBytes];
        int read = await reader.ReadBlockAsync(buffer.AsMemory(0, maxBytes), ct);
        var text = new string(buffer, 0, read);
        if (read == maxBytes)
            text += "\n... (output truncated)";
        return text;
    }
}
