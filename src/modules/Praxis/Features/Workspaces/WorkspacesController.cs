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
    /// Most recent first.
    /// </summary>
    /// <param name="page">1-based.</param>
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

    [HttpGet("{id:guid}/files")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWorkspaceFiles(Guid id, CancellationToken ct)
    {
        var files = await workspaceService.GetWorkspaceFilesAsync(id, ct);
        return files is null ? NotFound() : Ok(files);
    }

    /// <summary>
    /// A page of a workspace's files, newest first.
    /// </summary>
    /// <param name="page">1-based.</param>
    /// <param name="search">Text the file name must contain (case-insensitive).</param>
    [HttpGet("{id:guid}/files/page")]
    [ProducesResponseType(typeof(PagedResponse<WorkspaceFileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWorkspaceFilesPage(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationQuery.DefaultPageSize,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var files = await workspaceService.GetWorkspaceFilesPageAsync(id, page, pageSize, search, ct);
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
    /// Stores the file as <see cref="FileProcessingStatus.Pending"/>; the ingestion worker indexes it in the background.
    /// </summary>
    /// <param name="workspaceId">Without one the file goes to the user's "Chat uploads" workspace.</param>
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
    /// Newest first.
    /// </summary>
    /// <param name="workspaceId">All workspaces when omitted.</param>
    /// <param name="search">Text the file name must contain (case-insensitive).</param>
    [HttpGet("files")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFiles([FromQuery] Guid? workspaceId, [FromQuery] string? search, CancellationToken ct)
    {
        var files = await workspaceService.GetFilesAsync(workspaceId, search, ct);
        return Ok(files);
    }

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
