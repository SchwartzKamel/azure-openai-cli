using AzureOpenAI_CLI.Squad;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// Unit tests for the Squad persona system — SquadConfig, PersonaMemory, and SquadCoordinator.
/// Each test method uses an isolated temp directory, cleaned up on dispose.
/// </summary>
public class SquadTests : IDisposable
{
    private readonly string _tempDir;

    public SquadTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "squad-test-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }

    // ── SquadConfig ─────────────────────────────────────────────────────

    [Fact]
    public void Load_ReturnsNull_WhenNoFileExists()
    {
        var config = SquadConfig.Load(_tempDir);
        Assert.Null(config);
    }

    [Fact]
    public void Load_ParsesValidJson_WithPersonasAndRouting()
    {
        var json = """
        {
            "team": { "name": "Test Squad", "description": "A test team" },
            "personas": [
                {
                    "name": "Coder",
                    "role": "developer",
                    "description": "Writes code",
                    "system_prompt": "You write code.",
                    "tools": ["shell", "file"],
                    "model": "gpt-4o"
                },
                {
                    "name": "Reviewer",
                    "role": "reviewer",
                    "description": "Reviews code",
                    "system_prompt": "You review code.",
                    "tools": ["file"]
                }
            ],
            "routing": [
                {
                    "pattern": "code, implement, build",
                    "persona": "Coder",
                    "description": "Development tasks"
                },
                {
                    "pattern": "review, check, audit",
                    "persona": "Reviewer",
                    "description": "Review tasks"
                }
            ]
        }
        """;
        File.WriteAllText(Path.Combine(_tempDir, ".squad.json"), json);

        var config = SquadConfig.Load(_tempDir);

        Assert.NotNull(config);
        Assert.Equal("Test Squad", config.Team.Name);
        Assert.Equal("A test team", config.Team.Description);
        Assert.Equal(2, config.Personas.Count);
        Assert.Equal("Coder", config.Personas[0].Name);
        Assert.Equal("developer", config.Personas[0].Role);
        Assert.Equal("You write code.", config.Personas[0].SystemPrompt);
        Assert.Equal(new[] { "shell", "file" }, config.Personas[0].Tools);
        Assert.Equal("gpt-4o", config.Personas[0].Model);
        Assert.Null(config.Personas[1].Model);
        Assert.Equal(2, config.Routing.Count);
        Assert.Equal("code, implement, build", config.Routing[0].Pattern);
        Assert.Equal("Coder", config.Routing[0].Persona);
    }

    [Fact]
    public void GetPersona_IsCaseInsensitive()
    {
        var config = CreateTestConfig();

        Assert.NotNull(config.GetPersona("coder"));
        Assert.NotNull(config.GetPersona("CODER"));
        Assert.NotNull(config.GetPersona("Coder"));
        Assert.Equal("Coder", config.GetPersona("coder")!.Name);
    }

    [Fact]
    public void GetPersona_ReturnsNull_ForUnknownName()
    {
        var config = CreateTestConfig();
        Assert.Null(config.GetPersona("nonexistent"));
        Assert.Null(config.GetPersona(""));
    }

    [Fact]
    public void ListPersonas_ReturnsAllNames()
    {
        var config = CreateTestConfig();
        var names = config.ListPersonas();

        Assert.Equal(2, names.Count);
        Assert.Contains("Coder", names);
        Assert.Contains("Reviewer", names);
    }

    [Fact]
    public void Save_CreatesValidJsonFile()
    {
        var config = CreateTestConfig();
        config.Save(_tempDir);

        var path = Path.Combine(_tempDir, ".squad.json");
        Assert.True(File.Exists(path));

        // Round-trip: reload and verify
        var reloaded = SquadConfig.Load(_tempDir);
        Assert.NotNull(reloaded);
        Assert.Equal(config.Team.Name, reloaded.Team.Name);
        Assert.Equal(config.Personas.Count, reloaded.Personas.Count);
        Assert.Equal(config.Personas[0].Name, reloaded.GetPersona("Coder")!.Name);
        Assert.Equal(config.Routing.Count, reloaded.Routing.Count);
    }

    [Fact]
    public void Load_HandlesCommentsAndTrailingCommas()
    {
        var json = """
        {
            // This is a comment
            "team": { "name": "Commented Squad", },
            "personas": [
                {
                    "name": "Bot",
                    "role": "assistant",
                    "description": "Helpful bot",
                    "system_prompt": "Be helpful.",
                    "tools": [],
                },
            ],
            "routing": [],
        }
        """;
        File.WriteAllText(Path.Combine(_tempDir, ".squad.json"), json);

        var config = SquadConfig.Load(_tempDir);

        Assert.NotNull(config);
        Assert.Equal("Commented Squad", config.Team.Name);
        Assert.Single(config.Personas);
        Assert.Equal("Bot", config.Personas[0].Name);
    }

    [Fact]
    public void Load_EmptyPersonasList_IsValid()
    {
        var json = """
        {
            "team": { "name": "Empty Squad" },
            "personas": [],
            "routing": []
        }
        """;
        File.WriteAllText(Path.Combine(_tempDir, ".squad.json"), json);

        var config = SquadConfig.Load(_tempDir);

        Assert.NotNull(config);
        Assert.Empty(config.Personas);
        Assert.Empty(config.Routing);
        Assert.Empty(config.ListPersonas());
    }

    // ── PersonaMemory ───────────────────────────────────────────────────

    [Fact]
    public void ReadHistory_ReturnsEmpty_ForNewPersona()
    {
        var memory = new PersonaMemory(Path.Combine(_tempDir, ".squad"));
        memory.Initialize();

        var history = memory.ReadHistory("unknown-persona");
        Assert.Equal("", history);
    }

    [Fact]
    public void AppendHistory_CreatesFileAndDirectory()
    {
        var squadDir = Path.Combine(_tempDir, ".squad");
        var memory = new PersonaMemory(squadDir);

        memory.AppendHistory("coder", "Write a parser", "Wrote a recursive descent parser");

        var historyPath = Path.Combine(squadDir, "history", "coder.md");
        Assert.True(File.Exists(historyPath));

        var content = File.ReadAllText(historyPath);
        Assert.Contains("Write a parser", content);
        Assert.Contains("Wrote a recursive descent parser", content);
        Assert.Contains("## Session", content);
    }

    [Fact]
    public void AppendHistory_AccumulatesEntries()
    {
        var memory = new PersonaMemory(Path.Combine(_tempDir, ".squad"));
        memory.Initialize();

        memory.AppendHistory("coder", "Task 1", "Result 1");
        memory.AppendHistory("coder", "Task 2", "Result 2");
        memory.AppendHistory("coder", "Task 3", "Result 3");

        var history = memory.ReadHistory("coder");
        Assert.Contains("Task 1", history);
        Assert.Contains("Task 2", history);
        Assert.Contains("Task 3", history);
        Assert.Contains("Result 1", history);
        Assert.Contains("Result 3", history);
    }

    [Fact]
    public void ReadHistory_TruncatesLongHistory_KeepsTail()
    {
        var memory = new PersonaMemory(Path.Combine(_tempDir, ".squad"));
        memory.Initialize();

        // Write enough data to exceed MaxHistoryBytes (32 KB)
        for (int i = 0; i < 500; i++)
        {
            memory.AppendHistory("verbose", $"Task {i}", $"Result {i} — " + new string('X', 100));
        }

        var history = memory.ReadHistory("verbose");

        // Should be truncated
        Assert.Contains("...(earlier history truncated)...", history);
        // Tail (most recent) should be preserved
        Assert.Contains("Task 499", history);
        // Head (oldest) should be gone
        Assert.DoesNotContain("Task 0", history);
    }

    [Fact]
    public void LogDecision_CreatesDecisionsFile()
    {
        var squadDir = Path.Combine(_tempDir, ".squad");
        var memory = new PersonaMemory(squadDir);

        memory.LogDecision("coder", "Use System.Text.Json over Newtonsoft");

        var path = Path.Combine(squadDir, "decisions.md");
        Assert.True(File.Exists(path));

        var content = File.ReadAllText(path);
        Assert.Contains("coder", content);
        Assert.Contains("Use System.Text.Json over Newtonsoft", content);
    }

    [Fact]
    public void LogDecision_AppendsEntries()
    {
        var memory = new PersonaMemory(Path.Combine(_tempDir, ".squad"));
        memory.Initialize();

        memory.LogDecision("coder", "Decision A");
        memory.LogDecision("reviewer", "Decision B");

        var decisions = memory.ReadDecisions();
        Assert.Contains("Decision A", decisions);
        Assert.Contains("Decision B", decisions);
        Assert.Contains("coder", decisions);
        Assert.Contains("reviewer", decisions);
    }

    [Fact]
    public void ReadDecisions_ReturnsEmpty_WhenNoFile()
    {
        var memory = new PersonaMemory(Path.Combine(_tempDir, ".squad-nonexistent"));
        var decisions = memory.ReadDecisions();
        Assert.Equal("", decisions);
    }

    [Fact]
    public void IsInitialized_ReturnsFalse_BeforeInit()
    {
        var memory = new PersonaMemory(Path.Combine(_tempDir, ".squad-fresh"));
        Assert.False(memory.IsInitialized());
    }

    [Fact]
    public void Initialize_CreatesDirectoryStructure()
    {
        var squadDir = Path.Combine(_tempDir, ".squad");
        var memory = new PersonaMemory(squadDir);

        memory.Initialize();

        Assert.True(Directory.Exists(squadDir));
        Assert.True(Directory.Exists(Path.Combine(squadDir, "history")));
        Assert.True(File.Exists(Path.Combine(squadDir, "decisions.md")));
        Assert.True(memory.IsInitialized());
    }

    [Fact]
    public void Initialize_IsIdempotent()
    {
        var squadDir = Path.Combine(_tempDir, ".squad");
        var memory = new PersonaMemory(squadDir);

        memory.Initialize();
        var decisionsContent1 = File.ReadAllText(Path.Combine(squadDir, "decisions.md"));

        memory.Initialize(); // second call should not throw or corrupt
        var decisionsContent2 = File.ReadAllText(Path.Combine(squadDir, "decisions.md"));

        Assert.Equal(decisionsContent1, decisionsContent2);
        Assert.True(memory.IsInitialized());
    }

    // ── SquadCoordinator ────────────────────────────────────────────────

    [Fact]
    public void Route_ReturnsFirstPersona_WhenNoRoutingRules()
    {
        var config = new SquadConfig
        {
            Personas = new List<PersonaConfig>
            {
                new() { Name = "Default" },
                new() { Name = "Other" },
            },
            Routing = new List<RoutingRule>(), // no routing rules
        };
        var coordinator = new SquadCoordinator(config);

        var persona = coordinator.Route("anything at all");
        Assert.NotNull(persona);
        Assert.Equal("Default", persona.Name);
    }

    [Fact]
    public void Route_MatchesSingleKeyword()
    {
        var config = CreateTestConfig();
        var coordinator = new SquadCoordinator(config);

        var persona = coordinator.Route("please review my pull request");
        Assert.NotNull(persona);
        Assert.Equal("Reviewer", persona.Name);
    }

    [Fact]
    public void Route_PicksHighestScoringMatch_WithMultipleKeywords()
    {
        var config = CreateTestConfig();
        var coordinator = new SquadCoordinator(config);

        // "implement and build" matches 2 keywords for Coder (implement, build)
        // "review" would match 1 keyword for Reviewer
        var persona = coordinator.Route("implement and build a new feature");
        Assert.NotNull(persona);
        Assert.Equal("Coder", persona.Name);
    }

    [Fact]
    public void Route_ReturnsFallback_WhenNoKeywordMatch()
    {
        var config = CreateTestConfig();
        var coordinator = new SquadCoordinator(config);

        // None of the routing keywords match
        var persona = coordinator.Route("tell me a joke about cats");
        Assert.NotNull(persona);
        Assert.Equal("Coder", persona.Name); // falls back to first persona
    }

    [Fact]
    public void Route_HandlesEmptyPrompt()
    {
        var config = CreateTestConfig();
        var coordinator = new SquadCoordinator(config);

        var persona = coordinator.Route("");
        Assert.NotNull(persona);
        Assert.Equal("Coder", persona.Name); // first persona (empty/whitespace path)

        var persona2 = coordinator.Route("   ");
        Assert.NotNull(persona2);
        Assert.Equal("Coder", persona2.Name);
    }

    [Fact]
    public void Route_IsCaseInsensitive()
    {
        var config = CreateTestConfig();
        var coordinator = new SquadCoordinator(config);

        var persona = coordinator.Route("REVIEW THIS PULL REQUEST NOW");
        Assert.NotNull(persona);
        Assert.Equal("Reviewer", persona.Name);
    }

    [Fact]
    public void Route_ReturnsNull_WhenNoPersonas()
    {
        var config = new SquadConfig
        {
            Personas = new List<PersonaConfig>(),
            Routing = new List<RoutingRule>(),
        };
        var coordinator = new SquadCoordinator(config);

        var persona = coordinator.Route("anything");
        Assert.Null(persona);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static SquadConfig CreateTestConfig() => new()
    {
        Team = new TeamConfig { Name = "Test Squad", Description = "Test team" },
        Personas = new List<PersonaConfig>
        {
            new()
            {
                Name = "Coder",
                Role = "developer",
                Description = "Writes code",
                SystemPrompt = "You write clean code.",
                Tools = new List<string> { "shell", "file" },
                Model = "gpt-4o",
            },
            new()
            {
                Name = "Reviewer",
                Role = "reviewer",
                Description = "Reviews code",
                SystemPrompt = "You review code carefully.",
                Tools = new List<string> { "file" },
            },
        },
        Routing = new List<RoutingRule>
        {
            new()
            {
                Pattern = "code, implement, build",
                Persona = "Coder",
                Description = "Development tasks",
            },
            new()
            {
                Pattern = "review, check, audit",
                Persona = "Reviewer",
                Description = "Review tasks",
            },
        },
    };
}
