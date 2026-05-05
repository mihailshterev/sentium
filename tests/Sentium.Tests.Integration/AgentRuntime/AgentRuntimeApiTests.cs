using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Sentium.AgentRuntime.Core.Dtos;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.AgentRuntime;

public class AgentRuntimeApiTests(SentiumWebApplicationFactory<Sentium.AgentRuntime.Api.Program> factory)
    : IClassFixture<SentiumWebApplicationFactory<Sentium.AgentRuntime.Api.Program>>
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

        var response = await _client.PostAsJsonAsync("/agents", request, Ct);

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
        await _client.PostAsJsonAsync("/agents", new CreateAgentRequest(name, "desc", "model"), Ct);

        var response = await _client.GetAsync("/agents", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<List<AgentResponse>>(Ct);
        list.Should().Contain(a => a.Name == name);
    }

    [Fact]
    public async Task UpdateAgent_ExistingAgent_ReturnsNoContent()
    {
        var createResp = await _client.PostAsJsonAsync("/agents", new CreateAgentRequest("OldName", "desc", "m"), Ct);
        var created = await createResp.Content.ReadFromJsonAsync<AgentResponse>(Ct);
        var updateRequest = new UpdateAgentRequest(created!.Id, "NewName", "Updated Desc", "qwen3");

        var response = await _client.PutAsJsonAsync($"/agents/{created.Id}", updateRequest, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var updated = await _client.GetFromJsonAsync<AgentResponse>($"/agents/{created.Id}", Ct);
        updated!.Name.Should().Be("NewName");
    }

    [Fact]
    public async Task CreateConversation_ValidRequest_PersistsInSystem()
    {
        var request = new CreateConversationRequest("Project Alpha Chat", "gemma3:1b");

        var response = await _client.PostAsJsonAsync("/conversations", request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var summary = await response.Content.ReadFromJsonAsync<ConversationSummary>(Ct);
        summary!.Title.Should().Be("Project Alpha Chat");
    }

    [Fact]
    public async Task GetConversationDetail_ReturnsMessages()
    {
        var postResp = await _client.PostAsJsonAsync("/conversations", new CreateConversationRequest("Detail Test", "m"), Ct);
        var summary = await postResp.Content.ReadFromJsonAsync<ConversationSummary>(Ct);

        var response = await _client.GetAsync($"/conversations/{summary!.Id}", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<ConversationResponse>(Ct);
        detail!.Id.Should().Be(summary.Id);
        detail.Messages.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateWorkflow_ValidDefinition_ReturnsCreated()
    {
        var request = new CreateWorkflowRequest("Cleanup-Workflow", "System maintenance", []);

        var response = await _client.PostAsJsonAsync("/workflows", request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var wf = await response.Content.ReadFromJsonAsync<WorkflowResponse>(Ct);
        wf!.Name.Should().Be("Cleanup-Workflow");
    }

    [Fact]
    public async Task DeleteWorkflow_ExistingId_RemovesResource()
    {
        var postResp = await _client.PostAsJsonAsync("/workflows", new CreateWorkflowRequest("Temporary", "d", []), Ct);
        var wf = await postResp.Content.ReadFromJsonAsync<WorkflowResponse>(Ct);

        var response = await _client.DeleteAsync($"/workflows/{wf!.Id}", Ct);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync($"/workflows/{wf.Id}", Ct);
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
