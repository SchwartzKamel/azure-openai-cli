using System.ComponentModel;

namespace AzureOpenAI_CLI_V2.Tools;

/// <summary>
/// Read the contents of a file from the local filesystem.
/// MAF version: uses [Description] attributes for AIFunctionFactory.Create.
/// </summary>
internal static class ReadFileTool
{
    private const int MaxFileSizeBytes = 262_144; // 256 KB

    // Blocked path prefixes — any resolved path starting with these is denied.
    // Tilde-prefixed entries are expanded to the current user's home at check-time.
    private static readonly string[] BlockedPathPrefixes = new[]
    {
        "/etc/shadow",
        "/etc/passwd",
        "/etc/sudoers",
        "/etc/hosts",
        "/root/.ssh",
        "/proc/self/environ",
        "/proc/self/cmdline",
        "/var/run/secrets",   // Kubernetes / systemd service-account tokens
        "/run/secrets",       // Docker/Podman secret mount point
        "/var/run/docker.sock",
        "~/.aws",             // AWS CLI credentials / config
        "~/.azure",           // Azure CLI tokens & service-principal creds
        "~/.config/az-ai",    // az-ai CLI config directory
        "~/.azureopenai-cli.json", // this CLI's own config file
    };

    [Description("Read the contents of a file. Useful for reviewing code, config files, logs, or documents.")]
    public static Task<string> ReadAsync(
        [Description("Absolute or relative file path to read")] string path,
        CancellationToken ct = default)
    {
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
    /// Tilde-prefixed blocklist entries are expanded to the current user's
    /// home directory at check-time, so (e.g.) "~/.aws" matches both
    /// "/home/alice/.aws" and "/home/alice/.aws/credentials".
    /// </summary>
    internal static bool IsBlockedPath(string fullPath)
    {
        // Block any file whose name is `.env` or ends in `.env` (case-insensitive),
        // EXCEPT common example/sample variants which contain no real secrets.
        var fileName = Path.GetFileName(fullPath);
        if (!string.IsNullOrEmpty(fileName))
        {
            bool isEnvExample =
                fileName.EndsWith(".env.example", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".env.sample", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".env.template", StringComparison.OrdinalIgnoreCase);
            bool isEnv =
                fileName.Equals(".env", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".env", StringComparison.OrdinalIgnoreCase);
            if (isEnv && !isEnvExample)
                return true;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (var raw in BlockedPathPrefixes)
        {
            var prefix = raw.StartsWith('~')
                ? Path.Combine(home, raw[1..].TrimStart('/'))
                : raw;
            if (fullPath.Equals(prefix, StringComparison.OrdinalIgnoreCase)
                || fullPath.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase)
                || fullPath.StartsWith(prefix + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
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
