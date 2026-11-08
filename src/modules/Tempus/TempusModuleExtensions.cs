using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Security;
using Sentium.Tempus.Contracts;
using Sentium.Tempus.Data;
using Sentium.Tempus.Features.Agents;
using Sentium.Tempus.Features.Notes;
using Sentium.Tempus.Features.Overview;
using Sentium.Tempus.Features.Proposals;
using Sentium.Tempus.Features.Tasks;
using Sentium.Tempus.Testing;

namespace Sentium.Tempus;

/// <summary>
/// Tempus: each user's private year timeline - tasks on days or pinned to months, notes, and the changes agents
/// propose - plus the read/suggest/annotate view assistants get of it (through <see cref="ITimelineAgentAccess"/>).
/// </summary>
public static class TempusModuleExtensions
{
    internal const string ModuleName = "tempus";

    public static IHostApplicationBuilder AddTempusModule(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddModuleDbContext<TempusDbContext>(TempusDbContext.Schema);

        var services = builder.Services;

        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<ITimelineTaskService, TimelineTaskService>();
        services.AddScoped<ITimelineOverviewService, TimelineOverviewService>();
        services.AddScoped<ITimelineNoteService, TimelineNoteService>();
        services.AddScoped<ITimelineProposalService, TimelineProposalService>();

        services.AddScoped<TimelineAgentAccessService>();
        services.AddSingleton<ITimelineAgentAccess, ScopedTimelineAgentAccess>();

        services.AddHealthChecks().AddDbContextCheck<TempusDbContext>("tempus-db", tags: [ModuleName]);

        return builder;
    }

    public static async Task InitializeTempusModuleAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        await app.Services.MigrateModuleDbContextAsync<TempusDbContext>();

        if (app.Environment.IsEnvironment("Testing"))
        {
            await using var scope = app.Services.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();

            var db = scope.ServiceProvider.GetRequiredService<TempusDbContext>();
            var testUserId = Guid.Parse(app.Configuration["E2E:UserId"] ?? "e2e00000-0000-0000-0000-000000000001");

            await TempusE2EDataSeeder.SeedAsync(db, testUserId, TimeProvider.System);
        }
    }
}
