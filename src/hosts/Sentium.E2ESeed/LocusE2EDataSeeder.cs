using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Infrastructure.Testing;
using Sentium.Locus.Data;
using Sentium.Locus.Entities;
using Sentium.Locus.Features.Assets;
using Sentium.Locus.Features.Locations;

namespace Sentium.E2ESeed;

/// <summary>
/// Baseline inventory for the Playwright suite (Testing environment only). The seeded asset is private so
/// the knowledge-sync worker never tries to embed it (the e2e stack runs without inference models).
/// </summary>
public sealed class LocusE2EDataSeeder : IE2ESeeder
{
    public const string HomeName = "e2e-baseline-home";
    public const string GarageName = "e2e-baseline-garage";
    public const string AssetName = "e2e-baseline-drill";

    public Task SeedAsync(IServiceProvider services, E2ESeedContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(context);

        return SeedAsync(services.GetRequiredService<LocusDbContext>(), context.TimeProvider, ct);
    }

    private static async Task SeedAsync(LocusDbContext db, TimeProvider timeProvider, CancellationToken ct)
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

    private static Location NewLocation(string name, Guid? parentId, DateTimeOffset now)
    {
        return new()
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
}
