using System.Text;
using AzureOpenAI_CLI;
using AzureOpenAI_CLI.Credentials;
using AzureOpenAI_CLI.Setup;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// Tests for <see cref="FirstRunWizard"/>. IO is injected via <see cref="StringReader"/>
/// / <see cref="StringWriter"/> so flows are fully deterministic.
///
/// Validation-ping caveat: the wizard always tries to reach the Azure OpenAI
/// endpoint. These tests use the <c>.invalid</c> reserved TLD (RFC 2606) so DNS
/// resolution fails immediately → the SDK throws a non-cancellation exception
/// → the wizard classifies the failure as
/// <c>ValidationFailure.Other</c> and prompts "Save creds anyway?". Tests feed
/// "y" to exercise the persistence path. Worst-case latency is a couple of
/// seconds per test (DNS NXDOMAIN + SDK pipeline).
///
/// The collection is shared with <see cref="UserConfigTests"/> because the
/// wizard calls <c>UserConfig.Save()</c>, which writes to
/// <c>~/.azureopenai-cli.json</c> — we must not race other file-writing tests.
/// </summary>
[Collection("UserConfigFile")]
public class FirstRunWizardTests : IDisposable
{
    private readonly string _configPath;
    private readonly string _backupPath;
    private readonly bool _hadExistingConfig;

    // A test-unique host under the never-resolvable .invalid TLD (RFC 2606).
    private const string UnreachableEndpoint = "https://az-ai-test-wizard.invalid/";
    private const string ValidKey = "valid-api-key-that-is-at-least-32-chars-long";
    private const string SentinelKey = "SENTINEL-TEST-KEY-VALUE-12345678901234";

    public FirstRunWizardTests()
    {
        _configPath = UserConfig.GetConfigPath();
        _backupPath = _configPath + ".wizard-test-backup";
        _hadExistingConfig = File.Exists(_configPath);
        if (_hadExistingConfig)
            File.Copy(_configPath, _backupPath, overwrite: true);
        if (File.Exists(_configPath))
            File.Delete(_configPath);
    }

    public void Dispose()
    {
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

    // ── Test double ─────────────────────────────────────────────────

    private sealed class FakeCredentialStore : ICredentialStore
    {
        public string? StoredKey { get; set; }
        public Exception? ThrowOnStore { get; set; }
        public string ProviderName => "fake";
        public void Store(string apiKey)
        {
            if (ThrowOnStore != null) throw ThrowOnStore;
            StoredKey = apiKey;
        }
        public string? Retrieve() => StoredKey;
        public void Delete() => StoredKey = null;
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private sealed record Harness(
        FirstRunWizard Wizard,
        UserConfig Config,
        FakeCredentialStore Store,
        StringWriter Output,
        StringWriter Error);

    private static Harness MakeWizard(string input)
    {
        var config = new UserConfig();
        var store = new FakeCredentialStore();
        var reader = new StringReader(input);
        var outw = new StringWriter();
        var errw = new StringWriter();
        var wizard = new FirstRunWizard(config, store, reader, outw, errw);
        return new Harness(wizard, config, store, outw, errw);
    }

    /// <summary>Builds a newline-joined input stream (with trailing newline).</summary>
    private static string Lines(params string[] lines) =>
        string.Join("\n", lines) + "\n";

    // ── Happy path + persistence ───────────────────────────────────

    [Fact]
    public async Task RunAsync_HappyPath_PersistsEndpointAndKeyAndFingerprint()
    {
        // endpoint, key, model, then "y" to Save-anyway after validation fails.
        var h = MakeWizard(Lines(UnreachableEndpoint, ValidKey, "gpt-4o", "y"));

        bool ok = await h.Wizard.RunAsync(CancellationToken.None);

        Assert.True(ok);
        Assert.Equal(ValidKey, h.Store.StoredKey);
        Assert.Equal(UnreachableEndpoint, h.Config.Endpoint);
        Assert.Contains("gpt-4o", h.Config.AvailableModels);
        Assert.Equal("gpt-4o", h.Config.ActiveModel);
        Assert.NotNull(h.Config.ApiKeyFingerprint);
        Assert.Equal(12, h.Config.ApiKeyFingerprint!.Length);
        Assert.Matches("^[0-9a-f]{12}$", h.Config.ApiKeyFingerprint);
        Assert.Equal("fake", h.Config.ApiKeyProvider);
    }

    [Fact]
    public async Task RunAsync_FingerprintMatchesKey()
    {
        var h = MakeWizard(Lines(UnreachableEndpoint, ValidKey, "gpt-4o", "y"));

        bool ok = await h.Wizard.RunAsync(CancellationToken.None);

        Assert.True(ok);
        Assert.Equal(UserConfig.ComputeFingerprint(ValidKey), h.Config.ApiKeyFingerprint);
    }

    // ── Endpoint validation ────────────────────────────────────────

    [Fact]
    public async Task RunAsync_RejectsHttpEndpoint_RetriesUntilValid()
    {
        var h = MakeWizard(Lines("http://foo", UnreachableEndpoint, ValidKey, "gpt-4o", "y"));

        bool ok = await h.Wizard.RunAsync(CancellationToken.None);

        Assert.True(ok);
        Assert.Contains("https://", h.Error.ToString()); // error mentions https
        Assert.Equal(UnreachableEndpoint, h.Config.Endpoint);
    }

    [Fact]
    public async Task RunAsync_RejectsEmptyEndpoint_Retries()
    {
        // Up to 3 endpoint attempts; first is empty, second valid.
        var h = MakeWizard(Lines("", UnreachableEndpoint, ValidKey, "gpt-4o", "y"));

        bool ok = await h.Wizard.RunAsync(CancellationToken.None);

        Assert.True(ok);
        Assert.Equal(UnreachableEndpoint, h.Config.Endpoint);
    }

    [Fact]
    public async Task RunAsync_RejectsInvalidUri_Retries()
    {
        var h = MakeWizard(Lines("not a url", UnreachableEndpoint, ValidKey, "gpt-4o", "y"));

        bool ok = await h.Wizard.RunAsync(CancellationToken.None);

        Assert.True(ok);
        Assert.Equal(UnreachableEndpoint, h.Config.Endpoint);
    }

    [Fact]
    public async Task RunAsync_EndpointRetriesExhausted_ReturnsFalse()
    {
        // 3 consecutive invalid endpoints — MaxEndpointAttempts is 3 — aborts.
        var h = MakeWizard(Lines("http://a", "not a url", "ftp://b"));

        bool ok = await h.Wizard.RunAsync(CancellationToken.None);

        Assert.False(ok);
        Assert.Null(h.Store.StoredKey);
        Assert.Null(h.Config.Endpoint);
        Assert.Contains("Too many invalid endpoints", h.Error.ToString());
    }

    // ── Model validation ───────────────────────────────────────────

    [Fact]
    public async Task RunAsync_EmptyModel_RepromptsUntilValid()
    {
        // empty model line (only whitespace/commas) → reprompt; then gpt-4o accepted.
        var h = MakeWizard(Lines(UnreachableEndpoint, ValidKey, "", "gpt-4o", "y"));

        bool ok = await h.Wizard.RunAsync(CancellationToken.None);

        Assert.True(ok);
        Assert.Equal("gpt-4o", h.Config.ActiveModel);
        Assert.Contains("At least one model", h.Error.ToString());
    }

    [Fact]
    public async Task RunAsync_CommaSeparatedModels_AllStoredTrimmed()
    {
        var h = MakeWizard(Lines(UnreachableEndpoint, ValidKey, "gpt-4o, gpt-4o-mini, gpt-35", "y"));

        bool ok = await h.Wizard.RunAsync(CancellationToken.None);

        Assert.True(ok);
        Assert.Equal(new[] { "gpt-4o", "gpt-4o-mini", "gpt-35" }, h.Config.AvailableModels);
        Assert.Equal("gpt-4o", h.Config.ActiveModel);
    }

    // ── Short-key handling ─────────────────────────────────────────

    [Fact]
    public async Task RunAsync_ShortKey_WarnsButAccepts_WhenUserConfirms()
    {
        const string shortKey = "short-key-20-charsx"; // < 32
        Assert.True(shortKey.Length < 32);

        // After short-key warning: "Use it anyway? [y/N]" → y.
        var h = MakeWizard(Lines(UnreachableEndpoint, shortKey, "y", "gpt-4o", "y"));

        bool ok = await h.Wizard.RunAsync(CancellationToken.None);

        Assert.True(ok);
        Assert.Equal(shortKey, h.Store.StoredKey);
        Assert.Contains("shorter than expected", h.Error.ToString());
    }

    [Fact]
    public async Task RunAsync_ShortKey_Declined_RepromptsForKey()
    {
        const string shortKey = "short-10ch";
        var h = MakeWizard(Lines(
            UnreachableEndpoint,
            shortKey, "n",       // decline short key → reprompt
            ValidKey,            // 32+ chars → accepted (no confirmation needed)
            "gpt-4o",
            "y"));               // save anyway after validation fails

        bool ok = await h.Wizard.RunAsync(CancellationToken.None);

        Assert.True(ok);
        Assert.Equal(ValidKey, h.Store.StoredKey);
    }

    // ── Cancellation ───────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_Cancelled_NoFileWriteNoStore()
    {
        var h = MakeWizard(Lines(UnreachableEndpoint, ValidKey, "gpt-4o", "y"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        bool ok = await h.Wizard.RunAsync(cts.Token);

        Assert.False(ok);
        Assert.Null(h.Store.StoredKey);
        Assert.Null(h.Config.Endpoint);
        Assert.Null(h.Config.ApiKeyFingerprint);
        Assert.Empty(h.Config.AvailableModels);
    }

    // ── Secret hygiene ─────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_DoesNotPrintRawKey_AnywhereInOutput()
    {
        Assert.True(SentinelKey.Length >= 32, "sentinel must avoid the short-key path");
        var h = MakeWizard(Lines(UnreachableEndpoint, SentinelKey, "gpt-4o", "y"));

        bool ok = await h.Wizard.RunAsync(CancellationToken.None);

        Assert.True(ok);
        Assert.Equal(SentinelKey, h.Store.StoredKey);

        string combined = h.Output.ToString() + h.Error.ToString();
        Assert.DoesNotContain(SentinelKey, combined, StringComparison.Ordinal);
    }

    // ── Argument validation ────────────────────────────────────────

    [Fact]
    public void Ctor_NullConfig_Throws()
    {
        var store = new FakeCredentialStore();
        Assert.Throws<ArgumentNullException>(() =>
            new FirstRunWizard(null!, store, TextReader.Null, new StringWriter(), new StringWriter()));
    }

    [Fact]
    public void Ctor_NullStore_Throws()
    {
        var config = new UserConfig();
        Assert.Throws<ArgumentNullException>(() =>
            new FirstRunWizard(config, null!, TextReader.Null, new StringWriter(), new StringWriter()));
    }
}
