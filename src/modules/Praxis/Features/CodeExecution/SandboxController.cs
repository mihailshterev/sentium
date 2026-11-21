using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Sentium.Core.Results;
using Sentium.Infrastructure.Storage;
using Sentium.Praxis.Contracts;

namespace Sentium.Praxis.Features.CodeExecution;

/// <summary>
/// Sandbox execution history and artifact downloads. Code runs in the separate Sentium.Sandbox service;
/// Praxis records every run it dispatches (see <see cref="ISandboxClient"/>) and serves the history here.
/// The routes use the <c>/api/sandbox</c> prefix the portal calls.
/// </summary>
[ApiController]
[Authorize]
[Route(SandboxRoutePrefix)]
internal sealed class SandboxController(
    IExecutionLogRepository executionLog,
    IObjectStore objectStore,
    IOptions<SandboxArtifactOptions> options) : ControllerBase
{
    private const string SandboxRoutePrefix = "api/sandbox";

    private readonly SandboxArtifactOptions _opts = options.Value;

    /// <summary>
    /// Returns a page of sandbox execution records (newest first), optionally filtered by outcome,
    /// language, or a free-text term matched against agent id / job id.
    /// Each record includes the original request context (code, agent ID, prompt) and the full result.
    /// </summary>
    /// <param name="page">1-based page number (default: 1).</param>
    /// <param name="pageSize">Number of records per page (default: 20, max: 100).</param>
    /// <param name="status">Optional outcome filter: Succeeded, Failed, or Denied.</param>
    /// <param name="language">Optional language filter: Python or Node.</param>
    /// <param name="search">Optional term matched against agent id or job id.</param>
    [HttpGet("executions")]
    [ProducesResponseType<PagedResponse<SandboxExecutionLogRecord>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExecutions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? language = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = new ExecutionLogQuery
        {
            Page = page,
            PageSize = pageSize,
            Status = Enum.TryParse<ExecutionStatusFilter>(status, ignoreCase: true, out var s) ? s : null,
            Language = Enum.TryParse<ExecutionLanguage>(language, ignoreCase: true, out var l) ? l : null,
            Search = string.IsNullOrWhiteSpace(search) ? null : search
        };

        var (entries, totalCount) = await executionLog.GetPagedAsync(query, ct);

        var items = entries.Select(ToRecord).ToList();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return Ok(new PagedResponse<SandboxExecutionLogRecord>(items, totalCount, page, pageSize, totalPages));
    }

    /// <summary>
    /// Returns a single sandbox execution record by its job id.
    /// </summary>
    /// <response code="404">If no execution with the given job id exists.</response>
    [HttpGet("executions/{jobId:guid}")]
    [ProducesResponseType<SandboxExecutionLogRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExecution(Guid jobId, CancellationToken ct)
    {
        var entry = await executionLog.GetByIdAsync(jobId, ct);
        return entry is null ? NotFound() : Ok(ToRecord(entry));
    }

    /// <summary>
    /// Returns aggregate outcome counts across all recorded executions.
    /// </summary>
    [HttpGet("executions/stats")]
    [ProducesResponseType<SandboxExecutionStatsResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var stats = await executionLog.GetStatsAsync(ct);
        return Ok(new SandboxExecutionStatsResponse
        {
            Total = stats.Total,
            Succeeded = stats.Succeeded,
            Failed = stats.Failed,
            Denied = stats.Denied
        });
    }

    private SandboxExecutionLogRecord ToRecord(ExecutionLogEntry e)
    {
        return new()
        {
            JobId = e.JobId,
            ExecutedAt = e.ExecutedAt,
            AgentId = e.AgentId,
            CorrelationId = e.CorrelationId,
            Language = e.Language.ToString(),
            Code = e.Code,
            OriginalUserPrompt = e.OriginalUserPrompt,
            FileContext = e.FileContext
                .Select(f => new SandboxFileContextDto { FileName = f.FileName, Content = f.Content })
                .ToList(),
            Succeeded = e.Succeeded,
            ExitCode = e.ExitCode,
            Output = e.Output,
            Error = e.Error,
            TimedOut = e.TimedOut,
            PolicyDenied = e.PolicyDenied,
            PolicyDenialReason = e.PolicyDenialReason,
            SentinelAuditId = e.SentinelAuditId,
            DurationMs = e.DurationMs,
            Artifacts = e.Artifacts
                .Select(a => new ArtifactDto
                {
                    FileName = a.FileName,
                    MimeType = a.MimeType,
                    BlobUri = a.BlobUri.ToString(),
                    DownloadPath = ArtifactKey(a) ?? string.Empty,
                    SizeBytes = a.SizeBytes
                })
                .ToList()
        };
    }

    /// <summary>
    /// Streams an artifact of one of the caller's runs through the backend, so the browser never needs blob storage
    /// credentials. The path is <c>{jobId:N}/{file}</c>; any other caller's artifact is reported as not found.
    /// </summary>
    [HttpGet("artifacts/{**path}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadArtifactAsync(string path, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains("..", StringComparison.Ordinal))
        {
            return BadRequest("Invalid artifact path.");
        }

        if (!await IsOwnArtifactAsync(path, ct))
        {
            return NotFound();
        }

        var artifact = await objectStore.OpenReadAsync(_opts.ArtifactContainerName, path, ct);
        if (artifact is null)
        {
            return NotFound();
        }

        var fileName = Path.GetFileName(path);

        // Artifacts are written by sandboxed code: even if a browser opens one instead of downloading it, it runs nothing.
        Response.Headers[HeaderNames.ContentSecurityPolicy] = "default-src 'none'; sandbox";
        return File(artifact.Content, artifact.ContentType, fileName, enableRangeProcessing: true);
    }

    /// <summary>
    /// The execution history is user-scoped, so finding the run that harvested <paramref name="path"/> proves the
    /// caller may read it.
    /// </summary>
    private async Task<bool> IsOwnArtifactAsync(string path, CancellationToken ct)
    {
        var separator = path.IndexOf('/', StringComparison.Ordinal);
        if (separator <= 0 || !Guid.TryParseExact(path[..separator], "N", out var jobId))
        {
            return false;
        }

        var entry = await executionLog.GetByIdAsync(jobId, ct);

        return entry is not null && entry.Artifacts.Any(a => ArtifactKey(a) == path);
    }

    private string? ArtifactKey(ArtifactRecord artifact) => ObjectUri.GetKey(artifact.BlobUri, _opts.ArtifactContainerName);
}
