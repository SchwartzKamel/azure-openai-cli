using System.Text.Json;
using AzureOpenAI_CLI_V2.Observability;
using AzureOpenAI_CLI_V2.Tools;
using Microsoft.Extensions.AI;

namespace AzureOpenAI_CLI.V2.Tests;

/// <summary>
/// Kramer audit wave follow-ups:
///   • M3 — price-table JSON uses the single app-wide <c>AppJsonContext</c>.
///   • M4 — tool alias resolution is canonicalized once; short aliases and full
///     names are interchangeable but produce the same tool set.
///   • M5 — <c>ToolRegistry.DefaultAgentTools</c> is a single source of truth
///     and contains the expected canonical names.
///   • L4 — <c>--completions</c> unknown shell routes through
///     <c>ErrorAndExit</c> with exit code 2 and the <c>[ERROR]</c> prefix.
/// Pass the pass AND fail the fail — every case asserts the positive and, where
/// meaningful, the negative path.
/// </summary>
[Collection("ConsoleCapture")]
public class V2CleanupWaveTests
{
    // ── M3: PriceTableEntry flows through AppJsonContext ───────────────────

    [Fact]
    public void AppJsonContext_ExposesPriceTableEntry_DictionaryTypeInfo()
    {
        var ti = AppJsonContext.Default.DictionaryStringPriceTableEntry;
        Assert.NotNull(ti);
        Assert.Equal(typeof(Dictionary<string, PriceTableEntry>), ti.Type);
    }

    [Fact]
    public void AppJsonContext_DeserializesPriceTableJson()
    {
        var json = """
            { "gpt-test": { "InputPer1K": 0.01, "OutputPer1K": 0.02 } }
            """;
        var parsed = JsonSerializer.Deserialize(
            json, AppJsonContext.Default.DictionaryStringPriceTableEntry);
        Assert.NotNull(parsed);
        Assert.Single(parsed!);
        Assert.Equal(0.01, parsed["gpt-test"].InputPer1K);
        Assert.Equal(0.02, parsed["gpt-test"].OutputPer1K);
    }

    [Fact]
    public void AppJsonContext_InvalidPriceTableJson_Throws()
    {
        // Fail the fail: malformed JSON must throw, not silently return empty.
        var bad = "{ not-valid-json";
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize(bad, AppJsonContext.Default.DictionaryStringPriceTableEntry));
    }

    // ── M4: alias canonicalization ─────────────────────────────────────────

    [Fact]
    public void ToolRegistry_Canonicalize_ShortAlias_ReturnsFullName()
    {
        Assert.Equal("shell_exec", ToolRegistry.Canonicalize("shell"));
        Assert.Equal("read_file", ToolRegistry.Canonicalize("file"));
        Assert.Equal("web_fetch", ToolRegistry.Canonicalize("web"));
        Assert.Equal("get_clipboard", ToolRegistry.Canonicalize("clipboard"));
        Assert.Equal("get_datetime", ToolRegistry.Canonicalize("datetime"));
        Assert.Equal("delegate_task", ToolRegistry.Canonicalize("delegate"));
    }

    [Fact]
    public void ToolRegistry_Canonicalize_FullName_Unchanged()
    {
        Assert.Equal("shell_exec", ToolRegistry.Canonicalize("shell_exec"));
        Assert.Equal("read_file", ToolRegistry.Canonicalize("read_file"));
    }

    [Fact]
    public void ToolRegistry_Canonicalize_Unknown_Unchanged()
    {
        // Unknown tokens pass through so downstream filter silently ignores them.
        Assert.Equal("bogus", ToolRegistry.Canonicalize("bogus"));
    }

    [Fact]
    public void ToolRegistry_CreateMafTools_ShortAndFullNames_Equivalent()
    {
        var a = ToolRegistry.CreateMafTools(new[] { "shell", "file" });
        var b = ToolRegistry.CreateMafTools(new[] { "shell_exec", "read_file" });
        Assert.Equal(a.Count, b.Count);
        var aNames = a.OfType<AIFunction>().Select(t => t.Name).OrderBy(s => s).ToArray();
        var bNames = b.OfType<AIFunction>().Select(t => t.Name).OrderBy(s => s).ToArray();
        Assert.Equal(aNames, bNames);
        Assert.Equal(new[] { "read_file", "shell_exec" }, aNames);
    }

    [Fact]
    public void ToolRegistry_CreateMafTools_MixedAliasesAndFullNames_Deduplicated()
    {
        // M4 positive path: asking for "shell" and "shell_exec" together resolves
        // to a single tool (canonicalize-once dedupes via the hashset).
        var tools = ToolRegistry.CreateMafTools(new[] { "shell", "shell_exec", "file" });
        var names = tools.OfType<AIFunction>().Select(t => t.Name).OrderBy(s => s).ToArray();
        Assert.Equal(new[] { "read_file", "shell_exec" }, names);
    }

    [Fact]
    public void ToolRegistry_CreateMafTools_UnknownToken_SilentlyIgnored()
    {
        var tools = ToolRegistry.CreateMafTools(new[] { "bogus-tool" });
        Assert.Empty(tools);
    }

    [Fact]
    public void ToolRegistry_CreateMafTools_Null_EnablesAll()
    {
        var tools = ToolRegistry.CreateMafTools(null);
        var names = tools.OfType<AIFunction>().Select(t => t.Name).OrderBy(s => s).ToArray();
        Assert.Equal(6, names.Length);
        Assert.Contains("shell_exec", names);
        Assert.Contains("read_file", names);
        Assert.Contains("web_fetch", names);
        Assert.Contains("get_clipboard", names);
        Assert.Contains("get_datetime", names);
        Assert.Contains("delegate_task", names);
    }

    // ── M5: DefaultAgentTools is the single source of truth ───────────────

    [Fact]
    public void ToolRegistry_DefaultAgentTools_Contract()
    {
        var expected = new[] { "shell_exec", "read_file", "web_fetch", "get_datetime", "delegate_task" };
        Assert.Equal(expected.OrderBy(s => s), ToolRegistry.DefaultAgentTools.OrderBy(s => s));
    }

    [Fact]
    public void ToolRegistry_DefaultChildAgentTools_ExcludesDelegateAndClipboard()
    {
        // Fail the fail: the child default must NOT enable nested delegation
        // (even if the depth guard would save us) and must NOT touch clipboard.
        Assert.DoesNotContain("delegate_task", ToolRegistry.DefaultChildAgentTools);
        Assert.DoesNotContain("get_clipboard", ToolRegistry.DefaultChildAgentTools);
        Assert.Contains("shell_exec", ToolRegistry.DefaultChildAgentTools);
        Assert.Contains("read_file", ToolRegistry.DefaultChildAgentTools);
        Assert.Contains("web_fetch", ToolRegistry.DefaultChildAgentTools);
        Assert.Contains("get_datetime", ToolRegistry.DefaultChildAgentTools);
    }

    // ── L4: unknown --completions shell routes through ErrorAndExit ────────

    [Fact]
    public void EmitCompletions_UnknownShell_ReturnsTwo_WritesErrorPrefix()
    {
        var stderr = new StringWriter();
        var oldErr = Console.Error;
        Console.SetError(stderr);
        try
        {
            var rc = Program.EmitCompletions("tcsh");
            Assert.Equal(2, rc);
            var msg = stderr.ToString();
            Assert.Contains("[ERROR]", msg);
            Assert.Contains("Unsupported shell", msg);
        }
        finally { Console.SetError(oldErr); }
    }

    [Theory]
    [InlineData("bash")]
    [InlineData("zsh")]
    [InlineData("fish")]
    public void EmitCompletions_KnownShell_ReturnsZero(string shell)
    {
        var oldOut = Console.Out;
        Console.SetOut(new StringWriter());
        try
        {
            Assert.Equal(0, Program.EmitCompletions(shell));
        }
        finally { Console.SetOut(oldOut); }
    }
}
