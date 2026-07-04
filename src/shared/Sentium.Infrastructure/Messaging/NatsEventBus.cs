using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.Serializers.Json;

namespace Sentium.Infrastructure.Messaging;

public sealed class NatsEventBus(INatsConnection connection, ILogger<NatsEventBus> logger) : IEventBus
{
    private readonly Lazy<NatsJSContext> _jetStream = new(() => new NatsJSContext(connection));

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

    public async Task PublishPersistentAsync<T>(string subject, T message, string? messageId = null, CancellationToken ct = default)
    {
        var selectedSerializer = typeof(T) == typeof(byte[]) || typeof(T) == typeof(string) ? null : NatsJsonSerializer<T>.Default;
        var headers = string.IsNullOrEmpty(messageId) ? null : new NatsHeaders { ["Nats-Msg-Id"] = messageId };

        const int maxAttempts = 5;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var ack = await _jetStream.Value.PublishAsync(subject, message, serializer: selectedSerializer, headers: headers, cancellationToken: ct);
                if (ack.Duplicate)
                {
                    logger.LogInformation("Duplicate persistent publish to {Subject} (msgId {MessageId}) ignored by JetStream.", subject, messageId);
                }
                else
                {
                    logger.LogDebug("Persisted message to {Subject} (stream {Stream}, seq {Seq})", subject, ack.Stream, ack.Seq);
                }

                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && ex is NatsJSException or NatsNoRespondersException)
            {
                logger.LogWarning(ex, "Persistent publish to {Subject} failed (attempt {Attempt}/{Max}); retrying.", subject, attempt, maxAttempts);
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to persist message to {Subject}", subject);
                throw;
            }
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
