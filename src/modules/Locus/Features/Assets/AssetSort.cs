using System.Text.Json.Serialization;

namespace Sentium.Locus.Features.Assets;

[JsonConverter(typeof(JsonStringEnumConverter<AssetSort>))]
public enum AssetSort
{
    Name,
    Updated,
    Created
}
