using System.Diagnostics;
using System.Runtime.InteropServices;
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using AzureOpenAI_CLI.Credentials;

namespace AzureOpenAI_CLI.Setup;

/// <summary>
/// Interactive first-run wizard: collects endpoint, API key (masked), and model
/// deployment name, pings the service to validate the triple, then persists the
/// config + credentials via the injected <see cref="ICredentialStore"/>.
/// </summary>
/// <remarks>
/// IO streams are injected for unit-testability. When <c>input</c> is
/// <see cref="Console.In"/> and stdin is a TTY we use <see cref="Console.ReadKey"/>
/// to implement masked key entry (bullet echo); otherwise we fall back to
/// <see cref="TextReader.ReadLineAsync()"/> so tests can pipe lines in deterministically.
/// </remarks>
internal sealed class FirstRunWizard
{
    private const int MaxEndpointAttempts = 3;
    private const int MinKeyLength = 32;
    private const int PingTimeoutSeconds = 10;

    private readonly UserConfig _config;
    private readonly ICredentialStore _store;
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private readonly TextWriter _errorOutput;
    private readonly bool _useConsoleKeyMasking;

    public FirstRunWizard(
        UserConfig config,
        ICredentialStore store,
        TextReader? input = null,
        TextWriter? output = null,
        TextWriter? errorOutput = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _input = input ?? Console.In;
        _output = output ?? Console.Out;
        _errorOutput = errorOutput ?? Console.Error;

        // Masked bullet-echo key entry only makes sense when the injected reader is
        // the real console AND stdin isn't redirected. In test harnesses a
        // StringReader is passed in and we just ReadLineAsync().
        _useConsoleKeyMasking = ReferenceEquals(_input, Console.In) && !Console.IsInputRedirected;
    }

    /// <summary>
    /// Runs the full wizard. Returns <c>true</c> when credentials were validated
    /// (or explicitly force-saved by the user) and persisted; <c>false</c> on
    /// cancellation, repeated validation failure without force-save, or fatal
    /// input errors.
    /// </summary>
    public async Task<bool> RunAsync(CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ConsoleCancelEventHandler? handler = (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            Console.CancelKeyPress += handler;
        }
        catch
        {
            handler = null;
        }

        string? endpoint = null;
        string? apiKey = null;
        List<string>? models = null;

        try
        {
            await _output.WriteLineAsync("Welcome to az-ai! Let's get you set up. (takes ~30 seconds)").ConfigureAwait(false);
            await _output.WriteLineAsync().ConfigureAwait(false);

            // ── Outer retry loop: validation failures can restart at any stage. ──
            // `stage` selects which prompt we resume at on retry.
            //   0 = endpoint, 1 = key, 2 = model, 3 = save (no prompts)
            int stage = 0;

            while (true)
            {
                cts.Token.ThrowIfCancellationRequested();

                if (stage <= 0)
                {
                    endpoint = await PromptEndpointAsync(cts.Token).ConfigureAwait(false);
                    if (endpoint == null) return await CancelAsync(cts.Token).ConfigureAwait(false);
                    stage = 1;
                }

                if (stage <= 1)
                {
                    apiKey = await PromptApiKeyAsync(cts.Token).ConfigureAwait(false);
                    if (apiKey == null) return await CancelAsync(cts.Token).ConfigureAwait(false);
                    stage = 2;
                }

                if (stage <= 2)
                {
                    models = await PromptModelsAsync(cts.Token).ConfigureAwait(false);
                    if (models == null) return await CancelAsync(cts.Token).ConfigureAwait(false);
                    stage = 3;
                }

                // Validation ping.
                var validation = await ValidateAsync(endpoint!, apiKey!, models![0], cts.Token).ConfigureAwait(false);
                if (validation.Success)
                {
                    break;
                }

                // Ask the user how to recover based on the failure category.
                var (prompt, retryStage) = validation.Category switch
                {
                    ValidationFailure.Auth => ("Try again? [y/N]", 1),
                    ValidationFailure.ModelNotFound => ("Try again? [y/N]", 2),
                    ValidationFailure.Timeout => ("Try again? [y/N]", 0),
                    ValidationFailure.Other => ("Save creds anyway without validation? [y/N]", -1),
                    _ => ("Try again? [y/N]", 0),
                };

                bool yes = await ConfirmYesAsync(prompt, cts.Token).ConfigureAwait(false);
                if (!yes)
                {
                    await _errorOutput.WriteLineAsync("Setup aborted — no changes saved.").ConfigureAwait(false);
                    return false;
                }

                if (retryStage < 0)
                {
                    // Force-save path (Other). Break out; save without validation success.
                    break;
                }

                stage = retryStage;
            }

            cts.Token.ThrowIfCancellationRequested();

            // ── Persist. This is the only path that touches disk / credential store. ──
            _config.Endpoint = endpoint!.Trim();
            _config.AvailableModels = models!;
            _config.ActiveModel = models![0];

            try
            {
                _store.Store(apiKey!);
            }
            catch (CredentialStoreException ex)
            {
                await _errorOutput.WriteLineAsync($"Failed to save credential: {ex.Message}").ConfigureAwait(false);
                return false;
            }

            _config.ApiKeyProvider = _store.ProviderName;
            _config.ApiKeyFingerprint = UserConfig.ComputeFingerprint(apiKey!);
            _config.Save();

            var configPath = UserConfig.GetConfigPath();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await _output.WriteLineAsync($"Saved to {configPath} (API key DPAPI-encrypted for current user)").ConfigureAwait(false);
            }
            else
            {
                await _output.WriteLineAsync($"Saved to {configPath} (mode 0600)").ConfigureAwait(false);
            }
            await _output.WriteLineAsync("Run 'az-ai --config show' anytime to inspect settings.").ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return await CancelAsync(cts.Token).ConfigureAwait(false);
        }
        finally
        {
            // Best-effort: zero the key buffer so it doesn't linger on the managed heap.
            if (apiKey != null)
            {
                // Strings are immutable; best we can do is drop the reference. No-op intentionally documented.
                apiKey = null;
            }

            if (handler != null)
            {
                try { Console.CancelKeyPress -= handler; } catch { /* best effort */ }
            }
        }
    }

    // ─── Stage 1: endpoint ────────────────────────────────────────────

    private async Task<string?> PromptEndpointAsync(CancellationToken ct)
    {
        for (int attempt = 0; attempt < MaxEndpointAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            await _output.WriteLineAsync("Azure OpenAI endpoint URL").ConfigureAwait(false);
            await _output.WriteLineAsync("  e.g. https://my-resource.openai.azure.com/").ConfigureAwait(false);
            await _output.WriteAsync("> ").ConfigureAwait(false);
            await _output.FlushAsync(ct).ConfigureAwait(false);

            string? line = await ReadLineAsync(ct).ConfigureAwait(false);
            if (line == null) return null;
            line = line.Trim();

            if (IsValidEndpoint(line))
            {
                await _output.WriteLineAsync().ConfigureAwait(false);
                return line;
            }

            await _errorOutput.WriteLineAsync("Must be a valid https:// URL, e.g. https://my-resource.openai.azure.com/").ConfigureAwait(false);
        }

        await _errorOutput.WriteLineAsync("Too many invalid endpoints. Aborting.").ConfigureAwait(false);
        return null;
    }

    private static bool IsValidEndpoint(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri)) return false;
        if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase)) return false;
        if (string.IsNullOrEmpty(uri.Host)) return false;
        // Reject bare IPs — Azure endpoints are always named hostnames. Catches
        // 127.0.0.1, file://-like tricks, and IPv6 literals.
        if (uri.HostNameType == UriHostNameType.IPv4 || uri.HostNameType == UriHostNameType.IPv6) return false;
        return true;
    }

    // ─── Stage 2: API key (masked) ────────────────────────────────────

    private async Task<string?> PromptApiKeyAsync(CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            // Mickey's "announcement": give a screen reader something humane
            // to say *before* a stream of bullet glyphs starts arriving. Only
            // emitted on the masked-console path — when stdin is redirected
            // we never echo masking glyphs, so the line would be a lie.
            if (_useConsoleKeyMasking)
            {
                await _output.WriteLineAsync("Your key will be masked as you type. Press Enter when done.").ConfigureAwait(false);
            }
            await _output.WriteLineAsync("API key (input hidden)").ConfigureAwait(false);
            await _output.WriteAsync("> ").ConfigureAwait(false);
            await _output.FlushAsync(ct).ConfigureAwait(false);

            string? key = _useConsoleKeyMasking
                ? ReadMaskedFromConsole(ct)
                : await ReadLineAsync(ct).ConfigureAwait(false);

            if (key == null) return null;
            key = key.Trim();

            if (string.IsNullOrEmpty(key))
            {
                await _errorOutput.WriteLineAsync("API key cannot be empty.").ConfigureAwait(false);
                continue;
            }

            if (key.Length < MinKeyLength)
            {
                await _errorOutput.WriteLineAsync($"API key is shorter than expected ({key.Length} chars; Azure keys are typically 84).").ConfigureAwait(false);
                bool ok = await ConfirmYesAsync("Use it anyway? [y/N]", ct).ConfigureAwait(false);
                if (!ok) continue;
            }

            await _output.WriteLineAsync().ConfigureAwait(false);
            return key;
        }
    }

    private string ReadMaskedFromConsole(CancellationToken ct)
    {
        var buf = new List<char>(128);
        try
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var ki = Console.ReadKey(intercept: true);
                if (ki.Key == ConsoleKey.Enter)
                {
                    if (buf.Count == 0)
                    {
                        // Let caller loop re-prompt by signalling empty.
                        Console.WriteLine();
                        return string.Empty;
                    }
                    Console.WriteLine();
                    return new string(buf.ToArray());
                }
                if (ki.Key == ConsoleKey.Backspace)
                {
                    if (buf.Count > 0)
                    {
                        buf.RemoveAt(buf.Count - 1);
                        Console.Write("\b \b");
                    }
                    continue;
                }
                if ((ki.Modifiers & ConsoleModifiers.Control) != 0 && ki.Key == ConsoleKey.C)
                {
                    throw new OperationCanceledException(ct);
                }
                if (!char.IsControl(ki.KeyChar))
                {
                    buf.Add(ki.KeyChar);
                    Console.Write("•");
                }
            }
        }
        finally
        {
            // Best-effort scrub. Strings created from this buffer will still live in the heap,
            // but we don't leave the cleartext chars in the list.
            for (int i = 0; i < buf.Count; i++) buf[i] = '\0';
            buf.Clear();
        }
    }

    // ─── Stage 3: models ──────────────────────────────────────────────

    private async Task<List<string>?> PromptModelsAsync(CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            await _output.WriteLineAsync("Model deployment name (comma-separated for multiple)").ConfigureAwait(false);
            await _output.WriteLineAsync("  e.g. gpt-4o,gpt-4o-mini").ConfigureAwait(false);
            await _output.WriteAsync("> ").ConfigureAwait(false);
            await _output.FlushAsync(ct).ConfigureAwait(false);

            string? line = await ReadLineAsync(ct).ConfigureAwait(false);
            if (line == null) return null;

            var parts = line.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (parts.Count == 0)
            {
                await _errorOutput.WriteLineAsync("At least one model deployment name is required.").ConfigureAwait(false);
                continue;
            }

            await _output.WriteLineAsync().ConfigureAwait(false);
            return parts;
        }
    }

    // ─── Stage 4: validation ping ─────────────────────────────────────

    private enum ValidationFailure { None, Auth, ModelNotFound, Timeout, Other }

    private readonly record struct ValidationResult(bool Success, ValidationFailure Category, string? Message);

    private async Task<ValidationResult> ValidateAsync(string endpoint, string apiKey, string model, CancellationToken ct)
    {
        await _output.WriteAsync("Testing connection... ").ConfigureAwait(false);
        await _output.FlushAsync(ct).ConfigureAwait(false);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(PingTimeoutSeconds));
        var sw = Stopwatch.StartNew();

        try
        {
            var client = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
            var chat = client.GetChatClient(model);
            var options = new ChatCompletionOptions { MaxOutputTokenCount = 5 };
            var messages = new List<ChatMessage> { new UserChatMessage("ping") };
            _ = await chat.CompleteChatAsync(messages, options, timeoutCts.Token).ConfigureAwait(false);

            sw.Stop();
            await _output.WriteLineAsync($"✓ authenticated ({model} responded in {sw.ElapsedMilliseconds}ms)").ConfigureAwait(false);
            return new ValidationResult(true, ValidationFailure.None, null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            await _output.WriteLineAsync($"✗ connection timed out. Endpoint may be wrong or unreachable.").ConfigureAwait(false);
            return new ValidationResult(false, ValidationFailure.Timeout, "timeout");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (RequestFailedException ex)
        {
            if (ex.Status == 401)
            {
                await _output.WriteLineAsync("✗ authentication failed — the API key was rejected.").ConfigureAwait(false);
                return new ValidationResult(false, ValidationFailure.Auth, ex.Message);
            }
            if (ex.Status == 404)
            {
                await _output.WriteLineAsync($"✗ model '{model}' not found on this endpoint. Check the deployment name.").ConfigureAwait(false);
                return new ValidationResult(false, ValidationFailure.ModelNotFound, ex.Message);
            }
            await _output.WriteLineAsync($"✗ {ex.GetType().Name}: HTTP {ex.Status} {ex.Message}").ConfigureAwait(false);
            return new ValidationResult(false, ValidationFailure.Other, ex.Message);
        }
        catch (Exception ex)
        {
            await _output.WriteLineAsync($"✗ {ex.GetType().Name}: {ex.Message}").ConfigureAwait(false);
            return new ValidationResult(false, ValidationFailure.Other, ex.Message);
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────

    private async Task<string?> ReadLineAsync(CancellationToken ct)
    {
        // TextReader.ReadLineAsync(CancellationToken) exists on .NET 7+; respects cancellation.
        var line = await _input.ReadLineAsync(ct).ConfigureAwait(false);
        return line;
    }

    private async Task<bool> ConfirmYesAsync(string prompt, CancellationToken ct)
    {
        await _output.WriteAsync(prompt + " ").ConfigureAwait(false);
        await _output.FlushAsync(ct).ConfigureAwait(false);
        string? resp = await ReadLineAsync(ct).ConfigureAwait(false);
        if (resp == null) return false;
        resp = resp.Trim();
        return resp.Equals("y", StringComparison.OrdinalIgnoreCase)
            || resp.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> CancelAsync(CancellationToken _)
    {
        await _errorOutput.WriteLineAsync().ConfigureAwait(false);
        await _errorOutput.WriteLineAsync().ConfigureAwait(false);
        await _errorOutput.WriteLineAsync("Setup cancelled — no changes saved.").ConfigureAwait(false);
        return false;
    }
}
