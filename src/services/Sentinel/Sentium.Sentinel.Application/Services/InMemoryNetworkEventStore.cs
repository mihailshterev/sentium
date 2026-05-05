using Sentium.Sentinel.Core.Events;
using Sentium.Sentinel.Core.Stores;

namespace Sentium.Sentinel.Application.Services;

public sealed class InMemoryNetworkEventStore : INetworkEventStore
{
    private const int MaxCapacity = 200;

    private readonly LinkedList<NetworkEventRecord> _buffer = new();
    private readonly object _lock = new();

    public void Add(NetworkEventRecord record)
    {
        lock (_lock)
        {
            _buffer.AddFirst(record);
            if (_buffer.Count > MaxCapacity)
            {
                _buffer.RemoveLast();
            }
        }
    }

    public IReadOnlyList<NetworkEventRecord> GetRecent(int count = 100)
    {
        lock (_lock)
        {
            return _buffer.Take(Math.Max(1, count)).ToList();
        }
    }
}
