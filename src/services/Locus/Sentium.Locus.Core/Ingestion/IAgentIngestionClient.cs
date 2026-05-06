using Sentium.Locus.Core.Dtos;

namespace Sentium.Locus.Core.Ingestion;

/// <summary>
/// Sends asset data to the AgentRuntime knowledge base so agents can reference
/// inventory context when answering questions or running workflows.
/// </summary>
public interface IAgentIngestionClient
{
    /// <summary>
    /// Ingests (or re-ingests) an asset into the vector knowledge base.
    /// Only called when <see cref="AssetDto.IsAgentAccessible"/> is <c>true</c>.
    /// </summary>
    Task IngestAssetAsync(AssetDto asset, CancellationToken ct = default);

    /// <summary>
    /// Removes all knowledge-base vectors associated with the given asset.
    /// Called on delete or when <see cref="AssetDto.IsAgentAccessible"/> is toggled to <c>false</c>.
    /// </summary>
    Task RemoveAssetAsync(Guid assetId, CancellationToken ct = default);
}
