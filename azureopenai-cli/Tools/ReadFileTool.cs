using System.ComponentModel;
using System.Text;

namespace AzureOpenAI_CLI.Tools;

/// <summary>
/// Read the contents of a file from the local filesystem.
/// MAF version: uses [Description] attributes for AIFunctionFactory.Create.
/// </summary>
internal static class ReadFileTool
{
    private const int MaxFileSizeBytes = 262_144; // 256 KB

    // Blocked path prefixes — any resolved path starting with these is denied.
    // Tilde-prefixed entries are expanded to the current user's home at check-time.
    //
    // S02E26 *The Locked Drawer* (Newman) extended this list to cover the 7
    // home-dir credential stores logged by S02E23 *The Adversary*:
    //   ssh-userdir, kube-config, gnupg, netrc, docker-config,
    //   git-credentials, npmrc-pypirc. gh-cli/hosts.yml added as defensible
    //   bonus (gh-cli's token store, same threat shape).
    private static readonly string[] BlockedPathPrefixes = new[]
    {
        // System-level
        "/etc/shadow",
        "/etc/passwd",
        "/etc/sudoers",
        "/etc/hosts",
        "/root/.ssh",
        "/proc/self/environ",
        "/proc/self/cmdline",
        "/var/run/secrets",        // Kubernetes / systemd service-account tokens
        "/run/secrets",            // Docker/Podman secret mount point
        "/var/run/docker.sock",

        // Per-user credential stores (pre-existing)
        "~/.aws",                  // AWS CLI credentials / config
        "~/.azure",                // Azure CLI tokens & service-principal creds
        "~/.config/az-ai",         // az-ai CLI config directory
        "~/.azureopenai-cli.json", // this CLI's own config file

        // Per-user credential stores (S02E26 extensions — the "locked drawer")
        "~/.ssh",                  // OpenSSH private keys, known_hosts, authorized_keys
        "~/.kube",                 // kubectl cluster creds + tokens (config, cache)
        "~/.gnupg",                // GPG private keyrings, trust DB, agent sockets
        "~/.netrc",                // machine/login/password credentials (FTP, curl, git)
        "~/.docker/config.json",   // registry auth tokens (docker login)
        "~/.git-credentials",      // unencrypted git credential-store output
        "~/.config/git/credentials", // XDG-config location for git-credentials
        "~/.npmrc",                // npm auth tokens (_authToken)
        "~/.pypirc",               // PyPI upload credentials
        "~/.config/gh/hosts.yml",  // GitHub CLI OAuth tokens
    };

    [Description("Read the contents of a file. Useful for reviewing code, config files, logs, or documents.")]
    public static Task<string> ReadAsync(
        [Description("Absolute or relative file path to read")] string path,
        CancellationToken ct = default)
    {
        var validation = Validate(path, out var canonical);
        if (validation != null)
            return Task.FromResult(validation);

        if (!File.Exists(canonical))
            return Task.FromResult($"Error: file not found: {canonical}");

        // Resolve symlinks: the real target path must also be checked against
        // the blocklist. A symlink /tmp/evil -> /etc/shadow would otherwise
        // leak shadow contents past the logical-path gate above.
        var resolvedPath = ResolveSymlinks(canonical);
        if (resolvedPath != canonical && IsBlockedPath(resolvedPath))
            return Task.FromResult($"Error: access to '{canonical}' is blocked for security (symlink target is restricted).");

        var info = new FileInfo(canonical);
        if (info.Length > MaxFileSizeBytes)
            return Task.FromResult($"Error: file too large ({info.Length:N0} bytes, max {MaxFileSizeBytes:N0}).");

        ct.ThrowIfCancellationRequested();
        var content = File.ReadAllText(canonical);
        return Task.FromResult(content);
    }

    /// <summary>
    /// Defense-in-depth validation pipeline for an LLM-supplied file path
    /// (S02E26 *The Locked Drawer*, structurally modelled on E32's
    /// <c>ShellExecTool.Validate</c>). Returns an <c>"Error: ..."</c> string
    /// if the path should be rejected, or <c>null</c> if it may proceed;
    /// the canonicalized absolute path is returned via <paramref name="canonical"/>.
    ///
    /// Stages:
    ///   1. Non-empty check + reject NUL / control bytes that can truncate
    ///      the path before it reaches the OS (<c>"~/.ssh/id_rsa\0/etc/hostname"</c>).
    ///   2. Reject percent-encoded path components (<c>%2E</c>, <c>%2F</c>,
    ///      <c>%00</c>) — .NET does not URL-decode paths, so these almost
    ///      always indicate an evasion attempt rather than a legitimate
    ///      filename. Default-deny.
    ///   3. NFKC-normalize so fullwidth / compatibility lookalikes (e.g.
    ///      <c>\uFF0E\u0073\u0073\u0068</c> for ".ssh") collapse to their
    ///      ASCII equivalents before prefix matching.
    ///   4. Expand a leading <c>~</c> to the current user's home directory.
    ///   5. <c>Path.GetFullPath</c> to canonicalize: resolves <c>..</c>,
    ///      collapses <c>//double/slash</c>, strips trailing separators.
    ///   6. Blocklist prefix match against <see cref="IsBlockedPath"/> on
    ///      the canonical path — substring-on-raw-input is forbidden
    ///      (that's what E32 fixed for shell, and same shape here).
    /// Symlink resolution happens in <see cref="ReadAsync"/> after existence
    /// check because <c>File.ResolveLinkTarget</c> requires the link itself
    /// to exist.
    /// </summary>
    internal static string? Validate(string rawPath, out string canonical)
    {
        canonical = string.Empty;

        if (string.IsNullOrEmpty(rawPath))
            return "Error: parameter 'path' must not be empty.";

        // 1. Reject NUL / control bytes.
        foreach (var c in rawPath)
        {
            if (c == '\0' || (c < 0x20 && c != '\t'))
                return "Error: path contains control characters.";
        }

        // 2. Reject percent-encoded segments. .NET's file APIs treat
        //    "%2F" as literal characters, not a URL-decoded "/", so a
        //    caller smuggling "~/%2Essh/id_rsa" is almost certainly
        //    trying to bypass the blocklist.
        if (rawPath.Contains("%2f", StringComparison.OrdinalIgnoreCase) ||
            rawPath.Contains("%2e", StringComparison.OrdinalIgnoreCase) ||
            rawPath.Contains("%00", StringComparison.OrdinalIgnoreCase))
            return "Error: percent-encoded paths are not supported.";

        // 3. NFKC-normalize to collapse unicode lookalikes.
        string normalized;
        try { normalized = rawPath.Normalize(NormalizationForm.FormKC); }
        catch (ArgumentException) { return "Error: path is not valid Unicode."; }

        // 4. Expand leading ~ to home directory.
        string expanded = normalized;
        if (expanded.StartsWith('~'))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            expanded = Path.Combine(home, expanded[1..].TrimStart('/', '\\'));
        }

        // 5. Canonicalize. Path.GetFullPath resolves "..", collapses
        //    duplicate separators, and returns an absolute path.
        try { canonical = Path.GetFullPath(expanded); }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
        {
            return $"Error: invalid path: {ex.Message}";
        }

        // 6. Blocklist prefix match on the canonical form.
        if (IsBlockedPath(canonical))
            return $"Error: access to '{canonical}' is blocked for security.";

        return null;
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
