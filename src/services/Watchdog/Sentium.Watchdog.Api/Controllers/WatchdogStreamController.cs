using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NATS.Client.Serializers.Json;
using Sentium.Infrastructure.Messaging;
using Sentium.Shared.Constants;
using Sentium.Watchdog.Core.Dtos;
using Sentium.Watchdog.Core.Monitoring;

namespace Sentium.Watchdog.Api.Controllers;

/// <summary>
/// Server-Sent Events stream of live health and incident updates, backed by NATS.
/// </summary>
[ApiController]
[Authorize]
[Route("stream")]
public sealed class WatchdogStreamController(IEventBus eventBus, ILogger<WatchdogStreamController> logger) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Opens the Server-Sent Events stream of live health and incident updates.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task Stream(CancellationToken ct)
    {
        Response.Headers.Append(CommonHeaderNames.ContentType, "text/event-stream");
        Response.Headers.Append(CommonHeaderNames.CacheControl, "no-cache");
        Response.Headers.Append(CommonHeaderNames.Connection, "keep-alive");
        Response.Headers.Append(CommonHeaderNames.AccelBuffering, "no");

        await WriteFrameAsync(new WatchdogConnectedFrame(), ct);

        var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });

        var subscriptions = new[]
        {
            PumpAsync<ServiceHealthPayload>(NatsSubjects.WatchdogStatusUpdates, "status", channel.Writer, ct),
            PumpAsync<Incident>(NatsSubjects.WatchdogIncidentOpened, "incident.opened", channel.Writer, ct),
            PumpAsync<Incident>(NatsSubjects.WatchdogIncidentResolved, "incident.resolved", channel.Writer, ct)
        };

        _ = Task.WhenAll(subscriptions).ContinueWith(_ => channel.Writer.TryComplete(), TaskScheduler.Default);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var read = channel.Reader.ReadAsync(ct).AsTask();
                var completed = await Task.WhenAny(read, Task.Delay(TimeSpan.FromSeconds(20), ct));

                if (completed != read)
                {
                    await Response.WriteAsync(": heartbeat\n\n", ct);
                    await Response.Body.FlushAsync(ct);
                    continue;
                }

                var payload = await read;
                await Response.WriteAsync(payload, ct);
                await Response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Client disconnected from Watchdog stream");
        }
        catch (ChannelClosedException)
        {
        }
    }

    private async Task PumpAsync<T>(string subject, string type, ChannelWriter<string> writer, CancellationToken ct)
    {
        try
        {
            await foreach (var msg in eventBus.SubscribeStreamAsync<T>(subject, serializer: NatsJsonSerializer<T>.Default, ct: ct))
            {
                if (msg.Data is null)
                {
                    continue;
                }

                var frame = $"data: {JsonSerializer.Serialize(new WatchdogStreamFrame<T>(type, msg.Data), JsonOptions)}\n\n";
                await writer.WriteAsync(frame, ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Watchdog stream subscription for {Subject} failed", subject);
        }
    }

    private async Task WriteFrameAsync(object payload, CancellationToken ct)
    {
        await Response.WriteAsync($"data: {JsonSerializer.Serialize(payload, JsonOptions)}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }
}
