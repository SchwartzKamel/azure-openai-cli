using System.Text.Json.Serialization;

namespace AzureOpenAI_CLI.Eval;

/// <summary>One built-in corpus, for <c>--list-corpora --json</c>.</summary>
internal sealed record CorpusInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("fixtures")] int Fixtures,
    [property: JsonPropertyName("safety_fixtures")] int SafetyFixtures,
    [property: JsonPropertyName("bytes")] int Bytes);
