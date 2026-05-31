using Sentium.AgentRuntime.Core.Dtos;
using Sentium.AgentRuntime.Core.Workspaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.AgentRuntime.Core.Files;

namespace Sentium.AgentRuntime.Api.Controllers;

/// <summary>
/// API controller for managing workspaces.
/// </summary>
[ApiController]
[Authorize]
[Route("workspaces")]
public sealed class WorkspacesController(IWorkspaceService workspaceService) : ControllerBase
{
    private const long MaxFileSizeBytes = 100 * 1024 * 1024;

    /// <summary>
    /// Retrieves all workspaces.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>An HTTP 200 response containing a list of workspace DTOs.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWorkspaces(CancellationToken ct)
    {
        var workspaces = await workspaceService.GetWorkspacesAsync(ct);
        return Ok(workspaces);
    }

    /// <summary>
    /// Retrieves a specific workspace by its identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the workspace.</param>
    /// <param name="ct">A cancellation token.</param>
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

    /// <summary>
    /// Creates a new workspace.
    /// </summary>
    /// <param name="request">The creation request containing workspace name and optional description.</param>
    /// <param name="ct">A cancellation token.</param>
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
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "Workspace name is required." });
        }

        var workspace = await workspaceService.CreateWorkspaceAsync(request, ct);
        if (workspace is null)
        {
            return Conflict(new { error = $"A workspace named '{request.Name}' already exists." });
        }

        return CreatedAtAction(nameof(GetWorkspace), new { id = workspace.Id }, workspace);
    }

    /// <summary>
    /// Updates an existing workspace's metadata.
    /// </summary>
    /// <param name="id">The identifier of the workspace to update.</param>
    /// <param name="request">The update request containing new name and optional description.</param>
    /// <param name="ct">A cancellation token.</param>
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
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "Workspace name is required." });
        }

        try
        {
            var workspace = await workspaceService.UpdateWorkspaceAsync(id, request, ct);
            if (workspace is null)
            {
                return NotFound();
            }

            return Ok(workspace);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Deletes a workspace.
    /// </summary>
    /// <param name="id">The identifier of the workspace to delete.</param>
    /// <param name="ct">A cancellation token.</param>
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
    /// <param name="id">The identifier of the workspace.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>
    /// An HTTP 200 OK response containing a list of file DTOs;
    /// or an HTTP 404 Not Found response if the workspace does not exist.
    /// </returns>
    [HttpGet("{id:guid}/files")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWorkspaceFiles(Guid id, CancellationToken ct)
    {
        try
        {
            var files = await workspaceService.GetWorkspaceFilesAsync(id, ct);
            return Ok(files);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Uploads a file to a workspace.
    /// </summary>
    /// <remarks>
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
    /// </remarks>
    /// <param name="file">The file to upload (multipart form data).</param>
    /// <param name="workspaceId">Optional workspace ID to associate with the file. If null, the file is not workspace-specific.</param>
    /// <param name="ct">A cancellation token.</param>
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
            return BadRequest(new { error = "A non-empty file is required." });
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return BadRequest(new { error = "File exceeds the 100 MB size limit." });
        }

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedFileTypes.IsAllowed(extension))
        {
            return BadRequest(new { error = $"File type '{extension}' is not supported. Allowed types: {AllowedFileTypes.AllowedList}." });
        }

        using var stream = file.OpenReadStream();
        var fileDto = await workspaceService.UploadFileAsync(stream, file.FileName, workspaceId, ct);

        if (fileDto is null)
        {
            return BadRequest(new { error = $"Workspace '{workspaceId}' not found." });
        }

        return CreatedAtAction(nameof(GetFiles), new { workspaceId }, fileDto);
    }

    /// <summary>
    /// Retrieves files, optionally filtered by workspace.
    /// </summary>
    /// <param name="workspaceId">Optional workspace ID to filter files. If null, returns all files.</param>
    /// <param name="ct">A cancellation token.</param>
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
    /// <param name="ct">A cancellation token.</param>
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
