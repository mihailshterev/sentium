using System.Runtime.CompilerServices;
using NATS.Client.Core;

namespace Infrastructure.Messaging;

public sealed class NatsEventBus : IEventBus, IAsyncDisposable
{
    private readonly NatsConnection Connection;
    private bool Disposed;

    public NatsEventBus(string url)
    {
        Connection = new NatsConnection(new NatsOpts { Url = url });
    }

    public async Task PublishAsync<T>(string subject, T message, CancellationToken ct = default)
    {
        await Connection.PublishAsync(subject, message, cancellationToken: ct);
    }

    public async IAsyncEnumerable<T> SubscribeAsync<T>(string subject, Action<T> handler, [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(handler);
        await foreach (var msg in Connection.SubscribeAsync<T>(subject, cancellationToken: ct).WithCancellation(ct))
        {
            if (msg.Data is not null)
            {
                yield return msg.Data;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Disposed)
        {
            return;
        }

        await Connection.DisposeAsync();
        Disposed = true;
        GC.SuppressFinalize(this);
    }
}
