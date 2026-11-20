using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Core.Results;
using Sentium.Praxis.Contracts;
using Sentium.Praxis.Data;
using Sentium.Praxis.Entities;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Agents.Runtime;
using Sentium.Praxis.Features.Conversations;
using Sentium.Praxis.Features.Workflows;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Praxis;

public class PraxisApiTests(MonolithWebApplicationFactory factory)
    : IClassFixture<MonolithWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateAgent_ValidRequest_ReturnsCreated()
    {
        var request = new CreateAgentRequest(
            Name: $"Agent-{Guid.NewGuid()}",
            Description: "Enterprise Test Agent",
            Model: "gemma3:1b"
        );

        var response = await _client.PostAsJsonAsync("/api/praxis/agents", request, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<AgentResponse>(Ct);

        Assert.NotNull(created);
        Assert.Equal(request.Name, created!.Name);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task GetAgents_AfterCreation_IncludesNewAgent()
    {
        var name = $"ListAgent-{Guid.NewGuid()}";
        await _client.PostAsJsonAsync("/api/praxis/agents", new CreateAgentRequest(name, "desc", "model"), Ct);

        var response = await _client.GetAsync("/api/praxis/agents", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paged = await response.Content.ReadFromJsonAsync<PagedResponse<AgentResponse>>(Ct);
        Assert.Contains(paged!.Items, a => a.Name == name);
    }

    [Fact]
    public async Task UpdateAgent_ExistingAgent_ReturnsNoContent()
    {
        var createResp = await _client.PostAsJsonAsync("/api/praxis/agents", new CreateAgentRequest("OldName", "desc", "m"), Ct);
        var created = await createResp.Content.ReadFromJsonAsync<AgentResponse>(Ct);
        var updateRequest = new UpdateAgentRequest(created!.Id, "NewName", "Updated Desc", "qwen3");

        var response = await _client.PutAsJsonAsync($"/api/praxis/agents/{created.Id}", updateRequest, Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var updated = await _client.GetFromJsonAsync<AgentResponse>($"/api/praxis/agents/{created.Id}", Ct);
        Assert.Equal("NewName", updated!.Name);
    }

    [Fact]
    public async Task CreateConversation_ValidRequest_PersistsInSystem()
    {
        var request = new CreateConversationRequest("Project Alpha Chat", "gemma3:1b");

        var response = await _client.PostAsJsonAsync("/api/praxis/conversations", request, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<ConversationSummary>(Ct);
        Assert.Equal("Project Alpha Chat", summary!.Title);
    }

    [Fact]
    public async Task CreateConversation_WithAnAgent_StoresItsCanonicalName()
    {
        var response = await _client.PostAsJsonAsync("/api/praxis/conversations", new CreateConversationRequest(null, "m", "timelineplanner"), Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<ConversationSummary>(Ct);
        Assert.Equal("TimelinePlanner", summary!.AgentName);

        var detail = await _client.GetFromJsonAsync<ConversationResponse>($"/api/praxis/conversations/{summary.Id}", Ct);
        Assert.Equal("TimelinePlanner", detail!.AgentName);
    }

    [Theory]
    [InlineData("Orchestrator")]
    [InlineData("NoSuchAgent")]
    public async Task CreateConversation_WithAControlRoleOrUnknownAgent_Returns400(string agentName)
    {
        var response = await _client.PostAsJsonAsync("/api/praxis/conversations", new CreateConversationRequest(null, "m", agentName), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetRoster_ListsSpecialists_ButNoControlRoles()
    {
        var roster = await _client.GetFromJsonAsync<List<AgentRosterEntry>>("/api/praxis/agents/roster", Ct);

        var actual = roster!.Select(r => r.Name);
        Assert.All(new[] { "GeneralAssistant", "TimelinePlanner", "ResearchAnalyst" }, expected => Assert.Contains(expected, actual));
        Assert.All(new[] { "Orchestrator", "Validator", "Summarizer" }, unexpected => Assert.DoesNotContain(unexpected, actual));
    }

    [Fact]
    public async Task GetTools_ListsTheCatalogWithPolicy()
    {
        var tools = await _client.GetFromJsonAsync<List<ToolDescriptor>>("/api/praxis/tools", Ct);

        Assert.Contains(tools!, t => t.Name == "knowledge_base_search" && !t.RequiresApproval);
        Assert.Contains(tools!, t => t.Name == "execute_code_sandbox" && t.RequiresApproval && t.Resource == "Code");
    }

    [Fact]
    public async Task CreateAgent_WithTools_PersistsThem_AndRejectsUnknownTools()
    {
        var request = new CreateAgentRequest($"Tooled-{Guid.NewGuid()}", "desc", "m", ["find_assets", "knowledge_base_search"]);

        var response = await _client.PostAsJsonAsync("/api/praxis/agents", request, Ct);
        var created = await response.Content.ReadFromJsonAsync<AgentResponse>(Ct);
        Assert.Equivalent(new string[] { "find_assets", "knowledge_base_search" }, created!.Tools);

        var update = new UpdateAgentRequest(created.Id, created.Name, "desc", "m", Tools: null);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PutAsJsonAsync($"/api/praxis/agents/{created.Id}", update, Ct)).StatusCode);
        // null means every tool
        Assert.Null((await _client.GetFromJsonAsync<AgentResponse>($"/api/praxis/agents/{created.Id}", Ct))!.Tools);

        var invalid = await _client.PostAsJsonAsync("/api/praxis/agents", new CreateAgentRequest($"Bad-{Guid.NewGuid()}", "d", "m", ["rm_rf"]), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task BuiltInAgentTools_CanBeOverriddenAndReset()
    {
        var initial = await _client.GetFromJsonAsync<List<BuiltInAgentResponse>>("/api/praxis/agents/built-in", Ct);
        var analyst = initial!.Single(a => a.Name == "ResearchAnalyst");
        Assert.False(analyst.IsCustomized);
        Assert.Equivalent(analyst.DefaultTools, analyst.Tools);
        Assert.DoesNotContain(initial!, a => a.Name == "Orchestrator");

        var set = await _client.PutAsJsonAsync("/api/praxis/agents/built-in/ResearchAnalyst/tools", new UpdateAgentToolsRequest(["find_assets"]), Ct);
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        var overridden = await set.Content.ReadFromJsonAsync<BuiltInAgentResponse>(Ct);
        Assert.True(overridden!.IsCustomized);
        Assert.Equal(new string[] { "find_assets" }, overridden.Tools);

        var reset = await _client.DeleteAsync("/api/praxis/agents/built-in/ResearchAnalyst/tools", Ct);
        Assert.False((await reset.Content.ReadFromJsonAsync<BuiltInAgentResponse>(Ct))!.IsCustomized);

        Assert.Equal(HttpStatusCode.NotFound, (await _client.PutAsJsonAsync("/api/praxis/agents/built-in/Orchestrator/tools", new UpdateAgentToolsRequest(null), Ct))
            .StatusCode);
    }

    [Fact]
    public async Task GetConversationDetail_ReturnsMessages()
    {
        var postResp = await _client.PostAsJsonAsync("/api/praxis/conversations", new CreateConversationRequest("Detail Test", "m"), Ct);
        var summary = await postResp.Content.ReadFromJsonAsync<ConversationSummary>(Ct);

        var response = await _client.GetAsync($"/api/praxis/conversations/{summary!.Id}", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<ConversationResponse>(Ct);
        Assert.Equal(summary.Id, detail!.Id);
        Assert.NotNull(detail.Messages);
    }

    [Fact]
    public async Task CreateConversation_WithoutTitle_StartsAsPlaceholder()
    {
        var response = await _client.PostAsJsonAsync("/api/praxis/conversations", new CreateConversationRequest(null, "m"), Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<ConversationSummary>(Ct);
        Assert.Equal(ConversationTitles.Placeholder, summary!.Title);
    }

    [Fact]
    public async Task CreateConversation_DuplicateTitles_AreAllowed()
    {
        var title = $"Same title {Guid.NewGuid():N}";

        var first = await _client.PostAsJsonAsync("/api/praxis/conversations", new CreateConversationRequest(title, "m"), Ct);
        var second = await _client.PostAsJsonAsync("/api/praxis/conversations", new CreateConversationRequest(title, "m"), Ct);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Fact]
    public async Task RenameConversation_PersistsTitle()
    {
        var postResp = await _client.PostAsJsonAsync("/api/praxis/conversations", new CreateConversationRequest(null, "m"), Ct);
        var summary = await postResp.Content.ReadFromJsonAsync<ConversationSummary>(Ct);

        var response = await _client.PutAsJsonAsync($"/api/praxis/conversations/{summary!.Id}/title", new UpdateTitleRequest("  Trip planning  "), Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var detail = await _client.GetFromJsonAsync<ConversationResponse>($"/api/praxis/conversations/{summary.Id}", Ct);
        Assert.Equal("Trip planning", detail!.Title);
    }

    [Fact]
    public async Task RenameConversation_UnknownId_ReturnsNotFound()
    {
        var response = await _client.PutAsJsonAsync($"/api/praxis/conversations/{Guid.NewGuid()}/title", new UpdateTitleRequest("Anything"), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RenameConversation_BlankTitle_ReturnsBadRequest()
    {
        var postResp = await _client.PostAsJsonAsync("/api/praxis/conversations", new CreateConversationRequest(null, "m"), Ct);
        var summary = await postResp.Content.ReadFromJsonAsync<ConversationSummary>(Ct);

        var response = await _client.PutAsJsonAsync($"/api/praxis/conversations/{summary!.Id}/title", new UpdateTitleRequest("   "), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RenameWorkflowRun_PersistsTitle()
    {
        var runId = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PraxisDbContext>();
            db.WorkflowRuns.Add(new WorkflowRun
            {
                Id = runId,
                UserId = Guid.Parse(TestAuthHandler.TestUserId),
                TriggerType = "workflow.dynamic",
                TriggerPayload = "{\"activity\":\"check the backups\"}",
                Explanation = "ok",
                Risk = string.Empty,
                Recommendation = string.Empty,
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(Ct);
        }

        var response = await _client.PutAsJsonAsync($"/api/praxis/workflows/runs/{runId}/title", new UpdateTitleRequest("Backup check"), Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var run = await _client.GetFromJsonAsync<WorkflowRunResponse>($"/api/praxis/workflows/runs/{runId}", Ct);
        Assert.Equal("Backup check", run!.Title);
    }

    [Fact]
    public async Task RenameWorkflowRun_UnknownId_ReturnsNotFound()
    {
        var response = await _client.PutAsJsonAsync($"/api/praxis/workflows/runs/{Guid.NewGuid()}/title", new UpdateTitleRequest("Anything"), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetConversations_Search_MatchesTitleCaseInsensitively()
    {
        var marker = Guid.NewGuid().ToString("N")[..8];
        await _client.PostAsJsonAsync("/api/praxis/conversations", new CreateConversationRequest($"Kayak trip {marker}", "m"), Ct);
        await _client.PostAsJsonAsync("/api/praxis/conversations", new CreateConversationRequest($"Tax return {marker}", "m"), Ct);

        var paged = await _client.GetFromJsonAsync<PagedResponse<ConversationSummary>>(
            $"/api/praxis/conversations?search={Uri.EscapeDataString($"KAYAK TRIP {marker}")}", Ct);

        Assert.Equal($"Kayak trip {marker}", Assert.Single(paged!.Items).Title);
        Assert.Equal(1, paged.TotalCount);
    }

    [Fact]
    public async Task GetConversations_Search_TreatsLikeWildcardsLiterally()
    {
        var marker = Guid.NewGuid().ToString("N")[..8];
        await _client.PostAsJsonAsync("/api/praxis/conversations", new CreateConversationRequest($"Growth 100% {marker}", "m"), Ct);
        await _client.PostAsJsonAsync("/api/praxis/conversations", new CreateConversationRequest($"Growth 1000 {marker}", "m"), Ct);

        var paged = await _client.GetFromJsonAsync<PagedResponse<ConversationSummary>>(
            $"/api/praxis/conversations?search={Uri.EscapeDataString($"100% {marker}")}", Ct);

        Assert.Equal($"Growth 100% {marker}", Assert.Single(paged!.Items).Title);
    }

    [Fact]
    public async Task GetWorkflowRuns_Search_MatchesTitle_AndScenarioOfUntitledRuns()
    {
        var marker = Guid.NewGuid().ToString("N")[..8];
        var titled = Guid.NewGuid();
        var untitled = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PraxisDbContext>();
            db.WorkflowRuns.AddRange(
                NewRun(titled, $"Backup audit {marker}", "{\"activity\":\"unrelated\"}"),
                NewRun(untitled, null, $"{{\"activity\":\"check the backup {marker}\"}}"),
                NewRun(Guid.NewGuid(), $"Something else {marker}", "{\"activity\":\"nothing\"}"));
            await db.SaveChangesAsync(Ct);
        }

        var paged = await _client.GetFromJsonAsync<PagedResponse<WorkflowRunSummaryResponse>>(
            $"/api/praxis/workflows/runs?search={Uri.EscapeDataString($"backup")}&pageSize=100", Ct);

        var ids = paged!.Items.Select(r => r.Id).ToList();
        Assert.All(new[] { titled, untitled }, expected => Assert.Contains(expected, ids));
        Assert.DoesNotContain(paged.Items, r => r.Title == $"Something else {marker}");
    }

    private static WorkflowRun NewRun(Guid id, string? title, string payload)
    {
        return new()
        {
            Id = id,
            UserId = Guid.Parse(TestAuthHandler.TestUserId),
            Title = title,
            TriggerType = "workflow.dynamic",
            TriggerPayload = payload,
            Explanation = "ok",
            Risk = string.Empty,
            Recommendation = string.Empty,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
        };
    }

    [Fact]
    public async Task CreateWorkflow_ValidDefinition_ReturnsCreated()
    {
        var request = new CreateWorkflowRequest("Cleanup-Workflow", "System maintenance", []);

        var response = await _client.PostAsJsonAsync("/api/praxis/workflows", request, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var wf = await response.Content.ReadFromJsonAsync<WorkflowResponse>(Ct);
        Assert.Equal("Cleanup-Workflow", wf!.Name);
    }

    [Fact]
    public async Task DeleteWorkflow_ExistingId_RemovesResource()
    {
        var postResp = await _client.PostAsJsonAsync("/api/praxis/workflows", new CreateWorkflowRequest("Temporary", "d", []), Ct);
        var wf = await postResp.Content.ReadFromJsonAsync<WorkflowResponse>(Ct);

        var response = await _client.DeleteAsync($"/api/praxis/workflows/{wf!.Id}", Ct);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await _client.GetAsync($"/api/praxis/workflows/{wf.Id}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }
}
