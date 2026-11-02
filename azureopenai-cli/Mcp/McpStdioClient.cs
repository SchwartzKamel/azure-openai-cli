using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace AzureOpenAI_CLI.Mcp;

/// <summary>
/// FR-013 Phase 1: Outbound Model Context Protocol (MCP) stdio client.
/// Spawns an external MCP server subprocess (or connects over arbitrary
/// <see cref="TextReader"/>/<see cref="TextWriter"/> streams for in-process testing),
/// runs the <c>initialize</c> + <c>notifications/initialized</c> handshake,
/// discovers remote tools via <c>tools/list</c>, and exposes them as
/// <see cref="AIFunction"/> instances (<see cref="McpClientAIFunction"/>) so
/// <c>hopper --agent</c> and <c>--ralph</c> can call any MCP server in the ecosystem.
/// 100% Native AOT compatible via <see cref="McpJsonContext"/>.
/// </summary>
internal sealed class McpStdioClient : IDisposable
{
    private static readonly string[] ScrubbedSecretEnvVars = new[]
    {
        "AZURE_OPENAI_API_KEY",
        "AZUREOPENAIAPI",
        "AZUREOPENAIENDPOINT",
        "AZUREOPENAIMODEL",
        "OPENAI_API_KEY",
        "ANTHROPIC_API_KEY",
    };

    private readonly Process? _process;
    private readonly TextWriter _writer;
    private readonly TextReader _reader;
    private readonly SemaphoreSlim _ioLock = new(1, 1);
    private int _nextId;
    private bool _disposed;

    public string ServerName { get; }

    internal McpStdioClient(string serverName, TextReader reader, TextWriter writer, Process? process = null)
    {
        ServerName = serverName;
        _reader = reader;
        _writer = writer;
        _process = process;
    }

    /// <summary>
    /// Parses a <c>--mcp-server</c> spec (<c>"name=command args..."</c> or bare <c>"command args..."</c>)
    /// and spawns + initializes the MCP stdio server subprocess.
    /// </summary>
    public static async Task<McpStdioClient> ConnectSubprocessAsync(
        string serverSpec,
        int timeoutSeconds = 15,
        CancellationToken ct = default)
    {
        var (serverName, argv) = ParseServerSpec(serverSpec);
        if (argv.Count == 0 || string.IsNullOrWhiteSpace(argv[0]))
        {
            throw new ArgumentException($"Invalid MCP server specification: '{serverSpec}'");
        }

        var psi = new ProcessStartInfo
        {
            FileName = argv[0],
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        for (int i = 1; i < argv.Count; i++)
        {
            psi.ArgumentList.Add(argv[i]);
        }

        foreach (var secret in ScrubbedSecretEnvVars)
        {
            psi.Environment.Remove(secret);
        }

        var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start MCP server process '{argv[0]}'");

        var client = new McpStdioClient(serverName, proc.StandardOutput, proc.StandardInput, proc);
        try
        {
            using var initCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            initCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            await client.InitializeAsync(initCts.Token).ConfigureAwait(false);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Connects over in-memory or piped streams and runs <see cref="InitializeAsync"/>.
    /// </summary>
    public static async Task<McpStdioClient> ConnectStreamsAsync(
        string serverName,
        TextReader reader,
        TextWriter writer,
        CancellationToken ct = default)
    {
        var client = new McpStdioClient(serverName, reader, writer);
        await client.InitializeAsync(ct).ConfigureAwait(false);
        return client;
    }

    public async Task<McpInitializeResult?> InitializeAsync(CancellationToken ct = default)
    {
        var initParams = new McpInitializeRequestParams(
            ProtocolVersion: McpStdioServer.SupportedProtocolVersion,
            Capabilities: new McpClientCapabilities(),
            ClientInfo: new McpServerInfo(Name: "hopper-client", Version: Program.VersionSemver));
        var paramsElem = JsonSerializer.SerializeToElement(initParams, McpJsonContext.Default.McpInitializeRequestParams);

        var resp = await SendRequestAsync("initialize", paramsElem, ct).ConfigureAwait(false);
        await SendNotificationAsync("notifications/initialized", ct).ConfigureAwait(false);

        if (resp.Result is { ValueKind: JsonValueKind.Object } resElem)
        {
            return JsonSerializer.Deserialize(resElem.GetRawText(), McpJsonContext.Default.McpInitializeResult);
        }
        return null;
    }

    public async Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(CancellationToken ct = default)
    {
        var resp = await SendRequestAsync("tools/list", null, ct).ConfigureAwait(false);
        if (resp.Error is not null)
        {
            throw new InvalidOperationException($"MCP tools/list error ({resp.Error.Code}): {resp.Error.Message}");
        }

        if (resp.Result is { ValueKind: JsonValueKind.Object } resElem)
        {
            var parsed = JsonSerializer.Deserialize(resElem.GetRawText(), McpJsonContext.Default.McpToolsListResult);
            return parsed?.Tools ?? new List<McpToolInfo>();
        }

        return Array.Empty<McpToolInfo>();
    }

    public async Task<McpCallToolResult> CallToolAsync(
        string toolName,
        IDictionary<string, object?>? arguments = null,
        CancellationToken ct = default)
    {
        var argMap = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (arguments is not null)
        {
            foreach (var kv in arguments)
            {
                if (kv.Value is null) continue;
                argMap[kv.Key] = ConvertObjectToJsonElement(kv.Value);
            }
        }

        var callParams = new McpCallToolRequestParams(
            Name: toolName,
            Arguments: argMap);
        var paramsElem = JsonSerializer.SerializeToElement(callParams, McpJsonContext.Default.McpCallToolRequestParams);

        var resp = await SendRequestAsync("tools/call", paramsElem, ct).ConfigureAwait(false);
        if (resp.Error is not null)
        {
            return new McpCallToolResult(
                Content: new List<McpContentItem> { new("text", $"MCP error ({resp.Error.Code}): {resp.Error.Message}") },
                IsError: true);
        }

        if (resp.Result is { ValueKind: JsonValueKind.Object } resElem)
        {
            var parsed = JsonSerializer.Deserialize(resElem.GetRawText(), McpJsonContext.Default.McpCallToolResult);
            if (parsed is not null)
                return parsed;
        }

        return new McpCallToolResult(
            Content: new List<McpContentItem> { new("text", "(empty MCP tool response)") },
            IsError: false);
    }

    /// <summary>
    /// Discovers all tools exposed by this MCP server and wraps them as
    /// <see cref="AITool"/> instances for MAF agent consumption.
    /// </summary>
    public async Task<IReadOnlyList<AITool>> CreateMafToolsAsync(CancellationToken ct = default)
    {
        var remoteTools = await ListToolsAsync(ct).ConfigureAwait(false);
        var result = new List<AITool>(remoteTools.Count);
        foreach (var info in remoteTools)
        {
            result.Add(new McpClientAIFunction(this, info));
        }
        return result;
    }

    internal async Task<McpJsonRpcResponse> SendRequestAsync(
        string method,
        JsonElement? parameters,
        CancellationToken ct)
    {
        await _ioLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            int id = Interlocked.Increment(ref _nextId);
            var req = new McpJsonRpcRequest(
                JsonRpc: "2.0",
                Id: id,
                Method: method,
                Params: parameters);
            var json = JsonSerializer.Serialize(req, McpJsonContext.Default.McpJsonRpcRequest);
            await _writer.WriteLineAsync(json.AsMemory(), ct).ConfigureAwait(false);
            await _writer.FlushAsync(ct).ConfigureAwait(false);

            while (true)
            {
                var (payload, _) = await McpStdioServer.ReadNextFrameAsync(_reader, ct).ConfigureAwait(false);
                if (payload is null)
                {
                    throw new EndOfStreamException($"MCP server '{ServerName}' closed stdout before responding to '{method}'.");
                }
                if (string.IsNullOrWhiteSpace(payload))
                {
                    continue;
                }

                var resp = JsonSerializer.Deserialize(payload, McpJsonContext.Default.McpJsonRpcResponse);
                if (resp is null)
                {
                    continue;
                }

                // Skip server-initiated notifications (no id) while awaiting our request id
                if (resp.Id is null)
                {
                    continue;
                }

                return resp;
            }
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private async Task SendNotificationAsync(string method, CancellationToken ct)
    {
        await _ioLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var req = new McpJsonRpcRequest(
                JsonRpc: "2.0",
                Id: null,
                Method: method,
                Params: null);
            var json = JsonSerializer.Serialize(req, McpJsonContext.Default.McpJsonRpcRequest);
            await _writer.WriteLineAsync(json.AsMemory(), ct).ConfigureAwait(false);
            await _writer.FlushAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    /// <summary>
    /// Parses <c>"name=cmd arg1 arg2"</c> or <c>"cmd arg1 arg2"</c> into
    /// <c>(ServerName, ArgvList)</c>, honoring single and double quotes.
    /// </summary>
    internal static (string Name, List<string> Argv) ParseServerSpec(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec))
            return ("mcp", new List<string>());

        string name = "mcp";
        string cmdPart = spec.Trim();

        int eqIdx = cmdPart.IndexOf('=');
        int firstSpace = cmdPart.IndexOf(' ');
        if (eqIdx > 0 && (firstSpace < 0 || eqIdx < firstSpace))
        {
            name = cmdPart[..eqIdx].Trim();
            cmdPart = cmdPart[(eqIdx + 1)..].Trim();
        }

        var argv = SplitCommandLine(cmdPart);
        if (name == "mcp" && argv.Count > 0)
        {
            name = Path.GetFileNameWithoutExtension(argv[0]);
            if (string.IsNullOrWhiteSpace(name))
                name = "mcp";
        }

        return (name, argv);
    }

    internal static List<string> SplitCommandLine(string commandLine)
    {
        var args = new List<string>();
        var current = new StringBuilder();
        char quote = '\0';

        for (int i = 0; i < commandLine.Length; i++)
        {
            char c = commandLine[i];
            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (char.IsWhiteSpace(c))
            {
                if (current.Length > 0)
                {
                    args.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            args.Add(current.ToString());
        }

        return args;
    }

    private static JsonElement ConvertObjectToJsonElement(object value)
    {
        if (value is JsonElement je)
            return je.Clone();

        if (value is string s)
            return JsonSerializer.SerializeToElement(s, AppJsonContext.Default.String);

        if (value is bool b)
        {
            using var doc = JsonDocument.Parse(b ? "true" : "false");
            return doc.RootElement.Clone();
        }

        if (value is int or long or float or double or decimal)
        {
            var numStr = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "0";
            using var doc = JsonDocument.Parse(numStr);
            return doc.RootElement.Clone();
        }

        var strFallback = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        return JsonSerializer.SerializeToElement(strFallback, AppJsonContext.Default.String);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (_process is not null && !_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch { }
        try { _process?.Dispose(); } catch { }
        _ioLock.Dispose();
    }
}

/// <summary>
/// Wraps a remote MCP tool discovered by <see cref="McpStdioClient"/> as an
/// <see cref="AIFunction"/> for Microsoft Agent Framework.
/// </summary>
internal sealed class McpClientAIFunction : AIFunction
{
    private readonly McpStdioClient _client;
    private readonly McpToolInfo _toolInfo;

    public McpClientAIFunction(McpStdioClient client, McpToolInfo toolInfo)
    {
        _client = client;
        _toolInfo = toolInfo;
    }

    public override string Name => _toolInfo.Name;

    public override string Description => _toolInfo.Description;

    public override JsonElement JsonSchema => _toolInfo.InputSchema;

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        var res = await _client.CallToolAsync(_toolInfo.Name, arguments, cancellationToken).ConfigureAwait(false);
        var combined = string.Join("\n", res.Content.Select(c => c.Text));
        if (res.IsError && !combined.StartsWith("Error:", StringComparison.OrdinalIgnoreCase))
        {
            return $"Error: {combined}";
        }
        return combined;
    }
}

// ── Outbound MCP client request records (registered in McpJsonContext) ───────

internal sealed record McpJsonRpcRequest(
    [property: JsonPropertyName("jsonrpc")] string JsonRpc,
    [property: JsonPropertyName("id")] int? Id,
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("params")] JsonElement? Params
);

internal sealed record McpInitializeRequestParams(
    [property: JsonPropertyName("protocolVersion")] string ProtocolVersion,
    [property: JsonPropertyName("capabilities")] McpClientCapabilities Capabilities,
    [property: JsonPropertyName("clientInfo")] McpServerInfo ClientInfo
);

internal sealed record McpClientCapabilities();

internal sealed record McpCallToolRequestParams(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("arguments")] Dictionary<string, JsonElement> Arguments
);

/// <summary>
/// Disposes a set of <see cref="McpStdioClient"/> subprocesses when its scope
/// unwinds. Lets <c>Program.RunAsync</c> own client lifetime with a single
/// <c>using var</c> instead of wrapping the whole chat flow in a try/finally.
/// </summary>
internal sealed class McpClientScope : IDisposable
{
    private readonly IReadOnlyList<McpStdioClient> _clients;

    public McpClientScope(IReadOnlyList<McpStdioClient> clients) => _clients = clients;

    public void Dispose()
    {
        foreach (var c in _clients)
        {
            try { c.Dispose(); } catch { /* best-effort: never mask the real exit path */ }
        }
    }
}
