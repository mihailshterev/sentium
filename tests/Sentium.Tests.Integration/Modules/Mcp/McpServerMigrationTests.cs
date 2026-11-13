using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Mcp.Data;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Mcp;

/// <summary>
/// MCP servers used to live in the Praxis schema. The Mcp module's first migration must MOVE the existing table (and
/// its rows) rather than recreate it, and hand it back on rollback.
/// </summary>
public sealed class McpServerMigrationTests(MonolithWebApplicationFactory factory) : IClassFixture<MonolithWebApplicationFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task InitialCreate_MovesExistingPraxisServersIntoTheMcpSchema_AndBack()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<McpDbContext>();
        var migrator = db.GetService<IMigrator>();
        var id = Guid.NewGuid();
        var name = "legacy-" + id.ToString("N")[..8];
        const string emptyVariables = "{}";

        // The pre-split state: the table sits in the praxis schema.
        await migrator.MigrateAsync("0", Ct);
        (await TableSchemasAsync(db)).Should().BeEquivalentTo(["praxis"]);

        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO praxis."McpServers"
                ("Id", "Name", "DisplayName", "Transport", "Args", "Variables", "Enabled", "CreatedAt", "UpdatedAt")
            VALUES ({id}, {name}, 'Legacy', 'Http', ARRAY[]::text[], CAST({emptyVariables} AS jsonb), true, now(), now())
            """, Ct);

        try
        {
            await migrator.MigrateAsync(cancellationToken: Ct);

            (await TableSchemasAsync(db)).Should().BeEquivalentTo(["mcp"]);
            (await db.McpServers.AsNoTracking().SingleAsync(s => s.Id == id, Ct)).Name.Should().Be(name);
        }
        finally
        {
            await db.Database.ExecuteSqlAsync($"""DELETE FROM mcp."McpServers" WHERE "Id" = {id}""", Ct);
        }
    }

    private static async Task<List<string>> TableSchemasAsync(McpDbContext db) =>
        await db.Database.SqlQuery<string>(
            $"""SELECT table_schema AS "Value" FROM information_schema.tables WHERE table_name = 'McpServers'""")
            .ToListAsync(Ct);
}
