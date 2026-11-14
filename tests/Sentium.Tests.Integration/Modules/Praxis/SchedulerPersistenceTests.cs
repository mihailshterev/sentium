using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Quartz;
using Quartz.Impl.AdoJobStore;
using Sentium.Core.Constants;
using Sentium.Scheduling.Features.Jobs;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Praxis;

/// <summary>
/// The scheduler runs on the Quartz ADO.NET store in the praxis schema, so scheduled jobs survive restarts.
/// </summary>
public class SchedulerPersistenceTests(MonolithWebApplicationFactory factory) : IClassFixture<MonolithWebApplicationFactory>
{
    // Never fires during the test run.
    private const string FarFuture = "0 0 0 1 1 ? 2099";

    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Scheduler_UsesThePersistentPostgresStore()
    {
        var scheduler = await factory.Services.GetRequiredService<ISchedulerFactory>().GetScheduler(Ct);
        var metadata = await scheduler.GetMetaData(Ct);

        metadata.SchedulerName.Should().Be(SchedulerDefaults.SchedulerName);
        metadata.JobStoreType.Should().Be<JobStoreTX>();
        metadata.JobStoreSupportsPersistence.Should().BeTrue();
    }

    [Fact]
    public async Task ScheduledJob_IsWrittenToPraxisSchema_AndListedByTheApi()
    {
        var scheduler = await factory.Services.GetRequiredService<ISchedulerFactory>().GetScheduler(Ct);
        var group = SchedulerDefaults.GroupFor(Guid.Parse(TestAuthHandler.TestUserId));
        var jobKey = new JobKey($"agent-cron-{Guid.NewGuid()}", group);

        var job = JobBuilder.Create<ExecuteJob>()
            .WithIdentity(jobKey)
            .UsingJobData(ScheduledJobData.AgentId, "IntegrationAgent")
            .UsingJobData(ScheduledJobData.JobName, "persisted-job")
            .UsingJobData(ScheduledJobData.Code, "print('hi')")
            .UsingJobData(ScheduledJobData.Language, nameof(ScheduledJobLanguage.Python))
            .Build();
        var trigger = TriggerBuilder.Create()
            .WithIdentity($"agent-trigger-{Guid.NewGuid()}", group)
            .WithCronSchedule(FarFuture, s => s.InTimeZone(TimeZoneInfo.Utc))
            .Build();

        await scheduler.ScheduleJob(job, trigger, Ct);

        var connectionString = factory.Services.GetRequiredService<IConfiguration>().GetConnectionString(ResourceNames.SentiumDb);
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync(Ct);
            await using var command = new NpgsqlCommand(
                """
                SELECT ct.cron_expression, ct.time_zone_id, jd.job_class_name
                FROM praxis.qrtz_job_details jd
                JOIN praxis.qrtz_triggers t ON t.sched_name = jd.sched_name AND t.job_name = jd.job_name AND t.job_group = jd.job_group
                JOIN praxis.qrtz_cron_triggers ct ON ct.sched_name = t.sched_name AND ct.trigger_name = t.trigger_name AND ct.trigger_group = t.trigger_group
                WHERE jd.sched_name = @sched AND jd.job_name = @name AND jd.job_group = @group
                """, connection);
            command.Parameters.AddWithValue("sched", SchedulerDefaults.SchedulerName);
            command.Parameters.AddWithValue("name", jobKey.Name);
            command.Parameters.AddWithValue("group", jobKey.Group);

            await using var reader = await command.ExecuteReaderAsync(Ct);
            (await reader.ReadAsync(Ct)).Should().BeTrue("the job and its cron trigger must be persisted");
            reader.GetString(0).Should().Be(FarFuture);
            reader.GetString(1).Should().Be(TimeZoneInfo.Utc.Id);
            reader.GetString(2).Should().Be("Sentium.Scheduling.Features.Jobs.ExecuteJob, Sentium.Scheduling",
                "the RenameScheduledJobType migration rewrites persisted jobs to exactly this name");
        }

        var response = await _client.GetAsync("/api/praxis/scheduler/jobs", Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jobs = await response.Content.ReadFromJsonAsync<List<ScheduledJobResponse>>(Ct);
        jobs.Should().Contain(j => j.JobId == jobKey.Name && j.AgentId == "IntegrationAgent" && j.JobName == "persisted-job");

        var delete = await _client.DeleteAsync($"/api/praxis/scheduler/agents/IntegrationAgent/jobs/{jobKey.Name}", Ct);
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await scheduler.CheckExists(jobKey, Ct)).Should().BeFalse();
    }
}
