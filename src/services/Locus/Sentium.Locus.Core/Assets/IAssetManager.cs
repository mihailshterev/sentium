using Sentium.Locus.Core.Dtos;

namespace Sentium.Locus.Core.Assets;

/// <summary>
/// Defines data-access operations for <see cref="Entities.Asset"/> entities.
/// </summary>
public interface IAssetManager
{
    Task<IReadOnlyList<AssetDto>> GetAssetsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AssetDto>> GetAssetsByLocationAsync(Guid locationId, CancellationToken ct = default);
    Task<AssetDto?> GetAssetAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task<AssetDto> CreateAssetAsync(CreateAssetRequest request, CancellationToken ct = default);
    Task<AssetDto?> UpdateAssetAsync(Guid id, UpdateAssetRequest request, CancellationToken ct = default);
    Task DeleteAssetAsync(Guid id, CancellationToken ct = default);
}
