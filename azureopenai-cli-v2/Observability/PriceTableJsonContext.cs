using System.Text.Json.Serialization;

namespace AzureOpenAI_CLI_V2.Observability;

/// <summary>
/// Price table entry for JSON deserialization (matches custom price table format).
/// Kramer audit M3: the source-generated JSON context for this type now lives
/// on the single app-wide <c>AppJsonContext</c>; this file intentionally only
/// declares the DTO.
/// </summary>
internal record PriceTableEntry(double InputPer1K, double OutputPer1K);
