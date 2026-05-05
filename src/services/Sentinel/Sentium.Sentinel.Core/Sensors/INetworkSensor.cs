using Sentium.Sentinel.Core.Events;

namespace Sentium.Sentinel.Core.Sensors;

public interface INetworkSensor
{
    Task ScanAsync(Func<SentinelEvent, Task> onNewConnection, CancellationToken ct);
}
