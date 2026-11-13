using System.Reflection;
using Sentium.Codex;
using Sentium.E2ESeed;
using Sentium.Identity;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Security;
using Sentium.Infrastructure.Testing;
using Sentium.Locus;
using Sentium.Mcp;
using Sentium.Praxis;
using Sentium.Registry;
using Sentium.Scheduling;
using Sentium.Sentinel;
using Sentium.Tempus;
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
        typeof(TempusModuleExtensions).Assembly,
        typeof(PraxisModuleExtensions).Assembly,
        typeof(SchedulingModuleExtensions).Assembly,
        typeof(McpModuleExtensions).Assembly,
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
        builder.AddTempusModule();
        builder.AddPraxisModule();
        builder.AddSchedulingModule();
        builder.AddMcpModule();
        builder.AddVigilModule();

        if (builder.Environment.IsEnvironment("Testing"))
        {
            builder.Services.AddE2ESeeders();
        }

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
        await app.InitializeTempusModuleAsync();
        await app.InitializePraxisModuleAsync();
        await app.InitializeMcpModuleAsync();

        if (app.Environment.IsEnvironment("Testing"))
        {
            await SeedE2EDataAsync(app);
        }

        logger.LogInformation("Sentium modules initialized");
    }

    private static async Task SeedE2EDataAsync(WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();

        var context = new E2ESeedContext(
            Guid.Parse(app.Configuration["E2E:UserId"] ?? "e2e00000-0000-0000-0000-000000000001"),
            TimeProvider.System);

        foreach (var seeder in scope.ServiceProvider.GetServices<IE2ESeeder>())
        {
            await seeder.SeedAsync(scope.ServiceProvider, context);
        }
    }
}
