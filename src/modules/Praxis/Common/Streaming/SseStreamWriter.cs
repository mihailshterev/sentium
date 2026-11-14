using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sentium.Core.Constants;

namespace Sentium.Praxis.Common.Streaming;

/// <summary>
/// The Server-Sent Events transport for the assistant and workflow streams. It speaks two frame shapes: the assistant
/// writes <c>{"type", "message": {"content"}, "done"}</c> frames through <see cref="WriteUpdateAsync"/>, while
/// <see cref="ForwardAsync"/> relays a workflow's <see cref="AgentStreamUpdate"/>s as-is, with the relay sequence
/// number as the SSE <c>id</c>. Both end with the shared <see cref="StreamDoneFrame"/> or <see cref="StreamErrorFrame"/>.
/// Every write goes through one lock, so the keep-alive heartbeat never interleaves with a frame. A stream ends with at
/// most one terminal frame (<see cref="CompleteAsync"/>, <see cref="FailAsync"/> or a terminal relay update in
/// <see cref="ForwardAsync"/>); anything written after it is dropped. Dispose to stop the heartbeat.
/// </summary>
public sealed class SseStreamWriter : IAgentUpdateSink, IAsyncDisposable
{
    private const string IdField = "id: ";
    private const string DataField = "data: ";
    private const string FrameEnd = "\n\n";

    private static readonly TimeSpan DefaultHeartbeatInterval = TimeSpan.FromSeconds(15);
    private static readonly byte[] OpeningComment = ": connected\n\n"u8.ToArray();
    private static readonly byte[] HeartbeatComment = ": ping\n\n"u8.ToArray();

    private readonly HttpResponse response;
    private readonly ILogger logger;
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private readonly CancellationTokenSource heartbeatCts;
    private Task heartbeat = Task.CompletedTask;
    private bool completed;

    private SseStreamWriter(HttpResponse response, ILogger logger, CancellationToken requestAborted)
    {
        this.response = response;
        this.logger = logger;
        heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
    }

    public static Task<SseStreamWriter> StartAsync(HttpResponse response, ILogger logger, CancellationToken ct) =>
        StartAsync(response, DefaultHeartbeatInterval, logger, ct);

    /// <summary>
    /// Sets the SSE headers, flushes an opening comment and starts the heartbeat.
    /// </summary>
    public static async Task<SseStreamWriter> StartAsync(HttpResponse response, TimeSpan heartbeatInterval, ILogger logger, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(heartbeatInterval, TimeSpan.Zero);

        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        response.Headers.Connection = "keep-alive";
        response.Headers.Append(CommonHeaderNames.AccelBuffering, "no");

        await response.StartAsync(ct);

        var writer = new SseStreamWriter(response, logger, ct);
        await writer.WriteAsync(OpeningComment, terminal: false, ct);
        writer.heartbeat = writer.RunHeartbeatAsync(heartbeatInterval, writer.heartbeatCts.Token);

        return writer;
    }

    /// <summary>
    /// Writes an assistant update frame: <c>{"type": …, "message": {"content": …}, "done": false}</c>.
    /// </summary>
    public Task WriteUpdateAsync(string type, string content, CancellationToken ct) =>
        WriteAsync(DataFrame(new UpdateFrame(type, new UpdateMessage(content))), terminal: false, ct);

    public Task CompleteAsync(CancellationToken ct) =>
        WriteAsync(DataFrame(new StreamDoneFrame(AgentUpdateTypes.Done)), terminal: true, ct);

    /// <summary>
    /// Ends the stream with an error frame. Best effort: it ignores cancellation and never throws, so it is safe to
    /// call from a catch block after the client may already have gone.
    /// </summary>
    public async Task FailAsync(string message)
    {
        try
        {
            await WriteAsync(DataFrame(new StreamErrorFrame(message)), terminal: true, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to write the terminal SSE error frame.");
        }
    }

    /// <summary>
    /// Writes relay frames with their sequence number as the SSE <c>id</c>, so an EventSource reconnect resumes from
    /// <c>Last-Event-ID</c>. Frames without text are skipped. Returns after the first terminal update (done, error or
    /// cancelled), which becomes the stream's terminal frame.
    /// </summary>
    public async Task ForwardAsync(IAsyncEnumerable<StreamFrame> frames, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(frames);

        await foreach (var (seq, update) in frames.WithCancellation(ct))
        {
            if (update.Type is AgentUpdateTypes.Done or AgentUpdateTypes.Cancelled)
            {
                await WriteAsync(DataFrame(new StreamDoneFrame(update.Type), seq), terminal: true, ct);
                return;
            }

            if (update.Type == AgentUpdateTypes.Error)
            {
                await WriteAsync(DataFrame(new StreamErrorFrame(update.Text), seq), terminal: true, ct);
                return;
            }

            if (!string.IsNullOrEmpty(update.Text))
            {
                await WriteAsync(DataFrame(update, seq), terminal: false, ct);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await heartbeatCts.CancelAsync();
        await heartbeat;
        heartbeatCts.Dispose();
        writeLock.Dispose();
    }

    private static byte[] DataFrame<T>(T payload, long? id = null)
    {
        var json = JsonSerializer.Serialize(payload);
        var frame = id is { } seq ? $"{IdField}{seq}\n{DataField}{json}{FrameEnd}" : $"{DataField}{json}{FrameEnd}";
        return Encoding.UTF8.GetBytes(frame);
    }

    private async Task WriteAsync(byte[] bytes, bool terminal, CancellationToken ct)
    {
        await writeLock.WaitAsync(ct);
        try
        {
            if (completed)
            {
                return;
            }

            // Marked before the write so a failed terminal write is not followed by a second terminal frame.
            completed = terminal;

            await response.Body.WriteAsync(bytes, ct);
            await response.Body.FlushAsync(ct);
        }
        finally
        {
            writeLock.Release();
        }
    }

    private async Task RunHeartbeatAsync(TimeSpan interval, CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(interval);
            while (await timer.WaitForNextTickAsync(ct))
            {
                await WriteAsync(HeartbeatComment, terminal: false, ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "SSE heartbeat stopped due to a write failure.");
        }
    }

    private sealed record UpdateFrame(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("message")] UpdateMessage Message,
        [property: JsonPropertyName("done")] bool Done = false);

    private sealed record UpdateMessage([property: JsonPropertyName("content")] string Content);
}
