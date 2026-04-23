namespace AzureOpenAI_CLI.Credentials;

/// <summary>
/// Plaintext credential store backed by <c>~/.azureopenai-cli.json</c> at mode <c>0600</c>
/// (owner read/write only on Unix; ACL inheritance from the user profile on Windows).
/// Used on Linux and inside containers where no first-class OS keystore is guaranteed.
/// </summary>
/// <remarks>
/// <para>
/// This store is intentionally plaintext at mode <c>0600</c>. That matches the baseline of the
/// AWS CLI (<c>~/.aws/credentials</c>), the GitHub CLI on Linux (<c>~/.config/gh/hosts.yml</c>),
/// and the Azure CLI on Linux (<c>~/.azure/</c>). The compensating control is user-initiated
/// key rotation — see the README section on credential hygiene.
/// </para>
/// <para>
/// For higher assurance, users should prefer environment variables, Docker secrets, or CI
/// secret stores. Environment variables always take precedence over values read from this store.
/// </para>
/// <para>
/// The key is never logged, printed, or included in <see cref="ToString"/>.
/// </para>
/// </remarks>
internal sealed class PlaintextCredentialStore : ICredentialStore
{
    private readonly UserConfig _config;

    public PlaintextCredentialStore(UserConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
    }

    public string ProviderName => "plaintext";

    public void Store(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API key must not be null, empty, or whitespace.", nameof(apiKey));
        }

        _config.ApiKey = apiKey;
        _config.ApiKeyProvider = ProviderName;
        _config.Save();
    }

    public string? Retrieve() => _config.ApiKey;

    public void Delete()
    {
        _config.ApiKey = null;
        _config.ApiKeyProvider = null;
        _config.Save();
    }

    /// <summary>
    /// Returns a provider-only description. Never includes the stored key.
    /// </summary>
    public override string ToString() => $"PlaintextCredentialStore(provider={ProviderName})";
}
