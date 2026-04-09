using System.Text.Json;

namespace AzureOpenAI_CLI.Tools;

/// <summary>
/// Read the contents of a file from the local filesystem.
/// </summary>
internal sealed class ReadFileTool : IBuiltInTool
{
    private const int MaxFileSizeBytes = 262_144; // 256 KB

    // Blocked path prefixes — any resolved path starting with these is denied.
    private static readonly string[] BlockedPathPrefixes = new[]
    {
        "/etc/shadow",
        "/etc/passwd",
        "/etc/sudoers",
        "/etc/hosts",
        "/root/.ssh",
        "/proc/self/environ",
        "/proc/self/cmdline",
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
        if (arguments.ValueKind != JsonValueKind.Object ||
            !arguments.TryGetProperty("path", out var pathProp))
            return Task.FromResult("Error: missing required parameter 'path'.");

        var path = pathProp.GetString();
        if (string.IsNullOrEmpty(path))
            return Task.FromResult("Error: parameter 'path' must not be empty.");

        // Expand ~ to home directory
        if (path.StartsWith('~'))
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[1..].TrimStart('/'));

        path = Path.GetFullPath(path);

        // Check the logical path against blocked prefixes first
        if (IsBlockedPath(path))
            return Task.FromResult($"Error: access to '{path}' is blocked for security.");

        if (!File.Exists(path))
            return Task.FromResult($"Error: file not found: {path}");

        // Resolve symlinks: the real target path must also be checked
        var resolvedPath = ResolveSymlinks(path);
        if (resolvedPath != path && IsBlockedPath(resolvedPath))
            return Task.FromResult($"Error: access to '{path}' is blocked for security (symlink target is restricted).");

        var info = new FileInfo(path);
        if (info.Length > MaxFileSizeBytes)
            return Task.FromResult($"Error: file too large ({info.Length:N0} bytes, max {MaxFileSizeBytes:N0}).");

        ct.ThrowIfCancellationRequested();
        var content = File.ReadAllText(path);
        return Task.FromResult(content);
    }

    /// <summary>
    /// Check if a path matches or falls under any blocked path prefix.
    /// </summary>
    internal static bool IsBlockedPath(string fullPath)
    {
        foreach (var prefix in BlockedPathPrefixes)
        {
            if (fullPath.Equals(prefix, StringComparison.OrdinalIgnoreCase)
                || fullPath.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Resolve symlinks to the final real path. Returns the original path if not a symlink
    /// or if resolution fails.
    /// </summary>
    private static string ResolveSymlinks(string path)
    {
        try
        {
            var target = File.ResolveLinkTarget(path, returnFinalTarget: true);
            if (target is not null)
                return Path.GetFullPath(target.FullName);
        }
        catch
        {
            // If we can't resolve, fall through and use the original path
        }
        return path;
    }
}
