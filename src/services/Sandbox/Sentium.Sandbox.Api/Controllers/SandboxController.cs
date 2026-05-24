using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Sentium.Sandbox.Api.Dtos;
using Sentium.Sandbox.Application;
using Sentium.Sandbox.Application.Options;
using Sentium.Sandbox.Core.Interfaces;
using Sentium.Sandbox.Core.Models;
using Sentium.Shared.Constants;

namespace Sentium.Sandbox.Api.Controllers;

[ApiController]
[AllowAnonymous] // temp
[Route("sandbox")]
public sealed class SandboxController(
    SandboxOrchestrator orchestrator,
    IExecutionLogRepository executionLog,
    BlobServiceClient blobServiceClient,
    IOptions<SandboxOptions> options) : ControllerBase
{
    private readonly SandboxOptions _opts = options.Value;

    /// <summary>
    /// Returns the most recent sandbox execution records (newest first).
    /// Each record includes the original request context (code, agent ID, prompt) and the full result.
    /// </summary>
    [HttpGet("executions")]
    [ProducesResponseType<IReadOnlyList<SandboxExecutionLogRecord>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExecutions([FromQuery] int count = 100, CancellationToken ct = default)
    {
        var entries = await executionLog.GetRecentAsync(Math.Min(count, 500), ct);

        var dtos = entries.Select(e => new SandboxExecutionLogRecord
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
                    DownloadPath = new BlobUriBuilder(a.BlobUri).BlobName,
                    SizeBytes = a.SizeBytes
                })
                .ToList()
        }).ToList();

        return Ok(dtos);
    }

    /// <summary>
    /// Submits Python or Node.js code for isolated execution inside a Docker container.
    /// The request must first be authorized by the Sentinel PDP.
    /// The container runs with NetworkDisabled, ReadonlyRootfs, CapDrop=ALL and CPU/memory/PID hard limits.
    /// </summary>
    [HttpPost("execute")]
    [ProducesResponseType<SandboxExecutionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExecuteAsync([FromBody] SandboxExecutionRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);

        if (!Enum.TryParse<ExecutionLanguage>(body.Language, ignoreCase: true, out var language))
        {
            return BadRequest($"Unknown language '{body.Language}'. Valid values: {string.Join(", ", Enum.GetNames<ExecutionLanguage>())}");
        }

        if (string.IsNullOrWhiteSpace(body.Code))
        {
            return BadRequest("'Code' must not be empty.");
        }

        if (body.Code.Length > _opts.MaxCodeSizeBytes)
        {
            return BadRequest($"Code exceeds the maximum allowed size of {_opts.MaxCodeSizeBytes:N0} bytes.");
        }

        if (string.IsNullOrWhiteSpace(body.AgentId))
        {
            return BadRequest("'AgentId' must not be empty.");
        }

        var fileContext = body.FileContext ?? [];

        if (fileContext.Count > _opts.MaxFileContextEntries)
        {
            return BadRequest($"FileContext exceeds the maximum of {_opts.MaxFileContextEntries} entries.");
        }

        foreach (var file in fileContext)
        {
            if (string.IsNullOrWhiteSpace(file.FileName))
            {
                return BadRequest("A FileContext entry has an empty FileName.");
            }

            if (string.IsNullOrWhiteSpace(file.Content))
            {
                return BadRequest($"FileContext entry '{file.FileName}' has empty Content.");
            }

            if (file.Content.Length > _opts.MaxFileContentBytes)
            {
                return BadRequest($"FileContext entry '{file.FileName}' exceeds the maximum content size of {_opts.MaxFileContentBytes:N0} bytes.");
            }
        }

        var correlationId = HttpContext.Request.Headers[CommonHeaderNames.CorrelationId].FirstOrDefault() ?? Guid.NewGuid().ToString();

        var request = new ExecutionRequest
        {
            Language = language,
            Code = body.Code,
            FileContext = fileContext.Select(f => new SandboxFileContext
            {
                FileName = f.FileName,
                Content = f.Content
            }).ToList(),
            AgentId = body.AgentId,
            CorrelationId = correlationId,
            OriginalUserPrompt = body.OriginalUserPrompt
        };

        var result = await orchestrator.ExecuteAsync(request, ct);

        if (result.PolicyDenied)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "Execution denied by security policy.",
                reason = result.PolicyDenialReason,
                sentinelAuditId = result.SentinelAuditId
            });
        }

        return Ok(new SandboxExecutionResponse
        {
            Succeeded = result.Succeeded,
            ExitCode = result.ExitCode,
            Output = result.Output,
            Error = result.Error,
            TimedOut = result.TimedOut,
            JobId = result.JobId,
            SentinelAuditId = result.SentinelAuditId,
            DurationMs = result.DurationMs,
            Artifacts = result.Artifacts
                .Select(a => new ArtifactDto
                {
                    FileName = a.FileName,
                    MimeType = a.MimeType,
                    BlobUri = a.BlobUri.ToString(),
                    DownloadPath = new BlobUriBuilder(a.BlobUri).BlobName,
                    SizeBytes = a.SizeBytes
                })
                .ToList()
        });
    }

    /// <summary>
    /// Streams the requested artifact blob through the backend, avoiding direct blob storage credential requirements.
    /// </summary>
    [HttpGet("artifacts/{**path}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadArtifactAsync(string path, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains(".."))
        {
            return BadRequest("Invalid artifact path.");
        }

        var container = blobServiceClient.GetBlobContainerClient(_opts.ArtifactContainerName);
        var blob = container.GetBlobClient(path);

        if (!await blob.ExistsAsync(ct))
        {
            return NotFound();
        }

        var props = await blob.GetPropertiesAsync(cancellationToken: ct);
        var contentType = string.IsNullOrEmpty(props.Value.ContentType) ? "application/octet-stream" : props.Value.ContentType;

        var stream = await blob.OpenReadAsync(cancellationToken: ct);
        var fileName = Path.GetFileName(path);

        return File(stream, contentType, fileName, enableRangeProcessing: true);
    }
}
