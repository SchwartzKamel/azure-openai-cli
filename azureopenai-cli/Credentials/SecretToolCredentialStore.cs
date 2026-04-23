using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;

namespace AzureOpenAI_CLI.Credentials;

/// <summary>
/// Opportunistic Linux credential store backed by libsecret via
/// <c>/usr/bin/secret-tool</c> (GNOME Keyring, KDE Wallet through the
/// libsecret-kwallet bridge, and any other libsecret-compatible backend
/// exposed on the user's DBus session bus). Service <c>az-ai</c>,
/// account <c>$USER</c>.
/// </summary>
/// <remarks>
/// Mirrors <see cref="MacSecurityCredentialStore"/>: the backing store
/// holds the actual key; <see cref="UserConfig"/> only persists the
/// provider name and a non-secret fingerprint so the load path can locate
/// the key. The secret itself is handed to <c>secret-tool store</c> via
/// stdin — never on argv — which is the whole point of libsecret. All
/// spawns use <see cref="ProcessStartInfo.ArgumentList"/> (never the
/// shell-interpreted <c>Arguments</c> string).
/// </remarks>
[SupportedOSPlatform("linux")]
internal sealed class SecretToolCredentialStore : ICredentialStore
{
    private const string SecretToolBinary = "/usr/bin/secret-tool";
    private const string ServiceName = "az-ai";
    private const int SpawnTimeoutMs = 10_000;

    private readonly UserConfig _config;
    private readonly string _account;
    private string? _cachedKey;

    public SecretToolCredentialStore(UserConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _account = ResolveAccount();
    }

    public string ProviderName => "libsecret";

    public void Store(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API key must not be null, empty, or whitespace.", nameof(apiKey));
        }

        // `secret-tool store` reads the secret from stdin. The label is
        // cosmetic; attributes (service/username) are how we look it up.
        var (exitCode, _, stderr) = RunSecretTool(
            stdin: apiKey,
            "store", "--label=az-ai",
            "service", ServiceName,
            "username", _account);

        if (exitCode != 0)
        {
            throw new CredentialStoreException(
                $"secret-tool store failed (exit {exitCode}): {Scrub(stderr, apiKey)}");
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

        var (exitCode, stdout, stderr) = RunSecretTool(
            stdin: null,
            "lookup",
            "service", ServiceName,
            "username", _account);

        if (exitCode == 0)
        {
            string key = stdout.TrimEnd('\r', '\n');
            if (key.Length == 0)
            {
                return null;
            }
            _cachedKey = key;
            return key;
        }

        // libsecret uses exit 1 for "not found" AND for genuine errors
        // without distinguishing. Treat empty-stderr exit 1 as not-found;
        // anything else bubbles as a store exception.
        string scrubbed = Scrub(stderr, null);
        if (string.IsNullOrEmpty(scrubbed))
        {
            return null;
        }

        throw new CredentialStoreException(
            $"secret-tool lookup failed (exit {exitCode}): {scrubbed}");
    }

    public void Delete()
    {
        // `secret-tool clear` is idempotent and exits 0 whether or not an
        // entry existed. Any non-zero exit indicates a real failure.
        var (exitCode, _, stderr) = RunSecretTool(
            stdin: null,
            "clear",
            "service", ServiceName,
            "username", _account);

        if (exitCode != 0)
        {
            throw new CredentialStoreException(
                $"secret-tool clear failed (exit {exitCode}): {Scrub(stderr, null)}");
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

    private static (int ExitCode, string Stdout, string Stderr) RunSecretTool(string? stdin, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = SecretToolBinary,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = stdin != null,
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
            throw new CredentialStoreException($"Failed to spawn {SecretToolBinary}: {ex.Message}", ex);
        }

        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        if (stdin != null)
        {
            try
            {
                proc.StandardInput.Write(stdin);
                proc.StandardInput.Write('\n');
                proc.StandardInput.Close();
            }
            catch (Exception ex)
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* best-effort */ }
                throw new CredentialStoreException(
                    $"Failed to write secret to {SecretToolBinary} stdin: {ex.Message}", ex);
            }
        }

        if (!proc.WaitForExit(SpawnTimeoutMs))
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* best-effort */ }
            throw new CredentialStoreException(
                $"secret-tool timed out after {SpawnTimeoutMs}ms (possible keyring unlock prompt).");
        }

        // Ensure async readers drain.
        proc.WaitForExit();

        return (proc.ExitCode, stdout.ToString(), stderr.ToString());
    }

    /// <summary>
    /// Scrubs any occurrence of the raw API key from diagnostic text before it is
    /// surfaced in an exception message or log. Defence in depth — <c>secret-tool</c>
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
