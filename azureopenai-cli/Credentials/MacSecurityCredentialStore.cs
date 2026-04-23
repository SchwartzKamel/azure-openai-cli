using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;

namespace AzureOpenAI_CLI.Credentials;

/// <summary>
/// macOS Keychain-backed credential store. Shells out to <c>/usr/bin/security</c>
/// (<c>add-generic-password</c> / <c>find-generic-password</c> / <c>delete-generic-password</c>)
/// under service <c>az-ai</c>, account <c>$USER</c>.
/// </summary>
/// <remarks>
/// The Keychain holds the actual key; <see cref="UserConfig"/> only persists the
/// provider name and a non-secret fingerprint so the load path can locate the key.
/// All spawns use <see cref="ProcessStartInfo.ArgumentList"/> (never the shell-interpreted
/// <c>Arguments</c> string) to eliminate shell-injection risk on the key value.
/// </remarks>
[SupportedOSPlatform("macos")]
internal sealed class MacSecurityCredentialStore : ICredentialStore
{
    private const string SecurityBinary = "/usr/bin/security";
    private const string ServiceName = "az-ai";
    private const int SpawnTimeoutMs = 10_000;

    // `security` exits 44 for errSecItemNotFound via the CLI.
    private const int ExitItemNotFound = 44;

    private readonly UserConfig _config;
    private readonly string _account;
    private string? _cachedKey;

    public MacSecurityCredentialStore(UserConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _account = ResolveAccount();
    }

    public string ProviderName => "macos-keychain";

    public void Store(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API key must not be null, empty, or whitespace.", nameof(apiKey));
        }

        var (exitCode, _, stderr) = RunSecurity(
            "add-generic-password", "-U",
            "-s", ServiceName,
            "-a", _account,
            "-w", apiKey);

        if (exitCode != 0)
        {
            throw new CredentialStoreException(
                $"security add-generic-password failed (exit {exitCode}): {Scrub(stderr, apiKey)}");
        }

        _cachedKey = apiKey;
        _config.ApiKeyProvider = ProviderName;
        _config.Save();
    }

    public string? Retrieve()
    {
        if (_cachedKey != null)
        {
            return _cachedKey;
        }

        var (exitCode, stdout, stderr) = RunSecurity(
            "find-generic-password",
            "-s", ServiceName,
            "-a", _account,
            "-w");

        if (exitCode == ExitItemNotFound)
        {
            return null;
        }

        if (exitCode != 0)
        {
            throw new CredentialStoreException(
                $"security find-generic-password failed (exit {exitCode}): {Scrub(stderr, null)}");
        }

        // `-w` prints just the password followed by a newline.
        string key = stdout.TrimEnd('\r', '\n');
        if (key.Length == 0)
        {
            return null;
        }

        _cachedKey = key;
        return key;
    }

    public void Delete()
    {
        var (exitCode, _, stderr) = RunSecurity(
            "delete-generic-password",
            "-s", ServiceName,
            "-a", _account);

        if (exitCode != 0 && exitCode != ExitItemNotFound)
        {
            throw new CredentialStoreException(
                $"security delete-generic-password failed (exit {exitCode}): {Scrub(stderr, null)}");
        }

        _cachedKey = null;
        _config.ApiKeyProvider = null;
        _config.ApiKeyFingerprint = null;
        _config.Save();
    }

    private static string ResolveAccount()
    {
        string user = Environment.UserName;
        if (!string.IsNullOrEmpty(user))
        {
            return user;
        }
        return Environment.GetEnvironmentVariable("USER") ?? "default";
    }

    private static (int ExitCode, string Stdout, string Stderr) RunSecurity(params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = SecurityBinary,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string a in args)
        {
            psi.ArgumentList.Add(a);
        }

        using var proc = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

        try
        {
            proc.Start();
        }
        catch (Exception ex)
        {
            throw new CredentialStoreException($"Failed to spawn {SecurityBinary}: {ex.Message}", ex);
        }

        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        if (!proc.WaitForExit(SpawnTimeoutMs))
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* best-effort */ }
            throw new CredentialStoreException(
                $"security timed out after {SpawnTimeoutMs}ms (possible Keychain UI prompt).");
        }

        // Ensure async readers drain.
        proc.WaitForExit();

        return (proc.ExitCode, stdout.ToString(), stderr.ToString());
    }

    /// <summary>
    /// Scrubs any occurrence of the raw API key from diagnostic text before it is
    /// surfaced in an exception message or log. Defence in depth — <c>security</c>
    /// should not echo the key, but we never want to rely on that.
    /// </summary>
    private static string Scrub(string text, string? secret)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }
        string cleaned = text.Trim();
        if (!string.IsNullOrEmpty(secret))
        {
            cleaned = cleaned.Replace(secret, "<redacted>", StringComparison.Ordinal);
        }
        return cleaned;
    }
}

