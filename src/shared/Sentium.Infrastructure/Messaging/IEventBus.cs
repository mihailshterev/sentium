using NATS.Client.Core;

namespace Sentium.Infrastructure.Messaging;

/// <summary>
/// Publish/subscribe and streaming abstraction over the NATS message bus.
/// </summary>
public interface IEventBus
{
    Task PublishAsync<T>(string subject, T message, INatsSerializer<T>? serializer = null, CancellationToken ct = default);

    /// <summary>
    /// Publishes a message durably to JetStream so it survives a subscriber restart and is delivered
    /// at-least-once. The message is stored on the stream that captures <paramref name="subject"/> and is
    /// only removed once a consumer acknowledges it. When <paramref name="messageId"/> is supplied it is
    /// sent as the <c>Nats-Msg-Id</c> header, enabling JetStream publish de-duplication within the
    /// stream's duplicate window (guards double-submits/retries).
    /// </summary>
    Task PublishPersistentAsync<T>(string subject, T message, string? messageId = null, CancellationToken ct = default);

    Task SubscribeAsync<T>(string subject, Func<NatsMsg<T>, Task> handler, CancellationToken ct = default);

    /// <summary>
    /// Subscribes and yields messages as an async stream.
    /// </summary>
    IAsyncEnumerable<NatsMsg<T>> SubscribeStreamAsync<T>(string subject, INatsSerializer<T>? serializer = null, CancellationToken ct = default);
}
