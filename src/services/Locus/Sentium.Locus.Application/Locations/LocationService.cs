using Sentium.Locus.Core.Dtos;
using Sentium.Locus.Core.Locations;

namespace Sentium.Locus.Application.Locations;

public sealed class LocationService(ILocationManager manager) : ILocationService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<LocationDto>> GetLocationsAsync(CancellationToken ct = default)
        => manager.GetLocationsAsync(ct);

    /// <inheritdoc />
    public Task<LocationDto?> GetLocationAsync(Guid id, CancellationToken ct = default)
        => manager.GetLocationAsync(id, ct);

    /// <inheritdoc />
    public Task<IReadOnlyList<LocationDto>> GetSubLocationsAsync(Guid parentId, CancellationToken ct = default)
        => manager.GetSubLocationsAsync(parentId, ct);

    /// <inheritdoc />
    public async Task<LocationDto?> CreateLocationAsync(CreateLocationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await manager.NameExistsAsync(request.Name, request.ParentLocationId, ct: ct))
        {
            return null;
        }

        return await manager.CreateLocationAsync(request, ct);
    }

    /// <inheritdoc />
    public async Task<LocationDto?> UpdateLocationAsync(Guid id, UpdateLocationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!await manager.ExistsAsync(id, ct))
        {
            return null;
        }

        if (await manager.NameExistsAsync(request.Name, request.ParentLocationId, excludeId: id, ct: ct))
        {
            throw new InvalidOperationException($"A location named '{request.Name}' already exists under the same parent.");
        }

        return await manager.UpdateLocationAsync(id, request, ct);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteLocationAsync(Guid id, CancellationToken ct = default)
    {
        if (!await manager.ExistsAsync(id, ct))
        {
            return false;
        }

        await manager.DeleteLocationAsync(id, ct);
        return true;
    }
}
