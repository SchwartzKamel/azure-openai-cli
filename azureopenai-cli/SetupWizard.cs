using System.Net;
using System.Text.Json;

namespace AzureOpenAI_CLI;

/// <summary>
/// Interactive setup wizard (<c>--setup</c> / <c>az-ai setup</c>).
/// Guides the user through endpoint, API key, and model configuration.
/// Runs before credential resolution so it works even when the current
/// config is broken — that's the whole point.
/// </summary>
internal static class SetupWizard
{
    internal static async Task<int> RunAsync()
    {
        Console.WriteLine("╭──────────────────────────────────────────────╮");
        Console.WriteLine("│  az-ai setup wizard                         │");
        Console.WriteLine("╰──────────────────────────────────────────────╯");
        Console.WriteLine();

        // ── Step 1: Endpoint ──────────────────────────────────────────
        var currentEndpoint = Environment.GetEnvironmentVariable("AZUREOPENAIENDPOINT");
        if (!string.IsNullOrWhiteSpace(currentEndpoint))
            Console.WriteLine($"  Current endpoint: {currentEndpoint}");

        Console.Write("  Azure OpenAI endpoint URL (https://...): ");
        var endpoint = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            if (!string.IsNullOrWhiteSpace(currentEndpoint))
            {
                endpoint = currentEndpoint;
                Console.WriteLine($"  (keeping current: {endpoint})");
            }
            else
            {
                Console.Error.WriteLine("[ERROR] Endpoint is required.");
                return 1;
            }
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri)
            || endpointUri.Scheme != "https")
        {
            Console.Error.WriteLine($"[ERROR] Invalid endpoint: '{endpoint}'. Must be a valid HTTPS URL.");
            return 1;
        }

        // ── Step 2: API Key ───────────────────────────────────────────
        var currentKey = Environment.GetEnvironmentVariable("AZUREOPENAIAPI");
        var hasCurrentKey = !string.IsNullOrWhiteSpace(currentKey);
        if (hasCurrentKey)
            Console.WriteLine($"  Current API key: {currentKey![..4]}...{currentKey[^4..]}");

        Console.Write("  API key (paste, or press Enter to keep current): ");
        var apiKey = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            if (hasCurrentKey)
            {
                apiKey = currentKey;
                Console.WriteLine("  (keeping current key)");
            }
            else
            {
                Console.Error.WriteLine("[ERROR] API key is required.");
                return 1;
            }
        }

        // ── Step 3: Connectivity test ─────────────────────────────────
        Console.Write("  Testing connectivity... ");
        var reachable = await TestConnectivityAsync(endpointUri);
        if (reachable)
        {
            Console.WriteLine("OK");
        }
        else
        {
            Console.WriteLine("FAILED");
            Console.Error.WriteLine($"  [WARNING] Could not reach {endpointUri.Host}. The endpoint may be wrong or temporarily down.");
            Console.Write("  Continue anyway? [y/N]: ");
            var cont = Console.ReadLine()?.Trim();
            if (!string.Equals(cont, "y", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("  Setup cancelled.");
                return 1;
            }
        }

        // ── Step 4: Model deployment ──────────────────────────────────
        var currentModel = Environment.GetEnvironmentVariable("AZUREOPENAIMODEL");
        if (!string.IsNullOrWhiteSpace(currentModel))
            Console.WriteLine($"  Current model: {currentModel}");

        Console.Write("  Default model deployment name (e.g. gpt-4o-mini): ");
        var model = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(model))
        {
            model = currentModel ?? "gpt-4o-mini";
            Console.WriteLine($"  (using: {model})");
        }

        // ── Step 5: Model alias ───────────────────────────────────────
        Console.Write("  Alias for this model (e.g. 'fast', or press Enter to skip): ");
        var alias = Console.ReadLine()?.Trim();

        // ── Step 6: Save config ───────────────────────────────────────
        var config = UserConfig.Load(quiet: true);
        if (!string.IsNullOrWhiteSpace(alias))
        {
            config.Models[alias] = model;
            config.DefaultModel = alias;
        }
        else
        {
            config.DefaultModel = model;
        }
        config.Save();
        Console.WriteLine($"  Config saved to {config.LoadedFrom ?? UserConfig.DefaultPath}");

        // ── Step 7: Write .env file ───────────────────────────────────
        Console.Write("  Write a .env file in the current directory? [Y/n]: ");
        var writeEnv = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(writeEnv)
            || string.Equals(writeEnv, "y", StringComparison.OrdinalIgnoreCase))
        {
            var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
            var lines = new List<string>
            {
                $"AZUREOPENAIENDPOINT={endpoint}",
                $"AZUREOPENAIAPI={apiKey}",
                $"AZUREOPENAIMODEL={model}",
            };
            File.WriteAllLines(envPath, lines);
            Console.WriteLine($"  .env written to {envPath}");
        }

        // ── Step 8: Print shell exports ───────────────────────────────
        Console.WriteLine();
        Console.WriteLine("  Add these to your shell profile (~/.bashrc, ~/.zshrc, etc.):");
        Console.WriteLine();
        Console.WriteLine($"    export AZUREOPENAIENDPOINT=\"{endpoint}\"");
        Console.WriteLine($"    export AZUREOPENAIAPI=\"{apiKey}\"");
        Console.WriteLine($"    export AZUREOPENAIMODEL=\"{model}\"");
        Console.WriteLine();
        Console.WriteLine("  Setup complete. Try: az-ai \"Hello, world!\"");

        return 0;
    }

    /// <summary>
    /// Quick connectivity test — HEAD request to the endpoint root.
    /// Returns true if we get any HTTP response (even 4xx); false on DNS/TCP failure.
    /// </summary>
    private static async Task<bool> TestConnectivityAsync(Uri endpoint)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var request = new HttpRequestMessage(HttpMethod.Head, endpoint);
            var response = await http.SendAsync(request);
            return true; // Any HTTP response = endpoint is reachable
        }
        catch
        {
            return false;
        }
    }
}
