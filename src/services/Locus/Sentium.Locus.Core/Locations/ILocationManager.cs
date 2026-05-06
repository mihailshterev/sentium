using Sentium.Locus.Core.Dtos;

namespace Sentium.Locus.Core.Locations;

/// <summary>
/// Defines data-access operations for <see cref="Entities.Location"/> entities.
/// </summary>
public interface ILocationManager
{
    Task<IReadOnlyList<LocationDto>> GetLocationsAsync(CancellationToken ct = default);
    Task<LocationDto?> GetLocationAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<LocationDto>> GetSubLocationsAsync(Guid parentId, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task<bool> NameExistsAsync(string name, Guid? parentLocationId, Guid? excludeId = null, CancellationToken ct = default);
    Task<LocationDto> CreateLocationAsync(CreateLocationRequest request, CancellationToken ct = default);
    Task<LocationDto?> UpdateLocationAsync(Guid id, UpdateLocationRequest request, CancellationToken ct = default);
    Task DeleteLocationAsync(Guid id, CancellationToken ct = default);
}
