namespace AzureOpenAI_CLI.Credentials;

/// <summary>
/// Abstraction over a per-OS credential store used to persist the Azure OpenAI API key.
/// Implementations include DPAPI (Windows), macOS Keychain via <c>/usr/bin/security</c>,
/// and a plaintext file-backed store (Linux / containers).
/// </summary>
/// <remarks>
/// Contract:
/// <list type="bullet">
///   <item><description><see cref="Retrieve"/> returns <c>null</c> when no key is stored; it does
///     not throw for the "missing" case.</description></item>
///   <item><description>All methods throw <see cref="CredentialStoreException"/> on IO, permission,
///     or platform failures (e.g. DPAPI returns an error, <c>security</c> exits non-zero for a
///     reason other than "not found", config file unreadable).</description></item>
///   <item><description>Implementations must be safe to call without an existing store — <see cref="Store"/>
///     creates whatever backing state is needed; <see cref="Delete"/> is a no-op if nothing is stored.</description></item>
/// </list>
/// </remarks>
internal interface ICredentialStore
{
    /// <summary>
    /// Stable identifier for the provider. One of <c>"dpapi"</c>, <c>"macos-keychain"</c>, or
    /// <c>"plaintext"</c>. Persisted in <c>UserConfig.ApiKeyProvider</c> so the load path knows
    /// how to reconstruct the key.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Persists <paramref name="apiKey"/> to the backing store, overwriting any existing value.
    /// </summary>
    /// <param name="apiKey">The plaintext API key. Must not be null or empty.</param>
    /// <exception cref="CredentialStoreException">Thrown on IO, permission, or platform errors.</exception>
    void Store(string apiKey);

    /// <summary>
    /// Retrieves the stored API key in plaintext, or <c>null</c> if no key is stored.
    /// </summary>
    /// <returns>The plaintext API key, or <c>null</c> when absent.</returns>
    /// <exception cref="CredentialStoreException">Thrown on IO, permission, or platform errors
    /// (but <i>not</i> for a simple "not found" condition — that returns <c>null</c>).</exception>
    string? Retrieve();

    /// <summary>
    /// Removes any stored API key. A no-op if nothing is stored.
    /// </summary>
    /// <exception cref="CredentialStoreException">Thrown on IO, permission, or platform errors.</exception>
    void Delete();
}

/// <summary>
/// Thrown by <see cref="ICredentialStore"/> implementations when a store operation fails due to
/// IO, permission, or platform-specific errors. The "key not present" case does <i>not</i> throw;
/// <see cref="ICredentialStore.Retrieve"/> returns <c>null</c> instead.
/// </summary>
internal sealed class CredentialStoreException : Exception
{
    public CredentialStoreException(string message) : base(message) { }
    public CredentialStoreException(string message, Exception innerException) : base(message, innerException) { }
}
