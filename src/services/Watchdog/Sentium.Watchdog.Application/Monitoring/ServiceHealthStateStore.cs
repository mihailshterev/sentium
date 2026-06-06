using System.Collections.Concurrent;
using Sentium.Watchdog.Core.Monitoring;

namespace Sentium.Watchdog.Application.Monitoring;

public sealed class ServiceHealthStateStore : IServiceHealthStateStore
{
    private const int MaxSamples = 200;

    private readonly ConcurrentDictionary<string, TargetState> _targets = new();

    public ServiceHealthStatus UpdateStatus(ServiceHealthStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        var state = _targets.GetOrAdd(status.ServiceName, _ => new TargetState());

        lock (state.Gate)
        {
            state.TotalChecks++;
            if (status.Status is ServiceStatus.Healthy or ServiceStatus.Degraded)
            {
                state.UpChecks++;
            }

            var previousStatus = state.Latest?.Status ?? ServiceStatus.Unknown;
            if (status.Status != previousStatus)
            {
                state.LastStateChange = status.CheckedAt;
            }

            state.ConsecutiveFailures = status.Status is ServiceStatus.Healthy ? 0 : state.ConsecutiveFailures + 1;

            state.Samples.Enqueue(new HealthSample(status.CheckedAt, status.Status, status.LatencyMs));
            while (state.Samples.Count > MaxSamples)
            {
                state.Samples.Dequeue();
            }

            var uptime = state.TotalChecks == 0 ? 0d : Math.Round((double)state.UpChecks / state.TotalChecks * 100d, 2);

            var enriched = status with
            {
                UptimePercent = uptime,
                LastStateChange = state.LastStateChange,
                ConsecutiveFailures = state.ConsecutiveFailures
            };

            state.Latest = enriched;
            return enriched;
        }
    }

    public IReadOnlyList<ServiceHealthStatus> GetAll()
        => [.. _targets.Values
            .Select(t => t.Latest)
            .Where(s => s is not null)
            .Select(s => s!)
            .OrderBy(s => s.Kind)
            .ThenBy(s => s.ServiceName)];

    public ServiceHealthStatus? Get(string serviceName) => _targets.GetValueOrDefault(serviceName)?.Latest;

    public IReadOnlyList<HealthSample> GetSamples(string serviceName, int take)
    {
        if (!_targets.TryGetValue(serviceName, out var state))
        {
            return [];
        }

        lock (state.Gate)
        {
            var count = Math.Min(Math.Max(take, 0), state.Samples.Count);
            return count == 0 ? [] : [.. state.Samples.TakeLast(count)];
        }
    }

    private sealed class TargetState
    {
        public object Gate { get; } = new();
        public ServiceHealthStatus? Latest { get; set; }
        public long TotalChecks { get; set; }
        public long UpChecks { get; set; }
        public int ConsecutiveFailures { get; set; }
        public DateTimeOffset LastStateChange { get; set; } = DateTimeOffset.UtcNow;
        public Queue<HealthSample> Samples { get; } = new();
    }
}
