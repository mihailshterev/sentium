using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Messaging;
using Sentium.Registry.Core.Settings;
using Sentium.Registry.Infrastructure.Data;
using Sentium.Registry.Infrastructure.Settings;
using Sentium.Shared.Constants;

namespace Sentium.Registry.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IHostApplicationBuilder AddRegistryInfrastructure(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddAuditedDbContext<RegistryDbContext>(ResourceNames.RegistryDb);

        builder.Services.AddScoped<ISettingsRepository, SettingsRepository>();
        builder.Services.AddSingleton<IEventBus, NatsEventBus>();

        return builder;
    }

    public static async Task ApplyMigrations(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegistryDbContext>();
        await db.Database.MigrateAsync();
    }
}
