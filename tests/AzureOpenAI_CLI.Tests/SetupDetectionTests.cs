using AzureOpenAI_CLI;
using AzureOpenAI_CLI.Credentials;
using AzureOpenAI_CLI.Setup;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// Tests for <see cref="SetupDetection"/> covering the <c>HasCredentials()</c>
/// decision matrix across environment variables, user config, and credential
/// stores.
///
/// Limitations: <c>IsInteractive()</c> and <c>IsContainer()</c> depend on ambient
/// process state (TTY attachment, <c>/.dockerenv</c>, env vars like
/// <c>KUBERNETES_SERVICE_HOST</c>) that cannot be safely mutated from within a
/// running xUnit process without cross-test contamination. They are exercised
/// indirectly via integration tests and by Program.cs callers. Only lightweight
/// "does-not-throw" assertions are made here for those two methods.
/// </summary>
public class SetupDetectionTests
{
    // ── Test double ─────────────────────────────────────────────────

    private sealed class FakeCredentialStore : ICredentialStore
    {
        public string? StoredKey { get; set; }
        public Exception? ThrowOnRetrieve { get; set; }
        public string ProviderName => "fake";
        public void Store(string apiKey) => StoredKey = apiKey;
        public string? Retrieve() => ThrowOnRetrieve != null ? throw ThrowOnRetrieve : StoredKey;
        public void Delete() => StoredKey = null;
    }

    // ── HasCredentials ──────────────────────────────────────────────

    [Fact]
    public void HasCredentials_AllSources_Missing_ReturnsFalse()
    {
        var config = new UserConfig();
        var store = new FakeCredentialStore();

        Assert.False(SetupDetection.HasCredentials(null, null, config, store));
    }

    [Fact]
    public void HasCredentials_EnvVarsBothSet_ReturnsTrue()
    {
        var config = new UserConfig();
        var store = new FakeCredentialStore();

        Assert.True(SetupDetection.HasCredentials(
            "https://foo.openai.azure.com/", "key-value", config, store));
    }

    [Fact]
    public void HasCredentials_EnvEndpoint_StoreKey_ReturnsTrue()
    {
        // ConfigEndpoint / ConfigApiKey currently return null (reserved for
        // userconfig-fields work). Endpoint must come from env; key from store.
        var config = new UserConfig();
        var store = new FakeCredentialStore { StoredKey = "k" };

        Assert.True(SetupDetection.HasCredentials(
            "https://foo.openai.azure.com/", null, config, store));
    }

    [Fact]
    public void HasCredentials_EnvKey_ConfigEndpoint_ReturnsTrue()
    {
        // UserConfig.Endpoint supplies the endpoint, env var supplies the key.
        var config = new UserConfig { Endpoint = "https://foo.openai.azure.com/" };
        var store = new FakeCredentialStore();

        Assert.True(SetupDetection.HasCredentials(null, "key", config, store));
    }

    [Fact]
    public void HasCredentials_ConfigEndpoint_ConfigApiKey_ReturnsTrue()
    {
        // Both endpoint and key come from persisted UserConfig (the post-wizard state
        // on Linux, where PlaintextCredentialStore round-trips via config.ApiKey).
        var config = new UserConfig
        {
            Endpoint = "https://foo.openai.azure.com/",
            ApiKey = "stored-key",
        };
        var store = new FakeCredentialStore();

        Assert.True(SetupDetection.HasCredentials(null, null, config, store));
    }

    [Fact]
    public void HasCredentials_StoreProvidesKey_ReturnsTrue()
    {
        var config = new UserConfig();
        var store = new FakeCredentialStore { StoredKey = "k" };

        Assert.True(SetupDetection.HasCredentials(
            "https://foo.openai.azure.com/", null, config, store));
    }

    [Fact]
    public void HasCredentials_EndpointPresentButKeyMissing_ReturnsFalse()
    {
        var config = new UserConfig();
        var store = new FakeCredentialStore();

        Assert.False(SetupDetection.HasCredentials(
            "https://foo.openai.azure.com/", null, config, store));
    }

    [Fact]
    public void HasCredentials_KeyPresentButEndpointMissing_ReturnsFalse()
    {
        var config = new UserConfig();
        var store = new FakeCredentialStore { StoredKey = "k" };

        Assert.False(SetupDetection.HasCredentials(null, null, config, store));
    }

    [Fact]
    public void HasCredentials_WhitespaceEndpoint_TreatedAsMissing()
    {
        var config = new UserConfig();
        var store = new FakeCredentialStore { StoredKey = "k" };

        Assert.False(SetupDetection.HasCredentials("   ", "k", config, store));
    }

    [Fact]
    public void HasCredentials_WhitespaceKey_TreatedAsMissing()
    {
        var config = new UserConfig();
        var store = new FakeCredentialStore();

        Assert.False(SetupDetection.HasCredentials(
            "https://foo.openai.azure.com/", "   ", config, store));
    }

    [Fact]
    public void HasCredentials_StoreThrows_PropagatesException()
    {
        // XML doc on HasCredentials states: "CredentialStoreException failures
        // are *not* swallowed here — callers should decide how to surface them;
        // this method will propagate the exception."
        var config = new UserConfig();
        var store = new FakeCredentialStore
        {
            ThrowOnRetrieve = new CredentialStoreException("boom"),
        };

        var ex = Assert.Throws<CredentialStoreException>(() =>
            SetupDetection.HasCredentials("https://foo.openai.azure.com/", null, config, store));
        Assert.Equal("boom", ex.Message);
    }

    [Fact]
    public void HasCredentials_NullConfig_Throws()
    {
        var store = new FakeCredentialStore();
        Assert.Throws<ArgumentNullException>(() =>
            SetupDetection.HasCredentials("https://foo.openai.azure.com/", "k", null!, store));
    }

    [Fact]
    public void HasCredentials_NullStore_Throws()
    {
        var config = new UserConfig();
        Assert.Throws<ArgumentNullException>(() =>
            SetupDetection.HasCredentials("https://foo.openai.azure.com/", "k", config, null!));
    }

    // ── IsInteractive / IsContainer: smoke checks only ──────────────

    [Fact]
    public void IsInteractive_DoesNotThrow()
    {
        // Ambient-state dependent; we only assert it's callable and returns a bool.
        _ = SetupDetection.IsInteractive();
    }

    [Fact]
    public void IsContainer_DoesNotThrow()
    {
        // Ambient-state dependent; we only assert it's callable and returns a bool.
        _ = SetupDetection.IsContainer();
    }
}
