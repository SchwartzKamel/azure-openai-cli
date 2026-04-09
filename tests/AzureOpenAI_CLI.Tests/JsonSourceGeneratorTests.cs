using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AzureOpenAI_CLI;
using AzureOpenAI_CLI.Squad;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// Validates that the System.Text.Json source generator context (AppJsonContext)
/// covers all required types and produces correct serialization output.
/// Tests both positive (valid round-trips) and negative (missing/invalid data) scenarios.
/// </summary>
public class JsonSourceGeneratorTests
{
    // ── Round-trip: UserConfig ──────────────────────────────────────

    [Fact]
    public void UserConfig_RoundTrip_PreservesAllFields()
    {
        // Arrange — populate every field
        var original = new UserConfig
        {
            ActiveModel = "gpt-4o",
            AvailableModels = new List<string> { "gpt-4o", "gpt-35-turbo", "gpt-4" },
            Temperature = 0.7f,
            MaxTokens = 4096,
            TimeoutSeconds = 60,
            SystemPrompt = "You are a helpful assistant."
        };

        // Act — serialize then deserialize using the source-generated context
        string json = JsonSerializer.Serialize(original, AppJsonContext.Default.UserConfig);
        var deserialized = JsonSerializer.Deserialize(json, AppJsonContext.Default.UserConfig);

        // Assert — every field survives the round-trip
        Assert.NotNull(deserialized);
        Assert.Equal(original.ActiveModel, deserialized.ActiveModel);
        Assert.Equal(original.AvailableModels, deserialized.AvailableModels);
        Assert.Equal(original.Temperature, deserialized.Temperature);
        Assert.Equal(original.MaxTokens, deserialized.MaxTokens);
        Assert.Equal(original.TimeoutSeconds, deserialized.TimeoutSeconds);
        Assert.Equal(original.SystemPrompt, deserialized.SystemPrompt);
    }

    [Fact]
    public void UserConfig_RoundTrip_NullOptionalFields_OmittedInJson()
    {
        // Arrange — only required fields, nullable fields left null
        var original = new UserConfig
        {
            ActiveModel = "gpt-4o",
            AvailableModels = new List<string> { "gpt-4o" }
        };

        // Act
        string json = JsonSerializer.Serialize(original, AppJsonContext.Default.UserConfig);
        var deserialized = JsonSerializer.Deserialize(json, AppJsonContext.Default.UserConfig);

        // Assert — null fields are omitted from JSON (WhenWritingNull)
        Assert.DoesNotContain("temperature", json);
        Assert.DoesNotContain("maxTokens", json);
        Assert.DoesNotContain("timeoutSeconds", json);
        Assert.DoesNotContain("systemPrompt", json);

        // And the deserialized object has them as null
        Assert.NotNull(deserialized);
        Assert.Null(deserialized.Temperature);
        Assert.Null(deserialized.MaxTokens);
        Assert.Null(deserialized.TimeoutSeconds);
        Assert.Null(deserialized.SystemPrompt);
    }

    [Fact]
    public void UserConfig_Serialization_UsesCamelCase()
    {
        // Arrange
        var config = new UserConfig
        {
            ActiveModel = "gpt-4o",
            AvailableModels = new List<string> { "gpt-4o" },
            MaxTokens = 1024
        };

        // Act
        string json = JsonSerializer.Serialize(config, AppJsonContext.Default.UserConfig);

        // Assert — property names should be camelCase per source-gen options
        Assert.Contains("\"activeModel\"", json);
        Assert.Contains("\"availableModels\"", json);
        Assert.Contains("\"maxTokens\"", json);

        // Negative: PascalCase names should NOT appear
        Assert.DoesNotContain("\"ActiveModel\"", json);
        Assert.DoesNotContain("\"AvailableModels\"", json);
        Assert.DoesNotContain("\"MaxTokens\"", json);
    }

    [Fact]
    public void UserConfig_Serialization_IsIndented()
    {
        // Arrange
        var config = new UserConfig { ActiveModel = "gpt-4o" };

        // Act
        string json = JsonSerializer.Serialize(config, AppJsonContext.Default.UserConfig);

        // Assert — WriteIndented = true means newlines and indentation
        Assert.Contains("\n", json);
        Assert.Contains("  ", json); // indentation spaces
    }

    [Fact]
    public void UserConfig_Deserialize_EmptyJson_ReturnsDefaults()
    {
        // Arrange
        string json = "{}";

        // Act
        var config = JsonSerializer.Deserialize(json, AppJsonContext.Default.UserConfig);

        // Assert — everything is at default values
        Assert.NotNull(config);
        Assert.Null(config.ActiveModel);
        Assert.Empty(config.AvailableModels);
        Assert.Null(config.Temperature);
        Assert.Null(config.MaxTokens);
    }

    [Fact]
    public void UserConfig_Deserialize_InvalidJson_Throws()
    {
        // Arrange
        string badJson = "{ not valid json }";

        // Act & Assert — source-gen deserializer throws on malformed JSON
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize(badJson, AppJsonContext.Default.UserConfig));
    }

    // ── Round-trip: SquadConfig ─────────────────────────────────────

    [Fact]
    public void SquadConfig_RoundTrip_PreservesStructure()
    {
        // Arrange
        var original = new SquadConfig
        {
            Team = new TeamConfig { Name = "Test Squad", Description = "A test team" },
            Personas = new List<PersonaConfig>
            {
                new()
                {
                    Name = "coder",
                    Role = "Software Engineer",
                    Description = "Writes code",
                    SystemPrompt = "You are a coder.",
                    Tools = new List<string> { "shell", "file" },
                    Model = "gpt-4o"
                }
            },
            Routing = new List<RoutingRule>
            {
                new() { Pattern = "code,fix", Persona = "coder", Description = "Coding tasks" }
            }
        };

        // Act
        string json = JsonSerializer.Serialize(original, AppJsonContext.Default.SquadConfig);
        var deserialized = JsonSerializer.Deserialize(json, AppJsonContext.Default.SquadConfig);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("Test Squad", deserialized.Team.Name);
        Assert.Single(deserialized.Personas);
        Assert.Equal("coder", deserialized.Personas[0].Name);
        Assert.Equal(2, deserialized.Personas[0].Tools.Count);
        Assert.Single(deserialized.Routing);
        Assert.Equal("code,fix", deserialized.Routing[0].Pattern);
    }

    [Fact]
    public void SquadConfig_RoundTrip_NullModel_Omitted()
    {
        // Arrange — persona with null Model
        var config = new SquadConfig
        {
            Personas = new List<PersonaConfig>
            {
                new() { Name = "reviewer", Role = "Reviewer", Model = null }
            }
        };

        // Act
        string json = JsonSerializer.Serialize(config, AppJsonContext.Default.SquadConfig);

        // Assert — null Model should not appear in output (WhenWritingNull)
        Assert.DoesNotContain("\"model\"", json);
    }

    // ── Context type coverage ───────────────────────────────────────

    [Theory]
    [InlineData(typeof(UserConfig))]
    [InlineData(typeof(SquadConfig))]
    [InlineData(typeof(TeamConfig))]
    [InlineData(typeof(PersonaConfig))]
    [InlineData(typeof(RoutingRule))]
    [InlineData(typeof(List<string>))]
    [InlineData(typeof(List<PersonaConfig>))]
    [InlineData(typeof(List<RoutingRule>))]
    public void AppJsonContext_HasTypeInfo_ForExpectedType(Type expectedType)
    {
        // Act — GetTypeInfo returns non-null if the type is registered in the context
        JsonTypeInfo? typeInfo = AppJsonContext.Default.GetTypeInfo(expectedType);

        // Assert — the source generator produced metadata for this type
        Assert.NotNull(typeInfo);
    }

    [Theory]
    [InlineData(typeof(Dictionary<string, object>))]
    [InlineData(typeof(HashSet<int>))]
    [InlineData(typeof(Stack<string>))]
    public void AppJsonContext_ReturnsNull_ForUnregisteredType(Type unregisteredType)
    {
        // Act — types not listed in the context should not resolve
        JsonTypeInfo? typeInfo = AppJsonContext.Default.GetTypeInfo(unregisteredType);

        // Assert — null means the type isn't covered by this context
        Assert.Null(typeInfo);
    }

    // ── Negative: type mismatch in deserialization ──────────────────

    [Fact]
    public void UserConfig_Deserialize_WrongTypeValue_Throws()
    {
        // Arrange — maxTokens should be an int, not a string
        string json = """{"maxTokens": "not-a-number"}""";

        // Act & Assert
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize(json, AppJsonContext.Default.UserConfig));
    }

    [Fact]
    public void SquadConfig_Deserialize_EmptyJson_ReturnsDefaults()
    {
        // Arrange
        string json = "{}";

        // Act
        var config = JsonSerializer.Deserialize(json, AppJsonContext.Default.SquadConfig);

        // Assert — defaults from the class
        Assert.NotNull(config);
        Assert.Equal("Default Squad", config.Team.Name);
        Assert.Empty(config.Personas);
        Assert.Empty(config.Routing);
    }
}
