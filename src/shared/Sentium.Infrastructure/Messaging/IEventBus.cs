using NATS.Client.Core;

namespace Sentium.Infrastructure.Messaging;

/// <summary>
/// Publish/subscribe and streaming abstraction over the NATS message bus.
/// </summary>
public interface IEventBus
{
    Task PublishAsync<T>(string subject, T message, INatsSerializer<T>? serializer = null, CancellationToken ct = default);
    Task SubscribeAsync<T>(string subject, Func<NatsMsg<T>, Task> handler, CancellationToken ct = default);

    /// <summary>
    /// Subscribes and yields messages as an async stream.
    /// </summary>
    IAsyncEnumerable<NatsMsg<T>> SubscribeStreamAsync<T>(string subject, INatsSerializer<T>? serializer = null, CancellationToken ct = default);
}
