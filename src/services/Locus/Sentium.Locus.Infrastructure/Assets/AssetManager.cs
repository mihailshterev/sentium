using Sentium.Locus.Core.Assets;
using Sentium.Locus.Core.Dtos;
using Sentium.Locus.Core.Entities;
using Sentium.Locus.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Sentium.Locus.Infrastructure.Assets;

public sealed class AssetManager(LocusDbContext context) : IAssetManager
{
    private static AssetDto ToDto(Asset a) => new(
        a.Id,
        a.DisplayName,
        a.Category,
        a.PhysicalDescription,
        a.Instructions,
        a.Manufacturer,
        a.ModelNumber,
        a.SerialNumber,
        a.PurchaseDate,
        a.LastServicedDate,
        a.WarrantyInfo,
        a.IsAgentAccessible,
        a.AgentInstructions,
        a.LocationId,
        a.Location.Name,
        a.CreatedAt,
        a.UpdatedAt);

    public async Task<IReadOnlyList<AssetDto>> GetAssetsAsync(CancellationToken ct = default)
    {
        return await context.Assets
            .AsNoTracking()
            .Include(a => a.Location)
            .OrderBy(a => a.DisplayName)
            .Select(a => new AssetDto(
                a.Id,
                a.DisplayName,
                a.Category,
                a.PhysicalDescription,
                a.Instructions,
                a.Manufacturer,
                a.ModelNumber,
                a.SerialNumber,
                a.PurchaseDate,
                a.LastServicedDate,
                a.WarrantyInfo,
                a.IsAgentAccessible,
                a.AgentInstructions,
                a.LocationId,
                a.Location.Name,
                a.CreatedAt,
                a.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AssetDto>> GetAssetsByLocationAsync(Guid locationId, CancellationToken ct = default)
    {
        return await context.Assets
            .AsNoTracking()
            .Include(a => a.Location)
            .Where(a => a.LocationId == locationId)
            .OrderBy(a => a.DisplayName)
            .Select(a => new AssetDto(
                a.Id,
                a.DisplayName,
                a.Category,
                a.PhysicalDescription,
                a.Instructions,
                a.Manufacturer,
                a.ModelNumber,
                a.SerialNumber,
                a.PurchaseDate,
                a.LastServicedDate,
                a.WarrantyInfo,
                a.IsAgentAccessible,
                a.AgentInstructions,
                a.LocationId,
                a.Location.Name,
                a.CreatedAt,
                a.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<AssetDto?> GetAssetAsync(Guid id, CancellationToken ct = default)
    {
        return await context.Assets
            .AsNoTracking()
            .Include(a => a.Location)
            .Where(a => a.Id == id)
            .Select(a => new AssetDto(
                a.Id,
                a.DisplayName,
                a.Category,
                a.PhysicalDescription,
                a.Instructions,
                a.Manufacturer,
                a.ModelNumber,
                a.SerialNumber,
                a.PurchaseDate,
                a.LastServicedDate,
                a.WarrantyInfo,
                a.IsAgentAccessible,
                a.AgentInstructions,
                a.LocationId,
                a.Location.Name,
                a.CreatedAt,
                a.UpdatedAt))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => await context.Assets.AnyAsync(a => a.Id == id, ct);

    public async Task<AssetDto> CreateAssetAsync(CreateAssetRequest request, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var asset = new Asset
        {
            Id = Guid.NewGuid(),
            DisplayName = request.DisplayName,
            Category = request.Category,
            PhysicalDescription = request.PhysicalDescription,
            Instructions = request.Instructions,
            Manufacturer = request.Manufacturer,
            ModelNumber = request.ModelNumber,
            SerialNumber = request.SerialNumber,
            PurchaseDate = request.PurchaseDate,
            LastServicedDate = request.LastServicedDate,
            WarrantyInfo = request.WarrantyInfo,
            IsAgentAccessible = request.IsAgentAccessible,
            AgentInstructions = request.AgentInstructions,
            LocationId = request.LocationId,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.Assets.Add(asset);
        await context.SaveChangesAsync(ct);

        var locationName = await context.Locations
            .AsNoTracking()
            .Where(l => l.Id == asset.LocationId)
            .Select(l => l.Name)
            .FirstAsync(ct);

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
            asset.LocationId,
            locationName,
            asset.CreatedAt,
            asset.UpdatedAt);
    }

    public async Task<AssetDto?> UpdateAssetAsync(Guid id, UpdateAssetRequest request, CancellationToken ct = default)
    {
        var asset = await context.Assets.FindAsync([id], ct);
        if (asset is null)
        {
            return null;
        }

        asset.DisplayName = request.DisplayName;
        asset.Category = request.Category;
        asset.PhysicalDescription = request.PhysicalDescription;
        asset.Instructions = request.Instructions;
        asset.Manufacturer = request.Manufacturer;
        asset.ModelNumber = request.ModelNumber;
        asset.SerialNumber = request.SerialNumber;
        asset.PurchaseDate = request.PurchaseDate;
        asset.LastServicedDate = request.LastServicedDate;
        asset.WarrantyInfo = request.WarrantyInfo;
        asset.IsAgentAccessible = request.IsAgentAccessible;
        asset.AgentInstructions = request.AgentInstructions;
        asset.LocationId = request.LocationId;
        asset.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(ct);

        var locationName = await context.Locations
            .AsNoTracking()
            .Where(l => l.Id == asset.LocationId)
            .Select(l => l.Name)
            .FirstAsync(ct);

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
            asset.LocationId,
            locationName,
            asset.CreatedAt,
            asset.UpdatedAt);
    }

    public async Task DeleteAssetAsync(Guid id, CancellationToken ct = default)
    {
        await context.Assets.Where(a => a.Id == id).ExecuteDeleteAsync(ct);
    }
}
