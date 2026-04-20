using System.ClientModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Azure.AI.OpenAI;
using dotenv.net;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

[assembly: InternalsVisibleTo("AzureOpenAI_CLI.V2.Tests")]

namespace AzureOpenAI_CLI_V2;

internal class Program
{
    private const int DEFAULT_TIMEOUT_SECONDS = 120;
    private const float DEFAULT_TEMPERATURE = 0.55f;
    private const int DEFAULT_MAX_TOKENS = 10000;
    private const string DEFAULT_SYSTEM_PROMPT = "You are a secure, concise CLI assistant. Keep answers factual, no fluff.";

    /// <summary>
    /// Holds parsed CLI flag values.
    /// </summary>
    internal record CliOptions(
        string? Model,
        float Temperature,
        int MaxTokens,
        int TimeoutSeconds,
        string SystemPrompt,
        bool Raw,
        bool ShowHelp,
        bool ShowVersion,
        bool AgentMode,
        string? Tools,
        bool SquadInit,
        string? Persona,
        bool ListPersonas,
        bool RalphMode,
        string? ValidateCommand,
        string? TaskFile,
        int MaxIterations,
        string? Prompt,
        bool ParseError // True if there was a parse error (forces exit code 1)
    );

    private static async Task<int> Main(string[] args)
    {
        // Load .env file if present (matches v1 behavior)
        DotEnv.Load(options: new DotEnvOptions(ignoreExceptions: true, probeForEnv: true));

        var opts = ParseArgs(args);

        if (opts.ShowHelp)
        {
            ShowHelp();
            return opts.ParseError ? 1 : 0;
        }

        if (opts.ShowVersion)
        {
            ShowVersion();
            return 0;
        }

        // Squad init: create .squad.json and .squad/ directory
        if (opts.SquadInit)
        {
            var initialized = AzureOpenAI_CLI_V2.Squad.SquadInitializer.Initialize();
            if (initialized)
            {
                Console.WriteLine("✓ Squad initialized: .squad.json and .squad/ directory created.");
                Console.WriteLine("  Edit .squad.json to customize personas and routing rules.");
                return 0;
            }
            else
            {
                Console.WriteLine("✓ Squad already initialized (found .squad.json).");
                return 0;
            }
        }

        // List personas: show available personas from .squad.json
        if (opts.ListPersonas)
        {
            var config = AzureOpenAI_CLI_V2.Squad.SquadConfig.Load();
            if (config == null)
            {
                return ErrorAndExit("No .squad.json found. Run --squad-init first.", 1, jsonMode: false);
            }

            var personas = config.ListPersonas();
            if (personas.Count == 0)
            {
                Console.WriteLine("No personas configured in .squad.json");
                return 0;
            }

            Console.WriteLine($"Available personas ({personas.Count}):");
            foreach (var name in personas)
            {
                var persona = config.GetPersona(name);
                Console.WriteLine($"  • {name} — {persona?.Description ?? "(no description)"}");
            }
            return 0;
        }

        // Validate --task-file exists early (before checking credentials)
        if (!string.IsNullOrWhiteSpace(opts.TaskFile) && !File.Exists(opts.TaskFile))
        {
            return ErrorAndExit($"Task file not found: {opts.TaskFile}", 1, jsonMode: false);
        }

        // Resolve endpoint and API key from env
        var endpoint = Environment.GetEnvironmentVariable("AZUREOPENAIENDPOINT");
        var apiKey = Environment.GetEnvironmentVariable("AZUREOPENAIAPI");

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return ErrorAndExit("AZUREOPENAIENDPOINT environment variable not set", 1, jsonMode: false);
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return ErrorAndExit("AZUREOPENAIAPI environment variable not set", 1, jsonMode: false);
        }

        // Resolve model (CLI flag > env > default "gpt-4o-mini")
        var model = opts.Model
            ?? Environment.GetEnvironmentVariable("AZUREOPENAIMODEL")
            ?? "gpt-4o-mini";

        // Resolve prompt: --task-file > positional arg > stdin if redirected > error
        var prompt = opts.Prompt;

        // Ralph mode: --task-file takes precedence
        if (opts.RalphMode && !string.IsNullOrWhiteSpace(opts.TaskFile))
        {
            try
            {
                prompt = await File.ReadAllTextAsync(opts.TaskFile);
            }
            catch (Exception ex)
            {
                return ErrorAndExit($"Failed to read task file: {ex.Message}", 1, jsonMode: false);
            }
        }
        else if (string.IsNullOrWhiteSpace(prompt) && Console.IsInputRedirected)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                prompt = await Console.In.ReadToEndAsync(cts.Token);
            }
            catch
            {
                return ErrorAndExit("Failed to read stdin within 5 seconds", 1, jsonMode: false);
            }
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return ErrorAndExit("No prompt provided (provide as argument or via stdin)", 1, jsonMode: false);
        }

        // Build chat client
        try
        {
            var client = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));
            var chatClient = client.GetChatClient(model).AsIChatClient();

            // Ralph mode: run autonomous loop
            if (opts.RalphMode)
            {
                using var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };

                return await AzureOpenAI_CLI_V2.Ralph.RalphWorkflow.RunAsync(
                    chatClient,
                    taskPrompt: prompt,
                    systemPrompt: opts.SystemPrompt,
                    validateCommand: opts.ValidateCommand,
                    maxIterations: opts.MaxIterations,
                    temperature: opts.Temperature,
                    maxTokens: opts.MaxTokens,
                    timeoutSeconds: opts.TimeoutSeconds,
                    tools: opts.Tools ?? "shell,file,web,datetime,delegate",
                    ct: cts.Token
                );
            }

            // Standard agent mode: wire tools if --agent is set
            var agent = opts.AgentMode
                ? chatClient.AsAIAgent(
                    instructions: opts.SystemPrompt,
                    tools: AzureOpenAI_CLI_V2.Tools.ToolRegistry.CreateMafTools(
                        opts.Tools?.Split(',', StringSplitOptions.RemoveEmptyEntries)))
                : chatClient.AsAIAgent(instructions: opts.SystemPrompt);

            // Run streaming chat
            using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(opts.TimeoutSeconds));
            int? inputTokens = null;
            int? outputTokens = null;

            await foreach (var update in agent.RunStreamingAsync(prompt, cancellationToken: cts2.Token))
            {
                if (!string.IsNullOrEmpty(update.Text))
                {
                    Console.Out.Write(update.Text);
                }

                // Capture token usage (MAF doesn't surface this directly in streaming yet, so this is a placeholder)
                // In Phase 2 we'll wire usage tracking via the underlying ChatClient if needed
            }

            Console.Out.Flush();

            // Token usage on stderr (only when NOT --raw)
            if (!opts.Raw && !Console.IsErrorRedirected && inputTokens.HasValue)
            {
                var total = inputTokens.Value + outputTokens.GetValueOrDefault();
                Console.Error.WriteLine($"  [tokens: {inputTokens}→{outputTokens}, {total} total]");
            }

            // Trailing newline (NOT in raw mode)
            if (!opts.Raw)
            {
                Console.WriteLine();
            }

            return 0;
        }
        catch (OperationCanceledException)
        {
            return ErrorAndExit("Request timed out", 3, jsonMode: false);
        }
        catch (Exception ex)
        {
            return ErrorAndExit($"Request failed: {ex.Message}", 1, jsonMode: false);
        }
    }

    /// <summary>
    /// Parses CLI arguments, handling flags and positional prompt.
    /// Env var precedence: CLI flag > env var > hardcoded default.
    /// </summary>
    internal static CliOptions ParseArgs(string[] args)
    {
        string? model = null;
        float? temperature = null;
        int? maxTokens = null;
        int? timeoutSeconds = null;
        string? systemPrompt = null;
        bool raw = false;
        bool showHelp = false;
        bool showVersion = false;
        bool agentMode = false;
        string? tools = null;
        bool squadInit = false;
        string? persona = null;
        bool listPersonas = false;
        bool ralphMode = false;
        string? validateCommand = null;
        string? taskFile = null;
        int maxIterations = 10;
        var positionalArgs = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg.ToLowerInvariant())
            {
                case "--model":
                case "-m":
                    if (i + 1 < args.Length)
                    {
                        model = args[++i];
                    }
                    break;
                case "--temperature":
                case "-t":
                    if (i + 1 < args.Length && float.TryParse(args[i + 1],
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float temp))
                    {
                        temperature = temp;
                        i++;
                    }
                    break;
                case "--max-tokens":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int tokens))
                    {
                        maxTokens = tokens;
                        i++;
                    }
                    break;
                case "--timeout":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int timeout))
                    {
                        timeoutSeconds = timeout;
                        i++;
                    }
                    break;
                case "--system":
                case "-s":
                    if (i + 1 < args.Length)
                    {
                        systemPrompt = args[++i];
                    }
                    break;
                case "--raw":
                    raw = true;
                    break;
                case "--agent":
                    agentMode = true;
                    break;
                case "--tools":
                    if (i + 1 < args.Length)
                    {
                        tools = args[++i];
                    }
                    break;
                case "--squad-init":
                    squadInit = true;
                    break;
                case "--persona":
                    if (i + 1 < args.Length)
                    {
                        persona = args[++i];
                    }
                    break;
                case "--personas":
                    listPersonas = true;
                    break;
                case "--ralph":
                    ralphMode = true;
                    agentMode = true; // Ralph implies agent mode
                    break;
                case "--validate":
                    if (i + 1 < args.Length)
                    {
                        validateCommand = args[++i];
                    }
                    else
                    {
                        Console.Error.WriteLine("[ERROR] --validate requires a command argument");
                        return new CliOptions(
                            Model: null,
                            Temperature: DEFAULT_TEMPERATURE,
                            MaxTokens: DEFAULT_MAX_TOKENS,
                            TimeoutSeconds: DEFAULT_TIMEOUT_SECONDS,
                            SystemPrompt: DEFAULT_SYSTEM_PROMPT,
                            Raw: false,
                            ShowHelp: true,
                            ShowVersion: false,
                            AgentMode: false,
                            Tools: null,
                            SquadInit: false,
                            Persona: null,
                            ListPersonas: false,
                            RalphMode: false,
                            ValidateCommand: null,
                            TaskFile: null,
                            MaxIterations: 10,
                            Prompt: null,
                            ParseError: true
                        );
                    }
                    break;
                case "--task-file":
                    if (i + 1 < args.Length)
                    {
                        taskFile = args[++i];
                    }
                    else
                    {
                        Console.Error.WriteLine("[ERROR] --task-file requires a file path argument");
                        return new CliOptions(
                            Model: null,
                            Temperature: DEFAULT_TEMPERATURE,
                            MaxTokens: DEFAULT_MAX_TOKENS,
                            TimeoutSeconds: DEFAULT_TIMEOUT_SECONDS,
                            SystemPrompt: DEFAULT_SYSTEM_PROMPT,
                            Raw: false,
                            ShowHelp: true,
                            ShowVersion: false,
                            AgentMode: false,
                            Tools: null,
                            SquadInit: false,
                            Persona: null,
                            ListPersonas: false,
                            RalphMode: false,
                            ValidateCommand: null,
                            TaskFile: null,
                            MaxIterations: 10,
                            Prompt: null,
                            ParseError: true
                        );
                    }
                    break;
                case "--max-iterations":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int iters))
                    {
                        if (iters < 1 || iters > 50)
                        {
                            Console.Error.WriteLine("[ERROR] --max-iterations must be between 1 and 50");
                            return new CliOptions(
                                Model: null,
                                Temperature: DEFAULT_TEMPERATURE,
                                MaxTokens: DEFAULT_MAX_TOKENS,
                                TimeoutSeconds: DEFAULT_TIMEOUT_SECONDS,
                                SystemPrompt: DEFAULT_SYSTEM_PROMPT,
                                Raw: false,
                                ShowHelp: true,
                                ShowVersion: false,
                                AgentMode: false,
                                Tools: null,
                                SquadInit: false,
                                Persona: null,
                                ListPersonas: false,
                                RalphMode: false,
                                ValidateCommand: null,
                                TaskFile: null,
                                MaxIterations: 10,
                                Prompt: null,
                                ParseError: true
                            );
                        }
                        maxIterations = iters;
                        i++;
                    }
                    else
                    {
                        Console.Error.WriteLine("[ERROR] --max-iterations requires a numeric value (1-50)");
                        return new CliOptions(
                            Model: null,
                            Temperature: DEFAULT_TEMPERATURE,
                            MaxTokens: DEFAULT_MAX_TOKENS,
                            TimeoutSeconds: DEFAULT_TIMEOUT_SECONDS,
                            SystemPrompt: DEFAULT_SYSTEM_PROMPT,
                            Raw: false,
                            ShowHelp: true,
                            ShowVersion: false,
                            AgentMode: false,
                            Tools: null,
                            SquadInit: false,
                            Persona: null,
                            ListPersonas: false,
                            RalphMode: false,
                            ValidateCommand: null,
                            TaskFile: null,
                            MaxIterations: 10,
                            Prompt: null,
                            ParseError: true
                        );
                    }
                    break;
                case "--help":
                case "-h":
                    showHelp = true;
                    break;
                case "--version":
                case "-v":
                    showVersion = true;
                    break;
                default:
                    // Positional argument (prompt)
                    positionalArgs.Add(args[i]);
                    break;
            }
        }

        // Apply env var fallbacks
        if (!temperature.HasValue)
        {
            var envTemp = Environment.GetEnvironmentVariable("AZURE_TEMPERATURE");
            if (!string.IsNullOrWhiteSpace(envTemp) && float.TryParse(envTemp,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float t))
            {
                temperature = t;
            }
        }

        if (!maxTokens.HasValue)
        {
            var envTokens = Environment.GetEnvironmentVariable("AZURE_MAX_TOKENS");
            if (!string.IsNullOrWhiteSpace(envTokens) && int.TryParse(envTokens, out int mt))
            {
                maxTokens = mt;
            }
        }

        if (!timeoutSeconds.HasValue)
        {
            var envTimeout = Environment.GetEnvironmentVariable("AZURE_TIMEOUT");
            if (!string.IsNullOrWhiteSpace(envTimeout) && int.TryParse(envTimeout, out int to))
            {
                timeoutSeconds = to;
            }
        }

        if (string.IsNullOrWhiteSpace(systemPrompt))
        {
            systemPrompt = Environment.GetEnvironmentVariable("SYSTEMPROMPT");
        }

        // Join positional args as prompt
        var prompt = positionalArgs.Count > 0 ? string.Join(" ", positionalArgs) : null;

        return new CliOptions(
            Model: model,
            Temperature: temperature ?? DEFAULT_TEMPERATURE,
            MaxTokens: maxTokens ?? DEFAULT_MAX_TOKENS,
            TimeoutSeconds: timeoutSeconds ?? DEFAULT_TIMEOUT_SECONDS,
            SystemPrompt: systemPrompt ?? DEFAULT_SYSTEM_PROMPT,
            Raw: raw,
            ShowHelp: showHelp,
            ShowVersion: showVersion,
            AgentMode: agentMode,
            Tools: tools,
            SquadInit: squadInit,
            Persona: persona,
            ListPersonas: listPersonas,
            RalphMode: ralphMode,
            ValidateCommand: validateCommand,
            TaskFile: taskFile,
            MaxIterations: maxIterations,
            Prompt: prompt,
            ParseError: false
        );
    }

    /// <summary>
    /// Writes an error message to stderr (with [ERROR] prefix) and returns the specified exit code.
    /// Matches v1 ErrorAndExit semantics.
    /// </summary>
    internal static int ErrorAndExit(string message, int exitCode, bool jsonMode)
    {
        if (jsonMode)
        {
            var errorObj = new ErrorJsonResponse(Error: true, Message: message, ExitCode: exitCode);
            Console.WriteLine(JsonSerializer.Serialize(errorObj, AppJsonContext.Default.ErrorJsonResponse));
        }
        else
        {
            Console.Error.WriteLine($"[ERROR] {message}");
        }
        return exitCode;
    }

    private static void ShowHelp()
    {
        Console.WriteLine(@"az-ai-v2 (v2.0.0-alpha.1) — Azure OpenAI CLI (Microsoft Agent Framework)

Usage:
  az-ai-v2 [OPTIONS] <prompt>
  echo ""prompt"" | az-ai-v2 [OPTIONS]

Options:
  --model, -m <name>        Model deployment name (env: AZUREOPENAIMODEL)
  --temperature, -t <float> Sampling temperature 0.0-2.0 (env: AZURE_TEMPERATURE, default: 0.55)
  --max-tokens <int>        Max completion tokens (env: AZURE_MAX_TOKENS, default: 10000)
  --timeout <seconds>       Request timeout in seconds (env: AZURE_TIMEOUT, default: 120)
  --system, -s <text>       System prompt (env: SYSTEMPROMPT)
  --agent                   Enable agent mode with tool calling (requires --tools)
  --tools <list>            Comma-separated tool list (shell,file,web,clipboard,datetime,delegate)
  --raw                     Suppress all non-content output (for Espanso/AHK)
  --help, -h                Show this help
  --version, -v             Show version

Ralph Mode (Autonomous Agent Loop):
  --ralph                   Enable Ralph mode (autonomous loop with validation)
  --validate <command>      Shell command to validate each iteration (exit 0 = pass)
  --task-file <path>        Read task prompt from file
  --max-iterations <n>      Maximum loop iterations (default: 10, max: 50)

Environment Variables (required):
  AZUREOPENAIENDPOINT       Azure OpenAI endpoint URL
  AZUREOPENAIAPI            API key

Examples:
  az-ai-v2 ""What is the capital of France?""
  az-ai-v2 --model gpt-4o --temperature 0.7 ""Write a haiku""
  az-ai-v2 --agent --tools shell,file ""List and summarize the files in this directory""
  az-ai-v2 --ralph --task-file task.md --validate ""dotnet test"" --max-iterations 5
  echo ""Summarize this"" | az-ai-v2 --raw
");
    }

    private static void ShowVersion()
    {
        Console.WriteLine("az-ai-v2 2.0.0-alpha.1 (Microsoft Agent Framework)");
    }
}
