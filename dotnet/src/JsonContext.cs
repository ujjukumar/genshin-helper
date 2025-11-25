using System.Text.Json.Serialization;

namespace AutoSkipper;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ConfigData))]
internal partial class JsonContext : JsonSerializerContext
{
}
