using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzureOpenAI_CLI;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(UserConfig))]
public partial class UserConfigJsonContext : JsonSerializerContext
{
}
