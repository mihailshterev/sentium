using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using Sentium.Core.Constants;
using Sentium.Infrastructure.Security;
using Sentium.Sandbox.Contracts;
using Sentium.Sandbox.Execution;
using Sentium.Sandbox.Models;

namespace Sentium.Sandbox.Controllers;

[ApiController]
[AuthorizeSystem]
internal sealed class SandboxController(SandboxOrchestrator orchestrator) : ControllerBase
{
    /// <summary>
    /// Submits Python or Node.js code for isolated execution inside a Docker container.
    /// The request must first be authorized by the Sentinel PDP; a denial is returned as a result
    /// with <see cref="SandboxExecutionResponse.PolicyDenied"/> set, so the caller can record it.
    /// The container runs with NetworkDisabled, ReadonlyRootfs, CapDrop=ALL and CPU/memory/PID hard limits.
    /// </summary>
    [HttpPost("execute")]
    [ProducesResponseType<SandboxExecutionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SandboxExecutionResponse>> ExecuteAsync([FromBody] SandboxExecutionRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);

        var language = Enum.Parse<ExecutionLanguage>(body.Language, ignoreCase: true);
        var fileContext = body.FileContext ?? [];

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

        return Ok(new SandboxExecutionResponse
        {
            Succeeded = result.Succeeded,
            ExitCode = result.ExitCode,
            Output = result.Output,
            Error = result.Error,
            TimedOut = result.TimedOut,
            PolicyDenied = result.PolicyDenied,
            PolicyDenialReason = result.PolicyDenialReason,
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
}
