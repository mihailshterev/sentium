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
/// Composition root for the Sentium modules. <see cref="Modules"/> is the single ordered list: registration and
/// initialization run in that order, and a module always comes after the modules whose contracts it consumes.
/// </summary>
internal static class SentiumModules
{
    private const string E2EUserIdKey = "E2E:UserId";
    private const string DefaultE2EUserId = "e2e00000-0000-0000-0000-000000000001";

    private static readonly SentiumModule[] Modules =
    [
        new(typeof(IdentityModuleExtensions), b => b.AddIdentityModule(), a => a.InitializeIdentityModuleAsync()),
        new(typeof(RegistryModuleExtensions), b => b.AddRegistryModule(), a => a.InitializeRegistryModuleAsync()),
        new(typeof(SentinelModuleExtensions), b => b.AddSentinelModule(), a => a.InitializeSentinelModuleAsync()),
        new(typeof(CodexModuleExtensions), b => b.AddCodexModule(), a => a.InitializeCodexModuleAsync()),
        new(typeof(LocusModuleExtensions), b => b.AddLocusModule(), a => a.InitializeLocusModuleAsync()),
        new(typeof(TempusModuleExtensions), b => b.AddTempusModule(), a => a.InitializeTempusModuleAsync()),
        new(typeof(PraxisModuleExtensions), b => b.AddPraxisModule(), a => a.InitializePraxisModuleAsync()),
        new(typeof(SchedulingModuleExtensions), b => b.AddSchedulingModule()),
        new(typeof(McpModuleExtensions), b => b.AddMcpModule(), a => a.InitializeMcpModuleAsync()),
        new(typeof(VigilModuleExtensions), b => b.AddVigilModule()),
    ];

    public static IReadOnlyList<Assembly> Assemblies { get; } = [.. Modules.Select(m => m.Marker.Assembly)];

    public static IHostApplicationBuilder AddSentiumModules(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        foreach (var module in Modules)
        {
            module.Add(builder);
        }

        if (builder.Environment.IsTesting())
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

        if (app.Environment.IsTesting())
        {
            logger.LogInformation("Dropping the Sentium e2e database...");
            await app.Services.DropSentiumDatabaseAsync();
        }

        foreach (var module in Modules)
        {
            if (module.InitializeAsync is { } initialize)
            {
                await initialize(app);
            }
        }

        if (app.Environment.IsTesting())
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
            Guid.Parse(app.Configuration[E2EUserIdKey] ?? DefaultE2EUserId),
            TimeProvider.System);

        foreach (var seeder in scope.ServiceProvider.GetServices<IE2ESeeder>())
        {
            await seeder.SeedAsync(scope.ServiceProvider, context);
        }
    }

    /// <summary>
    /// One module: a type from its assembly (for controllers and validators), its registration and, when it has
    /// one, its startup initialization.
    /// </summary>
    private sealed record SentiumModule(
        Type Marker,
        Func<IHostApplicationBuilder, IHostApplicationBuilder> Add,
        Func<WebApplication, Task>? InitializeAsync = null);
}
