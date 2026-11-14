using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Sentium.Mcp.Data;
using Sentium.Praxis.Data;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Praxis;

/// <summary>
/// Scheduling moved to its own assembly, and Quartz persists each job's CLR type name. Roll back to the migration before
/// the rename, store a job under the old name, migrate, and check Quartz can now resolve the job class.
/// </summary>
public sealed class ScheduledJobTypeMigrationTests(MonolithWebApplicationFactory factory) : IClassFixture<MonolithWebApplicationFactory>
{
    private const string PreviousMigration = "20261108195306_AddMcpServerVariables";
    private const string OldJobType = "Sentium.Praxis.Features.Scheduling.ExecuteJob, Sentium.Praxis";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RenameScheduledJobType_RewritesPersistedJobsToTheSchedulingAssembly()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PraxisDbContext>();
        var migrator = db.GetService<IMigrator>();
        var mcpMigrator = scope.ServiceProvider.GetRequiredService<McpDbContext>().GetService<IMigrator>();
        var jobName = $"legacy-{Guid.NewGuid():N}";

        await mcpMigrator.MigrateAsync("0", Ct);
        await migrator.MigrateAsync(PreviousMigration, Ct);
        try
        {
            await db.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO praxis.qrtz_job_details
                    (sched_name, job_name, job_group, job_class_name, is_durable, is_nonconcurrent, is_update_data, requests_recovery)
                VALUES ('sentium-scheduler', {jobName}, 'legacy', {OldJobType}, false, true, true, false)
                """, Ct);
        }
        finally
        {
            await migrator.MigrateAsync(cancellationToken: Ct);
            await mcpMigrator.MigrateAsync(cancellationToken: Ct);
        }

        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(Ct);
        try
        {
            await using var command = new NpgsqlCommand(
                "SELECT job_class_name FROM praxis.qrtz_job_details WHERE job_name = @name", connection);
            command.Parameters.AddWithValue("name", jobName);

            (await command.ExecuteScalarAsync(Ct)).Should().Be("Sentium.Scheduling.Features.Jobs.ExecuteJob, Sentium.Scheduling");
        }
        finally
        {
            await db.Database.ExecuteSqlAsync($"DELETE FROM praxis.qrtz_job_details WHERE job_name = {jobName}", Ct);
            await db.Database.CloseConnectionAsync();
        }
    }
}
