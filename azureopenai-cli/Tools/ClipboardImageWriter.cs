using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace AzureOpenAI_CLI.Tools;

/// <summary>
/// Copies a PNG image file to the system clipboard. Platform-specific:
/// Linux (X11): xclip, Linux (Wayland): wl-copy, macOS: osascript,
/// Windows/WSL: powershell.exe.
/// This is a helper class, not a registered tool.
/// </summary>
internal static class ClipboardImageWriter
{
    private const int TimeoutMs = 10_000;

    /// <summary>
    /// Copies the image at <paramref name="filePath"/> to the system clipboard.
    /// Returns true on success, false on failure (logs to stderr, never throws).
    /// </summary>
    public static bool CopyToClipboard(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                WriteWarning($"File not found: {filePath}");
                return false;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return RunMacOS(filePath);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (IsWSL())
                {
                    return RunWSL(filePath);
                }

                if (IsWayland())
                {
                    if (TryRunWayland(filePath))
                        return true;
                    // Fall back to xclip on Wayland if wl-copy is unavailable
                }

                return RunX11(filePath);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return RunWindows(filePath);
            }

            WriteWarning("Clipboard image copy not supported on this platform.");
            return false;
        }
        catch (Exception ex)
        {
            WriteWarning($"Clipboard copy failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// macOS: use osascript to set the clipboard to a PNG image.
    /// </summary>
    private static bool RunMacOS(string filePath)
    {
        var script = $"set the clipboard to (read (POSIX file \"{filePath}\") as «class PNGf»)";
        return RunProcess("osascript", $"-e '{script}'", redirectInput: null);
    }

    /// <summary>
    /// Linux X11: use xclip to copy PNG to clipboard.
    /// </summary>
    private static bool RunX11(string filePath)
    {
        if (FindCommand("xclip") is null)
        {
            WriteWarning("xclip not found. Install xclip for clipboard image support.");
            return false;
        }

        return RunProcess("xclip", $"-selection clipboard -t image/png -i \"{filePath}\"", redirectInput: null);
    }

    /// <summary>
    /// Linux Wayland: use wl-copy to copy PNG to clipboard.
    /// </summary>
    private static bool TryRunWayland(string filePath)
    {
        if (FindCommand("wl-copy") is null)
        {
            return false;
        }

        return RunProcess("wl-copy", "--type image/png", redirectInput: filePath);
    }

    /// <summary>
    /// WSL: convert path with wslpath and use powershell.exe to write to Windows clipboard.
    /// </summary>
    private static bool RunWSL(string filePath)
    {
        // Convert WSL path to Windows path
        var winPath = GetWindowsPath(filePath);
        if (winPath is null)
        {
            WriteWarning("Failed to convert WSL path to Windows path.");
            return false;
        }

        var psCommand = $"Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.Clipboard]::SetImage([System.Drawing.Image]::FromFile('{winPath}'))";
        return RunProcess("powershell.exe", $"-NoProfile -Command \"{psCommand}\"", redirectInput: null);
    }

    /// <summary>
    /// Native Windows: use powershell to write to clipboard.
    /// </summary>
    private static bool RunWindows(string filePath)
    {
        var psCommand = $"Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.Clipboard]::SetImage([System.Drawing.Image]::FromFile('{filePath}'))";
        return RunProcess("powershell", $"-NoProfile -Command \"{psCommand}\"", redirectInput: null);
    }

    /// <summary>
    /// Runs a process with the given arguments. If <paramref name="redirectInput"/> is set,
    /// pipes the file contents to stdin. Returns true if exit code is 0.
    /// </summary>
    private static bool RunProcess(string command, string arguments, string? redirectInput)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = redirectInput is not null,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        try
        {
            using var process = Process.Start(psi);
            if (process is null)
            {
                WriteWarning($"Failed to start process: {command}");
                return false;
            }

            if (redirectInput is not null)
            {
                using var inputStream = File.OpenRead(redirectInput);
                inputStream.CopyTo(process.StandardInput.BaseStream);
                process.StandardInput.Close();
            }

            var completed = process.WaitForExit(TimeoutMs);
            if (!completed)
            {
                try { process.Kill(); } catch { /* best effort */ }
                WriteWarning($"Clipboard operation timed out ({command}).");
                return false;
            }

            if (process.ExitCode != 0)
            {
                var stderr = process.StandardError.ReadToEnd().Trim();
                WriteWarning($"Clipboard command failed (exit {process.ExitCode}): {stderr}");
                return false;
            }

            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            WriteWarning($"Command not found: {command}");
            return false;
        }
    }

    /// <summary>
    /// Detects whether the current environment is WSL by checking /proc/version.
    /// </summary>
    private static bool IsWSL()
    {
        try
        {
            if (!File.Exists("/proc/version"))
                return false;

            var content = File.ReadAllText("/proc/version");
            return content.Contains("microsoft", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Detects whether the current session is running under Wayland.
    /// </summary>
    private static bool IsWayland()
    {
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
    }

    /// <summary>
    /// Converts a WSL Linux path to a Windows path using wslpath.
    /// </summary>
    private static string? GetWindowsPath(string linuxPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "wslpath",
                Arguments = $"-w \"{linuxPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            using var process = Process.Start(psi);
            if (process is null) return null;

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(5000);

            return process.ExitCode == 0 && !string.IsNullOrEmpty(output) ? output : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Locates a command on PATH using 'which'.
    /// Returns the full path if found, null otherwise.
    /// </summary>
    private static string? FindCommand(string commandName)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = commandName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
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

    /// <summary>
    /// Writes a warning message to stderr.
    /// </summary>
    private static void WriteWarning(string message)
    {
        Console.Error.WriteLine($"[WARNING] {message}");
    }
}
