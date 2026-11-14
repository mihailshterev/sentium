using Sentium.Core.Results;

namespace Sentium.Locus.Features.Locations;

internal interface ILocationService
{
    Task<IReadOnlyList<LocationDto>> GetLocationsAsync(CancellationToken ct = default);

    Task<LocationDto?> GetLocationAsync(Guid id, CancellationToken ct = default);

    Task<Result<LocationDto>> CreateLocationAsync(SaveLocationRequest request, CancellationToken ct = default);

    Task<Result<LocationDto>> UpdateLocationAsync(Guid id, SaveLocationRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes an empty location, or - with <paramref name="moveContentsTo"/> - first moves its direct
    /// sub-locations and assets there. Whole subtrees are never cascade-deleted.
    /// </summary>
    Task<LocationDeleteResult> DeleteLocationAsync(Guid id, Guid? moveContentsTo, CancellationToken ct = default);
}
