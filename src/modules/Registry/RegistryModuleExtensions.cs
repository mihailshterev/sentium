using Sentium.Infrastructure.Extensions;
using Sentium.Registry.Contracts;
using Sentium.Registry.Data;
using Sentium.Registry.Features.Settings;

namespace Sentium.Registry;

public static class RegistryModuleExtensions
{
    public static IHostApplicationBuilder AddRegistryModule(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddModuleDbContext<RegistryDbContext>(RegistryDbContext.Schema);

        var services = builder.Services;

        services.AddScoped<ISettingsRepository, SettingsRepository>();

        services.AddSingleton<ISettingsCatalog>(SettingsCatalog.FromAssembly(typeof(RegistryModuleExtensions).Assembly));
        services.AddSingleton<SettingsSchemaProvider>();
        services.AddScoped<SettingsService>();
        services.AddSingleton<ISettingsService, ScopedSettingsService>();

        return builder;
    }

    public static Task InitializeRegistryModuleAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Services.MigrateModuleDbContextAsync<RegistryDbContext>();
    }
}
