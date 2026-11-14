using Microsoft.EntityFrameworkCore;
using Sentium.Core.Text;
using Sentium.Locus.Contracts;
using Sentium.Locus.Contracts.Models;
using Sentium.Locus.Data;
using Sentium.Locus.Entities;
using Sentium.Locus.Features.Assets;
using Sentium.Locus.Features.Locations;

namespace Sentium.Locus.Features.Agents;

/// <summary>
/// Scoped implementation behind <see cref="IInventoryLookup"/>. Every query starts from
/// <see cref="SharedAssets"/>, so a private asset can never leak through any method.
/// </summary>
public sealed class InventoryLookupService(LocusDbContext db) : IInventoryLookup
{
    public const int MaxLimit = 25;

    private IQueryable<Asset> SharedAssets => db.Assets.AsNoTracking().Where(a => a.IsAgentAccessible);

    public async Task<IReadOnlyList<InventoryAssetSummary>> FindAssetsAsync(InventoryAssetQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var tree = await db.LoadLocationTreeAsync(ct);
        var assets = SharedAssets;

        if (!string.IsNullOrWhiteSpace(query.Location))
        {
            var locationIds = tree.Resolve(query.Location).SelectMany(l => tree.GetSubtreeIds(l.Id)).Distinct().ToList();
            if (locationIds.Count == 0)
            {
                return [];
            }

            assets = assets.Where(a => locationIds.Contains(a.LocationId));
        }

        if (TagNormalizer.NormalizeOne(query.Tag) is { } tag)
        {
            assets = assets.Where(a => a.Tags.Contains(tag));
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var category = query.Category.Trim().ToLowerInvariant();
            assets = assets.Where(a => a.Category != null && a.Category.ToLower() == category);
        }

        assets = AssetSearch.ApplyTerms(assets, AssetSearch.ParseTerms(query.Query), tree);

        var rows = await assets
            .OrderBy(a => a.DisplayName)
            .ThenBy(a => a.Id)
            .Take(Math.Clamp(query.Limit, 1, MaxLimit))
            .ToListAsync(ct);

        return [.. rows.Select(a => ToSummary(a, tree))];
    }

    public async Task<InventoryAssetDetail?> GetAssetAsync(Guid id, CancellationToken ct = default)
    {
        var asset = await SharedAssets.Include(a => a.Attachments).FirstOrDefaultAsync(a => a.Id == id, ct);
        if (asset is null)
        {
            return null;
        }

        var tree = await db.LoadLocationTreeAsync(ct);
        return ToDetail(asset, tree);
    }

    public async Task<IReadOnlyList<InventoryAssetDetail>> FindAssetsByNameAsync(string name, int limit = 5, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(name);

        var text = name.Trim().ToLowerInvariant();
        if (text.Length == 0)
        {
            return [];
        }

        var take = Math.Clamp(limit, 1, MaxLimit);

        var matches = await SharedAssets
            .Include(a => a.Attachments)
            .Where(a => a.DisplayName.ToLower() == text)
            .OrderBy(a => a.DisplayName)
            .Take(take)
            .ToListAsync(ct);

        if (matches.Count == 0)
        {
            var pattern = AssetSearch.ToContainsPattern(text);
            matches = await SharedAssets
                .Include(a => a.Attachments)
                .Where(a => EF.Functions.ILike(a.DisplayName, pattern, AssetSearch.EscapeCharacter.ToString()))
                .OrderBy(a => a.DisplayName)
                .Take(take)
                .ToListAsync(ct);
        }

        if (matches.Count == 0)
        {
            return [];
        }

        var tree = await db.LoadLocationTreeAsync(ct);
        return [.. matches.Select(a => ToDetail(a, tree))];
    }

    public async Task<InventoryLocationLookup> GetLocationContentsAsync(string? location, bool includeNested = false, int limit = 25, CancellationToken ct = default)
    {
        var tree = await db.LoadLocationTreeAsync(ct);
        var take = Math.Clamp(limit, 1, MaxLimit);

        if (string.IsNullOrWhiteSpace(location))
        {
            return new InventoryLocationLookup(InventoryLocationLookupStatus.Found, null, [.. tree.Roots.Select(r => ToInfo(r, tree))], [], [], false);
        }

        var matches = tree.Resolve(location);
        if (matches.Count == 0)
        {
            return new InventoryLocationLookup(InventoryLocationLookupStatus.NotFound, null, [], [], [], false);
        }

        if (matches.Count > 1)
        {
            return new InventoryLocationLookup(InventoryLocationLookupStatus.Ambiguous, null, [], [], [.. matches.Take(MaxLimit).Select(m => ToInfo(m, tree))], matches.Count > MaxLimit);
        }

        var target = matches[0];
        var locationIds = includeNested ? tree.GetSubtreeIds(target.Id).ToList() : [target.Id];

        var assets = await SharedAssets
            .Where(a => locationIds.Contains(a.LocationId))
            .OrderBy(a => a.DisplayName)
            .ThenBy(a => a.Id)
            .Take(take + 1)
            .ToListAsync(ct);

        return new InventoryLocationLookup(
            InventoryLocationLookupStatus.Found,
            ToInfo(target, tree),
            [.. tree.ChildrenOf(target.Id).Select(c => ToInfo(c, tree))],
            [.. assets.Take(take).Select(a => ToSummary(a, tree))],
            [],
            assets.Count > take);
    }

    private static InventoryAssetSummary ToSummary(Asset asset, LocationTree tree)
        => new(asset.Id, asset.DisplayName, asset.Category, asset.Tags, tree.GetPathString(asset.LocationId));

    private static InventoryAssetDetail ToDetail(Asset asset, LocationTree tree)
    {
        return new(
            asset.Id,
            asset.DisplayName,
            asset.Category,
            asset.Tags,
            tree.GetPathString(asset.LocationId),
            asset.Manufacturer,
            asset.ModelNumber,
            asset.SerialNumber,
            asset.PhysicalDescription,
            asset.Instructions,
            asset.PurchaseDate,
            asset.LastServicedDate,
            asset.WarrantyInfo,
            asset.AgentInstructions,
            [.. asset.Attachments.OrderBy(a => a.CreatedAt).Select(a => new InventoryAttachmentInfo(a.FileName, a.Kind.ToString()))]);
    }

    private static InventoryLocationInfo ToInfo(LocationNode node, LocationTree tree)
        => new(node.Id, node.Name, tree.GetPathString(node.Id), tree.ChildrenOf(node.Id).Count);
}
