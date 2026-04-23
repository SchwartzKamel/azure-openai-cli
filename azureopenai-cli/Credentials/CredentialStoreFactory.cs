using AzureOpenAI_CLI.Setup;

namespace AzureOpenAI_CLI.Credentials;

/// <summary>
/// Selects the appropriate <see cref="ICredentialStore"/> implementation for the current
/// runtime environment.
/// </summary>
/// <remarks>
/// Precedence (first match wins):
/// <list type="number">
///   <item><description>Container runtime → plaintext (no desktop keyring / DBus session in containers).</description></item>
///   <item><description>Windows → DPAPI (user-scoped).</description></item>
///   <item><description>macOS → Keychain via <c>/usr/bin/security</c>.</description></item>
///   <item><description>Linux, opportunistically → libsecret via <c>/usr/bin/secret-tool</c>
///     when the binary is present AND a DBus session bus is advertised
///     (<c>DBUS_SESSION_BUS_ADDRESS</c>). Covers GNOME Keyring and KDE Wallet
///     (via libsecret-kwallet-bridge).</description></item>
///   <item><description>Anything else (Linux without libsecret, headless boxes, unknown OS) → plaintext at
///     <c>~/.azureopenai-cli.json</c> mode <c>0600</c>.</description></item>
/// </list>
/// </remarks>
internal static class CredentialStoreFactory
{
    /// <summary>
    /// Creates a credential store for the current OS / runtime.
    /// </summary>
    /// <param name="config">The loaded user configuration. Reserved for future provider-pinning;
    /// current implementation selects purely by platform.</param>
    public static ICredentialStore Create(UserConfig config)
    {
        if (SetupDetection.IsContainer())
        {
            return new PlaintextCredentialStore(config);
        }

        if (OperatingSystem.IsWindows())
        {
            return new DpapiCredentialStore(config);
        }

        if (OperatingSystem.IsMacOS())
        {
            return new MacSecurityCredentialStore(config);
        }

        if (OperatingSystem.IsLinux() && SecretToolAvailable())
        {
            return new SecretToolCredentialStore(config);
        }

        return new PlaintextCredentialStore(config);
    }

    /// <summary>
    /// Returns true iff <c>/usr/bin/secret-tool</c> is installed AND a DBus session bus is
    /// advertised via <c>DBUS_SESSION_BUS_ADDRESS</c>. Both are required — secret-tool without
    /// a session bus will hang or error; an advertised bus without the binary can't be used.
    /// </summary>
    private static bool SecretToolAvailable()
    {
        if (!File.Exists("/usr/bin/secret-tool"))
        {
            return false;
        }
        string? dbus = Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS");
        return !string.IsNullOrEmpty(dbus);
    }
}
