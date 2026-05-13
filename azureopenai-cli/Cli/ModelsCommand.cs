using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using AzureOpenAI_CLI.Registry;

namespace AzureOpenAI_CLI.Cli;

// S04E04 -- Reading Room (Elaine, lead). The `az-ai models` sub-command
// surface. Three read-only sub-commands over the model registry:
//
//   az-ai models list
//   az-ai models show <name>
//   az-ai models capabilities
//
// All three honour --json (AOT-safe via AppJsonContext), --raw (no headers,
// no markers, no truncation), --no-color (and the NO_COLOR env var). Empty
// registry exits rc=0 with a single [INFO] line on stderr. Unknown model
// for `show` exits rc=2 with a single [ERROR] line on stderr.
//
// Dependencies (Wave 1, file-disjoint):
//   - Mickey  -- Cli/TableRenderer.cs (NEW). This file currently uses an
//                inline private renderer (RenderTable below) as a stand-in
//                so the build stays green until Mickey lands. When her
//                TableRenderer.Render(columns, rows, options) lands, swap
//                the two calls in `RenderTableInternal` to delegate.
//   - Babu    -- Localization/EastAsianWidth.cs. Consumed only transitively
//                through Mickey's renderer; this file does not call it
//                directly. The local stand-in uses string.Length, which is
//                ASCII-safe and acceptable for the embedded seed registry.
//                CJK / emoji widths land with Mickey.
//   - Kramer  -- Registry/ModelRegistry.cs additions (EnumerateInOrder,
//                TryFind, and the A11Y-CG-01 shell-hostile-name reject).
//                If those helpers have not landed when this file is built,
//                we fall through to Program.RegistryEntries directly (same
//                in-memory snapshot, identical semantics). When Kramer's
//                helpers land, the two `// >>> Kramer` markers below are
//                the only edits required to rewire.
//
// See docs/adr/ADR-014-output-formatting-standard.md for the column /
// marker / JSON / truncation / empty-cell conventions enforced here.

/// <summary>
/// Entry-point for the <c>az-ai models</c> sub-command tree (S04E04).
/// Read-only inspection of the model registry loaded at startup; no I/O
/// beyond what <see cref="ModelRegistry.Load"/> already did. Output paths
/// are table (default) or JSON (<c>--json</c>); <c>--raw</c> emits
/// header-less tuple lines for shell pipelines.
/// </summary>
internal static class ModelsCommand
{
    /// <summary>Hard cap on models-per-capability in the table view (F-EE-AR-09).</summary>
    private const int CapabilitiesRowCap = 5;

    /// <summary>Sentinel literal for missing-field rendering (S04E02 polish: not "(no card)").</summary>
    private const string Unknown = "unknown";

    /// <summary>Marker appended to the Default cell (ADR-014: ASCII word, not glyph).</summary>
    private const string DefaultMarker = "(default)";

    /// <summary>Marker appended to the Allowlisted cell (ADR-014: ASCII word, not glyph).</summary>
    private const string AllowMarker = "(allow)";

    /// <summary>
    /// Dispatch entry. <paramref name="args"/> is the full process argv
    /// (so <c>args[0]</c> is "models"). Returns the process exit code.
    /// </summary>
    public static int Run(string[] args)
    {
        // args[0] is "models"; subcommand at args[1].
        var opts = ParseFlags(args);
        if (opts.ShowHelp)
        {
            Console.Out.Write(HelpRoot);
            return 0;
        }
        return opts.Subcommand switch
        {
            "list" => RunList(opts),
            "show" => RunShow(opts),
            "capabilities" => RunCapabilities(opts),
            null or "" => FailUsage("models requires a subcommand: list | show | capabilities", opts.Json),
            _ => FailUsage($"unknown subcommand: '{opts.Subcommand}'", opts.Json),
        };
    }

    // -- subcommand: list ----------------------------------------------------

    private static int RunList(Options opts)
    {
        var entries = SortedEntries();
        if (entries.Count == 0)
        {
            // Acceptance criterion 10: stderr line, rc=0.
            Console.Error.WriteLine("[INFO] No models registered. See --doctor.");
            return 0;
        }

        var (defaultModel, allowed) = ModelAllowlist();

        if (opts.Json)
        {
            var rows = new ModelListEntryJson[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                rows[i] = new ModelListEntryJson(
                    e.Name,
                    e.Provider,
                    CanonicalOrder(e.Capabilities),
                    IsDefault(e.Name, defaultModel),
                    IsAllowlisted(e.Name, allowed));
            }
            WriteJson(rows, AppJsonContext.Default.ModelListEntryJsonArray);
            return 0;
        }

        if (opts.Raw)
        {
            // Tab-separated tuple lines: name <TAB> provider <TAB> caps,csv <TAB> default <TAB> allow
            foreach (var e in entries)
            {
                var caps = string.Join(",", CanonicalOrder(e.Capabilities));
                Console.Out.WriteLine(string.Join(
                    "\t",
                    e.Name,
                    e.Provider,
                    caps,
                    IsDefault(e.Name, defaultModel) ? "true" : "false",
                    IsAllowlisted(e.Name, allowed) ? "true" : "false"));
            }
            return 0;
        }

        var columns = new[] { "Name", "Provider", "Capabilities", "Default", "Allowlisted" };
        var rowsTbl = new List<string[]>(entries.Count);
        foreach (var e in entries)
        {
            var caps = string.Join(", ", CanonicalOrder(e.Capabilities));
            rowsTbl.Add(new[]
            {
                e.Name,
                e.Provider,
                caps.Length == 0 ? Unknown : caps,
                IsDefault(e.Name, defaultModel) ? DefaultMarker : "",
                IsAllowlisted(e.Name, allowed) ? AllowMarker : "",
            });
        }
        RenderTable(columns, rowsTbl);
        return 0;
    }

    // -- subcommand: show ----------------------------------------------------

    private static int RunShow(Options opts)
    {
        if (string.IsNullOrEmpty(opts.Positional))
        {
            return FailUsage("models show requires a model name", opts.Json);
        }

        if (!TryFindEntry(opts.Positional, out var entry) || entry is null)
        {
            // Acceptance criterion 6: rc=2 + single stderr line.
            Console.Error.WriteLine(
                $"[ERROR] model '{opts.Positional}' not found in registry. "
                + "Use 'az-ai models list' to see available models.");
            return 2;
        }

        var (defaultModel, allowed) = ModelAllowlist();
        var caps = CanonicalOrder(entry.Capabilities);
        var family = Unknown;        // Not on ModelRegistryEntry; reserved for E05.
        var modalities = Array.Empty<string>(); // Not on ModelRegistryEntry; reserved for E05.
        var cardPath = string.IsNullOrEmpty(entry.CardPath) ? Unknown : entry.CardPath!;
        var ctx = entry.ContextWindow > 0 ? entry.ContextWindow.ToString(System.Globalization.CultureInfo.InvariantCulture) : Unknown;
        var costTier = string.IsNullOrEmpty(entry.CostTier) ? Unknown : entry.CostTier;

        if (opts.Json)
        {
            var dto = new ModelShowJson(
                entry.Name,
                entry.Provider,
                family,
                caps,
                entry.ContextWindow > 0 ? entry.ContextWindow : null,
                modalities,
                cardPath == Unknown ? null : cardPath,
                costTier == Unknown ? null : costTier,
                IsDefault(entry.Name, defaultModel),
                IsAllowlisted(entry.Name, allowed));
            WriteJson(dto, AppJsonContext.Default.ModelShowJson);
            return 0;
        }

        if (opts.Raw)
        {
            // key<TAB>value, one per line.
            Console.Out.WriteLine($"name\t{entry.Name}");
            Console.Out.WriteLine($"provider\t{entry.Provider}");
            Console.Out.WriteLine($"family\t{family}");
            Console.Out.WriteLine($"capabilities\t{string.Join(",", caps)}");
            Console.Out.WriteLine($"context_window\t{ctx}");
            Console.Out.WriteLine($"modalities\t{(modalities.Length == 0 ? Unknown : string.Join(",", modalities))}");
            Console.Out.WriteLine($"card_path\t{cardPath}");
            Console.Out.WriteLine($"cost_tier\t{costTier}");
            Console.Out.WriteLine($"default\t{(IsDefault(entry.Name, defaultModel) ? "true" : "false")}");
            Console.Out.WriteLine($"allowlisted\t{(IsAllowlisted(entry.Name, allowed) ? "true" : "false")}");
            return 0;
        }

        Console.Out.WriteLine(entry.Name);
        Console.Out.WriteLine(new string('-', entry.Name.Length));
        Console.Out.WriteLine($"Provider       : {entry.Provider}");
        Console.Out.WriteLine($"Family         : {family}");
        Console.Out.WriteLine($"Capabilities   : {(caps.Length == 0 ? Unknown : string.Join(", ", caps))}");
        Console.Out.WriteLine($"Context Window : {ctx}");
        Console.Out.WriteLine($"Modalities     : {(modalities.Length == 0 ? Unknown : string.Join(", ", modalities))}");
        Console.Out.WriteLine($"Card Path      : {cardPath}");
        Console.Out.WriteLine($"Cost Tier      : {costTier}");
        Console.Out.WriteLine($"Default        : {(IsDefault(entry.Name, defaultModel) ? "yes " + DefaultMarker : "no")}");
        Console.Out.WriteLine($"Allowlisted    : {(IsAllowlisted(entry.Name, allowed) ? "yes " + AllowMarker : "no")}");
        return 0;
    }

    // -- subcommand: capabilities -------------------------------------------

    private static int RunCapabilities(Options opts)
    {
        var entries = SortedEntries();

        // Canonical capability order from ModelCapability.AllowedTags, sorted
        // ordinally for stability. ADR-014 freezes this order for the lifetime
        // of v2.x; E05 may add tags, never reorder or remove.
        var caps = ModelCapability.AllowedTags
            .OrderBy(t => t, StringComparer.Ordinal)
            .ToArray();

        // Build the inverted index once.
        var index = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var cap in caps)
        {
            var hits = entries
                .Where(e => e.Capabilities is not null
                    && Array.IndexOf(e.Capabilities, cap) >= 0)
                .Select(e => e.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();
            index[cap] = hits;
        }

        if (opts.Json)
        {
            WriteJson(index, AppJsonContext.Default.DictionaryStringStringArray);
            return 0;
        }

        if (opts.Raw)
        {
            foreach (var cap in caps)
            {
                Console.Out.WriteLine($"{cap}\t{string.Join(",", index[cap])}");
            }
            return 0;
        }

        // Table view. Cap each row at CapabilitiesRowCap and emit a tail.
        var rows = new List<string[]>(caps.Length);
        foreach (var cap in caps)
        {
            var models = index[cap];
            string cell;
            if (models.Length == 0)
            {
                cell = Unknown;
            }
            else if (models.Length <= CapabilitiesRowCap)
            {
                cell = string.Join(", ", models);
            }
            else
            {
                var head = string.Join(", ", models.Take(CapabilitiesRowCap));
                var more = models.Length - CapabilitiesRowCap;
                cell = $"{head} ({more} more; see models list)";
            }
            rows.Add(new[] { cap, cell });
        }
        RenderTable(new[] { "Capability", "Models" }, rows);
        return 0;
    }

    // -- registry helpers ---------------------------------------------------

    // >>> Kramer (S04E04 Wave 1): replace this body with
    //     ModelRegistry.EnumerateInOrder().OrderBy(...).ToList() once
    //     the helper lands on main. The semantics are identical -- this
    //     snapshot reads the same in-memory array that EnumerateInOrder
    //     will expose. See ADR-014 backlog.
    private static IReadOnlyList<ModelRegistryEntry> SortedEntries()
    {
        var entries = Program.RegistryEntries;
        if (entries is null || entries.Length == 0)
            return Array.Empty<ModelRegistryEntry>();

        // Acceptance criterion 9: alphabetical by Name (ordinal), ties broken
        // by registration order. OrderBy is a stable sort in .NET, so the
        // original index implicitly tie-breaks without an extra key.
        return entries
            .OrderBy(e => e.Name, StringComparer.Ordinal)
            .ToList();
    }

    // >>> Kramer (S04E04 Wave 1): replace this with
    //     ModelRegistry.TryFind(name, out entry) once it lands. Same
    //     semantics: ordinal name match, false on missing-registry / empty
    //     name / no-match. See ADR-014 backlog.
    private static bool TryFindEntry(string name, out ModelRegistryEntry? entry)
    {
        entry = null;
        if (string.IsNullOrEmpty(name)) return false;
        var entries = Program.RegistryEntries;
        if (entries is null) return false;
        foreach (var e in entries)
        {
            if (string.Equals(e.Name, name, StringComparison.Ordinal))
            {
                entry = e;
                return true;
            }
        }
        return false;
    }

    private static (string? DefaultModel, HashSet<string>? Allowed) ModelAllowlist()
    {
        // ParseModelEnv lives in Program.cs and is the canonical source for
        // the AZUREOPENAIMODEL split. Reusing it keeps "what is default" and
        // "what is allowlisted" consistent with the resolver in BuildChatClient.
        var (def, allowed) = Program.ParseModelEnv();
        return (def, allowed);
    }

    private static bool IsDefault(string name, string? defaultModel)
        => !string.IsNullOrEmpty(defaultModel)
           && string.Equals(name, defaultModel, StringComparison.OrdinalIgnoreCase);

    private static bool IsAllowlisted(string name, HashSet<string>? allowed)
        => allowed is not null && allowed.Contains(name);

    private static string[] CanonicalOrder(string[]? caps)
    {
        if (caps is null || caps.Length == 0) return Array.Empty<string>();
        var arr = (string[])caps.Clone();
        Array.Sort(arr, StringComparer.Ordinal);
        return arr;
    }

    // -- flag parsing --------------------------------------------------------

    private sealed class Options
    {
        public string? Subcommand;
        public string? Positional;
        public bool Json;
        public bool Raw;
        public bool NoColor;
        public bool ShowHelp;
    }

    private static Options ParseFlags(string[] args)
    {
        var opts = new Options();
        // args[0] == "models"; start at 1.
        for (int i = 1; i < args.Length; i++)
        {
            var a = args[i];
            switch (a)
            {
                case "--json":
                    opts.Json = true;
                    break;
                case "--raw":
                    opts.Raw = true;
                    break;
                case "--no-color":
                    opts.NoColor = true;
                    break;
                case "--help":
                case "-h":
                case "help":
                    opts.ShowHelp = true;
                    break;
                default:
                    if (a.StartsWith('-'))
                    {
                        // Unknown flag -- defer to the dispatch to emit the error
                        // with the standard prefix. Stash as a sentinel.
                        opts.Subcommand ??= a;
                        break;
                    }
                    if (opts.Subcommand is null)
                    {
                        opts.Subcommand = a;
                    }
                    else if (opts.Positional is null)
                    {
                        opts.Positional = a;
                    }
                    break;
            }
        }
        // NO_COLOR env honoured even when --no-color is absent.
        if (!opts.NoColor)
        {
            var nc = Environment.GetEnvironmentVariable("NO_COLOR");
            if (!string.IsNullOrEmpty(nc)) opts.NoColor = true;
        }
        return opts;
    }

    // -- output: JSON --------------------------------------------------------

    private static void WriteJson<T>(T value, JsonTypeInfo<T> typeInfo)
    {
        var json = JsonSerializer.Serialize(value, typeInfo);
        Console.Out.WriteLine(json);
    }

    // -- output: table (Mickey's TableRenderer + Babu's EastAsianWidth) ------

    // S04E04 W2.5 (Elaine, A11Y-MR-03): inline stub deleted. We now delegate
    // to Cli.TableRenderer.Render, which measures display width via Babu's
    // EastAsianWidth helper so CJK / combining-mark / ZWJ cells align. The
    // call site builds Mickey's Column / RenderOptions records from this
    // file's local string-array column model; no public API additions.
    private static void RenderTable(string[] headers, IReadOnlyList<string[]> rows)
    {
        var columns = new TableRenderer.Column[headers.Length];
        for (int i = 0; i < headers.Length; i++)
        {
            columns[i] = new TableRenderer.Column(headers[i]);
        }
        var options = new TableRenderer.RenderOptions();
        Console.Out.WriteLine(TableRenderer.Render(columns, rows, options));
    }

    // -- failure paths -------------------------------------------------------

    private static int FailUsage(string msg, bool json)
    {
        if (json)
        {
            var err = new ErrorJsonResponse(true, msg, 2);
            Console.Error.WriteLine(JsonSerializer.Serialize(err, AppJsonContext.Default.ErrorJsonResponse));
        }
        else
        {
            Console.Error.WriteLine($"[ERROR] {msg}. Run 'az-ai models --help'.");
        }
        return 2;
    }

    // -- help text (Bookman-tier brevity; Elaine self-reviewed) -------------

    private const string HelpRoot =
@"az-ai models -- inspect the model registry

Usage:
  az-ai models <subcommand> [--json] [--raw] [--no-color]

Subcommands:
  list           Tabular index of registered models.
  show <name>    Full card view for one model.
  capabilities   Inverted index: capability -> models.

Flags:
  --json         Machine-readable output (AOT-safe, stable shape).
  --raw          Header-less tuples; no markers, no truncation.
  --no-color     Disable colour (NO_COLOR env is also honoured).

Examples:
  az-ai models list
  az-ai models show gpt-4o-mini --json
  az-ai models capabilities

See docs/adr/ADR-014-output-formatting-standard.md for the full output contract.
";
}

// -- AOT-safe JSON DTOs for `az-ai models` (registered in AppJsonContext) ---

/// <summary>
/// One row in <c>az-ai models list --json</c>. Field order matches the
/// table column order. <c>default</c> and <c>allowlisted</c> are booleans
/// (not strings) so <c>jq</c> can filter without parsing markers.
/// </summary>
internal record ModelListEntryJson(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("capabilities")] string[] Capabilities,
    [property: JsonPropertyName("default")] bool Default,
    [property: JsonPropertyName("allowlisted")] bool Allowlisted
);

/// <summary>
/// Single-object payload for <c>az-ai models show &lt;name&gt; --json</c>.
/// Keys follow snake_case per ADR-014 (criterion 5 in the S04E04 brief).
/// Optional fields are null-skipped on serialize.
/// </summary>
internal record ModelShowJson(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("family")] string Family,
    [property: JsonPropertyName("capabilities")] string[] Capabilities,
    [property: JsonPropertyName("context_window")] int? ContextWindow,
    [property: JsonPropertyName("modalities")] string[] Modalities,
    [property: JsonPropertyName("card_path")] string? CardPath,
    [property: JsonPropertyName("cost_tier")] string? CostTier,
    [property: JsonPropertyName("default")] bool Default,
    [property: JsonPropertyName("allowlisted")] bool Allowlisted
);
