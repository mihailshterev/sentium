using Sentium.Vigil.Features.Settings;

namespace Sentium.Vigil.Features.Monitoring;

/// <summary>
/// Periodically probes every monitored target (services and infrastructure), records the result,
/// evaluates incidents, and broadcasts changes through <see cref="IVigilFeed"/> for the live SSE stream. Cadence and
/// thresholds are read from the Registry-backed settings on every cycle so changes apply live.
/// </summary>
internal sealed class MonitoringWorker(
    IEnumerable<IHealthProbe> probes,
    IServiceHealthStateStore stateStore,
    IIncidentStore incidentStore,
    IVigilSettingsProvider settingsProvider,
    IVigilFeed feed,
    ILogger<MonitoringWorker> logger) : BackgroundService
{
    private readonly IReadOnlyList<IHealthProbe> _probes = [.. probes];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("MonitoringWorker started – monitoring {Count} targets", _probes.Count);

        while (!stoppingToken.IsCancellationRequested)
        {
            VigilRuntimeSettings settings;
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

    private Task PollAllAsync(VigilRuntimeSettings settings, CancellationToken stoppingToken)
        => Task.WhenAll(_probes.Select(p => ProbeTargetAsync(p, settings, stoppingToken)));

    private async Task ProbeTargetAsync(IHealthProbe probe, VigilRuntimeSettings settings, CancellationToken stoppingToken)
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

        EvaluateIncident(probe, current, settings);
        LogTransition(previous, current);

        if (previous is null || previous.Status != current.Status)
        {
            PublishStatus(current);
        }
    }

    private void EvaluateIncident(IHealthProbe probe, ServiceHealthStatus current, VigilRuntimeSettings settings)
    {
        var open = incidentStore.GetOpen(probe.Target);

        switch (current.Status)
        {
            case ServiceStatus.Unhealthy when open is null:
                PublishIncident(
                    incidentStore.Open(probe.Target, probe.Kind, IncidentSeverity.Critical, current.Status, current.Details),
                    VigilFeedEventTypes.IncidentOpened);
                break;

            case ServiceStatus.Degraded when open is null && current.ConsecutiveFailures >= settings.ConsecutiveFailuresToOpenIncident:
                PublishIncident(
                    incidentStore.Open(probe.Target, probe.Kind, IncidentSeverity.Warning, current.Status, current.Details),
                    VigilFeedEventTypes.IncidentOpened);
                break;

            case ServiceStatus.Healthy when open is not null:
                PublishIncident(incidentStore.Resolve(probe.Target), VigilFeedEventTypes.IncidentResolved);
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

    private void PublishStatus(ServiceHealthStatus status)
    {
        var payload = new ServiceHealthPayload(
            ServiceName: status.ServiceName,
            Kind: status.Kind.ToString(),
            Status: status.Status.ToString(),
            LatencyMs: status.LatencyMs,
            UptimePercent: status.UptimePercent,
            Timestamp: status.CheckedAt.ToString("O"),
            Details: status.Details);

        feed.Publish(new VigilFeedEvent(VigilFeedEventTypes.Status, payload));
    }

    private void PublishIncident(Incident? incident, string type)
    {
        if (incident is null)
        {
            return;
        }

        logger.LogWarning("Incident {Action} for {Target} (severity {Severity})",
            type == VigilFeedEventTypes.IncidentResolved ? "RESOLVED" : "OPENED", incident.Target, incident.Severity);

        feed.Publish(new VigilFeedEvent(type, incident));
    }

    private static ServiceHealthStatus ProbeFault(IHealthProbe probe, string message)
    {
        return new()
        {
            ServiceName = probe.Target,
            Kind = probe.Kind,
            Status = ServiceStatus.Unhealthy,
            LatencyMs = 0,
            CheckedAt = DateTimeOffset.UtcNow,
            Details = message
        };
    }
}
