using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Sentium.Core.Constants;

namespace Sentium.Tests.Integration.Common;

/// <summary>
/// The monolith started in the <c>Testing</c> environment, as the Playwright suite runs it: the database is dropped, every
/// module migrates, and the e2e baseline data is seeded. <c>appsettings.Testing.json</c> points at the compose stack, so
/// the container connection strings are layered on top as in-memory configuration, which wins over the JSON files. The
/// host drops its database on startup, which PostgreSQL refuses for the container's default <c>postgres</c> database, so
/// it gets a database of its own on the same container.
/// </summary>
public sealed class TestingEnvironmentWebApplicationFactory : MonolithWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.ConfigureWebHost(builder);

        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{ResourceNames.SentiumDb}"] = new NpgsqlConnectionStringBuilder(DatabaseConnectionString) { Database = "sentiumdb_e2e" }.ConnectionString,
            [$"ConnectionStrings:{ResourceNames.Redis}"] = RedisConnectionString,
            [$"ConnectionStrings:{ResourceNames.WorkspaceBlobs}"] = "UseDevelopmentStorage=true",
        }));
    }
}
