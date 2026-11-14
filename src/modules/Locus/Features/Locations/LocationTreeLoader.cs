using Microsoft.EntityFrameworkCore;
using Sentium.Locus.Data;

namespace Sentium.Locus.Features.Locations;

internal static class LocationTreeLoader
{
    public static async Task<LocationTree> LoadLocationTreeAsync(this LocusDbContext db, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        var nodes = await db.Locations
            .AsNoTracking()
            .Select(l => new LocationNode(l.Id, l.ParentLocationId, l.Name))
            .ToListAsync(ct);

        return new LocationTree(nodes);
    }
}
