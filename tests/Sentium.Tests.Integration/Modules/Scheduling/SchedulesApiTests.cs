using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Sentium.Core.Constants;
using Sentium.Praxis.Contracts;
using Sentium.Scheduling.Features.BackgroundJobs;
using Sentium.Scheduling.Features.Jobs;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Scheduling;

/// <summary>
/// Agent Schedules through the HTTP API on the persistent Quartz store, where every job data value must be a string.
/// </summary>
public class SchedulesApiTests(MonolithWebApplicationFactory factory) : IClassFixture<MonolithWebApplicationFactory>
{
    private const string Route = "/api/praxis/scheduler";

    // Never fires during the test run.
    private const string FarFuture = "0 0 0 1 1 ? 2099";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AgentSchedule_IsPersisted_PausedResumedEdited_AndRecordsItsLastRun()
    {
        var client = factory.CreateClient();

        var created = await CreateAsync(client, "Integration briefing");
        Assert.Equal(ScheduleKind.Agent, created.Kind);
        Assert.Equal("GeneralAssistant", created.TargetName);
        Assert.Equal("AgentRunJob", await PersistedJobClassAsync(created.JobId));

        var paused = await PostAsync(client, $"{Route}/jobs/{created.JobId}/pause");
        Assert.True(paused.Paused);

        var edited = await PutAsync(client, created.JobId, new SaveScheduleRequest("Renamed briefing", ScheduleKind.Agent, FarFuture, AgentName: "GeneralAssistant", Prompt: "Brief me"));
        Assert.Equal("Renamed briefing", edited.JobName);
        Assert.True(edited.Paused);

        var resumed = await PostAsync(client, $"{Route}/jobs/{created.JobId}/resume");
        Assert.False(resumed.Paused);

        var runId = Guid.NewGuid();
        foreach (var observer in factory.Services.GetServices<IUnattendedRunObserver>())
        {
            await observer.OnRunCompletedAsync(
                new UnattendedRunCompletion(created.JobId, Guid.Parse(TestAuthHandler.TestUserId), runId, UnattendedRunStatus.Completed, DateTimeOffset.UtcNow),
                Ct);
        }

        var listed = Assert.Single(await ListAsync(client), j => j.JobId == created.JobId);
        Assert.Equal(runId, listed.LastRun!.RunId);
        Assert.Equal("Brief me", listed.Prompt);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{Route}/jobs/{created.JobId}", Ct)).StatusCode);
        Assert.DoesNotContain(await ListAsync(client), j => j.JobId == created.JobId);
    }

    [Fact]
    public async Task AnotherMember_CannotSeeOrChangeTheSchedule()
    {
        var owner = factory.CreateClient();
        var member = factory.CreateClient();
        member.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, Guid.NewGuid().ToString());

        var created = await CreateAsync(owner, "Private briefing");

        Assert.DoesNotContain(await ListAsync(member), j => j.JobId == created.JobId);
        Assert.Equal(HttpStatusCode.NotFound, (await member.PostAsync($"{Route}/jobs/{created.JobId}/pause", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await member.DeleteAsync($"{Route}/jobs/{created.JobId}", Ct)).StatusCode);

        await owner.DeleteAsync($"{Route}/jobs/{created.JobId}", Ct);
    }

    [Fact]
    public async Task UnknownAgent_IsRejected()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            $"{Route}/jobs",
            new SaveScheduleRequest("Nope", ScheduleKind.Agent, FarFuture, AgentName: "NoSuchAgent", Prompt: "hi"),
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Templates_AndPreApprovableTools_AreServed()
    {
        var client = factory.CreateClient();

        var templates = await client.GetFromJsonAsync<List<ScheduleTemplate>>($"{Route}/templates", Ct);
        Assert.Equal(["morning-briefing", "weekly-digest"], templates!.Select(t => t.Id));

        var tools = await client.GetAsync($"{Route}/pre-approvable-tools?kind=Agent&agentName=GeneralAssistant", Ct);
        Assert.Equal(HttpStatusCode.OK, tools.StatusCode);
        var unknown = await client.GetAsync($"{Route}/pre-approvable-tools?kind=Agent&agentName=NoSuchAgent", Ct);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task BackgroundJobs_AreForSovereignsOnly()
    {
        var sovereign = await factory.CreateClient().GetAsync($"{Route}/background-jobs", Ct);
        Assert.Equal(HttpStatusCode.OK, sovereign.StatusCode);
        var body = await sovereign.Content.ReadFromJsonAsync<BackgroundJobsResponse>(Ct);
        Assert.NotNull(body);

        var member = factory.CreateClient();
        member.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync($"{Route}/background-jobs", Ct)).StatusCode);
    }

    private static async Task<ScheduledJobResponse> CreateAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync(
            $"{Route}/jobs",
            new SaveScheduleRequest(name, ScheduleKind.Agent, FarFuture, AgentName: "generalassistant", Prompt: "Brief me"),
            Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ScheduledJobResponse>(Ct))!;
    }

    private static async Task<ScheduledJobResponse> PostAsync(HttpClient client, string url)
    {
        var response = await client.PostAsync(url, null, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ScheduledJobResponse>(Ct))!;
    }

    private static async Task<ScheduledJobResponse> PutAsync(HttpClient client, string jobId, SaveScheduleRequest request)
    {
        var response = await client.PutAsJsonAsync($"{Route}/jobs/{jobId}", request, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ScheduledJobResponse>(Ct))!;
    }

    private static async Task<List<ScheduledJobResponse>> ListAsync(HttpClient client)
    {
        return (await client.GetFromJsonAsync<List<ScheduledJobResponse>>($"{Route}/jobs", Ct))!;
    }

    private async Task<string> PersistedJobClassAsync(string jobName)
    {
        var connectionString = factory.Services.GetRequiredService<IConfiguration>().GetConnectionString(ResourceNames.SentiumDb);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("SELECT job_class_name FROM praxis.qrtz_job_details WHERE job_name = @name", connection);
        command.Parameters.AddWithValue("name", jobName);

        var className = (string)(await command.ExecuteScalarAsync(Ct))!;
        return className.Split(',')[0].Split('.')[^1];
    }
}
