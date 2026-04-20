using System.Text.Json.Serialization;

namespace AzureOpenAI_CLI_V2.Observability;

/// <summary>
/// Price table entry for JSON deserialization (matches custom price table format).
/// </summary>
internal record PriceTableEntry(double InputPer1K, double OutputPer1K);

/// <summary>
/// Source-generated JSON context for AOT-safe deserialization.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(Dictionary<string, PriceTableEntry>))]
internal partial class PriceTableJsonContext : JsonSerializerContext { }
