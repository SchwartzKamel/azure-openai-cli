using System.Text.Json;
using AzureOpenAI_CLI.Squad;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// Unit tests for <see cref="SquadInitializer"/>.
/// Covers scaffolding (file/directory creation), idempotency, and default config content.
/// </summary>
public class SquadInitializerTests : IDisposable
{
    private readonly string _tempDir;

    public SquadInitializerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "squad-init-test-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }

    // ── Initialize: File/directory creation ─────────────────────────────

    [Fact]
    public void Initialize_CreatesSquadJsonFile()
    {
        var result = SquadInitializer.Initialize(_tempDir);

        Assert.True(result);
        Assert.True(File.Exists(Path.Combine(_tempDir, ".squad.json")));
    }

    [Fact]
    public void Initialize_CreatesSquadDirectory()
    {
        SquadInitializer.Initialize(_tempDir);

        Assert.True(Directory.Exists(Path.Combine(_tempDir, ".squad")));
    }

    [Fact]
    public void Initialize_CreatesHistoryDirectory()
    {
        SquadInitializer.Initialize(_tempDir);

        Assert.True(Directory.Exists(Path.Combine(_tempDir, ".squad", "history")));
    }

    [Fact]
    public void Initialize_CreatesDecisionsMdWithHeader()
    {
        SquadInitializer.Initialize(_tempDir);

        var decisionsPath = Path.Combine(_tempDir, ".squad", "decisions.md");
        Assert.True(File.Exists(decisionsPath));

        var content = File.ReadAllText(decisionsPath);
        Assert.StartsWith("# Squad Decisions", content);
        Assert.Contains("Shared decision log", content);
    }

    [Fact]
    public void Initialize_CreatesReadmeMd()
    {
        SquadInitializer.Initialize(_tempDir);

        var readmePath = Path.Combine(_tempDir, ".squad", "README.md");
        Assert.True(File.Exists(readmePath));

        var content = File.ReadAllText(readmePath);
        Assert.Contains(".squad/", content);
        Assert.Contains("history/", content);
    }

    [Fact]
    public void Initialize_WritesValidJson()
    {
        SquadInitializer.Initialize(_tempDir);

        var json = File.ReadAllText(Path.Combine(_tempDir, ".squad.json"));
        var config = JsonSerializer.Deserialize<SquadConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });

        Assert.NotNull(config);
        Assert.NotEmpty(config.Personas);
    }

    // ── Initialize: Idempotency ─────────────────────────────────────────

    [Fact]
    public void Initialize_ReturnsFalseIfConfigAlreadyExists()
    {
        // First call creates the config
        var first = SquadInitializer.Initialize(_tempDir);
        Assert.True(first);

        // Second call detects it already exists
        var second = SquadInitializer.Initialize(_tempDir);
        Assert.False(second);
    }

    [Fact]
    public void Initialize_DoesNotOverwriteExistingConfig()
    {
        // Arrange: create a custom .squad.json
        var configPath = Path.Combine(_tempDir, ".squad.json");
        var customContent = """{"team":{"name":"My Custom Team"}}""";
        File.WriteAllText(configPath, customContent);

        // Act: initialize on top of it
        var result = SquadInitializer.Initialize(_tempDir);

        // Assert: returns false AND original content is preserved
        Assert.False(result);
        var actual = File.ReadAllText(configPath);
        Assert.Equal(customContent, actual);
    }

    // ── CreateDefaultConfig: Personas ───────────────────────────────────

    [Fact]
    public void CreateDefaultConfig_HasExactlyFivePersonas()
    {
        var config = SquadInitializer.CreateDefaultConfig();

        Assert.Equal(5, config.Personas.Count);
    }

    [Fact]
    public void CreateDefaultConfig_ContainsAllExpectedPersonas()
    {
        var config = SquadInitializer.CreateDefaultConfig();
        var names = config.Personas.Select(p => p.Name).ToHashSet();

        Assert.Contains("coder", names);
        Assert.Contains("reviewer", names);
        Assert.Contains("architect", names);
        Assert.Contains("writer", names);
        Assert.Contains("security", names);
    }

    [Fact]
    public void CreateDefaultConfig_AllPersonasHaveNonEmptySystemPrompt()
    {
        var config = SquadInitializer.CreateDefaultConfig();

        foreach (var persona in config.Personas)
        {
            Assert.False(
                string.IsNullOrWhiteSpace(persona.SystemPrompt),
                $"Persona '{persona.Name}' has empty SystemPrompt");
        }
    }

    [Fact]
    public void CreateDefaultConfig_AllPersonasHaveAtLeastOneTool()
    {
        var config = SquadInitializer.CreateDefaultConfig();

        foreach (var persona in config.Personas)
        {
            Assert.NotEmpty(persona.Tools);
        }
    }

    [Fact]
    public void CreateDefaultConfig_AllPersonasHaveNonEmptyRole()
    {
        var config = SquadInitializer.CreateDefaultConfig();

        foreach (var persona in config.Personas)
        {
            Assert.False(
                string.IsNullOrWhiteSpace(persona.Role),
                $"Persona '{persona.Name}' has empty Role");
        }
    }

    [Fact]
    public void CreateDefaultConfig_AllPersonasHaveNonEmptyDescription()
    {
        var config = SquadInitializer.CreateDefaultConfig();

        foreach (var persona in config.Personas)
        {
            Assert.False(
                string.IsNullOrWhiteSpace(persona.Description),
                $"Persona '{persona.Name}' has empty Description");
        }
    }

    // ── CreateDefaultConfig: Routing ────────────────────────────────────

    [Fact]
    public void CreateDefaultConfig_HasRoutingRulesForAllPersonas()
    {
        var config = SquadInitializer.CreateDefaultConfig();
        var routedPersonas = config.Routing.Select(r => r.Persona).ToHashSet();
        var personaNames = config.Personas.Select(p => p.Name).ToHashSet();

        // Every persona should have a routing rule
        Assert.Equal(personaNames, routedPersonas);
    }

    [Fact]
    public void CreateDefaultConfig_RoutingPatternsAreCommaSeparatedKeywords()
    {
        var config = SquadInitializer.CreateDefaultConfig();

        foreach (var rule in config.Routing)
        {
            // Pattern should contain at least one comma (multiple keywords)
            Assert.Contains(",", rule.Pattern);

            // Each keyword should be a single word (no spaces)
            var keywords = rule.Pattern.Split(',');
            Assert.All(keywords, kw =>
            {
                Assert.False(string.IsNullOrWhiteSpace(kw), "Empty keyword in routing pattern");
                Assert.DoesNotContain(" ", kw);
            });
        }
    }

    [Fact]
    public void CreateDefaultConfig_RoutingRulesHaveDescriptions()
    {
        var config = SquadInitializer.CreateDefaultConfig();

        foreach (var rule in config.Routing)
        {
            Assert.False(
                string.IsNullOrWhiteSpace(rule.Description),
                $"Routing rule for persona '{rule.Persona}' has empty Description");
        }
    }

    // ── CreateDefaultConfig: Team ───────────────────────────────────────

    [Fact]
    public void CreateDefaultConfig_DefaultTeamNameIsDefaultSquad()
    {
        var config = SquadInitializer.CreateDefaultConfig();

        Assert.Equal("Default Squad", config.Team.Name);
    }

    [Fact]
    public void CreateDefaultConfig_TeamHasNonEmptyDescription()
    {
        var config = SquadInitializer.CreateDefaultConfig();

        Assert.False(string.IsNullOrWhiteSpace(config.Team.Description));
    }

    // ── Negative: No persona named "nonexistent" ────────────────────────

    [Fact]
    public void CreateDefaultConfig_DoesNotContainUnexpectedPersona()
    {
        var config = SquadInitializer.CreateDefaultConfig();
        var names = config.Personas.Select(p => p.Name).ToHashSet();

        Assert.DoesNotContain("manager", names);
        Assert.DoesNotContain("devops", names);
        Assert.DoesNotContain("tester", names);
    }

    // ── Roundtrip: serialized config can be deserialized ────────────────

    [Fact]
    public void Initialize_RoundtripSerializationPreservesPersonas()
    {
        SquadInitializer.Initialize(_tempDir);

        var json = File.ReadAllText(Path.Combine(_tempDir, ".squad.json"));
        var loaded = JsonSerializer.Deserialize<SquadConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });

        Assert.NotNull(loaded);
        Assert.Equal(5, loaded.Personas.Count);

        var coder = loaded.Personas.FirstOrDefault(p => p.Name == "coder");
        Assert.NotNull(coder);
        Assert.Equal("Software Engineer", coder.Role);
        Assert.Contains("shell", coder.Tools);
    }
}
