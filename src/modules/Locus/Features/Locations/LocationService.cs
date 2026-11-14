using Microsoft.EntityFrameworkCore;
using Sentium.Core.Results;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Security;
using Sentium.Locus.Common;
using Sentium.Locus.Data;
using Sentium.Locus.Entities;
using Sentium.Locus.Features.Knowledge;

namespace Sentium.Locus.Features.Locations;

/// <summary>
/// Location tree maintenance. Every structural change runs in a transaction that first takes one
/// transaction-scoped advisory lock, so two concurrent moves can never combine into a cycle; the tree is
/// only read after the lock is held.
/// </summary>
public sealed class LocationService(
    LocusDbContext db,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    KnowledgeSyncOutbox outbox,
    KnowledgeSyncSignal signal) : ILocationService
{
    /// <summary>
    /// Arbitrary constant identifying the Locus tree lock among the database's advisory locks.
    /// </summary>
    private const long TreeLockKey = 0x4C6F_6375_7354_7265; // "LocusTre"

    public async Task<IReadOnlyList<LocationDto>> GetLocationsAsync(CancellationToken ct = default)
    {
        var locations = await db.Locations.AsNoTracking().ToListAsync(ct);

        var assetCounts = await db.Assets
            .AsNoTracking()
            .GroupBy(a => a.LocationId)
            .Select(g => new { LocationId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.LocationId, x => x.Count, ct);

        var tree = new LocationTree(locations.Select(l => new LocationNode(l.Id, l.ParentLocationId, l.Name)));

        return [.. locations
            .Select(l => ToDto(l, tree, assetCounts))
            .OrderBy(d => d.Path, StringComparer.OrdinalIgnoreCase)];
    }

    public async Task<LocationDto?> GetLocationAsync(Guid id, CancellationToken ct = default)
        => (await GetLocationsAsync(ct)).FirstOrDefault(l => l.Id == id);

    public async Task<Result<LocationDto>> CreateLocationAsync(SaveLocationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = LocationNames.Clean(request.Name);
        Result<Guid> result;

        try
        {
            result = await db.Database.ExecuteInTransactionAsync(() => InsertLocationAsync(request, name, ct), ct: ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation(LocusDbContext.SiblingNameIndex))
        {
            return Result<LocationDto>.Conflict(DuplicateNameError(name));
        }

        return result.IsSuccess
            ? Result<LocationDto>.Success((await GetLocationAsync(result.Value, ct))!)
            : result.Propagate<Guid, LocationDto>();
    }

    public async Task<Result<LocationDto>> UpdateLocationAsync(Guid id, SaveLocationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = LocationNames.Clean(request.Name);
        Result<bool> result;

        try
        {
            result = await db.Database.ExecuteInTransactionAsync(() => SaveLocationChangesAsync(id, request, name, ct), ct: ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation(LocusDbContext.SiblingNameIndex))
        {
            return Result<LocationDto>.Conflict(DuplicateNameError(name));
        }

        if (!result.IsSuccess)
        {
            return result.Propagate<bool, LocationDto>();
        }

        if (result.Value)
        {
            signal.Notify();
        }

        return Result<LocationDto>.Success((await GetLocationAsync(id, ct))!);
    }

    public async Task<LocationDeleteResult> DeleteLocationAsync(Guid id, Guid? moveContentsTo, CancellationToken ct = default)
    {
        var (outcome, queued) = await db.Database.ExecuteInTransactionAsync(() => RemoveLocationAsync(id, moveContentsTo, ct), ct: ct);

        if (queued)
        {
            signal.Notify();
        }

        return outcome;
    }

    private async Task<Result<Guid>> InsertLocationAsync(SaveLocationRequest request, string name, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await LockTreeAsync(ct);
        var tree = await db.LoadLocationTreeAsync(ct);

        if (request.ParentLocationId is { } parentId && !tree.Contains(parentId))
        {
            return Result<Guid>.Invalid($"Parent location '{parentId}' does not exist.");
        }

        if (tree.ValidatePlacement(null, request.ParentLocationId, LocusLimits.MaxLocationDepth) is { } error)
        {
            return Result<Guid>.Conflict(error);
        }

        var now = timeProvider.GetUtcNow();
        var location = new Location
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            NormalizedName = LocationNames.Normalize(name),
            Description = Blank(request.Description),
            AccessNotes = Blank(request.AccessNotes),
            ParentLocationId = request.ParentLocationId,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = currentUser.UserName,
            UpdatedBy = currentUser.UserName
        };

        db.Locations.Add(location);
        await db.SaveChangesAsync(ct);

        return Result<Guid>.Success(location.Id);
    }

    /// <summary>
    /// Applies the request to the location; the result value tells whether knowledge syncs were queued.
    /// </summary>
    private async Task<Result<bool>> SaveLocationChangesAsync(Guid id, SaveLocationRequest request, string name, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await LockTreeAsync(ct);

        var location = await db.Locations.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (location is null)
        {
            return Result<bool>.NotFound();
        }

        var tree = await db.LoadLocationTreeAsync(ct);

        if (request.ParentLocationId is { } parentId && !tree.Contains(parentId))
        {
            return Result<bool>.Invalid($"Parent location '{parentId}' does not exist.");
        }

        if (tree.ValidatePlacement(id, request.ParentLocationId, LocusLimits.MaxLocationDepth) is { } error)
        {
            return Result<bool>.Conflict(error);
        }

        var pathChanged = !string.Equals(location.Name, name, StringComparison.Ordinal)
                          || location.ParentLocationId != request.ParentLocationId;

        location.Name = name;
        location.NormalizedName = LocationNames.Normalize(name);
        location.Description = Blank(request.Description);
        location.AccessNotes = Blank(request.AccessNotes);
        location.ParentLocationId = request.ParentLocationId;
        location.UpdatedAt = timeProvider.GetUtcNow();
        location.UpdatedBy = currentUser.UserName;

        await db.SaveChangesAsync(ct);

        // Shared assets anywhere below carry this location in their knowledge-base text.
        var queued = pathChanged && await EnqueueSharedAssetsAsync(tree.GetSubtreeIds(id), ct);
        return Result<bool>.Success(queued);
    }

    private async Task<(LocationDeleteResult Outcome, bool Queued)> RemoveLocationAsync(Guid id, Guid? moveContentsTo, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await LockTreeAsync(ct);
        var tree = await db.LoadLocationTreeAsync(ct);

        if (!tree.Contains(id))
        {
            return (new LocationDeleteResult(LocationDeleteStatus.NotFound), false);
        }

        var children = tree.ChildrenOf(id);
        var assetCount = await db.Assets.CountAsync(a => a.LocationId == id, ct);

        if (moveContentsTo is { } targetId)
        {
            return await MoveContentsAndDeleteAsync(tree, id, targetId, children, assetCount, ct);
        }

        if (children.Count > 0 || assetCount > 0)
        {
            return (new LocationDeleteResult(LocationDeleteStatus.NotEmpty, children.Count, assetCount,
                "The location still contains sub-locations or assets. Move them first or choose where to move them."), false);
        }

        await db.Locations.Where(l => l.Id == id).ExecuteDeleteAsync(ct);
        return (new LocationDeleteResult(LocationDeleteStatus.Deleted), false);
    }

    private async Task<(LocationDeleteResult Outcome, bool Queued)> MoveContentsAndDeleteAsync(
        LocationTree tree,
        Guid id,
        Guid targetId,
        IReadOnlyList<LocationNode> children,
        int assetCount,
        CancellationToken ct)
    {
        if (targetId == id || !tree.Contains(targetId))
        {
            return (new LocationDeleteResult(LocationDeleteStatus.InvalidTarget, children.Count, assetCount,
                "Choose an existing location other than the one being deleted."), false);
        }

        if (tree.IsSelfOrDescendant(targetId, id))
        {
            return (new LocationDeleteResult(LocationDeleteStatus.Conflict, children.Count, assetCount,
                "Contents cannot be moved into a sub-location of the location being deleted."), false);
        }

        var targetChildNames = tree.ChildrenOf(targetId)
            .Select(c => LocationNames.Normalize(c.Name))
            .ToHashSet(StringComparer.Ordinal);

        var clash = children.FirstOrDefault(c => targetChildNames.Contains(LocationNames.Normalize(c.Name)));
        if (clash is not null)
        {
            return (new LocationDeleteResult(LocationDeleteStatus.Conflict, children.Count, assetCount,
                $"'{tree.GetPathString(targetId)}' already has a location named '{clash.Name}'."), false);
        }

        foreach (var child in children)
        {
            if (tree.ValidatePlacement(child.Id, targetId, LocusLimits.MaxLocationDepth) is { } error)
            {
                return (new LocationDeleteResult(LocationDeleteStatus.Conflict, children.Count, assetCount, error), false);
            }
        }

        var now = timeProvider.GetUtcNow();
        var user = currentUser.UserName;
        var subtree = tree.GetSubtreeIds(id);

        await db.Locations
            .Where(l => l.ParentLocationId == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.ParentLocationId, (Guid?)targetId)
                .SetProperty(l => l.UpdatedAt, now)
                .SetProperty(l => l.UpdatedBy, user), ct);

        await db.Assets
            .Where(a => a.LocationId == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.LocationId, targetId)
                .SetProperty(a => a.UpdatedAt, now)
                .SetProperty(a => a.UpdatedBy, user), ct);

        await db.Locations.Where(l => l.Id == id).ExecuteDeleteAsync(ct);

        // Everything that was below the deleted location now has a different path.
        var anyQueued = await EnqueueSharedAssetsAsync(subtree, ct);
        return (new LocationDeleteResult(LocationDeleteStatus.Deleted, children.Count, assetCount), anyQueued);
    }

    private async Task<bool> EnqueueSharedAssetsAsync(IReadOnlyList<Guid> locationIds, CancellationToken ct)
    {
        var assetIds = await db.Assets
            .AsNoTracking()
            .Where(a => locationIds.Contains(a.LocationId) && (a.IsAgentAccessible || a.KnowledgeContentHash != null))
            .Select(a => a.Id)
            .ToListAsync(ct);

        await outbox.EnqueueAsync(assetIds, ct);
        return assetIds.Count > 0;
    }

    private Task LockTreeAsync(CancellationToken ct)
        => db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({TreeLockKey})", ct);

    private static LocationDto ToDto(Location location, LocationTree tree, Dictionary<Guid, int> assetCounts)
    {
        var subtreeAssets = tree.GetSubtreeIds(location.Id).Sum(id => assetCounts.GetValueOrDefault(id));

        return new LocationDto(
            location.Id,
            location.Name,
            location.Description,
            location.AccessNotes,
            location.ParentLocationId,
            tree.GetPathString(location.Id),
            tree.GetDepth(location.Id),
            assetCounts.GetValueOrDefault(location.Id),
            subtreeAssets,
            tree.ChildrenOf(location.Id).Count,
            location.CreatedAt,
            location.UpdatedAt,
            location.CreatedBy,
            location.UpdatedBy);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string DuplicateNameError(string name) => $"A location named '{name}' already exists in the same place.";
}
