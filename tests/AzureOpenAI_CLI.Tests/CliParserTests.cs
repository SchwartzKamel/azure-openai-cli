namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// Tests for CLI argument parsing (flags, env vars, defaults, positional prompt).
/// </summary>
[Collection("ConsoleCapture")]
public class CliParserTests
{
    [Fact]
    public void ParseArgs_NoArgs_UsesDefaults()
    {
        var opts = Program.ParseArgs([]);

        Assert.Null(opts.Model);
        Assert.Equal(0.55f, opts.Temperature);
        Assert.Equal(10000, opts.MaxTokens);
        Assert.Equal(120, opts.TimeoutSeconds);
        Assert.Equal("You are a secure, concise CLI assistant. Keep answers factual, no fluff.", opts.SystemPrompt);
        Assert.False(opts.Raw);
        Assert.False(opts.ShowHelp);
        Assert.False(opts.ShowVersion);
        Assert.Null(opts.Prompt);
    }

    [Fact]
    public void ParseArgs_PositionalPrompt_CapturedCorrectly()
    {
        var opts = Program.ParseArgs(["What", "is", "the", "capital", "of", "France?"]);

        Assert.Equal("What is the capital of France?", opts.Prompt);
    }

    [Theory]
    [InlineData("--model", "gpt-4o", "gpt-4o")]
    [InlineData("-m", "gpt-4", "gpt-4")]
    public void ParseArgs_ModelFlag_SetsModel(string flag, string value, string expected)
    {
        var opts = Program.ParseArgs([flag, value, "test prompt"]);

        Assert.Equal(expected, opts.Model);
        Assert.Equal("test prompt", opts.Prompt);
    }

    [Theory]
    [InlineData("--temperature", "0.7", 0.7f)]
    [InlineData("-t", "1.2", 1.2f)]
    [InlineData("--temperature", "0", 0.0f)]
    public void ParseArgs_TemperatureFlag_SetsTemperature(string flag, string value, float expected)
    {
        var opts = Program.ParseArgs([flag, value, "prompt"]);

        Assert.Equal(expected, opts.Temperature);
    }

    [Fact]
    public void ParseArgs_MaxTokensFlag_SetsMaxTokens()
    {
        var opts = Program.ParseArgs(["--max-tokens", "5000", "prompt"]);

        Assert.Equal(5000, opts.MaxTokens);
    }

    [Fact]
    public void ParseArgs_TimeoutFlag_SetsTimeout()
    {
        var opts = Program.ParseArgs(["--timeout", "60", "prompt"]);

        Assert.Equal(60, opts.TimeoutSeconds);
    }

    [Theory]
    [InlineData("--system", "You are a pirate")]
    [InlineData("-s", "You are a helpful assistant")]
    public void ParseArgs_SystemFlag_SetsSystemPrompt(string flag, string value)
    {
        var opts = Program.ParseArgs([flag, value, "prompt"]);

        Assert.Equal(value, opts.SystemPrompt);
    }

    [Fact]
    public void ParseArgs_RawFlag_SetsRaw()
    {
        var opts = Program.ParseArgs(["--raw", "prompt"]);

        Assert.True(opts.Raw);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void ParseArgs_HelpFlag_SetsShowHelp(string flag)
    {
        var opts = Program.ParseArgs([flag]);

        Assert.True(opts.ShowHelp);
    }

    [Theory]
    [InlineData("--version")]
    [InlineData("-v")]
    public void ParseArgs_VersionFlag_SetsShowVersion(string flag)
    {
        var opts = Program.ParseArgs([flag]);

        Assert.True(opts.ShowVersion);
    }

    [Fact]
    public void ParseArgs_MultipleFlags_AllRecognized()
    {
        var opts = Program.ParseArgs([
            "--model", "gpt-4o",
            "--temperature", "0.8",
            "--max-tokens", "2000",
            "--timeout", "90",
            "--system", "Custom system",
            "--raw",
            "my", "prompt"
        ]);

        Assert.Equal("gpt-4o", opts.Model);
        Assert.Equal(0.8f, opts.Temperature);
        Assert.Equal(2000, opts.MaxTokens);
        Assert.Equal(90, opts.TimeoutSeconds);
        Assert.Equal("Custom system", opts.SystemPrompt);
        Assert.True(opts.Raw);
        Assert.Equal("my prompt", opts.Prompt);
    }

    [Fact]
    public void ParseArgs_EnvVarTemperature_AppliesWhenNoFlag()
    {
        Environment.SetEnvironmentVariable("AZURE_TEMPERATURE", "0.9");
        try
        {
            var opts = Program.ParseArgs(["prompt"]);
            Assert.Equal(0.9f, opts.Temperature);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZURE_TEMPERATURE", null);
        }
    }

    [Fact]
    public void ParseArgs_EnvVarMaxTokens_AppliesWhenNoFlag()
    {
        Environment.SetEnvironmentVariable("AZURE_MAX_TOKENS", "8000");
        try
        {
            var opts = Program.ParseArgs(["prompt"]);
            Assert.Equal(8000, opts.MaxTokens);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZURE_MAX_TOKENS", null);
        }
    }

    [Fact]
    public void ParseArgs_EnvVarTimeout_AppliesWhenNoFlag()
    {
        Environment.SetEnvironmentVariable("AZURE_TIMEOUT", "180");
        try
        {
            var opts = Program.ParseArgs(["prompt"]);
            Assert.Equal(180, opts.TimeoutSeconds);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZURE_TIMEOUT", null);
        }
    }

    [Fact]
    public void ParseArgs_EnvVarSystemPrompt_AppliesWhenNoFlag()
    {
        Environment.SetEnvironmentVariable("SYSTEMPROMPT", "Env system prompt");
        try
        {
            var opts = Program.ParseArgs(["prompt"]);
            Assert.Equal("Env system prompt", opts.SystemPrompt);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SYSTEMPROMPT", null);
        }
    }

    [Fact]
    public void ParseArgs_FlagOverridesEnvVar()
    {
        Environment.SetEnvironmentVariable("AZURE_TEMPERATURE", "0.9");
        Environment.SetEnvironmentVariable("AZURE_MAX_TOKENS", "8000");
        try
        {
            var opts = Program.ParseArgs(["--temperature", "0.3", "--max-tokens", "3000", "prompt"]);

            Assert.Equal(0.3f, opts.Temperature);
            Assert.Equal(3000, opts.MaxTokens);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZURE_TEMPERATURE", null);
            Environment.SetEnvironmentVariable("AZURE_MAX_TOKENS", null);
        }
    }
}
