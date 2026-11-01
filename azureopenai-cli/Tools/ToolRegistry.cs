using Microsoft.Extensions.AI;

namespace AzureOpenAI_CLI.Tools;

/// <summary>
/// Registry of built-in tools available for agent mode in v2.
/// Uses Microsoft.Extensions.AI's AIFunctionFactory to generate AITool instances.
/// </summary>
internal static class ToolRegistry
{
    /// <summary>
    /// Short alias → canonical full tool name. Kramer audit M4: resolved once at
    /// the <see cref="CreateMafTools"/> entry so downstream code works on
    /// canonical names only.
    /// </summary>
    private static readonly Dictionary<string, string> ShortAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["shell"] = "shell_exec",
        ["file"] = "read_file",
        ["web"] = "web_fetch",
        ["clipboard"] = "get_clipboard",
        ["datetime"] = "get_datetime",
        ["delegate"] = "delegate_task",
        ["queue"] = "queue_add",
        ["queue-status"] = "queue_status",
    };

    /// <summary>
    /// Kramer audit M5: canonical default tool set used by agent mode when the
    /// user does not pass <c>--tools</c>. Referenced by <c>Program.RunAsync</c>
    /// (Ralph mode default) and <see cref="DelegateTaskTool"/> (child-agent default)
    /// so the list has exactly one source of truth. Order doesn't matter for
    /// behavior; sorted for readability.
    /// </summary>
    internal static readonly IReadOnlyList<string> DefaultAgentTools = new[]
    {
        "shell_exec",
        "read_file",
        "web_fetch",
        "get_datetime",
        "delegate_task",
    };

    /// <summary>
    /// Default tool set for a <em>child</em> agent spawned via <c>delegate_task</c>.
    /// Intentionally omits <c>delegate_task</c> to discourage runaway recursion
    /// even though <see cref="DelegateTaskTool.MaxDepth"/> already caps it.
    /// Clipboard is also excluded — child agents don't get to touch the GUI surface.
    /// </summary>
    internal static readonly IReadOnlyList<string> DefaultChildAgentTools = new[]
    {
        "shell_exec",
        "read_file",
        "web_fetch",
        "get_datetime",
    };

    /// <summary>
    /// Canonicalize a user-supplied tool name (short alias or full name) to its
    /// canonical full name. Returns the input unchanged when it's already a full
    /// name or an unknown token (unknown tokens are silently ignored downstream).
    /// </summary>
    internal static string Canonicalize(string name)
        => ShortAliases.TryGetValue(name, out var full) ? full : name;

    /// <summary>
    /// Returns true if <paramref name="name"/> matches a built-in tool name or
    /// short alias (used by <see cref="ManifestLoader"/> so plugins can never
    /// shadow built-in tools).
    /// </summary>
    internal static bool IsBuiltInNameOrAlias(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        if (ShortAliases.ContainsKey(name)) return true;
        foreach (var kv in ShortAliases)
        {
            if (string.Equals(kv.Value, name, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Create a list of AITool instances for MAF consumption.
    /// Pass null to enable all tools (built-ins plus discovered manifest plugins).
    /// Accepts full tool names or short aliases — both are canonicalized once at
    /// entry, then matched by full name only.
    /// </summary>
    public static IList<AITool> CreateMafTools(
        IEnumerable<string>? enabledTools = null,
        int? maxRounds = null,
        PluginLoadOptions? pluginOptions = null)
    {
        var allToolFactories = GetBuiltInToolFactories();

        // M4: canonicalize once. Downstream match is full-name only.
        HashSet<string>? enabled = null;
        if (enabledTools is not null)
        {
            enabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in enabledTools)
            {
                if (!string.IsNullOrWhiteSpace(t))
                    enabled.Add(Canonicalize(t.Trim()));
            }
        }

        var counter = maxRounds.HasValue ? new ToolRoundCounter(maxRounds.Value) : null;
        var tools = new List<AITool>();
        foreach (var (name, factory) in allToolFactories)
        {
            // queue_add and queue_status are opt-in built-in tools (requested via
            // --tools queue,queue-status or exposed via MCP server) so the core 6-tool
            // default contract (V2CleanupWaveTests) remains unchanged when enabledTools is null.
            bool isOptInQueueTool = string.Equals(name, "queue_add", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "queue_status", StringComparison.OrdinalIgnoreCase);

            if ((enabled is null && !isOptInQueueTool) || (enabled is not null && enabled.Contains(name)))
            {
                var tool = factory();
                if (counter is not null && tool is AIFunction fn)
                {
                    tool = new MaxRoundsAIFunction(fn, counter);
                }
                tools.Add(tool);
            }
        }

        // Append custom manifest tools (built-ins already won on collision inside ManifestLoader)
        var manifests = ManifestLoader.LoadManifests(pluginOptions);
        foreach (var (manifest, _) in manifests)
        {
            if (enabled is null || enabled.Contains(manifest.Name))
            {
                AITool pluginTool = new ManifestAIFunction(manifest);
                if (counter is not null && pluginTool is AIFunction pfn)
                {
                    pluginTool = new MaxRoundsAIFunction(pfn, counter);
                }
                tools.Add(pluginTool);
            }
        }

        return tools;
    }

    /// <summary>
    /// Enumerates all built-in and active manifest tools for <c>--list-tools</c>
    /// and MCP server introspection.
    /// </summary>
    internal static IReadOnlyList<ToolDescriptor> DescribeAllTools(PluginLoadOptions? pluginOptions = null)
    {
        var descriptors = new List<ToolDescriptor>();
        var reverseAliases = ShortAliases.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var (name, factory) in GetBuiltInToolFactories())
        {
            var tool = factory();
            var fn = tool as AIFunction;
            reverseAliases.TryGetValue(name, out var alias);
            bool dangerous = string.Equals(name, "shell_exec", StringComparison.OrdinalIgnoreCase);
            descriptors.Add(new ToolDescriptor(
                Name: name,
                Alias: alias,
                Source: "builtin",
                Description: fn?.Description ?? tool.Description ?? string.Empty,
                Dangerous: dangerous,
                Parameters: fn?.JsonSchema));
        }

        foreach (var (manifest, source) in ManifestLoader.LoadManifests(pluginOptions))
        {
            var fn = new ManifestAIFunction(manifest);
            descriptors.Add(new ToolDescriptor(
                Name: manifest.Name,
                Alias: null,
                Source: source,
                Description: manifest.Description,
                Dangerous: manifest.Dangerous,
                Parameters: fn.JsonSchema));
        }

        return descriptors;
    }

    private static Dictionary<string, Func<AITool>> GetBuiltInToolFactories() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["shell_exec"] = () => AIFunctionFactory.Create(ShellExecTool.ExecuteAsync, name: "shell_exec"),
        ["read_file"] = () => AIFunctionFactory.Create(ReadFileTool.ReadAsync, name: "read_file"),
        ["web_fetch"] = () => AIFunctionFactory.Create(WebFetchTool.FetchAsync, name: "web_fetch"),
        ["get_clipboard"] = () => AIFunctionFactory.Create(GetClipboardTool.GetAsync, name: "get_clipboard"),
        ["get_datetime"] = () => AIFunctionFactory.Create(GetDateTimeTool.GetAsync, name: "get_datetime"),
        ["delegate_task"] = () => AIFunctionFactory.Create(DelegateTaskTool.DelegateAsync, name: "delegate_task"),
        ["queue_add"] = () => AIFunctionFactory.Create(QueueTools.AddAsync, name: "queue_add"),
        ["queue_status"] = () => AIFunctionFactory.Create(QueueTools.StatusAsync, name: "queue_status"),
    };
}

/// <summary>
/// Thread-safe per-agent invocation counter used by <see cref="MaxRoundsAIFunction"/>
/// to enforce <c>--max-rounds</c> in agent mode.
/// </summary>
internal sealed class ToolRoundCounter
{
    private int _used;
    public int MaxRounds { get; }
    public int UsedRounds => Volatile.Read(ref _used);

    public ToolRoundCounter(int maxRounds)
    {
        MaxRounds = Math.Max(1, maxRounds);
    }

    public bool TryIncrement(out int current)
    {
        current = Interlocked.Increment(ref _used);
        return current <= MaxRounds;
    }
}

/// <summary>
/// Decorator around <see cref="DelegatingAIFunction"/> that enforces <c>--max-rounds</c>
/// by refusing additional tool calls once the configured cap is exceeded.
/// </summary>
internal sealed class MaxRoundsAIFunction : DelegatingAIFunction
{
    private readonly ToolRoundCounter _counter;

    public MaxRoundsAIFunction(AIFunction innerFunction, ToolRoundCounter counter)
        : base(innerFunction)
    {
        _counter = counter;
    }

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        if (!_counter.TryIncrement(out _))
        {
            return $"Error: maximum tool-call rounds ({_counter.MaxRounds}) reached (--max-rounds). Provide your final answer using the information gathered so far.";
        }
        return await base.InvokeCoreAsync(arguments, cancellationToken).ConfigureAwait(false);
    }
}
