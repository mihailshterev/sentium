using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.Serializers.Json;

namespace Sentium.Infrastructure.Messaging;

public sealed class NatsEventBus(INatsConnection connection, ILogger<NatsEventBus> logger) : IEventBus
{
    public async Task PublishAsync<T>(string subject, T message, INatsSerializer<T>? serializer = null, CancellationToken ct = default)
    {
        try
        {
            var selectedSerializer = serializer ?? (typeof(T) == typeof(byte[]) || typeof(T) == typeof(string) ? null : NatsJsonSerializer<T>.Default);
            await connection.PublishAsync(subject, message, serializer: selectedSerializer, cancellationToken: ct);
            logger.LogDebug("Published message to {Subject}", subject);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish message to {Subject}", subject);
            throw;
        }
    }

    public Task SubscribeAsync<T>(string subject, Func<NatsMsg<T>, Task> handler, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(handler);

        _ = Task.Run(async () =>
        {
            try
            {
                INatsSerializer<T>? serializer = typeof(T) == typeof(byte[]) ? null : NatsJsonSerializer<T>.Default;

                await foreach (var msg in connection.SubscribeAsync(subject, serializer: serializer, cancellationToken: ct).WithCancellation(ct))
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
            catch (OperationCanceledException)
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Subscription to {Subject} was cancelled.", subject);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fatal error in subscription loop for {Subject}", subject);
            }
        }, ct);

        return Task.CompletedTask;
    }

    public IAsyncEnumerable<NatsMsg<T>> SubscribeStreamAsync<T>(string subject, INatsSerializer<T>? serializer = null, CancellationToken ct = default)
    {
        var selectedSerializer = serializer ?? (typeof(T) == typeof(byte[]) ? null : NatsJsonSerializer<T>.Default);
        return connection.SubscribeAsync(subject, serializer: selectedSerializer, cancellationToken: ct);
    }
}
