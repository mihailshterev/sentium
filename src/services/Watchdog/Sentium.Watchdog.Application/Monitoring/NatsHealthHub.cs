using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client.Serializers.Json;
using Sentium.Infrastructure.Messaging;
using Sentium.Watchdog.Core.Monitoring;

namespace Sentium.Watchdog.Application.Monitoring;

public sealed class NatsHealthHub(
    IEventBus eventBus,
    ChannelReader<ServiceHealthStatus> statusChannel,
    ILogger<NatsHealthHub> logger) : BackgroundService
{
    private const string Subject = "sentium.status.updates";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("NatsHealthHub started: publishing status changes to '{Subject}'", Subject);

        await foreach (var update in statusChannel.ReadAllAsync(stoppingToken))
        {
            try
            {
                var payload = new ServiceHealthPayload(
                    ServiceName: update.ServiceName,
                    Status: update.Status.ToString(),
                    LatencyMs: update.LatencyMs,
                    Timestamp: update.CheckedAt.ToString("O"),
                    Details: update.Details
                );

                await eventBus.PublishAsync(
                    Subject,
                    payload,
                    serializer: NatsJsonSerializer<ServiceHealthPayload>.Default,
                    ct: stoppingToken
                );

                logger.LogDebug("Published health update for {Service}: {Status} ({LatencyMs}ms)", update.ServiceName, update.Status, update.LatencyMs);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Failed to publish health update for {Service}", update.ServiceName);
            }
        }
    }
}
