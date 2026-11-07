using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sentium.Core.Results;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Security;
using Sentium.Locus.Data;
using Sentium.Locus.Dtos;
using Sentium.Locus.Entities;
using Sentium.Locus.Features.Attachments;
using Sentium.Locus.Features.Knowledge;
using Sentium.Locus.Features.Locations;

namespace Sentium.Locus.Features.Assets;

public interface IAssetService
{
    Task<PagedResponse<AssetSummaryDto>> SearchAssetsAsync(AssetSearchQuery query, CancellationToken ct = default);

    Task<AssetDto?> GetAssetAsync(Guid id, CancellationToken ct = default);

    Task<Result<AssetDto>> CreateAssetAsync(SaveAssetRequest request, CancellationToken ct = default);

    Task<Result<AssetDto>> UpdateAssetAsync(Guid id, SaveAssetRequest request, CancellationToken ct = default);

    Task<bool> DeleteAssetAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Re-queues the asset's knowledge-base sync (e.g. after it was marked failed).
    /// </summary>
    Task<bool> RequestKnowledgeSyncAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<TagCountDto>> GetTagsAsync(string? prefix, CancellationToken ct = default);

    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken ct = default);
}

public sealed class AssetService(
    LocusDbContext db,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    KnowledgeSyncOutbox outbox,
    KnowledgeSyncSignal signal,
    IAttachmentStore attachmentStore,
    ILogger<AssetService> logger) : IAssetService
{
    private const int MaxTagSuggestions = 50;

    public async Task<PagedResponse<AssetSummaryDto>> SearchAssetsAsync(AssetSearchQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var (page, pageSize) = query.Normalize();
        var tree = await db.LoadLocationTreeAsync(ct);
        var assets = db.Assets.AsNoTracking();

        if (query.LocationId is { } locationId)
        {
            var locationIds = query.IncludeSubLocations ? tree.GetSubtreeIds(locationId).ToList() : [locationId];
            assets = assets.Where(a => locationIds.Contains(a.LocationId));
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var category = query.Category.Trim();
            assets = assets.Where(a => a.Category == category);
        }

        foreach (var tag in TagNormalizer.Normalize(query.Tags))
        {
            assets = assets.Where(a => a.Tags.Contains(tag));
        }

        if (query.AgentAccessible is { } accessible)
        {
            assets = assets.Where(a => a.IsAgentAccessible == accessible);
        }

        assets = AssetSearch.ApplyTerms(assets, AssetSearch.ParseTerms(query.Q), tree);

        var total = await assets.CountAsync(ct);

        var ordered = query.Sort switch
        {
            AssetSort.Updated => assets.OrderByDescending(a => a.UpdatedAt).ThenBy(a => a.Id),
            AssetSort.Created => assets.OrderByDescending(a => a.CreatedAt).ThenBy(a => a.Id),
            _ => assets.OrderBy(a => a.DisplayName).ThenBy(a => a.Id)
        };

        var rows = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.DisplayName,
                a.Category,
                a.Tags,
                a.Manufacturer,
                a.ModelNumber,
                a.LocationId,
                a.IsAgentAccessible,
                a.KnowledgeContentHash,
                a.UpdatedAt,
                AttachmentCount = a.Attachments.Count,
                CoverId = a.Attachments
                    .Where(x => x.ContentType.StartsWith("image/"))
                    .OrderBy(x => x.CreatedAt)
                    .Select(x => (Guid?)x.Id)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        var ids = rows.Select(r => r.Id).ToList();
        var entries = await db.KnowledgeSyncEntries
            .AsNoTracking()
            .Where(e => ids.Contains(e.AssetId))
            .ToDictionaryAsync(e => e.AssetId, e => e.Status, ct);

        var items = rows.Select(r => new AssetSummaryDto(
            r.Id,
            r.DisplayName,
            r.Category,
            r.Tags,
            r.Manufacturer,
            r.ModelNumber,
            r.LocationId,
            tree.GetPathString(r.LocationId),
            r.IsAgentAccessible,
            AssetMapping.ResolveKnowledgeStatus(r.IsAgentAccessible, r.KnowledgeContentHash, entries.TryGetValue(r.Id, out var status) ? status : null),
            r.AttachmentCount,
            r.CoverId is { } coverId ? AssetMapping.ContentUrl(r.Id, coverId) : null,
            r.UpdatedAt)).ToList();

        return PagedResponse<AssetSummaryDto>.Create(items, total, page, pageSize);
    }

    public async Task<AssetDto?> GetAssetAsync(Guid id, CancellationToken ct = default)
    {
        var asset = await db.Assets
            .AsNoTracking()
            .Include(a => a.Attachments)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (asset is null)
        {
            return null;
        }

        var tree = await db.LoadLocationTreeAsync(ct);
        var entry = await db.KnowledgeSyncEntries.AsNoTracking().FirstOrDefaultAsync(e => e.AssetId == id, ct);

        return new AssetDto(
            asset.Id,
            asset.DisplayName,
            asset.Category,
            asset.PhysicalDescription,
            asset.Instructions,
            asset.Manufacturer,
            asset.ModelNumber,
            asset.SerialNumber,
            asset.PurchaseDate,
            asset.LastServicedDate,
            asset.WarrantyInfo,
            asset.IsAgentAccessible,
            asset.AgentInstructions,
            asset.Tags,
            asset.LocationId,
            tree.GetPathString(asset.LocationId),
            tree.GetPath(asset.LocationId),
            AssetMapping.ResolveKnowledgeStatus(asset.IsAgentAccessible, asset.KnowledgeContentHash, entry?.Status),
            entry?.LastError,
            asset.KnowledgeSyncedAt,
            [.. asset.Attachments.OrderBy(a => a.CreatedAt).Select(AssetMapping.ToDto)],
            asset.CreatedAt,
            asset.UpdatedAt,
            asset.CreatedBy,
            asset.UpdatedBy);
    }

    public async Task<Result<AssetDto>> CreateAssetAsync(SaveAssetRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await db.Database.ExecuteInTransactionAsync(async () =>
        {
            db.ChangeTracker.Clear();

            if (!await db.Locations.AnyAsync(l => l.Id == request.LocationId, ct))
            {
                return Result<Guid>.Invalid($"Location '{request.LocationId}' does not exist.");
            }

            var now = timeProvider.GetUtcNow();
            var asset = new Asset
            {
                Id = Guid.CreateVersion7(),
                CreatedAt = now,
                CreatedBy = currentUser.UserName
            };

            Apply(asset, request, now);
            db.Assets.Add(asset);
            await db.SaveChangesAsync(ct);

            if (asset.IsAgentAccessible)
            {
                await outbox.EnqueueAsync([asset.Id], ct);
            }

            return Result<Guid>.Success(asset.Id);
        }, ct: ct);

        if (!result.IsSuccess)
        {
            return result.Propagate<Guid, AssetDto>();
        }

        signal.Notify();
        return Result<AssetDto>.Success((await GetAssetAsync(result.Value, ct))!);
    }

    public async Task<Result<AssetDto>> UpdateAssetAsync(Guid id, SaveAssetRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await db.Database.ExecuteInTransactionAsync(async () =>
        {
            db.ChangeTracker.Clear();

            var asset = await db.Assets.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (asset is null)
            {
                return Result<bool>.NotFound();
            }

            if (asset.LocationId != request.LocationId && !await db.Locations.AnyAsync(l => l.Id == request.LocationId, ct))
            {
                return Result<bool>.Invalid($"Location '{request.LocationId}' does not exist.");
            }

            var wasShared = asset.IsAgentAccessible;
            Apply(asset, request, timeProvider.GetUtcNow());
            await db.SaveChangesAsync(ct);

            // Shared now (content may have changed) or shared before / still has a copy (must be removed).
            var queue = asset.IsAgentAccessible || wasShared || asset.KnowledgeContentHash is not null;
            if (queue)
            {
                await outbox.EnqueueAsync([asset.Id], ct);
            }

            return Result<bool>.Success(queue);
        }, ct: ct);

        if (!result.IsSuccess)
        {
            return result.Propagate<bool, AssetDto>();
        }

        if (result.Value)
        {
            signal.Notify();
        }

        return Result<AssetDto>.Success((await GetAssetAsync(id, ct))!);
    }

    public async Task<bool> DeleteAssetAsync(Guid id, CancellationToken ct = default)
    {
        var deleted = await db.Database.ExecuteInTransactionAsync(async () =>
        {
            // Attachment rows go with the asset (FK cascade); the blobs are purged after the commit.
            var removed = await db.Assets.Where(a => a.Id == id).ExecuteDeleteAsync(ct);
            if (removed == 0)
            {
                return false;
            }

            // Removal needs no embedding model and is a no-op when nothing was ever shared.
            await outbox.EnqueueAsync([id], ct);
            return true;
        }, ct: ct);

        if (!deleted)
        {
            return false;
        }

        signal.Notify();

        try
        {
            await attachmentStore.DeletePrefixAsync($"{id}/", ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Orphaned blobs are harmless (unreachable without a row); never fail the delete for them.
            logger.LogWarning(ex, "Could not delete the attachment files of asset {AssetId}", id);
        }

        return true;
    }

    public async Task<bool> RequestKnowledgeSyncAsync(Guid id, CancellationToken ct = default)
    {
        if (!await db.Assets.AnyAsync(a => a.Id == id, ct))
        {
            return false;
        }

        await outbox.EnqueueAsync([id], ct);
        signal.Notify();
        return true;
    }

    public async Task<IReadOnlyList<TagCountDto>> GetTagsAsync(string? prefix, CancellationToken ct = default)
    {
        var tagLists = await db.Assets.AsNoTracking().Select(a => a.Tags).ToListAsync(ct);
        var normalizedPrefix = TagNormalizer.NormalizeOne(prefix);

        return [.. tagLists
            .SelectMany(t => t)
            .Where(t => normalizedPrefix is null || t.StartsWith(normalizedPrefix, StringComparison.Ordinal))
            .GroupBy(t => t, StringComparer.Ordinal)
            .Select(g => new TagCountDto(g.Key, g.Count()))
            .OrderByDescending(t => t.Count)
            .ThenBy(t => t.Name, StringComparer.Ordinal)
            .Take(MaxTagSuggestions)];
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken ct = default)
        => await db.Assets
            .AsNoTracking()
            .Where(a => a.Category != null)
            .Select(a => a.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);

    private void Apply(Asset asset, SaveAssetRequest request, DateTimeOffset now)
    {
        asset.DisplayName = request.DisplayName.Trim();
        asset.Category = Blank(request.Category);
        asset.PhysicalDescription = Blank(request.PhysicalDescription);
        asset.Instructions = Blank(request.Instructions);
        asset.Manufacturer = Blank(request.Manufacturer);
        asset.ModelNumber = Blank(request.ModelNumber);
        asset.SerialNumber = Blank(request.SerialNumber);
        asset.PurchaseDate = request.PurchaseDate;
        asset.LastServicedDate = request.LastServicedDate;
        asset.WarrantyInfo = Blank(request.WarrantyInfo);
        asset.IsAgentAccessible = request.IsAgentAccessible;
        asset.AgentInstructions = Blank(request.AgentInstructions);
        asset.Tags = TagNormalizer.Normalize(request.Tags);
        asset.LocationId = request.LocationId;
        asset.UpdatedAt = now;
        asset.UpdatedBy = currentUser.UserName;
        asset.SearchText = AssetSearch.BuildSearchText(asset);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
