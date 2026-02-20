using Infrastructure.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sentinel.Core.Sensors;

namespace Sentinel.Infrastructure.Workers;

public sealed class NetworkSentinelWorker(
    INetworkSensor sensor,
    IEventBus bus,
    ILogger<NetworkSentinelWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await sensor.ScanAsync(async evt =>
                {
                    logger.LogInformation("New network event detected: {Event}", evt);
                    foreach (var signal in evt.Metadata)
                    {
                        logger.LogInformation(" - Signal: ({SignalValue})", signal.Value);
                    }
                    //await bus.PublishAsync("agents.analyst.inbound", evt, stoppingToken);
                }, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Sentinel Heartbeat Failure");
            }
        }
    }
}
