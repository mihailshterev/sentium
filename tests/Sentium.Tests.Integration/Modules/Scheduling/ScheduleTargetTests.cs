using System.Net;
using System.Net.Http.Json;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Workflows;
using Sentium.Scheduling.Features.Jobs;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Scheduling;

/// <summary>
/// Schedules follow the agent or workflow they run: a renamed agent keeps its Schedules, and deleting an agent or a
/// workflow deletes its Schedules instead of leaving them to fail on every fire.
/// </summary>
public sealed class ScheduleTargetTests(MonolithWebApplicationFactory factory) : IClassFixture<MonolithWebApplicationFactory>
{
    private const string Route = "/api/praxis/scheduler";
    private const string FarFuture = "0 0 0 1 1 ? 2099";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RenamedAgent_KeepsItsSchedules_AndDeletingItRemovesThem()
    {
        var client = factory.CreateClient();
        var agent = await CreateAgentAsync(client, $"Digest-{Guid.NewGuid():N}"[..20]);
        var schedule = await ScheduleAsync(client, new SaveScheduleRequest("Digest", ScheduleKind.Agent, FarFuture, AgentName: agent.Name, Prompt: "Summarize"));
        var unrelated = await ScheduleAsync(client, new SaveScheduleRequest("Briefing", ScheduleKind.Agent, FarFuture, AgentName: "GeneralAssistant", Prompt: "Brief me"));

        var renamed = $"Renamed-{Guid.NewGuid():N}"[..20];
        var update = await client.PutAsJsonAsync($"/api/praxis/agents/{agent.Id}", new UpdateAgentRequest(agent.Id, renamed, "Digests"), Ct);
        Assert.True(update.IsSuccessStatusCode);

        var afterRename = Assert.Single(await ListAsync(client), j => j.JobId == schedule.JobId);
        Assert.Equal(renamed, afterRename.TargetName);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/praxis/agents/{agent.Id}", Ct)).StatusCode);

        var remaining = await ListAsync(client);
        Assert.DoesNotContain(remaining, j => j.JobId == schedule.JobId);
        Assert.Contains(remaining, j => j.JobId == unrelated.JobId);
    }

    [Fact]
    public async Task DeletingAWorkflow_RemovesItsSchedules()
    {
        var client = factory.CreateClient();
        var agent = await CreateAgentAsync(client, $"Step-{Guid.NewGuid():N}"[..20]);
        var created = await client.PostAsJsonAsync("/api/praxis/workflows", new CreateWorkflowRequest("Nightly", "Runs nightly", [new WorkflowAgentRef(agent.Id, 1)]), Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var workflow = (await created.Content.ReadFromJsonAsync<WorkflowResponse>(Ct))!;

        var schedule = await ScheduleAsync(client, new SaveScheduleRequest("Nightly run", ScheduleKind.Workflow, FarFuture, WorkflowId: workflow.Id, Prompt: "Go"));

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/praxis/workflows/{workflow.Id}", Ct)).StatusCode);

        Assert.DoesNotContain(await ListAsync(client), j => j.JobId == schedule.JobId);
    }

    private static async Task<AgentResponse> CreateAgentAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/praxis/agents", new CreateAgentRequest(name, "A test agent"), Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AgentResponse>(Ct))!;
    }

    private static async Task<ScheduledJobResponse> ScheduleAsync(HttpClient client, SaveScheduleRequest request)
    {
        var response = await client.PostAsJsonAsync($"{Route}/jobs", request, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ScheduledJobResponse>(Ct))!;
    }

    private static async Task<List<ScheduledJobResponse>> ListAsync(HttpClient client)
    {
        return (await client.GetFromJsonAsync<List<ScheduledJobResponse>>($"{Route}/jobs", Ct))!;
    }
}
