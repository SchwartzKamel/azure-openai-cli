using Azure;
using Azure.AI.OpenAI;
using Azure.AI.OpenAI.Chat;
using OpenAI.Chat;
using dotenv.net;
using AzureOpenAI_CLI;

class Program
{
    // Security: Cap prompt size to prevent abuse and excessive API costs
    private const int MAX_PROMPT_LENGTH = 32000;

    static int Main(string[] args)
    {
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
            string? systemPrompt = Environment.GetEnvironmentVariable("SYSTEMPROMPT");

            // Initialize available models from environment (supports comma-separated list)
            config.InitializeFromEnvironment(azureOpenAiModel);

            // Handle model management commands
            if (args.Length > 0)
            {
                int commandResult = HandleModelCommands(args, config);
                if (commandResult != -1)
                {
                    return commandResult;
                }
            }

            if (args.Length == 0)
            {
                ShowUsage();
                return 1;
            }

            string userPrompt = string.Join(' ', args);

            // Validate prompt length to prevent abuse and excessive API costs
            if (userPrompt.Length > MAX_PROMPT_LENGTH)
            {
                Console.Error.WriteLine($"[ERROR] Prompt too long ({userPrompt.Length} chars). Maximum allowed is {MAX_PROMPT_LENGTH} chars.");
                return 1;
            }

            // Early validation: API key must not be empty/whitespace
            if (string.IsNullOrWhiteSpace(azureOpenAiApiKey))
                throw new ArgumentNullException(nameof(azureOpenAiApiKey), "Azure OpenAI API key is not set.");

            // Early validation: endpoint must be a valid URL
            if (string.IsNullOrWhiteSpace(azureOpenAiEndpoint))
                throw new ArgumentNullException(nameof(azureOpenAiEndpoint), "Azure OpenAI endpoint is not set.");
            if (!Uri.TryCreate(azureOpenAiEndpoint, UriKind.Absolute, out var endpoint)
                || (endpoint.Scheme != "https" && endpoint.Scheme != "http"))
            {
                Console.Error.WriteLine($"[ERROR] Invalid endpoint URL: '{azureOpenAiEndpoint}'. Must be a valid HTTP/HTTPS URL.");
                return 1;
            }
            
            // Use the active model from user config, falling back to the first model in the list
            var deploymentName = config.ActiveModel 
                ?? config.AvailableModels.FirstOrDefault() 
                ?? throw new InvalidOperationException("Azure OpenAI model is not set. Configure AZUREOPENAIMODEL in your .env file.");
            var apiKey = azureOpenAiApiKey;

            AzureOpenAIClient azureClient = new(
                endpoint,
                new AzureKeyCredential(apiKey));
            ChatClient chatClient = azureClient.GetChatClient(deploymentName);

            // Read configurable parameters from environment, with sensible defaults
            int maxTokens = TryParseEnvInt("AZURE_MAX_TOKENS", 10000);
            float temperature = TryParseEnvFloat("AZURE_TEMPERATURE", 0.55f);
            int timeoutSeconds = TryParseEnvInt("AZURE_TIMEOUT", 120);

            var requestOptions = new ChatCompletionOptions()
            {
                MaxOutputTokenCount = maxTokens,
                Temperature = temperature,
                TopP = 1.0f,
                FrequencyPenalty = 0.0f,
                PresencePenalty = 0.0f,
            };

            #pragma warning disable AOAI001
            requestOptions.SetNewMaxCompletionTokensPropertyEnabled(true);
            #pragma warning restore AOAI001

            List<ChatMessage> messages = new List<ChatMessage>()
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userPrompt),
            };

            // Use a timeout to prevent indefinite hangs during streaming
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            var response = chatClient.CompleteChatStreaming(messages, requestOptions, cts.Token);

            foreach (StreamingChatCompletionUpdate update in response)
            {
                foreach (ChatMessageContentPart updatePart in update.ContentUpdate)
                {
                    System.Console.Write(updatePart.Text);
                }
            }
            System.Console.WriteLine("");

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
            Console.Error.WriteLine($"[AZURE ERROR] HTTP {status}: {detail}");
            return 2;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("[ERROR] Request timed out. Increase AZURE_TIMEOUT (seconds) if needed.");
            return 3;
        }
        catch (Exception ex)
        {
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
        Console.WriteLine("  --help, -h            Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  azureopenai-cli \"Explain quantum computing\"");
        Console.WriteLine("  azureopenai-cli --models");
        Console.WriteLine("  azureopenai-cli --set-model gpt-4o");
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
}
