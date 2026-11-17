using FluentAssertions;
using NSubstitute;
using Sentium.Codex.Contracts;
using Sentium.Core.Results;
using Sentium.Praxis.Features.Storage;
using Sentium.Praxis.Features.Workspaces;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Workspaces;

public sealed class WorkspaceServiceTests
{
    private readonly IWorkspaceRepository _repository = Substitute.For<IWorkspaceRepository>();
    private readonly ILocalFileService _fileService = Substitute.For<ILocalFileService>();
    private readonly IWorkspaceFileTextReader _textReader = Substitute.For<IWorkspaceFileTextReader>();
    private readonly IFileIngestionQueue _ingestionQueue = Substitute.For<IFileIngestionQueue>();
    private readonly IDocumentIngestionService _ingestion = Substitute.For<IDocumentIngestionService>();
    private readonly WorkspaceService _sut;

    public WorkspaceServiceTests()
    {
        _sut = new WorkspaceService(_repository, _fileService, _textReader, _ingestionQueue, _ingestion, new PassThroughScopedCache());
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
    public async Task UploadFileAsync_QueuesIngestion_WhenSuccessful()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var blobId = Guid.NewGuid();
        var fileDto = MakeFileDto();
        var defaultWorkspaceId = Guid.NewGuid();
        _repository.GetOrCreateIdAsync(WorkspaceService.DefaultWorkspaceName, Arg.Any<string>(), ct).Returns(defaultWorkspaceId);
        _fileService.UploadToWorkspaceAsync(Arg.Any<Stream>(), "test.pdf", defaultWorkspaceId, ct).Returns(blobId);
        _repository.AddFileRecordAsync(Arg.Any<AddFileRecord>(), ct).Returns(fileDto);

        using var stream = new MemoryStream([1, 2, 3]);

        // Act
        var result = await _sut.UploadFileAsync(stream, "test.pdf", null, ct);

        // Assert
        result.Should().NotBeNull();
        await _repository.Received(1).AddFileRecordAsync(Arg.Is<AddFileRecord>(r => r.WorkspaceId == defaultWorkspaceId && r.BlobName == blobId), ct);
        _ingestionQueue.Received(1).Enqueue(new FileIngestionRequest(fileDto.Id, fileDto.WorkspaceId));
    }

    [Fact]
    public async Task DeleteFileAsync_RemovesTheFilesKnowledgeChunks_BlobAndRecord()
    {
        var ct = TestContext.Current.CancellationToken;
        var fileId = Guid.NewGuid();
        var blobName = Guid.NewGuid();
        _repository.GetStoredFileAsync(fileId, ct).Returns(new StoredFileRecord(fileId, "notes.md", blobName, ".md", null));

        var deleted = await _sut.DeleteFileAsync(fileId, ct);

        deleted.Should().BeTrue();
        await _ingestion.Received(1).RemoveDocumentAsync(WorkspaceFileMetadata.FileId, fileId.ToString(), null, ct);
        await _fileService.Received(1).DeleteFileAsync(blobName, ct);
        await _repository.Received(1).DeleteFileRecordAsync(fileId, ct);
    }

    [Fact]
    public async Task DeleteWorkspaceAsync_RemovesEveryFile_BeforeTheWorkspace()
    {
        var ct = TestContext.Current.CancellationToken;
        var workspaceId = Guid.NewGuid();
        var first = new StoredFileRecord(Guid.NewGuid(), "a.md", Guid.NewGuid(), ".md", workspaceId);
        var second = new StoredFileRecord(Guid.NewGuid(), "b.pdf", Guid.NewGuid(), ".pdf", workspaceId);
        _repository.ExistsAsync(workspaceId, ct).Returns(true);
        _repository.GetStoredFilesAsync(workspaceId, ct).Returns([first, second]);

        var deleted = await _sut.DeleteWorkspaceAsync(workspaceId, ct);

        deleted.Should().BeTrue();
        foreach (var file in new[] { first, second })
        {
            await _ingestion.Received(1).RemoveDocumentAsync(WorkspaceFileMetadata.FileId, file.FileId.ToString(), null, ct);
            await _fileService.Received(1).DeleteFileAsync(file.BlobName, ct);
            await _repository.Received(1).DeleteFileRecordAsync(file.FileId, ct);
        }

        Received.InOrder(() =>
        {
            _repository.DeleteFileRecordAsync(second.FileId, ct);
            _repository.DeleteWorkspaceAsync(workspaceId, ct);
        });
    }

    [Fact]
    public async Task ReindexFileAsync_ResetsAndQueuesTheFile()
    {
        var ct = TestContext.Current.CancellationToken;
        var file = new StoredFileRecord(Guid.NewGuid(), "a.md", Guid.NewGuid(), ".md", Guid.NewGuid());
        _repository.GetStoredFileAsync(file.FileId, ct).Returns(file);
        _repository.ResetForReindexAsync(file.FileId, ct).Returns(true);

        var queued = await _sut.ReindexFileAsync(file.FileId, ct);

        queued.Should().BeTrue();
        _ingestionQueue.Received(1).Enqueue(new FileIngestionRequest(file.FileId, file.WorkspaceId));
    }

    [Fact]
    public async Task ReindexFileAsync_ReturnsFalse_ForAnUnknownFile()
    {
        var ct = TestContext.Current.CancellationToken;

        var queued = await _sut.ReindexFileAsync(Guid.NewGuid(), ct);

        queued.Should().BeFalse();
        _ingestionQueue.DidNotReceive().Enqueue(Arg.Any<FileIngestionRequest>());
    }

    [Fact]
    public async Task GetFileTextAsync_CutsAtMaxChars_AndReportsTheTotal()
    {
        var ct = TestContext.Current.CancellationToken;
        var file = new StoredFileRecord(Guid.NewGuid(), "a.md", Guid.NewGuid(), ".md", null);
        _repository.GetStoredFileAsync(file.FileId, ct).Returns(file);
        _textReader.ReadAsync(file.BlobName, ".md", ct).Returns("0123456789");

        var result = await _sut.GetFileTextAsync(file.FileId, 4, ct);

        result.Status.Should().Be(ResultStatus.Success);
        result.Value.Should().BeEquivalentTo(new { Text = "0123", Truncated = true, TotalChars = 10 });
    }

    [Fact]
    public async Task GetFileTextAsync_IsInvalid_WhenTheDocumentCannotBeParsed()
    {
        var ct = TestContext.Current.CancellationToken;
        var file = new StoredFileRecord(Guid.NewGuid(), "a.pdf", Guid.NewGuid(), ".pdf", null);
        _repository.GetStoredFileAsync(file.FileId, ct).Returns(file);
        _textReader.ReadAsync(file.BlobName, ".pdf", ct).Returns<string>(_ => throw new InvalidDataException("The PDF could not be read."));

        var result = await _sut.GetFileTextAsync(file.FileId, 100, ct);

        result.Status.Should().Be(ResultStatus.Invalid);
    }
}
