using Sentium.AgentRuntime.Core.Files;
using Sentium.AgentRuntime.Core.Workspaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sentium.AgentRuntime.Api.Controllers;

/// <summary>
/// API controller for managing files within workspaces.
/// </summary>
/// <remarks>
/// <para>
/// This controller handles file-level operations:
/// - Upload files to workspaces with validation.
/// - Query files by workspace.
/// - Delete files from workspaces.
/// </para>
/// <para>
/// All operations require authentication. Files are validated against <see cref="AllowedFileTypes"/> and limited to 100 MB.
/// Uploaded files are automatically queued for RAG ingestion via the background worker.
/// </para>
/// </remarks>
[ApiController]
[Authorize]
[Route("workspace")]
public sealed class WorkspaceController(IWorkspaceService workspaceService) : ControllerBase
{
    /// <summary>
    /// Maximum allowed file upload size: 100 MB.
    /// </summary>
    private const long MaxFileSizeBytes = 100 * 1024 * 1024;

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
