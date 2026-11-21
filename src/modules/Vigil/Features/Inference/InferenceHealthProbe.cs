using System.Globalization;
using Sentium.Nuntius.Contracts;
using Sentium.Vigil.Features.Monitoring;
using Sentium.Vigil.Features.Monitoring.Probes;

namespace Sentium.Vigil.Features.Inference;

/// <summary>
/// Judges inference from the collector's latest view instead of calling the servers again. Requests waiting for
/// a free slot, or a model whose last load failed, make it Degraded; the monitoring worker opens an incident once
/// that lasts its usual number of polls, so a brief queue never alarms.
/// </summary>
internal sealed class InferenceHealthProbe(InferenceLiveState liveState) : IIncidentCategoryProbe
{
    public const string TargetName = "Inference";

    public string Target => TargetName;
    public ComponentKind Kind => ComponentKind.Infrastructure;
    public string NotificationCategory => NotificationCategories.SystemInference;

    public Task<ServiceHealthStatus> ProbeAsync(CancellationToken ct)
    {
        var models = liveState.Snapshot();

        return Task.FromResult(Evaluate(models));
    }

    internal static ServiceHealthStatus Evaluate(IReadOnlyList<InferenceModelState> models)
    {
        var failed = models.Where(m => m.LoadFailed).Select(m => m.Model).ToList();
        if (failed.Count > 0)
        {
            return Result(ServiceStatus.Degraded, $"Failed to load: {string.Join(", ", failed)}");
        }

        var queued = models.Where(m => m.RequestsDeferred > 0).ToList();
        if (queued.Count > 0)
        {
            var waiting = string.Join(", ", queued.Select(m => string.Create(CultureInfo.InvariantCulture, $"{m.RequestsDeferred} for {m.Model}")));
            return Result(ServiceStatus.Degraded, $"Requests waiting for a free slot: {waiting}");
        }

        var loaded = models.Count(m => string.Equals(m.Status, ServedModel.Loaded, StringComparison.OrdinalIgnoreCase));
        if (loaded == 0)
        {
            return Result(ServiceStatus.Unknown, "No model is loaded");
        }

        return Result(ServiceStatus.Healthy, string.Create(CultureInfo.InvariantCulture, $"{loaded} model(s) loaded, no queue"));
    }

    private static ServiceHealthStatus Result(ServiceStatus status, string details)
    {
        return ProbeResult.Create(TargetName, ComponentKind.Infrastructure, status, latencyMs: 0, details);
    }
}
