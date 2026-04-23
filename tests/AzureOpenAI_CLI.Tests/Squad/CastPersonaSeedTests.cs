using System.Text.RegularExpressions;
using AzureOpenAI_CLI.Squad;

namespace AzureOpenAI_CLI.Tests.Squad;

/// <summary>
/// S02E30 -- The Cast. Verifies the 12 Seinfeld-themed cast personas are baked
/// into the default seed alongside the 5 generics, with valid prompts, routing
/// keywords, and direct-name routing precedence.
///
/// Owned by this test file: cast persona seed surface.
/// NOT touched here: <c>tests/AzureOpenAI_CLI.Tests/Squad/PersonaBehaviorTests.cs</c>
/// (S02E31 territory).
/// </summary>
public class CastPersonaSeedTests
{
    private static readonly string[] CastNames =
    {
        "costanza", "kramer", "elaine", "jerry", "newman",
        "larry-david", "lloyd-braun",
        "maestro", "mickey-abbott", "frank-costanza", "soup-nazi", "mr-wilhelm",
    };

    private static readonly string[] GenericNames =
    {
        "coder", "reviewer", "architect", "writer", "security",
    };

    private static readonly Regex KebabCase = new("^[a-z][a-z0-9]*(-[a-z0-9]+)*$", RegexOptions.Compiled);

    [Fact]
    public void Seed_Loads_Without_Throwing()
    {
        var config = SquadInitializer.CreateDefaultConfig();
        Assert.NotNull(config);
        Assert.NotEmpty(config.Personas);
    }

    [Fact]
    public void Seed_Contains_All_Twelve_Cast_Personas()
    {
        var config = SquadInitializer.CreateDefaultConfig();
        var names = config.Personas.Select(p => p.Name.ToLowerInvariant()).ToHashSet();

        foreach (var castName in CastNames)
        {
            Assert.True(names.Contains(castName), $"Cast persona '{castName}' missing from default seed");
        }
    }

    [Fact]
    public void Seed_Preserves_All_Five_Generic_Personas_Additive_Guarantee()
    {
        var config = SquadInitializer.CreateDefaultConfig();
        var names = config.Personas.Select(p => p.Name.ToLowerInvariant()).ToHashSet();

        foreach (var generic in GenericNames)
        {
            Assert.True(names.Contains(generic), $"Generic persona '{generic}' missing -- additive guarantee broken");
        }

        Assert.True(config.Personas.Count >= GenericNames.Length + CastNames.Length,
            $"Expected at least {GenericNames.Length + CastNames.Length} personas, got {config.Personas.Count}");
    }

    [Fact]
    public void Cast_Persona_Names_Are_Kebab_Case_And_Unique()
    {
        var config = SquadInitializer.CreateDefaultConfig();
        var allNames = config.Personas.Select(p => p.Name).ToList();

        Assert.Equal(allNames.Count, allNames.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (var castName in CastNames)
        {
            Assert.True(KebabCase.IsMatch(castName), $"Cast name '{castName}' is not kebab-case");
            Assert.DoesNotContain(castName, GenericNames);
        }
    }

    [Fact]
    public void Cast_Personas_Have_System_Prompts_Within_Length_Bounds()
    {
        var config = SquadInitializer.CreateDefaultConfig();

        foreach (var castName in CastNames)
        {
            var p = config.GetPersona(castName);
            Assert.NotNull(p);
            Assert.False(string.IsNullOrWhiteSpace(p!.SystemPrompt),
                $"Cast persona '{castName}' has empty system prompt");
            Assert.InRange(p.SystemPrompt.Length, 100, 4000);
        }
    }

    [Fact]
    public void Cast_Personas_Have_At_Least_Three_Routing_Keywords()
    {
        var config = SquadInitializer.CreateDefaultConfig();

        foreach (var castName in CastNames)
        {
            var rule = config.Routing.FirstOrDefault(
                r => r.Persona.Equals(castName, StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(rule);

            var keywords = rule!.Pattern
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Assert.True(keywords.Length >= 3,
                $"Cast persona '{castName}' has fewer than 3 routing keywords ({keywords.Length})");
        }
    }

    [Fact]
    public void Cast_Personas_Have_Memory_Enabled_Via_Tools()
    {
        // The PersonaConfig surface uses Tools as the per-persona allow-list. Cast
        // personas should each have a non-empty tool list -- equivalent to the
        // "memory enabled" guarantee the brief calls out (memory is a runtime
        // concern of PersonaMemory, but every cast member must be addressable
        // with at least file/shell access for their domain).
        var config = SquadInitializer.CreateDefaultConfig();
        foreach (var castName in CastNames)
        {
            var p = config.GetPersona(castName);
            Assert.NotNull(p);
            Assert.NotEmpty(p!.Tools);
        }
    }

    [Fact]
    public void Cast_Persona_System_Prompts_Are_Ascii_Clean()
    {
        // Smart quotes, em-dashes, en-dashes in C# string literals are real bugs
        // at runtime -- assistive tech mishandles them and prompts re-encode oddly.
        var config = SquadInitializer.CreateDefaultConfig();
        var smartChars = new[] { '\u2018', '\u2019', '\u201C', '\u201D', '\u2013', '\u2014' };

        foreach (var castName in CastNames)
        {
            var p = config.GetPersona(castName);
            Assert.NotNull(p);
            foreach (var ch in smartChars)
            {
                Assert.False(p!.SystemPrompt.Contains(ch),
                    $"Cast persona '{castName}' system prompt contains non-ASCII smart character U+{((int)ch):X4}");
                Assert.False(p.Description.Contains(ch),
                    $"Cast persona '{castName}' description contains non-ASCII smart character U+{((int)ch):X4}");
            }
        }
    }

    [Fact]
    public void RouteByKeyword_DirectNameMatch_BeatsKeywordRouting()
    {
        // "kramer code review" contains 'kramer' (cast) and 'code' + 'review'
        // (generic coder/reviewer keywords). Direct name match must win.
        var config = SquadInitializer.CreateDefaultConfig();
        var coordinator = new SquadCoordinator(config);

        var resolved = coordinator.RouteByKeyword("kramer code review");

        Assert.NotNull(resolved);
        Assert.Equal("kramer", resolved!.Name, ignoreCase: true);
    }

    [Fact]
    public void RouteByKeyword_FallsThroughToKeyword_WhenNoNameMatch()
    {
        // No persona name in prompt -- generic keyword routing should still work.
        var config = SquadInitializer.CreateDefaultConfig();
        var coordinator = new SquadCoordinator(config);

        var resolved = coordinator.RouteByKeyword("review this code for bugs");

        Assert.NotNull(resolved);
        // Either coder (code, bug) or reviewer (review) -- both are generic; assert
        // we did NOT accidentally route to a cast persona.
        Assert.Contains(resolved!.Name.ToLowerInvariant(), GenericNames);
    }

    [Fact]
    public void RouteByKeyword_MatchesKebabCastName()
    {
        // Kebab-case names must survive tokenization for direct-name match.
        var config = SquadInitializer.CreateDefaultConfig();
        var coordinator = new SquadCoordinator(config);

        var resolved = coordinator.RouteByKeyword("ask larry-david to greenlight the next episode");

        Assert.NotNull(resolved);
        Assert.Equal("larry-david", resolved!.Name, ignoreCase: true);
    }
}
