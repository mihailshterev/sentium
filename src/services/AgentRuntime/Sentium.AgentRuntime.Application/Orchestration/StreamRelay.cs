using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Workflows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.Serializers.Json;

namespace Sentium.AgentRuntime.Application.Orchestration;

public readonly record struct StreamFrame(long Seq, AgentStreamUpdate Update);

/// <summary>
/// In-process fan-out + replay buffer for agent stream updates. It subscribes to <c>stream.&gt;</c> once at
/// startup - before any workflow can emit - so no update is ever lost to the classic "subscribe after the
/// producer already published" race. SSE consumers attach via <see cref="Subscribe"/> and receive the full
/// backlog (filtered by <c>Last-Event-ID</c>) followed by live updates, which also makes EventSource
/// reconnects replay cleanly without duplicates.
/// </summary>
public interface IStreamRelay
{
    /// <summary>
    /// Replays buffered frames with <c>Seq &gt; <paramref name="fromSeqExclusive"/></c> then yields live frames,
    /// completing when a terminal (Done/Error) frame has been delivered or <paramref name="ct"/> fires.
    /// </summary>
    IAsyncEnumerable<StreamFrame> Subscribe(string streamId, long fromSeqExclusive, CancellationToken ct);
}

public sealed class StreamRelay(INatsConnection connection, IConfiguration configuration, ILogger<StreamRelay> logger) : BackgroundService, IStreamRelay
{
    private const string SubjectPrefix = "stream.";
    private const int MaxBufferedEntries = 5000;
    private const int MaxBufferedChars = 1_000_000;
    private const int MaxSubscriberQueue = 8192;
    private const int MaxTrackedStreams = 1000;
    private static readonly TimeSpan CompletedGrace = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan DefaultIdleTtl = TimeSpan.FromHours(2);
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<string, StreamState> _streams = new(StringComparer.Ordinal);

    private readonly TimeSpan _idleTtl = ResolveIdleTtl(configuration);

    public IAsyncEnumerable<StreamFrame> Subscribe(string streamId, long fromSeqExclusive, CancellationToken ct)
    {
        var state = _streams.GetOrAdd(streamId, _ => new StreamState());
        return state.ReadAsync(fromSeqExclusive, ct);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Stream relay starting; subscribing to {Subject}.", SubjectPrefix + ">");
        try
        {
            await Task.WhenAll(PumpAsync(stoppingToken), SweepAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Stream relay shutting down.");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Stream relay encountered a fatal error.");
        }
    }

    private async Task PumpAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await foreach (var msg in connection.SubscribeAsync<AgentStreamUpdate>(SubjectPrefix + ">", serializer: NatsJsonSerializer<AgentStreamUpdate>.Default, cancellationToken: ct))
                {
                    if (msg.Data is null)
                    {
                        continue;
                    }

                    var streamId = msg.Subject.Length > SubjectPrefix.Length ? msg.Subject[SubjectPrefix.Length..] : msg.Subject;
                    var state = _streams.GetOrAdd(streamId, _ => new StreamState());
                    state.Publish(msg.Data);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Stream relay subscription faulted; re-subscribing shortly.");
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
        }
    }

    private static TimeSpan ResolveIdleTtl(IConfiguration configuration)
    {
        var raw = configuration["Orchestration:StreamIdleTtl"];
        if (!string.IsNullOrWhiteSpace(raw))
        {
            if (TimeSpan.TryParse(raw, out var asTimeSpan) && asTimeSpan > TimeSpan.Zero)
            {
                return asTimeSpan;
            }

            if (double.TryParse(raw, out var asMinutes) && asMinutes > 0)
            {
                return TimeSpan.FromMinutes(asMinutes);
            }
        }

        return DefaultIdleTtl;
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(SweepInterval);
        while (await timer.WaitForNextTickAsync(ct))
        {
            var now = DateTime.UtcNow;

            foreach (var (id, state) in _streams)
            {
                if (state.IsEvictable(now, CompletedGrace, _idleTtl))
                {
                    _streams.TryRemove(id, out _);
                }
            }

            var overflow = _streams.Count - MaxTrackedStreams;
            if (overflow > 0)
            {
                foreach (var id in _streams.OrderBy(kvp => kvp.Value.LastActivityUtc).Take(overflow).Select(kvp => kvp.Key))
                {
                    _streams.TryRemove(id, out _);
                }
            }
        }
    }

    private sealed class StreamState
    {
        private readonly Lock _gate = new();
        private readonly LinkedList<StreamFrame> _buffer = new();
        private readonly List<Channel<StreamFrame>> _subscribers = [];
        private long _seq;
        private int _bufferChars;
        private bool _completed;
        private DateTime _completedAtUtc;

        public DateTime LastActivityUtc { get; private set; } = DateTime.UtcNow;

        public void Publish(AgentStreamUpdate update)
        {
            lock (_gate)
            {
                LastActivityUtc = DateTime.UtcNow;
                var frame = new StreamFrame(++_seq, update);

                _buffer.AddLast(frame);
                _bufferChars += update.Text?.Length ?? 0;

                while (_buffer.Count > MaxBufferedEntries || _bufferChars > MaxBufferedChars)
                {
                    _bufferChars -= _buffer.First!.Value.Update.Text?.Length ?? 0;
                    _buffer.RemoveFirst();
                }

                foreach (var sub in _subscribers)
                {
                    sub.Writer.TryWrite(frame);
                }

                if (IsTerminal(update.Type))
                {
                    _completed = true;
                    _completedAtUtc = DateTime.UtcNow;
                    foreach (var sub in _subscribers)
                    {
                        sub.Writer.TryComplete();
                    }

                    _subscribers.Clear();
                }
            }
        }

        public async IAsyncEnumerable<StreamFrame> ReadAsync(long fromSeqExclusive, [EnumeratorCancellation] CancellationToken ct)
        {
            var channel = Channel.CreateBounded<StreamFrame>(new BoundedChannelOptions(MaxSubscriberQueue)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });

            lock (_gate)
            {
                foreach (var frame in _buffer)
                {
                    if (frame.Seq > fromSeqExclusive)
                    {
                        channel.Writer.TryWrite(frame);
                    }
                }

                if (_completed)
                {
                    channel.Writer.TryComplete();
                }
                else
                {
                    _subscribers.Add(channel);
                }
            }

            try
            {
                await foreach (var frame in channel.Reader.ReadAllAsync(ct))
                {
                    yield return frame;
                }
            }
            finally
            {
                lock (_gate)
                {
                    _subscribers.Remove(channel);
                }
            }
        }

        public bool IsEvictable(DateTime now, TimeSpan completedGrace, TimeSpan idleTtl)
        {
            lock (_gate)
            {
                if (_subscribers.Count > 0)
                {
                    return false;
                }

                if (_completed && now - _completedAtUtc > completedGrace)
                {
                    return true;
                }

                return now - LastActivityUtc > idleTtl;
            }
        }

        private static bool IsTerminal(string type) => type is AgentUpdateTypes.Done or AgentUpdateTypes.Error or AgentUpdateTypes.Cancelled;
    }
}
