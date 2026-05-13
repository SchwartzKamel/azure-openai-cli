using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace AzureOpenAI_CLI.Registry;

// S04E01 -- The Registry (Kramer). Static loader for ModelRegistryEntry[].
//
// Load order:
//   1. Embedded registry.json (seed -- always present).
//   2. ~/.config/az-ai/registry.json -- if present, REPLACES the seed list
//      (override semantics; no merge). Documented in ADR-012.
//
// Validation: every entry's capabilities must be a subset of
// ModelCapability.AllowedTags. An unknown tag causes rc=99 and an [ERROR]
// message naming both the tag and the entry. Missing cardPath is a WARN
// (to stderr unless isRaw) but never fatal.
//
// cardPath resolution: paths are relative to AppContext.BaseDirectory so
// the binary can run from any CWD and still resolve cards correctly
// (see brief Risk row 3 / ADR-012).
//
// AOT safety: DynamicDependency on Load() pins ModelRegistryEntry so the
// ILC linker cannot trim it even though the only reach is via JSON
// deserialization (brief Risk row 6).

/// <summary>
/// Static loader for the model registry. Call <see cref="Load"/> once at
/// startup; the result is stored in <c>Program.RegistryEntries</c>.
/// </summary>
internal static class ModelRegistry
{
    private const string EmbeddedResourceName =
        "AzureOpenAI_CLI.Registry.registry.json";

    /// <summary>
    /// Load and validate the registry. Reads the embedded seed first; if
    /// <c>~/.config/az-ai/registry.json</c> exists it replaces the seed
    /// list entirely (no merge). Unknown capability tags cause rc=99.
    /// </summary>
    // DynamicDependency: pins ModelRegistryEntry against ILC trimming.
    // Only constructors + properties are needed for JSON source-gen deserialization.
    // Using All here adds ~89 KB to the AOT binary; scoped access keeps the delta small.
    [DynamicDependency(
        DynamicallyAccessedMemberTypes.PublicConstructors
        | DynamicallyAccessedMemberTypes.PublicProperties,
        typeof(ModelRegistryEntry))]
    public static ModelRegistryEntry[] Load(bool isRaw = false)
    {
        var entries = LoadEmbedded();
        entries = ApplyUserOverride(entries, isRaw);
        ValidateEntries(entries, isRaw);
        return entries;
    }

    // -- private helpers -------------------------------------------------

    private static ModelRegistryEntry[] LoadEmbedded()
    {
        var assembly = typeof(ModelRegistry).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName);
        if (stream is null)
        {
            Console.Error.WriteLine(
                $"[ERROR] Embedded resource '{EmbeddedResourceName}' not found in assembly.");
            Environment.Exit(99);
            return []; // unreachable; satisfies compiler
        }

        using var reader = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8);
        var json = reader.ReadToEnd();
        var result = JsonSerializer.Deserialize(json, AppJsonContext.Default.ModelRegistryEntryArray);
        return result ?? [];
    }

    private static ModelRegistryEntry[] ApplyUserOverride(
        ModelRegistryEntry[] seed, bool isRaw)
    {
        var userPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "az-ai", "registry.json");

        if (!File.Exists(userPath))
            return seed;

        try
        {
            var json = File.ReadAllText(userPath);
            var overrideEntries = JsonSerializer.Deserialize(
                json, AppJsonContext.Default.ModelRegistryEntryArray);
            return overrideEntries ?? seed;
        }
        catch (Exception ex)
        {
            if (!isRaw)
                Console.Error.WriteLine(
                    $"[WARN] Failed to load user registry override '{userPath}': {ex.Message}");
            return seed;
        }
    }

    private static void ValidateEntries(ModelRegistryEntry[] entries, bool isRaw)
    {
        foreach (var entry in entries)
        {
            // Validate capability tags -- unknown tag is fatal (rc=99).
            foreach (var tag in entry.Capabilities ?? [])
            {
                if (!ModelCapability.IsValid(tag))
                {
                    Console.Error.WriteLine(
                        $"[ERROR] Unknown capability tag '{tag}' in registry entry '{entry.Name}'. "
                        + "Allowed tags: "
                        + string.Join(", ", ModelCapability.AllowedTags)
                        + ". rc=99.");
                    Environment.Exit(99);
                    return; // unreachable; satisfies compiler
                }
            }

            // Missing cardPath is a warning, not fatal.
            if (string.IsNullOrEmpty(entry.CardPath) && !isRaw)
            {
                Console.Error.WriteLine(
                    $"[WARN] Registry entry '{entry.Name}' has no cardPath.");
            }
        }
    }
}
