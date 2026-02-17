using Sentinel.Core.Events;

namespace Sentinel.Core.Sensors;

public interface INetworkSensor
{
    Task ScanAsync(Func<SentinelEvent, Task> onNewConnection, CancellationToken ct);
}
