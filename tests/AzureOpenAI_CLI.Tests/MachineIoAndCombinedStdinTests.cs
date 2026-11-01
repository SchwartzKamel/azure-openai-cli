using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// T1 tests: proves the machine I/O contract (--json happy-path envelope,
/// --schema wire enforcement in BuildModernChatOptions, --code block extraction,
/// and FR-001 combined stdin + positional prompt piping).
/// </summary>
[Collection("ConsoleCapture")]
public class MachineIoAndCombinedStdinTests
{
    // ── Combined Stdin + Positional Prompt (FR-001 parity) ─────────────────

    [Fact]
    public void MergeStdinAndPrompt_BothProvided_CombinesWithContextTag()
    {
        var merged = Program.MergeStdinAndPrompt("fn main() {}\n", "explain this code");

        Assert.NotNull(merged);
        Assert.Contains("<context>\nfn main() {}\n</context>", merged);
        Assert.EndsWith("\n\nexplain this code", merged);
    }

    [Fact]
    public void MergeStdinAndPrompt_OnlyStdin_ReturnsStdinUnchanged()
    {
        var merged = Program.MergeStdinAndPrompt("only stdin text", null);
        Assert.Equal("only stdin text", merged);
    }

    [Fact]
    public void MergeStdinAndPrompt_OnlyPrompt_ReturnsPromptUnchanged()
    {
        var merged = Program.MergeStdinAndPrompt(null, "only prompt text");
        Assert.Equal("only prompt text", merged);
    }

    [Fact]
    public void RunEstimate_WithStdinAndPositionalPrompt_CombinesBothInTokenEstimate()
    {
        // Proves that when stdin is redirected AND a positional prompt is passed,
        // RunEstimate (and RunAsync) merges both rather than discarding stdin.
        var origIn = Console.In;
        var origOut = Console.Out;
        var outWriter = new StringWriter();
        try
        {
            Console.SetIn(new StringReader(new string('x', 400))); // ~100 tokens
            Console.SetOut(outWriter);

            var opts = Program.ParseArgs(["--estimate", "--json", "--model", "gpt-4o-mini", "summarize this"]);
            int rc = Program.RunEstimate(opts);

            Assert.Equal(0, rc);
            using var doc = JsonDocument.Parse(outWriter.ToString());
            int inputTokens = doc.RootElement.GetProperty("input_tokens_est").GetInt32();
            // "summarize this" alone is ~4 tokens; combined with 400 chars is > 90 tokens.
            Assert.True(inputTokens > 80, $"Expected combined stdin+prompt tokens > 80, got {inputTokens}");
        }
        finally
        {
            Console.SetIn(origIn);
            Console.SetOut(origOut);
        }
    }

    // ── --code Extraction Mode (FR-005) ────────────────────────────────────

    [Fact]
    public void ParseArgs_CodeFlag_SetsCodeTrue()
    {
        var opts = Program.ParseArgs(["--code", "write a script"]);
        Assert.True(opts.Code);
        Assert.Equal("write a script", opts.Prompt);
    }

    [Fact]
    public void ExtractCodeBlocks_SingleFencedBlock_StripsProseAndFences()
    {
        var raw = """
            Here is the Python function you requested:
            ```python
            def greet(name: str) -> str:
                return f"Hello, {name}!"
            ```
            Hope that helps!
            """;

        var extracted = Program.ExtractCodeBlocks(raw);
        Assert.Equal("def greet(name: str) -> str:\n    return f\"Hello, {name}!\"", extracted);
    }

    [Fact]
    public void ExtractCodeBlocks_MultipleFencedBlocks_JoinsWithBlankLine()
    {
        var raw = """
            First file:
            ```sh
            echo "one"
            ```
            Second file:
            ```sh
            echo "two"
            ```
            """;

        var extracted = Program.ExtractCodeBlocks(raw);
        Assert.Equal("echo \"one\"\n\necho \"two\"", extracted);
    }

    [Fact]
    public void ExtractCodeBlocks_NoFences_ReturnsTrimmedResponse()
    {
        var raw = "  SELECT * FROM users WHERE active = 1;  \n";
        var extracted = Program.ExtractCodeBlocks(raw);
        Assert.Equal("SELECT * FROM users WHERE active = 1;", extracted);
    }

    // ── --json Happy-Path Envelope (ChatJsonResponse) ──────────────────────

    [Fact]
    public void FormatChatJsonResponse_ProducesValidJsonWithAllFields()
    {
        var json = Program.FormatChatJsonResponse(
            response: "42",
            model: "gpt-4o-mini",
            mode: "standard",
            inputTokens: 12,
            outputTokens: 3,
            durationMs: 145,
            cached: false);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("42", root.GetProperty("response").GetString());
        Assert.Equal("gpt-4o-mini", root.GetProperty("model").GetString());
        Assert.Equal("standard", root.GetProperty("mode").GetString());
        Assert.Equal(12, root.GetProperty("input_tokens").GetInt32());
        Assert.Equal(3, root.GetProperty("output_tokens").GetInt32());
        Assert.Equal(15, root.GetProperty("total_tokens").GetInt32());
        Assert.Equal(145, root.GetProperty("duration_ms").GetInt64());
        Assert.False(root.GetProperty("cached").GetBoolean());
    }

    [Fact]
    public async Task RalphWorkflow_JsonAndCodeMode_EmitsSingleValidJsonWithExtractedCode()
    {
        // Proves that Ralph mode with --json and --code suppresses human stderr banners
        // and emits a single valid ChatJsonResponse on stdout containing extracted code.
        var origOut = Console.Out;
        var origErr = Console.Error;
        var outWriter = new StringWriter();
        var errWriter = new StringWriter();
        var tempDir = Path.Combine(Path.GetTempPath(), $"ralph-json-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var origCwd = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(tempDir);
            Console.SetOut(outWriter);
            Console.SetError(errWriter);

            var fakeClient = new FakeReplyClient("Here is the fix:\n```csharp\nreturn 0;\n```\nDone.");
            int rc = await AzureOpenAI_CLI.Ralph.RalphWorkflow.RunAsync(
                fakeClient,
                taskPrompt: "fix bug",
                systemPrompt: "sys",
                validateCommand: "exit 0",
                maxIterations: 2,
                temperature: 0.15f,
                maxTokens: 512,
                timeoutSeconds: 10,
                tools: "",
                ct: CancellationToken.None,
                jsonMode: true,
                rawMode: false,
                codeMode: true,
                modelName: "gpt-4o");

            Assert.Equal(0, rc);
            Assert.Equal(string.Empty, errWriter.ToString());

            using var doc = JsonDocument.Parse(outWriter.ToString());
            var root = doc.RootElement;
            Assert.Equal("return 0;", root.GetProperty("response").GetString());
            Assert.Equal("gpt-4o", root.GetProperty("model").GetString());
            Assert.Equal("ralph", root.GetProperty("mode").GetString());
        }
        finally
        {
            Console.SetOut(origOut);
            Console.SetError(origErr);
            try { Directory.SetCurrentDirectory(origCwd); } catch { }
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    // ── --schema Wire Enforcement (BuildModernChatOptions) ─────────────────

    [Fact]
    public void BuildModernChatOptions_WithSchemaTemperatureAndMaxTokens_PopulatesChatOptions()
    {
        var schemaJson = """{"type":"object","properties":{"ok":{"type":"boolean"}},"required":["ok"]}""";
        var opts = Program.BuildModernChatOptions(temperature: 0.2f, maxTokens: 1024, schema: schemaJson);

        Assert.Equal(0.2f, opts.Temperature);
        Assert.Equal(1024, opts.MaxOutputTokens);
        Assert.NotNull(opts.ResponseFormat);
        var schemaFormat = Assert.IsType<ChatResponseFormatJson>(opts.ResponseFormat);
        Assert.Equal("response_schema", schemaFormat.SchemaName);
        Assert.NotNull(schemaFormat.Schema);
    }

    private sealed class FakeReplyClient : IChatClient
    {
        private readonly string _reply;
        public FakeReplyClient(string reply) => _reply = reply;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, _reply)));

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, _reply);
            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
