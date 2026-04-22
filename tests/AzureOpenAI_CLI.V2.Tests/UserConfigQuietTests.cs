using System.Reflection;

namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// FDR v2 dogfood High-severity (fdr-v2-raw-config-warning) tests:
/// a malformed <c>~/.azureopenai-cli.json</c> must NOT emit a [WARNING]
/// on stderr when the caller set <c>--raw</c>. Espanso / AHK consumers
/// pipe stderr-clean; leaking diagnostics breaks their contract.
/// </summary>
[Collection("ConsoleCapture")]
public class UserConfigQuietTests
{
    [Fact]
    public void Load_Quiet_MalformedJson_NoStderrOutput()
    {
        var dir = Path.Combine(Path.GetTempPath(), "uc-quiet-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "bad.json");
        File.WriteAllText(path, "{ this is : not json");

        var origErr = Console.Error;
        var errCap = new StringWriter();
        Console.SetError(errCap);
        try
        {
            var cfg = UserConfig.Load(path, quiet: true);

            Assert.NotNull(cfg);
            Assert.Equal(string.Empty, errCap.ToString());
        }
        finally
        {
            Console.SetError(origErr);
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Load_NotQuiet_MalformedJson_EmitsWarning()
    {
        // Negative path: default (quiet=false) MUST emit the warning so
        // interactive users still see their typo.
        var dir = Path.Combine(Path.GetTempPath(), "uc-quiet-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "bad.json");
        File.WriteAllText(path, "{ this is : not json");

        var origErr = Console.Error;
        var errCap = new StringWriter();
        Console.SetError(errCap);
        try
        {
            var cfg = UserConfig.Load(path, quiet: false);

            Assert.NotNull(cfg);
            var err = errCap.ToString();
            Assert.Contains("[WARNING]", err);
            Assert.Contains("invalid JSON", err);
        }
        finally
        {
            Console.SetError(origErr);
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Load_Quiet_ValidJson_ReturnsParsedConfig()
    {
        // Happy-path: quiet mode must still return a correctly parsed config
        // when the JSON is well-formed — the silence is only about warnings.
        var dir = Path.Combine(Path.GetTempPath(), "uc-quiet-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "good.json");
        File.WriteAllText(path, """{"default_model":"fast","models":{"fast":"gpt-4o-mini"}}""");

        var origErr = Console.Error;
        var errCap = new StringWriter();
        Console.SetError(errCap);
        try
        {
            var cfg = UserConfig.Load(path, quiet: true);

            Assert.NotNull(cfg);
            Assert.Equal("fast", cfg.DefaultModel);
            Assert.Equal(string.Empty, errCap.ToString());
        }
        finally
        {
            Console.SetError(origErr);
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task MainWithRawAndMalformedConfig_StderrIsEmpty()
    {
        // End-to-end: invoke Program.Main with --raw against a malformed
        // project-local config; stderr must be empty. Uses --help as a
        // short-circuit so no API call fires, but UserConfig.Load() still
        // runs before the help branch. Actually --help short-circuits BEFORE
        // UserConfig.Load, so we use --current-model which runs after load.
        var dir = Path.Combine(Path.GetTempPath(), "uc-raw-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var origCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(dir);

        // Malformed project-local config (takes precedence over user-home).
        File.WriteAllText(Path.Combine(dir, ".azureopenai-cli.json"), "{ not: json");

        var origOut = Console.Out;
        var origErr = Console.Error;
        var outCap = new StringWriter();
        var errCap = new StringWriter();
        Console.SetOut(outCap);
        Console.SetError(errCap);
        try
        {
            var main = typeof(Program).GetMethod("Main",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;
            // --raw + --current-model: exercises UserConfig.Load under --raw.
            var result = main.Invoke(null, new object[] { new[] { "--raw", "--current-model" } });
            if (result is Task<int> t) await t;

            // Stderr must be absolutely empty under --raw, even with malformed config.
            Assert.Equal(string.Empty, errCap.ToString());
        }
        finally
        {
            Console.SetOut(origOut);
            Console.SetError(origErr);
            Directory.SetCurrentDirectory(origCwd);
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task MainWithoutRawAndMalformedConfig_StderrHasWarning()
    {
        // Negative counterpart: without --raw, the warning MUST surface.
        var dir = Path.Combine(Path.GetTempPath(), "uc-raw-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var origCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(dir);

        File.WriteAllText(Path.Combine(dir, ".azureopenai-cli.json"), "{ not: json");

        var origOut = Console.Out;
        var origErr = Console.Error;
        var outCap = new StringWriter();
        var errCap = new StringWriter();
        Console.SetOut(outCap);
        Console.SetError(errCap);
        try
        {
            var main = typeof(Program).GetMethod("Main",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;
            var result = main.Invoke(null, new object[] { new[] { "--current-model" } });
            if (result is Task<int> t) await t;

            Assert.Contains("[WARNING]", errCap.ToString());
        }
        finally
        {
            Console.SetOut(origOut);
            Console.SetError(origErr);
            Directory.SetCurrentDirectory(origCwd);
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }
}
