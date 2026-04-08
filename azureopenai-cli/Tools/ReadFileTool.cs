using System.Text.Json;

namespace AzureOpenAI_CLI.Tools;

/// <summary>
/// Read the contents of a file from the local filesystem.
/// </summary>
internal sealed class ReadFileTool : IBuiltInTool
{
    private const int MaxFileSizeBytes = 262_144; // 256 KB

    private static readonly HashSet<string> BlockedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/etc/shadow", "/etc/passwd", "/etc/sudoers",
    };

    public string Name => "read_file";
    public string Description => "Read the contents of a file. Useful for reviewing code, config files, logs, or documents.";
    public BinaryData ParametersSchema => BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "path": { "type": "string", "description": "Absolute or relative file path to read" }
            },
            "required": ["path"]
        }
        """);

    public Task<string> ExecuteAsync(JsonElement arguments, CancellationToken ct)
    {
        var path = arguments.GetProperty("path").GetString()
            ?? throw new ArgumentException("Missing 'path' parameter");

        // Expand ~ to home directory
        if (path.StartsWith('~'))
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[1..].TrimStart('/'));

        path = Path.GetFullPath(path);

        if (BlockedPaths.Any(bp => path.Equals(bp, StringComparison.OrdinalIgnoreCase)))
            return Task.FromResult($"Error: access to '{path}' is blocked for security.");

        if (!File.Exists(path))
            return Task.FromResult($"Error: file not found: {path}");

        var info = new FileInfo(path);
        if (info.Length > MaxFileSizeBytes)
            return Task.FromResult($"Error: file too large ({info.Length:N0} bytes, max {MaxFileSizeBytes:N0}).");

        ct.ThrowIfCancellationRequested();
        var content = File.ReadAllText(path);
        return Task.FromResult(content);
    }
}
