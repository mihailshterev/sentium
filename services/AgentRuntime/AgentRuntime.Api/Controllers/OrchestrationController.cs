using AgentRuntime.Application.Workflows;
using AgentRuntime.Core.Dtos;
using AgentRuntime.Core.WorkflowManagement;
using AgentRuntime.Core.Workflows;
using Infrastructure.Messaging;
using Microsoft.AspNetCore.Mvc;
using NATS.Client.Serializers.Json;

namespace AgentRuntime.Api.Controllers;

[ApiController]
[Route("agents")]
public sealed class OrchestrationController(IEventBus eventBus, IWorkflowService workflowService) : ControllerBase
{
    [HttpPost("test-pipeline")]
    public async Task<IActionResult> RunPipeline([FromBody] dynamic customInput, CancellationToken ct)
    {
        var payload = customInput ?? new { activity = "Manual trigger", user = "admin" };
        var jsonPayload = System.Text.Json.JsonSerializer.Serialize(payload);

        await eventBus.PublishAsync(WorkflowEvents.Dynamic, jsonPayload, ct: ct);
        return Ok(new { eventId = WorkflowEvents.Dynamic });
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

        var jsonPayload = System.Text.Json.JsonSerializer.Serialize(payload);
        await eventBus.PublishAsync(WorkflowEvents.CustomWorkflow, jsonPayload, ct: ct);

        return Ok(new { eventId = WorkflowEvents.CustomWorkflow });
    }

    [HttpGet("stream/{eventId}")]
    public async Task StreamAgentExecution(string eventId, CancellationToken ct)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        var init = System.Text.Json.JsonSerializer.Serialize(new AgentStreamUpdate("System", "Listening for agent telemetry..."));
        await Response.WriteAsync($"data: {init}\n\n", ct);
        await Response.Body.FlushAsync(ct);

        await foreach (var msg in eventBus.SubscribeStreamAsync($"stream.{eventId}", serializer: NatsJsonSerializer<AgentStreamUpdate>.Default, ct: ct))
        {
            Console.WriteLine($"[STREAM] Received from {msg.Data?.Author}: {msg.Data?.Text}");
            var json = System.Text.Json.JsonSerializer.Serialize(msg.Data);

            await Response.WriteAsync($"data: {json}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
    }

    [HttpGet("health")]
    public IActionResult AgentHealthCheck()
    {
        return Ok("Agent service is healthy.");
    }
}
