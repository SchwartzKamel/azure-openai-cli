using System.Text.Json;
using AzureOpenAI_CLI.Squad;

namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// T5 tests: proves NDJSON streaming event formatting (<c>--jsonl</c>) and
/// machine-readable JSON discovery for <c>--personas --json</c> and <c>--models --json</c>.
/// </summary>
[Collection("ConsoleCapture")]
public class JsonlAndMachineDiscoveryTests
{
    [Fact]
    public void ParseArgs_JsonlFlag_SetsJsonlTrue()
    {
        var opts = Program.ParseArgs(["--jsonl", "stream this"]);
        Assert.True(opts.Jsonl);
        Assert.False(opts.ParseError);
        Assert.Equal("stream this", opts.Prompt);
    }

    [Fact]
    public void FormatJsonlEvents_ProduceSingleLineCompactNdjson()
    {
        var delta1 = Program.FormatJsonlDeltaEvent("Hello ");
        var delta2 = Program.FormatJsonlDeltaEvent("world!");
        var done = Program.FormatJsonlDoneEvent(
            response: "Hello world!",
            model: "gpt-4o-mini",
            mode: "standard",
            inputTokens: 10,
            outputTokens: 4,
            durationMs: 88);

        // Must be single-line (no embedded newlines) for NDJSON parsers
        Assert.DoesNotContain("\n", delta1);
        Assert.DoesNotContain("\n", delta2);
        Assert.DoesNotContain("\n", done);

        using var d1 = JsonDocument.Parse(delta1);
        Assert.Equal("delta", d1.RootElement.GetProperty("type").GetString());
        Assert.Equal("Hello ", d1.RootElement.GetProperty("text").GetString());

        using var doneDoc = JsonDocument.Parse(done);
        Assert.Equal("done", doneDoc.RootElement.GetProperty("type").GetString());
        Assert.Equal("Hello world!", doneDoc.RootElement.GetProperty("response").GetString());
        Assert.Equal(14, doneDoc.RootElement.GetProperty("total_tokens").GetInt32());
        Assert.Equal(88, doneDoc.RootElement.GetProperty("duration_ms").GetInt64());
    }

    [Fact]
    public void ListPersonasCommand_WithJsonFlag_EmitsValidPersonaArray()
    {
        using var cwd = new TempCwd();
        SquadInitializer.Initialize();

        var origOut = Console.Out;
        var outWriter = new StringWriter();
        try
        {
            Console.SetOut(outWriter);
            var opts = Program.ParseArgs(["--personas", "--json"]);
            int rc = Program.ListPersonasCommand(opts);

            Assert.Equal(0, rc);
            using var doc = JsonDocument.Parse(outWriter.ToString());
            Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
            Assert.True(doc.RootElement.GetArrayLength() >= 1);
            var first = doc.RootElement[0];
            Assert.False(string.IsNullOrWhiteSpace(first.GetProperty("name").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(first.GetProperty("system_prompt").GetString()));
        }
        finally
        {
            Console.SetOut(origOut);
        }
    }

    [Fact]
    public void ListModelsCommand_WithJsonFlag_EmitsValidJsonDictionary()
    {
        var cfg = new UserConfig();
        cfg.Models["fast"] = "gpt-4o-mini";
        cfg.Models["smart"] = "gpt-4o";

        var origOut = Console.Out;
        var outWriter = new StringWriter();
        try
        {
            Console.SetOut(outWriter);
            int rc = Program.ListModelsCommand(cfg, jsonMode: true);

            Assert.Equal(0, rc);
            using var doc = JsonDocument.Parse(outWriter.ToString());
            Assert.Equal("gpt-4o-mini", doc.RootElement.GetProperty("fast").GetString());
            Assert.Equal("gpt-4o", doc.RootElement.GetProperty("smart").GetString());
        }
        finally
        {
            Console.SetOut(origOut);
        }
    }
}
