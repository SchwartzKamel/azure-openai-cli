using AzureOpenAI_CLI.Squad;

namespace AzureOpenAI_CLI.Tests.Squad;

/// <summary>
/// S02E31 -- The Audition.
///
/// Adversarial behavior coverage of the FIVE existing generic personas
/// (coder, reviewer, architect, writer, security) shipped by
/// <see cref="SquadInitializer.CreateDefaultConfig"/>.
///
/// These tests INSPECT current behavior. Where a behavior gap is
/// discovered, the test is marked Skipped with the corresponding
/// finding name from docs/exec-reports/s02e31-findings.md. This is
/// deliberate -- S02E31 is a test-only audit episode. No Squad code
/// changes; the gaps roll forward as findings for later episodes.
/// </summary>
public class PersonaBehaviorTests : IDisposable
{
    private readonly string _tempDir;

    public PersonaBehaviorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "persona-behavior-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }

    private static SquadConfig DefaultConfig() => SquadInitializer.CreateDefaultConfig();

    // -- 1. Out-of-character prompts -------------------------------------
    //
    // The persona system prompt MUST be present in the eventual request
    // payload so the model has at least the chance to push back on a
    // wildly off-topic ask. We can't run the model in a unit test, but
    // we can verify the persona's SystemPrompt exists, is non-empty, and
    // that the persona is the one the coordinator hands back.

    [Fact]
    public void Coder_SystemPromptIsPresentAndNonEmpty()
    {
        var config = DefaultConfig();

        var coder = config.GetPersona("coder");

        Assert.NotNull(coder);
        Assert.False(string.IsNullOrWhiteSpace(coder.SystemPrompt),
            "coder must have a system prompt -- empty prompts degenerate the persona to default behavior.");
        Assert.Contains("software engineer", coder.SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Skip = "Behavior gap; tracked as finding e31-personas-no-stay-in-character-clause")]
    public void Coder_SystemPromptShouldContainStayInCharacterGuidance_OutOfCharacterAdversarial()
    {
        // Adversarial: a user asks `coder` to "write a poem about clouds".
        // The persona prompt has no explicit "decline off-topic asks" or
        // "stay in role" clause, so the model is free to silently comply.
        // Document the gap here; do not fix in S02E31.
        var coder = DefaultConfig().GetPersona("coder")!;
        Assert.Contains("decline", coder.SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Skip = "Behavior gap; tracked as finding e31-personas-no-stay-in-character-clause")]
    public void Security_SystemPromptShouldContainStayInCharacterGuidance_OutOfCharacterAdversarial()
    {
        // Adversarial: ask `security` to "recommend a restaurant in Queens".
        // Same gap -- no role-anchoring clause.
        var security = DefaultConfig().GetPersona("security")!;
        Assert.Contains("decline", security.SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }

    // -- 2. Persona-name-collision routing -------------------------------

    [Fact]
    public void Route_ReviewTheSecurityOfThisCode_RoutesToCoder_DocumentedSurprise()
    {
        var config = DefaultConfig();
        var coordinator = new SquadCoordinator(config);

        // "review the security of this code" hits a THREE-WAY tie at
        // score 1: coder ("code"), reviewer ("review"), security
        // ("security"). LINQ OrderByDescending(Score) is stable so the
        // first matching rule in input order wins -- and `coder` is
        // declared first. The user's most natural phrasing for a
        // security review thus routes to the coder. Pin this surprise
        // here; logged as finding e31-routing-substring-coder-overshadow.
        var persona = coordinator.Route("review the security of this code");

        Assert.NotNull(persona);
        Assert.Equal("coder", persona.Name);
    }

    [Fact]
    public void Route_WriteASecurityAudit_RoutesToReviewer_DocumentedSurprise()
    {
        // "audit" (1) for reviewer; "security" (1) for security. Tie ->
        // reviewer wins by rule order. Note: "write" is NOT in writer's
        // pattern (which uses document/readme/docs/...) so writer scores
        // zero. This is itself a smell -- logged as finding.
        var config = DefaultConfig();
        var coordinator = new SquadCoordinator(config);

        var persona = coordinator.Route("write a security audit");

        Assert.NotNull(persona);
        Assert.Equal("reviewer", persona.Name);
    }

    // -- 3. Empty / whitespace persona name ------------------------------

    [Fact]
    public void GetPersona_EmptyString_ReturnsNull_DoesNotThrow()
    {
        var config = DefaultConfig();

        var persona = config.GetPersona("");

        Assert.Null(persona);
    }

    [Fact]
    public void GetPersona_WhitespaceOnly_ReturnsNull_DoesNotThrow()
    {
        var config = DefaultConfig();

        var persona = config.GetPersona("   ");

        Assert.Null(persona);
    }

    // -- 4. Unknown persona ----------------------------------------------

    [Fact]
    public void GetPersona_Unknown_ReturnsNull_NoSilentFallback()
    {
        // Lookup itself does NOT silently fall back. The CLI layer in
        // Program.cs:716-719 turns a null into a helpful "Unknown
        // persona '<x>'. Available: ..." error. That's the correct
        // shape -- record it.
        var config = DefaultConfig();

        var persona = config.GetPersona("doesnotexist");

        Assert.Null(persona);
    }

    [Fact]
    public void Route_AutoWithGibberishPrompt_FallsBackToFirstPersona_DocumentedSurprise()
    {
        // `--persona auto` with a prompt that matches no routing keywords
        // silently falls back to the first persona (coder). This is a
        // SILENT fallback for the auto-routing path, not the explicit
        // persona path. Surface it so callers know.
        var config = DefaultConfig();
        var coordinator = new SquadCoordinator(config);

        var persona = coordinator.Route("xyzzy plugh quux");

        Assert.NotNull(persona);
        Assert.Equal("coder", persona.Name);
    }

    // -- 5. Memory cap ---------------------------------------------------

    [Fact]
    public void Memory_ExceedingCap_TruncatesToDocumented32KB_KeepsTail()
    {
        // ADR-002: persona memory caps at 32 KB. Verify exactly that.
        var memory = new PersonaMemory(Path.Combine(_tempDir, ".squad"));
        memory.Initialize();

        // Stuff enough bytes to exceed 32 KB by a wide margin.
        for (int i = 0; i < 1000; i++)
        {
            memory.AppendHistory("coder", $"Task {i}", "R " + new string('Y', 200));
        }

        var history = memory.ReadHistory("coder");

        // Truncated content includes the marker + at most 32 KB of tail
        // (so total bound is marker length + 32_768).
        const int Cap = 32_768;
        const string Marker = "...(earlier history truncated)...\n";
        Assert.StartsWith(Marker, history);
        Assert.Equal(Marker.Length + Cap, history.Length);
        // Tail (newest entry) preserved.
        Assert.Contains("Task 999", history);
        // Head (oldest) gone.
        Assert.DoesNotContain("Task 0\n", history);
    }

    // -- 6. Casing normalization -----------------------------------------

    [Theory]
    [InlineData("coder")]
    [InlineData("Coder")]
    [InlineData("CODER")]
    [InlineData("CoDeR")]
    public void GetPersona_CasingVariants_AllNormalizeToSamePersona(string variant)
    {
        var config = DefaultConfig();

        var persona = config.GetPersona(variant);

        Assert.NotNull(persona);
        Assert.Equal("coder", persona.Name);
    }

    [Fact(Skip = "Behavior gap; tracked as finding e31-persona-name-no-kebab-snake-normalization")]
    public void GetPersona_KebabAndSnakeCaseVariantsOfMultiWordName_ShouldNormalize()
    {
        // None of the five generic personas are multi-word, but the
        // forthcoming S02E30 cast (e.g., `mr-pitt`, `mr_pitt`,
        // `MrPitt`, `mr pitt`) will hit this. GetPersona currently
        // does case-insensitive Equals only -- no separator
        // normalization. Pre-emptive coverage so the gap is logged
        // before the cast personas land.
        var config = DefaultConfig();
        config.Personas.Add(new PersonaConfig { Name = "mr-pitt", SystemPrompt = "You are exacting." });

        Assert.NotNull(config.GetPersona("mr_pitt"));
        Assert.NotNull(config.GetPersona("MrPitt"));
        Assert.NotNull(config.GetPersona("MR-PITT"));
    }

    // -- 7. Routing keyword overlap / tiebreak ---------------------------

    [Fact]
    public void Route_KeywordTiebreak_PreservesInputOrder_FirstRuleWins()
    {
        // Construct a config where two rules tie on score. Verify the
        // tiebreak is deterministic AND follows input order. This pins
        // the invariant; if it ever flips, the test fails loudly.
        var config = new SquadConfig
        {
            Personas = new List<PersonaConfig>
            {
                new() { Name = "alpha", SystemPrompt = "A" },
                new() { Name = "beta", SystemPrompt = "B" },
            },
            Routing = new List<RoutingRule>
            {
                new() { Pattern = "design", Persona = "alpha" },
                new() { Pattern = "design", Persona = "beta" },
            },
        };
        var coordinator = new SquadCoordinator(config);

        var persona = coordinator.Route("propose a design");

        Assert.NotNull(persona);
        Assert.Equal("alpha", persona.Name);
    }

    // -- 8. Empty / missing system prompt --------------------------------

    [Fact]
    public void GetPersona_WithEmptySystemPrompt_StillReturnsPersona_NoValidation()
    {
        // SquadConfig does NOT validate that SystemPrompt is non-empty.
        // A persona with an empty prompt would degenerate to the
        // default model behavior (no role anchoring). This is a gap
        // (validation) -- surface it.
        var config = new SquadConfig
        {
            Personas = new List<PersonaConfig>
            {
                new() { Name = "ghost", SystemPrompt = "" },
            },
        };

        var persona = config.GetPersona("ghost");

        Assert.NotNull(persona);
        Assert.Equal("", persona.SystemPrompt);
    }

    // -- 9. Tool-availability mismatch -----------------------------------

    [Fact]
    public void Architect_DeclaresTools_SystemPromptDoesNotMentionToolAvailability_DocumentedContradiction()
    {
        // The `architect` persona declares tools (file, web, datetime).
        // In standard mode (no --agent flag), Program.cs does not
        // expose those tools to the model -- yet the persona's prompt
        // never mentions whether tools are available. The model could
        // believe it has tools and "narrate" tool calls in plain text.
        // No fix here; just pin the current shape.
        var architect = DefaultConfig().GetPersona("architect")!;

        Assert.NotEmpty(architect.Tools);
        // System prompt does NOT clarify tool availability.
        Assert.DoesNotContain("tool", architect.SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }

    // -- 10. Concurrent persona invocations / memory isolation -----------

    [Fact]
    public async Task Memory_ConcurrentAppendsAcrossPersonas_NoCrossContamination()
    {
        var squadDir = Path.Combine(_tempDir, ".squad");
        var memory = new PersonaMemory(squadDir);
        memory.Initialize();

        // Two concurrent writers, distinct personas. The on-disk file
        // for each persona must contain ONLY its own task entries.
        var coderTask = Task.Run(() =>
        {
            for (int i = 0; i < 50; i++)
                memory.AppendHistory("coder", $"coder-task-{i}", $"coder-result-{i}");
        });
        var reviewerTask = Task.Run(() =>
        {
            for (int i = 0; i < 50; i++)
                memory.AppendHistory("reviewer", $"reviewer-task-{i}", $"reviewer-result-{i}");
        });

        await Task.WhenAll(coderTask, reviewerTask);

        var coderHistory = memory.ReadHistory("coder");
        var reviewerHistory = memory.ReadHistory("reviewer");

        Assert.Contains("coder-task-49", coderHistory);
        Assert.DoesNotContain("reviewer-task-", coderHistory);
        Assert.Contains("reviewer-task-49", reviewerHistory);
        Assert.DoesNotContain("coder-task-", reviewerHistory);
    }

    // -- 11. Persona + Ralph mode interaction ----------------------------

    [Fact(Skip = "Integration surface; gap tracked as finding e31-persona-ralph-composition-untested")]
    public void PersonaCoder_PlusRalphMode_PreservesPersonaPrompt_AppendsRalphAppendix()
    {
        // The composition lives in Program.cs:1717-1721 and runs
        // inside the Ralph loop, which calls Azure OpenAI. There is
        // no seam to test the composition without a network call.
        // Cross-references S02E18 finding e18-ralph-mode-temperature-
        // inheritance: when a persona is active in Ralph mode, the
        // ralph-default temperature (0.55) is also not honored.
        // Logged as gap; needs a code seam (extract prompt
        // composition into a pure helper) before it's testable.
        Assert.True(false);
    }

    // -- 12. Persona + agent mode interaction ----------------------------

    [Fact(Skip = "Integration surface; gap tracked as finding e31-persona-agent-tool-override-untested")]
    public void PersonaReviewer_PlusAgentMode_OverridesEnabledToolsToReviewerTools()
    {
        // Program.cs:735-738 mutates opts.EnabledTools when a persona
        // declares a tool list. Reviewer declares ["file", "shell"];
        // a user passing --tools=web alongside --persona reviewer will
        // silently lose `web` access. This is intentional per design
        // but undocumented and untested. No seam to assert without
        // refactoring opts handling. Logged as gap.
        Assert.True(false);
    }

    // -- Bonus coverage: per-persona prompt content sanity ---------------

    [Theory]
    [InlineData("coder")]
    [InlineData("reviewer")]
    [InlineData("architect")]
    [InlineData("writer")]
    [InlineData("security")]
    public void AllFiveGenericPersonas_HaveNonEmptyPromptAndAtLeastOneRoutingRule(string name)
    {
        var config = DefaultConfig();

        var persona = config.GetPersona(name);
        Assert.NotNull(persona);
        Assert.False(string.IsNullOrWhiteSpace(persona.SystemPrompt));
        Assert.False(string.IsNullOrWhiteSpace(persona.Role));

        var rule = config.Routing.FirstOrDefault(r =>
            r.Persona.Equals(name, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(rule);
        Assert.False(string.IsNullOrWhiteSpace(rule.Pattern));
    }
}
