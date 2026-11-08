using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Praxis.Data;
using Sentium.Praxis.Dtos;
using Sentium.Praxis.Entities;
using Sentium.Core.Results;
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

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<AgentResponse>(Ct);

        created.Should().NotBeNull();
        created!.Name.Should().Be(request.Name);
        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAgents_AfterCreation_IncludesNewAgent()
    {
        var name = $"ListAgent-{Guid.NewGuid()}";
        await _client.PostAsJsonAsync("/api/praxis/agents", new CreateAgentRequest(name, "desc", "model"), Ct);

        var response = await _client.GetAsync("/api/praxis/agents", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged = await response.Content.ReadFromJsonAsync<PagedResponse<AgentResponse>>(Ct);
        paged!.Items.Should().Contain(a => a.Name == name);
    }

    [Fact]
    public async Task UpdateAgent_ExistingAgent_ReturnsNoContent()
    {
        var createResp = await _client.PostAsJsonAsync("/api/praxis/agents", new CreateAgentRequest("OldName", "desc", "m"), Ct);
        var created = await createResp.Content.ReadFromJsonAsync<AgentResponse>(Ct);
        var updateRequest = new UpdateAgentRequest(created!.Id, "NewName", "Updated Desc", "qwen3");

        var response = await _client.PutAsJsonAsync($"/api/praxis/agents/{created.Id}", updateRequest, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var updated = await _client.GetFromJsonAsync<AgentResponse>($"/api/praxis/agents/{created.Id}", Ct);
        updated!.Name.Should().Be("NewName");
    }

    [Fact]
    public async Task CreateConversation_ValidRequest_PersistsInSystem()
    {
        var request = new CreateConversationRequest("Project Alpha Chat", "gemma3:1b");

        var response = await _client.PostAsJsonAsync("/api/praxis/conversations", request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var summary = await response.Content.ReadFromJsonAsync<ConversationSummary>(Ct);
        summary!.Title.Should().Be("Project Alpha Chat");
    }

    [Fact]
    public async Task CreateConversation_WithAnAgent_StoresItsCanonicalName()
    {
        var response = await _client.PostAsJsonAsync("/api/praxis/conversations", new CreateConversationRequest(null, "m", "timelineplanner"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var summary = await response.Content.ReadFromJsonAsync<ConversationSummary>(Ct);
        summary!.AgentName.Should().Be("TimelinePlanner");

        var detail = await _client.GetFromJsonAsync<ConversationResponse>($"/api/praxis/conversations/{summary.Id}", Ct);
        detail!.AgentName.Should().Be("TimelinePlanner");
    }

    [Theory]
    [InlineData("Orchestrator")]
    [InlineData("NoSuchAgent")]
    public async Task CreateConversation_WithAControlRoleOrUnknownAgent_Returns400(string agentName)
    {
        var response = await _client.PostAsJsonAsync("/api/praxis/conversations", new CreateConversationRequest(null, "m", agentName), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetRoster_ListsSpecialists_ButNoControlRoles()
    {
        var roster = await _client.GetFromJsonAsync<List<AgentRosterEntry>>("/api/praxis/agents/roster", Ct);

        roster!.Select(r => r.Name).Should().Contain(["GeneralAssistant", "TimelinePlanner", "ResearchAnalyst"])
            .And.NotContain(["Orchestrator", "Validator", "Summarizer"]);
    }

    [Fact]
    public async Task GetTools_ListsTheCatalogWithPolicy()
    {
        var tools = await _client.GetFromJsonAsync<List<ToolDescriptor>>("/api/praxis/tools", Ct);

        tools!.Should().Contain(t => t.Name == "knowledge_base_search" && !t.RequiresApproval);
        tools.Should().Contain(t => t.Name == "execute_code_sandbox" && t.RequiresApproval && t.Resource == "Code");
    }

    [Fact]
    public async Task CreateAgent_WithTools_PersistsThem_AndRejectsUnknownTools()
    {
        var request = new CreateAgentRequest($"Tooled-{Guid.NewGuid()}", "desc", "m", ["find_assets", "knowledge_base_search"]);

        var response = await _client.PostAsJsonAsync("/api/praxis/agents", request, Ct);
        var created = await response.Content.ReadFromJsonAsync<AgentResponse>(Ct);
        created!.Tools.Should().BeEquivalentTo("find_assets", "knowledge_base_search");

        var update = new UpdateAgentRequest(created.Id, created.Name, "desc", "m", Tools: null);
        (await _client.PutAsJsonAsync($"/api/praxis/agents/{created.Id}", update, Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.GetFromJsonAsync<AgentResponse>($"/api/praxis/agents/{created.Id}", Ct))!.Tools.Should().BeNull("null means every tool");

        var invalid = await _client.PostAsJsonAsync("/api/praxis/agents", new CreateAgentRequest($"Bad-{Guid.NewGuid()}", "d", "m", ["rm_rf"]), Ct);
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task BuiltInAgentTools_CanBeOverriddenAndReset()
    {
        var initial = await _client.GetFromJsonAsync<List<BuiltInAgentResponse>>("/api/praxis/agents/built-in", Ct);
        var analyst = initial!.Single(a => a.Name == "ResearchAnalyst");
        analyst.IsCustomized.Should().BeFalse();
        analyst.Tools.Should().BeEquivalentTo(analyst.DefaultTools);
        initial.Should().NotContain(a => a.Name == "Orchestrator");

        var set = await _client.PutAsJsonAsync("/api/praxis/agents/built-in/ResearchAnalyst/tools", new UpdateAgentToolsRequest(["find_assets"]), Ct);
        set.StatusCode.Should().Be(HttpStatusCode.OK);
        var overridden = await set.Content.ReadFromJsonAsync<BuiltInAgentResponse>(Ct);
        overridden!.IsCustomized.Should().BeTrue();
        overridden.Tools.Should().Equal("find_assets");

        var reset = await _client.DeleteAsync("/api/praxis/agents/built-in/ResearchAnalyst/tools", Ct);
        (await reset.Content.ReadFromJsonAsync<BuiltInAgentResponse>(Ct))!.IsCustomized.Should().BeFalse();

        (await _client.PutAsJsonAsync("/api/praxis/agents/built-in/Orchestrator/tools", new UpdateAgentToolsRequest(null), Ct))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetConversationDetail_ReturnsMessages()
    {
        var postResp = await _client.PostAsJsonAsync("/api/praxis/conversations", new CreateConversationRequest("Detail Test", "m"), Ct);
        var summary = await postResp.Content.ReadFromJsonAsync<ConversationSummary>(Ct);

        var response = await _client.GetAsync($"/api/praxis/conversations/{summary!.Id}", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<ConversationResponse>(Ct);
        detail!.Id.Should().Be(summary.Id);
        detail.Messages.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateConversation_WithoutTitle_StartsAsPlaceholder()
    {
        var response = await _client.PostAsJsonAsync("/api/praxis/conversations", new CreateConversationRequest(null, "m"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var summary = await response.Content.ReadFromJsonAsync<ConversationSummary>(Ct);
        summary!.Title.Should().Be(ConversationTitles.Placeholder);
    }

    [Fact]
    public async Task CreateConversation_DuplicateTitles_AreAllowed()
    {
        var title = $"Same title {Guid.NewGuid():N}";

        var first = await _client.PostAsJsonAsync("/api/praxis/conversations", new CreateConversationRequest(title, "m"), Ct);
        var second = await _client.PostAsJsonAsync("/api/praxis/conversations", new CreateConversationRequest(title, "m"), Ct);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task RenameConversation_PersistsTitle()
    {
        var postResp = await _client.PostAsJsonAsync("/api/praxis/conversations", new CreateConversationRequest(null, "m"), Ct);
        var summary = await postResp.Content.ReadFromJsonAsync<ConversationSummary>(Ct);

        var response = await _client.PutAsJsonAsync($"/api/praxis/conversations/{summary!.Id}/title", new UpdateTitleRequest("  Trip planning  "), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var detail = await _client.GetFromJsonAsync<ConversationResponse>($"/api/praxis/conversations/{summary.Id}", Ct);
        detail!.Title.Should().Be("Trip planning");
    }

    [Fact]
    public async Task RenameConversation_UnknownId_ReturnsNotFound()
    {
        var response = await _client.PutAsJsonAsync($"/api/praxis/conversations/{Guid.NewGuid()}/title", new UpdateTitleRequest("Anything"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RenameConversation_BlankTitle_ReturnsBadRequest()
    {
        var postResp = await _client.PostAsJsonAsync("/api/praxis/conversations", new CreateConversationRequest(null, "m"), Ct);
        var summary = await postResp.Content.ReadFromJsonAsync<ConversationSummary>(Ct);

        var response = await _client.PutAsJsonAsync($"/api/praxis/conversations/{summary!.Id}/title", new UpdateTitleRequest("   "), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var run = await _client.GetFromJsonAsync<WorkflowRunResponse>($"/api/praxis/workflows/runs/{runId}", Ct);
        run!.Title.Should().Be("Backup check");
    }

    [Fact]
    public async Task RenameWorkflowRun_UnknownId_ReturnsNotFound()
    {
        var response = await _client.PutAsJsonAsync($"/api/praxis/workflows/runs/{Guid.NewGuid()}/title", new UpdateTitleRequest("Anything"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetConversations_Search_MatchesTitleCaseInsensitively()
    {
        var marker = Guid.NewGuid().ToString("N")[..8];
        await _client.PostAsJsonAsync("/api/praxis/conversations", new CreateConversationRequest($"Kayak trip {marker}", "m"), Ct);
        await _client.PostAsJsonAsync("/api/praxis/conversations", new CreateConversationRequest($"Tax return {marker}", "m"), Ct);

        var paged = await _client.GetFromJsonAsync<PagedResponse<ConversationSummary>>(
            $"/api/praxis/conversations?search={Uri.EscapeDataString($"KAYAK TRIP {marker}")}", Ct);

        paged!.Items.Should().ContainSingle().Which.Title.Should().Be($"Kayak trip {marker}");
        paged.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetConversations_Search_TreatsLikeWildcardsLiterally()
    {
        var marker = Guid.NewGuid().ToString("N")[..8];
        await _client.PostAsJsonAsync("/api/praxis/conversations", new CreateConversationRequest($"Growth 100% {marker}", "m"), Ct);
        await _client.PostAsJsonAsync("/api/praxis/conversations", new CreateConversationRequest($"Growth 1000 {marker}", "m"), Ct);

        var paged = await _client.GetFromJsonAsync<PagedResponse<ConversationSummary>>(
            $"/api/praxis/conversations?search={Uri.EscapeDataString($"100% {marker}")}", Ct);

        paged!.Items.Should().ContainSingle().Which.Title.Should().Be($"Growth 100% {marker}");
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
        ids.Should().Contain([titled, untitled]);
        paged.Items.Should().NotContain(r => r.Title == $"Something else {marker}");
    }

    private static WorkflowRun NewRun(Guid id, string? title, string payload) => new()
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

    [Fact]
    public async Task CreateWorkflow_ValidDefinition_ReturnsCreated()
    {
        var request = new CreateWorkflowRequest("Cleanup-Workflow", "System maintenance", []);

        var response = await _client.PostAsJsonAsync("/api/praxis/workflows", request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var wf = await response.Content.ReadFromJsonAsync<WorkflowResponse>(Ct);
        wf!.Name.Should().Be("Cleanup-Workflow");
    }

    [Fact]
    public async Task DeleteWorkflow_ExistingId_RemovesResource()
    {
        var postResp = await _client.PostAsJsonAsync("/api/praxis/workflows", new CreateWorkflowRequest("Temporary", "d", []), Ct);
        var wf = await postResp.Content.ReadFromJsonAsync<WorkflowResponse>(Ct);

        var response = await _client.DeleteAsync($"/api/praxis/workflows/{wf!.Id}", Ct);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync($"/api/praxis/workflows/{wf.Id}", Ct);
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
