using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client.Serializers.Json;
using Sentium.Infrastructure.Messaging;
using Sentium.Shared.Constants;
using Sentium.Watchdog.Core.Monitoring;
using Sentium.Watchdog.Core.Settings;

namespace Sentium.Watchdog.Application.Monitoring;

/// <summary>
/// Periodically probes every monitored target (services and infrastructure), records the result,
/// evaluates incidents, and broadcasts changes over NATS for the live SSE stream. Cadence and
/// thresholds are read from the Registry-backed settings on every cycle so changes apply live.
/// </summary>
public sealed class MonitoringWorker(
    IEnumerable<IHealthProbe> probes,
    IServiceHealthStateStore stateStore,
    IIncidentStore incidentStore,
    IWatchdogSettingsProvider settingsProvider,
    IEventBus eventBus,
    ILogger<MonitoringWorker> logger) : BackgroundService
{
    private readonly IReadOnlyList<IHealthProbe> _probes = [.. probes];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("MonitoringWorker started – monitoring {Count} targets", _probes.Count);

        while (!stoppingToken.IsCancellationRequested)
        {
            WatchdogRuntimeSettings settings;
            try
            {
                settings = await settingsProvider.GetAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await PollAllAsync(settings, stoppingToken);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(settings.PollIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private Task PollAllAsync(WatchdogRuntimeSettings settings, CancellationToken stoppingToken)
        => Task.WhenAll(_probes.Select(p => ProbeTargetAsync(p, settings, stoppingToken)));

    private async Task ProbeTargetAsync(IHealthProbe probe, WatchdogRuntimeSettings settings, CancellationToken stoppingToken)
    {
        var previous = stateStore.Get(probe.Target);

        ServiceHealthStatus raw;
        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken))
        {
            cts.CancelAfter(TimeSpan.FromSeconds(settings.ProbeTimeoutSeconds));
            try
            {
                raw = await probe.ProbeAsync(cts.Token);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                raw = ProbeFault(probe, ex.Message);
            }
        }

        if (raw.Status == ServiceStatus.Healthy && raw.LatencyMs > settings.DegradedLatencyMs)
        {
            raw = raw with
            {
                Status = ServiceStatus.Degraded,
                Details = $"High latency: {raw.LatencyMs:0.#}ms (threshold {settings.DegradedLatencyMs}ms)"
            };
        }

        var current = stateStore.UpdateStatus(raw);

        await EvaluateIncidentAsync(probe, current, settings, stoppingToken);
        LogTransition(previous, current);

        if (previous is null || previous.Status != current.Status)
        {
            await PublishStatusAsync(current, stoppingToken);
        }
    }

    private async Task EvaluateIncidentAsync(IHealthProbe probe, ServiceHealthStatus current, WatchdogRuntimeSettings settings, CancellationToken ct)
    {
        var open = incidentStore.GetOpen(probe.Target);

        switch (current.Status)
        {
            case ServiceStatus.Unhealthy when open is null:
                await TryPublishIncidentAsync(
                    incidentStore.Open(probe.Target, probe.Kind, IncidentSeverity.Critical, current.Status, current.Details),
                    NatsSubjects.WatchdogIncidentOpened, ct);
                break;

            case ServiceStatus.Degraded when open is null && current.ConsecutiveFailures >= settings.ConsecutiveFailuresToOpenIncident:
                await TryPublishIncidentAsync(
                    incidentStore.Open(probe.Target, probe.Kind, IncidentSeverity.Warning, current.Status, current.Details),
                    NatsSubjects.WatchdogIncidentOpened, ct);
                break;

            case ServiceStatus.Healthy when open is not null:
                await TryPublishIncidentAsync(incidentStore.Resolve(probe.Target), NatsSubjects.WatchdogIncidentResolved, ct);
                break;
        }
    }

    private void LogTransition(ServiceHealthStatus? previous, ServiceHealthStatus current)
    {
        if (current.Status == ServiceStatus.Unhealthy && previous?.Status != ServiceStatus.Unhealthy)
        {
            logger.LogCritical("Target {Target} is UNHEALTHY. Latency {LatencyMs}ms. {Details}", current.ServiceName, current.LatencyMs, current.Details ?? "no details");
        }
        else if (current.Status == ServiceStatus.Degraded && previous?.Status != ServiceStatus.Degraded)
        {
            logger.LogWarning("Target {Target} is DEGRADED. {Details}", current.ServiceName, current.Details ?? "no details");
        }
        else if (current.Status == ServiceStatus.Healthy && previous is not null && previous.Status != ServiceStatus.Healthy)
        {
            logger.LogInformation("Target {Target} has recovered and is now HEALTHY", current.ServiceName);
        }
    }

    private async Task PublishStatusAsync(ServiceHealthStatus status, CancellationToken ct)
    {
        try
        {
            var payload = new ServiceHealthPayload(
                ServiceName: status.ServiceName,
                Kind: status.Kind.ToString(),
                Status: status.Status.ToString(),
                LatencyMs: status.LatencyMs,
                UptimePercent: status.UptimePercent,
                Timestamp: status.CheckedAt.ToString("O"),
                Details: status.Details);

            await eventBus.PublishAsync(NatsSubjects.WatchdogStatusUpdates, payload, serializer: NatsJsonSerializer<ServiceHealthPayload>.Default, ct: ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Failed to publish status update for {Target}", status.ServiceName);
        }
    }

    private async Task TryPublishIncidentAsync(Incident? incident, string subject, CancellationToken ct)
    {
        if (incident is null)
        {
            return;
        }

        logger.LogWarning("Incident {Action} for {Target} (severity {Severity})",
            subject == NatsSubjects.WatchdogIncidentResolved ? "RESOLVED" : "OPENED", incident.Target, incident.Severity);

        try
        {
            await eventBus.PublishAsync(subject, incident, serializer: NatsJsonSerializer<Incident>.Default, ct: ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Failed to publish incident event for {Target}", incident.Target);
        }
    }

    private static ServiceHealthStatus ProbeFault(IHealthProbe probe, string message) => new()
    {
        ServiceName = probe.Target,
        Kind = probe.Kind,
        Status = ServiceStatus.Unhealthy,
        LatencyMs = 0,
        CheckedAt = DateTimeOffset.UtcNow,
        Details = message
    };
}
