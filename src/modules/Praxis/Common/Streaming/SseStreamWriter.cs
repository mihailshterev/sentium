using System.Buffers;
using System.Text.Json;
using Sentium.Core.Constants;

namespace Sentium.Praxis.Common.Streaming;

/// <summary>
/// Writes the assistant's Server-Sent Events stream: <c>data: {json}</c> frames, serialized through one lock so the
/// keep-alive heartbeat never interleaves with a frame. Dispose to stop the heartbeat.
/// </summary>
internal sealed class SseStreamWriter : IAsyncDisposable
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);

    private static readonly byte[] DataPrefix = "data: "u8.ToArray();
    private static readonly byte[] FrameDelimiter = "\n\n"u8.ToArray();
    private static readonly byte[] HeartbeatLine = ": ping\n\n"u8.ToArray();
    private static readonly byte[] KeepAliveLine = ": keep-alive\n\n"u8.ToArray();

    private readonly HttpResponse response;
    private readonly ILogger logger;
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private readonly CancellationTokenSource heartbeatCts;
    private Task heartbeat = Task.CompletedTask;

    private SseStreamWriter(HttpResponse response, ILogger logger, CancellationToken requestAborted)
    {
        this.response = response;
        this.logger = logger;
        heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
    }

    /// <summary>
    /// Sets the SSE headers, flushes an initial keep-alive comment and starts the heartbeat.
    /// </summary>
    public static async Task<SseStreamWriter> StartAsync(HttpResponse response, ILogger logger, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        response.Headers.Connection = "keep-alive";
        response.Headers.Append(CommonHeaderNames.AccelBuffering, "no");

        await response.StartAsync(ct);

        var writer = new SseStreamWriter(response, logger, ct);
        await writer.WriteRawAsync(KeepAliveLine, ct);
        writer.heartbeat = writer.RunHeartbeatAsync(writer.heartbeatCts.Token);

        return writer;
    }

    /// <summary>
    /// Writes a UI update frame: <c>{"type": …, "message": {"content": …}, "done": false}</c>.
    /// </summary>
    public async Task SendUpdateAsync(string type, string content, CancellationToken ct)
    {
        await writeLock.WaitAsync(ct);
        try
        {
            var pipe = response.BodyWriter;
            pipe.Write(DataPrefix);

            await using (var json = new Utf8JsonWriter(pipe))
            {
                json.WriteStartObject();
                json.WriteString("type", type);
                json.WriteStartObject("message");
                json.WriteString("content", content);
                json.WriteEndObject();
                json.WriteBoolean("done", false);
                json.WriteEndObject();
                await json.FlushAsync(ct);
            }

            pipe.Write(FrameDelimiter);
            await pipe.FlushAsync(ct);
        }
        finally
        {
            writeLock.Release();
        }
    }

    /// <summary>
    /// Writes a terminal or custom frame serialized from <paramref name="frame"/>.
    /// </summary>
    public async Task SendFrameAsync<T>(T frame, CancellationToken ct)
    {
        await writeLock.WaitAsync(ct);
        try
        {
            await response.WriteAsync($"data: {JsonSerializer.Serialize(frame)}\n\n", ct);
            await response.Body.FlushAsync(ct);
        }
        finally
        {
            writeLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await heartbeatCts.CancelAsync();
        await heartbeat;
        heartbeatCts.Dispose();
        writeLock.Dispose();
    }

    private async Task WriteRawAsync(byte[] bytes, CancellationToken ct)
    {
        await writeLock.WaitAsync(ct);
        try
        {
            response.BodyWriter.Write(bytes);
            await response.BodyWriter.FlushAsync(ct);
        }
        finally
        {
            writeLock.Release();
        }
    }

    private async Task RunHeartbeatAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(HeartbeatInterval, ct);
                await WriteRawAsync(HeartbeatLine, ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "SSE heartbeat stopped due to write failure.");
        }
    }
}
