using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using Azure.AI.OpenAI.Chat;
using OpenAI.Chat;
using dotenv.net;
using AzureOpenAI_CLI;
using AzureOpenAI_CLI.Tools;

[assembly: InternalsVisibleTo("AzureOpenAI_CLI.Tests")]

class Program
{
    // Security: Cap prompt size to prevent abuse and excessive API costs
    private const int MAX_PROMPT_LENGTH = 32000;
    private const int MAX_STDIN_BYTES = 1_048_576;
    private const int DEFAULT_TIMEOUT_SECONDS = 120;
    private const int DEFAULT_MAX_AGENT_ROUNDS = 5;
    private const float DEFAULT_TEMPERATURE = 0.55f;
    private const int DEFAULT_MAX_TOKENS = 10000;
    private const string DEFAULT_SYSTEM_PROMPT = "You are a secure, concise CLI assistant. Keep answers factual, no fluff.";

    /// <summary>
    /// Holds parsed CLI flag values, separated from the remaining positional arguments.
    /// </summary>
    private record CliOptions(
        float? Temperature,
        int? MaxTokens,
        string? SystemPrompt,
        bool ShowConfig,
        bool AgentMode,
        int MaxAgentRounds,
        HashSet<string>? EnabledTools,
        string? JsonSchema,
        string[] RemainingArgs);

    /// <summary>
    /// Represents a CLI flag parse failure with the error message and exit code.
    /// </summary>
    private record CliParseError(string Message, int ExitCode);

    /// <summary>
    /// Parses all CLI flags (--temperature, --max-tokens, --system, --config, --agent, --tools,
    /// --max-rounds) out of the argument array. Returns either a populated CliOptions or a
    /// CliParseError if a flag is malformed.
    /// </summary>
    static (CliOptions? Options, CliParseError? Error) ParseCliFlags(string[] args)
    {
        float? cliTemperature = null;
        int? cliMaxTokens = null;
        string? cliSystemPrompt = null;
        bool showConfig = false;
        bool agentMode = false;
        int maxAgentRounds = DEFAULT_MAX_AGENT_ROUNDS;
        HashSet<string>? enabledTools = null;
        string? jsonSchema = null;
        var cleanedArgs = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i].ToLowerInvariant();
            if (arg == "--temperature" || arg == "-t")
            {
                if (i + 1 < args.Length && float.TryParse(args[i + 1],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float temp))
                {
                    cliTemperature = temp;
                    i++;
                }
                else
                {
                    return (null, new CliParseError("[ERROR] --temperature requires a numeric value (e.g., --temperature 0.7)", 1));
                }
            }
            else if (arg == "--max-tokens")
            {
                if (i + 1 < args.Length && int.TryParse(args[i + 1], out int tokens))
                {
                    cliMaxTokens = tokens;
                    i++;
                }
                else
                {
                    return (null, new CliParseError("[ERROR] --max-tokens requires an integer value (e.g., --max-tokens 5000)", 1));
                }
            }
            else if (arg == "--system")
            {
                if (i + 1 < args.Length)
                {
                    cliSystemPrompt = args[i + 1];
                    i++;
                }
                else
                {
                    return (null, new CliParseError("[ERROR] --system requires a value (e.g., --system \"You are a pirate\")", 1));
                }
            }
            else if (arg == "--config")
            {
                if (i + 1 < args.Length && args[i + 1].ToLowerInvariant() == "show")
                {
                    showConfig = true;
                    i++;
                }
                else
                {
                    return (null, new CliParseError("[ERROR] Unknown --config subcommand. Usage: --config show", 1));
                }
            }
            else if (arg == "--agent")
            {
                agentMode = true;
            }
            else if (arg == "--max-rounds")
            {
                if (i + 1 < args.Length && int.TryParse(args[i + 1], out int rounds) && rounds > 0 && rounds <= 20)
                {
                    maxAgentRounds = rounds;
                    i++;
                }
                else
                {
                    return (null, new CliParseError("[ERROR] --max-rounds requires an integer 1-20", 1));
                }
            }
            else if (arg == "--tools")
            {
                if (i + 1 < args.Length)
                {
                    enabledTools = new HashSet<string>(
                        args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                        StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    return (null, new CliParseError("[ERROR] --tools requires comma-separated tool names (e.g., --tools shell,file,web)", 1));
                }
            }
            else if (arg == "--schema")
            {
                if (i + 1 < args.Length)
                {
                    var schemaStr = args[i + 1];
                    i++;
                    try
                    {
                        JsonDocument.Parse(schemaStr);
                        jsonSchema = schemaStr;
                    }
                    catch (JsonException ex)
                    {
                        return (null, new CliParseError($"[ERROR] Invalid JSON schema: {ex.Message}", 1));
                    }
                }
                else
                {
                    return (null, new CliParseError("[ERROR] --schema requires a JSON schema string", 1));
                }
            }
            else
            {
                cleanedArgs.Add(args[i]);
            }
        }

        return (new CliOptions(cliTemperature, cliMaxTokens, cliSystemPrompt, showConfig,
            agentMode, maxAgentRounds, enabledTools, jsonSchema, cleanedArgs.ToArray()), null);
    }

    /// <summary>
    /// Validates that the Azure OpenAI credentials (API key, endpoint, deployment model)
    /// are properly configured. Returns the validated endpoint URI, API key, and deployment name.
    /// Throws on missing API key or endpoint; returns an error message for invalid endpoint URLs.
    /// </summary>
    static (Uri endpoint, string apiKey, string deploymentName, string? errorMessage) ValidateConfiguration(
        string? azureOpenAiEndpoint, string? azureOpenAiApiKey, UserConfig config)
    {
        // Early validation: API key must not be empty/whitespace
        if (string.IsNullOrWhiteSpace(azureOpenAiApiKey))
            throw new ArgumentNullException(nameof(azureOpenAiApiKey), "Azure OpenAI API key is not set.");

        // Early validation: endpoint must be a valid URL
        if (string.IsNullOrWhiteSpace(azureOpenAiEndpoint))
            throw new ArgumentNullException(nameof(azureOpenAiEndpoint), "Azure OpenAI endpoint is not set.");
        if (!Uri.TryCreate(azureOpenAiEndpoint, UriKind.Absolute, out var endpoint)
            || endpoint.Scheme != "https")
        {
            return (null!, null!, null!, $"Invalid endpoint URL: '{azureOpenAiEndpoint}'. Must be a valid HTTPS URL.");
        }

        // Use the active model from user config, falling back to the first model in the list
        var deploymentName = config.ActiveModel
            ?? config.AvailableModels.FirstOrDefault()
            ?? throw new InvalidOperationException("Azure OpenAI model is not set. Configure AZUREOPENAIMODEL in your .env file.");

        return (endpoint, azureOpenAiApiKey, deploymentName, null);
    }

    /// <summary>
    /// Resolves the effective temperature, max tokens, timeout, and system prompt by applying
    /// the precedence chain: CLI flags > UserConfig > Env vars > Defaults.
    /// </summary>
    static (float temperature, int maxTokens, int timeoutSeconds, string systemPrompt) GetEffectiveConfig(
        CliOptions opts, UserConfig config, string? envSystemPrompt)
    {
        float temperature = opts.Temperature ?? config.Temperature ?? TryParseEnvFloat("AZURE_TEMPERATURE", DEFAULT_TEMPERATURE);
        int maxTokens = opts.MaxTokens ?? config.MaxTokens ?? TryParseEnvInt("AZURE_MAX_TOKENS", DEFAULT_MAX_TOKENS);
        int timeoutSeconds = config.TimeoutSeconds ?? TryParseEnvInt("AZURE_TIMEOUT", DEFAULT_TIMEOUT_SECONDS);
        string systemPrompt = opts.SystemPrompt ?? config.SystemPrompt ?? envSystemPrompt ?? DEFAULT_SYSTEM_PROMPT;
        return (temperature, maxTokens, timeoutSeconds, systemPrompt);
    }

    static async Task<int> Main(string[] args)
    {
        // Detect --json flag anywhere in args
        bool jsonMode = args.Contains("--json");
        if (jsonMode)
        {
            args = args.Where(a => a != "--json").ToArray();
        }

        // Parse preference and config flags (removed from args before prompt building)
        var (opts, parseError) = ParseCliFlags(args);
        if (parseError != null)
        {
            Console.Error.WriteLine(parseError.Message);
            return parseError.ExitCode;
        }
        args = opts!.RemainingArgs;

        try
        {
            // Always load from the baked‑in .env file
            DotEnv.Load(new DotEnvOptions(
                envFilePaths: new[] { ".env" },
                overwriteExistingVars: true,
                trimValues: true
            ));

            // Load user configuration
            var config = UserConfig.Load();

            // Get environment variables
            string? azureOpenAiEndpoint = Environment.GetEnvironmentVariable("AZUREOPENAIENDPOINT");
            string? azureOpenAiModel = Environment.GetEnvironmentVariable("AZUREOPENAIMODEL");
            string? azureOpenAiApiKey = Environment.GetEnvironmentVariable("AZUREOPENAIAPI");
            string? envSystemPrompt = Environment.GetEnvironmentVariable("SYSTEMPROMPT");

            // Initialize available models from environment (supports comma-separated list)
            config.InitializeFromEnvironment(azureOpenAiModel);

            // Handle --config show (does not require Azure credentials)
            if (opts.ShowConfig)
            {
                return ShowEffectiveConfig(config, opts.Temperature, opts.MaxTokens, opts.SystemPrompt,
                    azureOpenAiEndpoint, config.ActiveModel ?? config.AvailableModels.FirstOrDefault());
            }

            // Handle model management commands
            if (args.Length > 0)
            {
                int commandResult = HandleModelCommands(args, config);
                if (commandResult != -1)
                {
                    return commandResult;
                }
            }

            // Build prompt from args and/or stdin
            string argsPrompt = args.Length > 0 ? string.Join(' ', args) : "";
            string? stdinContent = null;

            if (Console.IsInputRedirected && Console.In.Peek() != -1)
            {
                char[] buffer = new char[MAX_STDIN_BYTES];
                int charsRead = Console.In.ReadBlock(buffer, 0, MAX_STDIN_BYTES);
                if (Console.In.Peek() != -1)
                {
                    Console.Error.WriteLine("Error: stdin input exceeds 1 MB limit.");
                    return 1;
                }
                stdinContent = new string(buffer, 0, charsRead);
                if (string.IsNullOrWhiteSpace(stdinContent))
                    stdinContent = null;
            }

            string userPrompt;
            if (!string.IsNullOrEmpty(argsPrompt) && stdinContent != null)
            {
                userPrompt = $"{stdinContent}\n\n{argsPrompt}";
            }
            else if (stdinContent != null)
            {
                userPrompt = stdinContent;
            }
            else if (!string.IsNullOrEmpty(argsPrompt))
            {
                userPrompt = argsPrompt;
            }
            else
            {
                if (jsonMode)
                {
                    OutputJsonError("No prompt provided. Pass a prompt as arguments or pipe via stdin.", 1);
                    return 1;
                }
                ShowUsage();
                return 1;
            }

            // Validate prompt length to prevent abuse and excessive API costs
            if (userPrompt.Length > MAX_PROMPT_LENGTH)
            {
                var msg = $"Prompt too long ({userPrompt.Length} chars). Maximum allowed is {MAX_PROMPT_LENGTH} chars.";
                if (jsonMode)
                {
                    OutputJsonError(msg, 1);
                    return 1;
                }
                Console.Error.WriteLine($"[ERROR] {msg}");
                return 1;
            }

            // Validate Azure credentials and endpoint
            var (endpoint, apiKey, deploymentName, validationError) = ValidateConfiguration(
                azureOpenAiEndpoint, azureOpenAiApiKey, config);
            if (validationError != null)
            {
                if (jsonMode)
                {
                    OutputJsonError(validationError, 1);
                    return 1;
                }
                Console.Error.WriteLine($"[ERROR] {validationError}");
                return 1;
            }

            AzureOpenAIClient azureClient = new(
                endpoint,
                new AzureKeyCredential(apiKey));
            ChatClient chatClient = azureClient.GetChatClient(deploymentName);

            // Apply precedence: CLI flags > UserConfig > Env vars > Defaults
            var (temperature, maxTokens, timeoutSeconds, effectiveSystemPrompt) =
                GetEffectiveConfig(opts, config, envSystemPrompt);

            var requestOptions = new ChatCompletionOptions()
            {
                MaxOutputTokenCount = maxTokens,
                Temperature = temperature,
                TopP = 1.0f,
                FrequencyPenalty = 0.0f,
                PresencePenalty = 0.0f,
            };

            // Apply structured output schema if provided (already validated as valid JSON in ParseCliFlags)
            if (opts.JsonSchema != null)
            {
                requestOptions.ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    "structured_output",
                    BinaryData.FromString(opts.JsonSchema),
                    jsonSchemaIsStrict: true);
            }

            List<ChatMessage> messages = new List<ChatMessage>()
            {
                new SystemChatMessage(effectiveSystemPrompt),
                new UserChatMessage(userPrompt),
            };

            // === AGENTIC MODE ===
            if (opts.AgentMode)
            {
                return await RunAgentLoop(chatClient, messages, requestOptions,
                    deploymentName, opts.EnabledTools, opts.MaxAgentRounds, jsonMode, timeoutSeconds);
            }

            // === STANDARD MODE (single-shot streaming) ===
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            // JSON mode: collect tokens and measure duration instead of streaming to stdout
            var stopwatch = jsonMode ? Stopwatch.StartNew() : null;
            var responseBuilder = jsonMode ? new StringBuilder() : null;

            // Start spinner on stderr while waiting for first token (not in JSON mode)
            using var spinnerCts = new CancellationTokenSource();
            bool showSpinner = !jsonMode && !Console.IsErrorRedirected;
            Task? spinnerTask = null;

            if (showSpinner)
            {
                var spinnerChars = new[] { '⠋', '⠙', '⠹', '⠸', '⠼', '⠴', '⠦', '⠧', '⠇', '⠏' };
                spinnerTask = Task.Run(async () =>
                {
                    int i = 0;
                    while (!spinnerCts.Token.IsCancellationRequested)
                    {
                        Console.Error.Write($"\r{spinnerChars[i % spinnerChars.Length]} Thinking...");
                        i++;
                        try { await Task.Delay(80, spinnerCts.Token); }
                        catch (OperationCanceledException) { break; }
                    }
                });
            }

            bool firstToken = true;
            // Retry loop for transient API errors in streaming mode
            int streamAttempt = 0;
            const int maxStreamRetries = 3;
            try
            {
                while (true)
                {
                    try
                    {
                        var response = chatClient.CompleteChatStreaming(messages, requestOptions, cts.Token);
                        foreach (StreamingChatCompletionUpdate update in response)
                        {
                            foreach (ChatMessageContentPart updatePart in update.ContentUpdate)
                            {
                                if (firstToken)
                                {
                                    firstToken = false;
                                    if (showSpinner)
                                    {
                                        spinnerCts.Cancel();
                                        if (spinnerTask != null) await spinnerTask;
                                        Console.Error.Write("\r              \r"); // clear spinner line
                                    }
                                }
                                if (jsonMode)
                                {
                                    responseBuilder!.Append(updatePart.Text);
                                }
                                else
                                {
                                    System.Console.Write(updatePart.Text);
                                }
                            }
                        }
                        break; // Success — exit retry loop
                    }
                    catch (RequestFailedException ex) when (firstToken && streamAttempt < maxStreamRetries && (ex.Status == 429 || ex.Status >= 500))
                    {
                        streamAttempt++;
                        var delay = TimeSpan.FromSeconds(Math.Pow(2, streamAttempt - 1));
                        if (!Console.IsErrorRedirected)
                            Console.Error.Write($"\r⏳ Retry {streamAttempt}/{maxStreamRetries} in {delay.TotalSeconds:F0}s...");
                        await Task.Delay(delay, cts.Token);
                    }
                }
            }
            finally
            {
                // Always clean up spinner, even on exception
                if (showSpinner && spinnerTask != null && !spinnerCts.IsCancellationRequested)
                {
                    spinnerCts.Cancel();
                    try { await spinnerTask; } catch { }
                    Console.Error.Write("\r              \r");
                }
            }

            // If no tokens arrived, still clean up the spinner
            if (firstToken && showSpinner)
            {
                if (!spinnerCts.IsCancellationRequested)
                {
                    spinnerCts.Cancel();
                    if (spinnerTask != null) await spinnerTask;
                }
                Console.Error.Write("\r              \r");
            }

            if (jsonMode)
            {
                stopwatch!.Stop();
                var jsonOutput = new
                {
                    model = deploymentName,
                    response = responseBuilder!.ToString(),
                    duration_ms = stopwatch.ElapsedMilliseconds
                };
                var options = new JsonSerializerOptions { WriteIndented = true };
                Console.WriteLine(JsonSerializer.Serialize(jsonOutput, options));
            }
            else
            {
                System.Console.WriteLine("");
            }

            return 0;
        }
        catch (RequestFailedException ex)
        {
            int status = ex.Status;
            string detail = status switch
            {
                401 => "Authentication failed — check your AZUREOPENAIAPI key.",
                403 => "Access denied — your key may lack permissions for this resource.",
                404 => "Resource not found — verify your AZUREOPENAIENDPOINT and model deployment name.",
                429 => "Rate limited — too many requests. Wait and retry.",
                _ => ex.Message,
            };
            if (jsonMode)
            {
                OutputJsonError($"HTTP {status}: {detail}", 2);
                return 2;
            }
            Console.Error.WriteLine($"[AZURE ERROR] HTTP {status}: {detail}");
            return 2;
        }
        catch (OperationCanceledException)
        {
            if (jsonMode)
            {
                OutputJsonError("Request timed out. Increase AZURE_TIMEOUT (seconds) if needed.", 3);
                return 3;
            }
            Console.Error.WriteLine("[ERROR] Request timed out. Increase AZURE_TIMEOUT (seconds) if needed.");
            return 3;
        }
        catch (Exception ex)
        {
            if (jsonMode)
            {
                OutputJsonError($"{ex.GetType().Name}: {ex.Message}", 99);
                return 99;
            }
            Console.Error.WriteLine($"[UNHANDLED ERROR] {ex.GetType().Name}: {ex.Message}");
            return 99;
        }
    }

    /// <summary>
    /// Handles model management commands. Returns exit code if a command was handled, or -1 if not.
    /// </summary>
    static int HandleModelCommands(string[] args, UserConfig config)
    {
        string firstArg = args[0].ToLowerInvariant();

        switch (firstArg)
        {
            case "--models":
            case "--list-models":
                return ListModels(config);

            case "--current-model":
                return ShowCurrentModel(config);

            case "--set-model":
                if (args.Length < 2)
                {
                    Console.Error.WriteLine("[ERROR] Please specify a model name. Usage: --set-model <model-name>");
                    return 1;
                }
                return SetModel(args[1], config);

            case "--version":
            case "-v":
                var version = Assembly.GetEntryAssembly()?.GetName().Version;
                Console.WriteLine($"Azure OpenAI CLI v{version?.ToString(3) ?? "unknown"}");
                return 0;

            case "--help":
            case "-h":
                ShowUsage();
                return 0;

            default:
                // Not a model command, continue with normal processing
                return -1;
        }
    }

    /// <summary>
    /// Lists all available models, highlighting the currently active one.
    /// </summary>
    static int ListModels(UserConfig config)
    {
        if (config.AvailableModels.Count == 0)
        {
            Console.WriteLine("No models configured.");
            Console.WriteLine("Configure models in your .env file using AZUREOPENAIMODEL (comma-separated for multiple):");
            Console.WriteLine("  AZUREOPENAIMODEL=gpt-4,gpt-35-turbo,gpt-4o");
            return 0;
        }

        Console.WriteLine("Available models:");
        foreach (var model in config.AvailableModels)
        {
            bool isActive = model.Equals(config.ActiveModel, StringComparison.OrdinalIgnoreCase);
            string marker = isActive ? " *" : "";
            string prefix = isActive ? "→ " : "  ";
            Console.WriteLine($"{prefix}{model}{marker}");
        }
        Console.WriteLine();
        Console.WriteLine($"Config file: {UserConfig.GetConfigPath()}");
        return 0;
    }

    /// <summary>
    /// Shows the currently active model.
    /// </summary>
    static int ShowCurrentModel(UserConfig config)
    {
        if (string.IsNullOrEmpty(config.ActiveModel))
        {
            Console.WriteLine("No active model set.");
            Console.WriteLine("Use --set-model <model-name> to select a model, or configure AZUREOPENAIMODEL in your .env file.");
            return 1;
        }

        Console.WriteLine($"Current model: {config.ActiveModel}");
        return 0;
    }

    /// <summary>
    /// Sets the active model.
    /// </summary>
    static int SetModel(string modelName, UserConfig config)
    {
        if (config.AvailableModels.Count == 0)
        {
            Console.Error.WriteLine("[ERROR] No models configured. Configure models in your .env file first.");
            return 1;
        }

        if (config.SetActiveModel(modelName))
        {
            config.Save();
            Console.WriteLine($"Active model set to: {config.ActiveModel}");
            return 0;
        }
        else
        {
            Console.Error.WriteLine($"[ERROR] Model '{modelName}' not found in available models.");
            Console.WriteLine("Available models:");
            foreach (var model in config.AvailableModels)
            {
                Console.WriteLine($"  - {model}");
            }
            return 1;
        }
    }

    /// <summary>
    /// Shows usage information.
    /// </summary>
    static void ShowUsage()
    {
        Console.WriteLine("Azure OpenAI CLI");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  <prompt>              Send a prompt to the AI");
        Console.WriteLine("  --models              List available models (* marks active)");
        Console.WriteLine("  --current-model       Show the currently active model");
        Console.WriteLine("  --set-model <name>    Set the active model");
        Console.WriteLine("  --version, -v         Show version information");
        Console.WriteLine("  --help, -h            Show this help message");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --json                Output response as JSON (for scripting)");
        Console.WriteLine("  -t, --temperature <v> Override temperature (0.0-2.0)");
        Console.WriteLine("  --max-tokens <n>      Override max output tokens");
        Console.WriteLine("  --system <prompt>     Override system prompt for this invocation");
        Console.WriteLine("  --schema <json>      Enforce structured output with JSON schema (strict mode)");
        Console.WriteLine("  --config show         Display effective configuration and exit");
        Console.WriteLine();
        Console.WriteLine("Agent Mode:");
        Console.WriteLine("  --agent               Enable agentic mode (model can call tools)");
        Console.WriteLine("  --tools <list>        Comma-separated tool names to enable (default: all)");
        Console.WriteLine("                        Available: shell,file,web,clipboard,datetime");
        Console.WriteLine("  --max-rounds <n>      Max tool-calling rounds (default: 5, max: 20)");
        Console.WriteLine();
        Console.WriteLine("Piping:");
        Console.WriteLine("  echo \"question\" | azureopenai-cli");
        Console.WriteLine("  git diff | azureopenai-cli \"review this code\"");
        Console.WriteLine("  cat file.md | azureopenai-cli \"summarize this\"");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  azureopenai-cli \"Explain quantum computing\"");
        Console.WriteLine("  azureopenai-cli --models");
        Console.WriteLine("  azureopenai-cli --set-model gpt-4o");
        Console.WriteLine("  azureopenai-cli --json \"What is Docker?\"");
        Console.WriteLine("  echo \"code\" | azureopenai-cli --json \"review this\"");
        Console.WriteLine("  azureopenai-cli --agent \"what time is it in Tokyo?\"");
        Console.WriteLine("  azureopenai-cli --agent \"summarize ~/notes.md\"");
        Console.WriteLine("  azureopenai-cli --agent --tools shell \"run git log -5 and summarize\"");
    }

    /// <summary>
    /// Displays the effective configuration with source attribution for each value.
    /// Precedence: CLI flags > UserConfig > Env vars > Defaults.
    /// </summary>
    static int ShowEffectiveConfig(UserConfig config, float? cliTemperature, int? cliMaxTokens,
        string? cliSystemPrompt, string? endpoint, string? activeModel)
    {

        // Determine temperature and source
        string? envTempStr = Environment.GetEnvironmentVariable("AZURE_TEMPERATURE");
        float? envTemp = envTempStr != null && float.TryParse(envTempStr,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float et) ? et : null;

        float effectiveTemp;
        string tempSource;
        if (cliTemperature.HasValue) { effectiveTemp = cliTemperature.Value; tempSource = "cli flag"; }
        else if (config.Temperature.HasValue) { effectiveTemp = config.Temperature.Value; tempSource = "config"; }
        else if (envTemp.HasValue) { effectiveTemp = envTemp.Value; tempSource = "env"; }
        else { effectiveTemp = DEFAULT_TEMPERATURE; tempSource = "default"; }

        // Determine max tokens and source
        string? envMaxStr = Environment.GetEnvironmentVariable("AZURE_MAX_TOKENS");
        int? envMax = envMaxStr != null && int.TryParse(envMaxStr, out int em) ? em : null;

        int effectiveMaxTokens;
        string maxSource;
        if (cliMaxTokens.HasValue) { effectiveMaxTokens = cliMaxTokens.Value; maxSource = "cli flag"; }
        else if (config.MaxTokens.HasValue) { effectiveMaxTokens = config.MaxTokens.Value; maxSource = "config"; }
        else if (envMax.HasValue) { effectiveMaxTokens = envMax.Value; maxSource = "env"; }
        else { effectiveMaxTokens = DEFAULT_MAX_TOKENS; maxSource = "default"; }

        // Determine timeout and source (no CLI flag for timeout)
        string? envTimeoutStr = Environment.GetEnvironmentVariable("AZURE_TIMEOUT");
        int? envTimeout = envTimeoutStr != null && int.TryParse(envTimeoutStr, out int eto) ? eto : null;

        int effectiveTimeout;
        string timeoutSource;
        if (config.TimeoutSeconds.HasValue) { effectiveTimeout = config.TimeoutSeconds.Value; timeoutSource = "config"; }
        else if (envTimeout.HasValue) { effectiveTimeout = envTimeout.Value; timeoutSource = "env"; }
        else { effectiveTimeout = DEFAULT_TIMEOUT_SECONDS; timeoutSource = "default"; }

        // Determine system prompt and source
        string? envSysPrompt = Environment.GetEnvironmentVariable("SYSTEMPROMPT");

        string effectiveSysPrompt;
        string sysSource;
        if (cliSystemPrompt != null) { effectiveSysPrompt = cliSystemPrompt; sysSource = "cli flag"; }
        else if (config.SystemPrompt != null) { effectiveSysPrompt = config.SystemPrompt; sysSource = "config"; }
        else if (envSysPrompt != null) { effectiveSysPrompt = envSysPrompt; sysSource = "env"; }
        else { effectiveSysPrompt = DEFAULT_SYSTEM_PROMPT; sysSource = "default"; }

        // Truncate long prompts for display
        string displayPrompt = effectiveSysPrompt.Length > 60
            ? effectiveSysPrompt[..60] + "..."
            : effectiveSysPrompt;

        string endpointSource = endpoint != null ? "env" : "missing";
        string modelSource = activeModel != null ? "config/env" : "missing";

        Console.WriteLine("Azure OpenAI CLI Configuration");
        Console.WriteLine("===============================");
        Console.WriteLine($"  Endpoint:      {endpoint ?? "(not set)"} ({endpointSource})");
        Console.WriteLine($"  Model:         {activeModel ?? "(not set)"} ({modelSource})");
        Console.WriteLine($"  Temperature:   {effectiveTemp} ({tempSource})");
        Console.WriteLine($"  Max Tokens:    {effectiveMaxTokens} ({maxSource})");
        Console.WriteLine($"  Timeout:       {effectiveTimeout}s ({timeoutSource})");
        Console.WriteLine($"  System Prompt: {displayPrompt} ({sysSource})");
        Console.WriteLine($"  Config File:   {UserConfig.GetConfigPath()}");

        return 0;
    }

    /// <summary>
    /// Parses an environment variable as int, returning default if missing or invalid.
    /// </summary>
    static int TryParseEnvInt(string envVar, int defaultValue)
    {
        string? value = Environment.GetEnvironmentVariable(envVar);
        return int.TryParse(value, out int result) ? result : defaultValue;
    }

    /// <summary>
    /// Parses an environment variable as float, returning default if missing or invalid.
    /// </summary>
    static float TryParseEnvFloat(string envVar, float defaultValue)
    {
        string? value = Environment.GetEnvironmentVariable(envVar);
        return float.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float result) ? result : defaultValue;
    }

    /// <summary>
    /// Retry an async operation with exponential backoff for transient Azure API errors.
    /// Handles HTTP 429 (rate limit) and 5xx (server error) responses.
    /// </summary>
    internal static async Task<T> WithRetryAsync<T>(Func<Task<T>> operation, int maxRetries = 3, CancellationToken ct = default)
    {
        int attempt = 0;
        while (true)
        {
            try
            {
                return await operation();
            }
            catch (RequestFailedException ex) when (attempt < maxRetries && (ex.Status == 429 || ex.Status >= 500))
            {
                attempt++;
                // Exponential backoff: 1s, 2s, 4s
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));

                // Check for Retry-After header
                if (ex.Status == 429)
                {
                    var rawResponse = ex.GetRawResponse();
                    if (rawResponse != null
                        && rawResponse.Headers.TryGetValue("Retry-After", out string? retryValue)
                        && int.TryParse(retryValue, out int retrySeconds))
                    {
                        delay = TimeSpan.FromSeconds(retrySeconds);
                    }
                }

                if (!Console.IsErrorRedirected)
                    Console.Error.Write($"\r⏳ Retry {attempt}/{maxRetries} in {delay.TotalSeconds:F0}s...");

                await Task.Delay(delay, ct);
            }
        }
    }

    /// <summary>
    /// Outputs a JSON-formatted error to stdout for --json mode.
    /// </summary>
    static void OutputJsonError(string message, int exitCode)
    {
        var errorObj = new
        {
            error = true,
            message = message,
            exit_code = exitCode
        };
        var options = new JsonSerializerOptions { WriteIndented = true };
        Console.WriteLine(JsonSerializer.Serialize(errorObj, options));
    }

    /// <summary>
    /// Agentic mode: tool-calling loop where the model can invoke built-in tools
    /// to gather context before generating a final response.
    /// </summary>
    static async Task<int> RunAgentLoop(
        ChatClient chatClient,
        List<ChatMessage> messages,
        ChatCompletionOptions options,
        string deploymentName,
        HashSet<string>? enabledToolNames,
        int maxRounds,
        bool jsonMode,
        int timeoutSeconds)
    {
        var registry = ToolRegistry.Create(enabledToolNames);

        // Add tool definitions directly to the options (already configured with temp/tokens/etc)
        var chatTools = registry.ToChatTools();
        foreach (var tool in chatTools)
            options.Tools.Add(tool);

        if (chatTools.Count > 0)
            options.ToolChoice = ChatToolChoice.CreateAutoChoice();

        // Prepend agent-aware system instruction to existing messages
        var systemMsg = messages.OfType<SystemChatMessage>().FirstOrDefault();
        if (systemMsg is not null)
        {
            int idx = messages.IndexOf(systemMsg);
            string toolNames = string.Join(", ", registry.All.Select(t => t.Name));
            messages[idx] = new SystemChatMessage(
                systemMsg.Content[0].Text +
                $"\n\nYou have tools available: [{toolNames}]. Use them when the user's request requires real-time data, file access, or system interaction. Call tools rather than guessing.");
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        var stopwatch = Stopwatch.StartNew();
        bool showStatus = !jsonMode && !Console.IsErrorRedirected;
        int round = 0;
        int totalToolCalls = 0;

        if (showStatus)
            Console.Error.Write("⚡ Agent mode");

        while (round < maxRounds)
        {
            round++;

            // Use streaming for all rounds — gives real-time text output on the
            // final response while still detecting tool calls inline.
            var toolCallsById = new Dictionary<int, (string Id, string Name, StringBuilder Args)>();
            var textBuilder = new StringBuilder();
            bool isToolCallRound = false;
            bool firstTextToken = true;

            await foreach (var update in chatClient.CompleteChatStreamingAsync(messages, options, cts.Token))
            {
                // Accumulate tool call fragments (streamed as chunked updates)
                if (update.ToolCallUpdates is { Count: > 0 })
                {
                    isToolCallRound = true;
                    foreach (var tcUpdate in update.ToolCallUpdates)
                    {
                        if (!toolCallsById.ContainsKey(tcUpdate.Index))
                            toolCallsById[tcUpdate.Index] = (tcUpdate.ToolCallId, tcUpdate.FunctionName, new StringBuilder());

                        if (tcUpdate.FunctionArgumentsUpdate is not null)
                            toolCallsById[tcUpdate.Index].Args.Append(tcUpdate.FunctionArgumentsUpdate.ToString());
                    }
                }

                // Stream text tokens to console immediately (only for non-tool responses)
                foreach (var part in update.ContentUpdate)
                {
                    if (!isToolCallRound)
                    {
                        if (firstTextToken)
                        {
                            firstTextToken = false;
                            if (showStatus)
                                Console.Error.Write($"\r                              \r");
                        }

                        textBuilder.Append(part.Text);

                        if (!jsonMode)
                            Console.Write(part.Text);
                    }
                }

                // Finish reason appears on the last streaming update
                if (update.FinishReason == ChatFinishReason.ToolCalls)
                    isToolCallRound = true;
            }

            if (isToolCallRound && toolCallsById.Count > 0)
            {
                // Build assistant message with tool calls for conversation history
                var toolCallList = toolCallsById.OrderBy(kv => kv.Key)
                    .Select(kv => ChatToolCall.CreateFunctionToolCall(
                        kv.Value.Id, kv.Value.Name,
                        BinaryData.FromString(kv.Value.Args.ToString())))
                    .ToList();
                messages.Add(new AssistantChatMessage(toolCallList));

                if (showStatus)
                    Console.Error.Write($"\r🔧 Round {round}: ");

                totalToolCalls += toolCallList.Count;

                if (showStatus)
                    Console.Error.Write(string.Join(" ", toolCallList.Select(tc => tc.FunctionName)));

                // Execute tool calls in parallel
                var toolTasks = toolCallList.Select(tc => registry.ExecuteAsync(
                    tc.FunctionName,
                    tc.FunctionArguments?.ToString() ?? "{}",
                    cts.Token)).ToList();

                var results = await Task.WhenAll(toolTasks);

                // Add results in order matching tool call IDs
                for (int i = 0; i < toolCallList.Count; i++)
                    messages.Add(new ToolChatMessage(toolCallList[i].Id, results[i]));

                if (showStatus)
                    Console.Error.WriteLine();

                continue;
            }

            // Final text response — text was already streamed to console in non-JSON mode
            if (showStatus && firstTextToken)
                Console.Error.Write($"\r                              \r");

            stopwatch.Stop();
            var responseText = textBuilder.ToString();

            if (jsonMode)
            {
                var jsonOutput = new
                {
                    model = deploymentName,
                    response = responseText,
                    duration_ms = stopwatch.ElapsedMilliseconds,
                    agent = new { rounds = round, tools_called = totalToolCalls }
                };
                var jsonOpts = new JsonSerializerOptions { WriteIndented = true };
                Console.WriteLine(JsonSerializer.Serialize(jsonOutput, jsonOpts));
            }
            else
            {
                Console.WriteLine();
            }

            return 0;
        }

        // Hit max rounds without a final response
        var msg = $"Agent exhausted {maxRounds} tool-calling rounds without completing.";
        if (jsonMode)
        {
            OutputJsonError(msg, 1);
            return 1;
        }
        Console.Error.WriteLine($"\r[WARN] {msg}");
        return 1;
    }
}
