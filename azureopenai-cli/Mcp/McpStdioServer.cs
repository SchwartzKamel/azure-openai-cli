using System.Text.Json;
using System.Text.Json.Serialization;
using AzureOpenAI_CLI.Patterns;
using AzureOpenAI_CLI.Squad;
using AzureOpenAI_CLI.Tools;
using Microsoft.Extensions.AI;

namespace AzureOpenAI_CLI.Mcp;

/// <summary>
/// FR-013 Phase 2: Model Context Protocol (MCP) server over <c>stdio</c>
/// (newline-delimited JSON-RPC 2.0). Allows external AI agents (Claude Code,
/// GitHub Copilot CLI, Cursor, Codex, etc.) to discover and invoke <c>hopper</c>'s
/// built-in tools and custom manifest plugins in-process with zero reflection
/// (100% Native AOT compatible via <see cref="AppJsonContext"/>).
/// </summary>
internal static class McpStdioServer
{
    internal const string SupportedProtocolVersion = "2024-11-05";

    /// <summary>
    /// Runs the MCP stdio server loop until <paramref name="input"/> reaches EOF
    /// or <paramref name="ct"/> is cancelled.
    /// </summary>
    public static async Task<int> RunAsync(
        TextReader input,
        TextWriter output,
        bool allowShell = false,
        bool readOnly = false,
        string? explicitTools = null,
        PluginLoadOptions? pluginOptions = null,
        CancellationToken ct = default)
    {
        var toolMap = BuildExposedToolMap(allowShell, readOnly, explicitTools, pluginOptions);

        while (!ct.IsCancellationRequested)
        {
            string? payload;
            bool useContentLengthFraming = false;
            try
            {
                (payload, useContentLengthFraming) = await ReadNextFrameAsync(input, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (payload is null)
            {
                break; // EOF
            }

            if (string.IsNullOrWhiteSpace(payload))
            {
                continue;
            }

            var responseJson = await ProcessLineAsync(payload, toolMap, allowShell, ct).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(responseJson))
            {
                if (useContentLengthFraming)
                {
                    int byteCount = System.Text.Encoding.UTF8.GetByteCount(responseJson);
                    var header = $"Content-Length: {byteCount}\r\n\r\n";
                    await output.WriteAsync(header.AsMemory(), ct).ConfigureAwait(false);
                    await output.WriteAsync(responseJson.AsMemory(), ct).ConfigureAwait(false);
                }
                else
                {
                    await output.WriteLineAsync(responseJson.AsMemory(), ct).ConfigureAwait(false);
                }
                await output.FlushAsync(ct).ConfigureAwait(false);
            }
        }

        return 0;
    }

    /// <summary>
    /// Reads either a newline-delimited JSON-RPC line OR a <c>Content-Length: N</c>
    /// header-framed JSON-RPC payload from <paramref name="input"/>.
    /// </summary>
    internal static async Task<(string? Payload, bool IsContentLengthFramed)> ReadNextFrameAsync(
        TextReader input,
        CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            var line = await input.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null)
            {
                return (null, false);
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                var lenStr = line["Content-Length:".Length..].Trim();
                if (!int.TryParse(lenStr, out int contentLength) || contentLength < 0 || contentLength > 4 * 1024 * 1024)
                {
                    return (string.Empty, true);
                }

                // Consume remaining headers until blank line
                while (true)
                {
                    var headerLine = await input.ReadLineAsync(ct).ConfigureAwait(false);
                    if (headerLine is null || headerLine.Length == 0)
                        break;
                }

                var buffer = new char[contentLength];
                int readTotal = 0;
                while (readTotal < contentLength)
                {
                    int n = await input.ReadAsync(buffer.AsMemory(readTotal, contentLength - readTotal), ct).ConfigureAwait(false);
                    if (n == 0) break;
                    readTotal += n;
                }

                return (new string(buffer, 0, readTotal), true);
            }

            return (line, false);
        }

        return (null, false);
    }

    internal static Dictionary<string, AIFunction> BuildExposedToolMap(
        bool allowShell,
        bool readOnly,
        string? explicitTools,
        PluginLoadOptions? pluginOptions)
    {
        IEnumerable<string>? filter = null;
        bool explicitShellRequested = false;

        if (!string.IsNullOrWhiteSpace(explicitTools))
        {
            var tokens = explicitTools.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            filter = tokens;
            explicitShellRequested = tokens.Any(t =>
                string.Equals(ToolRegistry.Canonicalize(t), "shell_exec", StringComparison.OrdinalIgnoreCase));
        }
        else if (readOnly)
        {
            filter = new[] { "read_file", "web_fetch", "get_clipboard", "get_datetime", "queue_status" };
        }

        bool shellPermitted = !readOnly && (allowShell || explicitShellRequested);

        var allTools = ToolRegistry.CreateMafTools(
            enabledTools: filter,
            maxRounds: null,
            pluginOptions: pluginOptions);

        // When no explicit filter is set, also expose queue_add and queue_status over MCP
        if (filter is null)
        {
            foreach (var qt in ToolRegistry.CreateMafTools(new[] { "queue_add", "queue_status" }, pluginOptions: new PluginLoadOptions(NoPlugins: true)))
            {
                allTools.Add(qt);
            }
        }

        var map = new Dictionary<string, AIFunction>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in allTools)
        {
            if (t is not AIFunction fn)
                continue;

            // Security gate (Option A / FR-013 §6.2): shell_exec and manifest tools
            // marked dangerous=true require explicit --allow-shell (or --tools shell).
            if (string.Equals(fn.Name, "shell_exec", StringComparison.OrdinalIgnoreCase) && !shellPermitted)
            {
                continue;
            }

            if (fn is ManifestAIFunction mfn && mfn.Manifest.Dangerous && !shellPermitted)
            {
                continue;
            }

            if (readOnly && string.Equals(fn.Name, "delegate_task", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            map[fn.Name] = fn;
        }

        return map;
    }

    internal static async Task<string?> ProcessLineAsync(
        string line,
        Dictionary<string, AIFunction> toolMap,
        bool allowShell,
        CancellationToken ct = default)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            return SerializeError(null, -32700, "Parse error: invalid JSON");
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return SerializeError(null, -32600, "Invalid Request: root must be a JSON object");
            }

            bool hasId = root.TryGetProperty("id", out var idProp);
            JsonElement? idClone = hasId ? idProp.Clone() : null;

            if (!root.TryGetProperty("method", out var methodProp) || methodProp.ValueKind != JsonValueKind.String)
            {
                if (!hasId) return null;
                return SerializeError(idClone, -32600, "Invalid Request: missing 'method'");
            }

            var method = methodProp.GetString() ?? string.Empty;
            root.TryGetProperty("params", out var paramsProp);

            // JSON-RPC notifications (no 'id' property) do not receive a response.
            if (!hasId || method.StartsWith("notifications/", StringComparison.Ordinal))
            {
                return null;
            }

            switch (method)
            {
                case "initialize":
                    {
                        var initResult = new McpInitializeResult(
                            ProtocolVersion: SupportedProtocolVersion,
                            Capabilities: new McpCapabilities(
                                Tools: new McpToolsCapability(ListChanged: false),
                                Prompts: new McpPromptsCapability(ListChanged: false),
                                Resources: new McpResourcesCapability(ListChanged: false)),
                            ServerInfo: new McpServerInfo(Name: "hopper", Version: Program.VersionSemver));
                        var element = JsonSerializer.SerializeToElement(initResult, McpJsonContext.Default.McpInitializeResult);
                        return SerializeResult(idClone, element);
                    }

                case "prompts/list":
                    {
                        var prompts = BuildPromptsList();
                        var listResult = new McpPromptsListResult(prompts);
                        var element = JsonSerializer.SerializeToElement(listResult, McpJsonContext.Default.McpPromptsListResult);
                        return SerializeResult(idClone, element);
                    }

                case "prompts/get":
                    {
                        if (paramsProp.ValueKind != JsonValueKind.Object
                            || !paramsProp.TryGetProperty("name", out var promptNameProp)
                            || promptNameProp.ValueKind != JsonValueKind.String)
                        {
                            return SerializeError(idClone, -32602, "Invalid params: 'name' is required for prompts/get");
                        }

                        var promptName = promptNameProp.GetString() ?? string.Empty;
                        string? inputArg = null;
                        if (paramsProp.TryGetProperty("arguments", out var pArgs)
                            && pArgs.ValueKind == JsonValueKind.Object
                            && pArgs.TryGetProperty("input", out var inVal)
                            && inVal.ValueKind == JsonValueKind.String)
                        {
                            inputArg = inVal.GetString();
                        }

                        var getResult = ResolvePromptGet(promptName, inputArg);
                        if (getResult is null)
                        {
                            return SerializeError(idClone, -32602, $"Unknown prompt '{promptName}'");
                        }

                        var element = JsonSerializer.SerializeToElement(getResult, McpJsonContext.Default.McpGetPromptResult);
                        return SerializeResult(idClone, element);
                    }

                case "resources/list":
                    {
                        var resources = BuildResourcesList();
                        var listResult = new McpResourcesListResult(resources);
                        var element = JsonSerializer.SerializeToElement(listResult, McpJsonContext.Default.McpResourcesListResult);
                        return SerializeResult(idClone, element);
                    }

                case "resources/read":
                    {
                        if (paramsProp.ValueKind != JsonValueKind.Object
                            || !paramsProp.TryGetProperty("uri", out var uriProp)
                            || uriProp.ValueKind != JsonValueKind.String)
                        {
                            return SerializeError(idClone, -32602, "Invalid params: 'uri' is required for resources/read");
                        }

                        var uri = uriProp.GetString() ?? string.Empty;
                        var readResult = ReadMcpResource(uri);
                        if (readResult is null)
                        {
                            return SerializeError(idClone, -32602, $"Resource not found or unauthorized: '{uri}'");
                        }

                        var element = JsonSerializer.SerializeToElement(readResult, McpJsonContext.Default.McpReadResourceResult);
                        return SerializeResult(idClone, element);
                    }

                case "ping":
                    {
                        using var emptyDoc = JsonDocument.Parse("{}");
                        return SerializeResult(idClone, emptyDoc.RootElement.Clone());
                    }

                case "tools/list":
                    {
                        var mcpTools = new List<McpToolInfo>(toolMap.Count);
                        foreach (var fn in toolMap.Values.OrderBy(f => f.Name, StringComparer.Ordinal))
                        {
                            mcpTools.Add(new McpToolInfo(
                                Name: fn.Name,
                                Description: fn.Description ?? string.Empty,
                                InputSchema: fn.JsonSchema));
                        }
                        var listResult = new McpToolsListResult(mcpTools);
                        var element = JsonSerializer.SerializeToElement(listResult, McpJsonContext.Default.McpToolsListResult);
                        return SerializeResult(idClone, element);
                    }

                case "tools/call":
                    {
                        if (paramsProp.ValueKind != JsonValueKind.Object
                            || !paramsProp.TryGetProperty("name", out var nameProp)
                            || nameProp.ValueKind != JsonValueKind.String)
                        {
                            return SerializeError(idClone, -32602, "Invalid params: 'name' is required for tools/call");
                        }

                        var toolName = ToolRegistry.Canonicalize(nameProp.GetString() ?? string.Empty);
                        if (!toolMap.TryGetValue(toolName, out var fn))
                        {
                            string denialMsg = string.Equals(toolName, "shell_exec", StringComparison.OrdinalIgnoreCase) && !allowShell
                                ? "Tool 'shell_exec' is disabled by default in MCP server mode. Pass --allow-shell (or --tools shell) to enable it."
                                : $"Unknown or disabled tool '{toolName}'.";
                            var errCall = new McpCallToolResult(
                                Content: new List<McpContentItem> { new("text", denialMsg) },
                                IsError: true);
                            var errElem = JsonSerializer.SerializeToElement(errCall, McpJsonContext.Default.McpCallToolResult);
                            return SerializeResult(idClone, errElem);
                        }

                        var argsDict = new AIFunctionArguments();
                        if (paramsProp.TryGetProperty("arguments", out var argsProp)
                            && argsProp.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in argsProp.EnumerateObject())
                            {
                                argsDict[prop.Name] = ConvertJsonElementToArg(prop.Value);
                            }
                        }

                        try
                        {
                            var resultObj = await fn.InvokeAsync(argsDict, ct).ConfigureAwait(false);
                            var text = resultObj?.ToString() ?? string.Empty;
                            bool isErr = text.StartsWith("Error:", StringComparison.OrdinalIgnoreCase);
                            var callResult = new McpCallToolResult(
                                Content: new List<McpContentItem> { new("text", text) },
                                IsError: isErr);
                            var elem = JsonSerializer.SerializeToElement(callResult, McpJsonContext.Default.McpCallToolResult);
                            return SerializeResult(idClone, elem);
                        }
                        catch (Exception ex)
                        {
                            var callResult = new McpCallToolResult(
                                Content: new List<McpContentItem> { new("text", $"Error executing tool '{toolName}': {ex.Message}") },
                                IsError: true);
                            var elem = JsonSerializer.SerializeToElement(callResult, McpJsonContext.Default.McpCallToolResult);
                            return SerializeResult(idClone, elem);
                        }
                    }

                default:
                    return SerializeError(idClone, -32601, $"Method not found: '{method}'");
            }
        }
    }

    private static List<McpPromptInfo> BuildPromptsList()
    {
        var list = new List<McpPromptInfo>();
        var inputArgSchema = new List<McpPromptArgument>
        {
            new(Name: "input", Description: "Optional input text or task context to apply the prompt to", Required: false),
        };

        foreach (var pat in PatternRegistry.ListAll())
        {
            list.Add(new McpPromptInfo(
                Name: pat.Name,
                Description: pat.Description,
                Arguments: inputArgSchema));
        }

        try
        {
            var squad = SquadConfig.Load();
            if (squad is not null)
            {
                foreach (var persona in squad.Personas)
                {
                    list.Add(new McpPromptInfo(
                        Name: $"persona:{persona.Name}",
                        Description: $"[{persona.Role}] {persona.Description}",
                        Arguments: inputArgSchema));
                }
            }
        }
        catch { }

        return list;
    }

    private static McpGetPromptResult? ResolvePromptGet(string name, string? inputArg)
    {
        string? description = null;
        string? systemPrompt = null;

        if (name.StartsWith("persona:", StringComparison.OrdinalIgnoreCase))
        {
            var personaName = name["persona:".Length..];
            var squad = SquadConfig.Load();
            var persona = squad?.GetPersona(personaName);
            if (persona is null) return null;
            description = $"[{persona.Role}] {persona.Description}";
            systemPrompt = persona.SystemPrompt;
        }
        else
        {
            var pat = PatternRegistry.Resolve(name);
            if (pat is null) return null;
            description = pat.Description;
            systemPrompt = pat.SystemPrompt;
        }

        var combinedText = string.IsNullOrWhiteSpace(inputArg)
            ? systemPrompt
            : $"{systemPrompt}\n\n---\n\n{inputArg}";

        return new McpGetPromptResult(
            Description: description,
            Messages: new List<McpPromptMessage>
            {
                new(Role: "user", Content: new McpContentItem(Type: "text", Text: combinedText)),
            });
    }

    private static List<McpResourceInfo> BuildResourcesList()
    {
        var list = new List<McpResourceInfo>();
        var cwd = Directory.GetCurrentDirectory();
        var squadPath = Path.Combine(cwd, ".squad.json");
        if (File.Exists(squadPath))
        {
            list.Add(new McpResourceInfo(
                Uri: "squad://config",
                Name: ".squad.json",
                Description: "Active Squad persona and routing configuration",
                MimeType: "application/json"));
        }

        var historyDir = Path.Combine(cwd, ".squad", "history");
        if (Directory.Exists(historyDir))
        {
            try
            {
                foreach (var file in Directory.GetFiles(historyDir, "*.md").OrderBy(f => f, StringComparer.Ordinal))
                {
                    var pName = Path.GetFileNameWithoutExtension(file);
                    list.Add(new McpResourceInfo(
                        Uri: $"squad://memory/{pName}",
                        Name: $"Persona Memory: {pName}",
                        Description: $"Persistent session memory for persona '{pName}'",
                        MimeType: "text/markdown"));
                }
            }
            catch { }
        }

        return list;
    }

    private static McpReadResourceResult? ReadMcpResource(string uri)
    {
        var cwd = Directory.GetCurrentDirectory();
        if (string.Equals(uri, "squad://config", StringComparison.OrdinalIgnoreCase))
        {
            var squadPath = Path.Combine(cwd, ".squad.json");
            if (!File.Exists(squadPath)) return null;
            var text = File.ReadAllText(squadPath);
            return new McpReadResourceResult(new List<McpResourceContent>
            {
                new(Uri: "squad://config", MimeType: "application/json", Text: text),
            });
        }

        const string memoryPrefix = "squad://memory/";
        if (uri.StartsWith(memoryPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var personaName = uri[memoryPrefix.Length..];
            try
            {
                var memory = new PersonaMemory();
                var history = memory.ReadHistory(personaName);
                if (string.IsNullOrEmpty(history)) return null;
                return new McpReadResourceResult(new List<McpResourceContent>
                {
                    new(Uri: uri, MimeType: "text/markdown", Text: history),
                });
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private static object? ConvertJsonElementToArg(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when element.TryGetInt32(out int i) => i,
            JsonValueKind.Number when element.TryGetInt64(out long l) => l,
            JsonValueKind.Number when element.TryGetDouble(out double d) => d,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => element.Clone(),
        };
    }

    private static string SerializeResult(JsonElement? id, JsonElement result)
    {
        var resp = new McpJsonRpcResponse(
            JsonRpc: "2.0",
            Id: id,
            Result: result,
            Error: null);
        return JsonSerializer.Serialize(resp, McpJsonContext.Default.McpJsonRpcResponse);
    }

    private static string SerializeError(JsonElement? id, int code, string message)
    {
        var resp = new McpJsonRpcResponse(
            JsonRpc: "2.0",
            Id: id,
            Result: null,
            Error: new McpJsonRpcError(Code: code, Message: message));
        return JsonSerializer.Serialize(resp, McpJsonContext.Default.McpJsonRpcResponse);
    }
}

// ── AOT-safe JSON-RPC 2.0 / MCP wire records ─────────────────────────────────

internal sealed record McpJsonRpcResponse(
    [property: JsonPropertyName("jsonrpc")] string JsonRpc,
    [property: JsonPropertyName("id")] JsonElement? Id,
    [property: JsonPropertyName("result")] JsonElement? Result,
    [property: JsonPropertyName("error")] McpJsonRpcError? Error
);

internal sealed record McpJsonRpcError(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string Message
);

internal sealed record McpInitializeResult(
    [property: JsonPropertyName("protocolVersion")] string ProtocolVersion,
    [property: JsonPropertyName("capabilities")] McpCapabilities Capabilities,
    [property: JsonPropertyName("serverInfo")] McpServerInfo ServerInfo
);

internal sealed record McpCapabilities(
    [property: JsonPropertyName("tools")] McpToolsCapability Tools,
    [property: JsonPropertyName("prompts")] McpPromptsCapability? Prompts = null,
    [property: JsonPropertyName("resources")] McpResourcesCapability? Resources = null
);

internal sealed record McpToolsCapability(
    [property: JsonPropertyName("listChanged")] bool ListChanged
);

internal sealed record McpPromptsCapability(
    [property: JsonPropertyName("listChanged")] bool ListChanged
);

internal sealed record McpResourcesCapability(
    [property: JsonPropertyName("listChanged")] bool ListChanged
);

internal sealed record McpServerInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string Version
);

internal sealed record McpToolInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("inputSchema")] JsonElement InputSchema
);

internal sealed record McpToolsListResult(
    [property: JsonPropertyName("tools")] List<McpToolInfo> Tools
);

internal sealed record McpContentItem(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] string Text
);

internal sealed record McpCallToolResult(
    [property: JsonPropertyName("content")] List<McpContentItem> Content,
    [property: JsonPropertyName("isError")] bool IsError
);

internal sealed record McpPromptArgument(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("required")] bool Required
);

internal sealed record McpPromptInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("arguments")] List<McpPromptArgument>? Arguments = null
);

internal sealed record McpPromptsListResult(
    [property: JsonPropertyName("prompts")] List<McpPromptInfo> Prompts
);

internal sealed record McpPromptMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] McpContentItem Content
);

internal sealed record McpGetPromptResult(
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("messages")] List<McpPromptMessage> Messages
);

internal sealed record McpResourceInfo(
    [property: JsonPropertyName("uri")] string Uri,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("mimeType")] string MimeType
);

internal sealed record McpResourcesListResult(
    [property: JsonPropertyName("resources")] List<McpResourceInfo> Resources
);

internal sealed record McpResourceContent(
    [property: JsonPropertyName("uri")] string Uri,
    [property: JsonPropertyName("mimeType")] string MimeType,
    [property: JsonPropertyName("text")] string Text
);

internal sealed record McpReadResourceResult(
    [property: JsonPropertyName("contents")] List<McpResourceContent> Contents
);
