namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// Tests for Scope 1 (FR-008 cache flags), Scope 2 (JSON errors → stderr),
/// and Scope 3 (reject unknown flags). Named per the ticket wave.
/// </summary>
[Collection("ConsoleCapture")]
public class V2UxAndCacheWaveTests
{
    // ── Scope 1: cache flag parsing ─────────────────────────────────────

    [Fact]
    public void ParseArgs_Default_CacheDisabled()
    {
        var opts = Program.ParseArgs([]);
        Assert.False(opts.CacheEnabled);
        Assert.Equal(168, opts.CacheTtlHours); // 7 days default
    }

    [Fact]
    public void ParseArgs_CacheFlag_Enables()
    {
        var opts = Program.ParseArgs(["--cache", "hi"]);
        Assert.True(opts.CacheEnabled);
        Assert.Equal("hi", opts.Prompt);
    }

    [Fact]
    public void ParseArgs_CacheTtl_AcceptsInteger()
    {
        var opts = Program.ParseArgs(["--cache", "--cache-ttl", "48", "hi"]);
        Assert.True(opts.CacheEnabled);
        Assert.Equal(48, opts.CacheTtlHours);
    }

    [Theory]
    [InlineData("0")]       // must be positive
    [InlineData("-3")]
    [InlineData("junk")]
    public void ParseArgs_CacheTtl_RejectsInvalid(string raw)
    {
        var oldErr = Console.Error;
        using var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            var opts = Program.ParseArgs(["--cache-ttl", raw]);
            Assert.True(opts.ParseError);
        }
        finally { Console.SetError(oldErr); }
    }

    [Fact]
    public void ParseArgs_AzCacheEnvVar_EnablesCache()
    {
        Environment.SetEnvironmentVariable("AZ_CACHE", "1");
        try
        {
            var opts = Program.ParseArgs(["hi"]);
            Assert.True(opts.CacheEnabled);
        }
        finally { Environment.SetEnvironmentVariable("AZ_CACHE", null); }
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("true")]    // only strict "1" enables
    [InlineData("yes")]
    public void ParseArgs_AzCacheEnvVar_StrictOne_Negative(string envVal)
    {
        Environment.SetEnvironmentVariable("AZ_CACHE", envVal);
        try
        {
            var opts = Program.ParseArgs(["hi"]);
            Assert.False(opts.CacheEnabled);
        }
        finally { Environment.SetEnvironmentVariable("AZ_CACHE", null); }
    }

    [Fact]
    public void ParseArgs_AzCacheTtlEnv_AppliesWhenFlagAbsent()
    {
        Environment.SetEnvironmentVariable("AZ_CACHE_TTL_HOURS", "12");
        try
        {
            var opts = Program.ParseArgs(["--cache", "hi"]);
            Assert.Equal(12, opts.CacheTtlHours);
        }
        finally { Environment.SetEnvironmentVariable("AZ_CACHE_TTL_HOURS", null); }
    }

    [Fact]
    public void ParseArgs_CliCacheTtl_Beats_Env()
    {
        Environment.SetEnvironmentVariable("AZ_CACHE_TTL_HOURS", "12");
        try
        {
            var opts = Program.ParseArgs(["--cache", "--cache-ttl", "99", "hi"]);
            Assert.Equal(99, opts.CacheTtlHours);
        }
        finally { Environment.SetEnvironmentVariable("AZ_CACHE_TTL_HOURS", null); }
    }

    // ── Scope 3: reject unknown flags ───────────────────────────────────

    [Fact]
    public void ParseArgs_UnknownFlag_SetsParseErrorWithExit2()
    {
        var oldErr = Console.Error;
        using var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            var opts = Program.ParseArgs(["--nope"]);
            Assert.True(opts.ParseError);
            Assert.Equal(2, opts.ParseErrorExitCode);
            Assert.Equal("--nope", opts.UnknownFlag);
            Assert.False(opts.ShowHelp); // unknown flag does NOT spam help
            var stderr = sw.ToString();
            Assert.Contains("[ERROR] unknown flag: --nope", stderr);
            Assert.Contains("Run --help for usage.", stderr);
        }
        finally { Console.SetError(oldErr); }
    }

    [Fact]
    public void ParseArgs_UnknownFlag_JsonMode_EmitsStructuredErrorToStderr()
    {
        var oldErr = Console.Error;
        var oldOut = Console.Out;
        using var errSw = new StringWriter();
        using var outSw = new StringWriter();
        Console.SetError(errSw);
        Console.SetOut(outSw);
        try
        {
            var opts = Program.ParseArgs(["--json", "--nope"]);
            Assert.True(opts.ParseError);
            Assert.Equal(2, opts.ParseErrorExitCode);

            // Stdout MUST be empty.
            Assert.Equal(string.Empty, outSw.ToString());

            var doc = System.Text.Json.JsonDocument.Parse(errSw.ToString());
            var root = doc.RootElement;
            Assert.True(root.TryGetProperty("error", out var errObj));
            Assert.Equal("unknown_flag", errObj.GetProperty("code").GetString());
            Assert.Equal("--nope", errObj.GetProperty("flag").GetString());
        }
        finally
        {
            Console.SetError(oldErr);
            Console.SetOut(oldOut);
        }
    }

    [Fact]
    public void ParseArgs_UnknownFlag_JsonDetected_EvenBeforeItsTurn()
    {
        // `--nope` parses FIRST (fails). We still must emit JSON because `--json`
        // appears later in argv — the spec ("JSON variant") is about user intent,
        // not argv order.
        var oldErr = Console.Error;
        using var errSw = new StringWriter();
        Console.SetError(errSw);
        try
        {
            var opts = Program.ParseArgs(["--nope", "--json"]);
            Assert.True(opts.ParseError);
            // Stderr content should parse as JSON with the nested envelope.
            var doc = System.Text.Json.JsonDocument.Parse(errSw.ToString());
            Assert.Equal("unknown_flag",
                doc.RootElement.GetProperty("error").GetProperty("code").GetString());
        }
        finally { Console.SetError(oldErr); }
    }

    [Fact]
    public void ParseArgs_DoubleDash_EscapesUnknownFlagRejection()
    {
        // POSIX `--` ends flag parsing — anything after is positional.
        var opts = Program.ParseArgs(["--", "--not-a-flag", "just", "a", "prompt"]);
        Assert.False(opts.ParseError);
        Assert.Equal("--not-a-flag just a prompt", opts.Prompt);
    }

    [Fact]
    public void ParseArgs_KnownFlagWithValue_NotTreatedAsUnknown()
    {
        // Sanity: values consumed by known flags should not trip the unknown-flag rule.
        var opts = Program.ParseArgs(["--model", "gpt-4o", "hi"]);
        Assert.False(opts.ParseError);
        Assert.Equal("gpt-4o", opts.Model);
    }

    [Fact]
    public void ParseArgs_GenericParseError_StillExitCode1()
    {
        // A malformed --temperature is a generic parse error, not an unknown flag,
        // so it must keep exit code 1 (not be promoted to 2).
        var oldErr = Console.Error;
        using var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            var opts = Program.ParseArgs(["--temperature", "notanumber"]);
            Assert.True(opts.ParseError);
            Assert.Equal(1, opts.ParseErrorExitCode);
            Assert.Null(opts.UnknownFlag);
            Assert.True(opts.ShowHelp); // generic errors still dump help
        }
        finally { Console.SetError(oldErr); }
    }

    [Fact]
    public void ParseArgs_GenericParseError_JsonMode_EmitsFlatEnvelopeToStderr()
    {
        // Generic parse errors keep the flat {error, message, exit_code} envelope.
        var oldErr = Console.Error;
        var oldOut = Console.Out;
        using var errSw = new StringWriter();
        using var outSw = new StringWriter();
        Console.SetError(errSw);
        Console.SetOut(outSw);
        try
        {
            var opts = Program.ParseArgs(["--json", "--temperature", "notanumber"]);
            Assert.True(opts.ParseError);
            Assert.Equal(string.Empty, outSw.ToString());
            var doc = System.Text.Json.JsonDocument.Parse(errSw.ToString());
            Assert.True(doc.RootElement.GetProperty("error").GetBoolean());
            Assert.Contains("temperature", doc.RootElement.GetProperty("message").GetString() ?? "");
        }
        finally
        {
            Console.SetError(oldErr);
            Console.SetOut(oldOut);
        }
    }
}
