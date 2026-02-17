using NATS.Client.Core;

namespace Infrastructure.Messaging;

public interface IEventBus
{
    Task PublishAsync<T>(string subject, T message, INatsSerializer<T>? serializer = null, CancellationToken ct = default);
    Task SubscribeAsync<T>(string subject, Func<NatsMsg<T>, Task> handler, CancellationToken ct = default);
}
