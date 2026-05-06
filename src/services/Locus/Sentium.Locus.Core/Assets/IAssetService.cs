using Sentium.Locus.Core.Dtos;

namespace Sentium.Locus.Core.Assets;

/// <summary>
/// Defines business-logic operations for managing assets in the digital twin inventory.
/// </summary>
public interface IAssetService
{
    Task<IReadOnlyList<AssetDto>> GetAssetsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AssetDto>> GetAssetsByLocationAsync(Guid locationId, CancellationToken ct = default);
    Task<AssetDto?> GetAssetAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Creates a new asset. Returns <c>null</c> if the specified location does not exist.
    /// </summary>
    Task<AssetDto?> CreateAssetAsync(CreateAssetRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing asset. Returns <c>null</c> if the asset is not found.
    /// Throws <see cref="InvalidOperationException"/> if the target location does not exist.
    /// </summary>
    Task<AssetDto?> UpdateAssetAsync(Guid id, UpdateAssetRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes an asset. Returns <c>false</c> if not found.
    /// </summary>
    Task<bool> DeleteAssetAsync(Guid id, CancellationToken ct = default);
}
