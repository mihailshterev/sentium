using Sentinel.Core.Events;

namespace Sentinel.Core.Stores;

public interface INetworkEventStore
{
    void Add(NetworkEventRecord record);
    IReadOnlyList<NetworkEventRecord> GetRecent(int count = 100);
}
