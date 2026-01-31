namespace Infrastructure.Messaging;

public interface IEventBus
{
    Task PublishAsync<T>(string subject, T message, CancellationToken ct = default);
    IAsyncEnumerable<T> SubscribeAsync<T>(string subject, Action<T> handler, CancellationToken ct = default);
}
