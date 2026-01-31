using NATS.Client.Core;
using AgentRuntime.Core.Workflows;
using AgentRuntime.Core.Agents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using AgentRuntime.Core.Orchestration;

namespace AgentRuntime.Application.Orchestration;

public sealed class NatsAgentOrchestrator : IHostedService
{
    private readonly INatsConnection Nats;
    private readonly IOrchestrator Orchestrator;
    private readonly ILogger<NatsAgentOrchestrator> Logger;

    public NatsAgentOrchestrator(
        INatsConnection nats,
        IOrchestrator orchestrator,
        ILogger<NatsAgentOrchestrator> logger)
    {
        Nats = nats;
        Orchestrator = orchestrator;
        Logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        await foreach (var msg in Nats.SubscribeAsync<string>(AgentEvents.AllEvents, cancellationToken: ct))
        {
            // Execute in background to keep NATS processing high-throughput
            _ = Task.Run(async () =>
            {
                var trigger = new WorkflowTrigger
                {
                    TriggerType = msg.Subject,
                    Payload = msg.Data!
                };

                var result = await Orchestrator.RunAsync(trigger, ct);

                await Nats.PublishAsync($"insights.{msg.Subject}", result, cancellationToken: ct);
            }, ct);
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
