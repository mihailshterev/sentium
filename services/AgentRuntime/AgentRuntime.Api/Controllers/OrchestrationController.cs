using AgentRuntime.Application.Workflows;
using Microsoft.AspNetCore.Mvc;
using NATS.Client.Core;
using NATS.Client.Serializers.Json;

namespace AgentRuntime.Api.Controllers;

[ApiController]
[Route("agents")]
public class OrchestrationController(INatsConnection nats) : ControllerBase
{
    [HttpPost("test-pipeline")]
    public async Task<IActionResult> RunPipeline([FromBody] dynamic customInput, CancellationToken ct)
    {
        var payload = customInput ?? new { activity = "Manual trigger", user = "admin" };
        var jsonPayload = System.Text.Json.JsonSerializer.Serialize(payload);

        await nats.PublishAsync("events.network.scan", jsonPayload, cancellationToken: ct);
        return Ok();
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

        await foreach (var msg in nats.SubscribeAsync($"stream.{eventId}", serializer: NatsJsonSerializer<AgentStreamUpdate>.Default, cancellationToken: ct))
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
