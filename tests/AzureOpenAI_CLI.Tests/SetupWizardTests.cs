using System.IO;
using AzureOpenAI_CLI;

namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// Tests for the first-run setup wizard: CLI flag plumbing, UserConfig
/// round-trip of the new credential fields, and redaction of the API key
/// in <see cref="UserConfig.ListKeys"/>.
/// <para>
/// Newman audit H-2 added Console-capturing tests to this class; the
/// whole class joins the <c>ConsoleCapture</c> collection so capture
/// can't interleave with other capturing suites.
/// </para>
/// </summary>
[Collection("ConsoleCapture")]
public class SetupWizardTests
{
    [Fact]
    public void ParseArgs_SetupFlag_IsRecognized()
    {
        var opts = Program.ParseArgs(["--setup"]);

        Assert.True(opts.Setup);
        Assert.False(opts.ParseError);
    }

    [Fact]
    public void ParseArgs_InitWizardAlias_IsRecognized()
    {
        var opts = Program.ParseArgs(["--init-wizard"]);

        Assert.True(opts.Setup);
        Assert.False(opts.ParseError);
    }

    [Fact]
    public void ParseArgs_NoSetupFlag_DefaultsToFalse()
    {
        var opts = Program.ParseArgs([]);
        Assert.False(opts.Setup);
    }

    [Fact]
    public void UserConfig_EndpointAndApiKey_RoundTrip()
    {
        var path = Path.Combine(Path.GetTempPath(), $"az-ai-wizard-rt-{Guid.NewGuid():N}.json");
        try
        {
            var cfg = new UserConfig
            {
                Endpoint = "https://example.openai.azure.com",
                ApiKey = "sk-test-0123456789abcdef-0123456789abcdef",
                DefaultModel = "default",
            };
            cfg.Models["default"] = "gpt-4o-mini";
            cfg.Save(path);

            var loaded = UserConfig.Load(path);

            Assert.Equal("https://example.openai.azure.com", loaded.Endpoint);
            Assert.Equal("sk-test-0123456789abcdef-0123456789abcdef", loaded.ApiKey);
            Assert.Equal("default", loaded.DefaultModel);
            Assert.Equal("gpt-4o-mini", loaded.Models["default"]);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void UserConfig_ListKeys_RedactsApiKey()
    {
        var cfg = new UserConfig
        {
            Endpoint = "https://example.openai.azure.com",
            ApiKey = "this-should-never-appear-in-output",
            DefaultModel = "default",
        };
        cfg.Models["default"] = "gpt-4o-mini";

        var keys = cfg.ListKeys().ToList();

        Assert.Contains("endpoint=https://example.openai.azure.com", keys);
        Assert.Contains("api_key=<redacted>", keys);
        Assert.DoesNotContain(keys, line => line.Contains("this-should-never-appear"));
    }

    [Fact]
    public void UserConfig_SetKey_AcceptsEndpointAndApiKey()
    {
        var cfg = new UserConfig();

        Assert.True(cfg.SetKey("endpoint", "https://example.openai.azure.com"));
        Assert.True(cfg.SetKey("api_key", "abcd1234"));

        Assert.Equal("https://example.openai.azure.com", cfg.Endpoint);
        Assert.Equal("abcd1234", cfg.ApiKey);
    }

    [Fact]
    public void UserConfig_GetKey_ReturnsEndpointAndApiKey()
    {
        var cfg = new UserConfig
        {
            Endpoint = "https://example.openai.azure.com",
            ApiKey = "topsecret",
        };

        Assert.Equal("https://example.openai.azure.com", cfg.GetKey("endpoint"));
        Assert.Equal("topsecret", cfg.GetKey("api_key"));
    }

    // ── Newman audit H-1: ReadMaskedLine fail-closed regression guard ────
    //
    // ReadMaskedLine's InvalidOperationException catch must NOT fall back
    // to Console.ReadLine() — that would echo the secret in plaintext on
    // pseudo-TTYs that pass the redirect check but lack a real console.
    // Forcing the exception in xUnit is hard (we can't easily fake
    // Console.ReadKey), so this is a static-analysis-style regression
    // guard: read the source file and assert Console.ReadLine does NOT
    // appear inside the body of ReadMaskedLine. Ugly, but it's a true
    // regression guard.
    [Fact]
    public void SetupWizard_ReadMaskedLine_DoesNotFallBackToReadLine()
    {
        var sourcePath = FindSourceFile("SetupWizard.cs");
        Assert.True(File.Exists(sourcePath), $"source file missing: {sourcePath}");
        var src = File.ReadAllText(sourcePath);

        // Locate the ReadMaskedLine method body.
        var sigIdx = src.IndexOf("private static string? ReadMaskedLine()", StringComparison.Ordinal);
        Assert.True(sigIdx > 0, "ReadMaskedLine signature not found in SetupWizard.cs");

        // Find the opening brace, then walk the brace-depth to find the close.
        var bodyStart = src.IndexOf('{', sigIdx);
        Assert.True(bodyStart > 0, "ReadMaskedLine opening brace not found");
        int depth = 0;
        int bodyEnd = -1;
        for (int i = bodyStart; i < src.Length; i++)
        {
            if (src[i] == '{') depth++;
            else if (src[i] == '}') { depth--; if (depth == 0) { bodyEnd = i; break; } }
        }
        Assert.True(bodyEnd > bodyStart, "ReadMaskedLine closing brace not found");

        var body = src.Substring(bodyStart, bodyEnd - bodyStart + 1);

        // Strip // line comments before scanning — comments referencing
        // the unmasked ReadLine API by name are fine (and useful), what
        // we're guarding against is an actual call. We don't bother with
        // /* */ block comments because the codebase doesn't use them in
        // method bodies.
        var stripped = System.Text.RegularExpressions.Regex.Replace(
            body, @"//[^\r\n]*", string.Empty);
        Assert.DoesNotContain("Console.ReadLine", stripped);

        // And the failure path must emit a stderr warning + return null
        // (the fail-closed contract). Anchor on the [ERROR] substring so a
        // future copy-edit doesn't have to chase the full sentence.
        Assert.Contains("Console.Error.WriteLine", body);
        Assert.Contains("[ERROR]", body);
    }

    // ── Newman audit H-2: --config get api_key refuses to print ──────────
    //
    // Exercises Program.HandleConfigSubcommand with the parsed opts and a
    // config that has an api_key set. Verifies the runtime path refuses
    // (exit 1) and never prints the raw key to stdout. Mirrors the style
    // of UserConfig_ListKeys_RedactsApiKey.
    [Fact]
    [Trait("Category", "ConsoleCapture")]
    public void ConfigGet_ApiKey_RefusesAndDoesNotPrintSecret()
    {
        var opts = Program.ParseArgs(["--config", "get", "api_key"]);
        Assert.Equal("get", opts.ConfigSubcommand);
        Assert.Equal("api_key", opts.ConfigKey);
        Assert.False(opts.ParseError);

        var cfg = new UserConfig
        {
            Endpoint = "https://example.openai.azure.com",
            ApiKey = "this-secret-must-never-appear-in-stdout",
        };

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var origOut = Console.Out;
        var origErr = Console.Error;
        int rc;
        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            rc = Program.HandleConfigSubcommand(opts, cfg);
        }
        finally
        {
            Console.SetOut(origOut);
            Console.SetError(origErr);
        }

        Assert.Equal(1, rc);
        var outText = stdout.ToString();
        var errText = stderr.ToString();

        // Hard invariant: the secret never appears on stdout OR stderr.
        Assert.DoesNotContain("this-secret-must-never-appear-in-stdout", outText);
        Assert.DoesNotContain("this-secret-must-never-appear-in-stdout", errText);

        // The refusal message lands on stderr (ErrorAndExit convention).
        Assert.Contains("Refusing to print api_key", errText);
        Assert.Contains("--setup", errText);
    }

    [Fact]
    [Trait("Category", "ConsoleCapture")]
    public void ConfigGet_ApiKey_JsonMode_RefusesWithStructuredError()
    {
        var opts = Program.ParseArgs(["--json", "--config", "get", "api_key"]);
        Assert.True(opts.Json);

        var cfg = new UserConfig
        {
            ApiKey = "this-secret-must-never-appear-in-stdout",
        };

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var origOut = Console.Out;
        var origErr = Console.Error;
        int rc;
        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            rc = Program.HandleConfigSubcommand(opts, cfg);
        }
        finally
        {
            Console.SetOut(origOut);
            Console.SetError(origErr);
        }

        Assert.Equal(1, rc);
        // Secret must not appear on either stream.
        Assert.DoesNotContain("this-secret-must-never-appear-in-stdout", stdout.ToString());
        Assert.DoesNotContain("this-secret-must-never-appear-in-stdout", stderr.ToString());
        // JSON-mode errors go to stderr (ErrorAndExit convention) — happy-path
        // stdout stays clean for `| jq` consumers.
        Assert.Contains("\"error\"", stderr.ToString());
    }

    [Fact]
    public void ConfigGet_NonSecretKey_StillReturnsValue()
    {
        // Sanity: redaction is api_key-specific, not blanket. endpoint and
        // default_model still print as before.
        var opts = Program.ParseArgs(["--config", "get", "endpoint"]);
        var cfg = new UserConfig { Endpoint = "https://example.openai.azure.com" };

        var stdout = new StringWriter();
        var origOut = Console.Out;
        int rc;
        try
        {
            Console.SetOut(stdout);
            rc = Program.HandleConfigSubcommand(opts, cfg);
        }
        finally
        {
            Console.SetOut(origOut);
        }

        Assert.Equal(0, rc);
        Assert.Contains("https://example.openai.azure.com", stdout.ToString());
    }

    // ── PromptEndpoint URL validation ────────────────────────────────────
    //
    // TryParseEndpointUrl is internal so it can be exercised directly.
    // These tests guard against a persisted misconfiguration that silently
    // breaks the client SDK's URL construction at runtime.

    [Theory]
    [InlineData("https://my-resource.openai.azure.com")]
    [InlineData("https://my-resource.openai.azure.com/")]
    public void TryParseEndpointUrl_ValidRootUrl_ReturnsTrue(string url)
    {
        Assert.True(SetupWizard.TryParseEndpointUrl(url, out var rejection));
        Assert.Null(rejection);
    }

    [Theory]
    [InlineData("http://my-resource.openai.azure.com", "must start with https")]
    [InlineData("my-resource.openai.azure.com", "must start with https")]
    [InlineData("https://my-resource.openai.azure.com/openai/deployments", "no path")]
    [InlineData("https://my-resource.openai.azure.com/openai", "no path")]
    [InlineData("https://my-resource.openai.azure.com?api-version=2024-05-01", "no path, query, or fragment")]
    [InlineData("https://my-resource.openai.azure.com/#section", "no path, query, or fragment")]
    public void TryParseEndpointUrl_InvalidUrl_ReturnsFalseWithMessage(string url, string expectedFragment)
    {
        var ok = SetupWizard.TryParseEndpointUrl(url, out var rejection);

        Assert.False(ok);
        Assert.NotNull(rejection);
        Assert.Contains(expectedFragment, rejection, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindSourceFile(string leaf)
    {
        // Walk up from the test assembly location until we find the repo's
        // azureopenai-cli/ directory containing the requested file.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "azureopenai-cli", leaf);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return string.Empty;
    }
}
