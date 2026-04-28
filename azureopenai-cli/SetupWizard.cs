using System.Text;

namespace AzureOpenAI_CLI;

/// <summary>
/// Interactive setup wizard (<c>--setup</c> / <c>--init-wizard</c> /
/// <c>az-ai setup</c>). Walks the user through configuring the Azure OpenAI
/// endpoint, API key, and a default model deployment, then persists the
/// values to <c>~/.azureopenai-cli.json</c> (0600 perms via
/// <see cref="UserConfig.Save"/>).
///
/// <para>
/// Invariants:
/// <list type="bullet">
///   <item>Never runs when stdin or stdout is redirected — interactive
///   prompts must never block a pipe / CI / script. The caller in
///   <see cref="Program"/> gates on <see cref="IsInteractiveTty"/> before
///   invoking <see cref="RunAsync"/>; the wizard re-checks defensively.</item>
///   <item>Never runs under <c>--raw</c> / <c>--json</c> (also gated by the
///   caller).</item>
///   <item>The API key is read character-by-character with <c>*</c> echoed
///   in place of each char; it is never printed to stdout or stderr in
///   plaintext.</item>
///   <item>Ctrl+C / Esc / EOF aborts with exit 130 and no partial writes —
///   <see cref="UserConfig.Save"/> is only called once every prompt has
///   succeeded.</item>
/// </list>
/// </para>
/// </summary>
internal static class SetupWizard
{
    /// <summary>
    /// Exit code for user-initiated cancellation (Ctrl+C / SIGINT / Esc / EOF).
    /// Matches the convention used elsewhere in <see cref="Program"/>.
    /// </summary>
    private const int ExitCanceled = 130;

    /// <summary>
    /// Run the wizard interactively. Returns a process exit code:
    /// 0 on success, 130 on user cancellation, 1 on validation failure.
    /// Persists via <see cref="UserConfig.Save"/> only after every prompt
    /// has succeeded — partial writes are not possible.
    /// </summary>
    internal static async Task<int> RunAsync()
    {
        // Defensive re-check: caller (Program.RunAsync) already gates on this,
        // but the wizard never trusts the caller for a security invariant.
        if (!IsInteractiveTty())
        {
            Console.Error.WriteLine(
                "[ERROR] Setup wizard requires an interactive terminal (stdin/stdout must not be redirected).");
            return 1;
        }

        // Touch await so the async signature stays meaningful and forward-
        // compatible with future async prompts (connectivity check, etc.).
        await Task.Yield();

        try
        {
            PrintBanner();

            var config = UserConfig.Load(quiet: true);

            var endpoint = PromptEndpoint(config.Endpoint);
            if (endpoint is null) return ExitCanceled;

            var apiKey = PromptApiKey(hasExisting: !string.IsNullOrEmpty(config.ApiKey));
            if (apiKey is null) return ExitCanceled;

            var (alias, deployment) = PromptDefaultModel(config);
            if (alias is null || deployment is null) return ExitCanceled;

            // All prompts succeeded — only now do we mutate + persist. No
            // partial writes can leak from an aborted wizard.
            config.Endpoint = endpoint;
            // Empty string from PromptApiKey signals "keep existing" — only
            // overwrite when the user actually typed a new key.
            if (apiKey.Length > 0) config.ApiKey = apiKey;
            config.Models[alias] = deployment;
            config.DefaultModel = alias;

            config.Save();

            PrintSuccess(config.LoadedFrom ?? UserConfig.DefaultPath);
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
    /// True when the wizard is safe to launch: both stdin and stdout are
    /// interactive TTYs. Guards against triggering the wizard in scripts,
    /// pipes, CI, or under <c>--raw</c> / <c>--json</c> consumers.
    /// </summary>
    internal static bool IsInteractiveTty()
    {
        if (Console.IsInputRedirected) return false;
        if (Console.IsOutputRedirected) return false;
        return true;
    }

    private static void PrintBanner()
    {
        Console.WriteLine();
        Console.WriteLine("Welcome to az-ai setup!");
        Console.WriteLine("This wizard will configure your Azure OpenAI credentials and save");
        Console.WriteLine($"them to {UserConfig.DefaultPath} (permissions 0600).");
        Console.WriteLine();
        Console.WriteLine("Press Ctrl+C at any time to abort without saving.");
        Console.WriteLine();
    }

    private static string? PromptEndpoint(string? existing)
    {
        // Env var fallback is informational only — we still write to UserConfig.
        if (string.IsNullOrEmpty(existing))
            existing = Environment.GetEnvironmentVariable("AZUREOPENAIENDPOINT");

        while (true)
        {
            var suffix = string.IsNullOrEmpty(existing) ? "" : $" [{existing}]";
            Console.Write($"Azure OpenAI endpoint URL{suffix}: ");
            var input = Console.ReadLine();
            if (input is null) return null; // EOF / Ctrl+D

            input = input.Trim();
            if (string.IsNullOrEmpty(input) && !string.IsNullOrEmpty(existing))
                input = existing;

            if (string.IsNullOrEmpty(input))
            {
                Console.WriteLine("  Endpoint is required (e.g. https://my-resource.openai.azure.com).");
                continue;
            }

            if (!input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("  Endpoint must start with https://");
                continue;
            }

            if (!Uri.TryCreate(input, UriKind.Absolute, out _))
            {
                Console.WriteLine("  That doesn't look like a valid URL. Try again.");
                continue;
            }

            return input.TrimEnd('/');
        }
    }

    private static string? PromptApiKey(bool hasExisting)
    {
        while (true)
        {
            var suffix = hasExisting ? " [press Enter to keep existing]" : "";
            Console.Write($"Azure OpenAI API key (input hidden){suffix}: ");
            var key = ReadMaskedLine();
            Console.WriteLine();

            if (key is null) return null; // Esc / EOF

            if (key.Length == 0)
            {
                if (hasExisting)
                {
                    // Empty result = "keep existing". Caller (RunAsync)
                    // interprets length==0 as no overwrite.
                    return string.Empty;
                }
                Console.WriteLine("  API key is required.");
                continue;
            }

            if (key.Length < 16)
            {
                Console.WriteLine("  That key looks too short — Azure OpenAI keys are typically 32+ chars. Try again.");
                continue;
            }

            return key;
        }
    }

    private static (string? alias, string? deployment) PromptDefaultModel(UserConfig config)
    {
        var existingDeployment = !string.IsNullOrEmpty(config.DefaultModel)
            && config.Models.TryGetValue(config.DefaultModel, out var ed)
                ? ed
                : null;
        var defaultValue = existingDeployment ?? "gpt-4o-mini";

        Console.Write($"Default model deployment name [{defaultValue}]: ");
        var input = Console.ReadLine();
        if (input is null) return (null, null);

        input = input.Trim();
        if (string.IsNullOrEmpty(input))
            input = defaultValue;

        // Reuse the existing alias if the user already had one configured;
        // otherwise default to "default" for predictability.
        var alias = !string.IsNullOrEmpty(config.DefaultModel)
            ? config.DefaultModel
            : "default";

        return (alias, input);
    }

    private static void PrintSuccess(string path)
    {
        Console.WriteLine();
        Console.WriteLine($"Configuration saved to {path}");
        Console.WriteLine();
        Console.WriteLine("You're all set. Try:");
        Console.WriteLine("  az-ai \"Hello, world\"");
        Console.WriteLine();
        Console.WriteLine("Re-run this wizard any time with: az-ai --setup");
        Console.WriteLine("Edit individual keys with:        az-ai --config set <key>=<value>");
        Console.WriteLine();
    }

    /// <summary>
    /// Read a line from stdin with each character echoed as <c>*</c>. Returns
    /// the entered string (possibly empty) on Enter, or null on Esc / EOF /
    /// console-not-a-tty fallback failure. Supports Backspace. Never echoes
    /// the actual key material to any output stream.
    /// </summary>
    private static string? ReadMaskedLine()
    {
        // Caller already gated on IsInteractiveTty(); ReadKey should be safe.
        // Defensive try/catch handles exotic hosts (e.g. some CI containers
        // claim a TTY but throw on ReadKey).
        try
        {
            var buffer = new StringBuilder();
            while (true)
            {
                var keyInfo = Console.ReadKey(intercept: true);
                if (keyInfo.Key == ConsoleKey.Enter)
                {
                    return buffer.ToString();
                }
                if (keyInfo.Key == ConsoleKey.Backspace)
                {
                    if (buffer.Length > 0)
                    {
                        buffer.Length -= 1;
                        Console.Write("\b \b");
                    }
                    continue;
                }
                if (keyInfo.Key == ConsoleKey.Escape)
                {
                    // Esc = cancel, exit-130 path.
                    return null;
                }
                // Ignore control chars (Tab, arrows, F-keys, etc.).
                if (keyInfo.KeyChar == '\0' || char.IsControl(keyInfo.KeyChar))
                    continue;

                buffer.Append(keyInfo.KeyChar);
                Console.Write('*');
            }
        }
        catch (InvalidOperationException)
        {
            // Console.ReadKey throws on pseudo-TTYs that pass the redirect
            // check but lack a real console (some container runtimes,
            // dotnet test capture, certain CI runners with tty: true but no
            // /dev/tty wiring, restricted hosts, WSL + ssh -t edge cases).
            // Fail closed: do NOT fall back to Console.ReadLine, which would
            // echo the secret to scrollback / tmux logs / TTY loggers. Emit
            // a one-line stderr warning and return null so the caller short-
            // circuits to ExitCanceled (130) without ever accepting plaintext.
            // Newman audit H-1.
            Console.Error.WriteLine(
                "[ERROR] Cannot read masked input on this terminal; refusing to "
                + "accept API key in plaintext. Set AZUREOPENAIAPI environment "
                + "variable instead.");
            return null;
        }
    }
}
