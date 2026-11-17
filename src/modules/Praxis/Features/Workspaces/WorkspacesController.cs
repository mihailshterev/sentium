using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Core.Results;
using Sentium.Infrastructure.Results;
using Sentium.Praxis.Features.Files;

namespace Sentium.Praxis.Features.Workspaces;

[ApiController]
[Authorize]
[Route($"{PraxisRoutes.Prefix}/workspaces")]
internal sealed class WorkspacesController(IWorkspaceService workspaceService) : ControllerBase
{
    private const int DefaultPreviewChars = 20_000;
    private const int MaxPreviewChars = 200_000;

    /// <summary>
    /// Retrieves a page of workspaces (most recent first).
    /// </summary>
    /// <param name="page">1-based page number (default: 1).</param>
    /// <param name="pageSize">Number of items per page (default: 20, max: 100).</param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<WorkspaceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWorkspaces(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationQuery.DefaultPageSize,
        CancellationToken ct = default)
    {
        var workspaces = await workspaceService.GetWorkspacesPagedAsync(page, pageSize, ct);
        return Ok(workspaces);
    }

    /// <summary>
    /// Retrieves a specific workspace by its identifier.
    /// </summary>
    /// <returns>
    /// An HTTP 200 response containing the workspace DTO if found;
    /// otherwise an HTTP 404 Not Found response.
    /// </returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWorkspace(Guid id, CancellationToken ct)
    {
        var workspace = await workspaceService.GetWorkspaceAsync(id, ct);
        if (workspace is null)
        {
            return NotFound();
        }

        return Ok(workspace);
    }

    /// <returns>
    /// An HTTP 201 Created response with the new workspace DTO;
    /// an HTTP 400 Bad Request if the name is empty;
    /// or an HTTP 409 Conflict if a workspace with this name already exists.
    /// </returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateWorkspace([FromBody] CreateWorkspaceRequest request, CancellationToken ct)
    {
        var result = await workspaceService.CreateWorkspaceAsync(request, ct);
        if (result.Status == ResultStatus.Conflict)
        {
            return Conflict(new ProblemDetails { Title = "Conflict", Detail = result.Error, Status = StatusCodes.Status409Conflict });
        }

        return CreatedAtAction(nameof(GetWorkspace), new { id = result.Value!.Id }, result.Value);
    }

    /// <summary>
    /// Updates an existing workspace's metadata.
    /// </summary>
    /// <returns>
    /// An HTTP 200 OK response with the updated workspace DTO;
    /// an HTTP 400 Bad Request if the name is empty;
    /// an HTTP 404 Not Found if the workspace does not exist;
    /// or an HTTP 409 Conflict if another workspace holds the same name.
    /// </returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateWorkspace(Guid id, [FromBody] UpdateWorkspaceRequest request, CancellationToken ct)
    {
        var result = await workspaceService.UpdateWorkspaceAsync(id, request, ct);
        return result.ToActionResult(this);
    }

    /// <returns>
    /// An HTTP 204 No Content response if the workspace was successfully deleted;
    /// or an HTTP 404 Not Found response if the workspace does not exist.
    /// </returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteWorkspace(Guid id, CancellationToken ct)
    {
        var deleted = await workspaceService.DeleteWorkspaceAsync(id, ct);
        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    /// <summary>
    /// Retrieves all files within a specific workspace.
    /// </summary>
    /// <returns>
    /// An HTTP 200 OK response containing a list of file DTOs;
    /// or an HTTP 404 Not Found response if the workspace does not exist.
    /// </returns>
    [HttpGet("{id:guid}/files")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWorkspaceFiles(Guid id, CancellationToken ct)
    {
        var files = await workspaceService.GetWorkspaceFilesAsync(id, ct);
        return files is null ? NotFound() : Ok(files);
    }

    /// <summary>
    /// The upload rules the portal validates against before sending a file.
    /// </summary>
    [HttpGet("files/rules")]
    [ProducesResponseType(typeof(UploadRulesResponse), StatusCodes.Status200OK)]
    public IActionResult GetUploadRules()
    {
        return Ok(new UploadRulesResponse([.. AllowedFileTypes.Extensions.Order()], AllowedFileTypes.MaxFileSizeBytes));
    }

    /// <summary>
    /// <para>
    /// The file is validated for:
    /// - Non-zero size.
    /// - Size not exceeding <see cref="AllowedFileTypes.MaxFileSizeBytes"/>.
    /// - A file name within <see cref="AllowedFileTypes.MaxFileNameLength"/> characters.
    /// - Extension in the allowed list (see <see cref="AllowedFileTypes"/>).
    /// </para>
    /// <para>
    /// Upon successful upload, the file is stored in blob storage and its metadata is recorded in the database
    /// with an initial <see cref="FileProcessingStatus.Pending"/> status.
    /// The background ingestion worker will process the file asynchronously for RAG indexing.
    /// </para>
    /// </summary>
    /// <param name="file">The file to upload (multipart form data).</param>
    /// <param name="workspaceId">The workspace to add the file to; without one it goes to the user's "Chat uploads" workspace.</param>
    /// <returns>
    /// An HTTP 201 Created response with the file metadata DTO if successful;
    /// an HTTP 400 Bad Request if validation fails;
    /// or an HTTP 404 Not Found if the workspace does not exist.
    /// </returns>
    [HttpPost("files")]
    [RequestSizeLimit(AllowedFileTypes.MaxFileSizeBytes)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadFile(IFormFile file, [FromForm] Guid? workspaceId, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return Problem(detail: "A non-empty file is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (file.Length > AllowedFileTypes.MaxFileSizeBytes)
        {
            return Problem(detail: "File exceeds the 100 MB size limit.", statusCode: StatusCodes.Status400BadRequest);
        }

        var fileName = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > AllowedFileTypes.MaxFileNameLength)
        {
            return Problem(detail: $"The file name must be 1-{AllowedFileTypes.MaxFileNameLength} characters long.", statusCode: StatusCodes.Status400BadRequest);
        }

        var extension = Path.GetExtension(fileName);
        if (!AllowedFileTypes.IsAllowed(extension))
        {
            return Problem(detail: $"File type '{extension}' is not supported. Allowed types: {AllowedFileTypes.AllowedList}.", statusCode: StatusCodes.Status400BadRequest);
        }

        await using var stream = file.OpenReadStream();
        var fileDto = await workspaceService.UploadFileAsync(stream, fileName, workspaceId, ct);

        if (fileDto is null)
        {
            return NotFound();
        }

        return CreatedAtAction(nameof(GetFileContent), new { id = fileDto.Id }, fileDto);
    }

    /// <summary>
    /// Retrieves files, optionally filtered by workspace.
    /// </summary>
    /// <param name="workspaceId">Optional workspace ID to filter files. If null, returns all files.</param>
    /// <returns>
    /// An HTTP 200 OK response containing a list of file DTOs,  sorted by creation date (most recent first).
    /// </returns>
    [HttpGet("files")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFiles([FromQuery] Guid? workspaceId, CancellationToken ct)
    {
        var files = await workspaceService.GetFilesAsync(workspaceId, ct);
        return Ok(files);
    }

    /// <summary>
    /// Deletes a file from its workspace and blob storage.
    /// </summary>
    /// <returns>
    /// An HTTP 204 No Content response if the file was successfully deleted;
    /// or an HTTP 404 Not Found response if the file does not exist.
    /// </returns>
    [HttpDelete("files/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteFile(Guid id, CancellationToken ct)
    {
        var deleted = await workspaceService.DeleteFileAsync(id, ct);
        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    /// <summary>
    /// Queues the file for ingestion again, e.g. after it failed.
    /// </summary>
    [HttpPost("files/{id:guid}/reindex")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReindexFile(Guid id, CancellationToken ct)
    {
        return await workspaceService.ReindexFileAsync(id, ct) ? Accepted() : NotFound();
    }

    /// <summary>
    /// Downloads the file as uploaded. Always sent as an attachment, and text files as <c>text/plain</c>, so an
    /// uploaded HTML or script file is never rendered on the portal's origin.
    /// </summary>
    [HttpGet("files/{id:guid}/content")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFileContent(Guid id, CancellationToken ct)
    {
        var content = await workspaceService.OpenFileAsync(id, ct);
        if (content is null)
        {
            return NotFound();
        }

        return File(content.Content, DownloadContentType(content.Extension), content.FileName);
    }

    /// <summary>
    /// The file's extracted text for previewing, cut at <paramref name="maxChars"/> characters.
    /// </summary>
    [HttpGet("files/{id:guid}/text")]
    [ProducesResponseType(typeof(WorkspaceFileText), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFileText(Guid id, [FromQuery] int maxChars = DefaultPreviewChars, CancellationToken ct = default)
    {
        var result = await workspaceService.GetFileTextAsync(id, Math.Clamp(maxChars, 1, MaxPreviewChars), ct);
        return result.ToActionResult(this);
    }

    private static string DownloadContentType(string extension)
    {
        if (string.Equals(extension, AllowedFileTypes.Pdf, StringComparison.OrdinalIgnoreCase))
        {
            return "application/pdf";
        }

        if (string.Equals(extension, AllowedFileTypes.Docx, StringComparison.OrdinalIgnoreCase))
        {
            return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        }

        return "text/plain; charset=utf-8";
    }
}
