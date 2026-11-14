using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Core.Constants;
using Sentium.Vigil.Features.Monitoring;

namespace Sentium.Vigil.Features.Streaming;

/// <summary>
/// Server-Sent Events stream of live health and incident updates from <see cref="IVigilFeed"/>.
/// </summary>
[ApiController]
[Authorize]
[Route($"{VigilRoutes.Prefix}/stream")]
internal sealed class VigilStreamController(IVigilFeed feed, ILogger<VigilStreamController> logger) : ControllerBase
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(20);

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

        try
        {
            await WriteFrameAsync(new VigilConnectedFrame(), ct);

            using var subscription = feed.Subscribe();

            while (true)
            {
                using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                waitCts.CancelAfter(HeartbeatInterval);

                try
                {
                    if (!await subscription.Reader.WaitToReadAsync(waitCts.Token))
                    {
                        break;
                    }
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    await Response.WriteAsync(": heartbeat\n\n", ct);
                    await Response.Body.FlushAsync(ct);
                    continue;
                }

                while (subscription.Reader.TryRead(out var update))
                {
                    await WriteFrameAsync(new VigilStreamFrame<object>(update.Type, update.Data), ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Client disconnected from Vigil stream");
        }
    }

    private async Task WriteFrameAsync(object payload, CancellationToken ct)
    {
        await Response.WriteAsync($"data: {JsonSerializer.Serialize(payload, JsonOptions)}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }
}
