using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Infrastructure.Security;
using Sentium.Praxis.Common.Streaming;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Registry;
using Sentium.Praxis.Features.WorkflowManagement;
using Sentium.Praxis.Features.Workflows;

namespace Sentium.Praxis.Features.Orchestration;

[ApiController]
[Authorize]
[Route($"{PraxisRoutes.Prefix}/orchestration")]
public sealed class OrchestrationController(
    IWorkflowRunQueue runQueue,
    IWorkflowExecutionRegistry executionRegistry,
    IStreamRelay streamRelay,
    IWorkflowService workflowService,
    ICurrentUser currentUser,
    IPromptEnhancementService promptEnhancementService,
    IRegistrySettingsService registrySettingsService,
    IConfiguration configuration,
    ILogger<OrchestrationController> logger) : ControllerBase
{
    private readonly TimeSpan _maxStreamDuration = ResolveTimeSpan(configuration, "Orchestration:StreamMaxDuration", TimeSpan.FromMinutes(30));
    private readonly TimeSpan _heartbeatInterval = ResolveTimeSpan(configuration, "Orchestration:HeartbeatInterval", TimeSpan.FromSeconds(20));

    /// <summary>
    /// Queues an ad-hoc dynamic workflow run. The input is a JSON object; its <c>activity</c> string is enhanced when
    /// prompt enhancement is on. Returns the stream id to follow the run.
    /// </summary>
    [HttpPost("run-dynamic-workflow")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<WorkflowAcceptedResponse>> RunDynamicWorkflow([FromBody] dynamic customInput, CancellationToken ct)
    {
        var user = User.Identity?.Name ?? "Unknown";

        var streamId = Guid.NewGuid().ToString("N");

        var payload = customInput ?? new DynamicWorkflowActivity("Manual trigger", user);
        var payloadNode = JsonNode.Parse(JsonSerializer.Serialize(payload))?.AsObject() ?? new JsonObject();
        payloadNode["userId"] = currentUser.UserId?.ToString();
        payloadNode["streamId"] = streamId;

        if (payloadNode["activity"] is JsonValue activityNode && activityNode.GetValueKind() == JsonValueKind.String)
        {
            var activityText = activityNode.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(activityText))
            {
                payloadNode["activity"] = await EnhanceIfEnabledAsync(activityText, ct);
            }
        }

        var jsonPayload = payloadNode.ToJsonString();

        await runQueue.EnqueueAsync(new WorkflowTrigger
        {
            TriggerType = WorkflowTriggerTypes.Dynamic,
            Payload = jsonPayload,
            UserId = currentUser.UserId,
            StreamId = streamId
        }, ct);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Dynamic workflow triggered by {User}", user);
        }

        return Accepted(new WorkflowAcceptedResponse(streamId));
    }

    /// <summary>
    /// Queues a run of a saved workflow. Returns the stream id to follow the run.
    /// </summary>
    [HttpPost("run-workflow")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkflowAcceptedResponse>> RunWorkflow([FromBody] RunWorkflowRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var workflow = await workflowService.GetWorkflowAsync(request.WorkflowId, ct);
        if (workflow is null)
        {
            return NotFound();
        }

        var enhancedScenario = await EnhanceIfEnabledAsync(request.Scenario, ct);

        var streamId = Guid.NewGuid().ToString("N");

        var payload = new WorkflowTriggerPayload(
            Activity: enhancedScenario,
            WorkflowId: workflow.Id,
            WorkflowName: workflow.Name,
            Agents: workflow.Agents.Select(a => a.AgentId).ToList(),
            WorkspaceId: request.WorkspaceId,
            UserId: currentUser.UserId,
            StreamId: streamId);

        var jsonPayload = JsonSerializer.Serialize(payload);
        await runQueue.EnqueueAsync(new WorkflowTrigger
        {
            TriggerType = WorkflowTriggerTypes.CustomWorkflow,
            Payload = jsonPayload,
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
        var signalled = executionRegistry.Cancel(streamId);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Cancellation requested for stream {StreamId} (in flight: {InFlight}).", streamId, signalled);
        }

        return Accepted();
    }

    /// <summary>
    /// Streams a run's updates over SSE: the buffered backlog after <c>Last-Event-ID</c>, then live frames, ending with
    /// one terminal frame. A connection is capped at <c>Orchestration:StreamMaxDuration</c>; the EventSource then
    /// reconnects and resumes from its last id.
    /// </summary>
    [HttpGet("stream/{eventId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task StreamAgentExecution(string eventId, CancellationToken ct)
    {
        var fromSeq = ParseLastEventId(Request.Headers["Last-Event-ID"].ToString());

        using var streamCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        streamCts.CancelAfter(_maxStreamDuration);
        var token = streamCts.Token;

        await using var sse = await SseStreamWriter.StartAsync(Response, _heartbeatInterval, logger, token);

        try
        {
            await sse.ForwardAsync(streamRelay.Subscribe(eventId, fromSeq, token), token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Client disconnected from stream {EventId}", eventId);
            }
        }
        catch (OperationCanceledException)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Stream {EventId} hit the per-connection cap; closing for transparent reconnect.", eventId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while streaming agent execution for event {EventId}.", eventId);
            await sse.FailAsync("An unexpected error occurred while streaming the run.");
        }
    }

    private static long ParseLastEventId(string header) => long.TryParse(header, out var seq) && seq > 0 ? seq : 0;

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
}
