using Microsoft.Extensions.Logging;
using NATS.Client.Core;

namespace Infrastructure.Messaging;

public sealed class NatsEventBus(INatsConnection connection, ILogger<NatsEventBus> logger) : IEventBus
{
    public async Task PublishAsync<T>(string subject, T message, INatsSerializer<T>? serializer = null, CancellationToken ct = default)
    {
        await connection.PublishAsync(subject, message, serializer: serializer, cancellationToken: ct);
    }

    public Task SubscribeAsync<T>(string subject, Func<NatsMsg<T>, Task> handler, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(handler);

        _ = Task.Run(async () =>
        {
            try
            {
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
}
