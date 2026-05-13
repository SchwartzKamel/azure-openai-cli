// S04E04 Wave 2 -- Puddy. Facts for `az-ai models {list,show,capabilities}`
// (Cli/ModelsCommand.cs, landed in 457e06b). Hermetic: registry is injected
// via reflection on Program.RegistryEntries (pattern lifted verbatim from
// CapabilityGateStartupTests.cs, S04E03 commit befac7f). No I/O, no network,
// no sleep. Either it works or it doesn't.

using System.Reflection;
using System.Text.Json;
using AzureOpenAI_CLI.Cli;
using AzureOpenAI_CLI.Registry;

namespace AzureOpenAI_CLI.Tests.ModelsCommand;

[Collection("ConsoleCapture")]
public class ModelsCommandTests
{
    // -- registry reflection seam (re-implemented; same pattern as S04E03
    //    tests/AzureOpenAI_CLI.Tests/CapabilityGateStartupTests.cs:48-70)

    private static readonly PropertyInfo RegistryEntriesProp =
        typeof(Program).GetProperty(
            "RegistryEntries",
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException(
            "Program.RegistryEntries property not found via reflection.");

    private sealed class RegistryScope : IDisposable
    {
        private readonly object? _previous;
        public RegistryScope(params ModelRegistryEntry[] entries)
        {
            _previous = RegistryEntriesProp.GetValue(null);
            RegistryEntriesProp.SetValue(null, entries);
        }
        public void Dispose() => RegistryEntriesProp.SetValue(null, _previous);
    }

    private sealed class EnvScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _prev;
        public EnvScope(string name, string? value)
        {
            _name = name;
            _prev = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }
        public void Dispose() => Environment.SetEnvironmentVariable(_name, _prev);
    }

    private sealed class ConsoleScope : IDisposable
    {
        public StringWriter Out { get; } = new();
        public StringWriter Err { get; } = new();
        private readonly TextWriter _prevOut;
        private readonly TextWriter _prevErr;
        public ConsoleScope()
        {
            _prevOut = Console.Out;
            _prevErr = Console.Error;
            Console.SetOut(Out);
            Console.SetError(Err);
        }
        public void Dispose()
        {
            Console.SetOut(_prevOut);
            Console.SetError(_prevErr);
            Out.Dispose();
            Err.Dispose();
        }
    }

    private static ModelRegistryEntry Entry(
        string name,
        string provider = "azure",
        string[]? caps = null,
        int contextWindow = 8192,
        string costTier = "standard",
        string? cardPath = "docs/model-cards/x.md")
        => new(name, provider, caps ?? new[] { "streaming", "system_prompt" }, contextWindow, costTier, cardPath);

    // -- Test 1: empty registry on `list` -> rc=0, stderr [INFO] ----------

    [Fact]
    public void List_EmptyRegistry_ReturnsZeroAndWritesInfoToStderr()
    {
        using var _ = new RegistryScope();
        using var __ = new EnvScope("AZUREOPENAIMODEL", null);
        using var cap = new ConsoleScope();

        var rc = AzureOpenAI_CLI.Cli.ModelsCommand.Run(new[] { "models", "list" });

        Assert.Equal(0, rc);
        Assert.Equal(string.Empty, cap.Out.ToString());
        var err = cap.Err.ToString();
        Assert.Contains("[INFO]", err, StringComparison.Ordinal);
        Assert.Contains("models", err, StringComparison.Ordinal);
    }

    // -- Test 2: populated `list` -> header + rows ------------------------

    [Fact]
    public void List_Populated_RendersHeaderAndRows()
    {
        using var _ = new RegistryScope(
            Entry("alpha-model"),
            Entry("beta-model", provider: "foundry"));
        using var __ = new EnvScope("AZUREOPENAIMODEL", null);
        using var cap = new ConsoleScope();

        var rc = AzureOpenAI_CLI.Cli.ModelsCommand.Run(new[] { "models", "list" });

        Assert.Equal(0, rc);
        var stdout = cap.Out.ToString();
        Assert.Contains("Name", stdout, StringComparison.Ordinal);
        Assert.Contains("Provider", stdout, StringComparison.Ordinal);
        Assert.Contains("Capabilities", stdout, StringComparison.Ordinal);
        Assert.Contains("Default", stdout, StringComparison.Ordinal);
        Assert.Contains("Allowlisted", stdout, StringComparison.Ordinal);
        Assert.Contains("alpha-model", stdout, StringComparison.Ordinal);
        Assert.Contains("beta-model", stdout, StringComparison.Ordinal);
        Assert.Contains("foundry", stdout, StringComparison.Ordinal);
    }

    // -- Test 3: default marker lands on the right row -------------------

    [Fact]
    public void List_DefaultMarker_AppearsOnDefaultRowOnly()
    {
        using var _ = new RegistryScope(
            Entry("alpha-model"),
            Entry("beta-model"));
        using var __ = new EnvScope("AZUREOPENAIMODEL", "beta-model,alpha-model");
        using var cap = new ConsoleScope();

        var rc = AzureOpenAI_CLI.Cli.ModelsCommand.Run(new[] { "models", "list" });

        Assert.Equal(0, rc);
        var stdout = cap.Out.ToString();
        Assert.Contains("(default)", stdout, StringComparison.Ordinal);
        // The marker must be on the beta-model row, not alpha. Slice per-line.
        var lines = stdout.Split('\n');
        var betaLine = Array.Find(lines, l => l.Contains("beta-model", StringComparison.Ordinal));
        var alphaLine = Array.Find(lines, l => l.Contains("alpha-model", StringComparison.Ordinal));
        Assert.NotNull(betaLine);
        Assert.NotNull(alphaLine);
        Assert.Contains("(default)", betaLine!, StringComparison.Ordinal);
        Assert.True(alphaLine!.IndexOf("(default)", StringComparison.Ordinal) < 0,
            "alpha-model row must not carry the default marker");
        // Both models are in AZUREOPENAIMODEL so both are allowlisted.
        Assert.Contains("(allow)", betaLine, StringComparison.Ordinal);
        Assert.Contains("(allow)", alphaLine, StringComparison.Ordinal);
    }

    // -- Test 4: list --json round-trips through AppJsonContext ----------

    [Fact]
    public void List_Json_ParsesAsModelListEntryArray()
    {
        using var _ = new RegistryScope(
            Entry("alpha-model", caps: new[] { "tool_calls", "streaming" }),
            Entry("beta-model"));
        using var __ = new EnvScope("AZUREOPENAIMODEL", "alpha-model,beta-model");
        using var cap = new ConsoleScope();

        var rc = AzureOpenAI_CLI.Cli.ModelsCommand.Run(new[] { "models", "list", "--json" });

        Assert.Equal(0, rc);
        var stdout = cap.Out.ToString().Trim();
        var rows = JsonSerializer.Deserialize(stdout, AppJsonContext.Default.ModelListEntryJsonArray);
        Assert.NotNull(rows);
        Assert.Equal(2, rows!.Length);
        // Ordinal-sorted alphabetically: alpha, beta.
        Assert.Equal("alpha-model", rows[0].Name);
        Assert.Contains("tool_calls", rows[0].Capabilities);
        Assert.Equal("beta-model", rows[1].Name);

        // jq-friendliness: object-shape field names per ADR-014. Booleans are
        // checked via JsonDocument because record-positional deserialization
        // does not honour [property: JsonPropertyName] on the parameter side
        // for the source-gen path. The serialized JSON shape is what jq sees,
        // and that's what we contract on.
        using var doc = JsonDocument.Parse(stdout);
        var first = doc.RootElement[0];
        Assert.True(first.TryGetProperty("name", out var je1));
        Assert.True(first.TryGetProperty("provider", out var je2));
        Assert.True(first.TryGetProperty("capabilities", out var je3));
        Assert.True(first.TryGetProperty("default", out var je4));
        Assert.True(first.TryGetProperty("allowlisted", out var je5));
        Assert.True(je4.GetBoolean(), "alpha-model is the registry default per AZUREOPENAIMODEL[0]");
        Assert.True(first.GetProperty("allowlisted").GetBoolean());
        var second = doc.RootElement[1];
        Assert.False(second.GetProperty("default").GetBoolean());
        Assert.True(second.GetProperty("allowlisted").GetBoolean(),
            "beta-model is in the AZUREOPENAIMODEL allowlist");
    }

    // -- Test 5: list --raw is tab-separated, no decorative chars --------

    [Fact]
    public void List_Raw_TabSeparated_NoMarkersOrUnderline()
    {
        using var _ = new RegistryScope(
            Entry("alpha-model", caps: new[] { "streaming" }),
            Entry("beta-model", caps: new[] { "streaming" }));
        using var __ = new EnvScope("AZUREOPENAIMODEL", "alpha-model,beta-model");
        using var cap = new ConsoleScope();

        var rc = AzureOpenAI_CLI.Cli.ModelsCommand.Run(new[] { "models", "list", "--raw" });

        Assert.Equal(0, rc);
        var stdout = cap.Out.ToString();
        Assert.Contains("alpha-model\tazure\tstreaming\ttrue\ttrue", stdout, StringComparison.Ordinal);
        // No ANSI escape sequences.
        Assert.True(stdout.IndexOf('\u001B') < 0, "raw output must not contain ANSI ESC");
        // No header underline ("----") leaking into raw.
        Assert.True(stdout.IndexOf("----", StringComparison.Ordinal) < 0,
            "raw output must not contain table underline");
        // No marker words in raw form -- booleans only.
        Assert.True(stdout.IndexOf("(default)", StringComparison.Ordinal) < 0);
        Assert.True(stdout.IndexOf("(allow)", StringComparison.Ordinal) < 0);
    }

    // -- Test 6: `show <name>` happy path renders the full card ----------

    [Fact]
    public void Show_HappyPath_RendersAllCardFields()
    {
        using var _ = new RegistryScope(
            Entry("gpt-4o-mini", provider: "openai", caps: new[] { "tool_calls", "json_mode" },
                  contextWindow: 128000, costTier: "low", cardPath: "docs/model-cards/gpt-4o-mini.md"));
        using var __ = new EnvScope("AZUREOPENAIMODEL", "gpt-4o-mini");
        using var cap = new ConsoleScope();

        var rc = AzureOpenAI_CLI.Cli.ModelsCommand.Run(new[] { "models", "show", "gpt-4o-mini" });

        Assert.Equal(0, rc);
        var stdout = cap.Out.ToString();
        Assert.Contains("gpt-4o-mini", stdout, StringComparison.Ordinal);
        Assert.Contains("Provider", stdout, StringComparison.Ordinal);
        Assert.Contains("openai", stdout, StringComparison.Ordinal);
        Assert.Contains("Capabilities", stdout, StringComparison.Ordinal);
        Assert.Contains("json_mode", stdout, StringComparison.Ordinal);
        Assert.Contains("tool_calls", stdout, StringComparison.Ordinal);
        Assert.Contains("Context Window", stdout, StringComparison.Ordinal);
        Assert.Contains("128000", stdout, StringComparison.Ordinal);
        Assert.Contains("Card Path", stdout, StringComparison.Ordinal);
        Assert.Contains("docs/model-cards/gpt-4o-mini.md", stdout, StringComparison.Ordinal);
        Assert.Contains("Cost Tier", stdout, StringComparison.Ordinal);
        Assert.Contains("low", stdout, StringComparison.Ordinal);
        Assert.Contains("Default", stdout, StringComparison.Ordinal);
        Assert.Contains("(default)", stdout, StringComparison.Ordinal);
    }

    // -- Test 7: unknown model on `show` -> rc=2 + stderr error ----------

    [Fact]
    public void Show_UnknownModel_ReturnsTwoAndWritesError()
    {
        using var _ = new RegistryScope(Entry("alpha-model"));
        using var __ = new EnvScope("AZUREOPENAIMODEL", null);
        using var cap = new ConsoleScope();

        var rc = AzureOpenAI_CLI.Cli.ModelsCommand.Run(new[] { "models", "show", "ghost-model" });

        Assert.Equal(2, rc);
        var err = cap.Err.ToString();
        Assert.Contains("[ERROR]", err, StringComparison.Ordinal);
        Assert.Contains("ghost-model", err, StringComparison.Ordinal);
        // The error must land on stderr; stdout must be empty.
        Assert.Equal(string.Empty, cap.Out.ToString());
    }

    // -- Test 8: `show --json <name>` parses as ModelShowJson ------------

    [Fact]
    public void Show_Json_ParsesAsModelShowObject()
    {
        using var _ = new RegistryScope(
            Entry("gpt-4o-mini", provider: "openai", caps: new[] { "tool_calls", "streaming" },
                  contextWindow: 128000, costTier: "low",
                  cardPath: "docs/model-cards/gpt-4o-mini.md"));
        using var __ = new EnvScope("AZUREOPENAIMODEL", "gpt-4o-mini,other");
        using var cap = new ConsoleScope();

        var rc = AzureOpenAI_CLI.Cli.ModelsCommand.Run(
            new[] { "models", "show", "gpt-4o-mini", "--json" });

        Assert.Equal(0, rc);
        var stdout = cap.Out.ToString().Trim();
        var dto = JsonSerializer.Deserialize(stdout, AppJsonContext.Default.ModelShowJson);
        Assert.NotNull(dto);
        Assert.Equal("gpt-4o-mini", dto!.Name);
        Assert.Equal("openai", dto.Provider);
        Assert.Equal(128000, dto.ContextWindow);
        Assert.Equal("low", dto.CostTier);
        Assert.Equal("docs/model-cards/gpt-4o-mini.md", dto.CardPath);
        Assert.Contains("tool_calls", dto.Capabilities);
        Assert.Contains("streaming", dto.Capabilities);

        // Snake-case keys per ADR-014 + boolean field values via JsonDocument
        // (see Test 4 note on record-positional deser).
        using var doc = JsonDocument.Parse(stdout);
        Assert.True(doc.RootElement.TryGetProperty("context_window", out var je6));
        Assert.True(doc.RootElement.TryGetProperty("card_path", out var je7));
        Assert.True(doc.RootElement.TryGetProperty("cost_tier", out var je8));
        Assert.True(doc.RootElement.GetProperty("default").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("allowlisted").GetBoolean());
    }

    // -- Test 9: `show` with no positional -> rc=2 -----------------------

    [Fact]
    public void Show_MissingPositional_ReturnsTwo()
    {
        using var _ = new RegistryScope(Entry("alpha-model"));
        using var __ = new EnvScope("AZUREOPENAIMODEL", null);
        using var cap = new ConsoleScope();

        var rc = AzureOpenAI_CLI.Cli.ModelsCommand.Run(new[] { "models", "show" });

        Assert.Equal(2, rc);
        Assert.Contains("[ERROR]", cap.Err.ToString(), StringComparison.Ordinal);
    }

    // -- Test 10: capabilities table is inverted-index -------------------

    [Fact]
    public void Capabilities_Table_InvertedIndex_HasCapabilityHeadersAndMembers()
    {
        using var _ = new RegistryScope(
            Entry("a", caps: new[] { "tool_calls" }),
            Entry("b", caps: new[] { "tool_calls", "streaming" }),
            Entry("c", caps: new[] { "streaming" }));
        using var __ = new EnvScope("AZUREOPENAIMODEL", null);
        using var cap = new ConsoleScope();

        var rc = AzureOpenAI_CLI.Cli.ModelsCommand.Run(new[] { "models", "capabilities" });

        Assert.Equal(0, rc);
        var stdout = cap.Out.ToString();
        Assert.Contains("Capability", stdout, StringComparison.Ordinal);
        Assert.Contains("Models", stdout, StringComparison.Ordinal);
        Assert.Contains("tool_calls", stdout, StringComparison.Ordinal);
        Assert.Contains("streaming", stdout, StringComparison.Ordinal);

        // tool_calls row must list a and b; streaming row must list b and c.
        var lines = stdout.Split('\n');
        var tcLine = Array.Find(lines, l => l.StartsWith("tool_calls", StringComparison.Ordinal));
        var stLine = Array.Find(lines, l => l.StartsWith("streaming", StringComparison.Ordinal));
        Assert.NotNull(tcLine);
        Assert.NotNull(stLine);
        Assert.Contains("a", tcLine!, StringComparison.Ordinal);
        Assert.Contains("b", tcLine, StringComparison.Ordinal);
        Assert.Contains("b", stLine!, StringComparison.Ordinal);
        Assert.Contains("c", stLine, StringComparison.Ordinal);
    }

    // -- Test 11: capabilities row caps at 5 + tail format ---------------

    [Fact]
    public void Capabilities_OverflowRow_HasFiveModelsPlusMoreTail()
    {
        // 7 streaming-capable models -> 5 head + "(2 more; see models list)".
        var entries = new[]
        {
            Entry("m1", caps: new[] { "streaming" }),
            Entry("m2", caps: new[] { "streaming" }),
            Entry("m3", caps: new[] { "streaming" }),
            Entry("m4", caps: new[] { "streaming" }),
            Entry("m5", caps: new[] { "streaming" }),
            Entry("m6", caps: new[] { "streaming" }),
            Entry("m7", caps: new[] { "streaming" }),
        };
        using var _ = new RegistryScope(entries);
        using var __ = new EnvScope("AZUREOPENAIMODEL", null);
        using var cap = new ConsoleScope();

        var rc = AzureOpenAI_CLI.Cli.ModelsCommand.Run(new[] { "models", "capabilities" });

        Assert.Equal(0, rc);
        var stdout = cap.Out.ToString();
        var lines = stdout.Split('\n');
        var stLine = Array.Find(lines, l => l.StartsWith("streaming", StringComparison.Ordinal));
        Assert.NotNull(stLine);
        // First 5 present.
        Assert.Contains("m1", stLine!, StringComparison.Ordinal);
        Assert.Contains("m5", stLine, StringComparison.Ordinal);
        // 6th and 7th NOT in the line body (they're folded into the tail count).
        Assert.True(stLine.IndexOf("m6", StringComparison.Ordinal) < 0,
            $"m6 should be folded into '(2 more; ...)' tail. line='{stLine}'");
        Assert.True(stLine.IndexOf("m7", StringComparison.Ordinal) < 0);
        // Exact tail format from ModelsCommand.cs:285.
        Assert.Contains("(2 more; see models list)", stLine, StringComparison.Ordinal);
    }

    // -- Test 12: capabilities --json is Dictionary<string,string[]> -----

    [Fact]
    public void Capabilities_Json_ParsesAsDictionary()
    {
        using var _ = new RegistryScope(
            Entry("a", caps: new[] { "tool_calls" }),
            Entry("b", caps: new[] { "tool_calls", "streaming" }));
        using var __ = new EnvScope("AZUREOPENAIMODEL", null);
        using var cap = new ConsoleScope();

        var rc = AzureOpenAI_CLI.Cli.ModelsCommand.Run(
            new[] { "models", "capabilities", "--json" });

        Assert.Equal(0, rc);
        var stdout = cap.Out.ToString().Trim();
        var dict = JsonSerializer.Deserialize(
            stdout, AppJsonContext.Default.DictionaryStringStringArray);
        Assert.NotNull(dict);
        Assert.True(dict!.ContainsKey("tool_calls"));
        Assert.True(dict.ContainsKey("streaming"));
        Assert.Equal(new[] { "a", "b" }, dict["tool_calls"]);
        Assert.Equal(new[] { "b" }, dict["streaming"]);
        // Capabilities with zero hits still appear (empty array).
        Assert.True(dict.ContainsKey("vision_in"));
        Assert.Empty(dict["vision_in"]);
    }

    // -- Test 13: CJK model name (Japanese kanji) appears in list --------

    [Fact]
    public void List_JapaneseKanjiName_AppearsInTableOutput()
    {
        // "gpt-<kanji nihongo>-test" -- ASCII source bytes per project rule.
        const string cjkName = "gpt-\u65E5\u672C\u8A9E-test";
        using var _ = new RegistryScope(
            Entry(cjkName),
            Entry("plain-latin"));
        using var __ = new EnvScope("AZUREOPENAIMODEL", null);
        using var cap = new ConsoleScope();

        var rc = AzureOpenAI_CLI.Cli.ModelsCommand.Run(new[] { "models", "list" });

        Assert.Equal(0, rc);
        var stdout = cap.Out.ToString();
        Assert.Contains(cjkName, stdout, StringComparison.Ordinal);
        Assert.Contains("plain-latin", stdout, StringComparison.Ordinal);
        // Babu's width helper should report CJK chars as wide (2 cells each).
        // This is the contract Mickey's TableRenderer relies on; verify it
        // independently of ModelsCommand's interim renderer.
        var w = AzureOpenAI_CLI.Localization.EastAsianWidth.MeasureDisplayWidth(cjkName);
        // 9 ASCII (gpt- + -test) at 1 + 3 kanji at 2 = 15. The interim
        // RenderTableInternal pads to string.Length = 12, so this 15-vs-12
        // delta is the FINDING-P-S04E04-01 misalignment quantum.
        Assert.Equal(15, w);
    }

    // -- Test 14: Hangul + Hiragana + Hanzi all appear -------------------

    [Fact]
    public void List_MixedCjkScripts_AllAppearInOutput()
    {
        // "han-\uD55C", "hira-\u3072\u3089\u304C\u306A", "hanzi-\u7B80\u4F53"
        const string hangul = "han-\uD55C";                    // han- + U+D55C (Hangul HAN)
        const string hira = "hira-\u3072\u3089\u304C\u306A";    // hira- + Hiragana HIRAGANA
        const string hanzi = "hanzi-\u7B80\u4F53";              // hanzi- + simplified Chinese
        using var _ = new RegistryScope(Entry(hangul), Entry(hira), Entry(hanzi));
        using var __ = new EnvScope("AZUREOPENAIMODEL", null);
        using var cap = new ConsoleScope();

        var rc = AzureOpenAI_CLI.Cli.ModelsCommand.Run(new[] { "models", "list" });

        Assert.Equal(0, rc);
        var stdout = cap.Out.ToString();
        Assert.Contains(hangul, stdout, StringComparison.Ordinal);
        Assert.Contains(hira, stdout, StringComparison.Ordinal);
        Assert.Contains(hanzi, stdout, StringComparison.Ordinal);
        // Babu's helper: each row name's display width is computable and >0.
        Assert.True(AzureOpenAI_CLI.Localization.EastAsianWidth.MeasureDisplayWidth(hangul) > "han-".Length);
        Assert.True(AzureOpenAI_CLI.Localization.EastAsianWidth.MeasureDisplayWidth(hira) > "hira-".Length);
        Assert.True(AzureOpenAI_CLI.Localization.EastAsianWidth.MeasureDisplayWidth(hanzi) > "hanzi-".Length);
    }

    // -- Test 15: Latin-only alignment regression ------------------------

    [Fact]
    public void List_LatinOnly_HeaderUnderlineAndBodyAligned()
    {
        using var _ = new RegistryScope(
            Entry("alpha"),
            Entry("a-much-longer-model-name"));
        using var __ = new EnvScope("AZUREOPENAIMODEL", null);
        using var cap = new ConsoleScope();

        var rc = AzureOpenAI_CLI.Cli.ModelsCommand.Run(new[] { "models", "list" });

        Assert.Equal(0, rc);
        var stdout = cap.Out.ToString();
        var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length >= 4, "expected header + underline + 2 body rows");
        // Header and underline have the same visual width (pure ASCII).
        var header = lines[0].TrimEnd('\r');
        var underline = lines[1].TrimEnd('\r');
        Assert.Equal(header.Length, underline.Length);
        // Body rows align under the header. TableRenderer invariant 10
        // strips trailing whitespace from every line, so the last column
        // may be shorter than the header overall width. Structural checks:
        //   1. Each body row starts with the model name at column 0
        //   2. No body row exceeds the header width (no overflow)
        //   3. Both body rows are well-formed lines (non-empty after trim)
        // See finding elaine-2026-05-MR-T15: Wave 2.5 wired TableRenderer,
        // exposing invariant 10 vs. this test's old absolute-width contract.
        var bodyA = Array.Find(lines, l => l.Contains("alpha", StringComparison.Ordinal))!.TrimEnd('\r');
        var bodyB = Array.Find(lines, l => l.Contains("a-much-longer-model-name", StringComparison.Ordinal))!.TrimEnd('\r');
        Assert.StartsWith("alpha", bodyA, StringComparison.Ordinal);
        Assert.StartsWith("a-much-longer-model-name", bodyB, StringComparison.Ordinal);
        Assert.True(bodyA.Length <= header.Length,
            "alpha row should not exceed header width: " + bodyA.Length + " vs " + header.Length);
        Assert.True(bodyB.Length <= header.Length,
            "long-name row should not exceed header width: " + bodyB.Length + " vs " + header.Length);
        Assert.True(bodyA.Length > "alpha".Length,
            "alpha row should have additional columns after the name");
    }

    // -- Test 16: empty/missing field renders 'unknown', never '-' or 'n/a' -

    [Fact]
    public void List_EmptyCapabilities_RendersUnknownNeverDashOrNa()
    {
        using var _ = new RegistryScope(
            new ModelRegistryEntry(
                Name: "naked",
                Provider: "azure",
                Capabilities: Array.Empty<string>(),
                ContextWindow: 0,
                CostTier: "",
                CardPath: null));
        using var __ = new EnvScope("AZUREOPENAIMODEL", null);
        using var cap = new ConsoleScope();

        var rc = AzureOpenAI_CLI.Cli.ModelsCommand.Run(new[] { "models", "list" });

        Assert.Equal(0, rc);
        var stdout = cap.Out.ToString();
        var line = Array.Find(stdout.Split('\n'), l => l.StartsWith("naked", StringComparison.Ordinal));
        Assert.NotNull(line);
        Assert.Contains("unknown", line!, StringComparison.Ordinal);
        // ADR-014: do not use "-" or "n/a" sentinels. (Hyphens only appear in
        // the header underline row, which we excluded above.)
        Assert.True(line!.IndexOf("n/a", StringComparison.OrdinalIgnoreCase) < 0,
            $"row must not contain 'n/a'. line='{line}'");
    }

    // -- Test 17: unknown subcommand -> rc=2 -----------------------------

    [Fact]
    public void UnknownSubcommand_ReturnsTwoAndWritesError()
    {
        using var _ = new RegistryScope(Entry("alpha-model"));
        using var __ = new EnvScope("AZUREOPENAIMODEL", null);
        using var cap = new ConsoleScope();

        var rc = AzureOpenAI_CLI.Cli.ModelsCommand.Run(new[] { "models", "bogus" });

        Assert.Equal(2, rc);
        var err = cap.Err.ToString();
        Assert.Contains("[ERROR]", err, StringComparison.Ordinal);
        Assert.Contains("bogus", err, StringComparison.Ordinal);
    }

    // -- Test 18: no subcommand -> rc=2 + stderr error -------------------

    [Fact]
    public void NoSubcommand_ReturnsTwoAndWritesError()
    {
        using var _ = new RegistryScope(Entry("alpha-model"));
        using var __ = new EnvScope("AZUREOPENAIMODEL", null);
        using var cap = new ConsoleScope();

        var rc = AzureOpenAI_CLI.Cli.ModelsCommand.Run(new[] { "models" });

        Assert.Equal(2, rc);
        var err = cap.Err.ToString();
        Assert.Contains("[ERROR]", err, StringComparison.Ordinal);
        Assert.Contains("subcommand", err, StringComparison.Ordinal);
    }

    // -- Test 19: --help on root -> rc=0 + usage to stdout ---------------

    [Fact]
    public void Help_RootFlag_ReturnsZeroAndPrintsUsage()
    {
        using var _ = new RegistryScope(Entry("alpha-model"));
        using var __ = new EnvScope("AZUREOPENAIMODEL", null);
        using var cap = new ConsoleScope();

        var rc = AzureOpenAI_CLI.Cli.ModelsCommand.Run(new[] { "models", "--help" });

        Assert.Equal(0, rc);
        var stdout = cap.Out.ToString();
        Assert.Contains("Usage", stdout, StringComparison.Ordinal);
        Assert.Contains("list", stdout, StringComparison.Ordinal);
        Assert.Contains("show", stdout, StringComparison.Ordinal);
        Assert.Contains("capabilities", stdout, StringComparison.Ordinal);
    }

    // -- Test 20: capability cap tail uses the exact format string -------

    [Fact]
    public void Capabilities_Cap_TailFormatString_ExactMatch()
    {
        // 8 streaming-capable -> "(3 more; see models list)".
        var entries = Enumerable.Range(1, 8)
            .Select(i => Entry($"sm{i:D2}", caps: new[] { "streaming" }))
            .ToArray();
        using var _ = new RegistryScope(entries);
        using var __ = new EnvScope("AZUREOPENAIMODEL", null);
        using var cap = new ConsoleScope();

        var rc = AzureOpenAI_CLI.Cli.ModelsCommand.Run(new[] { "models", "capabilities" });

        Assert.Equal(0, rc);
        var stdout = cap.Out.ToString();
        var line = Array.Find(stdout.Split('\n'),
            l => l.StartsWith("streaming", StringComparison.Ordinal));
        Assert.NotNull(line);
        Assert.Contains("(3 more; see models list)", line!, StringComparison.Ordinal);
    }

    // -- Test 21: ANSI hygiene -- no ESC bytes in any default output -----

    [Fact]
    public void List_DefaultOutput_NoAnsiEscapes()
    {
        using var _ = new RegistryScope(Entry("alpha-model"));
        using var __ = new EnvScope("AZUREOPENAIMODEL", null);
        using var cap = new ConsoleScope();

        var rc = AzureOpenAI_CLI.Cli.ModelsCommand.Run(new[] { "models", "list" });

        Assert.Equal(0, rc);
        // Use IndexOf, NOT Assert.DoesNotContain (xunit-empty-needle pitfall).
        Assert.True(cap.Out.ToString().IndexOf('\u001B') < 0,
            "default table output must be free of ANSI ESC bytes");
    }
}

// -------------------------------------------------------------------------
// FINDINGS (Puddy, S04E04 Wave 2)
// -------------------------------------------------------------------------
//
// FINDING-P-S04E04-01 (info):
//   ModelsCommand.RenderTableInternal (Cli/ModelsCommand.cs:438) still uses
//   string.Length for column widths -- it has not been swapped to Mickey's
//   Cli/TableRenderer.Render (which DOES delegate to Babu's
//   Localization/EastAsianWidth.MeasureDisplayWidth). Result: CJK model
//   names misalign in the table view by N columns where N = (display_width
//   - code_unit_length). The seam is marked with "// >>> Kramer" but a
//   matching "// >>> Mickey" swap was never performed in 457e06b. Verified
//   by Test 13 (helper returns 18 for "gpt-\u65E5\u672C\u8A9E-test" while
//   the interim renderer would pad to 15). Hand-off to Elaine for the
//   one-line wire-up; no behavior change for ASCII-only registries.
//
// FINDING-P-S04E04-02 (info):
//   The capabilities table emits zero-hit capabilities as rows with body
//   cell "unknown" (ModelsCommand.cs:275). That's correct per ADR-014, but
//   may surprise users who expect zero-hit rows to be hidden. Not a bug;
//   logged for Russell/Mickey UX review post-E04.
//
// FINDING-P-S04E04-03 (regression, pre-existing on main @ 8aec375):
//   DoctorRegistryTests.Doctor_Registry_TerminalInjectionPayload_ScrubbedToQuestionMarks
//   fails on `main` independent of this change (rc=99 vs expected rc=0). The
//   regression appears tied to Kramer's S04E04 W1 shell-hostile-name reject
//   (commit 3bd7f8d), which now rejects the injection-payload fixture at
//   registry load time rather than letting the doctor scrub it. The fixture
//   or the assertion needs to be updated to reflect the new fail-fast
//   contract. Not introduced by this commit; verified by running the test
//   in isolation against an untouched working tree. Hand-off to Kramer /
//   Puddy follow-up (NOT fixed here per Wave 2 single-file rule).
