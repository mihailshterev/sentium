using System.Text.Json.Serialization;

namespace Sentium.Memoria.Contracts.Models;

[JsonConverter(typeof(JsonStringEnumConverter<VaultRelation>))]
public enum VaultRelation
{
    LinksTo,
    LinkedFrom,

    /// <summary>
    /// Not linked directly, but both notes link to or from the same notes.
    /// </summary>
    SharesLinks,

    /// <summary>
    /// Semantically close by embedding, whether linked or not.
    /// </summary>
    Similar
}
