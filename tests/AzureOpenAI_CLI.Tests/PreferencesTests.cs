using System.Text.Json;

namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// FR-014 / S03E06 -- preferences.json v1 schema tests. Pass the pass,
/// FAIL the fail: every positive round-trip is paired with a negative
/// (missing file, malformed JSON, empty dictionaries, permission bits).
///
/// Joins the "ConsoleCapture" collection (DisableParallelization=true)
/// because the --config show subtests mutate AZUREOPENAIENDPOINT /
/// AZUREOPENAIMODEL / AZ_PROFILE / AZ_PROVIDER / XDG_CONFIG_HOME and
/// capture Console.Out. Without serialization, ProviderDispatchTests and
/// any other parallel suite that touches the same env vars races the
/// resolver mid-test (Newman flake report, S03E07).
/// </summary>
[Collection("ConsoleCapture")]
public class PreferencesTests
{
    private static string NewTempPath(string label)
    {
        var dir = Path.Combine(Path.GetTempPath(), "prefs-" + label + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "preferences.json");
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaultsNoThrow()
    {
        var path = NewTempPath("missing");
        Assert.False(File.Exists(path));

        var prefs = Preferences.Load(path);

        Assert.NotNull(prefs);
        Assert.Equal("1", prefs.Schema);
        Assert.Empty(prefs.Providers);
        Assert.Empty(prefs.Profiles);
        Assert.Null(prefs.LoadedFrom);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsSchemaVersion()
    {
        var path = NewTempPath("schema");
        try
        {
            var prefs = new Preferences { Schema = "1" };
            Preferences.Save(path, prefs);

            var loaded = Preferences.Load(path);
            Assert.Equal("1", loaded.Schema);
            Assert.Equal(path, loaded.LoadedFrom);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Save_EmptyDictionaries_SerializeAsObjectsNotNull()
    {
        var path = NewTempPath("empty-dicts");
        try
        {
            var prefs = new Preferences();
            Preferences.Save(path, prefs);

            var raw = File.ReadAllText(path);
            // Negative path: JSON must contain "providers": {} and "profiles": {},
            // not null. A null here would make consumers crash on key lookup.
            Assert.Contains("\"providers\": {}", raw, StringComparison.Ordinal);
            Assert.Contains("\"profiles\": {}", raw, StringComparison.Ordinal);
            Assert.DoesNotContain("\"providers\": null", raw, StringComparison.Ordinal);
            Assert.DoesNotContain("\"profiles\": null", raw, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsPopulatedPreferences()
    {
        var path = NewTempPath("populated");
        try
        {
            var prefs = new Preferences
            {
                Schema = "1",
                Providers = new Dictionary<string, ProviderEntry>(StringComparer.Ordinal)
                {
                    ["azure"] = new() { Endpoint = "https://x.openai.azure.com/", ModelAlias = "gpt-4o-mini", Notes = "primary" },
                    ["openai"] = new() { Endpoint = "https://api.openai.com/v1", ModelAlias = "gpt-4o-mini" },
                },
                Profiles = new Dictionary<string, ProfileEntry>(StringComparer.Ordinal)
                {
                    ["default"] = new() { Provider = "azure", Model = "gpt-4o-mini" },
                    ["work"] = new() { Provider = "azure", Model = "gpt-4o", Notes = "low-temp" },
                    ["personal"] = new() { Provider = "openai" },
                },
            };
            Preferences.Save(path, prefs);

            var loaded = Preferences.Load(path);

            Assert.Equal(2, loaded.Providers.Count);
            Assert.Equal(3, loaded.Profiles.Count);
            Assert.Equal("https://x.openai.azure.com/", loaded.Providers["azure"].Endpoint);
            Assert.Equal("gpt-4o-mini", loaded.Providers["azure"].ModelAlias);
            Assert.Equal("primary", loaded.Providers["azure"].Notes);
            Assert.Equal("openai", loaded.Profiles["personal"].Provider);
            Assert.Null(loaded.Profiles["personal"].Model);
            Assert.Equal("low-temp", loaded.Profiles["work"].Notes);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Save_NeverContainsApiKeyField()
    {
        // Negative invariant: the schema MUST NOT carry secret material.
        // A future contributor adding `apiKey` to ProviderEntry trips this.
        var path = NewTempPath("nosecret");
        try
        {
            var prefs = new Preferences
            {
                Providers = { ["azure"] = new() { Endpoint = "https://x.example.com/", ModelAlias = "gpt-4o-mini" } },
            };
            Preferences.Save(path, prefs);
            var raw = File.ReadAllText(path);
            Assert.DoesNotContain("apiKey", raw, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("api_key", raw, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret", raw, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_MalformedJson_ThrowsInvalidPreferencesException()
    {
        var path = NewTempPath("bad");
        try
        {
            File.WriteAllText(path, "{ this is : not json");
            var ex = Assert.Throws<InvalidPreferencesException>(() => Preferences.Load(path));
            Assert.Equal(path, ex.Path);
            Assert.Contains("Malformed JSON", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_EmptyFile_ReturnsDefaults()
    {
        var path = NewTempPath("empty-file");
        try
        {
            File.WriteAllText(path, "");
            var prefs = Preferences.Load(path);
            Assert.Equal("1", prefs.Schema);
            Assert.Empty(prefs.Providers);
            Assert.Empty(prefs.Profiles);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Save_OnUnix_SetsMode0600()
    {
        if (OperatingSystem.IsWindows())
        {
            // Windows ACLs are not POSIX-mode; covered by the round-trip tests.
            return;
        }

        var path = NewTempPath("perms");
        try
        {
            var prefs = new Preferences();
            Preferences.Save(path, prefs);

            var mode = File.GetUnixFileMode(path);
            var expected = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            Assert.Equal(expected, mode);

            // Negative: must NOT be group/other readable.
            Assert.Equal((UnixFileMode)0, mode & UnixFileMode.GroupRead);
            Assert.Equal((UnixFileMode)0, mode & UnixFileMode.OtherRead);
            Assert.Equal((UnixFileMode)0, mode & UnixFileMode.GroupWrite);
            Assert.Equal((UnixFileMode)0, mode & UnixFileMode.OtherWrite);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Save_CreatesMissingParentDirectories()
    {
        var dir = Path.Combine(Path.GetTempPath(), "prefs-mkdir-" + Guid.NewGuid().ToString("N"));
        var nested = Path.Combine(dir, "deep", "nest", "preferences.json");
        try
        {
            Assert.False(Directory.Exists(Path.GetDirectoryName(nested)!));
            Preferences.Save(nested, new Preferences());
            Assert.True(File.Exists(nested));
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void DefaultPath_HasExpectedShape()
    {
        var path = Preferences.DefaultPath();
        Assert.EndsWith("preferences.json", path, StringComparison.Ordinal);
        Assert.Contains("az-ai", path, StringComparison.Ordinal);

        if (!OperatingSystem.IsWindows())
        {
            // XDG convention: under .config/ or $XDG_CONFIG_HOME/.
            Assert.Contains("config", path, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void DefaultPath_RespectsXdgConfigHome_OnUnix()
    {
        if (OperatingSystem.IsWindows()) return;

        var orig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var tmp = Path.Combine(Path.GetTempPath(), "xdg-" + Guid.NewGuid().ToString("N"));
        try
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", tmp);
            var path = Preferences.DefaultPath();
            Assert.StartsWith(tmp, path, StringComparison.Ordinal);
            Assert.EndsWith(Path.Combine("az-ai", "preferences.json"), path, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", orig);
        }
    }

    [Fact]
    public void Json_RoundTripsThroughSourceGenerator()
    {
        // AOT invariant: serialisation must go through AppJsonContext, never
        // reflection. This test exercises that path explicitly.
        var prefs = new Preferences
        {
            Providers = { ["azure"] = new() { Endpoint = "https://x.example.com/" } },
            Profiles = { ["default"] = new() { Provider = "azure", Model = "gpt-4o-mini" } },
        };
        var json = JsonSerializer.Serialize(prefs, AppJsonContext.Default.Preferences);
        Assert.Contains("\"schema\": \"1\"", json, StringComparison.Ordinal);

        var roundTripped = JsonSerializer.Deserialize(json, AppJsonContext.Default.Preferences);
        Assert.NotNull(roundTripped);
        Assert.Equal("azure", roundTripped!.Profiles["default"].Provider);
        Assert.Equal("gpt-4o-mini", roundTripped.Profiles["default"].Model);
    }

    // ── --config show integration (FR-014 / S03E06) ─────────────────────

    private sealed class EnvScope : IDisposable
    {
        private readonly Dictionary<string, string?> _backup = new(StringComparer.Ordinal);

        public void Set(string name, string? value)
        {
            if (!_backup.ContainsKey(name))
                _backup[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            foreach (var kv in _backup)
                Environment.SetEnvironmentVariable(kv.Key, kv.Value);
        }
    }

    [Fact]
    public void ConfigShow_Json_EmitsResolvedLayersAndPaths()
    {
        using var home = new TempHome();
        using var env = new EnvScope();
        // Pin XDG so DefaultPath() lands inside the temp home, not the host.
        env.Set("XDG_CONFIG_HOME", Path.Combine(
            Environment.GetEnvironmentVariable("HOME")!, ".config"));
        env.Set("AZUREOPENAIENDPOINT", "https://test.openai.azure.com/");
        env.Set("AZUREOPENAIMODEL", "gpt-4o-mini,gpt-4o");
        env.Set("AZ_PROFILE", null);
        env.Set("AZ_PROVIDER", null);

        // Seed a preferences.json at the resolved DefaultPath.
        var prefsPath = Preferences.DefaultPath();
        Preferences.Save(prefsPath, new Preferences
        {
            Providers = { ["azure"] = new() { Endpoint = "https://x.example.com/", ModelAlias = "gpt-4o-mini" } },
            Profiles = { ["default"] = new() { Provider = "azure", Model = "gpt-4o" } },
        });

        using var stdout = new StringWriter();
        var oldOut = Console.Out;
        try
        {
            Console.SetOut(stdout);
            var opts = Program.ParseArgs(["--config", "show", "--json"]);
            int rc = Program.HandleConfigSubcommand(opts, UserConfig.Load());
            Assert.Equal(0, rc);
        }
        finally { Console.SetOut(oldOut); }

        var output = stdout.ToString();
        // Parse the JSON to assert on shape, not byte-for-byte.
        using var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("preferences_loaded").GetBoolean());
        Assert.Equal(prefsPath, root.GetProperty("preferences_path").GetString());

        var resolved = root.GetProperty("resolved");
        Assert.Equal("env AZUREOPENAIENDPOINT", resolved.GetProperty("endpoint").GetProperty("source").GetString());
        Assert.Equal("https://test.openai.azure.com/", resolved.GetProperty("endpoint").GetProperty("value").GetString());
        Assert.Equal("env AZUREOPENAIMODEL[0]", resolved.GetProperty("model").GetProperty("source").GetString());
        Assert.Equal("gpt-4o-mini", resolved.GetProperty("model").GetProperty("value").GetString());
        // Profile resolves from preferences.json since no AZ_PROFILE.
        Assert.Equal("default", resolved.GetProperty("profile").GetProperty("value").GetString());
        Assert.Contains("preferences.json", resolved.GetProperty("profile").GetProperty("source").GetString()!, StringComparison.Ordinal);

        // Negative path: secrets MUST NOT appear anywhere in the JSON.
        Assert.DoesNotContain("api", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConfigShow_NoPreferencesFile_StillSucceedsWithDefaults()
    {
        using var home = new TempHome();
        using var env = new EnvScope();
        env.Set("XDG_CONFIG_HOME", Path.Combine(
            Environment.GetEnvironmentVariable("HOME")!, ".config"));
        env.Set("AZUREOPENAIENDPOINT", null);
        env.Set("AZUREOPENAIMODEL", null);
        env.Set("AZ_PROFILE", null);
        env.Set("AZ_PROVIDER", null);

        using var stdout = new StringWriter();
        var oldOut = Console.Out;
        try
        {
            Console.SetOut(stdout);
            var opts = Program.ParseArgs(["--config", "show", "--json"]);
            int rc = Program.HandleConfigSubcommand(opts, new UserConfig());
            Assert.Equal(0, rc);
        }
        finally { Console.SetOut(oldOut); }

        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.False(doc.RootElement.GetProperty("preferences_loaded").GetBoolean());
        // Fallback chain lands on hardcoded ADR-009 model fallback.
        var modelSource = doc.RootElement
            .GetProperty("resolved").GetProperty("model").GetProperty("source").GetString();
        Assert.Contains("ADR-009", modelSource!, StringComparison.Ordinal);
        var providerSource = doc.RootElement
            .GetProperty("resolved").GetProperty("provider").GetProperty("source").GetString();
        Assert.Contains("hardcoded default", providerSource!, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigShow_TextOutput_KeepsLegacyEffectiveBlock()
    {
        // Regression: chaos suite asserts "# Effective configuration" header.
        using var home = new TempHome();
        using var env = new EnvScope();
        env.Set("XDG_CONFIG_HOME", Path.Combine(
            Environment.GetEnvironmentVariable("HOME")!, ".config"));
        env.Set("AZUREOPENAIENDPOINT", "https://test.openai.azure.com/");
        env.Set("AZUREOPENAIMODEL", "gpt-4o-mini");

        using var stdout = new StringWriter();
        var oldOut = Console.Out;
        try
        {
            Console.SetOut(stdout);
            var opts = Program.ParseArgs(["--config", "show"]);
            int rc = Program.HandleConfigSubcommand(opts, new UserConfig());
            Assert.Equal(0, rc);
        }
        finally { Console.SetOut(oldOut); }

        var text = stdout.ToString();
        Assert.StartsWith("# Effective configuration", text, StringComparison.Ordinal);
        Assert.Contains("Resolved configuration:", text, StringComparison.Ordinal);
        Assert.Contains("Preferences file:", text, StringComparison.Ordinal);
        // Negative: never print api key, even if present in UserConfig surface.
        Assert.DoesNotContain("api_key=sk-", text, StringComparison.Ordinal);
    }
}
