using System.Net;
using System.Net.Http.Json;
using Sentium.Core.Results;
using Sentium.Praxis.Features.Workspaces;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Praxis;

public sealed class WorkspaceApiTests(WorkspaceApiFactory factory) : IClassFixture<WorkspaceApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateWorkspace_ReturnsCreated_WithDto()
    {
        var request = new CreateWorkspaceRequest($"Workspace-{Guid.NewGuid()}", "test workspace");

        var response = await _client.PostAsJsonAsync("/api/praxis/workspaces", request, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<WorkspaceDto>(Ct);
        Assert.NotNull(dto);
        Assert.Equal(request.Name, dto!.Name);
    }

    [Fact]
    public async Task GetWorkspaces_AfterCreation_IncludesNew()
    {
        var name = $"ListWs-{Guid.NewGuid()}";
        await _client.PostAsJsonAsync("/api/praxis/workspaces", new CreateWorkspaceRequest(name, null), Ct);

        var response = await _client.GetAsync("/api/praxis/workspaces", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paged = await response.Content.ReadFromJsonAsync<PagedResponse<WorkspaceDto>>(Ct);
        Assert.Contains(paged!.Items, w => w.Name == name);
    }

    [Fact]
    public async Task CreateWorkspace_Returns409_WhenNameDuplicate()
    {
        var name = $"DupWs-{Guid.NewGuid()}";
        await _client.PostAsJsonAsync("/api/praxis/workspaces", new CreateWorkspaceRequest(name, null), Ct);

        var response = await _client.PostAsJsonAsync("/api/praxis/workspaces", new CreateWorkspaceRequest(name, null), Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdateWorkspace_Returns200_WithUpdatedDto()
    {
        var createResp = await _client.PostAsJsonAsync("/api/praxis/workspaces", new CreateWorkspaceRequest($"Original-{Guid.NewGuid()}", null), Ct);
        var created = await createResp.Content.ReadFromJsonAsync<WorkspaceDto>(Ct);

        var newName = $"Updated-{Guid.NewGuid()}";
        var response = await _client.PutAsJsonAsync($"/api/praxis/workspaces/{created!.Id}", new UpdateWorkspaceRequest(newName, "updated desc"), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<WorkspaceDto>(Ct);
        Assert.Equal(newName, updated!.Name);
    }

    [Fact]
    public async Task DeleteWorkspace_Returns204_ThenGetReturns404()
    {
        var createResp = await _client.PostAsJsonAsync("/api/praxis/workspaces", new CreateWorkspaceRequest($"ToDelete-{Guid.NewGuid()}", null), Ct);
        var created = await createResp.Content.ReadFromJsonAsync<WorkspaceDto>(Ct);

        var deleteResp = await _client.DeleteAsync($"/api/praxis/workspaces/{created!.Id}", Ct);
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        var getResp = await _client.GetAsync($"/api/praxis/workspaces/{created.Id}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    [Fact]
    public async Task GetWorkspaceFiles_Returns200_WhenWorkspaceExists()
    {
        var createResp = await _client.PostAsJsonAsync("/api/praxis/workspaces", new CreateWorkspaceRequest($"FilesWs-{Guid.NewGuid()}", null), Ct);
        var created = await createResp.Content.ReadFromJsonAsync<WorkspaceDto>(Ct);

        var response = await _client.GetAsync($"/api/praxis/workspaces/{created!.Id}/files", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetWorkspaceFiles_Returns404_WhenWorkspaceNotFound()
    {
        var response = await _client.GetAsync($"/api/praxis/workspaces/{Guid.NewGuid()}/files", Ct);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
