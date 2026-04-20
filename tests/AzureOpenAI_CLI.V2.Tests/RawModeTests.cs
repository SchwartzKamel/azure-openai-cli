namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// Tests verifying the --raw flag behavior (suppresses non-content output).
/// These are structural tests; actual API integration would require mocking infrastructure.
/// </summary>
public class RawModeTests
{
    [Fact]
    public void ParseArgs_RawFlag_SetsRawToTrue()
    {
        var opts = Program.ParseArgs(["--raw", "test"]);

        Assert.True(opts.Raw);
    }

    [Fact]
    public void ParseArgs_NoRawFlag_RawIsFalse()
    {
        var opts = Program.ParseArgs(["test"]);

        Assert.False(opts.Raw);
    }

    [Fact]
    public void ParseArgs_RawFlagWithOtherFlags_PreservesRaw()
    {
        var opts = Program.ParseArgs([
            "--model", "gpt-4o",
            "--temperature", "0.7",
            "--raw",
            "prompt"
        ]);

        Assert.True(opts.Raw);
        Assert.Equal("gpt-4o", opts.Model);
    }
}
