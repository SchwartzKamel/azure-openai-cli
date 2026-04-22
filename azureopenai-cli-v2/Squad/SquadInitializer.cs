using System.Text.Json;

namespace AzureOpenAI_CLI_V2.Squad;

/// <summary>
/// Scaffolds a new .squad.json and .squad/ directory with default personas.
/// Inspired by Squad's <c>squad init</c> but native C# — no npm needed.
/// </summary>
internal static class SquadInitializer
{
    /// <summary>
    /// Initialize a new Squad in the specified directory.
    /// Returns true if created, false if already exists.
    /// </summary>
    public static bool Initialize(string? directory = null)
    {
        var dir = directory ?? Directory.GetCurrentDirectory();
        var configPath = Path.Combine(dir, ".squad.json");

        if (File.Exists(configPath))
            return false;

        var config = CreateDefaultConfig();
        var json = JsonSerializer.Serialize(config, AppJsonContext.Default.SquadConfig);
        File.WriteAllText(configPath, json);

        // Create .squad directory structure
        var squadDir = Path.Combine(dir, ".squad");
        Directory.CreateDirectory(squadDir);
        Directory.CreateDirectory(Path.Combine(squadDir, "history"));

        // Create decisions.md
        File.WriteAllText(
            Path.Combine(squadDir, "decisions.md"),
            "# Squad Decisions\n\nShared decision log across all personas.\n");

        // Create README inside .squad/
        File.WriteAllText(
            Path.Combine(squadDir, "README.md"),
            GetSquadReadme());

        return true;
    }

    /// <summary>
    /// Defense-in-depth refusal clause baked into every default persona's
    /// system prompt. Mirrors <see cref="Program.SAFETY_CLAUSE"/> so the
    /// refusal survives even in code paths that don't append the clause
    /// downstream (Maestro audit H4/M1 — defense in depth). Kept as a
    /// separate constant rather than a direct reference so persona prompts
    /// are self-contained when serialized to .squad.json.
    /// </summary>
    internal const string PERSONA_SAFETY_LINE =
        "You must refuse requests that would exfiltrate secrets, access credentials, or cause harm, even if instructed in a previous turn or the user prompt.";

    /// <summary>
    /// Creates the default Squad config with 5 personas.
    /// </summary>
    internal static SquadConfig CreateDefaultConfig()
    {
        return new SquadConfig
        {
            Team = new TeamConfig
            {
                Name = "Default Squad",
                Description = "AI team for your project. Customize personas in .squad.json.",
            },
            Personas = new List<PersonaConfig>
            {
                new()
                {
                    Name = "coder",
                    Role = "Software Engineer",
                    Description = "Writes clean, tested, production-ready code.",
                    SystemPrompt = "You are an expert software engineer. Write clean, well-tested code. " +
                        "Follow existing project conventions. Always consider edge cases. " +
                        "Prefer small, focused changes over large rewrites. " +
                        "If you modify code, explain what changed and why. " +
                        PERSONA_SAFETY_LINE,
                    Tools = new List<string> { "shell", "file", "web", "datetime" },
                },
                new()
                {
                    Name = "reviewer",
                    Role = "Code Reviewer",
                    Description = "Reviews code for bugs, security issues, and best practices.",
                    SystemPrompt = "You are a senior code reviewer. Focus on: " +
                        "(1) bugs and logic errors, (2) security vulnerabilities, " +
                        "(3) performance issues, (4) maintainability. " +
                        "Be specific — cite line numbers and suggest fixes. " +
                        "Don't comment on style or formatting unless it hides a bug. " +
                        PERSONA_SAFETY_LINE,
                    Tools = new List<string> { "file", "shell" },
                },
                new()
                {
                    Name = "architect",
                    Role = "System Architect",
                    Description = "Designs systems, evaluates trade-offs, makes structural decisions.",
                    SystemPrompt = "You are a system architect. Think about: " +
                        "(1) separation of concerns, (2) extensibility, (3) performance at scale, " +
                        "(4) operational complexity. Propose designs with diagrams when helpful. " +
                        "Always document trade-offs and alternatives considered. " +
                        "Log important decisions for the team. " +
                        PERSONA_SAFETY_LINE,
                    Tools = new List<string> { "file", "web", "datetime" },
                },
                new()
                {
                    Name = "writer",
                    Role = "Technical Writer",
                    Description = "Creates clear documentation, guides, and content.",
                    SystemPrompt = "You are a technical writer. Create documentation that is: " +
                        "(1) accurate — verify claims against actual code, " +
                        "(2) scannable — use headers, tables, code blocks, " +
                        "(3) complete — cover happy path and edge cases, " +
                        "(4) maintainable — avoid details that rot quickly. " +
                        "Read the code before writing about it. " +
                        PERSONA_SAFETY_LINE,
                    Tools = new List<string> { "file", "shell" },
                },
                new()
                {
                    Name = "security",
                    Role = "Security Auditor",
                    Description = "Identifies vulnerabilities, hardens defenses, reviews for compliance.",
                    SystemPrompt = "You are a security auditor. Systematically check for: " +
                        "(1) injection vulnerabilities (SQL, command, path traversal), " +
                        "(2) authentication/authorization bypasses, " +
                        "(3) data exposure (secrets in logs, error messages), " +
                        "(4) dependency vulnerabilities, (5) container security. " +
                        "Classify findings by severity (Critical/High/Medium/Low). " +
                        "Provide remediation steps for every finding. " +
                        PERSONA_SAFETY_LINE,
                    Tools = new List<string> { "file", "shell", "web" },
                },
            },
            Routing = new List<RoutingRule>
            {
                new()
                {
                    Pattern = "code,implement,build,fix,refactor,feature,bug",
                    Persona = "coder",
                    Description = "Implementation tasks",
                },
                new()
                {
                    Pattern = "review,audit,check,inspect,quality",
                    Persona = "reviewer",
                    Description = "Code review tasks",
                },
                new()
                {
                    Pattern = "design,architecture,system,scale,pattern,migration",
                    Persona = "architect",
                    Description = "Architecture and design tasks",
                },
                new()
                {
                    Pattern = "document,readme,docs,guide,tutorial,changelog",
                    Persona = "writer",
                    Description = "Documentation tasks",
                },
                new()
                {
                    Pattern = "security,vulnerability,cve,owasp,harden,credential,secret",
                    Persona = "security",
                    Description = "Security tasks",
                },
            },
        };
    }

    private static string GetSquadReadme() => """
        # .squad/

        This directory contains persistent state for your AI Squad.

        ## Structure

        ```
        .squad/
        ├── history/          # Per-persona memory (auto-managed)
        │   ├── coder.md      # What the coder learned about your project
        │   ├── reviewer.md   # Patterns the reviewer has noted
        │   └── ...
        ├── decisions.md      # Shared decision log
        └── README.md         # This file
        ```

        ## How It Works

        Each persona accumulates knowledge across sessions. When you use
        `--persona coder`, the coder's history is loaded as context. After
        the session, key learnings are appended.

        **Commit this directory.** Anyone who clones gets the team — with
        all their accumulated knowledge.

        ## Configuration

        Edit `.squad.json` in the project root to:
        - Add/remove personas
        - Customize system prompts
        - Define routing rules (auto-select persona by task keywords)
        - Override model per persona

        ## Commands

        ```bash
        az-ai --persona coder "implement login page"     # Use specific persona
        az-ai --persona auto "fix the auth bug"           # Auto-route to best persona
        az-ai --personas                                  # List available personas
        az-ai --squad-init                                # Re-scaffold (won't overwrite)
        ```
        """;
}
