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
internal sealed class SandboxController(ISandboxRunner runner) : ControllerBase
{
    /// <summary>
    /// Runs Python or Node.js code in a Docker container with NetworkDisabled, ReadonlyRootfs, CapDrop=ALL and
    /// CPU/memory/PID hard limits. The caller (Praxis in Sentium.Web) has already had the run authorized by the
    /// Sentinel PDP and records the outcome; the Sandbox trusts it through the internal API key alone.
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
            CorrelationId = correlationId
        };

        var result = await runner.RunAsync(request, ct);

        return Ok(new SandboxExecutionResponse
        {
            Succeeded = result.Succeeded,
            ExitCode = result.ExitCode,
            Output = result.Output,
            Error = result.Error,
            TimedOut = result.TimedOut,
            JobId = result.JobId,
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
