using System.Diagnostics;
using System.Text.Json;

namespace AzureOpenAI_CLI.Tools;

/// <summary>
/// Delegate a subtask to a child agent instance of this CLI.
/// Enables hierarchical task decomposition — the parent agent can spawn
/// focused sub-agents for specific tasks (e.g., "read and summarize this file",
/// "research this topic on the web", "run tests and report results").
/// </summary>
internal sealed class DelegateTaskTool : IBuiltInTool
{
    private const int DefaultTimeoutMs = 60_000; // 60s for subtasks
    private const int MaxOutputBytes = 65_536; // 64 KB
    private const int MaxDepth = 3;

    public string Name => "delegate_task";
    public string Description => "Delegate a subtask to a child agent. Use this to break complex tasks into smaller, focused sub-tasks. The child agent has access to all tools (shell, file, web, etc.) and will return its response.";
    public BinaryData ParametersSchema => BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "task": { "type": "string", "description": "A clear, specific description of the subtask to delegate" },
                "tools": { "type": "string", "description": "Comma-separated list of tools to enable for the child agent (default: all). Options: shell, file, web, datetime, clipboard" }
            },
            "required": ["task"]
        }
        """);

    public async Task<string> ExecuteAsync(JsonElement arguments, CancellationToken ct)
    {
        var task = arguments.GetProperty("task").GetString()
            ?? throw new ArgumentException("Missing 'task' parameter");

        // Check recursion depth
        var depthStr = Environment.GetEnvironmentVariable("RALPH_DEPTH") ?? "0";
        if (!int.TryParse(depthStr, out int currentDepth))
            currentDepth = 0;

        if (currentDepth >= MaxDepth)
            return $"Error: maximum delegation depth ({MaxDepth}) reached. Complete this task directly instead of delegating.";

        // Build tools argument
        string toolsArg = "shell,file,web,datetime";
        if (arguments.TryGetProperty("tools", out var toolsEl) && toolsEl.GetString() is string tools && !string.IsNullOrWhiteSpace(tools))
            toolsArg = tools;

        // Find the CLI binary — use dotnet run or the compiled binary
        string cliPath;
        string cliArgs;

        var assemblyDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        var dllPath = Path.Combine(assemblyDir ?? ".", "AzureOpenAI_CLI.dll");

        if (File.Exists(dllPath))
        {
            cliPath = "dotnet";
            cliArgs = $"\"{dllPath}\" --agent --tools {toolsArg} \"{task.Replace("\"", "\\\"")}\"";
        }
        else
        {
            // Fallback: try to find the binary directly
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (exePath != null && File.Exists(exePath))
            {
                cliPath = exePath;
                cliArgs = $"--agent --tools {toolsArg} \"{task.Replace("\"", "\\\"")}\"";
            }
            else
            {
                return "Error: could not locate CLI binary for delegation.";
            }
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(DefaultTimeoutMs);

        var psi = new ProcessStartInfo
        {
            FileName = cliPath,
            Arguments = cliArgs,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // Pass through Azure credentials and increment depth
        foreach (var envVar in new[] { "AZUREOPENAIENDPOINT", "AZUREOPENAIAPI", "AZUREOPENAIMODEL", "AZURE_DEEPSEEK_KEY" })
        {
            var val = Environment.GetEnvironmentVariable(envVar);
            if (val != null)
                psi.Environment[envVar] = val;
        }
        psi.Environment["RALPH_DEPTH"] = (currentDepth + 1).ToString();

        try
        {
            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start child agent");

            process.StandardInput.Close();

            var buffer = new char[MaxOutputBytes];
            int read = await process.StandardOutput.ReadBlockAsync(buffer.AsMemory(0, MaxOutputBytes), cts.Token);

            try { await process.WaitForExitAsync(cts.Token); }
            catch (OperationCanceledException)
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                return $"Error: child agent timed out after {DefaultTimeoutMs / 1000}s";
            }

            var output = new string(buffer, 0, read);
            if (read == MaxOutputBytes)
                output += "\n... (child agent output truncated)";

            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync(cts.Token);
                if (!string.IsNullOrEmpty(stderr))
                    output += $"\n[child agent exit code {process.ExitCode}]\n[stderr] {stderr.Trim()}";
            }

            return string.IsNullOrEmpty(output) ? "(child agent produced no output)" : output;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return "Error: could not start child agent process.";
        }
    }
}
