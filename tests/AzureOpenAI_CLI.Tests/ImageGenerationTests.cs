using Xunit;
using AzureOpenAI_CLI;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// Tests for the --image flag, image model resolution, and flag conflict
/// validation. These are unit tests for parsing and resolution only -- no
/// actual API calls are made.
/// </summary>
public class ImageGenerationTests : IDisposable
{
    private readonly string? _origImageModel;
    private readonly string? _origFoundryModels;
    private readonly string? _origFoundryEndpoint;

    public ImageGenerationTests()
    {
        _origImageModel = Environment.GetEnvironmentVariable("AZURE_IMAGE_MODEL");
        _origFoundryModels = Environment.GetEnvironmentVariable("AZURE_FOUNDRY_MODELS");
        _origFoundryEndpoint = Environment.GetEnvironmentVariable("AZURE_FOUNDRY_ENDPOINT");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("AZURE_IMAGE_MODEL", _origImageModel);
        Environment.SetEnvironmentVariable("AZURE_FOUNDRY_MODELS", _origFoundryModels);
        Environment.SetEnvironmentVariable("AZURE_FOUNDRY_ENDPOINT", _origFoundryEndpoint);
    }

    // ── ParseArgs: --image flag ──────────────────────────────────────────

    [Fact]
    public void ParseArgs_Image_SetsImageMode()
    {
        var opts = Program.ParseArgs(["--image", "a cat in space"]);
        Assert.True(opts.ImageMode);
        Assert.Equal("a cat in space", opts.Prompt);
    }

    [Fact]
    public void ParseArgs_Image_DefaultsOutputAndSizeNull()
    {
        var opts = Program.ParseArgs(["--image", "test"]);
        Assert.Null(opts.OutputPath);
        Assert.Null(opts.ImageSize);
    }

    [Fact]
    public void ParseArgs_Output_SetsPath()
    {
        var opts = Program.ParseArgs(["--image", "--output", "/tmp/test.png", "a dog"]);
        Assert.True(opts.ImageMode);
        Assert.Equal("/tmp/test.png", opts.OutputPath);
    }

    [Fact]
    public void ParseArgs_Size_ValidFormat()
    {
        var opts = Program.ParseArgs(["--image", "--size", "512x512", "test"]);
        Assert.Equal("512x512", opts.ImageSize);
    }

    [Fact]
    public void ParseArgs_Size_InvalidFormat_ParseError()
    {
        var opts = Program.ParseArgs(["--image", "--size", "big", "test"]);
        Assert.True(opts.ParseError);
    }

    [Fact]
    public void ParseArgs_Size_MissingValue_ParseError()
    {
        var opts = Program.ParseArgs(["--image", "--size"]);
        Assert.True(opts.ParseError);
    }

    [Fact]
    public void ParseArgs_Output_MissingValue_ParseError()
    {
        var opts = Program.ParseArgs(["--image", "--output"]);
        Assert.True(opts.ParseError);
    }

    [Fact]
    public void ParseArgs_ImageWithRaw_BothSet()
    {
        var opts = Program.ParseArgs(["--image", "--raw", "sunrise"]);
        Assert.True(opts.ImageMode);
        Assert.True(opts.Raw);
    }

    [Fact]
    public void ParseArgs_ImageWithJson_BothSet()
    {
        var opts = Program.ParseArgs(["--image", "--json", "sunset"]);
        Assert.True(opts.ImageMode);
        Assert.True(opts.Json);
    }

    [Fact]
    public void ParseArgs_WithoutImage_ImageModeFalse()
    {
        var opts = Program.ParseArgs(["hello world"]);
        Assert.False(opts.ImageMode);
    }

    // ── ResolveImageModel ────────────────────────────────────────────────

    [Fact]
    public void ResolveImageModel_EnvSet_ReturnsEnvValue()
    {
        Environment.SetEnvironmentVariable("AZURE_IMAGE_MODEL", "flux-pro");
        Environment.SetEnvironmentVariable("AZURE_FOUNDRY_MODELS", "gpt-4o");

        var result = Program.ResolveImageModel();

        Assert.Equal("flux-pro", result);
    }

    [Fact]
    public void ResolveImageModel_EnvUnset_FallsBackToFoundry()
    {
        Environment.SetEnvironmentVariable("AZURE_IMAGE_MODEL", null);
        Environment.SetEnvironmentVariable("AZURE_FOUNDRY_MODELS", "dall-e-3,gpt-4o");

        var result = Program.ResolveImageModel();

        Assert.Equal("dall-e-3", result);
    }

    [Fact]
    public void ResolveImageModel_NothingSet_ReturnsNull()
    {
        Environment.SetEnvironmentVariable("AZURE_IMAGE_MODEL", null);
        Environment.SetEnvironmentVariable("AZURE_FOUNDRY_MODELS", null);

        var result = Program.ResolveImageModel();

        Assert.Null(result);
    }

    [Fact]
    public void ResolveImageModel_WhitespaceEnv_ReturnsNull()
    {
        Environment.SetEnvironmentVariable("AZURE_IMAGE_MODEL", "   ");
        Environment.SetEnvironmentVariable("AZURE_FOUNDRY_MODELS", null);

        var result = Program.ResolveImageModel();

        Assert.Null(result);
    }

    // ── BuildImageClient routing ─────────────────────────────────────────

    [Fact]
    public void BuildImageClient_AzureOpenAI_ReturnsNonNull()
    {
        // No Foundry configured -> should route to Azure OpenAI
        Environment.SetEnvironmentVariable("AZURE_FOUNDRY_ENDPOINT", null);
        Environment.SetEnvironmentVariable("AZURE_FOUNDRY_MODELS", null);

        var client = Program.BuildImageClient(
            "https://test.openai.azure.com/", "fake-key", "dall-e-3", false);

        Assert.NotNull(client);
    }

    [Fact]
    public void BuildImageClient_Foundry_WhenModelInFoundryList()
    {
        Environment.SetEnvironmentVariable("AZURE_FOUNDRY_ENDPOINT", "https://models.ai.azure.com");
        Environment.SetEnvironmentVariable("AZURE_FOUNDRY_MODELS", "flux-pro,dall-e-3");

        var client = Program.BuildImageClient(
            "https://test.openai.azure.com/", "fake-key", "flux-pro", false);

        Assert.NotNull(client);
    }

    [Fact]
    public void BuildImageClient_InvalidEndpoint_ReturnsNull()
    {
        Environment.SetEnvironmentVariable("AZURE_FOUNDRY_ENDPOINT", null);
        Environment.SetEnvironmentVariable("AZURE_FOUNDRY_MODELS", null);

        var client = Program.BuildImageClient("not-a-url", "fake-key", "dall-e-3", false);

        Assert.Null(client);
    }

    // ── Size parsing (WxH format) ────────────────────────────────────────

    [Theory]
    [InlineData("1024x1024")]
    [InlineData("512x512")]
    [InlineData("256x256")]
    [InlineData("1792x1024")]
    public void ParseArgs_Size_VariousValidFormats(string size)
    {
        var opts = Program.ParseArgs(["--image", "--size", size, "test"]);
        Assert.False(opts.ParseError);
        Assert.Equal(size, opts.ImageSize);
    }

    [Theory]
    [InlineData("large")]
    [InlineData("1024")]
    [InlineData("x1024")]
    [InlineData("1024x")]
    [InlineData("1024X1024")]  // uppercase X is invalid
    public void ParseArgs_Size_VariousInvalidFormats(string size)
    {
        var opts = Program.ParseArgs(["--image", "--size", size, "test"]);
        Assert.True(opts.ParseError);
    }
}
