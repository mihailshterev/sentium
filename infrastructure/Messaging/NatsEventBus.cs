using System.Runtime.CompilerServices;
using NATS.Client.Core;

namespace Infrastructure.Messaging;

public sealed class NatsEventBus(INatsConnection connection) : IEventBus, IAsyncDisposable
{
    private bool Disposed;

    public async Task PublishAsync<T>(string subject, T message, CancellationToken ct = default)
    {
        await connection.PublishAsync(subject, message, cancellationToken: ct);
    }

    public async IAsyncEnumerable<T> SubscribeAsync<T>(string subject, Action<T> handler, [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(handler);
        await foreach (var msg in connection.SubscribeAsync<T>(subject, cancellationToken: ct).WithCancellation(ct))
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

        await connection.DisposeAsync();
        Disposed = true;
        GC.SuppressFinalize(this);
    }
}
