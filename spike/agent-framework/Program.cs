// Phase 0 spike — Microsoft Agent Framework hello-agent harness.
//
// Three auth paths gated by --auth:
//   apikey   → Azure OpenAI direct + key  (preserves current env contract)
//   aad      → Azure OpenAI direct + DefaultAzureCredential (AAD / Managed Identity)
//   foundry  → Azure AI Foundry project endpoint + DefaultAzureCredential
//
// Output is intentionally minimal so the bench harness can time it cleanly:
//   stdout = model text only (raw)
//   stderr = phase markers (auth-ready, request-sent, first-token, complete)
//            with monotonic ns timestamps for benchmark parsing.
//
// NO retry, NO spinner, NO tool-calling yet. Phase 0 measures the core path only.
//
// Env vars (matches current az-ai contract):
//   AZUREOPENAIENDPOINT   Azure OpenAI endpoint URL (apikey, aad)
//   AZUREOPENAIAPI        API key (apikey)
//   AZUREOPENAIMODEL      Deployment name
//   AZURE_FOUNDRY_PROJECT_ENDPOINT  (foundry)

using System.ClientModel;
using System.Diagnostics;
using Azure.AI.OpenAI;
using Azure.Identity;
using dotenv.net;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

namespace AzAi.Spike.AgentFramework;

internal static class Program
{
    private static readonly long _processStartTicks = Stopwatch.GetTimestamp();

    private static async Task<int> Main(string[] args)
    {
        DotEnv.Load(options: new DotEnvOptions(ignoreExceptions: true, probeForEnv: true));

        var (auth, prompt, system) = ParseArgs(args);
        Mark("args-parsed");

        try
        {
            var agent = await BuildAgentAsync(auth);
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

    private static (string auth, string prompt, string? system) ParseArgs(string[] args)
    {
        string auth = "apikey";
        string? prompt = null;
        string? system = null;

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
                case "-h":
                case "--help":
                    Console.WriteLine("af-spike --auth {apikey|aad|foundry} --prompt <text> [--system <text>]");
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

        return (auth.ToLowerInvariant(), prompt!, system);
    }

    private static Task<AIAgent> BuildAgentAsync(string auth)
    {
        var deployment = Env("AZUREOPENAIMODEL") ?? "gpt-4o-mini";
        var systemPrompt = "You are a fast, concise CLI assistant. No fluff.";

        return auth switch
        {
            "apikey" => Task.FromResult(BuildApiKeyAgent(deployment, systemPrompt)),
            "aad" => Task.FromResult(BuildAadAgent(deployment, systemPrompt)),
            "foundry" => Task.FromResult(BuildFoundryAgent(deployment, systemPrompt)),
            _ => throw new ArgumentException($"unknown --auth value: {auth} (apikey|aad|foundry)")
        };
    }

    private static AIAgent BuildApiKeyAgent(string deployment, string systemPrompt)
    {
        var endpoint = Env("AZUREOPENAIENDPOINT") ?? throw new InvalidOperationException("AZUREOPENAIENDPOINT not set");
        var key = Env("AZUREOPENAIAPI") ?? throw new InvalidOperationException("AZUREOPENAIAPI not set");

        var client = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(key));
        return client.GetChatClient(deployment).AsIChatClient().AsAIAgent(instructions: systemPrompt);
    }

    private static AIAgent BuildAadAgent(string deployment, string systemPrompt)
    {
        var endpoint = Env("AZUREOPENAIENDPOINT") ?? throw new InvalidOperationException("AZUREOPENAIENDPOINT not set");
        var client = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());
        return client.GetChatClient(deployment).AsIChatClient().AsAIAgent(instructions: systemPrompt);
    }

    private static AIAgent BuildFoundryAgent(string deployment, string systemPrompt)
    {
        // NOTE: Microsoft.Agents.AI.AzureAI surface — verify exact factory in Phase 0.
        // Foundry uses persistent server-side agents; this skeleton treats it like
        // a fresh ephemeral agent for benchmark parity. Update once we confirm the
        // package's public API.
        var endpoint = Env("AZURE_FOUNDRY_PROJECT_ENDPOINT")
            ?? throw new InvalidOperationException("AZURE_FOUNDRY_PROJECT_ENDPOINT not set");
        throw new NotImplementedException(
            "Foundry path is a Phase 0 stub. Wire to PersistentAgentsClient once package surface is confirmed. " +
            $"Endpoint: {endpoint}, Deployment: {deployment}");
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
