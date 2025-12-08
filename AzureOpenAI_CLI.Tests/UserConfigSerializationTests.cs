using System.Text.Json;
using AzureOpenAI_CLI;

namespace AzureOpenAI_CLI.Tests;

public class UserConfigSerializationTests
{
    [Fact]
    public void SerializeDeserialize_RoundTrip_UsingSourceGenContext()
    {
        var original = new UserConfig
        {
            ActiveModel = "gpt-test",
            AvailableModels = new List<string> { "gpt-test", "gpt-2" }
        };

        string json = JsonSerializer.Serialize(original, UserConfigJsonContext.Default.UserConfig);
        var roundTripped = JsonSerializer.Deserialize<UserConfig>(json, UserConfigJsonContext.Default.UserConfig);

        Assert.NotNull(roundTripped);
        Assert.Equal(original.ActiveModel, roundTripped!.ActiveModel);
        Assert.Equal(original.AvailableModels, roundTripped.AvailableModels);
    }
}
