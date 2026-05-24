namespace Sentium.Watchdog.Core.Monitoring;

public interface IServiceHealthStateStore
{
    void UpdateStatus(ServiceHealthStatus status);
    IReadOnlyList<ServiceHealthStatus> GetAll();
    ServiceHealthStatus? Get(string serviceName);
}
