namespace AzureOpenAI_CLI.Squad;

/// <summary>
/// Manages persistent memory for each persona.
/// History is stored in .squad/history/{name}.md — one file per persona.
/// Each session appends learnings. Next session reads the full history.
/// </summary>
internal sealed class PersonaMemory
{
    private const string SquadDir = ".squad";
    private const string HistoryDir = "history";
    private const string DecisionsFile = "decisions.md";
    private const int MaxHistoryBytes = 32_768; // 32 KB per persona

    private readonly string _baseDir;

    public PersonaMemory(string? baseDir = null)
    {
        _baseDir = baseDir ?? Path.Combine(Directory.GetCurrentDirectory(), SquadDir);
    }

    /// <summary>
    /// Read the accumulated history for a persona.
    /// Returns empty string if no history exists.
    /// </summary>
    public string ReadHistory(string personaName)
    {
        var path = GetHistoryPath(personaName);
        if (!File.Exists(path))
            return "";

        var content = File.ReadAllText(path);
        // Truncate to max size (keep the tail — most recent learnings)
        if (content.Length > MaxHistoryBytes)
            content = "...(earlier history truncated)...\n" + content[^MaxHistoryBytes..];

        return content;
    }

    /// <summary>
    /// Append a session entry to persona history.
    /// </summary>
    public void AppendHistory(string personaName, string task, string summary)
    {
        var path = GetHistoryPath(personaName);
        var dir = Path.GetDirectoryName(path);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var entry = $"\n## Session — {DateTime.UtcNow:yyyy-MM-dd HH:mm UTC}\n" +
                    $"**Task:** {Truncate(task, 200)}\n" +
                    $"**Result:** {Truncate(summary, 500)}\n";

        File.AppendAllText(path, entry);
    }

    /// <summary>
    /// Log a decision to the shared decisions file.
    /// </summary>
    public void LogDecision(string personaName, string decision)
    {
        var path = Path.Combine(_baseDir, DecisionsFile);
        if (!Directory.Exists(_baseDir))
            Directory.CreateDirectory(_baseDir);

        var entry = $"\n### {DateTime.UtcNow:yyyy-MM-dd HH:mm UTC} — {personaName}\n{decision}\n";
        File.AppendAllText(path, entry);
    }

    /// <summary>
    /// Read shared decisions log.
    /// </summary>
    public string ReadDecisions()
    {
        var path = Path.Combine(_baseDir, DecisionsFile);
        if (!File.Exists(path))
            return "";

        var content = File.ReadAllText(path);
        if (content.Length > MaxHistoryBytes)
            content = "...(earlier decisions truncated)...\n" + content[^MaxHistoryBytes..];
        return content;
    }

    /// <summary>
    /// Check if the .squad directory exists (has been initialized).
    /// </summary>
    public bool IsInitialized() => Directory.Exists(_baseDir);

    /// <summary>
    /// Initialize the .squad directory structure.
    /// </summary>
    public void Initialize()
    {
        Directory.CreateDirectory(_baseDir);
        Directory.CreateDirectory(Path.Combine(_baseDir, HistoryDir));

        // Create decisions file header if it doesn't exist
        var decisionsPath = Path.Combine(_baseDir, DecisionsFile);
        if (!File.Exists(decisionsPath))
            File.WriteAllText(decisionsPath, "# Squad Decisions\n\nShared decision log across all personas.\n");
    }

    private string GetHistoryPath(string personaName) =>
        Path.Combine(_baseDir, HistoryDir, $"{personaName.ToLowerInvariant()}.md");

    private static string Truncate(string text, int maxLen) =>
        text.Length <= maxLen ? text : text[..maxLen] + "...";
}
