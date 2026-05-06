using Sentium.Locus.Core.Dtos;

namespace Sentium.Locus.Core.Locations;

/// <summary>
/// Defines business-logic operations for managing locations in the home hierarchy.
/// </summary>
public interface ILocationService
{
    Task<IReadOnlyList<LocationDto>> GetLocationsAsync(CancellationToken ct = default);
    Task<LocationDto?> GetLocationAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<LocationDto>> GetSubLocationsAsync(Guid parentId, CancellationToken ct = default);

    /// <summary>
    /// Creates a new location. Returns <c>null</c> if a location with the same name
    /// already exists under the same parent.
    /// </summary>
    Task<LocationDto?> CreateLocationAsync(CreateLocationRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing location. Returns <c>null</c> if not found.
    /// Throws <see cref="InvalidOperationException"/> if the new name conflicts with a sibling.
    /// </summary>
    Task<LocationDto?> UpdateLocationAsync(Guid id, UpdateLocationRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes a location. Returns <c>false</c> if not found.
    /// </summary>
    Task<bool> DeleteLocationAsync(Guid id, CancellationToken ct = default);
}
