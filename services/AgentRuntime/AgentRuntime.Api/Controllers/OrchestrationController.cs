using System.Text.Json;
using AgentRuntime.Application.Workflows;
using AgentRuntime.Core.Dtos;
using AgentRuntime.Core.WorkflowManagement;
using AgentRuntime.Core.Workflows;
using Infrastructure.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NATS.Client.Serializers.Json;

namespace AgentRuntime.Api.Controllers;

[ApiController]
[Authorize]
[Route("agents")]
public sealed class OrchestrationController(
    IEventBus eventBus,
    IWorkflowService workflowService,
    ILogger<OrchestrationController> logger) : ControllerBase
{
    [HttpPost("test-pipeline")]
    public async Task<IActionResult> RunPipeline([FromBody] dynamic customInput, CancellationToken ct)
    {
        var payload = customInput ?? new { activity = "Manual trigger", user = "admin" };
        var jsonPayload = JsonSerializer.Serialize(payload);

        await eventBus.PublishAsync(WorkflowEvents.Dynamic, jsonPayload, ct: ct);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Pipeline test triggered by {User}", User.Identity?.Name);
        }

        return Accepted(new Envelope(WorkflowEvents.Dynamic));
    }

    [HttpPost("run-workflow")]
    public async Task<IActionResult> RunWorkflow([FromBody] RunWorkflowRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var workflow = await workflowService.GetWorkflowAsync(request.WorkflowId, ct);

        var payload = new
        {
            activity = request.Scenario,
            workflowId = workflow.Id,
            workflowName = workflow.Name,
            agents = workflow.Agents.Select(a => a.AgentId)
        };

        var jsonPayload = JsonSerializer.Serialize(payload);
        await eventBus.PublishAsync(WorkflowEvents.CustomWorkflow, jsonPayload, ct: ct);

        return Accepted(new Envelope(WorkflowEvents.CustomWorkflow));
    }

    [HttpPost("analyze-network-event")]
    public async Task<IActionResult> AnalyzeNetworkEvent([FromBody] NetworkEventAnalysisRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = new
        {
            activity = $"Network anomaly detected: {request.OrigH} \u2192 {request.RespH} via {request.Proto.ToUpperInvariant()}. " +
                       $"ML confidence: {request.MlScore}. Recommended action: {request.Action}. Service: {request.Service}. " +
                       $"Timestamp: {request.Timestamp}. Investigate source IP {request.OrigH} and determine if this traffic is malicious.",
            source = request.Source,
            origH = request.OrigH,
            respH = request.RespH,
            proto = request.Proto,
            service = request.Service,
            mlScore = request.MlScore,
            action = request.Action,
            timestamp = request.Timestamp
        };

        var jsonPayload = JsonSerializer.Serialize(payload);
        await eventBus.PublishAsync(WorkflowEvents.NetworkScan, jsonPayload, ct: ct);

        return Accepted(new Envelope(WorkflowEvents.NetworkScan));
    }

    [HttpGet("stream/{eventId}")]
    public async Task StreamAgentExecution(string eventId, CancellationToken ct)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        Response.Headers.Append("X-Accel-Buffering", "no");

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

                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("[STREAM:{eventId}] Received from {Author}: {Text}", eventId, msg.Data?.Author, msg.Data?.Text);
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
