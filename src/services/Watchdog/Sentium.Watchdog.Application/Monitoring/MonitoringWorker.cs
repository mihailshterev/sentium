using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sentium.Shared.Constants;
using Sentium.Watchdog.Core.Monitoring;

namespace Sentium.Watchdog.Application.Monitoring;

public sealed class MonitoringWorker(
    IHttpClientFactory httpClientFactory,
    IServiceHealthStateStore stateStore,
    ChannelWriter<ServiceHealthStatus> statusChannel,
    ILogger<MonitoringWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);

    private static readonly (string DisplayName, string ClientName)[] MonitoredServices =
    [
        ("Identity",       ServiceNames.Identity),
        ("Sentinel",       ServiceNames.Sentinel),
        ("Agent Runtime",  ServiceNames.AgentRuntime)
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("MonitoringWorker started – polling {Count} services every {Interval}s", MonitoredServices.Length, PollInterval.TotalSeconds);

        await PollServicesAsync(stoppingToken);

        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await PollServicesAsync(stoppingToken);
        }
    }

    private Task PollServicesAsync(CancellationToken cancellationToken)
    {
        var tasks = MonitoredServices
            .Select(s => CheckServiceAsync(s.DisplayName, s.ClientName, cancellationToken));

        return Task.WhenAll(tasks);
    }

    private async Task CheckServiceAsync(string displayName, string clientName, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        ServiceStatus newStatus;
        string? details = null;

        try
        {
            var client = httpClientFactory.CreateClient(clientName);
            var response = await client.GetAsync("/health", cancellationToken);
            sw.Stop();

            newStatus = response.IsSuccessStatusCode ? ServiceStatus.Healthy : ServiceStatus.Unhealthy;
            if (!response.IsSuccessStatusCode)
            {
                details = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            newStatus = ServiceStatus.Unhealthy;
            details = ex.Message;
        }

        var previous = stateStore.Get(displayName);
        var current = new ServiceHealthStatus
        {
            ServiceName = displayName,
            Status = newStatus,
            LatencyMs = Math.Round(sw.Elapsed.TotalMilliseconds, 1),
            CheckedAt = DateTimeOffset.UtcNow,
            Details = details
        };

        stateStore.UpdateStatus(current);

        if (newStatus == ServiceStatus.Unhealthy)
        {
            logger.LogCritical("Service {ServiceName} is UNHEALTHY. Latency: {LatencyMs}ms. Details: {Details}", displayName, current.LatencyMs, details ?? "no details");
        }
        else if (previous?.Status == ServiceStatus.Unhealthy)
        {
            logger.LogInformation("Service {ServiceName} has recovered and is now HEALTHY", displayName);
        }

        if (previous is null || previous.Status != newStatus)
        {
            await statusChannel.WriteAsync(current, cancellationToken);
        }
    }
}
