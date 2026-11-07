using System.Reflection;
using Sentium.Codex;
using Sentium.Identity;
using Sentium.Infrastructure.Extensions;
using Sentium.Locus;
using Sentium.Praxis;
using Sentium.Registry;
using Sentium.Sentinel;
using Sentium.Vigil;

namespace Sentium.Web;

/// <summary>
/// Composition root for the Sentium modules. Registration and initialization run in dependency
/// order: a module is always added after the modules whose contracts it consumes.
/// </summary>
internal static class SentiumModules
{
    public static readonly Assembly[] Assemblies =
    [
        typeof(IdentityModuleExtensions).Assembly,
        typeof(RegistryModuleExtensions).Assembly,
        typeof(SentinelModuleExtensions).Assembly,
        typeof(CodexModuleExtensions).Assembly,
        typeof(LocusModuleExtensions).Assembly,
        typeof(PraxisModuleExtensions).Assembly,
        typeof(VigilModuleExtensions).Assembly,
    ];

    public static IHostApplicationBuilder AddSentiumModules(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddIdentityModule();
        builder.AddRegistryModule();
        builder.AddSentinelModule();
        builder.AddCodexModule();
        builder.AddLocusModule();
        builder.AddPraxisModule();
        builder.AddVigilModule();

        return builder;
    }

    public static IMvcBuilder AddSentiumModuleControllers(this IMvcBuilder mvc)
    {
        ArgumentNullException.ThrowIfNull(mvc);

        foreach (var assembly in Assemblies)
        {
            mvc.AddApplicationPart(assembly);
        }

        return mvc;
    }

    /// <summary>
    /// Applies every module's migrations (and Testing seed data). The modules share one database, so in
    /// Testing it is dropped exactly once up front, never per module.
    /// </summary>
    public static async Task InitializeSentiumModulesAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(SentiumModules));

        if (app.Environment.IsEnvironment("Testing"))
        {
            logger.LogInformation("Dropping the Sentium e2e database...");
            await app.Services.DropSentiumDatabaseAsync();
        }

        await app.InitializeIdentityModuleAsync();
        await app.InitializeRegistryModuleAsync();
        await app.InitializeSentinelModuleAsync();
        await app.InitializeCodexModuleAsync();
        await app.InitializeLocusModuleAsync();
        await app.InitializePraxisModuleAsync();

        logger.LogInformation("Sentium modules initialized");
    }
}
