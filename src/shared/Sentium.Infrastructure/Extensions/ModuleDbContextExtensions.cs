using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Sentium.Core.Constants;
using Sentium.Infrastructure.Interceptors;
using Sentium.Infrastructure.Security;

namespace Sentium.Infrastructure.Extensions;

public static class ModuleDbContextExtensions
{
    /// <summary>
    /// Connection string used only by design-time DbContext factories (<c>dotnet ef migrations add</c>
    /// never opens it); runtime connections come from <see cref="ResourceNames.SentiumDb"/>.
    /// </summary>
    public const string DesignTimeConnectionString = "Host=localhost;Database=sentiumdb;Username=postgres";

    /// <summary>
    /// Registers a module's audited <see cref="DbContext"/> against the shared Sentium PostgreSQL database.
    /// Every module owns one schema and keeps its migrations history inside it, so modules migrate independently.
    /// </summary>
    public static IHostApplicationBuilder AddModuleDbContext<TContext>(this IHostApplicationBuilder builder, string schema) where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);

        builder.Services.TryAddScoped<AuditInterceptor>();

        builder.Services.AddDbContext<TContext>((sp, options) =>
        {
            options.UseModuleNpgsql(builder.Configuration.GetConnectionString(ResourceNames.SentiumDb), schema)
                .AddInterceptors(sp.GetRequiredService<AuditInterceptor>());
        });

        builder.EnrichNpgsqlDbContext<TContext>();

        return builder;
    }

    /// <summary>
    /// Configures Npgsql for a module schema. Shared by the runtime registration and design-time factories.
    /// </summary>
    public static DbContextOptionsBuilder UseModuleNpgsql(this DbContextOptionsBuilder options, string? connectionString, string schema)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, schema));
    }

    /// <summary>
    /// Applies a module's pending migrations inside a system-scoped DI scope, so user-scoped query
    /// filters never hide rows from the migration or the seeders that follow it.
    /// </summary>
    public static async Task MigrateModuleDbContextAsync<TContext>(this IServiceProvider services, CancellationToken ct = default) where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();

        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        await db.Database.MigrateAsync(ct);
    }

    /// <summary>
    /// Drops the shared Sentium database. Because every module shares one database, this must run
    /// once, before any module migrates, rather than per module context.
    /// </summary>
    public static async Task DropSentiumDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        var configuration = services.GetRequiredService<IConfiguration>();
        var options = new DbContextOptionsBuilder()
            .UseNpgsql(configuration.GetConnectionString(ResourceNames.SentiumDb))
            .Options;

        await using var db = new DbContext(options);
        await db.Database.EnsureDeletedAsync(ct);
    }
}
