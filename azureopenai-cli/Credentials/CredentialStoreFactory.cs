using AzureOpenAI_CLI.Setup;

namespace AzureOpenAI_CLI.Credentials;

/// <summary>
/// Selects the appropriate <see cref="ICredentialStore"/> implementation for the current
/// runtime environment. Container → plaintext; Windows → DPAPI; macOS → Keychain; else plaintext.
/// </summary>
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

        return new PlaintextCredentialStore(config);
    }
}
