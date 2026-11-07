using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Registry.Data;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Registry;

/// <summary>
/// The Registry's data-only migrations on the settings jsonb column (the <c>ollama</c> → <c>inference</c>
/// rename and the Ollama-tag → llama-server preset mapping), verified against real PostgreSQL: roll back to
/// the previous migration, seed a legacy row, migrate.
/// </summary>
public sealed class RegistrySettingsMigrationTests(MonolithWebApplicationFactory factory) : IClassFixture<MonolithWebApplicationFactory>
{
    private const string PreviousMigration = "20261103180126_InitialCreate";
    private const string RenameMigration = "20261107104024_RenameOllamaSettingsToInference";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RenameOllamaSettingsToInference_MovesTheGroup_AndDropsTheContextWindow()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RegistryDbContext>();
        var migrator = db.GetService<IMigrator>();

        await migrator.MigrateAsync(PreviousMigration, Ct);

        var globalId = Guid.NewGuid();
        var userRowId = Guid.NewGuid();
        const string legacyGlobal = """
            {"harness":{"userHarnessPrompt":"","isBuiltInHarnessEnabled":true},
             "pdp":{"intentCheckModel":"qwen3:4b","autonomyLevel":5},
             "ollama":{"defaultModel":"qwen3:4b","agentTemperature":0.7,"agentContextWindow":8192}}
            """;
        const string userOnly = """{"harness":{"userHarnessPrompt":"mine","isBuiltInHarnessEnabled":false}}""";

        await db.Database.ExecuteSqlAsync($"""DELETE FROM registry."SystemSettings" """, Ct);
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO registry."SystemSettings" ("Id", "UserId", "Settings", "UpdatedAt", "UpdatedBy")
            VALUES ({globalId}, NULL, {legacyGlobal}::jsonb, now(), 'test'),
                   ({userRowId}, {Guid.NewGuid()}, {userOnly}::jsonb, now(), 'test')
            """, Ct);

        // Stop at the rename: the next migration maps model ids on purpose (covered below).
        await migrator.MigrateAsync(RenameMigration, Ct);

        try
        {
            await AssertRenamedAsync(db, globalId, userRowId);
        }
        finally
        {
            await migrator.MigrateAsync(cancellationToken: Ct);
        }
    }

    private static async Task AssertRenamedAsync(RegistryDbContext db, Guid globalId, Guid userRowId)
    {
        using (var global = await ReadSettingsAsync(db, globalId))
        {
            var root = global.RootElement;
            root.TryGetProperty("ollama", out _).Should().BeFalse();

            var inference = root.GetProperty("inference");
            inference.GetProperty("defaultModel").GetString().Should().Be("qwen3:4b");
            inference.GetProperty("agentTemperature").GetDouble().Should().BeApproximately(0.7, 1e-6);
            inference.TryGetProperty("agentContextWindow", out _).Should().BeFalse();

            root.GetProperty("pdp").GetProperty("intentCheckModel").GetString().Should().Be("qwen3:4b", "other groups are untouched");
        }

        using (var user = await ReadSettingsAsync(db, userRowId))
        {
            user.RootElement.TryGetProperty("inference", out _).Should().BeFalse("rows without an ollama group are left alone");
            user.RootElement.GetProperty("harness").GetProperty("userHarnessPrompt").GetString().Should().Be("mine");
        }

        var entity = await db.SystemSettings.AsNoTracking().SingleAsync(s => s.Id == globalId, Ct);
        entity.Settings.Inference.DefaultModel.Should().Be("qwen3:4b", "the application reads the migrated group");
        entity.Settings.Inference.AgentTemperature.Should().BeApproximately(0.7f, 1e-6f);
    }

    [Fact]
    public async Task MigrateModelIdsToLlamaPresets_PointsModelSettingsAtServedPresets()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RegistryDbContext>();
        var migrator = db.GetService<IMigrator>();

        await migrator.MigrateAsync(RenameMigration, Ct);

        var unknownTags = Guid.NewGuid();
        var knownTags = Guid.NewGuid();
        const string unknown = """{"pdp":{"intentCheckModel":"qwen3:4b"},"inference":{"defaultModel":"qwen3:4b","agentTemperature":0.5}}""";
        const string known = """{"pdp":{"intentCheckModel":"gemma4:e4b"},"inference":{"defaultModel":"gemma4:e4b","agentTemperature":0.5}}""";

        try
        {
            await db.Database.ExecuteSqlAsync($"""DELETE FROM registry."SystemSettings" """, Ct);
            await db.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO registry."SystemSettings" ("Id", "UserId", "Settings", "UpdatedAt", "UpdatedBy")
                VALUES ({unknownTags}, NULL, {unknown}::jsonb, now(), 'test'),
                       ({knownTags}, {Guid.NewGuid()}, {known}::jsonb, now(), 'test')
                """, Ct);
        }
        finally
        {
            await migrator.MigrateAsync(cancellationToken: Ct);
        }

        using (var row = await ReadSettingsAsync(db, unknownTags))
        {
            row.RootElement.GetProperty("inference").GetProperty("defaultModel").GetString().Should().Be("gemma-4-e4b", "the default must name a served model");
            row.RootElement.GetProperty("inference").GetProperty("agentTemperature").GetDouble().Should().BeApproximately(0.5, 1e-6);
            row.RootElement.GetProperty("pdp").GetProperty("intentCheckModel").GetString().Should().BeEmpty("an unknown intent-check model falls back to the default");
        }

        using (var row = await ReadSettingsAsync(db, knownTags))
        {
            row.RootElement.GetProperty("inference").GetProperty("defaultModel").GetString().Should().Be("gemma-4-e4b");
            row.RootElement.GetProperty("pdp").GetProperty("intentCheckModel").GetString().Should().Be("gemma-4-e4b");
        }
    }

    [Fact]
    public async Task RenameOllamaSettingsToInference_Down_RestoresTheOllamaGroup()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RegistryDbContext>();
        var migrator = db.GetService<IMigrator>();

        await migrator.MigrateAsync(cancellationToken: Ct);

        var id = Guid.NewGuid();
        const string current = """{"inference":{"defaultModel":"gemma4:e4b","agentTemperature":0.3}}""";

        await db.Database.ExecuteSqlAsync($"""DELETE FROM registry."SystemSettings" """, Ct);
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO registry."SystemSettings" ("Id", "UserId", "Settings", "UpdatedAt", "UpdatedBy")
            VALUES ({id}, NULL, {current}::jsonb, now(), 'test')
            """, Ct);

        try
        {
            await migrator.MigrateAsync(PreviousMigration, Ct);

            using var rolledBack = await ReadSettingsAsync(db, id);
            rolledBack.RootElement.TryGetProperty("inference", out _).Should().BeFalse();
            rolledBack.RootElement.GetProperty("ollama").GetProperty("defaultModel").GetString().Should().Be("gemma4:e4b");
        }
        finally
        {
            await migrator.MigrateAsync(cancellationToken: Ct);
        }
    }

    private static async Task<JsonDocument> ReadSettingsAsync(RegistryDbContext db, Guid id)
    {
        var json = await db.Database
            .SqlQuery<string>($"""SELECT "Settings"::text AS "Value" FROM registry."SystemSettings" WHERE "Id" = {id}""")
            .SingleAsync(Ct);

        return JsonDocument.Parse(json);
    }
}
