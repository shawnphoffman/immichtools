using System.Text.Json.Serialization;

namespace ImmichTools.Json;

[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(RequestData.CreateStack))]
[JsonSerializable(typeof(RequestData.UpdateAsset))]
[JsonSerializable(typeof(ReplyData.Asset))]
[JsonSerializable(typeof(ReplyData.Asset[]))]
[JsonSerializable(typeof(ReplyData.ExifInfo))]
internal partial class SerializerContext : JsonSerializerContext
{
}
