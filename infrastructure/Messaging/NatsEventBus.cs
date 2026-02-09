using Microsoft.Extensions.Logging;
using NATS.Client.Core;

namespace Infrastructure.Messaging;

public sealed class NatsEventBus(INatsConnection connection, ILogger<NatsEventBus> logger) : IEventBus, IAsyncDisposable
{
    private bool Disposed;

    public async Task PublishAsync<T>(string subject, T message, CancellationToken ct = default)
    {
        await connection.PublishAsync(subject, message, cancellationToken: ct);
    }

    public async Task SubscribeAsync<T>(string subject, Func<NatsMsg<T>, Task> handler, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(handler);

        await foreach (var msg in connection.SubscribeAsync<T>(subject, cancellationToken: ct).WithCancellation(ct))
        {
            if (msg.Data is not null)
            {
                try
                {
                    await handler(msg);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error handling message for subject {Subject}", subject);
                }
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
