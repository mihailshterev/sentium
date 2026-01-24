using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sentinel.Core.Sensors;

namespace Sentinel.Infrastructure.Workers;

public sealed class NetworkWorker : BackgroundService
{
    private readonly INetworkSensor Sensor;
    private readonly ILogger<NetworkWorker> Logger;

    public NetworkWorker(
        INetworkSensor sensor,
        ILogger<NetworkWorker> logger)
    {
        Sensor = sensor;
        Logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger.LogInformation("Network worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Sensor.Scan();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Network scan failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
