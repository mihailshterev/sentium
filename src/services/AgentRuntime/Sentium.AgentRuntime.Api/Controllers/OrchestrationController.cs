using System.Text.Json;
using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Dtos;
using Sentium.AgentRuntime.Core.WorkflowManagement;
using Sentium.AgentRuntime.Core.Workflows;
using Sentium.Infrastructure.Messaging;
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
    public async Task<ActionResult<Envelope>> RunDynamicWorkflow([FromBody] dynamic customInput, CancellationToken ct)
    {
        var user = User.Identity?.Name ?? "Unknown";

        var payload = customInput ?? new { activity = "Manual trigger", user = user };
        var jsonPayload = JsonSerializer.Serialize(payload);

        await eventBus.PublishAsync(WorkflowEvents.Dynamic, jsonPayload, ct: ct);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Dynamic workflow triggered by {User}", user);
        }

        return Accepted(new Envelope(WorkflowEvents.Dynamic));
    }

    /// <summary>
    /// Endpoint to trigger a predefined workflow run based on a registered workflow ID.
    /// </summary>
    /// <param name="request">The request containing the workflow ID, scenario name, and parameters.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>An accepted response indicating the workflow has been triggered.</returns>
    [HttpPost("run-workflow")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<Envelope>> RunWorkflow([FromBody] RunWorkflowRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var workflow = await workflowService.GetWorkflowAsync(request.WorkflowId, ct);

        var payload = new
        {
            activity = request.Scenario,
            workflowId = workflow.Id,
            workflowName = workflow.Name,
            agents = workflow.Agents.Select(a => a.AgentId),
            workspaceId = request.WorkspaceId
        };

        var jsonPayload = JsonSerializer.Serialize(payload);
        await eventBus.PublishAsync(WorkflowEvents.CustomWorkflow, jsonPayload, ct: ct);

        return Accepted(new Envelope(WorkflowEvents.CustomWorkflow));
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

        try
        {
            await foreach (var msg in eventBus.SubscribeStreamAsync($"stream.{eventId}", serializer: NatsJsonSerializer<AgentStreamUpdate>.Default, ct: ct))
            {
                if (msg.Data is null)
                {
                    continue;
                }

                if (msg.Data.Type == AgentUpdateTypes.Done)
                {
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
            return;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while streaming agent execution for event {EventId}: {Message}", eventId, ex.Message);
            var errorPayload = JsonSerializer.Serialize(new AgentStreamUpdate("System", $"Error: {ex.Message}"));
            await Response.WriteAsync($"data: {errorPayload}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }

    }

    public record Envelope(string EventId, string? CorrelationId = null, string Status = "Accepted");
}
