using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sentium.Core.Constants;
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

        services.AddSingleton<ISettingsDescriptor>(new SettingsDescriptor<HarnessSettings>(
            SettingsKeys.Harness, SettingsScope.PerUser, c => c.Harness, (c, v) => c.Harness = v)
        );

        services.AddSingleton<ISettingsDescriptor>(new SettingsDescriptor<PdpSettings>(
            SettingsKeys.Pdp, SettingsScope.Global, c => c.Pdp, (c, v) => c.Pdp = v)
        );

        services.AddSingleton<ISettingsDescriptor>(new SettingsDescriptor<OllamaSettings>(
            SettingsKeys.Ollama, SettingsScope.Global, c => c.Ollama, (c, v) => c.Ollama = v)
        );

        services.AddSingleton<ISettingsDescriptor>(new SettingsDescriptor<WatchdogSettings>(
            SettingsKeys.Watchdog, SettingsScope.Global, c => c.Watchdog, (c, v) => c.Watchdog = v)
        );

        services.AddSingleton<ISettingsCatalog, SettingsCatalog>();
        services.AddScoped<SettingsService>();
        services.AddSingleton<ISettingsService, ScopedSettingsService>();

        services.AddHealthChecks().AddDbContextCheck<RegistryDbContext>("registry-db", tags: [ModuleName]);

        return builder;
    }

    public static Task InitializeRegistryModuleAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Services.MigrateModuleDbContextAsync<RegistryDbContext>();
    }

    internal const string ModuleName = "registry";
}
