using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Security;
using Sentium.Locus.Contracts;
using Sentium.Locus.Data;
using Sentium.Locus.Features.Agents;
using Sentium.Locus.Features.Assets;
using Sentium.Locus.Features.Attachments;
using Sentium.Locus.Features.Knowledge;
using Sentium.Locus.Features.Locations;
using Sentium.Locus.Testing;

namespace Sentium.Locus;

/// <summary>
/// Locus: the household's shared inventory of locations and assets, its attachments, and the read-only
/// view assistants get of it (through <see cref="IInventoryLookup"/> and the knowledge base).
/// </summary>
public static class LocusModuleExtensions
{
    internal const string ModuleName = "locus";

    public static IHostApplicationBuilder AddLocusModule(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddModuleDbContext<LocusDbContext>(LocusDbContext.Schema);

        var services = builder.Services;

        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<ILocationService, LocationService>();
        services.AddScoped<IAssetService, AssetService>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddSingleton<IAttachmentStore, BlobAttachmentStore>();

        services.AddSingleton<KnowledgeSyncSignal>();
        services.AddScoped<KnowledgeSyncOutbox>();
        services.AddScoped<KnowledgeSyncProcessor>();
        services.AddHostedService<KnowledgeSyncWorker>();

        services.AddScoped<InventoryLookupService>();
        services.AddSingleton<IInventoryLookup, ScopedInventoryLookup>();

        services.AddHealthChecks().AddDbContextCheck<LocusDbContext>("locus-db", tags: [ModuleName]);

        return builder;
    }

    public static async Task InitializeLocusModuleAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        await app.Services.MigrateModuleDbContextAsync<LocusDbContext>();

        if (app.Environment.IsEnvironment("Testing"))
        {
            await using var scope = app.Services.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();

            var db = scope.ServiceProvider.GetRequiredService<LocusDbContext>();
            await LocusE2EDataSeeder.SeedAsync(db, TimeProvider.System);
        }
    }
}
