using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;

namespace Sentium.Infrastructure.Streaming;

/// <summary>
/// Server-Sent Event responses on the framework's own <see cref="TypedResults.ServerSentEvents{T}(IAsyncEnumerable{SseItem{T}})"/>,
/// with a heartbeat merged in. The heartbeat is a named <c>ping</c> event, not an SSE comment: a browser
/// <c>EventSource</c> never surfaces comments to script, and the portal needs to see the heartbeat to tell a live
/// connection from one that silently died (for example after the machine slept). <c>onmessage</c> handlers never see
/// a named event, so the ping does not disturb them.
/// </summary>
public static class SseStreams
{
    public const string PingEventType = "ping";

    public static readonly TimeSpan DefaultHeartbeatInterval = TimeSpan.FromSeconds(15);

    private static readonly SseItem<string> Ping = new("{}", PingEventType);

    /// <summary>
    /// Streams <paramref name="items"/> (each one's data written as-is, so pass serialized JSON) with a heartbeat after
    /// every <paramref name="heartbeatInterval"/> of silence. The stream ends quietly when <paramref name="ct"/> fires
    /// or <paramref name="items"/> throws <see cref="OperationCanceledException"/>, so a client going away or a
    /// per-connection cap is not logged as a failure.
    /// </summary>
    public static IResult Create(IAsyncEnumerable<SseItem<string>> items, TimeSpan heartbeatInterval, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(heartbeatInterval, TimeSpan.Zero);

        return new SseStreamResult(TypedResults.ServerSentEvents(WithHeartbeat(items, heartbeatInterval, ct)));
    }

    public static async IAsyncEnumerable<SseItem<string>> WithHeartbeat(
        IAsyncEnumerable<SseItem<string>> items,
        TimeSpan heartbeatInterval,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(items);

        var enumerator = items.GetAsyncEnumerator(ct);
        var next = enumerator.MoveNextAsync().AsTask();

        try
        {
            while (true)
            {
                var wait = await WaitAsync(next, heartbeatInterval, ct);
                if (wait == WaitResult.End)
                {
                    yield break;
                }

                if (wait == WaitResult.Heartbeat)
                {
                    yield return Ping;
                    continue;
                }

                yield return enumerator.Current;
                next = enumerator.MoveNextAsync().AsTask();
            }
        }
        finally
        {
            // An async iterator cannot be disposed while a MoveNextAsync is still pending.
            await SettleAsync(next);
            await enumerator.DisposeAsync();
        }
    }

    private static async Task<WaitResult> WaitAsync(Task<bool> next, TimeSpan heartbeatInterval, CancellationToken ct)
    {
        try
        {
            return await next.WaitAsync(heartbeatInterval, ct) ? WaitResult.Item : WaitResult.End;
        }
        catch (TimeoutException)
        {
            return WaitResult.Heartbeat;
        }
        catch (OperationCanceledException)
        {
            return WaitResult.End;
        }
    }

    private static async Task SettleAsync(Task<bool> next)
    {
        try
        {
            await next;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private enum WaitResult
    {
        Item,
        Heartbeat,
        End
    }
}
