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

    // ── ChatJsonResponse ────────────────────────────────────────────

    [Fact]
    public void ChatJsonResponse_RoundTrip_MatchesExpectedJson()
    {
        // Arrange
        var original = new ChatJsonResponse("gpt-4o", "Hello world", 1234);

        // Act
        string json = JsonSerializer.Serialize(original, AppJsonContext.Default.ChatJsonResponse);
        var deserialized = JsonSerializer.Deserialize(json, AppJsonContext.Default.ChatJsonResponse);

        // Assert — round-trip preserves all fields
        Assert.NotNull(deserialized);
        Assert.Equal(original.Model, deserialized.Model);
        Assert.Equal(original.Response, deserialized.Response);
        Assert.Equal(original.DurationMs, deserialized.DurationMs);

        // Assert — snake_case property names in JSON output
        Assert.Contains("\"model\"", json);
        Assert.Contains("\"response\"", json);
        Assert.Contains("\"duration_ms\"", json);

        // Negative — PascalCase should NOT appear
        Assert.DoesNotContain("\"Model\"", json);
        Assert.DoesNotContain("\"Response\"", json);
        Assert.DoesNotContain("\"DurationMs\"", json);
    }

    [Fact]
    public void ChatJsonResponse_NullTokens_OmittedInJson()
    {
        // Arrange — default null optional token fields
        var response = new ChatJsonResponse("gpt-4o", "Hi", 500);

        // Act
        string json = JsonSerializer.Serialize(response, AppJsonContext.Default.ChatJsonResponse);

        // Assert — null tokens are omitted (WhenWritingNull)
        Assert.DoesNotContain("input_tokens", json);
        Assert.DoesNotContain("output_tokens", json);

        // Positive — required fields still present
        Assert.Contains("\"model\"", json);
        Assert.Contains("\"response\"", json);
        Assert.Contains("\"duration_ms\"", json);
    }

    [Fact]
    public void ChatJsonResponse_WithTokens_IncludesInJson()
    {
        // Arrange — explicitly set token counts
        var response = new ChatJsonResponse("gpt-4o", "Hi", 500, InputTokens: 100, OutputTokens: 42);

        // Act
        string json = JsonSerializer.Serialize(response, AppJsonContext.Default.ChatJsonResponse);
        var deserialized = JsonSerializer.Deserialize(json, AppJsonContext.Default.ChatJsonResponse);

        // Assert — token fields are present in JSON
        Assert.Contains("\"input_tokens\"", json);
        Assert.Contains("\"output_tokens\"", json);

        // Assert — values survive round-trip
        Assert.NotNull(deserialized);
        Assert.Equal(100, deserialized.InputTokens);
        Assert.Equal(42, deserialized.OutputTokens);
    }

    // ── AgentJsonResponse ───────────────────────────────────────────

    [Fact]
    public void AgentJsonResponse_RoundTrip_IncludesAgentInfo()
    {
        // Arrange
        var agent = new AgentInfo(3, 7);
        var original = new AgentJsonResponse("gpt-4o", "Done", 2500, agent);

        // Act
        string json = JsonSerializer.Serialize(original, AppJsonContext.Default.AgentJsonResponse);
        var deserialized = JsonSerializer.Deserialize(json, AppJsonContext.Default.AgentJsonResponse);

        // Assert — top-level fields
        Assert.NotNull(deserialized);
        Assert.Equal("gpt-4o", deserialized.Model);
        Assert.Equal("Done", deserialized.Response);
        Assert.Equal(2500, deserialized.DurationMs);

        // Assert — nested agent object
        Assert.NotNull(deserialized.Agent);
        Assert.Equal(3, deserialized.Agent.Rounds);
        Assert.Equal(7, deserialized.Agent.ToolsCalled);

        // Assert — snake_case keys in JSON
        Assert.Contains("\"agent\"", json);
        Assert.Contains("\"rounds\"", json);
        Assert.Contains("\"tools_called\"", json);

        // Negative — PascalCase should NOT appear
        Assert.DoesNotContain("\"Rounds\"", json);
        Assert.DoesNotContain("\"ToolsCalled\"", json);
    }

    [Fact]
    public void AgentJsonResponse_NullTokens_OmittedInJson()
    {
        // Arrange
        var response = new AgentJsonResponse("gpt-4o", "OK", 100, new AgentInfo(1, 0));

        // Act
        string json = JsonSerializer.Serialize(response, AppJsonContext.Default.AgentJsonResponse);

        // Assert — null tokens omitted, agent info preserved
        Assert.DoesNotContain("input_tokens", json);
        Assert.DoesNotContain("output_tokens", json);
        Assert.Contains("\"agent\"", json);
        Assert.Contains("\"rounds\"", json);
    }

    // ── Context type coverage (updated) ─────────────────────────────

    [Theory]
    [InlineData(typeof(ChatJsonResponse))]
    [InlineData(typeof(AgentJsonResponse))]
    [InlineData(typeof(AgentInfo))]
    [InlineData(typeof(ErrorJsonResponse))]
    public void AppJsonContext_HasTypeInfo_ForResponseTypes(Type expectedType)
    {
        // Act
        JsonTypeInfo? typeInfo = AppJsonContext.Default.GetTypeInfo(expectedType);

        // Assert — source generator covers the new response types
        Assert.NotNull(typeInfo);
    }

    // ── ErrorJsonResponse ───────────────────────────────────────────

    [Fact]
    public void ErrorJsonResponse_Serialization_ProducesExpectedSnakeCaseKeys()
    {
        // Arrange — the same shape as the old anonymous type used in OutputJsonError
        var err = new ErrorJsonResponse(Error: true, Message: "boom", ExitCode: 7);

        // Act
        string json = JsonSerializer.Serialize(err, AppJsonContext.Default.ErrorJsonResponse);
        var deserialized = JsonSerializer.Deserialize(json, AppJsonContext.Default.ErrorJsonResponse);

        // Assert — round-trip preserves every field
        Assert.NotNull(deserialized);
        Assert.True(deserialized.Error);
        Assert.Equal("boom", deserialized.Message);
        Assert.Equal(7, deserialized.ExitCode);

        // Assert — snake_case / lowercase keys present
        Assert.Contains("\"error\"", json);
        Assert.Contains("\"message\"", json);
        Assert.Contains("\"exit_code\"", json);

        // Negative — the old anonymous-type / PascalCase names must NOT appear
        Assert.DoesNotContain("\"Error\"", json);
        Assert.DoesNotContain("\"Message\"", json);
        Assert.DoesNotContain("\"ExitCode\"", json);
        Assert.DoesNotContain("\"exitCode\"", json);
    }

    [Fact]
    public void ErrorJsonResponse_Deserialize_InvalidJson_Throws()
    {
        // Arrange — malformed JSON must still fail loudly; we don't want AOT
        // silently returning defaults when the caller hands us garbage.
        string badJson = "{ not json }";

        // Act + Assert
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize(badJson, AppJsonContext.Default.ErrorJsonResponse));
    }

    // ── Shared-context options: comments + trailing commas ──────────
    // SquadConfig.Load() used to construct a custom JsonSerializerOptions with
    // ReadCommentHandling=Skip / AllowTrailingCommas=true. After the AOT
    // migration those flags live on AppJsonContext itself, so confirm the
    // forgiving parse behavior survived the switch to source-gen.

    [Fact]
    public void SquadConfig_Deserialize_AllowsCommentsAndTrailingCommas()
    {
        // Arrange — JSON with // comments and a trailing comma
        string json = """
            {
              // team metadata
              "team": { "name": "Hackers", "description": "test", },
              "personas": [],
              "routing": [],
            }
            """;

        // Act
        var config = JsonSerializer.Deserialize(json, AppJsonContext.Default.SquadConfig);

        // Assert — parsed successfully despite the non-strict JSON
        Assert.NotNull(config);
        Assert.Equal("Hackers", config.Team.Name);
        Assert.Empty(config.Personas);
        Assert.Empty(config.Routing);
    }

    [Fact]
    public void SquadConfig_Deserialize_CaseInsensitivePropertyNames()
    {
        // Arrange — PascalCase keys (not camelCase). SquadConfig also uses
        // explicit [JsonPropertyName] lowercase tags, so only case differs.
        string json = """{ "TEAM": { "NAME": "Shout", "DESCRIPTION": "loud" } }""";

        // Act
        var config = JsonSerializer.Deserialize(json, AppJsonContext.Default.SquadConfig);

        // Assert — PropertyNameCaseInsensitive=true on the context lets this bind
        Assert.NotNull(config);
        Assert.Equal("Shout", config.Team.Name);
        Assert.Equal("loud", config.Team.Description);
    }

    [Fact]
    public void SquadConfig_RoundTrip_UsesExplicitSnakeCaseOnPersonaSystemPrompt()
    {
        // Arrange — PersonaConfig.SystemPrompt carries [JsonPropertyName("system_prompt")]
        var config = new SquadConfig
        {
            Personas = new List<PersonaConfig>
            {
                new() { Name = "coder", Role = "SE", SystemPrompt = "Be concise." }
            }
        };

        // Act
        string json = JsonSerializer.Serialize(config, AppJsonContext.Default.SquadConfig);
        var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.SquadConfig);

        // Assert — explicit snake_case wins over the CamelCase naming policy
        Assert.Contains("\"system_prompt\"", json);
        Assert.DoesNotContain("\"systemPrompt\"", json);

        // And round-trip still preserves the value
        Assert.NotNull(back);
        Assert.Equal("Be concise.", back.Personas[0].SystemPrompt);
    }
}
