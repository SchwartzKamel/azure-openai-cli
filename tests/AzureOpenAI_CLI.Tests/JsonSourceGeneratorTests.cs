namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// Tests for JSON source generation (AOT compatibility).
/// </summary>
public class JsonSourceGeneratorTests
{
    [Fact]
    public void AppJsonContext_CanSerializeErrorJsonResponse()
    {
        var error = new ErrorJsonResponse(Error: true, Message: "Test error", ExitCode: 1);

        var json = System.Text.Json.JsonSerializer.Serialize(error, AppJsonContext.Default.ErrorJsonResponse);

        // Property names match JsonPropertyName attributes
        Assert.Contains("\"error\"", json);
        Assert.Contains("\"message\"", json);
        Assert.Contains("\"exit_code\"", json);  // snake_case from JsonPropertyName
        Assert.Contains("true", json);
        Assert.Contains("Test error", json);
    }

    [Fact]
    public void AppJsonContext_CanDeserializeErrorJsonResponse()
    {
        var json = """{"error":true,"message":"Deserialized","exit_code":42}""";

        var error = System.Text.Json.JsonSerializer.Deserialize(json, AppJsonContext.Default.ErrorJsonResponse);

        Assert.NotNull(error);
        Assert.True(error.Error);
        Assert.Equal("Deserialized", error.Message);
        Assert.Equal(42, error.ExitCode);
    }

    [Fact]
    public void AppJsonContext_RoundTrip_PreservesData()
    {
        var original = new ErrorJsonResponse(Error: false, Message: "Round trip", ExitCode: 0);

        var json = System.Text.Json.JsonSerializer.Serialize(original, AppJsonContext.Default.ErrorJsonResponse);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize(json, AppJsonContext.Default.ErrorJsonResponse);

        Assert.NotNull(deserialized);
        Assert.Equal(original.Error, deserialized.Error);
        Assert.Equal(original.Message, deserialized.Message);
        Assert.Equal(original.ExitCode, deserialized.ExitCode);
    }
}
