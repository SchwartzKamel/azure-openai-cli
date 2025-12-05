namespace AzureOpenAI_CLI;

/// <summary>
/// Represents the result of parsing a slash command.
/// </summary>
public class SlashCommandResult
{
    public bool IsSlashCommand { get; set; }
    public string CommandName { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Parses and validates slash commands for the REPL mode.
/// Slash commands start with '/' and are processed locally, not sent to the model.
/// </summary>
public static class SlashCommandParser
{
    private static readonly HashSet<string> ValidCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "help",
        "system",
        "history",
        "clear",
        "export",
        "import",
        "merge",
        "quit",
        "exit"
    };

    /// <summary>
    /// Parses a line of input to determine if it's a slash command and extracts its components.
    /// </summary>
    /// <param name="input">The raw input line from the user.</param>
    /// <returns>A SlashCommandResult containing the parsed command details.</returns>
    public static SlashCommandResult Parse(string? input)
    {
        var result = new SlashCommandResult();

        if (string.IsNullOrWhiteSpace(input))
        {
            result.IsSlashCommand = false;
            return result;
        }

        string trimmed = input.Trim();

        // Check if it starts with '/'
        if (!trimmed.StartsWith('/'))
        {
            result.IsSlashCommand = false;
            return result;
        }

        result.IsSlashCommand = true;

        // Remove the leading '/'
        string commandPart = trimmed[1..];

        if (string.IsNullOrWhiteSpace(commandPart))
        {
            result.IsValid = false;
            result.ErrorMessage = "Empty command. Type /help for available commands.";
            return result;
        }

        // Split into command and arguments
        int spaceIndex = commandPart.IndexOf(' ');
        if (spaceIndex == -1)
        {
            result.CommandName = commandPart.ToLowerInvariant();
            result.Arguments = string.Empty;
        }
        else
        {
            result.CommandName = commandPart[..spaceIndex].ToLowerInvariant();
            result.Arguments = commandPart[(spaceIndex + 1)..].Trim();
        }

        // Validate the command
        if (ValidCommands.Contains(result.CommandName))
        {
            result.IsValid = true;
        }
        else
        {
            result.IsValid = false;
            result.ErrorMessage = $"Unknown command '/{result.CommandName}'. Type /help for available commands.";
        }

        return result;
    }

    /// <summary>
    /// Gets the list of valid slash commands.
    /// </summary>
    public static IReadOnlyCollection<string> GetValidCommands() => ValidCommands;
}
