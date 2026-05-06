using Sentium.Locus.Core.Dtos;

namespace Sentium.Locus.Core.Locations;

/// <summary>
/// Defines data-access operations for location entities within the Environmental Knowledge Base.
/// </summary>
public interface ILocationManager
{
    /// <summary>Retrieves all top-level and nested locations.</summary>
    Task<IReadOnlyList<LocationDto>> GetLocationsAsync(CancellationToken ct = default);

    /// <summary>Retrieves a single location by its unique identifier.</summary>
    Task<LocationDto?> GetLocationAsync(Guid id, CancellationToken ct = default);

    /// <summary>Retrieves all child locations for a given parent.</summary>
    Task<IReadOnlyList<LocationDto>> GetSubLocationsAsync(Guid parentId, CancellationToken ct = default);

    /// <summary>Checks if a location exists by ID.</summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Checks for name collisions within a specific level of the hierarchy.
    /// </summary>
    /// <param name="name">The name to check.</param>
    /// <param name="parentLocationId">The parent scope. Null implies the root level.</param>
    /// <param name="excludeId">An optional ID to ignore (used during updates).</param>
    Task<bool> NameExistsAsync(string name, Guid? parentLocationId, Guid? excludeId = null, CancellationToken ct = default);

    /// <summary>Persists a new location to the data store.</summary>
    Task<LocationDto> CreateLocationAsync(CreateLocationRequest request, CancellationToken ct = default);

    /// <summary>Updates an existing location's data.</summary>
    Task<LocationDto?> UpdateLocationAsync(Guid id, UpdateLocationRequest request, CancellationToken ct = default);

    /// <summary>Permanently removes a location from the data store.</summary>
    Task DeleteLocationAsync(Guid id, CancellationToken ct = default);
}
