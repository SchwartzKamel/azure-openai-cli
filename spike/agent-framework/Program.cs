// Phase 0 spike — Microsoft Agent Framework hello-agent harness.
//
// Three auth paths gated by --auth:
//   apikey   → Azure OpenAI direct + key  (preserves current env contract)
//   aad      → Azure OpenAI direct + DefaultAzureCredential (AAD / Managed Identity)
//   foundry  → Azure AI Foundry model catalog endpoint + api-key header
//
// Output is intentionally minimal so the bench harness can time it cleanly:
//   stdout = model text only (raw)
//   stderr = phase markers (auth-ready, request-sent, first-token, complete)
//            with monotonic ns timestamps for benchmark parsing.
//
// Env vars (matches current az-ai contract):
//   AZUREOPENAIENDPOINT     Azure OpenAI endpoint URL (apikey, aad)
//   AZUREOPENAIAPI          API key (apikey)
//   AZUREOPENAIMODEL        Deployment name
//   AZURE_FOUNDRY_ENDPOINT  (foundry - model catalog surface)
//   AZURE_FOUNDRY_API_KEY   API key for Foundry (foundry)
//   AZURE_FOUNDRY_MODEL     Deployment name for Foundry (foundry)

using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics;
using Azure.AI.OpenAI;
using Azure.Identity;
using dotenv.net;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

namespace AzAi.Spike.AgentFramework;

internal static class Program
{
    private static readonly long _processStartTicks = Stopwatch.GetTimestamp();

    private static async Task<int> Main(string[] args)
    {
        DotEnv.Load(options: new DotEnvOptions(ignoreExceptions: true, probeForEnv: true));

        var (auth, prompt, system, useTool) = ParseArgs(args);
        Mark("args-parsed");

        try
        {
            var agent = await BuildAgentAsync(auth, useTool);
            Mark("agent-ready");

            // Streaming run — measure TTFT.
            var firstTokenSeen = false;
            await foreach (var update in agent.RunStreamingAsync(prompt))
            {
                if (!firstTokenSeen)
                {
                    Mark("first-token");
                    firstTokenSeen = true;
                }
                if (!string.IsNullOrEmpty(update.Text))
                {
                    Console.Out.Write(update.Text);
                }
            }
            Console.Out.Flush();
            Mark("complete");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[spike-error] {ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }

    private static (string auth, string prompt, string? system, bool useTool) ParseArgs(string[] args)
    {
        string auth = "apikey";
        string? prompt = null;
        string? system = null;
        bool useTool = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--auth":
                    auth = args[++i];
                    break;
                case "--prompt":
                    prompt = args[++i];
                    break;
                case "--system":
                    system = args[++i];
                    break;
                case "--tool":
                    useTool = true;
                    break;
                case "-h":
                case "--help":
                    Console.WriteLine("af-spike --auth {apikey|aad|foundry} --prompt <text> [--system <text>] [--tool]");
                    Environment.Exit(0);
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            // Read stdin if no --prompt given (matches az-ai pipe behavior).
            if (Console.IsInputRedirected)
            {
                prompt = Console.In.ReadToEnd().Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            Console.Error.WriteLine("[spike-error] no prompt (--prompt or stdin)");
            Environment.Exit(2);
        }

        return (auth.ToLowerInvariant(), prompt!, system, useTool);
    }

    private static Task<AIAgent> BuildAgentAsync(string auth, bool useTool)
    {
        var deployment = Env("AZUREOPENAIMODEL") ?? "gpt-4o-mini";
        var systemPrompt = "You are a fast, concise CLI assistant. No fluff.";

        return auth switch
        {
            "apikey" => Task.FromResult(BuildApiKeyAgent(deployment, systemPrompt, useTool)),
            "aad" => Task.FromResult(BuildAadAgent(deployment, systemPrompt, useTool)),
            "foundry" => Task.FromResult(BuildFoundryAgent(deployment, systemPrompt, useTool)),
            _ => throw new ArgumentException($"unknown --auth value: {auth} (apikey|aad|foundry)")
        };
    }

    private static AIAgent BuildApiKeyAgent(string deployment, string systemPrompt, bool useTool)
    {
        var endpoint = Env("AZUREOPENAIENDPOINT") ?? throw new InvalidOperationException("AZUREOPENAIENDPOINT not set");
        var key = Env("AZUREOPENAIAPI") ?? throw new InvalidOperationException("AZUREOPENAIAPI not set");

        var client = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(key));
        var chatClient = client.GetChatClient(deployment).AsIChatClient();
        return useTool ? chatClient.AsAIAgent(instructions: systemPrompt, tools: [AIFunctionFactory.Create(GetDateTime)])
                       : chatClient.AsAIAgent(instructions: systemPrompt);
    }

    private static AIAgent BuildAadAgent(string deployment, string systemPrompt, bool useTool)
    {
        var endpoint = Env("AZUREOPENAIENDPOINT") ?? throw new InvalidOperationException("AZUREOPENAIENDPOINT not set");
        var client = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());
        var chatClient = client.GetChatClient(deployment).AsIChatClient();
        return useTool ? chatClient.AsAIAgent(instructions: systemPrompt, tools: [AIFunctionFactory.Create(GetDateTime)])
                       : chatClient.AsAIAgent(instructions: systemPrompt);
    }

    private static AIAgent BuildFoundryAgent(string deployment, string systemPrompt, bool useTool)
    {
        // Phase 0 pt2: MAF doesn't expose a Foundry factory for the model catalog
        // surface (services.ai.azure.com/models) that respects api-key header auth.
        // We fallback to the same pattern as the main CLI: hand-wire a ChatClient
        // with a PipelinePolicy that swaps Authorization Bearer → api-key header.
        var endpoint = Env("AZURE_FOUNDRY_ENDPOINT")
            ?? throw new InvalidOperationException("AZURE_FOUNDRY_ENDPOINT not set");
        var apiKey = Env("AZURE_FOUNDRY_API_KEY")
            ?? throw new InvalidOperationException("AZURE_FOUNDRY_API_KEY not set");
        var model = Env("AZURE_FOUNDRY_MODEL") ?? deployment;

        var options = new OpenAIClientOptions { Endpoint = new Uri(endpoint) };
        options.AddPolicy(new FoundryAuthPolicy(apiKey, "2024-05-01-preview"), PipelinePosition.PerCall);
        var chatClient = new ChatClient(model, new ApiKeyCredential(apiKey), options).AsIChatClient();
        return useTool ? chatClient.AsAIAgent(instructions: systemPrompt, tools: [AIFunctionFactory.Create(GetDateTime)])
                       : chatClient.AsAIAgent(instructions: systemPrompt);
    }

    /// <summary>
    /// Simple tool for round-trip benchmark: returns current UTC timestamp.
    /// </summary>
    [System.ComponentModel.Description("Get the current date and time in UTC")]
    private static string GetDateTime() => DateTime.UtcNow.ToString("O");

    /// <summary>
    /// Pipeline policy for Foundry's model catalog surface (services.ai.azure.com/models).
    /// Swaps OpenAI's Authorization: Bearer for api-key: header + appends api-version query.
    /// Borrowed from azureopenai-cli/Program.cs ADR-005 (FoundryAuthPolicy).
    /// </summary>
    private sealed class FoundryAuthPolicy : PipelinePolicy
    {
        private readonly string _apiKey;
        private readonly string _apiVersion;
        public FoundryAuthPolicy(string apiKey, string apiVersion)
        {
            _apiKey = apiKey;
            _apiVersion = apiVersion;
        }
        public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            Apply(message);
            ProcessNext(message, pipeline, currentIndex);
        }
        public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            Apply(message);
            await ProcessNextAsync(message, pipeline, currentIndex).ConfigureAwait(false);
        }
        private void Apply(PipelineMessage message)
        {
            var req = message.Request;
            req.Headers.Set("api-key", _apiKey);
            req.Headers.Remove("Authorization");
            if (req.Uri is Uri uri && !uri.Query.Contains("api-version=", StringComparison.OrdinalIgnoreCase))
            {
                var sep = string.IsNullOrEmpty(uri.Query) ? "?" : "&";
                req.Uri = new Uri(uri.AbsoluteUri + sep + "api-version=" + _apiVersion);
            }
        }
    }

    private static string? Env(string name)
    {
        var v = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }

    private static void Mark(string phase)
    {
        var ns = (long)((Stopwatch.GetTimestamp() - _processStartTicks) * (1_000_000_000.0 / Stopwatch.Frequency));
        Console.Error.WriteLine($"[mark] {phase} {ns}ns");
    }
}
