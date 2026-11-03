using System.ClientModel;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace AzureOpenAI_CLI.Resilience;

/// <summary>
/// Lets a model that refuses a <c>temperature</c> setting still answer.
///
/// <para>
/// Reasoning and "chat-latest" deployments accept only the default temperature, and reject any other value with
/// HTTP 400 <c>unsupported_value</c>. Hopper always sends one (its default is 0.55), so two of the three
/// deployments tried in S04SP11 (<c>gpt-5-nano</c>, <c>gpt-5.2-chat</c>) failed on every call until the user knew
/// to pass <c>--temperature 1</c>. Which models do this changes with each release and the registry cannot know, so
/// the rule is reactive: on that exact error, retry once without the setting.
/// </para>
///
/// <para>
/// Deliberately narrow. It retries only a <see cref="ClientResultException"/> whose message names
/// <c>temperature</c> and says it is unsupported, only when there was a temperature to drop, and only once. Any other
/// 400 (a bad schema, a bad parameter) still surfaces as an error. A model that has rejected a temperature is
/// remembered for the rest of the process, so an agent loop does not pay a failed round trip every round.
/// </para>
///
/// <para>
/// S04E24: a one-shot CLI call is a new process every time, so the CLI also passes a <c>memoryPath</c> and the
/// lesson is kept on disk (<c>temperature-rejecting-models</c> in the prompt-cache directory). GitHub Copilot's
/// GPT-5.x/6.x reasoning models otherwise cost a failed round trip (~1 s) on every invocation. A stale entry only
/// means the model gets its default temperature, which is what such a model requires anyway; delete the file to
/// relearn. Best effort: an unreadable or unwritable file never fails a call.
/// </para>
/// </summary>
internal sealed class TemperatureTolerantChatClient : DelegatingChatClient
{
    // Models that have already refused a temperature in this process. Keyed by model name: the same name on the
    // same host behaves the same way for the lifetime of a run.
    private static readonly ConcurrentDictionary<string, byte> RejectingModels = new(StringComparer.OrdinalIgnoreCase);

    // Memory files already read into RejectingModels in this process.
    private static readonly ConcurrentDictionary<string, byte> LoadedMemoryPaths = new(StringComparer.Ordinal);

    // Bounds on the on-disk memory: entries read, and file size beyond which nothing more is appended.
    internal const int MaxRememberedModels = 256;
    private const long MaxMemoryBytes = 32 * 1024;

    private readonly string _model;
    private readonly Action<string> _note;
    private readonly string? _memoryPath;

    public TemperatureTolerantChatClient(IChatClient inner, string model, Action<string>? note = null, string? memoryPath = null)
        : base(inner)
    {
        _model = model;
        _note = note ?? (line => Console.Error.WriteLine(line));
        _memoryPath = string.IsNullOrWhiteSpace(memoryPath) ? null : memoryPath;
        if (_memoryPath is not null && LoadedMemoryPaths.TryAdd(_memoryPath, 0))
        {
            LoadMemory(_memoryPath);
        }
    }

    /// <summary>Where the CLI keeps the cross-process memory: beside the prompt cache.</summary>
    internal static string DefaultMemoryPath() =>
        Path.Combine(AzureOpenAI_CLI.Cache.PromptCache.ResolveCacheDir(), "temperature-rejecting-models");

    /// <summary>Forgets every remembered model. For tests; nothing in the product calls it.</summary>
    internal static void ForgetAll()
    {
        RejectingModels.Clear();
        LoadedMemoryPaths.Clear();
    }

    /// <summary>
    /// Model names safe to write to and read from the memory file: one short line of plain identifier characters.
    /// </summary>
    internal static bool IsPersistableModel(string model) =>
        model.Length is > 0 and <= 128
        && model.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' or ':' or '/' or '@' or '+');

    private static void LoadMemory(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            foreach (var line in File.ReadLines(path).Take(MaxRememberedModels))
            {
                var name = line.Trim();
                if (IsPersistableModel(name)) RejectingModels.TryAdd(name, 0);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: without the memory the reactive retry still works.
        }
    }

    private void Remember()
    {
        if (_memoryPath is null || !IsPersistableModel(_model)) return;
        try
        {
            var dir = Path.GetDirectoryName(_memoryPath);
            if (!string.IsNullOrEmpty(dir))
            {
                if (OperatingSystem.IsWindows()) Directory.CreateDirectory(dir);
                else Directory.CreateDirectory(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            var info = new FileInfo(_memoryPath);
            if (info.Exists && info.Length >= MaxMemoryBytes) return;
            File.AppendAllText(_memoryPath, _model + "\n");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: the in-process memory still holds for this run.
        }
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var effective = Prepare(options);
        try
        {
            return await base.GetResponseAsync(messages, effective, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (effective?.Temperature is not null && IsTemperatureRejection(ex))
        {
            NoteRejection();
            return await base.GetResponseAsync(messages, WithoutTemperature(options), cancellationToken).ConfigureAwait(false);
        }
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var effective = Prepare(options);
        var enumerator = base.GetStreamingResponseAsync(messages, effective, cancellationToken).GetAsyncEnumerator(cancellationToken);
        bool hasItem;
        try
        {
            hasItem = await enumerator.MoveNextAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (effective?.Temperature is not null && IsTemperatureRejection(ex))
        {
            // The rejection arrives with the first read, before anything has been yielded, so retrying cannot repeat output.
            await enumerator.DisposeAsync().ConfigureAwait(false);
            NoteRejection();
            enumerator = base.GetStreamingResponseAsync(messages, WithoutTemperature(options), cancellationToken).GetAsyncEnumerator(cancellationToken);
            hasItem = await enumerator.MoveNextAsync().ConfigureAwait(false);
        }

        try
        {
            while (hasItem)
            {
                yield return enumerator.Current;
                hasItem = await enumerator.MoveNextAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }
    }

    // A model already known to refuse a temperature gets none, with no failed round trip first.
    private ChatOptions? Prepare(ChatOptions? options) =>
        options?.Temperature is not null && RejectingModels.ContainsKey(_model) ? WithoutTemperature(options) : options;

    private static ChatOptions? WithoutTemperature(ChatOptions? options)
    {
        if (options is null) return null;
        var copy = options.Clone(); // never mutate the caller's options: they may be reused for the next round
        copy.Temperature = null;
        return copy;
    }

    private void NoteRejection()
    {
        if (RejectingModels.TryAdd(_model, 0))
        {
            _note($"[note] model '{_model}' does not accept a temperature setting (reasoning models take only the default); retrying without it.");
            Remember();
        }
    }

    /// <summary>
    /// True when this is a client-side 400 whose message says the <c>temperature</c> parameter is not supported.
    /// The SDK message reads: "HTTP 400 (invalid_request_error: unsupported_value) Parameter: temperature ...
    /// Unsupported value: 'temperature' does not support 0.55 with this model. Only the default (1) value is supported."
    /// </summary>
    internal static bool IsTemperatureRejection(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (e is not ClientResultException cre) continue;
            if (cre.Status is not (0 or 400)) continue; // 0: the SDK had no response to read a status from
            var text = cre.Message;
            if (text.Contains("temperature", StringComparison.OrdinalIgnoreCase)
                && (text.Contains("unsupported", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("does not support", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }
        return false;
    }
}
