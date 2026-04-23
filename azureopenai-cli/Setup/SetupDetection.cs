using AzureOpenAI_CLI.Credentials;

namespace AzureOpenAI_CLI.Setup;

/// <summary>
/// Pure environment-detection helpers used by the first-run wizard, the credential
/// store factory, and <c>Program.cs</c> to decide whether interactive prompting is
/// appropriate and whether credentials are already resolvable from any source.
/// </summary>
/// <remarks>
/// All methods are deterministic given process environment (env vars, TTY state,
/// filesystem markers). They perform no network or user IO. Intended to be cheap
/// enough to call on every invocation of the CLI.
/// </remarks>
internal static class SetupDetection
{
    /// <summary>
    /// Returns <c>true</c> when both stdin and stderr are attached to a terminal, meaning
    /// the CLI can safely prompt the user and render hidden-input UI without corrupting
    /// scripted pipelines.
    /// </summary>
    /// <remarks>
    /// We deliberately check <see cref="Console.IsInputRedirected"/> and
    /// <see cref="Console.IsErrorRedirected"/> — stdout redirection (<c>| grep</c>,
    /// <c>&gt; out.txt</c>) is fine because prompts render on stderr. Edge cases:
    /// <list type="bullet">
    ///   <item><description>Espanso / AHK launch with piped stdin → returns <c>false</c>.</description></item>
    ///   <item><description>CI runners (GitHub Actions, etc.) → returns <c>false</c>.</description></item>
    ///   <item><description>Docker <c>-it</c> with attached TTY → returns <c>true</c>,
    ///     but <see cref="IsContainer"/> should still gate wizard execution separately.</description></item>
    /// </list>
    /// </remarks>
    public static bool IsInteractive()
    {
        return !Console.IsInputRedirected && !Console.IsErrorRedirected;
    }

    /// <summary>
    /// Returns <c>true</c> when the process is running inside a container (Docker,
    /// Podman, Kubernetes, or systemd-nspawn). Used to suppress the interactive wizard
    /// — containers are assumed to be scripted environments that should fail loudly
    /// when credentials are missing rather than prompt.
    /// </summary>
    /// <remarks>
    /// Probes, in order (short-circuit on first hit):
    /// <list type="number">
    ///   <item><description><c>/.dockerenv</c> exists — Docker / Podman marker file.</description></item>
    ///   <item><description><c>$container</c> env var is set — systemd-nspawn, Podman, and
    ///     some rootless runtimes.</description></item>
    ///   <item><description><c>$KUBERNETES_SERVICE_HOST</c> is set — pod injected by kubelet.</description></item>
    /// </list>
    /// Note: this does not detect WSL, which is an interactive terminal environment
    /// and should go through the normal TTY-based wizard flow.
    /// </remarks>
    public static bool IsContainer()
    {
        if (File.Exists("/.dockerenv"))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("container")))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST")))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns <c>true</c> when both an endpoint URL and an API key are resolvable from
    /// any configured source — environment variables, the persisted user config, or
    /// the OS-appropriate credential store. Used to decide whether the first-run wizard
    /// should launch.
    /// </summary>
    /// <param name="envEndpoint">Value of <c>AZUREOPENAIENDPOINT</c>, or <c>null</c>.</param>
    /// <param name="envApiKey">Value of <c>AZUREOPENAIAPI</c>, or <c>null</c>.</param>
    /// <param name="config">The loaded <see cref="UserConfig"/>. Reserved for when
    /// <c>userconfig-fields</c> adds the <c>Endpoint</c> / <c>ApiKey</c> properties; the
    /// parameter is part of the stable public shape consumed by the wizard trigger logic.</param>
    /// <param name="store">Credential store consulted for the API key. A <c>Retrieve()</c>
    /// call that returns non-null satisfies the key requirement.</param>
    /// <returns>
    /// <c>false</c> only when the endpoint is missing from every source AND the key is
    /// missing from every source. The model deployment name is intentionally *not* part
    /// of this check — the wizard runs when endpoint+key are missing; model can be
    /// resolved later via <c>--model</c>, env, or config.
    /// </returns>
    /// <remarks>
    /// Whitespace-only strings are treated as missing. Credential-store lookup failures
    /// (<see cref="CredentialStoreException"/>) are *not* swallowed here — callers should
    /// decide how to surface them; this method will propagate the exception.
    /// </remarks>
    public static bool HasCredentials(
        string? envEndpoint,
        string? envApiKey,
        UserConfig config,
        ICredentialStore store)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(store);

        bool hasEndpoint = !string.IsNullOrWhiteSpace(envEndpoint)
            || !string.IsNullOrWhiteSpace(ConfigEndpoint(config));

        if (!hasEndpoint)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(envApiKey))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(ConfigApiKey(config)))
        {
            return true;
        }

        string? stored = store.Retrieve();
        return !string.IsNullOrWhiteSpace(stored);
    }

    // These accessors isolate the UserConfig coupling so tests can reason about them
    // independently. ApiKey is the plaintext Linux/container field; DPAPI and Keychain
    // providers surface the key via ICredentialStore.Retrieve(), so we don't need to
    // read ApiKeyCiphertext here — the store handles decryption transparently.
    private static string? ConfigEndpoint(UserConfig config) => config.Endpoint;

    private static string? ConfigApiKey(UserConfig config) => config.ApiKey;
}
