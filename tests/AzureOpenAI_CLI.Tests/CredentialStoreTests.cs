using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using System.Text;
using AzureOpenAI_CLI;
using AzureOpenAI_CLI.Credentials;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// Shared xUnit collection for any test class that touches the real
/// <c>~/.azureopenai-cli.json</c> file. Serializes those classes to prevent
/// backup/restore races. Opt-in via <c>[Collection("UserConfigFile")]</c>.
/// </summary>
[CollectionDefinition("UserConfigFile", DisableParallelization = true)]
public sealed class UserConfigFileCollection { }

// ── Platform-gated Fact attributes ──────────────────────────────────
// These run on matching OS only; otherwise reported as "skipped" by the
// xUnit runner so Linux CI keeps a clean green.

public sealed class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "Windows-only test (DPAPI)";
        }
    }
}

public sealed class MacOnlyFactAttribute : FactAttribute
{
    public MacOnlyFactAttribute()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Skip = "macOS-only test (Keychain)";
        }
    }
}

public sealed class UnixOnlyFactAttribute : FactAttribute
{
    public UnixOnlyFactAttribute()
    {
        if (OperatingSystem.IsWindows())
        {
            Skip = "Unix-only test (file mode check)";
        }
    }
}

/// <summary>
/// Shared fixture: backs up the real <c>~/.azureopenai-cli.json</c> before each test
/// and restores it afterward. UserConfig.Save() writes to a fixed path, so tests that
/// exercise Save/Load must quarantine the real file.
/// </summary>
public abstract class CredentialStoreTestBase : IDisposable
{
    protected readonly string ConfigPath;
    private readonly string _backupPath;
    private readonly bool _hadExistingConfig;

    protected CredentialStoreTestBase()
    {
        ConfigPath = UserConfig.GetConfigPath();
        _backupPath = ConfigPath + ".credstore-test-backup";
        _hadExistingConfig = File.Exists(ConfigPath);

        if (_hadExistingConfig)
        {
            File.Copy(ConfigPath, _backupPath, overwrite: true);
        }

        if (File.Exists(ConfigPath))
        {
            File.Delete(ConfigPath);
        }
    }

    public void Dispose()
    {
        for (int i = 0; i < 3; i++)
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    File.Delete(ConfigPath);
                }
                if (_hadExistingConfig && File.Exists(_backupPath))
                {
                    File.Copy(_backupPath, ConfigPath, overwrite: true);
                    File.Delete(_backupPath);
                }
                else if (File.Exists(_backupPath))
                {
                    File.Delete(_backupPath);
                }
                GC.SuppressFinalize(this);
                return;
            }
            catch (IOException) when (i < 2)
            {
                Thread.Sleep(100);
            }
        }
    }
}

// ── PlaintextCredentialStore — runs everywhere ──────────────────────

[Collection("UserConfigFile")]
public sealed class PlaintextCredentialStoreTests : CredentialStoreTestBase
{
    private const string SampleKey = "PLAINTEXT-KEY-ZSENTINEL-123456";

    [Fact]
    public void ProviderName_Is_Plaintext()
    {
        var store = new PlaintextCredentialStore(new UserConfig());
        Assert.Equal("plaintext", store.ProviderName);
    }

    [Fact]
    public void Store_SetsApiKey_AndProvider()
    {
        var config = new UserConfig();
        var store = new PlaintextCredentialStore(config);

        store.Store(SampleKey);

        Assert.Equal(SampleKey, config.ApiKey);
        Assert.Equal("plaintext", config.ApiKeyProvider);
    }

    [Fact]
    public void Retrieve_ReturnsNull_WhenNoKey()
    {
        var store = new PlaintextCredentialStore(new UserConfig());
        Assert.Null(store.Retrieve());
    }

    [Fact]
    public void Retrieve_ReturnsKey_AfterStore()
    {
        var config = new UserConfig();
        var store = new PlaintextCredentialStore(config);

        store.Store(SampleKey);

        Assert.Equal(SampleKey, store.Retrieve());
    }

    [Fact]
    public void Delete_ClearsKey_AndProvider()
    {
        var config = new UserConfig();
        var store = new PlaintextCredentialStore(config);
        store.Store(SampleKey);

        store.Delete();

        Assert.Null(config.ApiKey);
        Assert.Null(config.ApiKeyProvider);
        Assert.Null(store.Retrieve());
    }

    [Fact]
    public void Store_Null_Throws()
    {
        var store = new PlaintextCredentialStore(new UserConfig());
        Assert.Throws<ArgumentException>(() => store.Store(null!));
    }

    [Fact]
    public void Store_Empty_Throws()
    {
        var store = new PlaintextCredentialStore(new UserConfig());
        Assert.Throws<ArgumentException>(() => store.Store(string.Empty));
    }

    [Fact]
    public void Store_Whitespace_Throws()
    {
        // Whitespace keys are always caller bugs (copy-paste artifact, empty env
        // expansion, accidental space). The Store() guard now rejects them
        // across all three providers via IsNullOrWhiteSpace.
        var config = new UserConfig();
        var store = new PlaintextCredentialStore(config);

        var ex = Assert.Throws<ArgumentException>(() => store.Store("   "));
        Assert.Contains("whitespace", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(config.ApiKey);
    }

    [Fact]
    public void ToString_DoesNotIncludeKey()
    {
        var config = new UserConfig();
        var store = new PlaintextCredentialStore(config);
        store.Store(SampleKey);

        string rendered = store.ToString()!;

        Assert.DoesNotContain(SampleKey, rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("ZSENTINEL", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void ExceptionMessage_DoesNotContainKey()
    {
        var store = new PlaintextCredentialStore(new UserConfig());
        // Force an ArgumentException path. The key argument is null/empty, so we
        // cannot leak it — but assert that the message never echoes ambient state.
        var ex = Assert.Throws<ArgumentException>(() => store.Store(string.Empty));
        Assert.DoesNotContain(SampleKey, ex.Message ?? string.Empty, StringComparison.Ordinal);
    }

    [UnixOnlyFact]
    public void Store_SetsFileModeTo0600_OnUnix()
    {
        var config = new UserConfig();
        var store = new PlaintextCredentialStore(config);

        store.Store(SampleKey);

        Assert.True(File.Exists(ConfigPath));
        if (OperatingSystem.IsWindows()) return; // CA1416 guard; attribute already skips.
        var mode = File.GetUnixFileMode(ConfigPath);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
    }

    [UnixOnlyFact]
    public void Store_DoesNotWriteKeyToDisk_InCleartextConfig_UnderTrivialGrep()
    {
        // FDR: even in plaintext mode, verify the key is actually the only thing
        // that leaks to disk — i.e. nothing else in the config echoes it.
        var config = new UserConfig { Endpoint = "https://example.openai.azure.com/" };
        var store = new PlaintextCredentialStore(config);
        store.Store(SampleKey);

        string contents = File.ReadAllText(ConfigPath);
        // The key SHOULD be there (plaintext is explicit). Sanity-check it appears exactly once.
        int count = 0;
        int idx = 0;
        while ((idx = contents.IndexOf(SampleKey, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += SampleKey.Length;
        }
        Assert.Equal(1, count);
    }
}

// ── DpapiCredentialStore — Windows only ─────────────────────────────

[SupportedOSPlatform("windows")]
[Collection("UserConfigFile")]
public sealed class DpapiCredentialStoreTests : CredentialStoreTestBase
{
    private const string SampleKey = "DPAPI-KEY-SENTINEL-ABC123-very-long-unique-marker";

    [WindowsOnlyFact]
    public void ProviderName_Is_Dpapi()
    {
        var store = new DpapiCredentialStore(new UserConfig());
        Assert.Equal("dpapi", store.ProviderName);
    }

    [WindowsOnlyFact]
    public void Store_EncryptsKey_AndSetsProvider()
    {
        var config = new UserConfig();
        var store = new DpapiCredentialStore(config);

        store.Store(SampleKey);

        Assert.False(string.IsNullOrEmpty(config.ApiKeyCiphertext));
        Assert.Equal("dpapi", config.ApiKeyProvider);
        // Plaintext field MUST NOT be populated by the DPAPI path.
        Assert.Null(config.ApiKey);
    }

    [WindowsOnlyFact]
    public void Retrieve_RoundTrip()
    {
        var config = new UserConfig();
        var store = new DpapiCredentialStore(config);

        store.Store(SampleKey);

        Assert.Equal(SampleKey, store.Retrieve());
    }

    [WindowsOnlyFact]
    public void Retrieve_ReturnsNull_WhenCiphertextIsNull()
    {
        var store = new DpapiCredentialStore(new UserConfig());
        Assert.Null(store.Retrieve());
    }

    [WindowsOnlyFact]
    public void Retrieve_Throws_OnCorruptedCiphertext()
    {
        var config = new UserConfig();
        var store = new DpapiCredentialStore(config);
        store.Store(SampleKey);

        // Truncate ciphertext — still valid base64 (probably), but DPAPI will reject.
        string original = config.ApiKeyCiphertext!;
        config.ApiKeyCiphertext = original.Substring(0, original.Length / 2);

        var ex = Assert.Throws<CredentialStoreException>(() => store.Retrieve());
        Assert.DoesNotContain(SampleKey, ex.Message, StringComparison.Ordinal);
    }

    [WindowsOnlyFact]
    public void Retrieve_Throws_OnInvalidBase64()
    {
        var config = new UserConfig
        {
            ApiKeyCiphertext = "not-base64!@#$%^&*()",
            ApiKeyProvider = "dpapi",
        };
        var store = new DpapiCredentialStore(config);

        Assert.Throws<CredentialStoreException>(() => store.Retrieve());
    }

    [WindowsOnlyFact]
    public void Delete_ClearsCiphertext_AndProvider()
    {
        var config = new UserConfig();
        var store = new DpapiCredentialStore(config);
        store.Store(SampleKey);

        store.Delete();

        Assert.Null(config.ApiKeyCiphertext);
        Assert.Null(config.ApiKeyProvider);
        Assert.Null(store.Retrieve());
    }

    [WindowsOnlyFact]
    public void Store_Null_Throws()
    {
        var store = new DpapiCredentialStore(new UserConfig());
        Assert.Throws<ArgumentException>(() => store.Store(null!));
    }

    [WindowsOnlyFact]
    public void Store_Empty_Throws()
    {
        var store = new DpapiCredentialStore(new UserConfig());
        Assert.Throws<ArgumentException>(() => store.Store(string.Empty));
    }

    [WindowsOnlyFact]
    public void Ciphertext_DoesNotContainKey()
    {
        var config = new UserConfig();
        var store = new DpapiCredentialStore(config);
        store.Store(SampleKey);

        byte[] raw = Convert.FromBase64String(config.ApiKeyCiphertext!);
        byte[] needle = Encoding.UTF8.GetBytes("SENTINEL");

        Assert.False(ContainsSubsequence(raw, needle),
            "DPAPI ciphertext leaks the plaintext key — encryption did not occur.");
    }

    [WindowsOnlyFact]
    public void ExceptionMessage_DoesNotContainKey()
    {
        var config = new UserConfig();
        var store = new DpapiCredentialStore(config);
        store.Store(SampleKey);
        // Corrupt ciphertext to force a decryption error.
        config.ApiKeyCiphertext = "AAAA" + config.ApiKeyCiphertext!.Substring(4);

        var ex = Assert.Throws<CredentialStoreException>(() => store.Retrieve());
        Assert.DoesNotContain(SampleKey, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("SENTINEL", ex.Message, StringComparison.Ordinal);
    }

    private static bool ContainsSubsequence(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0 || haystack.Length < needle.Length)
        {
            return false;
        }
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }
            if (match) return true;
        }
        return false;
    }
}

// ── MacSecurityCredentialStore — macOS only ─────────────────────────
//
// IMPORTANT: MacSecurityCredentialStore.ServiceName is a hard-coded "az-ai"
// constant with no test-time injection hook. Running the full suite would
// pollute the developer's login Keychain and could leave orphaned entries
// if cleanup fails mid-test. Per the task brief:
//
//   "If you cannot cleanly inject the service name, SKIP these tests with
//    a clear comment. Do not let them pollute the dev's login Keychain."
//
// All Mac tests are therefore unconditionally skipped. The scaffolding is
// retained so a future PR that adds an `internal` test constructor accepting
// a service-name override can enable them by dropping the explicit Skip.

public sealed class MacSecurityCredentialStoreTests
{
    private const string SkipReason =
        "MacSecurityCredentialStore has no test-time service-name injection hook; " +
        "enabling these tests would pollute the developer's login Keychain. " +
        "Add an internal ctor accepting a custom service name to unblock.";

    [Fact(Skip = SkipReason)] public void ProviderName_Is_MacosKeychain() { }
    [Fact(Skip = SkipReason)] public void Store_AddsToKeychain_AndSetsProvider() { }
    [Fact(Skip = SkipReason)] public void Retrieve_RoundTrip() { }
    [Fact(Skip = SkipReason)] public void Retrieve_ReturnsNull_WhenNotPresent() { }
    [Fact(Skip = SkipReason)] public void Delete_RemovesFromKeychain() { }
    [Fact(Skip = SkipReason)] public void Delete_Idempotent() { }
    [Fact(Skip = SkipReason)] public void Cache_ReturnsQuickly_OnSecondRetrieve() { }
    [Fact(Skip = SkipReason)] public void Store_Null_Throws() { }
    [Fact(Skip = SkipReason)] public void ExceptionMessage_DoesNotContainKey() { }
}

// ── SecretToolCredentialStore — Linux only ─────────────────────────
//
// The store/retrieve/roundtrip tests require a real libsecret daemon and
// a DBus session bus, neither of which exist on ubuntu-latest CI. Those
// are marked [Fact(Skip = ...)] following the Mac pattern. The guard
// tests (ProviderName, argument validation, factory detection) don't
// need the daemon and run unconditionally on Linux.

public sealed class LinuxOnlyFactAttribute : FactAttribute
{
    public LinuxOnlyFactAttribute()
    {
        if (!OperatingSystem.IsLinux())
        {
            Skip = "Linux-only test (libsecret / secret-tool)";
        }
    }
}

public sealed class SecretToolCredentialStoreTests
{
    private const string DaemonSkipReason =
        "Requires a running libsecret daemon and a DBus session bus, which " +
        "are not available in CI (ubuntu-latest). Runnable locally on a " +
        "GNOME / KDE desktop session.";

    [LinuxOnlyFact]
    [SupportedOSPlatform("linux")]
    public void ProviderName_Is_Libsecret()
    {
        var store = new SecretToolCredentialStore(new UserConfig());
        Assert.Equal("libsecret", store.ProviderName);
    }

    [LinuxOnlyFact]
    [SupportedOSPlatform("linux")]
    public void Store_Null_Throws()
    {
        var store = new SecretToolCredentialStore(new UserConfig());
        Assert.Throws<ArgumentException>(() => store.Store(null!));
    }

    [LinuxOnlyFact]
    [SupportedOSPlatform("linux")]
    public void Store_Empty_Throws()
    {
        var store = new SecretToolCredentialStore(new UserConfig());
        Assert.Throws<ArgumentException>(() => store.Store(string.Empty));
    }

    [LinuxOnlyFact]
    [SupportedOSPlatform("linux")]
    public void Store_Whitespace_Throws()
    {
        var config = new UserConfig();
        var store = new SecretToolCredentialStore(config);

        var ex = Assert.Throws<ArgumentException>(() => store.Store("   "));
        Assert.Contains("whitespace", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(config.ApiKey);
    }

    // Daemon-required tests — kept as scaffolding for local runs.
    [Fact(Skip = DaemonSkipReason)] public void Store_AddsToKeyring_AndSetsProvider() { }
    [Fact(Skip = DaemonSkipReason)] public void Retrieve_RoundTrip() { }
    [Fact(Skip = DaemonSkipReason)] public void Retrieve_ReturnsNull_WhenNotPresent() { }
    [Fact(Skip = DaemonSkipReason)] public void Delete_RemovesFromKeyring() { }
    [Fact(Skip = DaemonSkipReason)] public void Delete_Idempotent() { }
    [Fact(Skip = DaemonSkipReason)] public void Cache_ReturnsQuickly_OnSecondRetrieve() { }
    [Fact(Skip = DaemonSkipReason)] public void ExceptionMessage_DoesNotContainKey() { }
}

// ── CredentialStoreFactory — Linux branch detection ────────────────
//
// We can't easily test the positive branch (returns SecretToolCredentialStore)
// on CI because /usr/bin/secret-tool isn't installed. But we can verify the
// negative branch: no DBus → plaintext fallback even on Linux.

[Collection("UserConfigFile")]
public sealed class CredentialStoreFactoryLinuxTests : CredentialStoreTestBase
{
    [LinuxOnlyFact]
    public void NoDbus_FallsBackToPlaintext_OnLinux()
    {
        // Strip DBUS_SESSION_BUS_ADDRESS for the scope of this test so the
        // factory's Linux branch rejects libsecret regardless of whether
        // secret-tool happens to be installed.
        string? original = Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS");
        try
        {
            Environment.SetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS", null);
            var store = CredentialStoreFactory.Create(new UserConfig());
            Assert.IsType<PlaintextCredentialStore>(store);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS", original);
        }
    }

    [Fact]
    public void NotLinux_DoesNotReturnSecretToolStore()
    {
        // Covered cross-platform: on Windows/macOS the factory never picks
        // the libsecret branch even with DBUS_SESSION_BUS_ADDRESS set.
        if (OperatingSystem.IsLinux())
        {
            return; // tautological on Linux; the guard above covers it.
        }

        string? original = Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS");
        try
        {
            Environment.SetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS", "unix:path=/tmp/fake");
            var store = CredentialStoreFactory.Create(new UserConfig());
            Assert.False(store.GetType().Name.Contains("SecretTool", StringComparison.Ordinal),
                $"Expected non-libsecret store on this OS, got {store.GetType().Name}");
        }
        finally
        {
            Environment.SetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS", original);
        }
    }
}
