using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Infrastructure.Security;
using Sentium.Infrastructure.Streaming;

namespace Sentium.Infrastructure.Realtime;

public static class RealtimeEndpoints
{
    public const string StreamRoute = "api/realtime/stream";

    /// <summary>
    /// How long a browser waits before reconnecting on its own after a network error (the SSE <c>retry</c> field).
    /// </summary>
    private static readonly TimeSpan ReconnectionInterval = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Registers the realtime fan-out behind <see cref="IRealtimePublisher"/>.
    /// </summary>
    public static IServiceCollection AddSentiumRealtime(this IServiceCollection services)
    {
        services.AddSingleton<RealtimeHub>();
        services.AddSingleton<IRealtimePublisher>(sp => sp.GetRequiredService<RealtimeHub>());
        return services;
    }

    /// <summary>
    /// Maps <c>GET /api/realtime/stream</c>: the signed-in user's Server-Sent Event stream of <see cref="RealtimeTopics"/>
    /// frames (<c>{ topic, data }</c>), opening with a <see cref="RealtimeTopics.Connected"/> frame.
    /// </summary>
    public static IEndpointConventionBuilder MapSentiumRealtime(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet(StreamRoute, Stream).RequireAuthorization().ExcludeFromDescription();
    }

    private static IResult Stream(RealtimeHub hub, ICurrentUser currentUser, HttpContext context)
    {
        if (currentUser.UserId is not { } userId)
        {
            return TypedResults.Unauthorized();
        }

        var ct = context.RequestAborted;
        return SseStreams.Create(ReadAsync(hub, userId, currentUser.IsSovereign, ct), SseStreams.DefaultHeartbeatInterval, ct);
    }

    /// <summary>
    /// Subscribes before the connected frame, so nothing published after the client sees it is lost.
    /// </summary>
    private static async IAsyncEnumerable<SseItem<string>> ReadAsync(
        RealtimeHub hub,
        Guid userId,
        bool isSovereign,
        [EnumeratorCancellation] CancellationToken ct)
    {
        using var subscription = hub.Subscribe(userId, isSovereign);

        yield return new SseItem<string>(RealtimeHub.Serialize(new RealtimeFrame(RealtimeTopics.Connected)))
        {
            ReconnectionInterval = ReconnectionInterval
        };

        await foreach (var frame in subscription.Reader.ReadAllAsync(ct))
        {
            yield return new SseItem<string>(frame);
        }
    }
}
