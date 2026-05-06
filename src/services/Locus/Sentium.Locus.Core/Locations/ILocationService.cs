using Sentium.Locus.Core.Dtos;

namespace Sentium.Locus.Core.Locations;

/// <summary>
/// Orchestrates location-based business logic and validation rules.
/// </summary>
public interface ILocationService
{
    /// <inheritdoc cref="ILocationManager.GetLocationsAsync"/>
    Task<IReadOnlyList<LocationDto>> GetLocationsAsync(CancellationToken ct = default);

    /// <inheritdoc cref="ILocationManager.GetLocationAsync"/>
    Task<LocationDto?> GetLocationAsync(Guid id, CancellationToken ct = default);

    /// <inheritdoc cref="ILocationManager.GetSubLocationsAsync"/>
    Task<IReadOnlyList<LocationDto>> GetSubLocationsAsync(Guid parentId, CancellationToken ct = default);

    /// <summary>
    /// Creates a new location if the name is unique within the specified parent scope.
    /// </summary>
    /// <returns>The created location, or <c>null</c> if a name collision occurs.</returns>
    Task<LocationDto?> CreateLocationAsync(CreateLocationRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing location.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the new name conflicts with an existing sibling location.</exception>
    Task<LocationDto?> UpdateLocationAsync(Guid id, UpdateLocationRequest request, CancellationToken ct = default);

    /// <summary>
    /// Removes a location from the system.
    /// </summary>
    /// <returns><c>true</c> if the location was found and deleted; otherwise <c>false</c>.</returns>
    Task<bool> DeleteLocationAsync(Guid id, CancellationToken ct = default);
}
