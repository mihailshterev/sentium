using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Praxis.Data;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Praxis;

/// <summary>
/// Ollama tags stored on agents and conversations become llama-server preset ids: roll back to the previous
/// migration, seed legacy rows, migrate, and check every row names a model the router can serve.
/// </summary>
public sealed class PraxisModelIdMigrationTests(MonolithWebApplicationFactory factory) : IClassFixture<MonolithWebApplicationFactory>
{
    private const string PreviousMigration = "20261107095110_AddQuartzJobStore";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task MigrateModelIdsToLlamaPresets_MapsKnownTags_AndResetsTheRest()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PraxisDbContext>();
        var migrator = db.GetService<IMigrator>();

        await migrator.MigrateAsync(PreviousMigration, Ct);

        var user = Guid.NewGuid();
        var agents = new Dictionary<string, Guid>
        {
            ["gemma4:e4b"] = Guid.NewGuid(),
            ["gemma4:e4b-it-qat"] = Guid.NewGuid(),
            ["qwen3:4b"] = Guid.NewGuid(),
            [""] = Guid.NewGuid(),
        };
        var conversations = new Dictionary<string, Guid>
        {
            ["gemma4:e4b"] = Guid.NewGuid(),
            ["qwen3:4b"] = Guid.NewGuid(),
        };

        try
        {
            foreach (var (model, id) in agents)
            {
                await db.Database.ExecuteSqlAsync(
                    $"""
                    INSERT INTO praxis."Agents" ("Id", "UserId", "Name", "Description", "Model", "CreatedAt", "UpdatedAt")
                    VALUES ({id}, {user}, {"agent-" + id.ToString("N")}, '', {model}, now(), now())
                    """, Ct);
            }

            foreach (var (model, id) in conversations)
            {
                await db.Database.ExecuteSqlAsync(
                    $"""
                    INSERT INTO praxis."Conversations" ("Id", "UserId", "Title", "Model", "CreatedAt")
                    VALUES ({id}, {user}, {"chat-" + id.ToString("N")}, {model}, now())
                    """, Ct);
            }
        }
        finally
        {
            await migrator.MigrateAsync(cancellationToken: Ct);
        }

        (await ModelOfAsync(db, "Agents", agents["gemma4:e4b"])).Should().Be("gemma-4-e4b");
        (await ModelOfAsync(db, "Agents", agents["gemma4:e4b-it-qat"])).Should().Be("gemma-4-e4b");
        (await ModelOfAsync(db, "Agents", agents["qwen3:4b"])).Should().BeEmpty("an agent without a served model falls back to the default");
        (await ModelOfAsync(db, "Agents", agents[""])).Should().BeEmpty();

        (await ModelOfAsync(db, "Conversations", conversations["gemma4:e4b"])).Should().Be("gemma-4-e4b");
        (await ModelOfAsync(db, "Conversations", conversations["qwen3:4b"])).Should().Be("gemma-4-e4b", "a resumed conversation must name a served model");
    }

    private static Task<string> ModelOfAsync(PraxisDbContext db, string table, Guid id) => table switch
    {
        "Agents" => db.Database.SqlQuery<string>($"""SELECT "Model" AS "Value" FROM praxis."Agents" WHERE "Id" = {id}""").SingleAsync(Ct),
        _ => db.Database.SqlQuery<string>($"""SELECT "Model" AS "Value" FROM praxis."Conversations" WHERE "Id" = {id}""").SingleAsync(Ct),
    };
}
