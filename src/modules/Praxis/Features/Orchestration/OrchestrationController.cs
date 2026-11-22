using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Infrastructure.Security;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Registry;
using Sentium.Praxis.Features.WorkflowManagement;
using Sentium.Praxis.Features.Workflows;
using Sentium.Praxis.Features.Workspaces;

namespace Sentium.Praxis.Features.Orchestration;

[ApiController]
[Authorize]
[Route($"{PraxisRoutes.Prefix}/orchestration")]
internal sealed class OrchestrationController(
    IWorkflowRunQueue runQueue,
    IWorkflowExecutionRegistry executionRegistry,
    IStreamRelay streamRelay,
    IWorkflowService workflowService,
    ICurrentUser currentUser,
    IPromptEnhancementService promptEnhancementService,
    IRegistrySettingsService registrySettingsService,
    IWorkspaceService workspaceService,
    IWorkspaceContextBuilder workspaceContext,
    IConfiguration configuration,
    ILogger<OrchestrationController> logger) : ControllerBase
{
    private const string UnknownWorkspaceMessage = "There is no such workspace.";

    private readonly TimeSpan _maxStreamDuration = ResolveTimeSpan(configuration, "Orchestration:StreamMaxDuration", TimeSpan.FromMinutes(30));
    private readonly TimeSpan _heartbeatInterval = ResolveTimeSpan(configuration, "Orchestration:HeartbeatInterval", TimeSpan.FromSeconds(20));

    /// <summary>
    /// Queues an ad-hoc dynamic workflow run; the activity is enhanced when prompt enhancement is on. Returns the
    /// stream id to follow the run.
    /// </summary>
    [HttpPost("run-dynamic-workflow")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WorkflowAcceptedResponse>> RunDynamicWorkflow([FromBody] RunDynamicWorkflowRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!await IsOwnWorkspaceAsync(request.WorkspaceId, ct))
        {
            return Problem(detail: UnknownWorkspaceMessage, statusCode: StatusCodes.Status400BadRequest);
        }

        var streamId = Guid.NewGuid().ToString("N");

        ClaimStream(streamId);
        await runQueue.EnqueueAsync(new WorkflowTrigger
        {
            TriggerType = WorkflowTriggerTypes.Dynamic,
            Activity = await EnhanceIfEnabledAsync(request.Activity, ct),
            WorkspaceId = request.WorkspaceId,
            FileIds = await OwnFileIdsAsync(request.FileIds, ct),
            UserId = currentUser.UserId,
            StreamId = streamId
        }, ct);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Dynamic workflow triggered by {User}", currentUser.UserName);
        }

        return Accepted(new WorkflowAcceptedResponse(streamId));
    }

    /// <summary>
    /// Queues a run of a saved workflow. Returns the stream id to follow the run.
    /// </summary>
    [HttpPost("run-workflow")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkflowAcceptedResponse>> RunWorkflow([FromBody] RunWorkflowRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!await IsOwnWorkspaceAsync(request.WorkspaceId, ct))
        {
            return Problem(detail: UnknownWorkspaceMessage, statusCode: StatusCodes.Status400BadRequest);
        }

        var workflow = await workflowService.GetWorkflowAsync(request.WorkflowId, ct);
        if (workflow is null)
        {
            return NotFound();
        }

        var streamId = Guid.NewGuid().ToString("N");

        ClaimStream(streamId);
        await runQueue.EnqueueAsync(new WorkflowTrigger
        {
            TriggerType = WorkflowTriggerTypes.CustomWorkflow,
            Activity = await EnhanceIfEnabledAsync(request.Scenario, ct),
            WorkflowId = workflow.Id,
            WorkflowName = workflow.Name,
            WorkspaceId = request.WorkspaceId,
            FileIds = await OwnFileIdsAsync(request.FileIds, ct),
            UserId = currentUser.UserId,
            StreamId = streamId
        }, ct);

        return Accepted(new WorkflowAcceptedResponse(streamId));
    }

    /// <summary>
    /// Requests cancellation of an in-flight workflow run (a no-op if it is not running). The run is then
    /// recorded as cancelled and a terminal frame is emitted to its stream.
    /// </summary>
    [HttpPost("cancel/{streamId}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public IActionResult CancelRun(string streamId)
    {
        var signalled = currentUser.IsSovereign
            ? executionRegistry.Cancel(streamId)
            : currentUser.UserId is { } userId && executionRegistry.CancelOwned(streamId, userId);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Cancellation requested for stream {StreamId} (in flight: {InFlight}).", streamId, signalled);
        }

        return Accepted();
    }

    /// <summary>
    /// Streams a run's updates over SSE (see <see cref="RelayStreams"/>): the buffered backlog after <c>Last-Event-ID</c>
    /// or <c>?after=</c>, then live frames, ending with one terminal frame. A connection is capped at
    /// <c>Orchestration:StreamMaxDuration</c>; the client then reconnects and resumes from its last id.
    /// </summary>
    [HttpGet("stream/{eventId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IResult StreamAgentExecution(string eventId)
    {
        if (!currentUser.IsSovereign && (currentUser.UserId is not { } userId || !streamRelay.IsVisibleTo(eventId, userId)))
        {
            return TypedResults.NotFound();
        }

        return RelayStreams.Create(
            streamRelay,
            eventId,
            RelayStreams.ResumePoint(Request),
            _heartbeatInterval,
            _maxStreamDuration,
            HttpContext.RequestAborted);
    }

    private void ClaimStream(string streamId)
    {
        if (currentUser.UserId is { } userId)
        {
            streamRelay.Claim(streamId, userId);
        }
    }

    private static TimeSpan ResolveTimeSpan(IConfiguration configuration, string key, TimeSpan fallback)
    {
        var raw = configuration[key];
        if (!string.IsNullOrWhiteSpace(raw))
        {
            if (TimeSpan.TryParse(raw, out var asTimeSpan) && asTimeSpan > TimeSpan.Zero)
            {
                return asTimeSpan;
            }

            if (double.TryParse(raw, out var asSeconds) && asSeconds > 0)
            {
                return TimeSpan.FromSeconds(asSeconds);
            }
        }

        return fallback;
    }

    private async Task<string> EnhanceIfEnabledAsync(string? text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text ?? string.Empty;
        }

        var settings = await registrySettingsService.GetAsync(currentUser.UserId, ct);
        return settings.Harness.IsPromptEnhancementEnabled ? await promptEnhancementService.EnhanceAsync(text, ct) : text;
    }

    /// <summary>
    /// No workspace counts as valid. The lookup goes through the per-user query filter, and the run's context builder
    /// filters on the run's user again, so a Sovereign's foreign id can never expose another member's files.
    /// </summary>
    private async Task<bool> IsOwnWorkspaceAsync(Guid? workspaceId, CancellationToken ct)
    {
        return workspaceId is not { } id || await workspaceService.GetWorkspaceAsync(id, ct) is not null;
    }

    private async Task<IReadOnlyList<Guid>> OwnFileIdsAsync(IReadOnlyList<Guid>? fileIds, CancellationToken ct)
    {
        if (fileIds is not { Count: > 0 } || currentUser.UserId is not { } userId)
        {
            return [];
        }

        var owned = await workspaceContext.ResolveAttachmentsAsync(userId, fileIds, ct);
        return [.. owned.Select(a => a.FileId)];
    }
}
