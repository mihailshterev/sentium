using Microsoft.Extensions.DependencyInjection.Extensions;
using Sentium.Infrastructure.Extensions;
using Sentium.Nuntius.Contracts;
using Sentium.Nuntius.Data;
using Sentium.Nuntius.Features.Inbox;
using Sentium.Nuntius.Features.Publishing;
using Sentium.Nuntius.Features.Retention;
using Sentium.Nuntius.Features.Streaming;

namespace Sentium.Nuntius;

/// <summary>
/// Nuntius: Notifications other modules publish through <see cref="INotifier"/>, each user's Inbox, and the live
/// stream to their open portal sessions.
/// </summary>
public static class NuntiusModuleExtensions
{
    internal const string ModuleName = "nuntius";

    public static IHostApplicationBuilder AddNuntiusModule(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddModuleDbContext<NuntiusDbContext>(NuntiusDbContext.Schema);

        var services = builder.Services;

        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<INotificationFeed, NotificationFeed>();
        services.AddScoped<NotificationStore>();
        services.AddSingleton<INotificationStore, ScopedNotificationStore>();
        services.AddSingleton<INotifier, Notifier>();

        services.AddScoped<IInboxService, InboxService>();

        services.AddScoped<NotificationRetention>();
        services.AddHostedService<NotificationRetentionWorker>();

        services.AddHealthChecks().AddDbContextCheck<NuntiusDbContext>("nuntius-db", tags: [ModuleName]);

        return builder;
    }

    public static async Task InitializeNuntiusModuleAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        await app.Services.MigrateModuleDbContextAsync<NuntiusDbContext>();
    }
}
