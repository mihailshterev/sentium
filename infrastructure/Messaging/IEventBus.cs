namespace Infrastructure.Messaging;

public interface IEventBus
{
    void Publish<T>(string subject, T message);
    void Subscribe<T>(string subject, Action<T> handler);
}
