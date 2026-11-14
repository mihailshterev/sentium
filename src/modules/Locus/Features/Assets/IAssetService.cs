using Sentium.Core.Results;

namespace Sentium.Locus.Features.Assets;

public interface IAssetService
{
    Task<PagedResponse<AssetSummaryDto>> SearchAssetsAsync(AssetSearchQuery query, CancellationToken ct = default);

    Task<AssetDto?> GetAssetAsync(Guid id, CancellationToken ct = default);

    Task<Result<AssetDto>> CreateAssetAsync(SaveAssetRequest request, CancellationToken ct = default);

    Task<Result<AssetDto>> UpdateAssetAsync(Guid id, SaveAssetRequest request, CancellationToken ct = default);

    Task<bool> DeleteAssetAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Re-queues the asset's knowledge-base sync (e.g. after it was marked failed).
    /// </summary>
    Task<bool> RequestKnowledgeSyncAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<TagCountDto>> GetTagsAsync(string? prefix, CancellationToken ct = default);

    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken ct = default);
}
