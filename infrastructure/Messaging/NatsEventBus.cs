using System.Text.Json;
using NATS.Client;

namespace Infrastructure.Messaging;

public sealed class NatsEventBus : IEventBus, IDisposable, IAsyncDisposable
{
    private readonly IConnection Connection;

    public NatsEventBus(string url)
    {
        Connection = new ConnectionFactory().CreateConnection(url);
    }

    public void Publish<T>(string subject, T message)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(message);
        Connection.Publish(subject, payload);
        Connection.Flush();
    }

    public void Subscribe<T>(string subject, Action<T> handler)
    {
        var sub = Connection.SubscribeAsync(subject);
        sub.MessageHandler += (_, args) =>
        {
            var data = JsonSerializer.Deserialize<T>(args.Message.Data);
            if (data != null)
            {
                handler(data);
            }
        };
        sub.Start();
    }

    public void Dispose()
    {
        if (Connection != null && !Connection.IsClosed())
        {
            Connection.Drain();
            Connection.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (Connection != null && !Connection.IsClosed())
        {
            try
            {
                await Connection.DrainAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error draining NATS connection: {ex.Message}");
            }
            finally
            {
                Connection.Dispose();
            }
        }

        GC.SuppressFinalize(this);
    }
}
