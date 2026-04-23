using System.Text.Json;
using AzureOpenAI_CLI;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// Tests for the UserConfig class covering persistence, model selection,
/// and environment variable parsing.
///
/// File I/O tests operate on the real config path (~/.azureopenai-cli.json).
/// The fixture backs up any existing config before each test and restores it after.
/// </summary>
[Collection("UserConfigFile")]
public class UserConfigTests : IDisposable
{
    private readonly string _configPath;
    private readonly string _backupPath;
    private readonly bool _hadExistingConfig;

    public UserConfigTests()
    {
        _configPath = UserConfig.GetConfigPath();
        _backupPath = _configPath + ".test-backup";
        _hadExistingConfig = File.Exists(_configPath);

        if (_hadExistingConfig)
            File.Copy(_configPath, _backupPath, overwrite: true);

        // Start each test with a clean slate — no config file on disk
        if (File.Exists(_configPath))
            File.Delete(_configPath);
    }

    public void Dispose()
    {
        // Retry with short delay to handle transient file locks from parallel tests
        for (int i = 0; i < 3; i++)
        {
            try
            {
                if (File.Exists(_configPath))
                    File.Delete(_configPath);

                if (_hadExistingConfig && File.Exists(_backupPath))
                {
                    File.Copy(_backupPath, _configPath, overwrite: true);
                    File.Delete(_backupPath);
                }
                return;
            }
            catch (IOException) when (i < 2)
            {
                Thread.Sleep(100);
            }
        }
    }

    // ── Load / Save ────────────────────────────────────────────────

    [Fact]
    public void Load_ReturnsDefault_WhenFileDoesNotExist()
    {
        // Arrange — config file was deleted in the constructor

        // Act
        var config = UserConfig.Load();

        // Assert — fresh default has no model selected and no models available
        Assert.NotNull(config);
        Assert.Null(config.ActiveModel);
        Assert.Empty(config.AvailableModels);
    }

    [Fact]
    public void Save_And_Load_RoundTrip()
    {
        // Arrange
        var original = new UserConfig
        {
            ActiveModel = "gpt-4o",
            AvailableModels = new List<string> { "gpt-4o", "gpt-35-turbo", "gpt-4" }
        };

        // Act
        original.Save();
        var loaded = UserConfig.Load();

        // Assert — every field survives the round-trip
        Assert.Equal(original.ActiveModel, loaded.ActiveModel);
        Assert.Equal(original.AvailableModels, loaded.AvailableModels);
    }

    [Fact]
    public void Save_CreatesFileWithContent()
    {
        // Arrange
        var config = new UserConfig
        {
            ActiveModel = "gpt-4o",
            AvailableModels = new List<string> { "gpt-4o" }
        };

        // Act
        config.Save();

        // Assert — file exists and contains valid JSON
        Assert.True(File.Exists(_configPath), "Config file should be created on disk");

        string json = File.ReadAllText(_configPath);
        Assert.False(string.IsNullOrWhiteSpace(json), "Config file should not be empty");

        // Parsing must not throw — that's our validity check
        var doc = JsonDocument.Parse(json);
        Assert.NotNull(doc);
    }

    // ── SetActiveModel ─────────────────────────────────────────────

    [Fact]
    public void SetActiveModel_CaseInsensitive()
    {
        // Arrange — list stores the model in mixed case
        var config = new UserConfig
        {
            AvailableModels = new List<string> { "GPT-4o", "gpt-35-turbo" }
        };

        // Act — request with all-lowercase
        bool result = config.SetActiveModel("gpt-4o");

        // Assert — succeeds and preserves the canonical casing from the list
        Assert.True(result);
        Assert.Equal("GPT-4o", config.ActiveModel);
    }

    [Fact]
    public void SetActiveModel_InvalidModel_ReturnsFalse()
    {
        // Arrange
        var config = new UserConfig
        {
            AvailableModels = new List<string> { "gpt-4o", "gpt-35-turbo" }
        };

        // Act
        bool result = config.SetActiveModel("nonexistent-model");

        // Assert — no change to active model
        Assert.False(result);
        Assert.Null(config.ActiveModel);
    }

    // ── InitializeFromEnvironment ──────────────────────────────────

    [Fact]
    public void InitializeFromEnvironment_ParsesCommaList()
    {
        // Arrange
        var config = new UserConfig();

        // Act — spaces around items should be trimmed
        config.InitializeFromEnvironment("gpt-4o, gpt-35-turbo , gpt-4");

        // Assert
        Assert.Equal(3, config.AvailableModels.Count);
        Assert.Contains("gpt-4o", config.AvailableModels);
        Assert.Contains("gpt-35-turbo", config.AvailableModels);
        Assert.Contains("gpt-4", config.AvailableModels);
        // First model in the list becomes the active model
        Assert.Equal("gpt-4o", config.ActiveModel);
    }

    [Fact]
    public void InitializeFromEnvironment_NullOrEmpty_DoesNothing()
    {
        // Arrange
        var config = new UserConfig();

        // Act
        config.InitializeFromEnvironment(null);
        config.InitializeFromEnvironment("");

        // Assert — config remains at defaults
        Assert.Empty(config.AvailableModels);
        Assert.Null(config.ActiveModel);
    }

    [Fact]
    public void InitializeFromEnvironment_DeduplicatesModels()
    {
        // Arrange
        var config = new UserConfig();

        // Act — duplicate entries
        config.InitializeFromEnvironment("gpt-4o,gpt-4o,gpt-35-turbo");

        // Assert — duplicates removed
        Assert.Equal(2, config.AvailableModels.Count);
        Assert.Single(config.AvailableModels, m => m == "gpt-4o");
    }

    [Fact]
    public void InitializeFromEnvironment_PreservesValidActiveModel()
    {
        // Arrange — active model is already set and is in the new list
        var config = new UserConfig
        {
            ActiveModel = "gpt-35-turbo",
            AvailableModels = new List<string> { "gpt-35-turbo" }
        };

        // Act — reinitialize with a list that still includes the active model
        config.InitializeFromEnvironment("gpt-4o,gpt-35-turbo,gpt-4");

        // Assert — active model stays the same because it's still valid
        Assert.Equal("gpt-35-turbo", config.ActiveModel);
    }

    // ── ComputeFingerprint ─────────────────────────────────────────

    [Fact]
    public void ComputeFingerprint_KnownVector_ReturnsExpected()
    {
        // Ground truth: `echo -n test-api-key-12345678 | sha256sum` → b5aa8fe6584e...
        const string input = "test-api-key-12345678";
        const string expected = "b5aa8fe6584e";

        string actual = UserConfig.ComputeFingerprint(input);

        Assert.Equal(expected, actual);
        Assert.Equal(12, actual.Length);
        Assert.Matches("^[0-9a-f]{12}$", actual);
    }

    [Fact]
    public void ComputeFingerprint_EmptyString_Throws()
    {
        Assert.Throws<ArgumentException>(() => UserConfig.ComputeFingerprint(""));
    }

    [Fact]
    public void ComputeFingerprint_Null_Throws()
    {
        // Impl uses `string.IsNullOrEmpty` + `throw new ArgumentException`, so null also surfaces as ArgumentException.
        Assert.Throws<ArgumentException>(() => UserConfig.ComputeFingerprint(null!));
    }

    [Fact]
    public void ComputeFingerprint_IsDeterministic()
    {
        const string input = "deterministic-key-value";
        string first = UserConfig.ComputeFingerprint(input);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(first, UserConfig.ComputeFingerprint(input));
        }
    }

    [Fact]
    public void ComputeFingerprint_IsCaseSensitive()
    {
        // sha256 operates on bytes, so "Foo" vs "foo" MUST produce distinct fingerprints.
        string upper = UserConfig.ComputeFingerprint("Foo");
        string lower = UserConfig.ComputeFingerprint("foo");

        Assert.NotEqual(upper, lower);
    }

    [Theory]
    [InlineData("key-a", "key-b")]
    [InlineData("sk-0000000000000000", "sk-0000000000000001")]
    [InlineData("short", "short ")] // trailing space differs
    public void ComputeFingerprint_DifferentKeys_DifferentFingerprints(string a, string b)
    {
        Assert.NotEqual(UserConfig.ComputeFingerprint(a), UserConfig.ComputeFingerprint(b));
    }

    // ── Credential-field round-trip ────────────────────────────────

    [Fact]
    public void Save_Load_RoundTrip_NewCredentialFields()
    {
        // Arrange — populate every new credential field.
        var original = new UserConfig
        {
            ActiveModel = "gpt-4o",
            AvailableModels = new List<string> { "gpt-4o" },
            Endpoint = "https://example.openai.azure.com/",
            ApiKey = "plaintext-key-linux-only",
            ApiKeyCiphertext = "ZmFrZS1kcGFwaS1ibG9i", // base64-ish noise
            ApiKeyProvider = "plaintext",
            ApiKeyFingerprint = UserConfig.ComputeFingerprint("plaintext-key-linux-only")
        };

        // Act
        original.Save();
        var loaded = UserConfig.Load();

        // Assert — every new field survives.
        Assert.Equal(original.Endpoint, loaded.Endpoint);
        Assert.Equal(original.ApiKey, loaded.ApiKey);
        Assert.Equal(original.ApiKeyCiphertext, loaded.ApiKeyCiphertext);
        Assert.Equal(original.ApiKeyProvider, loaded.ApiKeyProvider);
        Assert.Equal(original.ApiKeyFingerprint, loaded.ApiKeyFingerprint);
        Assert.Equal(original.ActiveModel, loaded.ActiveModel);
    }

    [Fact]
    public void Save_Load_RoundTrip_HandlesAllNullCredentialFields()
    {
        // Arrange — completely empty config.
        var original = new UserConfig();

        // Act — must not throw on save or load.
        original.Save();
        var loaded = UserConfig.Load();

        // Assert — null-preservation for every new field.
        Assert.Null(loaded.Endpoint);
        Assert.Null(loaded.ApiKey);
        Assert.Null(loaded.ApiKeyCiphertext);
        Assert.Null(loaded.ApiKeyProvider);
        Assert.Null(loaded.ApiKeyFingerprint);
    }

    // ── ToString redaction ─────────────────────────────────────────

    [Fact]
    public void ToString_DoesNotLeakApiKey()
    {
        const string secret = "super-secret-12345";
        const string ciphertext = "ciphertext-bytes";

        var config = new UserConfig
        {
            Endpoint = "https://example.openai.azure.com/",
            ActiveModel = "gpt-4o",
            ApiKey = secret,
            ApiKeyCiphertext = ciphertext,
            ApiKeyProvider = "plaintext",
            ApiKeyFingerprint = UserConfig.ComputeFingerprint(secret)
        };

        string rendered = config.ToString();

        // Secrets must never appear.
        Assert.DoesNotContain(secret, rendered);
        Assert.DoesNotContain(ciphertext, rendered);

        // Non-sensitive debug fields SHOULD be visible.
        Assert.Contains("plaintext", rendered);
        Assert.Contains(config.ApiKeyFingerprint!, rendered);
        Assert.Contains("https://example.openai.azure.com/", rendered);
        Assert.Contains("gpt-4o", rendered);
    }
}
