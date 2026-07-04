using System.Text.Json;
using System.Text.Json.Nodes;
using Sentium.AgentRuntime.Application.Orchestration;
using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Dtos;
using Sentium.AgentRuntime.Core.Registry;
using Sentium.AgentRuntime.Core.WorkflowManagement;
using Sentium.AgentRuntime.Core.Workflows;
using Sentium.Infrastructure.Messaging;
using Sentium.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Shared.Constants;

namespace Sentium.AgentRuntime.Api.Controllers;

/// <summary>
/// Controller responsible for orchestrating agent workflows and streaming execution updates.
/// </summary>
[ApiController]
[Authorize]
[Route("orchestration")]
public sealed class OrchestrationController(
    IEventBus eventBus,
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
    /// Endpoint to trigger a dynamic workflow run with custom input. Useful for ad-hoc scenarios where the workflow definition is not known in advance.
    /// The input is expected to be a JSON object, which will be serialized and published as an event for downstream processing.
    /// </summary>
    /// <param name="customInput">The custom input for the dynamic workflow.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>An accepted response indicating the workflow has been triggered.</returns>
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

        await eventBus.PublishPersistentAsync(WorkflowEvents.Dynamic, jsonPayload, messageId: streamId, ct: ct);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Dynamic workflow triggered by {User}", user);
        }

        return Accepted(new WorkflowAcceptedResponse(streamId));
    }

    /// <summary>
    /// Endpoint to trigger a predefined workflow run based on a registered workflow ID.
    /// </summary>
    /// <param name="request">The request containing the workflow ID, scenario name, and parameters.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>An accepted response indicating the workflow has been triggered.</returns>
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
        await eventBus.PublishPersistentAsync(WorkflowEvents.CustomWorkflow, jsonPayload, messageId: streamId, ct: ct);

        return Accepted(new WorkflowAcceptedResponse(streamId));
    }

    /// <summary>
    /// Requests cancellation of an in-flight workflow run. The request is broadcast over core NATS so
    /// whichever instance is executing the run stops it (a no-op on instances that are not). The run is
    /// then recorded as cancelled and a terminal frame is emitted to its stream.
    /// </summary>
    /// <param name="streamId">The stream identifier of the run to stop.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>An accepted response indicating the cancellation request was dispatched.</returns>
    [HttpPost("cancel/{streamId}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> CancelRun(string streamId, CancellationToken ct)
    {
        await eventBus.PublishAsync(WorkflowEvents.CancelSignal, new WorkflowCancelRequest(streamId), ct: ct);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Cancellation requested for stream {StreamId}.", streamId);
        }

        return Accepted();
    }

    /// <summary>
    /// Endpoint to stream real-time updates for an agent execution based on a unique event ID.
    /// </summary>
    /// <param name="eventId">The unique identifier for the agent execution event.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [HttpGet("stream/{eventId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task StreamAgentExecution(string eventId, CancellationToken ct)
    {
        Response.Headers.Append(CommonHeaderNames.ContentType, "text/event-stream");
        Response.Headers.Append(CommonHeaderNames.CacheControl, "no-cache");
        Response.Headers.Append(CommonHeaderNames.Connection, "keep-alive");
        Response.Headers.Append(CommonHeaderNames.AccelBuffering, "no");

        var fromSeq = ParseLastEventId(Request.Headers["Last-Event-ID"].ToString());

        using var streamCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        streamCts.CancelAfter(_maxStreamDuration);
        var token = streamCts.Token;

        using var writeLock = new SemaphoreSlim(1, 1);

        await WriteRawAsync(": connected\n\n", writeLock, token);

        using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        var heartbeat = RunHeartbeatAsync(writeLock, heartbeatCts.Token);

        try
        {
            await foreach (var (seq, update) in streamRelay.Subscribe(eventId, fromSeq, token))
            {
                if (update.Type == AgentUpdateTypes.Done)
                {
                    await WriteFrameAsync(seq, JsonSerializer.Serialize(new StreamDoneFrame(AgentUpdateTypes.Done)), writeLock, token);
                    break;
                }

                if (update.Type == AgentUpdateTypes.Error)
                {
                    await WriteFrameAsync(seq, JsonSerializer.Serialize(new StreamErrorFrame(update.Text)), writeLock, token);
                    break;
                }

                if (update.Type == AgentUpdateTypes.Cancelled)
                {
                    await WriteFrameAsync(seq, JsonSerializer.Serialize(new StreamDoneFrame(AgentUpdateTypes.Cancelled)), writeLock, token);
                    break;
                }

                if (string.IsNullOrEmpty(update.Text))
                {
                    continue;
                }

                await WriteFrameAsync(seq, JsonSerializer.Serialize(update), writeLock, token);
            }
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
            await TryWriteTerminalErrorAsync("An unexpected error occurred while streaming the run.", writeLock);
        }
        finally
        {
            await heartbeatCts.CancelAsync();
            try
            {
                await heartbeat;
            }
            catch (OperationCanceledException)
            {
                // expected on shutdown / disconnect
            }
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

    private async Task WriteFrameAsync(long seq, string json, SemaphoreSlim writeLock, CancellationToken ct)
    {
        await writeLock.WaitAsync(ct);
        try
        {
            await Response.WriteAsync($"id: {seq}\n", ct);
            await Response.WriteAsync($"data: {json}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
        finally
        {
            writeLock.Release();
        }
    }

    private async Task WriteRawAsync(string raw, SemaphoreSlim writeLock, CancellationToken ct)
    {
        await writeLock.WaitAsync(ct);
        try
        {
            await Response.WriteAsync(raw, ct);
            await Response.Body.FlushAsync(ct);
        }
        finally
        {
            writeLock.Release();
        }
    }

    private async Task RunHeartbeatAsync(SemaphoreSlim writeLock, CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(_heartbeatInterval);
            while (await timer.WaitForNextTickAsync(ct))
            {
                await WriteRawAsync(": heartbeat\n\n", writeLock, ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "SSE heartbeat stopped due to a write failure.");
        }
    }

    private async Task TryWriteTerminalErrorAsync(string message, SemaphoreSlim writeLock)
    {
        try
        {
            await writeLock.WaitAsync(CancellationToken.None);
            try
            {
                await Response.WriteAsync($"data: {JsonSerializer.Serialize(new StreamErrorFrame(message))}\n\n", CancellationToken.None);
                await Response.Body.FlushAsync(CancellationToken.None);
            }
            finally
            {
                writeLock.Release();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to write terminal SSE error frame.");
        }
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
