using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Sentium.Infrastructure.Streaming;
using Sentium.Praxis.Common.Streaming;

namespace Sentium.Praxis.Features.Orchestration;

/// <summary>
/// The Server-Sent Event view of an <see cref="IStreamRelay"/> stream, shared by workflow runs and assistant turns.
/// Each update is written as <c>{ author, text, type }</c> with its relay sequence number as the SSE <c>id</c>, and the
/// stream ends with exactly one terminal frame: <see cref="StreamDoneFrame"/> (done, cancelled or
/// <see cref="AgentUpdateTypes.Gone"/>) or <see cref="StreamErrorFrame"/>.
/// </summary>
internal static class RelayStreams
{
    private const string LastEventIdHeader = "Last-Event-ID";
    private const string AfterQueryKey = "after";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Streams the buffered frames after <paramref name="fromSeq"/>, then live ones. A connection is capped at
    /// <paramref name="maxDuration"/>; the client then reconnects and resumes from its last id. A stream the relay no
    /// longer holds gets a single <see cref="AgentUpdateTypes.Gone"/> frame.
    /// </summary>
    public static IResult Create(
        IStreamRelay relay,
        string streamId,
        long fromSeq,
        TimeSpan heartbeatInterval,
        TimeSpan maxDuration,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(relay);

        return SseStreams.Create(ReadAsync(relay, streamId, fromSeq, maxDuration, ct), heartbeatInterval, ct);
    }

    /// <summary>
    /// The sequence number to resume after: the <c>Last-Event-ID</c> an EventSource sends on its own reconnects, or the
    /// <c>after</c> query parameter of a new connection that resumes (a new EventSource cannot set headers).
    /// </summary>
    public static long ResumePoint(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var raw = request.Headers[LastEventIdHeader].ToString();
        if (string.IsNullOrEmpty(raw))
        {
            raw = request.Query[AfterQueryKey].ToString();
        }

        return long.TryParse(raw, out var seq) && seq > 0 ? seq : 0;
    }

    private static async IAsyncEnumerable<SseItem<string>> ReadAsync(
        IStreamRelay relay,
        string streamId,
        long fromSeq,
        TimeSpan maxDuration,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (!relay.Exists(streamId))
        {
            yield return Item(new StreamDoneFrame(AgentUpdateTypes.Gone));
            yield break;
        }

        using var capped = CancellationTokenSource.CreateLinkedTokenSource(ct);
        capped.CancelAfter(maxDuration);

        await foreach (var (seq, update) in relay.Subscribe(streamId, fromSeq, capped.Token))
        {
            if (update.Type is AgentUpdateTypes.Done or AgentUpdateTypes.Cancelled)
            {
                yield return Item(new StreamDoneFrame(update.Type), seq);
                yield break;
            }

            if (update.Type == AgentUpdateTypes.Error)
            {
                yield return Item(new StreamErrorFrame(update.Text), seq);
                yield break;
            }

            if (!string.IsNullOrEmpty(update.Text))
            {
                yield return Item(update, seq);
            }
        }
    }

    private static SseItem<string> Item<T>(T payload, long? seq = null)
    {
        return new SseItem<string>(JsonSerializer.Serialize(payload, JsonOptions))
        {
            EventId = seq?.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
    }
}
