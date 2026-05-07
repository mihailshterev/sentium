using System.Collections.Concurrent;
using Sentium.Watchdog.Core.Monitoring;

namespace Sentium.Watchdog.Application.Monitoring;

public sealed class ServiceHealthStateStore : IServiceHealthStateStore
{
    private readonly ConcurrentDictionary<string, ServiceHealthStatus> _statuses = new();

    public void UpdateStatus(ServiceHealthStatus status)
        => _statuses[status.ServiceName] = status;

    public IReadOnlyList<ServiceHealthStatus> GetAll()
        => [.. _statuses.Values.OrderBy(s => s.ServiceName)];

    public ServiceHealthStatus? Get(string serviceName)
        => _statuses.GetValueOrDefault(serviceName);
}
