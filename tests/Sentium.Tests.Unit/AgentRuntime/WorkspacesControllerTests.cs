using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Sentium.AgentRuntime.Api.Controllers;
using Sentium.AgentRuntime.Core.Dtos;
using Sentium.AgentRuntime.Core.Workspaces;
using Sentium.Shared.Results;
using Xunit;

namespace Sentium.Tests.Unit.AgentRuntime;

public sealed class WorkspacesControllerTests
{
    private readonly IWorkspaceService _workspaceService = Substitute.For<IWorkspaceService>();
    private readonly WorkspacesController _controller;

    public WorkspacesControllerTests()
    {
        _controller = new WorkspacesController(_workspaceService);
    }

    private static WorkspaceDto MakeDto(Guid? id = null, string name = "Test Workspace") =>
        new(id ?? Guid.NewGuid(), name, null, 0, DateTime.UtcNow, DateTime.UtcNow);

    [Fact]
    public async Task GetWorkspaces_ReturnsOk_WithPagedResponse()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var workspaces = new List<WorkspaceDto> { MakeDto() };
        var paged = PagedResponse<WorkspaceDto>.Create(workspaces, 1, 1, 20);
        _workspaceService.GetWorkspacesPagedAsync(1, 20, ct).Returns(paged);

        // Act
        var result = await _controller.GetWorkspaces(1, 20, ct);

        // Assert
        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().Be(paged);
    }

    [Fact]
    public async Task GetWorkspace_ReturnsOk_WhenFound()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var dto = MakeDto(id);
        _workspaceService.GetWorkspaceAsync(id, ct).Returns(dto);

        // Act
        var result = await _controller.GetWorkspace(id, ct);

        // Assert
        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().Be(dto);
    }

    [Fact]
    public async Task GetWorkspace_ReturnsNotFound_WhenNull()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        _workspaceService.GetWorkspaceAsync(id, ct).Returns((WorkspaceDto?)null);

        // Act
        var result = await _controller.GetWorkspace(id, ct);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task CreateWorkspace_ReturnsCreated_OnSuccess()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = new CreateWorkspaceRequest("New Workspace", null);
        var dto = MakeDto();
        _workspaceService.CreateWorkspaceAsync(request, ct).Returns(Result<WorkspaceDto>.Success(dto));

        // Act
        var result = await _controller.CreateWorkspace(request, ct);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>()
            .Which.Value.Should().Be(dto);
    }

    [Fact]
    public async Task CreateWorkspace_ReturnsConflict_WhenNameExists()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = new CreateWorkspaceRequest("Existing", null);
        _workspaceService.CreateWorkspaceAsync(request, ct)
            .Returns(Result<WorkspaceDto>.Conflict("A workspace named 'Existing' already exists."));

        // Act
        var result = await _controller.CreateWorkspace(request, ct);

        // Assert
        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task UpdateWorkspace_ReturnsOk_OnSuccess()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var request = new UpdateWorkspaceRequest("Updated Name", null);
        var dto = MakeDto(id, "Updated Name");
        _workspaceService.UpdateWorkspaceAsync(id, request, ct).Returns(Result<WorkspaceDto>.Success(dto));

        // Act
        var result = await _controller.UpdateWorkspace(id, request, ct);

        // Assert
        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().Be(dto);
    }

    [Fact]
    public async Task UpdateWorkspace_ReturnsNotFound_WhenMissing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var request = new UpdateWorkspaceRequest("Updated Name", null);
        _workspaceService.UpdateWorkspaceAsync(id, request, ct).Returns(Result<WorkspaceDto>.NotFound());

        // Act
        var result = await _controller.UpdateWorkspace(id, request, ct);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task UpdateWorkspace_ReturnsConflict_WhenNameTaken()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var request = new UpdateWorkspaceRequest("Taken", null);
        _workspaceService.UpdateWorkspaceAsync(id, request, ct)
            .Returns(Result<WorkspaceDto>.Conflict("Name already exists."));

        // Act
        var result = await _controller.UpdateWorkspace(id, request, ct);

        // Assert
        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task DeleteWorkspace_ReturnsNoContent_WhenDeleted()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        _workspaceService.DeleteWorkspaceAsync(id, ct).Returns(true);

        // Act
        var result = await _controller.DeleteWorkspace(id, ct);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteWorkspace_ReturnsNotFound_WhenMissing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        _workspaceService.DeleteWorkspaceAsync(id, ct).Returns(false);

        // Act
        var result = await _controller.DeleteWorkspace(id, ct);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetWorkspaceFiles_ReturnsOk_WhenFound()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        IReadOnlyList<WorkspaceFileDto> files = [];
        _workspaceService.GetWorkspaceFilesAsync(id, ct).Returns(files);

        // Act
        var result = await _controller.GetWorkspaceFiles(id, ct);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetWorkspaceFiles_ReturnsNotFound_WhenWorkspaceMissing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        _workspaceService.GetWorkspaceFilesAsync(id, ct).Returns((IReadOnlyList<WorkspaceFileDto>?)null);

        // Act
        var result = await _controller.GetWorkspaceFiles(id, ct);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }
}
