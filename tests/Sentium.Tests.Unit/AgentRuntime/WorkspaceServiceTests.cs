using FluentAssertions;
using NSubstitute;
using Sentium.AgentRuntime.Application.WorkspaceManagement;
using Sentium.AgentRuntime.Core.Dtos;
using Sentium.AgentRuntime.Core.Storage;
using Sentium.AgentRuntime.Core.Workspaces;
using Sentium.Shared.Results;
using Sentium.Tests.Unit.Common;
using Xunit;

namespace Sentium.Tests.Unit.AgentRuntime;

public sealed class WorkspaceServiceTests
{
    private readonly IWorkspaceRepository _repository = Substitute.For<IWorkspaceRepository>();
    private readonly ILocalFileService _fileService = Substitute.For<ILocalFileService>();
    private readonly SpyEventBus _eventBus = new();
    private readonly WorkspaceService _sut;

    public WorkspaceServiceTests()
    {
        _sut = new WorkspaceService(_repository, _fileService, _eventBus, new PassThroughScopedCache());
    }

    private static WorkspaceDto MakeDto(Guid? id = null, string name = "Test Workspace") =>
        new(id ?? Guid.NewGuid(), name, null, 0, DateTime.UtcNow, DateTime.UtcNow);

    private static WorkspaceFileDto MakeFileDto(Guid? id = null) =>
        new(id ?? Guid.NewGuid(), "test.pdf", ".pdf", 100, null, "Pending", DateTime.UtcNow);

    [Fact]
    public async Task GetWorkspacesAsync_ReturnsList_WhenCalled()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var workspaces = new List<WorkspaceDto> { MakeDto() };
        _repository.GetWorkspacesAsync(ct).Returns(workspaces);

        // Act
        var result = await _sut.GetWorkspacesAsync(ct);

        // Assert
        result.Should().BeEquivalentTo(workspaces);
    }

    [Fact]
    public async Task GetWorkspaceAsync_ReturnsDto_WhenFound()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var dto = MakeDto(id);
        _repository.GetWorkspaceAsync(id, ct).Returns(dto);

        // Act
        var result = await _sut.GetWorkspaceAsync(id, ct);

        // Assert
        result.Should().Be(dto);
    }

    [Fact]
    public async Task GetWorkspaceAsync_ReturnsNull_WhenNotFound()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        _repository.GetWorkspaceAsync(id, ct).Returns((WorkspaceDto?)null);

        // Act
        var result = await _sut.GetWorkspaceAsync(id, ct);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateWorkspaceAsync_ReturnsConflict_WhenNameExists()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = new CreateWorkspaceRequest("Existing Workspace", null);
        _repository.NameExistsAsync(request.Name, ct: ct).Returns(true);

        // Act
        var result = await _sut.CreateWorkspaceAsync(request, ct);

        // Assert
        result.Status.Should().Be(ResultStatus.Conflict);
        result.Error.Should().Contain("Existing Workspace");
        await _repository.DidNotReceive().CreateWorkspaceAsync(Arg.Any<CreateWorkspaceRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateWorkspaceAsync_ReturnsSuccess_WhenNameUnique()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = new CreateWorkspaceRequest("New Workspace", null);
        var created = MakeDto(name: "New Workspace");
        _repository.NameExistsAsync(request.Name, ct: ct).Returns(false);
        _repository.CreateWorkspaceAsync(request, ct).Returns(created);

        // Act
        var result = await _sut.CreateWorkspaceAsync(request, ct);

        // Assert
        result.Status.Should().Be(ResultStatus.Success);
        result.Value.Should().Be(created);
    }

    [Fact]
    public async Task UpdateWorkspaceAsync_ReturnsNotFound_WhenWorkspaceDoesNotExist()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var request = new UpdateWorkspaceRequest("Updated", null);
        _repository.ExistsAsync(id, ct).Returns(false);

        // Act
        var result = await _sut.UpdateWorkspaceAsync(id, request, ct);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task UpdateWorkspaceAsync_ReturnsConflict_WhenNameTakenByAnother()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var request = new UpdateWorkspaceRequest("Taken Name", null);
        _repository.ExistsAsync(id, ct).Returns(true);
        _repository.NameExistsAsync(request.Name, excludeId: id, ct: ct).Returns(true);

        // Act
        var result = await _sut.UpdateWorkspaceAsync(id, request, ct);

        // Assert
        result.Status.Should().Be(ResultStatus.Conflict);
    }

    [Fact]
    public async Task UpdateWorkspaceAsync_ReturnsSuccess_WhenValid()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var request = new UpdateWorkspaceRequest("Updated Name", null);
        var updated = MakeDto(id, "Updated Name");
        _repository.ExistsAsync(id, ct).Returns(true);
        _repository.NameExistsAsync(request.Name, excludeId: id, ct: ct).Returns(false);
        _repository.UpdateWorkspaceAsync(id, request, ct).Returns(updated);

        // Act
        var result = await _sut.UpdateWorkspaceAsync(id, request, ct);

        // Assert
        result.Status.Should().Be(ResultStatus.Success);
        result.Value.Should().Be(updated);
    }

    [Fact]
    public async Task DeleteWorkspaceAsync_ReturnsFalse_WhenNotFound()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        _repository.ExistsAsync(id, ct).Returns(false);

        // Act
        var result = await _sut.DeleteWorkspaceAsync(id, ct);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteWorkspaceAsync_ReturnsTrue_WhenFound()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        _repository.ExistsAsync(id, ct).Returns(true);

        // Act
        var result = await _sut.DeleteWorkspaceAsync(id, ct);

        // Assert
        result.Should().BeTrue();
        await _repository.Received(1).DeleteWorkspaceAsync(id, ct);
    }

    [Fact]
    public async Task GetWorkspaceFilesAsync_ReturnsNull_WhenWorkspaceNotFound()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        _repository.ExistsAsync(id, ct).Returns(false);

        // Act
        var result = await _sut.GetWorkspaceFilesAsync(id, ct);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetWorkspaceFilesAsync_ReturnsList_WhenWorkspaceFound()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        IReadOnlyList<WorkspaceFileDto> files = [MakeFileDto()];
        _repository.ExistsAsync(id, ct).Returns(true);
        _repository.GetWorkspaceFilesAsync(id, ct).Returns(files);

        // Act
        var result = await _sut.GetWorkspaceFilesAsync(id, ct);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task UploadFileAsync_ReturnsNull_WhenWorkspaceNotFound()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var workspaceId = Guid.NewGuid();
        _repository.ExistsAsync(workspaceId, ct).Returns(false);

        using var stream = new MemoryStream([1, 2, 3]);

        // Act
        var result = await _sut.UploadFileAsync(stream, "test.pdf", workspaceId, ct);

        // Assert
        result.Should().BeNull();
        await _fileService.DidNotReceive().UploadToWorkspaceAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadFileAsync_PublishesEvent_WhenSuccessful()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var blobId = Guid.NewGuid();
        var fileDto = MakeFileDto();
        _fileService.UploadToWorkspaceAsync(Arg.Any<Stream>(), "test.pdf", null, ct).Returns(blobId);
        _repository.AddFileRecordAsync(Arg.Any<AddFileRecord>(), ct).Returns(fileDto);

        using var stream = new MemoryStream([1, 2, 3]);

        // Act
        var result = await _sut.UploadFileAsync(stream, "test.pdf", null, ct);

        // Assert
        result.Should().NotBeNull();
        _eventBus.PublishedSubjects.Should().ContainSingle(s => s == "internal.file.ingested");
    }
}
