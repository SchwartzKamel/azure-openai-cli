using System.Text;

namespace AzureOpenAI_CLI;

/// <summary>
/// Interactive setup wizard (<c>--setup</c> / <c>--init-wizard</c> /
/// <c>az-ai setup</c>). S03E11 *The Wizard, Reprise* extends the original
/// Azure-only flow into a provider-aware sequence:
///
/// <list type="number">
///   <item>Pick the default provider (azure / openai / groq / together / cloudflare).</item>
///   <item>Collect the credentials that provider needs.</item>
///   <item>Optionally loop to add a second (third, ...) provider.</item>
///   <item>Validate compat model strings via
///     <see cref="OpenAiCompatAdapter.ParseCompatModels"/>, then write
///     <c>~/.config/az-ai/env</c> with <c>[provider:NAME]</c> sections (E10
///     format) plus default-section back-compat exports
///     (<c>AZUREOPENAIENDPOINT</c>, <c>AZUREOPENAIAPI</c>,
///     <c>AZUREOPENAIMODEL</c>, <c>AZ_AI_COMPAT_MODELS</c>).</item>
/// </list>
///
/// <para>Invariants (preserved from S02 implementation):</para>
/// <list type="bullet">
///   <item>Refuses politely under <c>--raw</c> / <c>--json</c> / non-TTY.</item>
///   <item>API key reads via masked input -- never echoes plaintext.</item>
///   <item>Existing env file is backed up to <c>env.bak.&lt;timestamp&gt;</c>
///     before overwrite; idempotent re-runs (same answers) skip the backup.</item>
///   <item>chmod 600 on Unix (best-effort, matches UserConfig / Preferences).</item>
/// </list>
/// </summary>
internal static class SetupWizard
{
    /// <summary>Exit code for user-initiated cancellation (Ctrl+C / EOF).</summary>
    private const int ExitCanceled = 130;

    /// <summary>
    /// Run the wizard interactively. Returns 0 on success, 130 on user cancel.
    /// </summary>
    internal static async Task<int> RunAsync()
    {
        if (!IsInteractiveTty())
        {
            Console.Error.WriteLine(
                "[ERROR] Setup wizard requires an interactive terminal (stdin/stdout must not be redirected).");
            Console.Error.WriteLine(
                "        Set credentials manually instead -- see README \"Power user / scripted setup\".");
            return 1;
        }

        await Task.Yield();

        try
        {
            PrintBanner();

            var defaultProvider = PromptProviderChoice(
                prompt: "Default provider",
                highlight: SmartDefaultProvider());
            if (defaultProvider is null) return ExitCanceled;

            var answers = new List<ProviderAnswer>();
            var first = PromptProvider(defaultProvider);
            if (first is null) return ExitCanceled;
            answers.Add(first);

            while (true)
            {
                if (answers.Count >= WizardProviders.All.Length) break;
                var configured = answers.Select(a => a.Provider).ToHashSet(StringComparer.Ordinal);
                var remaining = WizardProviders.All.Where(p => !configured.Contains(p)).ToArray();
                if (remaining.Length == 0) break;

                Console.WriteLine();
                Console.Write($"Add another provider? [y/N] (remaining: {string.Join(", ", remaining)}): ");
                var ans = Console.ReadLine();
                if (ans is null) return ExitCanceled;
                ans = ans.Trim();
                if (!string.Equals(ans, "y", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(ans, "yes", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                var next = PromptProviderChoice(
                    prompt: "Which provider",
                    highlight: remaining[0],
                    allow: remaining);
                if (next is null) return ExitCanceled;
                var na = PromptProvider(next);
                if (na is null) return ExitCanceled;
                answers.Add(na);
            }

            var path = WizardSession.DefaultEnvFilePath();
            var content = WizardSession.BuildEnvFileContent(answers, defaultProvider, DateTimeOffset.UtcNow);

            if (File.Exists(path))
            {
                Console.WriteLine();
                Console.WriteLine($"An existing env file is at {path}.");
                Console.Write("Back up and overwrite? [Y/n]: ");
                var ans = Console.ReadLine();
                if (ans is null) return ExitCanceled;
                ans = ans.Trim();
                if (string.Equals(ans, "n", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(ans, "no", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Aborted. No changes saved.");
                    return ExitCanceled;
                }
            }

            var backup = WizardSession.WriteEnvFile(path, content, DateTimeOffset.UtcNow);

            PrintSuccess(path, backup, answers, defaultProvider);
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine();
            Console.WriteLine("Setup canceled. No changes saved.");
            return ExitCanceled;
        }
    }

    /// <summary>
    /// True when the wizard is safe to launch (TTY on stdin and stdout).
    /// </summary>
    internal static bool IsInteractiveTty()
    {
        if (Console.IsInputRedirected) return false;
        if (Console.IsOutputRedirected) return false;
        return true;
    }

    /// <summary>
    /// Smart default for the provider menu: <c>azure</c> if AOAI endpoint is
    /// already exported (existing user re-running the wizard); else
    /// <c>openai</c> (the most common net-new install in the multi-provider era).
    /// </summary>
    internal static string SmartDefaultProvider()
    {
        var ep = Environment.GetEnvironmentVariable("AZUREOPENAIENDPOINT");
        return !string.IsNullOrWhiteSpace(ep) ? WizardProviders.Azure : WizardProviders.OpenAI;
    }

    private static void PrintBanner()
    {
        Console.WriteLine();
        Console.WriteLine("Welcome to az-ai setup!");
        Console.WriteLine("This wizard will configure your providers and save them to");
        Console.WriteLine($"  {WizardSession.DefaultEnvFilePath()}");
        Console.WriteLine("(file mode 0600 on Unix; existing files are backed up first).");
        Console.WriteLine();
        Console.WriteLine("Press Ctrl+C at any time to abort without saving.");
        Console.WriteLine();
    }

    private static string? PromptProviderChoice(string prompt, string highlight, string[]? allow = null)
    {
        var choices = allow ?? WizardProviders.All;
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine(prompt + ":");
            for (int i = 0; i < choices.Length; i++)
            {
                var marker = string.Equals(choices[i], highlight, StringComparison.Ordinal) ? "*" : " ";
                Console.WriteLine($"  {marker} {i + 1}) {choices[i]}");
            }
            Console.Write($"Pick [{highlight}]: ");
            var input = Console.ReadLine();
            if (input is null) return null;
            input = input.Trim();
            if (input.Length == 0) return highlight;
            if (int.TryParse(input, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var idx)
                && idx >= 1 && idx <= choices.Length)
            {
                return choices[idx - 1];
            }
            if (WizardProviders.TryCanonicalize(input, out var canon)
                && Array.Exists(choices, c => string.Equals(c, canon, StringComparison.Ordinal)))
            {
                return canon;
            }
            Console.WriteLine($"  Not a valid choice. Pick a number 1..{choices.Length} or a name from the list.");
        }
    }

    private static ProviderAnswer? PromptProvider(string provider)
    {
        return string.Equals(provider, WizardProviders.Azure, StringComparison.Ordinal)
            ? PromptAzure()
            : PromptCompat(provider);
    }

    private static ProviderAnswer? PromptAzure()
    {
        var endpoint = PromptEndpoint(Environment.GetEnvironmentVariable("AZUREOPENAIENDPOINT"));
        if (endpoint is null) return null;

        var apiKey = PromptApiKey("Azure OpenAI API key", required: true);
        if (apiKey is null) return null;

        var models = PromptLine(
            "Azure model deployment name(s), comma-separated",
            defaultValue: "gpt-4o-mini",
            required: true);
        if (models is null) return null;

        return new ProviderAnswer(
            Provider: WizardProviders.Azure,
            ApiKey: apiKey,
            Models: models,
            Endpoint: endpoint);
    }

    private static ProviderAnswer? PromptCompat(string provider)
    {
        var apiKey = PromptApiKey($"{Capitalize(provider)} API key", required: true);
        if (apiKey is null) return null;

        var defaultModel = provider switch
        {
            "openai" => "gpt-4o-mini",
            "groq" => "llama-3.1-70b-versatile",
            "together" => "meta-llama/Meta-Llama-3.1-8B-Instruct-Turbo",
            "cloudflare" => "@cf/meta/llama-3.1-8b-instruct",
            _ => string.Empty,
        };

        while (true)
        {
            var models = PromptLine(
                $"{Capitalize(provider)} model name(s), comma-separated",
                defaultValue: defaultModel,
                required: true);
            if (models is null) return null;

            var rejection = WizardSession.ValidateCompatModels(provider, models);
            if (rejection is not null)
            {
                Console.WriteLine("  " + rejection);
                continue;
            }

            string? accountId = null;
            if (string.Equals(provider, WizardProviders.Cloudflare, StringComparison.Ordinal))
            {
                accountId = PromptLine(
                    "Cloudflare account id",
                    defaultValue: Environment.GetEnvironmentVariable("CLOUDFLARE_ACCOUNT_ID"),
                    required: true);
                if (accountId is null) return null;
            }

            return new ProviderAnswer(
                Provider: provider,
                ApiKey: apiKey,
                Models: models,
                AccountId: accountId);
        }
    }

    private static string? PromptEndpoint(string? existing)
    {
        while (true)
        {
            var suffix = string.IsNullOrEmpty(existing) ? "" : $" [{existing}]";
            Console.Write($"Azure OpenAI endpoint URL{suffix}: ");
            var input = Console.ReadLine();
            if (input is null) return null;

            input = input.Trim();
            if (string.IsNullOrEmpty(input) && !string.IsNullOrEmpty(existing))
                input = existing;

            if (string.IsNullOrEmpty(input))
            {
                Console.WriteLine("  Endpoint is required (e.g. https://my-resource.openai.azure.com).");
                continue;
            }

            if (!TryParseEndpointUrl(input, out var rejection))
            {
                Console.WriteLine($"  {rejection}");
                continue;
            }
            return input.TrimEnd('/');
        }
    }

    /// <summary>
    /// Validates that <paramref name="url"/> is a well-formed Azure OpenAI
    /// resource root URL.
    /// </summary>
    internal static bool TryParseEndpointUrl(string url, out string? rejection)
    {
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            rejection = "Endpoint must start with https://";
            return false;
        }
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            rejection = "That doesn't look like a valid URL. Try again.";
            return false;
        }
        if (uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            rejection =
                "Endpoint must be a root URL with no path, query, or fragment "
                + "(e.g. https://my-resource.openai.azure.com).";
            return false;
        }
        rejection = null;
        return true;
    }

    private static string? PromptApiKey(string label, bool required)
    {
        while (true)
        {
            Console.Write($"{label} (input hidden): ");
            var key = ReadMaskedLine();
            Console.WriteLine();

            if (key is null) return null;

            if (key.Length == 0)
            {
                if (!required) return string.Empty;
                Console.WriteLine("  API key is required.");
                continue;
            }
            if (key.Length < 8)
            {
                Console.WriteLine("  That key looks too short. Try again.");
                continue;
            }
            return key;
        }
    }

    private static string? PromptLine(string label, string? defaultValue, bool required)
    {
        while (true)
        {
            var suffix = string.IsNullOrEmpty(defaultValue) ? "" : $" [{defaultValue}]";
            Console.Write($"{label}{suffix}: ");
            var input = Console.ReadLine();
            if (input is null) return null;
            input = input.Trim();
            if (input.Length == 0)
            {
                if (!string.IsNullOrEmpty(defaultValue)) return defaultValue;
                if (!required) return string.Empty;
                Console.WriteLine("  Value is required.");
                continue;
            }
            return input;
        }
    }

    private static void PrintSuccess(string path, string? backup, IReadOnlyList<ProviderAnswer> answers, string defaultProvider)
    {
        Console.WriteLine();
        Console.WriteLine($"Configuration saved to {path}");
        if (!string.IsNullOrEmpty(backup))
        {
            Console.WriteLine($"Previous file backed up to {backup}");
        }
        Console.WriteLine($"Default provider: {defaultProvider}");
        Console.WriteLine($"Providers configured: {string.Join(", ", answers.Select(a => a.Provider))}");
        Console.WriteLine();
        Console.WriteLine("You're all set. Try:");
        Console.WriteLine("  az-ai \"Hello, world\"");
        Console.WriteLine();
        Console.WriteLine("Re-run the wizard with: az-ai --setup");
        Console.WriteLine();
    }

    private static string Capitalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        if (char.IsUpper(s[0])) return s;
        return char.ToUpperInvariant(s[0]) + s.Substring(1);
    }

    /// <summary>
    /// Read a line from stdin echoing <c>*</c> per character. Returns null on
    /// Esc / EOF / pseudo-TTY failure (Newman audit H-1: never falls back to
    /// Console.ReadLine, which would echo plaintext to scrollback).
    /// </summary>
    private static string? ReadMaskedLine()
    {
        try
        {
            var buffer = new StringBuilder();
            while (true)
            {
                var keyInfo = Console.ReadKey(intercept: true);
                if (keyInfo.Key == ConsoleKey.Enter) return buffer.ToString();
                if (keyInfo.Key == ConsoleKey.Backspace)
                {
                    if (buffer.Length > 0)
                    {
                        buffer.Length -= 1;
                        Console.Write("\b \b");
                    }
                    continue;
                }
                if (keyInfo.Key == ConsoleKey.Escape) return null;
                if (keyInfo.KeyChar == '\0' || char.IsControl(keyInfo.KeyChar)) continue;
                buffer.Append(keyInfo.KeyChar);
                Console.Write('*');
            }
        }
        catch (InvalidOperationException)
        {
            // Newman H-1: fail closed; never fall back to Console.ReadLine.
            Console.Error.WriteLine(
                "[ERROR] Cannot read masked input on this terminal; refusing to "
                + "accept API key in plaintext. Set the appropriate API_KEY "
                + "environment variable instead (see README).");
            return null;
        }
    }
}
