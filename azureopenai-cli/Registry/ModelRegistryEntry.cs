namespace AzureOpenAI_CLI.Registry;

// S04E01 -- The Registry (Kramer). Typed metadata for a single model.
// Field names use the CamelCase JsonNamingPolicy wired into AppJsonContext so
// they serialize to the exact JSON property names the brief requires:
//   Name          -> "name"
//   Provider      -> "provider"
//   Capabilities  -> "capabilities"
//   ContextWindow -> "contextWindow"
//   CostTier      -> "costTier"
//   CardPath      -> "cardPath"
//
// AOT safety: registered in AppJsonContext; source gen uses the constructor
// for deserialization. DynamicDependency guard is on ModelRegistry.Load().

/// <summary>
/// Metadata record for a single model known to the registry.
/// All fields are populated from <c>registry.json</c> (embedded seed or user
/// override). <c>CardPath</c> is optional; its absence triggers a WARN at
/// load time but never a fatal error.
/// </summary>
internal sealed record ModelRegistryEntry(
    string Name,
    string Provider,
    string[] Capabilities,
    int ContextWindow,
    string CostTier,
    string? CardPath);
