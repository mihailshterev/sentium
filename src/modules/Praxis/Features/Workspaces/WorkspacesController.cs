using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Core.Results;
using Sentium.Infrastructure.Results;
using Sentium.Praxis.Features.Files;
using Sentium.Praxis.Features.Workspaces;

namespace Sentium.Praxis.Features.Workspaces;

[ApiController]
[Authorize]
[Route($"{PraxisRoutes.Prefix}/workspaces")]
public sealed class WorkspacesController(IWorkspaceService workspaceService) : ControllerBase
{
    private const long MaxFileSizeBytes = 100 * 1024 * 1024;

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

    /// <param name="request">The creation request containing workspace name and optional description.</param>
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
    /// <param name="id">The identifier of the workspace to update.</param>
    /// <param name="request">The update request containing new name and optional description.</param>
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

    /// <param name="id">The identifier of the workspace to delete.</param>
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
    /// <para>
    /// The file is validated for:
    /// - Non-zero size.
    /// - Size not exceeding 100 MB.
    /// - Extension in the allowed list (see <see cref="AllowedFileTypes"/>).
    /// </para>
    /// <para>
    /// Upon successful upload, the file is stored in blob storage and its metadata is recorded in the database
    /// with an initial <see cref="Core.Files.FileProcessingStatus.Pending"/> status.
    /// The background ingestion worker will process the file asynchronously for RAG indexing.
    /// </para>
    /// </summary>
    /// <param name="file">The file to upload (multipart form data).</param>
    /// <param name="workspaceId">Optional workspace ID to associate with the file. If null, the file is not workspace-specific.</param>
    /// <returns>
    /// An HTTP 201 Created response with the file metadata DTO if successful;
    /// an HTTP 400 Bad Request if validation fails (empty file, exceeds size, disallowed extension, or workspace not found).
    /// </returns>
    [HttpPost("files")]
    [RequestSizeLimit(100 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadFile(IFormFile file, [FromForm] Guid? workspaceId, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return Problem(detail: "A non-empty file is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return Problem(detail: "File exceeds the 100 MB size limit.", statusCode: StatusCodes.Status400BadRequest);
        }

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedFileTypes.IsAllowed(extension))
        {
            return Problem(detail: $"File type '{extension}' is not supported. Allowed types: {AllowedFileTypes.AllowedList}.", statusCode: StatusCodes.Status400BadRequest);
        }

        using var stream = file.OpenReadStream();
        var fileDto = await workspaceService.UploadFileAsync(stream, file.FileName, workspaceId, ct);

        if (fileDto is null)
        {
            return NotFound();
        }

        return CreatedAtAction(nameof(GetFiles), new { workspaceId }, fileDto);
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
    /// <param name="id">The unique identifier of the file to delete.</param>
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
}
