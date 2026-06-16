using System.Net;
using System.Net.Http.Json;
using Azure.Storage.Blobs;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Sentium.AgentRuntime.Core.Dtos;
using Sentium.AgentRuntime.Core.Storage;
using Sentium.Shared.Results;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.AgentRuntime;

public sealed class WorkspaceApiTests(WorkspaceApiFactory factory) : IClassFixture<WorkspaceApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateWorkspace_ReturnsCreated_WithDto()
    {
        var request = new CreateWorkspaceRequest($"Workspace-{Guid.NewGuid()}", "test workspace");

        var response = await _client.PostAsJsonAsync("/workspaces", request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<WorkspaceDto>(Ct);
        dto.Should().NotBeNull();
        dto!.Name.Should().Be(request.Name);
    }

    [Fact]
    public async Task GetWorkspaces_AfterCreation_IncludesNew()
    {
        var name = $"ListWs-{Guid.NewGuid()}";
        await _client.PostAsJsonAsync("/workspaces", new CreateWorkspaceRequest(name, null), Ct);

        var response = await _client.GetAsync("/workspaces", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged = await response.Content.ReadFromJsonAsync<PagedResponse<WorkspaceDto>>(Ct);
        paged!.Items.Should().Contain(w => w.Name == name);
    }

    [Fact]
    public async Task CreateWorkspace_Returns409_WhenNameDuplicate()
    {
        var name = $"DupWs-{Guid.NewGuid()}";
        await _client.PostAsJsonAsync("/workspaces", new CreateWorkspaceRequest(name, null), Ct);

        var response = await _client.PostAsJsonAsync("/workspaces", new CreateWorkspaceRequest(name, null), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateWorkspace_Returns200_WithUpdatedDto()
    {
        var createResp = await _client.PostAsJsonAsync("/workspaces", new CreateWorkspaceRequest($"Original-{Guid.NewGuid()}", null), Ct);
        var created = await createResp.Content.ReadFromJsonAsync<WorkspaceDto>(Ct);

        var newName = $"Updated-{Guid.NewGuid()}";
        var response = await _client.PutAsJsonAsync($"/workspaces/{created!.Id}", new UpdateWorkspaceRequest(newName, "updated desc"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<WorkspaceDto>(Ct);
        updated!.Name.Should().Be(newName);
    }

    [Fact]
    public async Task DeleteWorkspace_Returns204_ThenGetReturns404()
    {
        var createResp = await _client.PostAsJsonAsync("/workspaces", new CreateWorkspaceRequest($"ToDelete-{Guid.NewGuid()}", null), Ct);
        var created = await createResp.Content.ReadFromJsonAsync<WorkspaceDto>(Ct);

        var deleteResp = await _client.DeleteAsync($"/workspaces/{created!.Id}", Ct);
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResp = await _client.GetAsync($"/workspaces/{created.Id}", Ct);
        getResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetWorkspaceFiles_Returns200_WhenWorkspaceExists()
    {
        var createResp = await _client.PostAsJsonAsync("/workspaces", new CreateWorkspaceRequest($"FilesWs-{Guid.NewGuid()}", null), Ct);
        var created = await createResp.Content.ReadFromJsonAsync<WorkspaceDto>(Ct);

        var response = await _client.GetAsync($"/workspaces/{created!.Id}/files", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetWorkspaceFiles_Returns404_WhenWorkspaceNotFound()
    {
        var response = await _client.GetAsync($"/workspaces/{Guid.NewGuid()}/files", Ct);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

public sealed class WorkspaceApiFactory : SentiumWebApplicationFactory<Sentium.AgentRuntime.Api.Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ILocalFileService>();
            services.RemoveAll<BlobServiceClient>();
            services.AddSingleton(Substitute.For<ILocalFileService>());
            services.AddSingleton(Substitute.For<BlobServiceClient>());
        });
    }
}
