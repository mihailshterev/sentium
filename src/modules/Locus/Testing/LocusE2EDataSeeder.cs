using Microsoft.EntityFrameworkCore;
using Sentium.Locus.Data;
using Sentium.Locus.Entities;
using Sentium.Locus.Features.Assets;
using Sentium.Locus.Features.Locations;

namespace Sentium.Locus.Testing;

/// <summary>
/// Baseline inventory for the Playwright suite (Testing environment only). The seeded asset is private so
/// the knowledge-sync worker never tries to embed it (the e2e stack runs without inference models).
/// </summary>
public static class LocusE2EDataSeeder
{
    public const string HomeName = "e2e-baseline-home";
    public const string GarageName = "e2e-baseline-garage";
    public const string AssetName = "e2e-baseline-drill";

    public static async Task SeedAsync(LocusDbContext db, TimeProvider timeProvider, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (await db.Locations.AnyAsync(l => l.Name == HomeName, ct))
        {
            return;
        }

        var now = timeProvider.GetUtcNow();

        var home = NewLocation(HomeName, null, now);
        var garage = NewLocation(GarageName, home.Id, now);

        var drill = new Asset
        {
            Id = Guid.CreateVersion7(),
            DisplayName = AssetName,
            Category = "Tools",
            Manufacturer = "Acme",
            ModelNumber = "D-100",
            Tags = ["tools", "power"],
            IsAgentAccessible = false,
            LocationId = garage.Id,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = "e2e-seeder",
            UpdatedBy = "e2e-seeder"
        };
        drill.SearchText = AssetSearch.BuildSearchText(drill);

        db.Locations.AddRange(home, garage);
        db.Assets.Add(drill);
        await db.SaveChangesAsync(ct);
    }

    private static Location NewLocation(string name, Guid? parentId, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        Name = name,
        NormalizedName = LocationNames.Normalize(name),
        ParentLocationId = parentId,
        CreatedAt = now,
        UpdatedAt = now,
        CreatedBy = "e2e-seeder",
        UpdatedBy = "e2e-seeder"
    };
}
