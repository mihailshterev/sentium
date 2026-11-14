using Microsoft.Extensions.DependencyInjection.Extensions;
using Sentium.Infrastructure.Extensions;
using Sentium.Registry.Contracts;
using Sentium.Tempus.Contracts;
using Sentium.Tempus.Data;
using Sentium.Tempus.Features.Agents;
using Sentium.Tempus.Features.Calendar;
using Sentium.Tempus.Features.Notes;
using Sentium.Tempus.Features.Overview;
using Sentium.Tempus.Features.Proposals;
using Sentium.Tempus.Features.Reminders;
using Sentium.Tempus.Features.Series;
using Sentium.Tempus.Features.Tasks;

namespace Sentium.Tempus;

/// <summary>
/// Tempus: each user's private year timeline - Tasks and Calendar Events on days or pinned to months, repeating
/// Series, Reminders, notes, and the changes agents propose - plus the read/suggest/annotate view assistants get of
/// it (through <see cref="ITimelineAgentAccess"/>).
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

        services.AddSingleton<IUserCalendarProvider, UserCalendarProvider>();
        services.AddScoped<TimelineSeriesWriter>();
        services.AddScoped<ITimelineTaskService, TimelineTaskService>();
        services.AddScoped<ITimelineOverviewService, TimelineOverviewService>();
        services.AddScoped<ITimelineNoteService, TimelineNoteService>();
        services.AddScoped<ITimelineProposalService, TimelineProposalService>();

        services.AddScoped<TimelineAgentAccessService>();
        services.AddSingleton<ITimelineAgentAccess, ScopedTimelineAgentAccess>();

        services.AddScoped<TimelineSeriesMaterializer>();
        services.AddScoped<TimelineReminderSweep>();
        services.AddSingleton<ISettingsChangedHandler, TimelineReminderRearmer>();
        services.AddHostedService<TimelineSeriesWorker>();
        services.AddHostedService<TimelineReminderWorker>();

        services.AddHealthChecks().AddDbContextCheck<TempusDbContext>("tempus-db", tags: [ModuleName]);

        return builder;
    }

    public static async Task InitializeTempusModuleAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        await app.Services.MigrateModuleDbContextAsync<TempusDbContext>();
    }
}
