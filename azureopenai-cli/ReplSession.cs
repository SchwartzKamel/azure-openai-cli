namespace AzureOpenAI_CLI;

/// <summary>
/// Represents a single message in the REPL session.
/// </summary>
public class SessionMessage
{
    public string Role { get; set; } = string.Empty;  // "system", "user", or "assistant"
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents the exit result of a REPL session.
/// </summary>
public class SessionExitResult
{
    public int ExitCode { get; set; }
    public string ExitReason { get; set; } = string.Empty;
    public bool WasClean { get; set; }
}

/// <summary>
/// Manages the state of an interactive REPL session.
/// </summary>
public class ReplSession
{
    private readonly List<SessionMessage> _messages = new();
    private bool _isActive = false;
    private string? _systemPrompt;
    private int _maxTurns = 100;

    public IReadOnlyList<SessionMessage> Messages => _messages.AsReadOnly();
    public bool IsActive => _isActive;
    public string? SystemPrompt => _systemPrompt;
    public int MaxTurns => _maxTurns;

    /// <summary>
    /// Starts a new REPL session.
    /// </summary>
    public void Start(string? systemPrompt = null)
    {
        _isActive = true;
        _messages.Clear();
        _systemPrompt = systemPrompt;

        if (!string.IsNullOrEmpty(systemPrompt))
        {
            _messages.Add(new SessionMessage
            {
                Role = "system",
                Content = systemPrompt,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Adds a user message to the session.
    /// </summary>
    public void AddUserMessage(string content)
    {
        if (!_isActive)
            throw new InvalidOperationException("Session is not active.");

        _messages.Add(new SessionMessage
        {
            Role = "user",
            Content = content,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Adds an assistant message to the session.
    /// </summary>
    public void AddAssistantMessage(string content)
    {
        if (!_isActive)
            throw new InvalidOperationException("Session is not active.");

        _messages.Add(new SessionMessage
        {
            Role = "assistant",
            Content = content,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Updates the system prompt during the session.
    /// </summary>
    public void SetSystemPrompt(string systemPrompt)
    {
        if (!_isActive)
            throw new InvalidOperationException("Session is not active.");

        _systemPrompt = systemPrompt;

        // Update or add the system message
        var existingSystem = _messages.FirstOrDefault(m => m.Role == "system");
        if (existingSystem != null)
        {
            existingSystem.Content = systemPrompt;
            existingSystem.Timestamp = DateTime.UtcNow;
        }
        else
        {
            _messages.Insert(0, new SessionMessage
            {
                Role = "system",
                Content = systemPrompt,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Gets the recent message history (last N turns).
    /// A turn is a user message followed by an assistant message.
    /// </summary>
    public IReadOnlyList<SessionMessage> GetHistory(int lastNTurns = 10)
    {
        // Count turns (user + assistant pairs)
        var nonSystemMessages = _messages.Where(m => m.Role != "system").ToList();
        int messagesToTake = Math.Min(lastNTurns * 2, nonSystemMessages.Count);
        
        return nonSystemMessages.TakeLast(messagesToTake).ToList().AsReadOnly();
    }

    /// <summary>
    /// Clears the session memory (keeps system prompt).
    /// </summary>
    public void ClearHistory()
    {
        if (!_isActive)
            throw new InvalidOperationException("Session is not active.");

        var systemMessage = _messages.FirstOrDefault(m => m.Role == "system");
        _messages.Clear();
        
        if (systemMessage != null)
        {
            _messages.Add(systemMessage);
        }
    }

    /// <summary>
    /// Ends the session cleanly.
    /// </summary>
    public SessionExitResult End(string reason = "quit")
    {
        _isActive = false;
        return new SessionExitResult
        {
            ExitCode = 0,
            ExitReason = reason,
            WasClean = true
        };
    }

    /// <summary>
    /// Ends the session with an error.
    /// </summary>
    public SessionExitResult EndWithError(string errorMessage)
    {
        _isActive = false;
        return new SessionExitResult
        {
            ExitCode = 1,
            ExitReason = errorMessage,
            WasClean = false
        };
    }

    /// <summary>
    /// Checks if the exit command is valid.
    /// </summary>
    public static bool IsExitCommand(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        string trimmed = input.Trim().ToLowerInvariant();
        return trimmed == "exit" || trimmed == "/quit" || trimmed == "/exit";
    }

    /// <summary>
    /// Gets the count of user/assistant turns (not including system).
    /// </summary>
    public int GetTurnCount()
    {
        return _messages.Count(m => m.Role == "user");
    }

    /// <summary>
    /// Checks if the session is near the context limit.
    /// </summary>
    public bool IsNearLimit()
    {
        return GetTurnCount() >= MaxTurns - 5;
    }
}
