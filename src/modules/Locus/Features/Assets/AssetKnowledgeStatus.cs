using System.Text.Json.Serialization;

namespace Sentium.Locus.Features.Assets;

/// <summary>
/// Where an asset's knowledge-base copy stands.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AssetKnowledgeStatus>))]
internal enum AssetKnowledgeStatus
{
    /// <summary>
    /// The asset is private; assistants cannot see it.
    /// </summary>
    NotShared,

    /// <summary>
    /// A knowledge-base update (or removal) is queued.
    /// </summary>
    Pending,

    /// <summary>
    /// The knowledge base holds the current content.
    /// </summary>
    Synced,

    /// <summary>
    /// Syncing failed repeatedly; edit the asset or retry manually.
    /// </summary>
    Failed
}
