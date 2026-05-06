using Sentium.Locus.Core.Dtos;
using Sentium.Locus.Core.Entities;
using Sentium.Locus.Core.Locations;
using Sentium.Locus.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Sentium.Locus.Infrastructure.Locations;

public sealed class LocationManager(LocusDbContext context) : ILocationManager
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<LocationDto>> GetLocationsAsync(CancellationToken ct = default)
    {
        return await context.Locations
            .AsNoTracking()
            .OrderBy(l => l.Name)
            .Select(l => new LocationDto(
                l.Id,
                l.Name,
                l.Description,
                l.AccessNotes,
                l.ParentLocationId,
                l.Assets.Count,
                l.SubLocations.Count,
                l.CreatedAt,
                l.UpdatedAt))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<LocationDto?> GetLocationAsync(Guid id, CancellationToken ct = default)
    {
        return await context.Locations
            .AsNoTracking()
            .Where(l => l.Id == id)
            .Select(l => new LocationDto(
                l.Id,
                l.Name,
                l.Description,
                l.AccessNotes,
                l.ParentLocationId,
                l.Assets.Count,
                l.SubLocations.Count,
                l.CreatedAt,
                l.UpdatedAt))
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LocationDto>> GetSubLocationsAsync(Guid parentId, CancellationToken ct = default)
    {
        return await context.Locations
            .AsNoTracking()
            .Where(l => l.ParentLocationId == parentId)
            .OrderBy(l => l.Name)
            .Select(l => new LocationDto(
                l.Id,
                l.Name,
                l.Description,
                l.AccessNotes,
                l.ParentLocationId,
                l.Assets.Count,
                l.SubLocations.Count,
                l.CreatedAt,
                l.UpdatedAt))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => await context.Locations.AnyAsync(l => l.Id == id, ct);

    /// <inheritdoc />
    public async Task<bool> NameExistsAsync(string name, Guid? parentLocationId, Guid? excludeId = null, CancellationToken ct = default)
    {
        var query = context.Locations.AsNoTracking()
            .Where(l => l.Name == name && l.ParentLocationId == parentLocationId);

        if (excludeId.HasValue)
        {
            query = query.Where(l => l.Id != excludeId.Value);
        }

        return await query.AnyAsync(ct);
    }

    /// <inheritdoc />
    public async Task<LocationDto> CreateLocationAsync(CreateLocationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = DateTime.UtcNow;
        var location = new Location
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            AccessNotes = request.AccessNotes,
            ParentLocationId = request.ParentLocationId,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.Locations.Add(location);
        await context.SaveChangesAsync(ct);

        return new LocationDto(
            location.Id,
            location.Name,
            location.Description,
            location.AccessNotes,
            location.ParentLocationId,
            AssetCount: 0,
            SubLocationCount: 0,
            location.CreatedAt,
            location.UpdatedAt);
    }

    /// <inheritdoc />
    public async Task<LocationDto?> UpdateLocationAsync(Guid id, UpdateLocationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var location = await context.Locations.FindAsync([id], ct);
        if (location is null)
        {
            return null;
        }

        location.Name = request.Name;
        location.Description = request.Description;
        location.AccessNotes = request.AccessNotes;
        location.ParentLocationId = request.ParentLocationId;
        location.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(ct);

        var assetCount = await context.Assets.CountAsync(a => a.LocationId == id, ct);
        var subCount = await context.Locations.CountAsync(l => l.ParentLocationId == id, ct);

        return new LocationDto(
            location.Id,
            location.Name,
            location.Description,
            location.AccessNotes,
            location.ParentLocationId,
            assetCount,
            subCount,
            location.CreatedAt,
            location.UpdatedAt);
    }

    /// <inheritdoc />
    public async Task DeleteLocationAsync(Guid id, CancellationToken ct = default)
    {
        await context.Locations.Where(l => l.Id == id).ExecuteDeleteAsync(ct);
    }
}
