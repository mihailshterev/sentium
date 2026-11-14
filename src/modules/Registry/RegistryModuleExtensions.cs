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

        services.AddSingleton<ISettingsDescriptor>(new SettingsDescriptor<InferenceSettings>(
            SettingsKeys.Inference, SettingsScope.Global, c => c.Inference, (c, v) => c.Inference = v)
        );

        services.AddSingleton<ISettingsDescriptor>(new SettingsDescriptor<VigilSettings>(
            SettingsKeys.Vigil, SettingsScope.Global, c => c.Vigil, (c, v) => c.Vigil = v)
        );

        services.AddSingleton<ISettingsDescriptor>(new SettingsDescriptor<McpSettings>(
            SettingsKeys.Mcp, SettingsScope.Global, c => c.Mcp, (c, v) => c.Mcp = v)
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
