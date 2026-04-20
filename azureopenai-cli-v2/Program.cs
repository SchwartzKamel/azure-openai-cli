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
        string? Prompt
    );

    private static async Task<int> Main(string[] args)
    {
        // Load .env file if present (matches v1 behavior)
        DotEnv.Load(options: new DotEnvOptions(ignoreExceptions: true, probeForEnv: true));

        var opts = ParseArgs(args);

        if (opts.ShowHelp)
        {
            ShowHelp();
            return 0;
        }

        if (opts.ShowVersion)
        {
            ShowVersion();
            return 0;
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

        // Resolve prompt: positional arg > stdin if redirected > error
        var prompt = opts.Prompt;
        if (string.IsNullOrWhiteSpace(prompt) && Console.IsInputRedirected)
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

        // Build AIAgent via MAF
        try
        {
            var client = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));
            var agent = client.GetChatClient(model).AsIChatClient().AsAIAgent(instructions: opts.SystemPrompt);

            // Run streaming chat
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(opts.TimeoutSeconds));
            int? inputTokens = null;
            int? outputTokens = null;

            await foreach (var update in agent.RunStreamingAsync(prompt, cancellationToken: cts.Token))
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
            Prompt: prompt
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
  --raw                     Suppress all non-content output (for Espanso/AHK)
  --help, -h                Show this help
  --version, -v             Show version

Environment Variables (required):
  AZUREOPENAIENDPOINT       Azure OpenAI endpoint URL
  AZUREOPENAIAPI            API key

Examples:
  az-ai-v2 ""What is the capital of France?""
  az-ai-v2 --model gpt-4o --temperature 0.7 ""Write a haiku""
  echo ""Summarize this"" | az-ai-v2 --raw
");
    }

    private static void ShowVersion()
    {
        Console.WriteLine("az-ai-v2 2.0.0-alpha.1 (Microsoft Agent Framework)");
    }
}
