using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Sentium.Praxis.Common.Streaming;

namespace Sentium.Praxis.Features.Orchestration;

public sealed class StreamRelay(IConfiguration configuration, ILogger<StreamRelay> logger) : BackgroundService, IStreamRelay, IAgentStreamPublisher
{
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

    public void Publish(string streamId, AgentStreamUpdate update)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        ArgumentNullException.ThrowIfNull(update);
        _streams.GetOrAdd(streamId, _ => new StreamState()).Publish(update);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await SweepAsync(stoppingToken);
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
