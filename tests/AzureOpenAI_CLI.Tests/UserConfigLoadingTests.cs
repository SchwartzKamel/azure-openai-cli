using System.Runtime.InteropServices;

namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// Exercises UserConfig file selection and persistence boundaries with isolated
/// home/current-directory fixtures. These complement CRUD tests by checking
/// candidate precedence and recovery behavior across multiple candidate files.
/// </summary>
[Collection("ConsoleCapture")]
public sealed class UserConfigLoadingTests
{
    public static TheoryData<string, string> LoadCases => new()
    {
        { "explicit path overrides local and home", "explicit" },
        { "local path overrides home", "local" },
        { "malformed local falls back to home", "home" },
        { "empty local falls back to home", "home" },
        { "home is used when local is absent", "home" },
        { "no candidates returns defaults", "<defaults>" },
        { "missing explicit path does not fall back", "<defaults>" },
        { "malformed explicit path does not fall back", "<defaults>" },
        { "empty explicit path does not fall back", "<defaults>" },
    };

    [Theory]
    [MemberData(nameof(LoadCases))]
    public void Load_UsesExpectedCandidateAndFallback(string scenario, string expected)
    {
        using var home = new TempHome();
        using var cwd = new TempCwd();

        var localPath = Path.Combine(Directory.GetCurrentDirectory(), UserConfig.ConfigFileName);
        var homePath = UserConfig.DefaultPath;
        var explicitPath = Path.Combine(Directory.GetCurrentDirectory(), "explicit.json");
        WriteConfig(homePath, "home");

        string? pathArgument = null;
        switch (scenario)
        {
            case "explicit path overrides local and home":
                WriteConfig(localPath, "local");
                WriteConfig(explicitPath, "explicit");
                pathArgument = explicitPath;
                break;
            case "local path overrides home":
                WriteConfig(localPath, "local");
                break;
            case "malformed local falls back to home":
                File.WriteAllText(localPath, "{ malformed");
                break;
            case "empty local falls back to home":
                File.WriteAllText(localPath, string.Empty);
                break;
            case "home is used when local is absent":
                break;
            case "no candidates returns defaults":
                File.Delete(homePath);
                break;
            case "missing explicit path does not fall back":
                WriteConfig(localPath, "local");
                pathArgument = explicitPath;
                break;
            case "malformed explicit path does not fall back":
                WriteConfig(localPath, "local");
                File.WriteAllText(explicitPath, "{ malformed");
                pathArgument = explicitPath;
                break;
            case "empty explicit path does not fall back":
                WriteConfig(localPath, "local");
                File.WriteAllText(explicitPath, string.Empty);
                pathArgument = explicitPath;
                break;
            default:
                throw new InvalidOperationException($"Unknown test scenario: {scenario}");
        }

        var config = UserConfig.Load(pathArgument, quiet: true);

        if (expected == "<defaults>")
        {
            Assert.Null(config.LoadedFrom);
            Assert.Null(config.DefaultModel);
            Assert.Empty(config.Models);
        }
        else
        {
            var expectedPath = expected switch
            {
                "explicit" => explicitPath,
                "local" => localPath,
                _ => homePath,
            };
            Assert.Equal(expected, config.DefaultModel);
            Assert.Equal(expectedPath, config.LoadedFrom);
        }
    }

    [Theory]
    [InlineData("explicit", "explicit")]
    [InlineData("loaded", "loaded")]
    [InlineData("default", "default")]
    public void Save_UsesExpectedDestinationAndUpdatesLoadedFrom(string destination, string expected)
    {
        using var home = new TempHome();
        using var cwd = new TempCwd();

        var explicitPath = Path.Combine(Directory.GetCurrentDirectory(), "chosen.json");
        var loadedPath = Path.Combine(Directory.GetCurrentDirectory(), "loaded.json");
        var defaultPath = UserConfig.DefaultPath;
        var config = new UserConfig
        {
            DefaultModel = "fast",
            LoadedFrom = destination == "loaded" ? loadedPath : null,
        };
        config.Models["fast"] = "deployment-fast";

        config.Save(destination == "explicit" ? explicitPath : null);

        var expectedPath = expected switch
        {
            "explicit" => explicitPath,
            "loaded" => loadedPath,
            _ => defaultPath,
        };
        Assert.Equal(expectedPath, config.LoadedFrom);
        Assert.True(File.Exists(expectedPath));
        Assert.False(File.Exists(expectedPath == explicitPath ? loadedPath : explicitPath));

        var roundTrip = UserConfig.Load(expectedPath, quiet: true);
        Assert.Equal("fast", roundTrip.DefaultModel);
        Assert.Equal("deployment-fast", roundTrip.Models["fast"]);
    }

    [Fact]
    public void Save_CreatesConfigWithOwnerOnlyPermissionsOnUnix()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        using var temp = new TempCwd();
        var path = Path.Combine(Directory.GetCurrentDirectory(), "private-config.json");
        var config = new UserConfig { DefaultModel = "private" };

        config.Save(path);

        Assert.True(File.Exists(path));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
        Assert.Equal(path, config.LoadedFrom);
    }

    private static void WriteConfig(string path, string model)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            default_model = model,
            models = new Dictionary<string, string> { [model] = $"deployment-{model}" },
        });
        File.WriteAllText(path, json);
    }
}
