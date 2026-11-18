using Microsoft.Extensions.DependencyInjection.Extensions;
using Sentium.Identity.Contracts;
using Sentium.Infrastructure.Extensions;
using Sentium.Memoria.Contracts;
using Sentium.Memoria.Data;
using Sentium.Memoria.Features.Agents;
using Sentium.Memoria.Features.Indexing;
using Sentium.Memoria.Features.Links;
using Sentium.Memoria.Features.Notes;
using Sentium.Memoria.Features.Sync;

namespace Sentium.Memoria;

/// <summary>
/// Memoria: each user's Obsidian vault, synced one-way from their PC by Sentium Satelles - notes, wikilinks, backlinks
/// and tags - embedded into the knowledge base and readable by assistants through <see cref="IVaultAgentAccess"/>.
/// </summary>
public static class MemoriaModuleExtensions
{
    internal const string ModuleName = "memoria";

    public static IHostApplicationBuilder AddMemoriaModule(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddModuleDbContext<MemoriaDbContext>(MemoriaDbContext.Schema);

        var services = builder.Services;

        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<NoteLinkUpdater>();
        services.AddScoped<NoteIndexOutbox>();
        services.AddSingleton<NoteIndexSignal>();
        services.AddScoped<NoteIndexProcessor>();
        services.AddHostedService<NoteIndexWorker>();

        services.AddScoped<IVaultSyncService, VaultSyncService>();

        services.AddScoped<VaultSemanticSearch>();
        services.AddScoped<VaultReader>();
        services.AddScoped<VaultEraser>();
        services.AddScoped<IVaultQueryService, VaultQueryService>();
        services.AddSingleton<IVaultAgentAccess, ScopedVaultAgentAccess>();

        services.AddSingleton<IUserRemovalObserver, MemoriaUserDataRemover>();

        services.AddHealthChecks().AddDbContextCheck<MemoriaDbContext>("memoria-db", tags: [ModuleName]);

        return builder;
    }

    public static async Task InitializeMemoriaModuleAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        await app.Services.MigrateModuleDbContextAsync<MemoriaDbContext>();
    }
}
