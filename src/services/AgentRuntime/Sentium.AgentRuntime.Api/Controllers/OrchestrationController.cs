using System.Text.Json;
using System.Text.Json.Nodes;
using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Dtos;
using Sentium.AgentRuntime.Core.Registry;
using Sentium.AgentRuntime.Core.WorkflowManagement;
using Sentium.AgentRuntime.Core.Workflows;
using Sentium.Infrastructure.Messaging;
using Sentium.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NATS.Client.Serializers.Json;
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
    IWorkflowService workflowService,
    ICurrentUser currentUser,
    IPromptEnhancementService promptEnhancementService,
    IRegistrySettingsService registrySettingsService,
    ILogger<OrchestrationController> logger) : ControllerBase
{
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

        await eventBus.PublishAsync(WorkflowEvents.Dynamic, jsonPayload, ct: ct);

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
        await eventBus.PublishAsync(WorkflowEvents.CustomWorkflow, jsonPayload, ct: ct);

        return Accepted(new WorkflowAcceptedResponse(streamId));
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

        var init = JsonSerializer.Serialize(new AgentStreamUpdate("System", "Listening for agent telemetry..."));
        await Response.WriteAsync($"data: {init}\n\n", ct);
        await Response.Body.FlushAsync(ct);

        var enumerator = eventBus
            .SubscribeStreamAsync($"stream.{eventId}", serializer: NatsJsonSerializer<AgentStreamUpdate>.Default, ct: ct)
            .GetAsyncEnumerator(ct);

        try
        {
            while (true)
            {
                var moveNextTask = enumerator.MoveNextAsync().AsTask();

                while (!moveNextTask.IsCompleted)
                {
                    await Task.WhenAny(moveNextTask, Task.Delay(TimeSpan.FromSeconds(20), ct));
                    if (!moveNextTask.IsCompleted && !ct.IsCancellationRequested)
                    {
                        await Response.WriteAsync(": heartbeat\n\n", ct);
                        await Response.Body.FlushAsync(ct);
                    }
                }

                if (!await moveNextTask)
                {
                    break;
                }

                var msg = enumerator.Current;

                if (msg.Data is null)
                {
                    continue;
                }

                if (msg.Data.Type == AgentUpdateTypes.Done)
                {
                    var doneJson = JsonSerializer.Serialize(new StreamDoneFrame(AgentUpdateTypes.Done));
                    await Response.WriteAsync($"data: {doneJson}\n\n", ct);
                    await Response.Body.FlushAsync(ct);
                    break;
                }

                if (string.IsNullOrEmpty(msg.Data.Text))
                {
                    continue;
                }

                var json = JsonSerializer.Serialize(msg.Data);
                await Response.WriteAsync($"data: {json}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Client disconnected from stream {EventId}", eventId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while streaming agent execution for event {EventId}: {Message}", eventId, ex.Message);
            try
            {
                var errorPayload = JsonSerializer.Serialize(new AgentStreamUpdate("System", $"Error: {ex.Message}"));
                await Response.WriteAsync($"data: {errorPayload}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
            catch (Exception writeEx)
            {
                logger.LogWarning(writeEx, "Failed to write SSE error response for stream {EventId}.", eventId);
            }
        }
        finally
        {
            await enumerator.DisposeAsync();
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
