using Sentium.Locus.Core.Assets;
using Sentium.Locus.Core.Dtos;
using Sentium.Locus.Core.Ingestion;
using Sentium.Locus.Core.Locations;

namespace Sentium.Locus.Application.Assets;

public sealed class AssetService(
    IAssetManager assetManager,
    ILocationManager locationManager,
    IAgentIngestionClient ingestionClient) : IAssetService
{
    public Task<IReadOnlyList<AssetDto>> GetAssetsAsync(CancellationToken ct = default)
        => assetManager.GetAssetsAsync(ct);

    public Task<IReadOnlyList<AssetDto>> GetAssetsByLocationAsync(Guid locationId, CancellationToken ct = default)
        => assetManager.GetAssetsByLocationAsync(locationId, ct);

    public Task<AssetDto?> GetAssetAsync(Guid id, CancellationToken ct = default)
        => assetManager.GetAssetAsync(id, ct);

    public async Task<AssetDto?> CreateAssetAsync(CreateAssetRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!await locationManager.ExistsAsync(request.LocationId, ct))
        {
            return null;
        }

        var asset = await assetManager.CreateAssetAsync(request, ct);

        if (asset.IsAgentAccessible)
        {
            await ingestionClient.IngestAssetAsync(asset, ct);
        }

        return asset;
    }

    public async Task<AssetDto?> UpdateAssetAsync(Guid id, UpdateAssetRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!await assetManager.ExistsAsync(id, ct))
        {
            return null;
        }

        if (!await locationManager.ExistsAsync(request.LocationId, ct))
        {
            throw new InvalidOperationException($"Location '{request.LocationId}' does not exist.");
        }

        var asset = await assetManager.UpdateAssetAsync(id, request, ct);
        if (asset is null)
        {
            return null;
        }

        if (asset.IsAgentAccessible)
        {
            await ingestionClient.IngestAssetAsync(asset, ct);
        }
        else
        {
            await ingestionClient.RemoveAssetAsync(id, ct);
        }

        return asset;
    }

    public async Task<bool> DeleteAssetAsync(Guid id, CancellationToken ct = default)
    {
        if (!await assetManager.ExistsAsync(id, ct))
        {
            return false;
        }

        await assetManager.DeleteAssetAsync(id, ct);
        await ingestionClient.RemoveAssetAsync(id, ct);
        return true;
    }
}
