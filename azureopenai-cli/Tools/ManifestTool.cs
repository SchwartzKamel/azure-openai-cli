using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace AzureOpenAI_CLI.Tools;

/// <summary>
/// FR-012: JSON manifest schema for external CLI tool plugins discovered from
/// <c>~/.config/azureopenai-cli/tools/*.json</c>, <c>--plugin-dir</c>, or
/// opt-in <c>./.azureopenai-cli/tools/*.json</c>.
/// Serialized via <see cref="AppJsonContext"/> (100% Native AOT compatible).
/// </summary>
internal sealed record ToolManifest(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("command")] List<string> Command,
    [property: JsonPropertyName("parameters")] JsonElement? Parameters = null,
    [property: JsonPropertyName("cwd")] string? Cwd = null,
    [property: JsonPropertyName("timeout_seconds")] int TimeoutSeconds = 10,
    [property: JsonPropertyName("stdout_limit_bytes")] int StdoutLimitBytes = 65_536,
    [property: JsonPropertyName("env_passthrough")] List<string>? EnvPassthrough = null,
    [property: JsonPropertyName("dangerous")] bool Dangerous = false
);

/// <summary>
/// Metadata entry emitted by <c>--list-tools</c> and <c>--list-tools --json</c>.
/// </summary>
internal sealed record ToolDescriptor(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("alias")] string? Alias,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("dangerous")] bool Dangerous,
    [property: JsonPropertyName("parameters")] JsonElement? Parameters = null
);

/// <summary>
/// Options controlling manifest tool discovery (FR-012).
/// </summary>
internal sealed record PluginLoadOptions(
    bool NoPlugins = false,
    bool AllowLocalPlugins = false,
    string? CustomPluginDir = null
)
{
    public static PluginLoadOptions Default { get; } = new();
}

/// <summary>
/// Discovers and validates <see cref="ToolManifest"/> JSON files from disk.
/// </summary>
internal static class ManifestLoader
{
    private static readonly Regex ValidToolNameRegex =
        new(@"^[a-zA-Z][a-zA-Z0-9_-]{0,63}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Load all valid <see cref="ToolManifest"/> definitions according to
    /// <paramref name="options"/>, paired with their source tag
    /// (<c>user-plugin</c>, <c>local-plugin</c>, or <c>custom-plugin</c>).
    /// Built-in tool names and aliases are skipped so plugins can never shadow
    /// a built-in tool.
    /// </summary>
    public static IReadOnlyList<(ToolManifest Manifest, string Source)> LoadManifests(
        PluginLoadOptions? options = null)
    {
        var opts = options ?? PluginLoadOptions.Default;
        if (opts.NoPlugins)
        {
            return Array.Empty<(ToolManifest, string)>();
        }

        var byName = new Dictionary<string, (ToolManifest Manifest, string Source)>(StringComparer.OrdinalIgnoreCase);

        // 1. User-scoped plugin directory: $XDG_CONFIG_HOME/azureopenai-cli/tools or ~/.config/azureopenai-cli/tools
        foreach (var dir in GetUserPluginDirectories())
        {
            LoadFromDirectory(dir, "user-plugin", byName);
        }

        // 2. Repo-local plugin directory: ./.azureopenai-cli/tools (ONLY when explicitly opted in)
        bool allowLocal = opts.AllowLocalPlugins
            || Environment.GetEnvironmentVariable("AZ_ALLOW_LOCAL_PLUGINS") == "1";
        if (allowLocal)
        {
            var localDir = Path.Combine(Directory.GetCurrentDirectory(), ".azureopenai-cli", "tools");
            LoadFromDirectory(localDir, "local-plugin", byName);
        }

        // 3. Explicit --plugin-dir override (highest plugin precedence)
        if (!string.IsNullOrWhiteSpace(opts.CustomPluginDir))
        {
            LoadFromDirectory(opts.CustomPluginDir, "custom-plugin", byName);
        }

        return byName.Values
            .OrderBy(x => x.Manifest.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static IEnumerable<string> GetUserPluginDirectories()
    {
        var dirs = new List<string>();
        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrWhiteSpace(xdg))
        {
            dirs.Add(Path.Combine(xdg, "azureopenai-cli", "tools"));
        }

        var home = Environment.GetEnvironmentVariable("HOME");
        if (string.IsNullOrWhiteSpace(home))
        {
            home = Environment.GetEnvironmentVariable("USERPROFILE");
        }
        if (string.IsNullOrWhiteSpace(home))
        {
            home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        if (!string.IsNullOrWhiteSpace(home))
        {
            var dotConfig = Path.Combine(home, ".config", "azureopenai-cli", "tools");
            if (!dirs.Contains(dotConfig, StringComparer.Ordinal))
            {
                dirs.Add(dotConfig);
            }
        }
        return dirs;
    }

    private static void LoadFromDirectory(
        string directoryPath,
        string sourceTag,
        Dictionary<string, (ToolManifest Manifest, string Source)> target)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return;
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(directoryPath, "*.json");
            Array.Sort(files, StringComparer.Ordinal);
        }
        catch
        {
            return;
        }

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var manifest = JsonSerializer.Deserialize(json, AppJsonContext.Default.ToolManifest);
                if (!IsValidManifest(manifest))
                {
                    continue;
                }

                // Built-in tools and short aliases ALWAYS win on collision (FR-012 §Security).
                if (ToolRegistry.IsBuiltInNameOrAlias(manifest!.Name))
                {
                    continue;
                }

                target[manifest.Name] = (manifest, sourceTag);
            }
            catch
            {
                // Ignore malformed manifest files so a bad plugin never crashes startup.
            }
        }
    }

    internal static bool IsValidManifest(ToolManifest? manifest)
    {
        if (manifest is null) return false;
        if (string.IsNullOrWhiteSpace(manifest.Name) || !ValidToolNameRegex.IsMatch(manifest.Name))
            return false;
        if (string.IsNullOrWhiteSpace(manifest.Description))
            return false;
        if (manifest.Command is null || manifest.Command.Count == 0 || string.IsNullOrWhiteSpace(manifest.Command[0]))
            return false;
        return true;
    }
}

/// <summary>
/// An <see cref="AIFunction"/> backed by a <see cref="ToolManifest"/> subprocess.
/// Executes commands strictly via <see cref="ProcessStartInfo.ArgumentList"/>
/// (never <c>/bin/sh -c</c>) to prevent shell command injection, scrubs parent
/// credentials from the child environment unless allowlisted in <c>env_passthrough</c>,
/// and enforces <c>timeout_seconds</c> and <c>stdout_limit_bytes</c>.
/// </summary>
internal sealed class ManifestAIFunction : AIFunction
{
    private static readonly JsonElement DefaultParametersSchema = ParseDefaultSchema();

    private static readonly string[] ScrubbedSecretEnvVars = new[]
    {
        "AZURE_OPENAI_API_KEY",
        "AZUREOPENAIAPI",
        "AZUREOPENAIENDPOINT",
        "AZUREOPENAIMODEL",
        "GITHUB_TOKEN",
        "GH_TOKEN",
        "OPENAI_API_KEY",
        "ANTHROPIC_API_KEY",
    };

    private static readonly Regex PlaceholderRegex =
        new(@"\{([a-zA-Z0-9_-]+)\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ToolManifest _manifest;
    private readonly JsonElement _schema;

    public ManifestAIFunction(ToolManifest manifest)
    {
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        _schema = manifest.Parameters is { ValueKind: JsonValueKind.Object } p
            ? p.Clone()
            : DefaultParametersSchema;
    }

    public ToolManifest Manifest => _manifest;

    public override string Name => _manifest.Name;

    public override string Description => _manifest.Description;

    public override JsonElement JsonSchema => _schema;

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        return await ExecuteManifestAsync(arguments, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<string> ExecuteManifestAsync(
        IDictionary<string, object?>? arguments,
        CancellationToken ct = default)
    {
        var argv = ExpandCommandArgv(_manifest.Command, arguments);
        if (argv.Count == 0 || string.IsNullOrWhiteSpace(argv[0]))
        {
            return "Error: manifest command is empty.";
        }

        int timeoutSec = _manifest.TimeoutSeconds > 0 && _manifest.TimeoutSeconds <= 600
            ? _manifest.TimeoutSeconds
            : 10;
        int maxBytes = _manifest.StdoutLimitBytes > 0 && _manifest.StdoutLimitBytes <= 1_048_576
            ? _manifest.StdoutLimitBytes
            : 65_536;

        var psi = new ProcessStartInfo
        {
            FileName = argv[0],
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        for (int i = 1; i < argv.Count; i++)
        {
            psi.ArgumentList.Add(argv[i]);
        }

        if (!string.IsNullOrWhiteSpace(_manifest.Cwd))
        {
            var expandedCwd = ExpandTemplate(_manifest.Cwd, arguments);
            if (!string.IsNullOrWhiteSpace(expandedCwd) && Directory.Exists(expandedCwd))
            {
                psi.WorkingDirectory = expandedCwd;
            }
        }

        // Scrub sensitive parent credentials unless explicitly listed in env_passthrough
        var passthrough = _manifest.EnvPassthrough is { Count: > 0 }
            ? new HashSet<string>(_manifest.EnvPassthrough, StringComparer.OrdinalIgnoreCase)
            : null;

        foreach (var secretVar in ScrubbedSecretEnvVars)
        {
            if (passthrough is null || !passthrough.Contains(secretVar))
            {
                psi.Environment.Remove(secretVar);
            }
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSec));

        try
        {
            using var process = Process.Start(psi);
            if (process is null)
            {
                return $"Error: failed to start plugin command '{argv[0]}'.";
            }

            process.StandardInput.Close();

            var stdoutTask = ReadCappedAsync(process.StandardOutput, maxBytes, cts.Token);
            var stderrTask = ReadCappedAsync(process.StandardError, Math.Max(4096, maxBytes / 4), cts.Token);

            try
            {
                await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch { }
                return $"Error: plugin '{_manifest.Name}' timed out after {timeoutSec}s.";
            }

            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            var result = stdout;
            if (process.ExitCode != 0 && !string.IsNullOrEmpty(stderr))
            {
                result += $"\n[exit code {process.ExitCode}]\n[stderr] {stderr}";
            }

            return string.IsNullOrEmpty(result) ? "(no output)" : result;
        }
        catch (Exception ex)
        {
            return $"Error executing plugin '{_manifest.Name}': {ex.Message}";
        }
    }

    /// <summary>
    /// Expands <c>{placeholder}</c> tokens across argv entries without invoking a shell.
    /// If an argv template is a pure placeholder (<c>"{path}"</c>) and the argument is
    /// omitted or empty, that argv entry is omitted cleanly.
    /// </summary>
    internal static List<string> ExpandCommandArgv(
        IReadOnlyList<string> commandTemplate,
        IDictionary<string, object?>? arguments)
    {
        var result = new List<string>(commandTemplate.Count);
        for (int i = 0; i < commandTemplate.Count; i++)
        {
            var token = commandTemplate[i];
            if (i > 0 && IsSinglePlaceholder(token, out var placeholderKey))
            {
                if (!TryGetArgumentString(arguments, placeholderKey, out var val) || string.IsNullOrEmpty(val))
                {
                    continue;
                }
                result.Add(val);
            }
            else
            {
                result.Add(ExpandTemplate(token, arguments));
            }
        }
        return result;
    }

    private static bool IsSinglePlaceholder(string token, out string key)
    {
        key = string.Empty;
        if (token.Length > 2 && token[0] == '{' && token[^1] == '}')
        {
            var inner = token[1..^1];
            if (!inner.Contains('{') && !inner.Contains('}'))
            {
                key = inner;
                return true;
            }
        }
        return false;
    }

    private static string ExpandTemplate(string template, IDictionary<string, object?>? arguments)
    {
        return PlaceholderRegex.Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            return TryGetArgumentString(arguments, key, out var val) ? val : string.Empty;
        });
    }

    private static bool TryGetArgumentString(
        IDictionary<string, object?>? arguments,
        string key,
        out string value)
    {
        value = string.Empty;
        if (arguments is null)
            return false;

        foreach (var kv in arguments)
        {
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase) && kv.Value is not null)
            {
                if (kv.Value is JsonElement je)
                {
                    value = je.ValueKind switch
                    {
                        JsonValueKind.String => je.GetString() ?? string.Empty,
                        JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
                        _ => je.GetRawText(),
                    };
                }
                else
                {
                    value = Convert.ToString(kv.Value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
                }
                return true;
            }
        }
        return false;
    }

    private static async Task<string> ReadCappedAsync(StreamReader reader, int maxBytes, CancellationToken ct)
    {
        var buffer = new char[4096];
        var sb = new StringBuilder();
        int totalRead = 0;

        while (totalRead < maxBytes)
        {
            int toRead = Math.Min(buffer.Length, maxBytes - totalRead);
            int read = await reader.ReadAsync(buffer.AsMemory(0, toRead), ct).ConfigureAwait(false);
            if (read == 0) break;
            sb.Append(buffer, 0, read);
            totalRead += read;
        }

        if (totalRead >= maxBytes)
        {
            sb.Append("\n... [output truncated at byte limit]");
        }

        return sb.ToString().TrimEnd();
    }

    private static JsonElement ParseDefaultSchema()
    {
        using var doc = JsonDocument.Parse("""{"type":"object","properties":{}}""");
        return doc.RootElement.Clone();
    }
}
